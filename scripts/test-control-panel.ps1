$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$pet = Join-Path $projectRoot 'artifacts/publish/win-x64/Charlotte.Windows.exe'
$data = Join-Path $projectRoot 'artifacts/control-panel-probe/data'

if (-not (Test-Path -LiteralPath $pet)) { throw 'Run scripts/publish.ps1 first.' }
New-Item -ItemType Directory -Path $data -Force | Out-Null
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class CharlottePanelProbe
{
    public struct WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public int Left, Top, Right, Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    public static WindowInfo[] ForProcess(int expectedProcessId)
    {
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var actualProcessId);
            if (actualProcessId != expectedProcessId || !IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(256);
            GetWindowText(hwnd, title, title.Capacity);
            GetWindowRect(hwnd, out var rect);
            result.Add(new WindowInfo { Handle=hwnd, Title=title.ToString(), Left=rect.Left, Top=rect.Top, Right=rect.Right, Bottom=rect.Bottom });
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }

    public static void RightClick(IntPtr hwnd, int x, int y)
    {
        var packed = new IntPtr((y << 16) | (x & 0xffff));
        PostMessage(hwnd, 0x0204, new IntPtr(2), packed);
        PostMessage(hwnd, 0x0205, IntPtr.Zero, packed);
    }

    public static void Escape(IntPtr hwnd)
    {
        PostMessage(hwnd, 0x0100, new IntPtr(0x1b), IntPtr.Zero);
        PostMessage(hwnd, 0x0101, new IntPtr(0x1b), IntPtr.Zero);
    }

    public static int[] WorkArea(IntPtr hwnd)
    {
        var info = new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info);
        return new[] { info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom };
    }
}
'@

function Wait-MainWindow([Diagnostics.Process]$Process) {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $Process.Refresh()
        if ($Process.HasExited) { throw "Charlotte exited early with code $($Process.ExitCode)." }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { return $Process.MainWindowHandle }
        Start-Sleep -Milliseconds 20
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Charlotte main window did not appear.'
}

function Wait-Panel([Diagnostics.Process]$Process,[bool]$Visible) {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $panel = @([CharlottePanelProbe]::ForProcess($Process.Id)) | Where-Object Title -eq 'Charlotte 管理' | Select-Object -First 1
        if (($null -ne $panel) -eq $Visible) { return $panel }
        Start-Sleep -Milliseconds 20
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Panel visibility did not become $Visible."
}

function Find-AutomationElement([Windows.Automation.AutomationElement]$Root,[string]$Name) {
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$Name)
    $element = $Root.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
    if ($null -eq $element) { throw "Automation element '$Name' was not found." }
    return $element
}

function Is-Selected([Windows.Automation.AutomationElement]$Element) {
    $pattern = [Windows.Automation.SelectionItemPattern]$Element.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)
    return $pattern.Current.IsSelected
}

$process = Start-Process -FilePath $pet -ArgumentList @('--data-dir',$data,'--diagnostic-shell') -PassThru
try {
    $main = Wait-MainWindow $process
    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    Start-Sleep -Milliseconds 100
    $mainWindow = @([CharlottePanelProbe]::ForProcess($process.Id)) | Where-Object Handle -eq $main | Select-Object -First 1
    [CharlottePanelProbe]::RightClick($main,[Math]::Max(1,($mainWindow.Right-$mainWindow.Left)/2),[Math]::Max(1,($mainWindow.Bottom-$mainWindow.Top)/2))
    $panel = Wait-Panel $process $true

    $work = [CharlottePanelProbe]::WorkArea($main)
    if ($panel.Left -lt $work[0] -or $panel.Top -lt $work[1] -or $panel.Right -gt $work[2] -or $panel.Bottom -gt $work[3]) {
        throw 'Control panel is outside the current display work area.'
    }

    $root = [Windows.Automation.AutomationElement]::FromHandle($panel.Handle)
    $daily = Find-AutomationElement $root '每日任务'
    if (-not (Is-Selected $daily)) { throw 'Daily Tasks is not the default panel tab.' }
    $schedule = Find-AutomationElement $root '日程'
    $schedulePattern = [Windows.Automation.SelectionItemPattern]$schedule.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)
    $schedulePattern.Select()
    if (-not (Is-Selected $schedule)) { throw 'Could not select the Schedule tab.' }

    [CharlottePanelProbe]::Escape($panel.Handle)
    $null = Wait-Panel $process $false
    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    [CharlottePanelProbe]::RightClick($main,[Math]::Max(1,($mainWindow.Right-$mainWindow.Left)/2),[Math]::Max(1,($mainWindow.Bottom-$mainWindow.Top)/2))
    $reopened = Wait-Panel $process $true
    $reopenedRoot = [Windows.Automation.AutomationElement]::FromHandle($reopened.Handle)
    if (-not (Is-Selected (Find-AutomationElement $reopenedRoot '日程'))) { throw 'Reopened panel did not preserve the Schedule tab.' }

    Write-Host "Control panel verified: bounds=$($reopened.Left),$($reopened.Top)-$($reopened.Right),$($reopened.Bottom); default tab=每日任务; reopened tab=日程."
}
finally {
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { $process.Kill($true) }
    }
    $process.Dispose()
}
