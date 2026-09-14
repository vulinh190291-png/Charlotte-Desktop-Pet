$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$publishRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/publish'))
$output = Join-Path $publishRoot 'win-x64'
$staging = Join-Path $publishRoot '.win-x64-staging'
$previous = Join-Path $publishRoot '.win-x64-previous'

function Remove-SafePublishDirectory([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $publishRoot.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the publish root: $full"
    }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}

& (Join-Path $PSScriptRoot 'verify.ps1')
if ($LASTEXITCODE -ne 0) { throw "Verification failed with exit code $LASTEXITCODE" }

Remove-SafePublishDirectory $staging
Remove-SafePublishDirectory $previous
try {
    & $dotnet publish (Join-Path $projectRoot 'src/Charlotte.Windows/Charlotte.Windows.csproj') `
        -c Release -r win-x64 --self-contained true --property:PublishSingleFile=false `
        --property:PublishTrimmed=false --output $staging
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }

    & $dotnet run --project (Join-Path $projectRoot 'tools/Charlotte.AssetCheck/Charlotte.AssetCheck.csproj') `
        -c Release --no-build -- (Join-Path $staging 'assets') (Join-Path $staging 'config/animations.json')
    if ($LASTEXITCODE -ne 0) { throw "Published asset check failed with exit code $LASTEXITCODE" }

    if (-not (Test-Path -LiteralPath (Join-Path $staging 'Charlotte.Windows.exe'))) {
        throw 'Published executable is missing.'
    }

    if (Test-Path -LiteralPath $output) { Move-Item -LiteralPath $output -Destination $previous }
    try { Move-Item -LiteralPath $staging -Destination $output }
    catch {
        if ((Test-Path -LiteralPath $previous) -and -not (Test-Path -LiteralPath $output)) {
            Move-Item -LiteralPath $previous -Destination $output
        }
        throw
    }
    Remove-SafePublishDirectory $previous
}
finally {
    Remove-SafePublishDirectory $staging
}

Write-Host "Published Charlotte to $output"
