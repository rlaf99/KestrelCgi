#!/use/bin/env -S dotnet fsi

printfn "Content-Type: text/plain"
printfn ""

let envVars = System.Environment.GetEnvironmentVariables()

for key in envVars.Keys do
    printfn "%s = %s" (key.ToString()) (envVars.[key].ToString())
