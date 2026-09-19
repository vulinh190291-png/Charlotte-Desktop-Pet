$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$publishRoot = Join-Path $projectRoot 'artifacts/publish/win-x64'
$sentinel = Join-Path $publishRoot 'stale.publish.probe'
$smokeRoot = Join-Path $artifactRoot 'release smoke'
$smokeStaging = Join-Path $artifactRoot '.release-smoke-staging'
$smokePrevious = Join-Path $artifactRoot '.release-smoke-previous'
$replacementRoot = Join-Path $artifactRoot 'release asset replacement'
$assetCheckProject = Join-Path $projectRoot 'tools/Charlotte.AssetCheck/Charlotte.AssetCheck.csproj'

function Remove-SafeArtifactDirectory([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the artifact root: $full"
    }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}

function Test-PublishedAssets([string]$Root) {
    Push-Location ([IO.Path]::GetTempPath())
    try {
        & $dotnet run --project $assetCheckProject -c Release --no-build -- `
            (Join-Path $Root 'assets') (Join-Path $Root 'config/animations.json')
        if ($LASTEXITCODE -ne 0) { throw "Asset check failed with exit code $LASTEXITCODE for $Root" }
    }
    finally { Pop-Location }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'Charlotte.Windows.exe'))) {
    throw 'Run scripts/publish.ps1 first.'
}

New-Item -ItemType File -Path $sentinel -Force | Out-Null
& (Join-Path $PSScriptRoot 'publish.ps1')
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE" }
if (Test-Path -LiteralPath $sentinel) { throw 'Publish retained a stale file from the previous output.' }

Remove-SafeArtifactDirectory $smokeStaging
Remove-SafeArtifactDirectory $smokePrevious
Copy-Item -LiteralPath $publishRoot -Destination $smokeStaging -Recurse
Test-PublishedAssets $smokeStaging

if (Test-Path -LiteralPath $smokeRoot) { Move-Item -LiteralPath $smokeRoot -Destination $smokePrevious }
try { Move-Item -LiteralPath $smokeStaging -Destination $smokeRoot }
catch {
    if ((Test-Path -LiteralPath $smokePrevious) -and -not (Test-Path -LiteralPath $smokeRoot)) {
        Move-Item -LiteralPath $smokePrevious -Destination $smokeRoot
    }
    throw
}
Remove-SafeArtifactDirectory $smokePrevious

Remove-SafeArtifactDirectory $replacementRoot
Copy-Item -LiteralPath $smokeRoot -Destination $replacementRoot -Recurse
$replacementFrame = Join-Path $replacementRoot 'assets/character/formal-v1/idle/01.png'
$replacementSource = Join-Path $replacementRoot 'assets/character/formal-v1/idle/02.png'
$publishedFrame = Join-Path $publishRoot 'assets/character/formal-v1/idle/01.png'
$publishedHash = (Get-FileHash -LiteralPath $publishedFrame -Algorithm SHA256).Hash
$beforeHash = (Get-FileHash -LiteralPath $replacementFrame -Algorithm SHA256).Hash
Copy-Item -LiteralPath $replacementSource -Destination $replacementFrame -Force
$afterHash = (Get-FileHash -LiteralPath $replacementFrame -Algorithm SHA256).Hash
if ($beforeHash -eq $afterHash) { throw 'Replacement probe frames are identical; the replacement was not demonstrated.' }
if ((Get-FileHash -LiteralPath $publishedFrame -Algorithm SHA256).Hash -ne $publishedHash) {
    throw 'Asset replacement modified the canonical publish output.'
}
Test-PublishedAssets $replacementRoot

Write-Host 'Fresh publish, space-path copy, alternate-CWD loading, and isolated frame replacement passed.'
