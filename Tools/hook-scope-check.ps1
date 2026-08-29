# Stop-хук: прогоняет Tools/scope-check.ps1 и блокирует ход, если тронуто незаявленное.
# Набор находок хэшируется в .agent-state/ScopeCheck/.last-report — сознательно расширенный скоуп,
# уже объяснённый пользователю, не должен блокировать каждый ход подряд.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$script = Join-Path $project "Tools\scope-check.ps1"
if (-not (Test-Path $script)) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script 2>&1
$found = $LASTEXITCODE -eq 1

$stampDir = Join-Path $project ".agent-state\ScopeCheck"
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
[Console]::Error.WriteLine("scope-check: work went outside the declared scope.`n$report")
exit 2
