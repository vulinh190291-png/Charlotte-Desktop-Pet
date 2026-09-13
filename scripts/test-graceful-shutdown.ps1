$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectRoot 'artifacts/publish/win-x64/Charlotte.Windows.exe'
$profileRoot = Join-Path $projectRoot ("artifacts/shutdown-smoke/{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

if (-not (Test-Path -LiteralPath $executable)) { throw 'Run scripts/publish.ps1 first.' }
New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null

$process = $null
try {
    $process = Start-Process -FilePath $executable `
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
    if (-not $process.CloseMainWindow()) { throw 'Windows did not accept the normal close request.' }
    if (-not $process.WaitForExit(5000)) { throw 'Charlotte did not finish graceful close within five seconds.' }

    $settingsPath = Join-Path $profileRoot 'settings.json'
    $dataPath = Join-Path $profileRoot 'data.json'
    if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'settings.json was not flushed before exit.' }
    if (-not (Test-Path -LiteralPath $dataPath)) { throw 'data.json was not flushed before exit.' }
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $data = Get-Content -LiteralPath $dataPath -Raw | ConvertFrom-Json
    [ordered]@{
        ExitCode = $process.ExitCode
        SettingsSchemaVersion = $settings.schemaVersion
        DataSchemaVersion = $data.schemaVersion
        XRatio = $settings.xRatio
        ProfileRoot = $profileRoot
    } | ConvertTo-Json
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
        $process.Dispose()
    }
}
