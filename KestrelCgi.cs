using System.Diagnostics;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using static KestrelCgi.CgiRequestConstants;

namespace KestrelCgi;

public record CgiExecutionInfo(
    string ScriptName,
    string PathInfo,
    string CommandPath,
    List<string> CommandArgs
);

public interface ICgiHttpContext
{
    HttpContext HttpContext { get; set; }

    TimeSpan ProcessingTimeout
    {
        get => TimeSpan.FromSeconds(3);
    }

    CgiExecutionInfo GetCgiExecutionInfo(ILogger? logger);
}

/// TODO:
///   - is http content length really necessary?
public class CgiHttpApplication<TContext>(ILogger? logger = null) : IHttpApplication<TContext>
    where TContext : ICgiHttpContext, new()
{
    const string CgiVersion11 = "CGI/1.1";
    const string CgiServerSoftware = "KestrelCgi";

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
            var response = context.HttpContext.Response;

            await Response500Async(response, ex.ToString());
        }

        logger?.LogTrace("Done processing {}", context.HttpContext.Request.Path);
    }

    async Task HandleCgiRequestAsync(TContext context, CancellationToken cancellation)
    {
        var cgiExec = context.GetCgiExecutionInfo(logger);

        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        var arguments = string.Join(" ", cgiExec.CommandArgs);

        logger?.LogTrace("Execute '{}' with arguments '{}'", cgiExec.CommandPath, arguments);

        ProcessStartInfo startInfo = new(cgiExec.CommandPath)
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
            env[SCRIPT_NAME] = cgiExec.ScriptName;
            env[PATH_INFO] = cgiExec.PathInfo;
            env[PATH_TRANSLATED] = null;
            env[QUERY_STRING] = request.QueryString.Value;
            env[CONTENT_TYPE] = request.ContentType;
            env[CONTENT_LENGTH] = request.ContentLength.ToString();

            foreach (var header in request.Headers)
            {
                var name = "HTTP_" + header.Key.ToUpperInvariant().Replace('-', '_');
                env[name] = header.Value;
            }
        }

        using var process = new Process() { StartInfo = startInfo };

        const int errorOutputCapacity = 64;
        StringBuilder errorOutput = new(errorOutputCapacity);

        void ErrorDataReceiver(object sender, DataReceivedEventArgs args)
        {
            if (errorOutput.Length < errorOutputCapacity)
            {
                errorOutput.Append(args.Data);
            }
        }

        process.ErrorDataReceived += ErrorDataReceiver;

        process.Start();
        process.BeginErrorReadLine();

        if (request.ContentLength > 0)
        {
            request.Body.CopyTo(process.StandardInput.BaseStream);
        }
        process.StandardInput.Close();

        CgiHeaders cgiHeaders = new();
        Dictionary<string, string> responseHeaders = [];

        for (; ; )
        {
            var line =
                await process.StandardOutput.ReadLineAsync(cancellation)
                ?? throw new InvalidDataException($"Invalid output from CGI program");

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

        if (logger is not null)
        {
            cgiHeaders.TraceValues(logger);
        }

        if (cgiHeaders.Location is not null)
        {
            throw new InvalidCastException($"{nameof(cgiHeaders.Location)} is not supported");
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

        // process.StandardOutput.BaseStream.CopyTo(response.Body);
        await response.WriteAsync(process.StandardOutput.ReadToEnd() ?? string.Empty, cancellation);

        await process.WaitForExitAsync(cancellation);

        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"CGI program exited with non-zero ({process.ExitCode})"
            );
        }

        if (errorOutput.Length != 0)
        {
            var errorMessage = errorOutput.ToString();
            throw new InvalidDataException($"CGI program produces error output: {errorMessage}");
        }
    }

    (string name, string value) ParseHeader(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex == -1)
        {
            throw new InvalidDataException($"Cannot find ':' in the header");
        }

        var name = line[..colonIndex];
        var value = line[(colonIndex + 1)..];

        return (name, value);
    }
}
