param(
    [int]$ColdRuns = 5,
    [int]$WarmupSeconds = 30,
    [int]$IdleSeconds = 300,
    [int]$HiddenSeconds = 60,
    [switch]$EnvironmentOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishRoot = Join-Path $projectRoot 'artifacts/publish/win-x64'
$executable = Join-Path $publishRoot 'Charlotte.Windows.exe'
$profileRoot = Join-Path $projectRoot 'artifacts/performance/profiles'
$resultRoot = Join-Path $projectRoot 'artifacts/performance'

$commit = (& git -c "safe.directory=$projectRoot" -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { throw 'Could not resolve the current Git commit.' }
$windowsKey = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$processorRegistry = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('HARDWARE\DESCRIPTION\System\CentralProcessor\0')
if ($null -eq $processorRegistry) { $processorName = $env:PROCESSOR_IDENTIFIER }
else {
    try { $processorName = [string]$processorRegistry.GetValue('ProcessorNameString',$env:PROCESSOR_IDENTIFIER) }
    finally { $processorRegistry.Dispose() }
}
Add-Type -AssemblyName Microsoft.VisualBasic
if (-not ('CharlottePerformanceNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class CharlottePerformanceNative
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    public static uint[] CursorMonitorDpi()
    {
        try
        {
            if (!GetCursorPos(out var point)) return new uint[] { 0, 0, 0 };
            var monitor = MonitorFromPoint(point, 2);
            if (monitor == IntPtr.Zero || GetDpiForMonitor(monitor, 0, out var x, out var y) != 0)
                return new uint[] { 0, 0, 0 };
            return new uint[] { x, y, 1 };
        }
        catch (DllNotFoundException) { return new uint[] { 0, 0, 0 }; }
        catch (EntryPointNotFoundException) { return new uint[] { 0, 0, 0 }; }
    }
}
'@
}
$memory = [Microsoft.VisualBasic.Devices.ComputerInfo]::new()
$cursorDpi = [CharlottePerformanceNative]::CursorMonitorDpi()
$dpiAvailable = $cursorDpi[2] -eq 1
$systemDpi = (Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction SilentlyContinue).AppliedDPI
$measuredDpiX = if ($dpiAvailable) { $cursorDpi[0] } elseif ($null -ne $systemDpi) { [uint32]$systemDpi } else { $null }
$measuredDpiY = if ($dpiAvailable) { $cursorDpi[1] } elseif ($null -ne $systemDpi) { [uint32]$systemDpi } else { $null }
$machine = [ordered]@{
    Processor = $processorName.Trim()
    LogicalProcessors = [Environment]::ProcessorCount
    TotalPhysicalMemoryGiB = [Math]::Round($memory.TotalPhysicalMemory / 1GB,2)
    AvailablePhysicalMemoryGiB = [Math]::Round($memory.AvailablePhysicalMemory / 1GB,2)
    Windows = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    WindowsDisplayVersion = $windowsKey.DisplayVersion
    WindowsBuild = "$($windowsKey.CurrentBuildNumber).$($windowsKey.UBR)"
    DpiX = $measuredDpiX
    DpiY = $measuredDpiY
    ScalePercent = if ($null -ne $measuredDpiX) { [Math]::Round($measuredDpiX / 96 * 100,2) } else { $null }
    DpiScope = if ($dpiAvailable) { 'effective DPI of the monitor containing the mouse cursor when measurement metadata was captured' } elseif ($null -ne $systemDpi) { 'system DPI registry fallback; cursor monitor DPI was unavailable' } else { 'unavailable' }
}

if ($EnvironmentOnly) {
    [ordered]@{ RecordedAt=(Get-Date).ToString('o'); Commit=$commit; Machine=$machine } | ConvertTo-Json -Depth 5
    return
}

if (-not (Test-Path -LiteralPath $executable)) { throw 'Run scripts/publish.ps1 before measuring performance.' }
New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null

function Start-TestPet([string]$Profile,[bool]$Hidden) {
    New-Item -ItemType Directory -Path $Profile -Force | Out-Null
    $arguments = @('--data-dir',$Profile,'--diagnostic-shell')
    if ($Hidden) { $arguments += '--start-hidden' }
    Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $env:TEMP -PassThru
}

function Wait-FirstWindow([System.Diagnostics.Process]$Process,[int]$TimeoutMilliseconds = 5000) {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        $Process.Refresh()
        if ($Process.HasExited) { throw "Charlotte exited during startup with code $($Process.ExitCode)." }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { return [double]$timer.Elapsed.TotalMilliseconds }
        Start-Sleep -Milliseconds 20
    }
    throw 'Charlotte did not expose its diagnostic window within 5 seconds.'
}

function Stop-TestPet([System.Diagnostics.Process]$Process) {
    $Process.Refresh()
    if ($Process.HasExited) { return }
    $null = $Process.CloseMainWindow()
    if (-not $Process.WaitForExit(3000)) { Stop-Process -Id $Process.Id -ErrorAction SilentlyContinue }
}

function Measure-Process([System.Diagnostics.Process]$Process,[int]$Seconds) {
    $Process.Refresh()
    $cpuStart = $Process.TotalProcessorTime
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    [long]$maxWorkingSet = $Process.WorkingSet64
    [long]$maxPrivate = $Process.PrivateMemorySize64
    while ($timer.Elapsed.TotalSeconds -lt $Seconds) {
        Start-Sleep -Milliseconds 1000
        $Process.Refresh()
        if ($Process.HasExited) { throw 'Charlotte exited during performance sampling.' }
        $maxWorkingSet = [Math]::Max($maxWorkingSet,$Process.WorkingSet64)
        $maxPrivate = [Math]::Max($maxPrivate,$Process.PrivateMemorySize64)
    }
    $Process.Refresh()
    $cpuDelta = $Process.TotalProcessorTime - $cpuStart
    $cpuPercent = ($cpuDelta.TotalMilliseconds / $timer.Elapsed.TotalMilliseconds / [Environment]::ProcessorCount) * 100
    [pscustomobject]@{
        DurationSeconds = [Math]::Round($timer.Elapsed.TotalSeconds,3)
        AverageCpuPercent = [Math]::Round($cpuPercent,4)
        MaxWorkingSetMiB = [Math]::Round($maxWorkingSet / 1MB,2)
        MaxPrivateMemoryMiB = [Math]::Round($maxPrivate / 1MB,2)
    }
}

$cold = @()
for ($index=1;$index -le $ColdRuns;$index++) {
    $profile = Join-Path $profileRoot "cold-$index"
    $process = Start-TestPet $profile $false
    try { $cold += [Math]::Round((Wait-FirstWindow $process),2) }
    finally { Stop-TestPet $process }
}
$orderedCold = @($cold | Sort-Object)
$medianCold = if ($orderedCold.Count % 2 -eq 1) {
    $orderedCold[[int][Math]::Floor($orderedCold.Count/2)]
} else {
    ($orderedCold[$orderedCold.Count/2-1]+$orderedCold[$orderedCold.Count/2])/2
}

$idleProcess = Start-TestPet (Join-Path $profileRoot 'idle') $false
try {
    $null = Wait-FirstWindow $idleProcess
    Start-Sleep -Seconds $WarmupSeconds
    $idle = Measure-Process $idleProcess $IdleSeconds
}
finally { Stop-TestPet $idleProcess }

$hiddenProcess = Start-TestPet (Join-Path $profileRoot 'hidden') $true
try {
    Start-Sleep -Seconds 2
    $hiddenProcess.Refresh()
    if ($hiddenProcess.HasExited) { throw 'Charlotte exited before hidden sampling.' }
    $hidden = Measure-Process $hiddenProcess $HiddenSeconds
}
finally { Stop-TestPet $hiddenProcess }

$result = [ordered]@{
    RecordedAt = (Get-Date).ToString('o')
    Commit = $commit
    AssetStage = 'placeholder'
    Machine = $machine
    ColdStart = [ordered]@{
        RunsMilliseconds = $cold
        MedianMilliseconds = [Math]::Round($medianCold,2)
        MaximumMilliseconds = [Math]::Round(($cold | Measure-Object -Maximum).Maximum,2)
        CacheCondition = 'new process and isolated profile each run; operating-system file cache was not flushed'
        MeetsTwoSecondTarget = (($cold | Measure-Object -Maximum).Maximum -le 2000)
    }
    Idle = $idle
    Hidden = $hidden
    Targets = [ordered]@{
        IdleCpuBelowTwoPercent = ($idle.AverageCpuPercent -lt 2)
        PrivateMemoryBelow250MiB = ($idle.MaxPrivateMemoryMiB -lt 250)
        ColdStartAtMostTwoSeconds = (($cold | Measure-Object -Maximum).Maximum -le 2000)
    }
}

$resultPath = Join-Path $resultRoot ("performance-{0}.json" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding UTF8
$result | ConvertTo-Json -Depth 8
Write-Host "Performance result: $resultPath"
