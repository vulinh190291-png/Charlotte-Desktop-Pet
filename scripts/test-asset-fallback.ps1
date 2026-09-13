$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishedRoot = Join-Path $projectRoot 'artifacts/publish/win-x64'
$smokeRoot = Join-Path $projectRoot ("artifacts/asset-fallback-smoke/{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$applicationRoot = Join-Path $smokeRoot 'application'
$profileRoot = Join-Path $smokeRoot 'profile'

if (-not (Test-Path -LiteralPath (Join-Path $publishedRoot 'Charlotte.Windows.exe'))) {
    throw 'Run scripts/publish.ps1 first.'
}
New-Item -ItemType Directory -Path $applicationRoot,$profileRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishedRoot '*') -Destination $applicationRoot -Recurse -Force
Set-Content -LiteralPath (Join-Path $applicationRoot 'config/animations.json') -Value '{broken' -Encoding UTF8

$process = $null
try {
    $process = Start-Process -FilePath (Join-Path $applicationRoot 'Charlotte.Windows.exe') `
        -ArgumentList @('--data-dir',$profileRoot,'--diagnostic-shell') `
        -WorkingDirectory $env:TEMP -PassThru
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt 5000) {
        $process.Refresh()
        if ($process.HasExited) { throw "Fallback process exited during startup with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 20
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Fallback window did not appear within five seconds.' }

    $logPath = Join-Path $profileRoot 'logs/charlotte.log'
    $timer.Restart()
    while ($timer.ElapsedMilliseconds -lt 3000 -and -not (Test-Path -LiteralPath $logPath)) {
        Start-Sleep -Milliseconds 20
    }
    if (-not (Test-Path -LiteralPath $logPath)) { throw 'Fallback diagnostic event was not written.' }
    $events = Get-Content -LiteralPath $logPath | ForEach-Object { $_ | ConvertFrom-Json }
    if ('asset-manifest-fallback' -notin $events.eventCode) { throw 'Expected asset-manifest-fallback event was not recorded.' }

    if (-not $process.CloseMainWindow()) { throw 'Windows did not accept the normal close request.' }
    if (-not $process.WaitForExit(5000)) { throw 'Fallback process did not close within five seconds.' }
    [ordered]@{
        ExitCode = $process.ExitCode
        FallbackEvent = $true
        WindowVisible = $true
        SmokeRoot = $smokeRoot
    } | ConvertTo-Json
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
        $process.Dispose()
    }
}
