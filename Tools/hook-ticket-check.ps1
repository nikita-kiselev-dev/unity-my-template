# Stop-хук тикетов: два независимых гейта на одном хуке.
#   1. ticket-check.ps1  — работа идёт без тикета в статусе In Progress.
#   2. ticket-format.ps1 — тикет не соответствует конвенции Process/Tickets.md.
# Седьмой хук в цепочку не добавляется: оба гейта про тикеты, и разделять их значило бы
# платить ещё одним запуском powershell на каждом ходу.
# Набор находок каждого гейта хэшируется в свой .agent-state/<Имя>/.last-report — один и тот же сигнал
# приходит один раз, иначе гейт превратился бы в шум и его начали бы игнорировать.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

# Возвращает текст отчёта, если гейт красный и этот же отчёт ещё не показывали; иначе $null.
function Invoke-Gate([string]$scriptName, [string]$stampName, [string[]]$scriptArgs) {
    $script = Join-Path $project "Tools\$scriptName"
    if (-not (Test-Path $script)) { return $null }

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script @scriptArgs 2>&1
    $found = $LASTEXITCODE -eq 1

    $stampDir = Join-Path $project ".agent-state\$stampName"
    $stamp = Join-Path $stampDir ".last-report"

    if (-not $found) {
        if (Test-Path $stamp) { Remove-Item $stamp -Force }
        return $null
    }

    $report = ($output | ForEach-Object { $_.ToString() }) -join "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($report)
    $hash = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash($bytes))

    if ((Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $hash) { return $null }

    New-Item -ItemType Directory -Force $stampDir | Out-Null
    Set-Content -Path $stamp -Value $hash -Encoding utf8

    return $report
}

$messages = @()

$missing = Invoke-Gate "ticket-check.ps1" "TicketCheck" @()
if ($missing) { $messages += "ticket-check: changes without an In Progress ticket.`n$missing" }

$format = Invoke-Gate "ticket-format.ps1" "TicketFormat" @()
if ($format) { $messages += "ticket-format: tickets violate Process/Tickets.md.`n$format" }

if (-not $messages) { exit 0 }

# Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
[Console]::Error.WriteLine(($messages -join "`n`n"))
exit 2
