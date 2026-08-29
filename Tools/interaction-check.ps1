# Машинная проверка инвариантов unity-my-template-docs/Architecture/Class-Interaction.md.
# Скрипт только сообщает — ничего не правит и не форматирует.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список путей (относительно корня проекта) вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # База сравнения для списка изменений рабочего дерева.
    [string]$BaseRef = "HEAD",
    # Полный проход по Assets/Framework/**/*.cs вместо изменений рабочего дерева.
    [switch]$All,
    # Другой корень исходников — для искусственных проверок самого скрипта.
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$sourceRoot = if ($SourceRoot) { $SourceRoot } else { Join-Path $project "Assets\Framework" }

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

# --- Исключения ------------------------------------------------------------------------------
# Формат строки: <правило>:<токен> # <причина>. Причина обязательна: исключение без неё — ошибка
# скрипта, а не разрешение. Правило в токене обязательно: одно и то же имя типа законно нарушает
# одно правило и не имеет права нарушать другое.

$exceptionsPath = Join-Path $PSScriptRoot "interaction-check.exceptions.txt"
$exceptions = @{}
$exceptionErrors = @()

if (Test-Path $exceptionsPath) {
    $lineNumber = 0
    foreach ($line in (Get-Content $exceptionsPath)) {
        $lineNumber++
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }

        $parts = $trimmed -split "\s*#\s*", 2
        $token = $parts[0].Trim()
        $reason = if ($parts.Count -gt 1) { $parts[1].Trim() } else { "" }

        if (-not $reason) {
            $exceptionErrors += "  interaction-check.exceptions.txt:${lineNumber}: '$token' has no reason. Every exception needs one."
            continue
        }

        if ($token -notmatch "^[a-z][a-z-]*:.+") {
            $exceptionErrors += "  interaction-check.exceptions.txt:${lineNumber}: '$token' must look like <rule>:<token>."
            continue
        }

        $exceptions[$token] = $reason
    }
}

function Test-Excluded([string]$rule, [string]$token) {
    return $exceptions.ContainsKey("${rule}:${token}")
}

# --- Исходники --------------------------------------------------------------------------------
# Индексные правила (signal-without-subscriber, cross-feature-concrete-type) отвечают на вопросы
# про всё дерево, а не про файл, поэтому дерево читается целиком независимо от списка целей.
# Тесты и Editor-сборки исключены: в графе DI и в границах фич они не участвуют, а фейки обязаны
# держать изменяемые списки.

function Test-Ignored([string]$relative) {
    return ($relative -like "*/Tests/*" -or $relative -like "*/Editor/*")
}

$sources = @{}

# Путь в отчёте и в правилах всегда выглядит как Assets/Framework/...: правила про фичи разбирают
# его по сегментам, и искусственный корень не должен менять их поведение.
if (Test-Path $sourceRoot) {
    $rootFull = (Resolve-Path $sourceRoot).Path
    foreach ($file in (Get-ChildItem $sourceRoot -Recurse -Filter *.cs -File)) {
        $relative = "Assets/Framework/" + (Normalize $file.FullName.Substring($rootFull.Length + 1))
        if (Test-Ignored $relative) { continue }
        $sources[$relative] = [System.IO.File]::ReadAllText($file.FullName)
    }
}

if (-not $sources.Count) { exit 0 }

if ($All) {
    $targets = @($sources.Keys | Sort-Object)
}
elseif ($Files) {
    $targets = @($Files |
        ForEach-Object { Normalize $_ } |
        ForEach-Object { if ([System.IO.Path]::IsPathRooted($_)) { $_.Substring($project.Length + 1) } else { $_ } } |
        Where-Object { $sources.ContainsKey($_) } |
        Sort-Object -Unique)
}
else {
    $changed = Invoke-Git @("diff", "--name-only", "--diff-filter=ACMR", $BaseRef)
    $changed += Invoke-Git @("ls-files", "--others", "--exclude-standard")
    $targets = @($changed |
        ForEach-Object { Normalize $_ } |
        Where-Object { $sources.ContainsKey($_) } |
        Sort-Object -Unique)
}

# --- Общее ------------------------------------------------------------------------------------

$findings = @()

function Add-Finding([string]$file, [string]$rule, [string]$detail) {
    $script:findings += "  ${file}: [$rule] $detail"
}

$typeDeclaration = "(?m)^(?<indent>[ \t]*)(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected)(?:\s+(?:static|sealed|abstract|partial|readonly|unsafe|new))*\s+(?<kind>class|interface|struct|enum|record)\s+(?<name>\w+)"

