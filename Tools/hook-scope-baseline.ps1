# SessionStart-хук: снимает снапшот грязных путей рабочего дерева до того, как агент начал работать.
# scope-check без него не может отличить предсуществующие изменения от правок агента и блокирует
# даже ход, в котором ничего не менялось.
#
# Снапшот — на сессию, а не на каждый промпт: перезапись между ходами сделала бы правки прошлого
# хода «предсуществующими» и открыла бы дыру в гейте.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

# ErrorActionPreference = Stop + stderr нативного процесса в PS 5.1 = NativeCommandError на ровном месте.
function Invoke-Git([string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { return @(& git -C $project @gitArgs 2>$null) }
    finally { $ErrorActionPreference = $previous }
}

$changed = @(Invoke-Git @("diff", "--name-only", "HEAD")) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
$changed = @($changed | ForEach-Object { ($_ -replace "\\", "/").Trim() } | Where-Object { $_ } | Sort-Object -Unique)

$stampDir = Join-Path $project ".agent-state\ScopeCheck"
New-Item -ItemType Directory -Force $stampDir | Out-Null

# Mtime в строке обязателен: иначе повторная правка предсуществующего файла выпала бы из
# проверки навсегда. Путей, которых нет на диске (удаления), время не спросить — им ставится "-".
$lines = @("# Snapshot taken at session start: $([DateTime]::UtcNow.ToString('o'))")
foreach ($path in $changed) {
    $full = Join-Path $project ($path -replace "/", "\")
    $ticks = if (Test-Path $full) { (Get-Item $full).LastWriteTimeUtc.Ticks } else { "-" }
    $lines += "$ticks|$path"
}

Set-Content -Path (Join-Path $stampDir "baseline.txt") -Value $lines -Encoding utf8
exit 0
