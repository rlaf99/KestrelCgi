using KestrelCgi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var shutDown = new TaskCompletionSource();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole();
    builder.AddDebug();
});

const int port = 5001;
KestrelServerOptions serverOptions = new();
serverOptions.ListenLocalhost(port);

SocketTransportOptions transportOptions = new();

var server = new KestrelServer(
    Options.Create(serverOptions),
    new SocketTransportFactory(Options.Create(transportOptions), loggerFactory),
    loggerFactory
);

var cgiHttpLogger = loggerFactory.CreateLogger<ExampleCgiServer>();
ExampleCgiServer cgiHttp = new(cgiHttpLogger);

Console.Out.WriteLine($"Listening on {port}");
await server.StartAsync(cgiHttp, CancellationToken.None);
await shutDown.Task;
await server.StopAsync(CancellationToken.None);

class ExampleCgiContext : ICgiHttpContext
{
    public required HttpContext HttpContext { get; set; }

    public bool LogErrorOutput { get; set; } = true;

    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromSeconds(3);
}

class ExampleCgiServer(ILogger? logger = null) : CgiHttpApplication<ExampleCgiContext>(logger)
{
    public override ExampleCgiContext CreateContext(IFeatureCollection contextFeatures)
    {
        ExampleCgiContext context = new() { HttpContext = new DefaultHttpContext(contextFeatures) };

        return context;
    }

    public override CgiExecutionInfo? GetCgiExecutionInfo(ExampleCgiContext context)
    {
        var request = context.HttpContext.Request;

        if (request.Path == @"/env.bat")
        {
            CgiExecutionInfo result = new(
                ScriptName: "env.bat",
                PathInfo: "",
                CommandPath: Path.Join(".", "script", "env.bat"),
                CommandArgs: []
            );

            return result;
        }
        else if (request.Path.StartsWithSegments(@"/env.fsx"))
        {
            const string scriptName = "/env.fsx";
            var pathInfo = request.Path.Value![scriptName.Length..];
            var envUpdate = new Dictionary<string, string> { { "FOO", "BAR" } };

            CgiExecutionInfo result = new(
                ScriptName: "env.fsx",
                PathInfo: pathInfo,
                CommandPath: "dotnet",
                CommandArgs: ["fsi", Path.Join(".", "script", "env.fsx")],
                EnvironmentUpdate: envUpdate
            );

            return result;
        }
        else
        {
            return null;
        }
    }
}
