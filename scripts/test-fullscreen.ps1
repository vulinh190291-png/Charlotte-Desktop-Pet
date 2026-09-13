$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$pet = Join-Path $projectRoot 'artifacts/publish/win-x64/Charlotte.Windows.exe'
$data = Join-Path $projectRoot 'artifacts/fullscreen-probe/data'
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.tools/nuget-http-cache'

if (-not (Test-Path -LiteralPath $pet)) { throw 'Run scripts/publish.ps1 first.' }
New-Item -ItemType Directory -Path $data -Force | Out-Null

& $dotnet run --project (Join-Path $projectRoot 'tools/Charlotte.FullscreenProbe/Charlotte.FullscreenProbe.csproj') `
    -c Release --no-build -- $pet $data
$exitCode = $LASTEXITCODE
if ($exitCode -eq 3) {
    throw 'Fullscreen probe needs an unlocked interactive desktop. Run this script from a foreground PowerShell window.'
}
if ($exitCode -ne 0) { throw "Fullscreen integration probe failed with exit code $exitCode" }