# Член типа: поле (tail '=' или ';'), свойство ('{' или '=>'), метод ('('). Два неочевидных места:
# 'tail' проверяет '=>' раньше '=', иначе expression-bodied свойство прочитается как поле с
# инициализатором; список generic-аргументов запрещает '=' внутри, иначе '>' из '=>' закрывает его,
# и 'public ReadOnlyReactiveProperty<bool> IsAdPlaying => _isAdPlaying;' читается как поле
# '_isAdPlaying' с типом 'ReadOnlyReactiveProperty<bool> IsAdPlaying =>'.
$memberPattern = "(?m)^[ \t]*(?<mod>public|protected)\s+(?<mods>(?:(?:static|virtual|override|abstract|sealed|new|readonly|partial|async|extern|unsafe|event)\s+)*)(?<type>[A-Za-z_][\w\.]*(?:<[^;()=\r\n]*>)?(?:\[\])?)\s+(?<name>\w+)\s*(?<tail>=>|[=;{(])"

# Изменяемое состояние наружу. Anchor на начало типа обязателен: ReadOnlyReactiveProperty<T>
# содержит подстроку 'ReactiveProperty<' и законен.
$mutableTypePattern = "^(?:[\w]+\.)*(ReactiveProperty|Subject|BehaviorSubject|ReplaySubject|List|Dictionary|SortedDictionary|HashSet|SortedSet|Queue|Stack|LinkedList)\s*<"

function Test-MutableType([string]$type) {
    if ($type -match $mutableTypePattern) { return $true }
    if ($type -match "\[\]$") { return $true }
    return $false
}

# Возвращённый массив почти всегда свежий результат вычисления (Serialize, Scan), а не ссылка на
# внутреннее состояние: требовать IReadOnly* на нём — шум. Хранимый массив в члене типа — наоборот,
# именно разделяемое изменяемое состояние, там правило остаётся.
function Test-MutableReturnType([string]$type) {
    return [bool]($type -match $mutableTypePattern)
}

# R0: способы получить зависимость в обход DI.
$locatorTokens = @(
    "IObjectResolver",
    "FindObjectOfType",
    "FindObjectsOfType",
    "FindAnyObjectByType",
    "FindFirstObjectByType",
    "FindObjectsByType",
    "GameObject.Find"
)

# --- Правила по файлу --------------------------------------------------------------------------

foreach ($relative in $targets) {
    $text = $sources[$relative]
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($relative)

    # Composition root собирает граф и по определению знает всех.
    $isCompositionRoot = [bool]([regex]::IsMatch($text, ":\s*LifetimeScope\b"))

    foreach ($match in [regex]::Matches($text, $memberPattern)) {
        $mods = $match.Groups["mods"].Value
        $type = $match.Groups["type"].Value
        $name = $match.Groups["name"].Value
        $tail = $match.Groups["tail"].Value

        # Исключение адресуется либо конкретному члену, либо типу целиком: у сериализуемых
        # ScriptableObject-ов правило нарушает каждое поле, и строка на поле — шум, а не решение.
        if ((Test-Excluded "member" "${fileName}.${name}") -or (Test-Excluded "member" $fileName)) { continue }

        if ($mods -match "\bevent\b") {
            Add-Finding $relative "public-event" "member '$name' is a public event. A lower layer notifies through a signal or an observable property, not an event."
            continue
        }

        $isField = ($tail -eq "=" -or $tail -eq ";")
        $isMethod = ($tail -eq "(")

        if ($isField -and $match.Groups["mod"].Value -eq "public" -and $mods -notmatch "\breadonly\b") {
            Add-Finding $relative "public-field" "field '$name' is public and mutable. State changes through a method of its owner."
        }

        if ($isMethod) {
            if (Test-MutableReturnType $type) {
                Add-Finding $relative "mutable-collection-returned" "method '$name' returns mutable '$type'. Return IReadOnly* instead."
            }
        }
        elseif (Test-MutableType $type) {
            Add-Finding $relative "mutable-state-exposed" "member '$name' exposes mutable '$type'. Keep it private and expose a read-only view."
        }
    }

    if (-not $isCompositionRoot -and -not (Test-Excluded "service-locator" $fileName)) {
        foreach ($token in $locatorTokens) {
            if ($text -notmatch [regex]::Escape($token)) { continue }

            # IObjectResolver сам по себе не service locator: Inject(child) — это композиция,
            # достройка уже созданного объекта. Локатором его делает Resolve, то есть запрос
            # «дай мне что-нибудь по типу» в обход собственных [Inject]-полей.
            if ($token -eq "IObjectResolver" -and $text -notmatch "\bResolve(<|\s*\()") { continue }

            Add-Finding $relative "service-locator" "uses '$token' outside a composition root. Take the dependency through [Inject]."
        }
    }

    if (-not (Test-Excluded "static-instance" $fileName)) {
        if ([regex]::IsMatch($text, "(?m)^\s*(?:public|internal)\s+static\s+[\w<>\.\[\]]+\s+Instance\b")) {
            Add-Finding $relative "static-instance" "declares a static 'Instance'. Global access point instead of a DI edge."
        }
    }
}

