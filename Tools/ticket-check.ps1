# Гейт тикета: изменения вне unity-my-template-docs/Tasks/ требуют тикета в статусе In Progress.
# Изменения только внутри Tasks/ проверку не запускают — иначе создание самого тикета требовало
# бы тикета.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список изменённых путей вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # Корень тикетов; переопределяется в проверках.
    [string]$TasksRoot,
    # База сравнения для списка изменений.
    [string]$BaseRef = "HEAD"
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $TasksRoot) { $TasksRoot = Join-Path $project "unity-my-template-docs\Tasks" }

function Normalize([string]$path) {
    return ($path -replace "\\", "/").Trim()
}

# ErrorActionPreference = Stop + stderr нативного процесса в PS 5.1 = NativeCommandError на ровном месте.
function Invoke-Git([string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { return @(& git -C $project @gitArgs 2>$null) }
    finally { $ErrorActionPreference = $previous }
}

# --- Изменения, требующие тикета ------------------------------------------------------------

if ($Files) {
    $changed = @($Files | ForEach-Object { Normalize $_ })
}
else {
    $changed = @(Invoke-Git @("diff", "--name-only", $BaseRef)) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
    $changed = @($changed | ForEach-Object { Normalize $_ })
}

# Корень тикетов может прийти относительным путём — режем префикс проекта только тогда,
# когда путь действительно внутри него.
$root = (Normalize $project) + "/"
$tasksPrefix = Normalize $TasksRoot
if ($tasksPrefix.StartsWith($root)) { $tasksPrefix = $tasksPrefix.Substring($root.Length) }
$tasksPrefix = $tasksPrefix.TrimEnd("/") + "/"
$relevant = @($changed | Where-Object { $_ -and -not $_.StartsWith(".agent-state/") -and -not $_.StartsWith("Temp/") -and -not $_.StartsWith($tasksPrefix) } | Sort-Object -Unique)
if (-not $relevant) { exit 0 }

# --- Активные тикеты -----------------------------------------------------------------------

if (-not (Test-Path $TasksRoot)) { exit 0 }

$active = @()
foreach ($file in (Get-ChildItem $TasksRoot -Recurse -Filter *.md -File)) {
    foreach ($line in (Get-Content $file.FullName -TotalCount 30)) {
        if ($line -match "^status:\s*[""']?In Progress[""']?\s*$") {
            $active += $file.BaseName
            break
        }
    }
}

if ($active) { exit 0 }

Write-Output "No ticket is In Progress, but $($relevant.Count) file(s) changed outside $tasksPrefix"
$relevant | Select-Object -First 10 | ForEach-Object { Write-Output "  $_" }
if ($relevant.Count -gt 10) { Write-Output "  ... $($relevant.Count - 10) more" }
Write-Output "Create a ticket in $tasksPrefix (Features/ or Bugs/) and set status: In Progress."
exit 1
