# KestrelCgi

KestrelCgi implements a simple CGI server directly on top of Kestrel.

## How it works

Derive from the abstract class `CgiHttpContext` and implement `GetCgiExecutionInfo` to provide `CgiExecutionInfo` to `CgiHttpContext`. `CgiExecutionInfo` gives necessary information on how to execute a CGI program.

Here is an example from See [Demo\Program.cs]:

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


## Installation

T.B.D.

### Project Setup

Your `.csproj` needs reference to framework `Microsoft.AspNetCore.App` for Kestrel related stuff (see [Demo\Demo.csproj]):

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```


## Demo

In the root directory, run:

```
cd Demo
dotnet run
```

Then in a Web browser, navigate to `http://localhost:5001/env.fsx` to see the output of the [F# script](./Demo/script/env.fsx).

[Demo\Program.cs]: .\Demo\Program.cs
[Demo\Demo.csproj]: .\Demo\Demo.csproj