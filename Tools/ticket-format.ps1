# Машинная проверка конвенции unity-my-template-docs/Process/Tickets.md.
# Скрипт только сообщает — ничего не правит и не форматирует.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список путей (относительно корня проекта) вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # База сравнения для списка изменений рабочего дерева.
    [string]$BaseRef = "HEAD",
    # Полный проход по всем тикетам вместо изменений рабочего дерева.
    [switch]$All,
    # Печатает словарь module статей и выходит: словарь не выдумывается, а выводится из vault.
    [switch]$ListModules,
    # Корень тикетов и корень vault; переопределяются в проверках.
    [string]$TasksRoot,
    [string]$DocsRoot
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $DocsRoot) { $DocsRoot = Join-Path $project "unity-my-template-docs" }
if (-not $TasksRoot) { $TasksRoot = Join-Path $DocsRoot "Tasks" }

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

# --- Разбор frontmatter ----------------------------------------------------------------------
# Полноценный YAML тут не нужен и вреден: тикет — плоская карта скаляров и списков строк,
# а тянуть парсер в скрипт, который запускается на каждом ходу, дороже, чем описать этот подвид.

function Read-Frontmatter([string[]]$lines) {
    $result = @{ Fields = @{}; Ok = $false; BodyStart = 0 }
    if (-not $lines -or $lines[0].Trim() -ne "---") { return $result }

    $key = $null
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.Trim() -eq "---") {
            $result.Ok = $true
            $result.BodyStart = $i + 1
            break
        }

        if ($line -match "^(?<key>[A-Za-z_][\w-]*):\s*(?<value>.*)$") {
            $key = $Matches["key"]
            $value = $Matches["value"].Trim()
            if ($value) { $result.Fields[$key] = @($value) }
            else { $result.Fields[$key] = @() }
        }
        elseif ($key -and $line -match "^\s+-\s*(?<item>.+?)\s*$") {
            $result.Fields[$key] = @($result.Fields[$key]) + @($Matches["item"])
        }
    }

    return $result
}

function Get-Scalar($fields, [string]$name) {
    if (-not $fields.ContainsKey($name)) { return "" }
    $values = @($fields[$name])
    if (-not $values.Count) { return "" }
    return ([string]$values[0]).Trim().Trim('"', "'")
}

