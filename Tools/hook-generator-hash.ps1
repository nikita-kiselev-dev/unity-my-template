# Stop-хук: сверяет исходники AutoDecorators.Generator с хэшем закоммиченной DLL.
# Exit 2 возвращает агента к работе с текстом в stderr; exit 0 — тихо пропускает.
# Один и тот же рассинхрон блокирует ход один раз (.agent-state/GeneratorHash/.last-report) — иначе
# правка генератора, которую пересобирают в конце серии ходов, блокировала бы каждый ход.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$script = Join-Path $PSScriptRoot "generator-hash.ps1"
if (-not (Test-Path $script)) { exit 0 }

$sourceDir = Join-Path $PSScriptRoot "AutoDecorators.Generator"
if (-not (Test-Path $sourceDir)) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script -Check 2>&1
$found = $LASTEXITCODE -ne 0

$stampDir = Join-Path $project ".agent-state\GeneratorHash"
$reportStamp = Join-Path $stampDir ".last-report"

if (-not $found) {
    if (Test-Path $reportStamp) { Remove-Item $reportStamp -Force }
    exit 0
}

$report = ($output | ForEach-Object { $_.ToString() }) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($report)
$hash = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash($bytes))

if ((Test-Path $reportStamp) -and (Get-Content $reportStamp -Raw).Trim() -eq $hash) { exit 0 }

New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $reportStamp -Value $hash -Encoding utf8

# Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
[Console]::Error.WriteLine("generator-hash: committed DLL is out of sync with its sources.`n$report")
exit 2
