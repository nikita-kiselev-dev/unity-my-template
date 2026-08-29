# Запуск Unity-тестов из CLI. Требование: редактор Unity с этим проектом должен быть закрыт.
# Использование:
#   .\Tools\run-tests.ps1                          # все EditMode-тесты
#   .\Tools\run-tests.ps1 -Filter "Framework.Foundation.Tests.ResultTests"
#   .\Tools\run-tests.ps1 -Platform PlayMode
param(
    [string]$Filter = "",
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode"
)

$ErrorActionPreference = "Stop"
$projectPath = Split-Path -Parent $PSScriptRoot

$versionLine = Get-Content (Join-Path $projectPath "ProjectSettings\ProjectVersion.txt") -TotalCount 1
$version = ($versionLine -split ":\s*")[1].Trim()
$unityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) {
    Write-Error "Unity $version не найден по пути $unityExe"
}

$lockFile = Join-Path $projectPath "Temp\UnityLockfile"
if (Test-Path $lockFile) {
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    }
    catch {
        Write-Error "Проект открыт в редакторе Unity — закройте редактор и повторите запуск."
    }
}

$artifacts = Join-Path $projectPath "Temp\TestResults"
New-Item -ItemType Directory -Force $artifacts | Out-Null
$resultsPath = Join-Path $artifacts "results-$Platform.xml"
$logPath = Join-Path $artifacts "log-$Platform.txt"
if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

$unityArgs = @(
    "-batchmode",
    "-projectPath", "`"$projectPath`"",
    "-runTests",
    "-testPlatform", $Platform,
    "-testResults", "`"$resultsPath`"",
    "-logFile", "`"$logPath`""
)
if ($Filter) { $unityArgs += @("-testFilter", "`"$Filter`"") }

Write-Host "Unity $version, $Platform, фильтр: $(if ($Filter) { $Filter } else { '<все>' })"
$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -Wait -NoNewWindow
$exitCode = $proc.ExitCode

if (-not (Test-Path $resultsPath)) {
    Write-Host "--- хвост лога Unity ---"
    Get-Content $logPath -Tail 40
    Write-Error "Файл результатов не создан (exit code $exitCode). Полный лог: $logPath"
}

[xml]$xml = Get-Content $resultsPath
$run = $xml."test-run"
Write-Host ("Итог: {0} | всего {1}, прошло {2}, упало {3}, пропущено {4}" -f `
    $run.result, $run.total, $run.passed, $run.failed, $run.skipped)

if ([int]$run.failed -gt 0) {
    $failed = $xml.SelectNodes("//test-case[@result='Failed']")
    foreach ($tc in $failed) {
        Write-Host ""
        Write-Host "FAILED: $($tc.fullname)"
        $msg = $tc.failure.message.InnerText
        if ($msg) { Write-Host $msg.Trim() }
        $trace = $tc.failure.'stack-trace'.InnerText
        if ($trace) { Write-Host ($trace.Trim() -split "`n" | Select-Object -First 5 | Out-String) }
    }
    exit 2
}

exit 0