# tags пишутся и списком, и инлайном [a, b, c] — обе формы легальны в YAML и обе встречаются.
function Get-List($fields, [string]$name) {
    if (-not $fields.ContainsKey($name)) { return @() }
    $values = @($fields[$name] | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
    if ($values.Count -eq 1 -and $values[0].StartsWith("[") -and $values[0].EndsWith("]")) {
        $inner = $values[0].Substring(1, $values[0].Length - 2)
        $values = @($inner -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
    return @($values | ForEach-Object { $_.Trim().Trim('"', "'") })
}

function Get-LinkTargets($values) {
    $targets = @()
    foreach ($value in @($values)) {
        foreach ($match in [regex]::Matches([string]$value, "\[\[([^\]\|#]+)")) {
            $targets += $match.Groups[1].Value.Trim()
        }
    }
    return @($targets)
}

function Get-Slug([string]$value) {
    return ($value.ToLowerInvariant() -replace "[\s/]+", "-").Trim("-")
}

# --- Словарь статей --------------------------------------------------------------------------
# module не выдумывается: допустимы ровно те значения, которые стоят в статьях vault.

$articleModules = @{}
$articleNames = @{}
$vaultFiles = @{}

$tasksPrefix = (Normalize $TasksRoot) + "/"
foreach ($file in (Get-ChildItem $DocsRoot -Recurse -Filter *.md -File)) {
    $full = Normalize $file.FullName
    if ($full -like "*/.obsidian/*") { continue }

    $vaultFiles[$file.BaseName] = $full
    if ($full.StartsWith($tasksPrefix)) { continue }

    $front = Read-Frontmatter (Get-Content -LiteralPath $file.FullName -Encoding UTF8)
    if (-not $front.Ok) { continue }

    $module = Get-Scalar $front.Fields "module"
    $articleNames[$file.BaseName] = $module
    if ($module) { $articleModules[$module] = $true }
}

if ($ListModules) {
    Write-Output "module dictionary (from vault articles):"
    foreach ($entry in ($articleNames.GetEnumerator() | Where-Object { $_.Value } | Sort-Object { $_.Value })) {
        Write-Output ("  {0,-26} {1}" -f $entry.Value, $entry.Key)
    }
    exit 0
}

# --- Исключения ------------------------------------------------------------------------------
# Формат строки: <файл>:<правило> # <причина>. Причина обязательна: исключение без неё —
# ошибка скрипта, а не разрешение (иначе файл превращается в свалку).

$exceptionsPath = Join-Path $PSScriptRoot "ticket-format.exceptions.txt"
$exceptions = @{}
$exceptionErrors = @()

if (Test-Path $exceptionsPath) {
    $lineNumber = 0
    foreach ($line in (Get-Content $exceptionsPath -Encoding UTF8)) {
        $lineNumber++
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }

        $parts = $trimmed -split "\s*#\s*", 2
        $token = $parts[0].Trim()
        $reason = if ($parts.Count -gt 1) { $parts[1].Trim() } else { "" }

        if (-not $reason) {
            $exceptionErrors += "  ticket-format.exceptions.txt:${lineNumber}: '$token' has no reason. Every exception needs one."
            continue
        }
        if ($token -notmatch "^[^:]+:[a-z-]+$") {
            $exceptionErrors += "  ticket-format.exceptions.txt:${lineNumber}: '$token' is not '<ticket>:<rule>'."
            continue
        }

        $exceptions[$token] = $reason
    }
}

function Test-Excluded([string]$ticket, [string]$rule) {
    return $exceptions.ContainsKey("${ticket}:${rule}")
}

# --- Тикеты ----------------------------------------------------------------------------------

$allTickets = @(Get-ChildItem $TasksRoot -Recurse -Filter *.md -File |
    Where-Object { $_.Name -ne "Kanban.md" } |
    ForEach-Object { Normalize $_.FullName })

if ($All) {
    $targets = $allTickets
}
elseif ($Files) {
    $targets = @($Files | ForEach-Object {
            $path = Normalize $_
            if ([System.IO.Path]::IsPathRooted($path)) { $path } else { Normalize (Join-Path $project $path) }
        })
}
else {
    $changed = @(Invoke-Git @("diff", "--name-only", "--diff-filter=ACMR", $BaseRef)) +
               @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
    $targets = @($changed | ForEach-Object { Normalize (Join-Path $project (Normalize $_)) } | Sort-Object -Unique)
}

$targets = @($targets | Where-Object { $allTickets -contains $_ })

# --- Правила ---------------------------------------------------------------------------------

$findings = @()
$areas = @("Foundation", "Features", "Project", "Cross-cutting")
$statuses = @("Todo", "In Progress", "Done", "Cancelled")
$required = @("title", "type", "kind", "status", "area", "module", "related", "created", "updated", "tags")

# Проверяются не все заголовки шаблона, а только те, отсутствие которых означает «тикет
# не дописан». Цель и Проблема у фичи, Воспроизведение и Причина у бага есть в шаблоне
# Process/Tickets.md, но машинно не требуются: пустая секция ради зелёного хука хуже
# отсутствующей, а требование полного набора на историческом корпусе порождало именно её.
# У эпика список полный: эпиков мало, каждый пишется с нуля, и «не дописан» тут дороже.
$skeletons = @{
    feature = @("Скоуп", "Критерии Done")
    bug     = @("Симптом", "Решение", "Критерии Done")
    epic    = @("Цель", "Проблема", "Подтикеты", "Критерии Done")
}

$kindByPrefix = @{ "UMT-Feature" = "feature"; "UMT-Bug" = "bug"; "UMT-Epic" = "epic" }
$folderByKind = @{ feature = "Features"; bug = "Bugs"; epic = "Epics" }

function Add-Finding([string]$ticket, [string]$rule, [string]$detail) {
    if (Test-Excluded $ticket $rule) { return }
    $script:findings += "  ${ticket}: [$rule] $detail"
}

# Индекс тестов и их последних исходов — вход правила ticket-test-reference. Строится один раз
# и только когда правило действительно понадобилось: тикетов в Done большинство, но прогон без
# ключей смотрит лишь изменённые.
$script:testIndex = $null
function Get-TestIndex {
    if ($script:testIndex) { return $script:testIndex }

    $classes = @{}
    $methods = @{}
    $failing = @{}

    $frameworkRoot = Join-Path $project "Assets\Framework"
    if (Test-Path $frameworkRoot) {
        $testDirs = @(Get-ChildItem $frameworkRoot -Recurse -Directory | Where-Object { $_.Name -eq "Tests" })
        foreach ($dir in $testDirs) {
            foreach ($file in (Get-ChildItem $dir.FullName -Recurse -Filter *.cs -File)) {
                $currentClass = ""
                foreach ($line in (Get-Content -LiteralPath $file.FullName -Encoding UTF8)) {
                    if ($line -match "\bclass\s+(\w+Tests)\b") {
                        $currentClass = $Matches[1]
                        $classes[$currentClass] = $true
                        continue
                    }
                    if ($currentClass -and $line -match "\bvoid\s+([A-Za-z_]\w*)\s*\(") {
                        $methods["$currentClass.$($Matches[1])"] = $true
                    }
                }
            }
        }
    }

    # Журнал только дописывается, поэтому «последний исход» — последняя строка по тесту.
    $journal = Join-Path $project ".agent-state\FastTests\history.jsonl"
    if (Test-Path $journal) {
        $last = @{}
        foreach ($line in (Get-Content $journal)) {
            $value = $line.Trim()
            if (-not $value) { continue }
            try { $entry = ConvertFrom-Json $value } catch { continue }
            if ($entry.test) { $last[$entry.test] = $entry }
        }

        foreach ($key in $last.Keys) {
            if ($last[$key].outcome -ne "failed") { continue }
            $parts = $key.Split(".")
            if ($parts.Count -lt 2) { continue }
            $short = "$($parts[-2]).$($parts[-1])"
            $failing[$short] = $last[$key].errorType
        }
    }

    $script:testIndex = [pscustomobject]@{ Classes = $classes; Methods = $methods; Failing = $failing }
    return $script:testIndex
}

foreach ($target in $targets) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($target)
    $lines = @(Get-Content -LiteralPath $target -Encoding UTF8)
    $front = Read-Frontmatter $lines

    if (-not $front.Ok) {
        Add-Finding $name "frontmatter-missing" "file has no closed --- frontmatter block."
        continue
    }

    # Имя файла задаёт тип: kind во frontmatter обязан ему соответствовать, а не наоборот.
    $prefix = ($name -replace "-\d+$", "")
    if (-not $kindByPrefix.ContainsKey($prefix)) {
        Add-Finding $name "file-name-format" "name must be UMT-Feature-N, UMT-Bug-N or UMT-Epic-N."
        continue
    }
    $expectedKind = $kindByPrefix[$prefix]

    $folder = Split-Path (Split-Path $target -Parent) -Leaf
    if ($folder -ne $folderByKind[$expectedKind]) {
        Add-Finding $name "wrong-folder" "a '$expectedKind' ticket must live in Tasks/$($folderByKind[$expectedKind])/, not Tasks/$folder/."
    }

    foreach ($field in $required) {
        $values = @()
        if ($front.Fields.ContainsKey($field)) { $values = @($front.Fields[$field] | Where-Object { ([string]$_).Trim() }) }
        if (-not $values.Count) {
            Add-Finding $name "field-missing" "required field '$field' is missing or empty."
        }
    }

    $type = Get-Scalar $front.Fields "type"
    if ($type -and $type -ne "task") {
        Add-Finding $name "type-not-task" "type is '$type', must be 'task' (Dataview selects tickets by it)."
    }

    $kind = Get-Scalar $front.Fields "kind"
    if ($kind -and $kind -ne $expectedKind) {
        Add-Finding $name "kind-mismatch" "kind is '$kind', file name says '$expectedKind'."
    }

    $status = Get-Scalar $front.Fields "status"
    if ($status -and $statuses -notcontains $status) {
        Add-Finding $name "status-unknown" "status '$status' is not one of: $($statuses -join ', ')."
    }

    $area = Get-Scalar $front.Fields "area"
    if ($area -and $areas -notcontains $area) {
        Add-Finding $name "area-unknown" "area '$area' is not one of: $($areas -join ', ')."
    }

    $module = Get-Scalar $front.Fields "module"
    if ($module -and -not $articleModules.ContainsKey($module)) {
        Add-Finding $name "module-unknown" "module '$module' is not the module of any vault article. Run -ListModules."
    }

    foreach ($field in @("created", "updated")) {
        $value = Get-Scalar $front.Fields $field
        if ($value -and $value -notmatch "^\d{4}-\d{2}-\d{2}$") {
            Add-Finding $name "date-format" "$field is '$value', expected YYYY-MM-DD."
        }
    }

    # related — единственная связь тикета со статьёй; ради неё эпик и затевался.
    $relatedRaw = Get-List $front.Fields "related"
    $related = Get-LinkTargets $relatedRaw
    if ($relatedRaw.Count -and -not $related.Count) {
        Add-Finding $name "related-not-links" "related has values but none of them is a [[wiki link]]."
    }

    $relatedModules = @()
    foreach ($link in $related) {
        if ($kindByPrefix.ContainsKey(($link -replace "-\d+$", ""))) {
            Add-Finding $name "related-points-to-ticket" "related links ticket '$link'. Use 'epic:' or 'blocked_by:' for that."
            continue
        }
        if (-not $vaultFiles.ContainsKey($link)) {
            Add-Finding $name "related-unresolved" "related link '$link' resolves to no file in the vault."
            continue
        }
        if ($articleNames.ContainsKey($link) -and $articleNames[$link]) { $relatedModules += $articleNames[$link] }
    }

    if ($module -and $related.Count -and $relatedModules.Count -and $relatedModules -notcontains $module) {
        Add-Finding $name "related-module-mismatch" "module '$module' matches no article in related ($($relatedModules -join ', '))."
    }

    $tags = Get-List $front.Fields "tags"
    $core = @("task")
    if ($kind) { $core += $kind }
    if ($area) { $core += (Get-Slug $area) }
    if ($module) { $core += (Get-Slug $module) }
    $missingTags = @($core | Where-Object { $tags -notcontains $_ })
    if ($missingTags.Count) {
        Add-Finding $name "tags-core-missing" "tags miss the required core: $($missingTags -join ', ')."
    }

    $epic = Get-Scalar $front.Fields "epic"
    if ($epic) {
        $epicTargets = Get-LinkTargets @($epic)
        if (-not $epicTargets.Count) {
            Add-Finding $name "epic-not-link" "epic is '$epic', expected a [[UMT-Epic-N]] link."
        }
        foreach ($link in $epicTargets) {
            if ($link -notmatch "^UMT-Epic-\d+$" -or -not $vaultFiles.ContainsKey($link)) {
                Add-Finding $name "epic-unresolved" "epic link '$link' resolves to no ticket in Tasks/Epics/."
            }
        }
    }

    # Скелет тела: проверяется наличие заголовков, а не содержимое. Пустая секция — сигнал
    # автору, что он о чём-то не подумал; текст «для галочки» проверкой не выманишь.
    if ($kind -and $skeletons.ContainsKey($kind)) {
        $headings = @()
        $inFence = $false
        for ($i = $front.BodyStart; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match '^\s*(```|~~~)') { $inFence = -not $inFence; continue }
            if ($inFence) { continue }
            if ($line -match "^#{2,4}\s+(?<text>.+?)\s*$") { $headings += $Matches["text"] }
        }

        foreach ($heading in $skeletons[$kind]) {
            $found = @($headings | Where-Object { $_ -like "$heading*" })
            if (-not $found.Count) {
                Add-Finding $name "body-heading-missing" "body has no '## $heading' section (kind '$kind')."
            }
        }
    }

    # Тест, названный в закрытом тикете, обязан существовать и быть зелёным: закрытый тикет —
    # это отчёт о сделанном, и ссылка на несуществующий тест в нём просто ложь. В Todo и
    # In Progress то же требование запрещало бы планировать тесты по имени, поэтому статус — Done.
    #
    # Барьер по created: корпус до 2026-08-13 писался без правила и законно ссылается на тесты,
    # переименованные или удалённые за месяцы после закрытия тикета (прогон -All показывал 30
    # таких находок). Переписывать историю ради зелёного хука нельзя, а глушить её тридцатью
    # строками исключений — значит закопать правило в шум.
    $created = Get-Scalar $front.Fields "created"
    if ($status -eq "Done" -and $created -ge "2026-08-13") {
        $index = Get-TestIndex
        $referenced = @{}

        for ($i = $front.BodyStart; $i -lt $lines.Count; $i++) {
            # Точка перед словом отсекает имена сборок и проектов (Foundation.Tests,
            # AutoDecorators.Generator.Tests) — они не тест-классы. Слеши по обе стороны
            # отсекают сегменты путей (.agent-state/FastTests/history.jsonl). Метод обязан начинаться с
            # заглавной (конвенция Method_ExpectedBehavior_Condition), иначе за метод сойдёт
            # расширение файла в «PopupStackTests.cs».
            foreach ($match in [regex]::Matches($lines[$i], "(?<![\w./\\])([A-Z]\w+Tests)(?![\w/\\])(?:\.([A-Z]\w+))?\b")) {
                $class = $match.Groups[1].Value
                $method = $match.Groups[2].Value
                $token = $class
                if ($method) { $token = "$class.$method" }
                $referenced[$token] = $class
            }
        }

        foreach ($token in ($referenced.Keys | Sort-Object)) {
            $class = $referenced[$token]

            if (-not $index.Classes.ContainsKey($class)) {
                Add-Finding $name "ticket-test-reference" "names test class '$class', which exists in no test assembly."
                continue
            }

            if ($token -ne $class -and -not $index.Methods.ContainsKey($token)) {
                Add-Finding $name "ticket-test-reference" "names test '$token', but class '$class' has no such test method."
                continue
            }

            $failing = @($index.Failing.Keys | Where-Object { $_ -eq $token -or $_ -like "*.$token" })
            if ($failing.Count) {
                Add-Finding $name "ticket-test-reference" "names test '$token', which is red in the last journal run ($($index.Failing[$failing[0]]))."
            }
        }
    }
}

