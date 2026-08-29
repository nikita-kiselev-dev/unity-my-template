# Stop-хук: прогоняет Tools/naming-check.ps1 по изменениям рабочего дерева и блокирует ход,
# если находки новые. Набор находок хэшируется в .agent-state/NamingCheck/.last-report — спорное имя,
# оставленное осознанно, не должно блокировать каждый ход подряд.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$script = Join-Path $project "Tools\naming-check.ps1"
if (-not (Test-Path $script)) { exit 0 }

$sources = Join-Path $project "Assets\Framework"
if (-not (Test-Path $sources)) { exit 0 }

$stampDir = Join-Path $project ".agent-state\NamingCheck"
$runStamp = Join-Path $stampDir ".last-hook-run"
$reportStamp = Join-Path $stampDir ".last-report"

$since = if (Test-Path $runStamp) { (Get-Item $runStamp).LastWriteTimeUtc } else { [DateTime]::MinValue }

# .meta попадает в список ради правил по ассетам: он переезжает вместе с любым переименованным
# ассетом, а сам ассет при переименовании не меняется и по дате записи не виден.
$watched = @(Get-ChildItem $sources -Recurse -Include *.cs, *.asset, *.meta -File)
$groups = Join-Path $project "Assets\AddressableAssetsData\AssetGroups"
if (Test-Path $groups) { $watched += @(Get-ChildItem $groups -Filter *.asset -File) }

$changed = $watched | Where-Object { $_.LastWriteTimeUtc -gt $since } | Select-Object -First 1
if (-not $changed) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script 2>&1
$found = $LASTEXITCODE -eq 1

New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $runStamp -Value "" -Encoding utf8

if (-not $found) {
    if (Test-Path $reportStamp) { Remove-Item $reportStamp -Force }
    exit 0
}

$report = ($output | ForEach-Object { $_.ToString() }) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($report)
$hash = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash($bytes))

if ((Test-Path $reportStamp) -and (Get-Content $reportStamp -Raw).Trim() -eq $hash) { exit 0 }

Set-Content -Path $reportStamp -Value $hash -Encoding utf8

# Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
[Console]::Error.WriteLine("naming-check: names violate Architecture/Naming.md.`n$report")
exit 2
