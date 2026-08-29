# Stop-хук: прогоняет Tools/fast-tests.ps1, если с прошлого прогона менялись .cs в Assets/Framework.
# Exit 2 возвращает агента к работе с текстом ошибки в stderr; exit 0 — тихо пропускает.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        # Повторный блок остановки зациклил бы ход, если тесты красные не из-за правок агента.
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$sources = Join-Path $project "Assets\Framework"
if (-not (Test-Path $sources)) { exit 0 }

# Без сгенерированных csproj fast-tests работать не умеет — это не повод блокировать ход.
if (-not (Test-Path (Join-Path $project "Foundation.csproj"))) { exit 0 }

$stampDir = Join-Path $project ".agent-state\FastTests"
$stamp = Join-Path $stampDir ".last-hook-run"
$since = if (Test-Path $stamp) { (Get-Item $stamp).LastWriteTimeUtc } else { [DateTime]::MinValue }

$changed = Get-ChildItem $sources -Recurse -Filter *.cs -File |
    Where-Object { $_.LastWriteTimeUtc -gt $since } |
    Select-Object -First 1
if (-not $changed) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $project "Tools\fast-tests.ps1") 2>&1
$failed = $LASTEXITCODE -ne 0

# Метка обновляется и при падении: сигнал приходит один раз на порцию правок, а не на каждый ход.
New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $stamp -Value "" -Encoding utf8

if ($failed) {
    $tail = ($output | Select-Object -Last 40) -join "`n"
    # Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
    [Console]::Error.WriteLine("fast-tests.ps1 failed - fix before finishing the turn:`n$tail")
    exit 2
}

exit 0
