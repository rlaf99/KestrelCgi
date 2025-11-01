# KestrelCgi

KestrelCgi implements a simple CGI server directly on top of Kestrel.

## How it works

KestrelCgi provides an abstract class `CgiHttpContext`, which can be de

Derive from the abstract class `CgiHttpContext` and implement `GetCgiExecutionInfo` to provide `CgiExecutionInfo` to the base. `CgiExecutionInfo` gives necessary information on how to execute a CGI program.

Here is an example:

```cs
class GitHttpBackendContext : CgiHttpContext
{
    public override CgiExecutionInfo? GetCgiExecutionInfo(ILogger? logger)
    {
        var request = HttpContext.Request;

        if (request.Path.StartsWithSegments(@"/env.fsx"))
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
```