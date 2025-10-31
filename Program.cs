using System.Diagnostics.CodeAnalysis;
using KestrelCgi;
using Microsoft.AspNetCore.Http;
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

KestrelServerOptions serverOptions = new();
serverOptions.ListenLocalhost(5001);

SocketTransportOptions transportOptions = new();

var server = new KestrelServer(
    Options.Create(serverOptions),
    new SocketTransportFactory(Options.Create(transportOptions), loggerFactory),
    loggerFactory
);

var cgiHttpLogger = loggerFactory.CreateLogger<CgiHttpApplication<GitHttpBackendContext>>();
CgiHttpApplication<GitHttpBackendContext> cgiHttp = new(cgiHttpLogger);

await server.StartAsync(cgiHttp, CancellationToken.None);

await shutDown.Task;
await server.StopAsync(CancellationToken.None);

class GitHttpBackendContext : ICgiHttpContext
{
    [AllowNull]
    public HttpContext HttpContext { get; set; }

    public CgiExecutionInfo GetCgiExecutionInfo(ILogger? logger)
    {
        var request = HttpContext.Request;

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
        else if (request.Path == @"/hello.exe")
        {
            CgiExecutionInfo result = new(
                ScriptName: "env.bat",
                PathInfo: "",
                CommandPath: Path.Join(".", "script", "hello.exe"),
                CommandArgs: []
            );

            return result;
        }
        else if (request.Path == @"/env.fsx")
        {
            CgiExecutionInfo result = new(
                ScriptName: "env.fsx",
                PathInfo: "",
                CommandPath: "dotnet",
                CommandArgs: ["fsi", Path.Join(".", "script", "env.fsx")]
            );

            return result;
        }
        else
        {
            throw new InvalidOperationException($"Unsupport request path: {request.Path}");
        }
    }
}
