# Stop-хук: прогоняет Tools/docs-coverage.ps1 и блокирует ход, если находки новые.
# Набор находок хэшируется в .agent-state/DocsCoverage/.last-report — один и тот же сигнал
# приходит один раз, иначе гейт превратился бы в шум и его начали бы игнорировать.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

[Console]::In.ReadToEnd() | Out-Null

$script = Join-Path $project "Tools\docs-coverage.ps1"
if (-not (Test-Path $script)) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script 2>&1
$found = $LASTEXITCODE -eq 1

$stampDir = Join-Path $project ".agent-state\DocsCoverage"
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
[Console]::Error.WriteLine("docs-coverage: documentation may be stale.`n$report")
exit 2
