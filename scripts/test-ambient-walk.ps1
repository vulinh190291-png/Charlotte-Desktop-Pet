$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishedRoot = Join-Path $projectRoot 'artifacts/publish/win-x64'
$smokeRoot = Join-Path $projectRoot ("artifacts/ambient-walk-smoke/{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$applicationRoot = Join-Path $smokeRoot 'application'
$profileRoot = Join-Path $smokeRoot 'profile'

if (-not (Test-Path -LiteralPath (Join-Path $publishedRoot 'Charlotte.Windows.exe'))) {
    throw 'Run scripts/publish.ps1 first.'
}
New-Item -ItemType Directory -Path $applicationRoot,$profileRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishedRoot '*') -Destination $applicationRoot -Recurse -Force

$manifestPath = Join-Path $applicationRoot 'config/animations.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.behavior.walkDelayMinimumMs = 1000
$manifest.behavior.walkDelayMaximumMs = 1000
$manifest.behavior.walkDistanceMinimumDip = 48
$manifest.behavior.walkDistanceMaximumDip = 48
$manifest.behavior.walkSpeedDipPerSecond = 48
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class AmbientWalkWindowProbe {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
}
'@

$process = $null
try {
    $process = Start-Process -FilePath (Join-Path $applicationRoot 'Charlotte.Windows.exe') `
        -ArgumentList @('--data-dir',$profileRoot,'--diagnostic-shell') `
        -WorkingDirectory $env:TEMP -PassThru
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt 5000) {
        $process.Refresh()
        if ($process.HasExited) { throw "Charlotte exited during startup with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 20
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Charlotte window did not appear within five seconds.' }
    $stableSamples = 0
    $previous = $null
    $timer.Restart()
    while ($timer.ElapsedMilliseconds -lt 800 -and $stableSamples -lt 5) {
        $current = New-Object AmbientWalkWindowProbe+Rect
        if ([AmbientWalkWindowProbe]::GetWindowRect($process.MainWindowHandle,[ref]$current)) {
            if ($null -ne $previous -and $current.Left -eq $previous.Left -and $current.Top -eq $previous.Top) {
                $stableSamples++
            } else {
                $stableSamples = 0
            }
            $previous = $current
        }
        Start-Sleep -Milliseconds 20
    }
    if ($stableSamples -lt 5) { throw 'Pet startup position did not stabilize before the walk deadline.' }
    $initial = $previous

    $moved = $false
    $distance = 0
    $timer.Restart()
    while ($timer.ElapsedMilliseconds -lt 3000) {
        Start-Sleep -Milliseconds 25
        $current = New-Object AmbientWalkWindowProbe+Rect
        if (-not [AmbientWalkWindowProbe]::GetWindowRect($process.MainWindowHandle,[ref]$current)) { continue }
        $distance = [Math]::Abs($current.Left-$initial.Left)
        if ($distance -ge 10) { $moved = $true; break }
    }
    if (-not $moved) { throw 'Automatic walk did not move the pet within three seconds.' }
    if (-not $process.CloseMainWindow()) { throw 'Windows did not accept the normal close request.' }
    if (-not $process.WaitForExit(5000)) { throw 'Charlotte did not close within five seconds.' }

    [ordered]@{
        ExitCode = $process.ExitCode
        AutomaticWalkObserved = $true
        HorizontalDistancePx = $distance
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