# --- WIP-лимит -------------------------------------------------------------------------------
# Считается всегда по всему корпусу, а не по целям прогона: лимит — свойство доски, и увидеть
# его нарушение обязан любой ход, а не только тот, который тронул нужный тикет.

$active = @()
foreach ($ticket in $allTickets) {
    $front = Read-Frontmatter (Get-Content -LiteralPath $ticket -Encoding UTF8)
    if (-not $front.Ok) { continue }
    if ((Get-Scalar $front.Fields "status") -ne "In Progress") { continue }
    if ((Get-Scalar $front.Fields "kind") -eq "epic") { continue }
    $active += [System.IO.Path]::GetFileNameWithoutExtension($ticket)
}

$wipLimit = 3
if ($active.Count -gt $wipLimit) {
    $findings += "  WIP: [wip-limit-exceeded] $($active.Count) tickets are In Progress, limit is $wipLimit."
    $active | Sort-Object | ForEach-Object { $findings += "    $_" }
}

# --- Отчёт -----------------------------------------------------------------------------------

if (-not $findings -and -not $exceptionErrors) { exit 0 }

if ($exceptionErrors) {
    Write-Output "Broken exception entries:"
    $exceptionErrors | ForEach-Object { Write-Output $_ }
    Write-Output ""
}

if ($findings) {
    Write-Output "Ticket convention findings (Process/Tickets.md):"
    $findings | ForEach-Object { Write-Output $_ }
    Write-Output ""
    Write-Output "Fix the ticket, or add an exception with a reason to Tools/ticket-format.exceptions.txt."
}

exit 1
