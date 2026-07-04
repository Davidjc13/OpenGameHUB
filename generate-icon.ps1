$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$png = Join-Path $root "Assets\app.png"
$ico = Join-Path $root "Assets\app.ico"

dotnet run --project (Join-Path $root "tools\GenerateIcon\GenerateIcon.csproj") -- $png $ico
