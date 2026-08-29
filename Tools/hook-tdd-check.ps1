# Stop-хук: прогоняет Tools/tdd-check.ps1 и блокирует ход, если TDD-цикл не соблюдён.
# Набор находок хэшируется в .agent-state/TddCheck/.last-report — осознанное отступление, уже
# объяснённое пользователю, не должно блокировать каждый ход подряд.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$script = Join-Path $project "Tools\tdd-check.ps1"
if (-not (Test-Path $script)) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script 2>&1
$found = $LASTEXITCODE -eq 1

$stampDir = Join-Path $project ".agent-state\TddCheck"
$stamp = Join-Path $stampDir ".last-report"

if (-not $found) {
    if (Test-Path $stamp) { Remove-Item $stamp -Force }
    exit 0
}

$report = ($output | ForEach-Object { $_.ToString() }) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($report)
$hash = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash($bytes))

if ((Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $hash) { exit 0 }

New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $stamp -Value $hash -Encoding utf8

# Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
[Console]::Error.WriteLine("tdd-check: the TDD cycle was not followed.`n$report")
exit 2
