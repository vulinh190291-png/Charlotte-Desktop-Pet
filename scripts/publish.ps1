$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$output = Join-Path $projectRoot 'artifacts/publish/win-x64'

& (Join-Path $PSScriptRoot 'verify.ps1')
if ($LASTEXITCODE -ne 0) { throw "Verification failed with exit code $LASTEXITCODE" }

& $dotnet publish (Join-Path $projectRoot 'src/Charlotte.Windows/Charlotte.Windows.csproj') `
    -c Release -r win-x64 --self-contained true --property:PublishSingleFile=false `
    --property:PublishTrimmed=false --output $output
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

& $dotnet run --project (Join-Path $projectRoot 'tools/Charlotte.AssetCheck/Charlotte.AssetCheck.csproj') `
    -c Release --no-build -- (Join-Path $output 'assets') (Join-Path $output 'config/animations.json')
if ($LASTEXITCODE -ne 0) { throw "Published asset check failed with exit code $LASTEXITCODE" }

if (-not (Test-Path (Join-Path $output 'Charlotte.Windows.exe'))) { throw 'Published executable is missing.' }
Write-Host "Published Charlotte to $output"
