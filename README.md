# KestrelCgi

KestrelCgi implements a simple CGI server directly on top of Kestrel.

## How it works

An example is provided in [Demo/Program.cs]:

First, implement `ICgiHttpContent`:

```cs
class ExampleCgiContext : ICgiHttpContext
{
    public required HttpContext HttpContext { get; set; }

    public bool LogErrorOutput { get; set; } = true;

    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
```

Then, derive from the abstract class `CgiHttpApplication` and implement `GetCgiExecutionInfo` to provide `CgiExecutionInfo` which gives necessary information on how to execute a CGI program:


```cs
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

Now it can be run on a `KestrelServer`:

```cs
KestrelServer server = new(/*...*/);
server.StartAsync(new ExampleCgiServer(), CancellationToken.None);
```

## See it in action

In the root directory, run:

```
cd Demo
dotnet run
```

Then in a Web browser, navigate to `http://localhost:5001/env.fsx` to see the output of the [F# script](./Demo/script/env.fsx).

## Installation

Install the nuget package:

```
dotnet package add KestrelCgi
```

For local instanll, a `--source` switch can be specified to indicate the directory where the nuget package resides.

### Project Setup

Your `.csproj` needs reference to framework `Microsoft.AspNetCore.App` for Kestrel related stuff (see [Demo/Demo.csproj]):

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```


## How to package the nuget

In the root directory, run:

```
dotnet pack Lib -o nupkg
```

[Demo/Program.cs]: ./Demo/Program.cs
[Demo/Demo.csproj]: ./Demo/Demo.csproj