# --- R5: фича ссылается только на границу другой фичи -----------------------------------------
# Общие подсистемы освобождены от правила: своего домена у них нет, и зависимость на них по замыслу.
# Новая общая фича добавляется сюда явно, а не по факту накопленных ссылок.

$sharedFeatures = @(
    "Items",        # экономика: IInventory, счётчики, CurrencyView — нужны любой фиче
    "UI",           # кит общих UI-компонентов
    "SaveLoad"      # игровые расширения сейва
)

# Composition root на уровне фич: регистрируют всё и потому видят всё.
$rootFeatures = @(
    "Initialization",   # игровые scope-ы
    "SaveLoad"          # реестр тегов сейва: знает каждый блоб по определению
)

$featureTypes = @{}

foreach ($relative in $sources.Keys) {
    if ($relative -notlike "Assets/Framework/Features/*") { continue }

    $segments = $relative -split "/"
    if ($segments.Count -lt 5) { continue }
    $feature = $segments[3]

    foreach ($match in [regex]::Matches($sources[$relative], $typeDeclaration)) {
        if ($match.Groups["indent"].Value.Length -le 4) {
            $featureTypes[$match.Groups["name"].Value] = $feature
        }
    }
}

$forbiddenCache = @{}

function Get-ForbiddenPattern([string]$feature) {
    if ($script:forbiddenCache.ContainsKey($feature)) { return $script:forbiddenCache[$feature] }

    $names = @()
    foreach ($entry in $script:featureTypes.GetEnumerator()) {
        $owner = $entry.Value
        $name = $entry.Key

        if ($owner -eq $feature) { continue }
        if ($script:sharedFeatures -contains $owner) { continue }
        if ($name -cmatch "^I[A-Z]") { continue }
        if ($name -cmatch "Constants$") { continue }

        $names += $name
    }

    $pattern = if ($names) { "\b(" + (($names | Sort-Object -Unique) -join "|") + ")\b" } else { $null }
    $script:forbiddenCache[$feature] = $pattern
    return $pattern
}

foreach ($relative in $targets) {
    if ($relative -notlike "Assets/Framework/Features/*") { continue }

    $segments = $relative -split "/"
    if ($segments.Count -lt 5) { continue }
    $feature = $segments[3]
    if ($rootFeatures -contains $feature) { continue }

    $pattern = Get-ForbiddenPattern $feature
    if (-not $pattern) { continue }

    $reported = @()
    foreach ($match in [regex]::Matches($sources[$relative], $pattern)) {
        $name = $match.Value
        if ($reported -contains $name) { continue }
        if (Test-Excluded "cross-feature-concrete-type" "${feature}->${name}") { continue }
        $reported += $name

        Add-Finding $relative "cross-feature-concrete-type" "feature '$feature' references concrete type '$name' of feature '$($featureTypes[$name])'. Use its boundary interface or *Constants."
    }
}

# --- R10: сигнал без подписчика ---------------------------------------------------------------
# Правило про дерево, а не про файл: последний Subscribe<T> исчезает в чужом файле, поэтому отчёт
# идёт по всем сигналам при любом прогоне, а не только по изменённым.

$signalDeclaration = "(?m)^\s*(?:public|internal)[^\r\n]*?\b(?:class|record|struct)\s+(?<name>\w+)\s*:\s*(?<bases>[^\r\n{]+)"

$signals = @{}

foreach ($relative in $sources.Keys) {
    foreach ($match in [regex]::Matches($sources[$relative], $signalDeclaration)) {
        $bases = $match.Groups["bases"].Value
        if ($bases -match "(^|[\s,])ISignal(\s*[,{]|\s*$)") {
            $signals[$match.Groups["name"].Value] = $relative
        }
    }
}

foreach ($entry in $signals.GetEnumerator()) {
    $name = $entry.Key
    if (Test-Excluded "signal-without-subscriber" $name) { continue }

    $subscribed = $false
    foreach ($text in $sources.Values) {
        if ($text -match "Subscribe<\s*$([regex]::Escape($name))\s*>") { $subscribed = $true; break }
    }

    if (-not $subscribed) {
        Add-Finding $entry.Value "signal-without-subscriber" "signal '$name' has no Subscribe<$name> in runtime code. Drop it, or state in the exceptions file which extension point waits for it."
    }
}

# --- Отчёт ------------------------------------------------------------------------------------

if (-not $findings -and -not $exceptionErrors) { exit 0 }

if ($exceptionErrors) {
    Write-Output "Broken exception entries:"
    $exceptionErrors | ForEach-Object { Write-Output $_ }
    Write-Output ""
}

if ($findings) {
    Write-Output "Class interaction findings (Architecture/Class-Interaction.md):"
    $findings | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
    Write-Output ""
    Write-Output "Fix the interaction, or add an exception with a reason to Tools/interaction-check.exceptions.txt."
}

exit 1
