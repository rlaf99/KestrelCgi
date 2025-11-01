using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using static KestrelCgi.CgiRequestConstants;

namespace KestrelCgi;

public class InvalidCgiOutputException : Exception
{
    public InvalidCgiOutputException(string? message)
        : base(message) { }
}

public record CgiExecutionInfo(
    string ScriptName,
    string PathInfo,
    string CommandPath,
    List<string> CommandArgs,
    Dictionary<string, string>? EnvironmentUpdate = null
);

public interface ICgiHttpContext
{
    HttpContext HttpContext { get; set; }

    bool LogErrorOutput
    {
        get => false;
    }

    TimeSpan ProcessingTimeout
    {
        get => TimeSpan.FromSeconds(3);
    }

    CgiExecutionInfo? GetCgiExecutionInfo(ILogger? logger);
}

public class CgiHttpApplication<TContext>(ILogger? logger = null) : IHttpApplication<TContext>
    where TContext : ICgiHttpContext, new()
{
    const string CgiVersion11 = "CGI/1.1";
    static readonly string CgiServerSoftware =
        typeof(ICgiHttpContext).Assembly.GetName().Name ?? "Unknown";

    public TContext CreateContext(IFeatureCollection contextFeatures)
    {
        var context = new TContext() { HttpContext = new DefaultHttpContext(contextFeatures) };

        return context;
    }

    public void DisposeContext(TContext context, Exception? exception)
    {
        if (context is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async Task Response500Async(HttpResponse response, string? additionalInfo = null)
    {
        response.StatusCode = StatusCodes.Status500InternalServerError;

        var message = additionalInfo ?? string.Empty;

        var content = $"""
            <body>
                <h1>Status: {StatusCodes.Status500InternalServerError}</h1>
                <h2>Error</h2>
                <pre>{message}</pre>
            </body>
            """;

        await response.WriteAsync(content);
    }

    public async Task Response404Async(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status404NotFound;

        var content = $"""
            <body>
                <h1>Status: {StatusCodes.Status404NotFound}</h1>
                <h2>Not Found</h2>
            </body>
            """;

        await response.WriteAsync(content);
    }

    public async Task ProcessRequestAsync(TContext context)
    {
        logger?.LogTrace("Start processing {}", context.HttpContext.Request.Path);

        try
        {
            using var cts = new CancellationTokenSource(context.ProcessingTimeout);

            await HandleCgiRequestAsync(context, cts.Token);

            cts.Cancel();
        }
        catch (Exception ex)
        {
            if (context.HttpContext.RequestAborted.IsCancellationRequested == false)
            {
                var response = context.HttpContext.Response;

                await Response500Async(response, ex.ToString());
            }
        }

        logger?.LogTrace("Done processing {}", context.HttpContext.Request.Path);
    }

    async Task HandleCgiRequestAsync(TContext context, CancellationToken timeoutToken)
    {
        var cgiExecInfo = context.GetCgiExecutionInfo(logger);
        if (cgiExecInfo is null)
        {
            await Response404Async(context.HttpContext.Response);

            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            context.HttpContext.RequestAborted,
            timeoutToken
        );

        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        var arguments = string.Join(" ", cgiExecInfo.CommandArgs);

        logger?.LogTrace("Execute '{}' with arguments '{}'", cgiExecInfo.CommandPath, arguments);

        ProcessStartInfo startInfo = new(cgiExecInfo.CommandPath)
        {
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        {
            var env = startInfo.Environment;

            env[GATEWAY_INTERFACE] = CgiVersion11;
            env[SERVER_PROTOCOL] = request.Protocol;
            env[SERVER_SOFTWARE] = CgiServerSoftware;
            env[SERVER_NAME] = request.Host.Host;
            env[SERVER_PORT] = request.Host.Port.ToString();
            env[REMOTE_ADDR] = context.HttpContext.Connection.RemoteIpAddress?.ToString();
            env[REMOTE_HOST] = null;
            env[REMOTE_IDENT] = null;
            env[REMOTE_USER] = context.HttpContext.User.Identity?.Name;
            env[AUTH_TYPE] = context.HttpContext.User.Identity?.AuthenticationType;
            env[REQUEST_METHOD] = request.Method;
            env[SCRIPT_NAME] = cgiExecInfo.ScriptName;
            env[PATH_INFO] = cgiExecInfo.PathInfo;
            env[PATH_TRANSLATED] = null;
            env[QUERY_STRING] = request.QueryString.Value;
            env[CONTENT_TYPE] = request.ContentType;
            env[CONTENT_LENGTH] = request.ContentLength.ToString();

            foreach (var header in request.Headers)
            {
                var name = "HTTP_" + header.Key.ToUpperInvariant().Replace('-', '_');
                env[name] = header.Value;
            }

            if (cgiExecInfo.EnvironmentUpdate is not null)
            {
                foreach (var (key, val) in cgiExecInfo.EnvironmentUpdate)
                {
                    env[key] = val;
                }
            }
        }

        using var process = new Process() { StartInfo = startInfo };

        void ErrorDataReceiver(object sender, DataReceivedEventArgs args)
        {
            if (context.LogErrorOutput)
            {
                logger?.LogError("CGI error outupt: {}", args.Data);
            }
        }

        process.ErrorDataReceived += ErrorDataReceiver;

        process.Start();
        process.BeginErrorReadLine();

        await request.Body.CopyToAsync(process.StandardInput.BaseStream, cts.Token);
        process.StandardInput.Close();

        CgiHeaders cgiHeaders = new();
        Dictionary<string, string> responseHeaders = [];

        for (; ; )
        {
            var line =
                await process.StandardOutput.ReadLineAsync(cts.Token)
                ?? throw new InvalidCgiOutputException($"Headers not followed by new line");

            if (line.Length == 0)
            {
                break;
            }

            if (cgiHeaders.TakeLine(line) == false)
            {
                var (name, value) = ParseHeader(line);
                responseHeaders.Add(name, value);

                logger?.LogTrace("HTTP header from CGI response: '{}' : '{}'", name, value);
            }
        }

        cgiHeaders.TraceValues(logger);

        if (cgiHeaders.Location is not null)
        {
            throw new NotImplementedException($"{nameof(cgiHeaders.Location)} is not supported");
        }

        if (cgiHeaders.Status is not null)
        {
            response.StatusCode = cgiHeaders.Status.Value;
        }
        else
        {
            response.StatusCode = StatusCodes.Status200OK;
        }

        foreach (var (name, value) in responseHeaders)
        {
            response.Headers.Append(name, value);
        }

        if (cgiHeaders.ContentType is not null)
        {
            response.Headers.ContentType = cgiHeaders.ContentType;
        }

        await response.StartAsync(cts.Token);

        await process.StandardOutput.BaseStream.CopyToAsync(response.Body, cts.Token);

        await process.WaitForExitAsync(cts.Token);

        if (process.ExitCode != 0)
        {
            throw new InvalidCgiOutputException(
                $"CGI program exited with non-zero ({process.ExitCode})"
            );
        }
    }

    (string name, string value) ParseHeader(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1)
        {
            throw new InvalidCgiOutputException($"Cannot find ':' in the header");
        }

        var name = line[..colonIndex];
        var value = line[(colonIndex + 1)..];

        return (name, value);
    }
}
