$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkRoot = Join-Path $projectRoot '.tools/dotnet'
$sdk = Join-Path $sdkRoot 'dotnet.exe'
if (Test-Path $sdk) { & $sdk --version; exit $LASTEXITCODE }
$archive = Join-Path $projectRoot '.tools/downloads/dotnet-sdk-10.0.401-win-x64.zip'
$expected = '24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430'
New-Item -ItemType Directory -Force (Split-Path $archive -Parent) | Out-Null
if (-not (Test-Path $archive)) {
    & curl.exe -fL --retry 2 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip' -o $archive
    if ($LASTEXITCODE -ne 0) { throw 'SDK download failed' }
}
if ((Get-FileHash $archive -Algorithm SHA512).Hash -ne $expected) { throw 'SDK SHA512 mismatch' }
Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot
& $sdk --version
if ($LASTEXITCODE -ne 0) { throw 'SDK verification failed' }
