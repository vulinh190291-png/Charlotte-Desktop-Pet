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
using System.Threading;

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
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    public static WindowInfo[] ForProcess(int expectedProcessId)
    {
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            uint actualProcessId;
            GetWindowThreadProcessId(hwnd, out actualProcessId);
            if (actualProcessId != expectedProcessId || !IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(256);
            GetWindowText(hwnd, title, title.Capacity);
            Rect rect;
            GetWindowRect(hwnd, out rect);
            result.Add(new WindowInfo { Handle=hwnd, Title=title.ToString(), Left=rect.Left, Top=rect.Top, Right=rect.Right, Bottom=rect.Bottom });
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }

    public static bool CursorAvailable()
    {
        Point point;
        return GetCursorPos(out point);
    }

    public static int[] CursorPosition()
    {
        Point point;
        GetCursorPos(out point);
        return new[] { point.X, point.Y };
    }

    public static void SetCursorPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void RightClick(IntPtr hwnd, int x, int y)
    {
        Rect rect;
        GetWindowRect(hwnd, out rect);
        SetCursorPos(rect.Left+x, rect.Top+y);
        Thread.Sleep(100);
        mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Escape(IntPtr hwnd)
    {
        PostMessage(hwnd, 0x0100, new IntPtr(0x1b), IntPtr.Zero);
        PostMessage(hwnd, 0x0101, new IntPtr(0x1b), IntPtr.Zero);
    }

    public static void Close(IntPtr hwnd)
    {
        PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
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

if (-not [CharlottePanelProbe]::CursorAvailable()) {
    Write-Warning 'Control-panel mouse probe skipped because this runner has no interactive cursor desktop.'
    exit 0
}

$originalCursor = [CharlottePanelProbe]::CursorPosition()
$process = Start-Process -FilePath $pet -ArgumentList @('--data-dir',$data,'--diagnostic-shell') -PassThru
$summary = $null
try {
    $main = Wait-MainWindow $process
    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    Start-Sleep -Milliseconds 100
    $mainWindow = @([CharlottePanelProbe]::ForProcess($process.Id)) | Where-Object Handle -eq $main | Select-Object -First 1
    $clickX = [Math]::Max(1,($mainWindow.Right-$mainWindow.Left)/2)
    $clickY = [Math]::Max(1,($mainWindow.Bottom-$mainWindow.Top)*0.45)
    [CharlottePanelProbe]::RightClick($main,$clickX,$clickY)
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

    [CharlottePanelProbe]::Close($panel.Handle)
    $null = Wait-Panel $process $false
    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    [CharlottePanelProbe]::RightClick($main,$clickX,$clickY)
    $reopened = Wait-Panel $process $true
    $reopenedRoot = [Windows.Automation.AutomationElement]::FromHandle($reopened.Handle)
    if (-not (Is-Selected (Find-AutomationElement $reopenedRoot '日程'))) { throw 'Reopened panel did not preserve the Schedule tab.' }

    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    [CharlottePanelProbe]::RightClick($main,$clickX,$clickY)
    $null = Wait-Panel $process $false
    [CharlottePanelProbe]::RightClick($main,$clickX,$clickY)
    $reopened = Wait-Panel $process $true

    [CharlottePanelProbe]::Escape($reopened.Handle)
    $null = Wait-Panel $process $false

    $null = [CharlottePanelProbe]::SetForegroundWindow($main)
    [CharlottePanelProbe]::RightClick($main,$clickX,$clickY)
    $null = Wait-Panel $process $true

    $summary = "Control panel verified: bounds=$($reopened.Left),$($reopened.Top)-$($reopened.Right),$($reopened.Bottom); close/reopen, repeated right-click, Escape, tab persistence, and shutdown with the panel open passed."
}
finally {
    [CharlottePanelProbe]::SetCursorPosition($originalCursor[0],$originalCursor[1])
    $shutdownError = $null
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            $process.Kill($true)
            $process.WaitForExit()
            $shutdownError = 'Charlotte did not exit gracefully while the panel was open.'
        }
    }
    if ($null -eq $shutdownError -and $process.ExitCode -ne 0) { $shutdownError = "Charlotte exited with code $($process.ExitCode)." }
    $process.Dispose()
    if ($null -ne $shutdownError) { throw $shutdownError }
}

Write-Host $summary
