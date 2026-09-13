$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.tools/nuget-http-cache'
$results = Join-Path $projectRoot 'artifacts/test-results'

function Invoke-Checked([string[]]$Arguments) {
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE" }
}

Push-Location $projectRoot
try {
    Invoke-Checked @('restore','Charlotte.sln','--locked-mode')
    Invoke-Checked @('test','Charlotte.sln','-c','Release','--no-restore','--logger','trx','--results-directory',$results)
    Invoke-Checked @('build','Charlotte.sln','-c','Release','--no-restore')
    Invoke-Checked @('run','--project','tools/Charlotte.AssetCheck/Charlotte.AssetCheck.csproj','-c','Release','--no-build','--','assets','config/animations.json')
    Write-Host 'Charlotte verification passed.'
}
finally { Pop-Location }
