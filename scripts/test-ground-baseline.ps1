$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectRoot 'artifacts/publish/win-x64/Charlotte.Windows.exe'
$profileRoot = Join-Path $projectRoot ("artifacts/ground-baseline-smoke/{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

if (-not (Test-Path -LiteralPath $executable)) { throw 'Run scripts/publish.ps1 first.' }
New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class CharlotteGroundProbe
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);

    public static int[] Measure(IntPtr hwnd)
    {
        Rect window;
        if (!GetWindowRect(hwnd, out window)) throw new InvalidOperationException("Could not read the pet window bounds.");
        var info = new MonitorInfo { Size=Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info)) throw new InvalidOperationException("Could not read the monitor work area.");
        return new[] { window.Top, info.Work.Bottom, (int)Math.Max(96,GetDpiForWindow(hwnd)) };
    }
}
'@

$process = $null
try {
    $process = Start-Process -FilePath $executable `
        -ArgumentList @('--data-dir',$profileRoot,'--diagnostic-shell') `
        -WorkingDirectory $env:TEMP -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw "Charlotte exited during startup with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 20
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Charlotte window did not appear within five seconds.' }

    Start-Sleep -Milliseconds 250
    $measurement = [CharlotteGroundProbe]::Measure($process.MainWindowHandle)
    $visibleIdleFootOffsetDip = 270.0
    # GetWindowRect and MONITORINFO are virtualized into the probe process coordinate space together.
    $visibleFootBottom = $measurement[0] + $visibleIdleFootOffsetDip
    $difference = $visibleFootBottom - $measurement[1]
    if ([Math]::Abs($difference) -gt 1.0) {
        throw "Visible idle feet miss the taskbar boundary by $([Math]::Round($difference,2)) virtualized pixels (windowTop=$($measurement[0]), workBottom=$($measurement[1]), dpi=$($measurement[2]))."
    }

    Write-Host "Ground baseline verified: visibleFootBottom=$visibleFootBottom; workAreaBottom=$($measurement[1]); dpi=$($measurement[2])."
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(5000)) { $process.Kill($true) }
        }
        $process.Dispose()
    }
}
