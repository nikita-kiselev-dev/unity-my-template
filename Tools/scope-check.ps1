# Гейт заявленного скопа: сверяет фактический диф рабочего дерева со списком файлов, который
# агент объявил до правок (.agent-state/ScopeCheck/declared.txt — по строке на путь, допустимы
# #-комментарии и *-маски). Молчаливое расширение скопа не лечится формулировкой в инструкциях,
# поэтому проверяется машинно.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список изменённых путей вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # Файл с заявленным скопом; переопределяется в проверках.
    [string]$Declared,
    # Снапшот грязного дерева на старте сессии; переопределяется в проверках.
    [string]$Baseline,
    # База сравнения для списка изменений.
    [string]$BaseRef = "HEAD"
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $Declared) { $Declared = Join-Path $project ".agent-state\ScopeCheck\declared.txt" }
if (-not $Baseline) { $Baseline = Join-Path $project ".agent-state\ScopeCheck\baseline.txt" }

function Normalize([string]$path) {
    return ($path -replace "\\", "/").Trim()
}

# Файл заявки может прийти относительным путём — для вывода режем корень проекта только тогда,
# когда путь действительно внутри него.
function Relative([string]$path) {
    $normalized = Normalize $path
    $root = (Normalize $project) + "/"
    if ($normalized.StartsWith($root)) { return $normalized.Substring($root.Length) }
    return $normalized
}

# ErrorActionPreference = Stop + stderr нативного процесса в PS 5.1 = NativeCommandError на ровном месте.
function Invoke-Git([string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { return @(& git -C $project @gitArgs 2>$null) }
    finally { $ErrorActionPreference = $previous }
}

# --- Фактические изменения -----------------------------------------------------------------

if ($Files) {
    $changed = @($Files | ForEach-Object { Normalize $_ })
}
else {
    $changed = @(Invoke-Git @("diff", "--name-only", $BaseRef)) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
    $changed = @($changed | ForEach-Object { Normalize $_ })
}

# Пути, которые пишет не агент:
#   .agent-state/ — состояние гейтов и сам файл заявки; в git не попадает, заявлять нечего;
#   Temp/ — артефакты сборки (outDir компиляции, вывод генератора), их чистит Unity;
#   .obsidian/ — состояние UI редактора; vault открыт у пользователя, и файл меняется от
#   переключения панелей. По времени правки это неотличимо от работы агента, поэтому
#   отсекается по пути.
$ignored = @(".agent-state/", "Temp/", "unity-my-template-docs/.obsidian/")
$changed = @($changed | Where-Object {
        $path = $_
        $path -and -not ($ignored | Where-Object { $path.StartsWith($_) })
    } | Sort-Object -Unique)
if (-not $changed) { exit 0 }

# --- Снапшот старта сессии -----------------------------------------------------------------

# Рабочее дерево бывает грязным до того, как агент начал работать: недоудалённый .meta,
# untracked-папка, правки прошлой сессии. Без точки отсчёта они читаются как работа вне скоупа,
# и ход блокируется даже когда агент не менял ничего. Снапшот пишет SessionStart-хук
# (Tools/hook-scope-baseline.ps1); нет снапшота — проверка остаётся строгой, а не тихой.
# При явном -Files фильтровать нечем: список путей задан снаружи.
if (-not $Files -and (Test-Path $Baseline)) {
    $preexisting = @{}
    foreach ($line in (Get-Content $Baseline)) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith("#")) { continue }
        $parts = $value.Split("|", 2)
        if ($parts.Count -eq 2) { $preexisting[(Normalize $parts[1])] = $parts[0] }
    }

    # Отбрасывается только путь, который с момента снапшота не трогали: сверка по mtime, иначе
    # повторная правка предсуществующего файла выпала бы из проверки навсегда.
    $changed = @($changed | Where-Object {
            if (-not $preexisting.ContainsKey($_)) { return $true }
            $full = Join-Path $project ($_ -replace "/", "\")
            $ticks = if (Test-Path $full) { (Get-Item $full).LastWriteTimeUtc.Ticks.ToString() } else { "-" }
            return $ticks -ne $preexisting[$_]
        })
    if (-not $changed) { exit 0 }
}

# --- Заявленный скоуп ----------------------------------------------------------------------

if (-not (Test-Path $Declared)) {
    Write-Output "Scope was not declared: $(Relative $Declared) is missing."
    Write-Output "Changed files ($($changed.Count)):"
    $changed | Select-Object -First 10 | ForEach-Object { Write-Output "  $_" }
    if ($changed.Count -gt 10) { Write-Output "  ... $($changed.Count - 10) more" }
    exit 1
}

# Рабочее дерево может держать незакоммиченные правки прошлых сессий, поэтому база сравнения
# не HEAD, а момент заявки: скоуп текущей работы — файлы, тронутые после неё. Пути, которых
# на диске больше нет (удаления), из сверки выпадают: время правки у них не спросить, а
# удаления прошлых сессий иначе горели бы в каждом ходу.
if (-not $Files) {
    $since = (Get-Item $Declared).LastWriteTimeUtc
    $changed = @($changed | Where-Object {
            $full = Join-Path $project ($_ -replace "/", "\")
            (Test-Path $full) -and (Get-Item $full).LastWriteTimeUtc -gt $since
        })
    if (-not $changed) { exit 0 }
}

$patterns = @()
foreach ($line in (Get-Content $Declared)) {
    $value = $line.Trim()
    if (-not $value -or $value.StartsWith("#")) { continue }
    $patterns += (Normalize $value)
}

if (-not $patterns) {
    Write-Output "Scope declaration is empty: $(Relative $Declared)."
    Write-Output "Changed files ($($changed.Count)):"
    $changed | ForEach-Object { Write-Output "  $_" }
    exit 1
}

function Test-Declared([string]$path) {
    foreach ($pattern in $patterns) {
        # Путь, заявленный папкой, покрывает всё внутри — так заявляют пакетные правки.
        if ($pattern.EndsWith("/") -and $path.StartsWith($pattern)) { return $true }
        if ($path -like $pattern) { return $true }
    }
    return $false
}

$unlisted = @($changed | Where-Object { -not (Test-Declared $_) })
if (-not $unlisted) { exit 0 }

Write-Output "Files changed outside the declared scope ($($unlisted.Count)):"
$unlisted | ForEach-Object { Write-Output "  $_" }
Write-Output "Declare them with a reason or revert them."
exit 1
