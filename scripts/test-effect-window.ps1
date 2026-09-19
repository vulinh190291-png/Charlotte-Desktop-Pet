$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$pet = Join-Path $projectRoot 'artifacts/publish/win-x64/Charlotte.Windows.exe'
$data = Join-Path $projectRoot 'artifacts/effect-window-probe/data'

if (-not (Test-Path -LiteralPath $pet)) { throw 'Run scripts/publish.ps1 first.' }
New-Item -ItemType Directory -Path $data -Force | Out-Null

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class CharlotteWindowProbe
{
    public struct WindowInfo
    {
        public IntPtr Handle;
        public long ExtendedStyle;
        public int Width;
        public int Height;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    public static WindowInfo[] ForProcess(int expectedProcessId)
    {
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var actualProcessId);
            if (actualProcessId != expectedProcessId || !IsWindowVisible(hwnd)) return true;
            GetWindowRect(hwnd, out var rect);
            result.Add(new WindowInfo
            {
                Handle = hwnd,
                ExtendedStyle = GetWindowLongPtr(hwnd, -20).ToInt64(),
                Width = rect.Right - rect.Left,
                Height = rect.Bottom - rect.Top
            });
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }
}
'@

$petProcess = Start-Process -FilePath $pet -ArgumentList @(
    '--data-dir', $data,
    '--diagnostic-shell',
    '--start-action', 'Battle'
) -PassThru

try {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    $windows = @()
    $main = $null
    $effect = $null
    $wsExTransparent = 0x20
    $wsExNoActivate = 0x08000000
    do {
        if ($petProcess.HasExited) { throw "Charlotte exited early with code $($petProcess.ExitCode)." }
        $windows = @( [CharlotteWindowProbe]::ForProcess($petProcess.Id) )
        $mainHandle = $petProcess.MainWindowHandle
        $main = $windows | Where-Object Handle -eq $mainHandle | Select-Object -First 1
        if ($null -ne $main) {
            $candidateEffect = $windows | Where-Object {
                $_.Handle -ne $mainHandle -and
                ($_.ExtendedStyle -band $wsExTransparent) -ne 0 -and
                ($_.ExtendedStyle -band $wsExNoActivate) -ne 0 -and
                $_.Width -gt $main.Width -and $_.Height -gt $main.Height
            } | Select-Object -First 1
            if ($null -ne $candidateEffect) {
                $effect = $candidateEffect
                break
            }
        }
        Start-Sleep -Milliseconds 20
    } while ([DateTime]::UtcNow -lt $deadline)

    foreach ($candidate in $windows) {
        Write-Host "Observed window: handle=$($candidate.Handle), size=$($candidate.Width)x$($candidate.Height), style=0x$($candidate.ExtendedStyle.ToString('X'))."
    }
    if ($null -eq $main) { throw 'The Charlotte main window was not observed.' }
    if ($null -ne $effect) { throw 'Formal Battle unexpectedly displayed a duplicate overlay effect window.' }

    Write-Host "Baked Battle effects verified: pet=$($main.Width)x$($main.Height), no duplicate overlay window became visible."
}
finally {
    if (-not $petProcess.HasExited) {
        $null = $petProcess.CloseMainWindow()
        if (-not $petProcess.WaitForExit(5000)) { $petProcess.Kill($true) }
    }
    $petProcess.Dispose()
}
