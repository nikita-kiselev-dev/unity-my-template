# Машинная проверка инвариантов unity-my-template-docs/Architecture/Naming.md.
# Скрипт только сообщает — ничего не правит и не форматирует.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список путей (относительно корня проекта) вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # База сравнения для списка изменений рабочего дерева.
    [string]$BaseRef = "HEAD",
    # Полный проход по Assets/Framework/**/*.cs вместо изменений рабочего дерева.
    [switch]$All
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $project "Assets\Framework"

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
# Формат строки: <токен> # <причина>. Причина обязательна: исключение без неё — ошибка скрипта,
# а не разрешение (иначе файл превращается в свалку и конвенция размывается второй раз).

$exceptionsPath = Join-Path $PSScriptRoot "naming-check.exceptions.txt"
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
            $exceptionErrors += "  naming-check.exceptions.txt:${lineNumber}: '$token' has no reason. Every exception needs one."
            continue
        }

        $exceptions[$token] = $reason
    }
}

function Test-Excluded([string]$token) {
    return $exceptions.ContainsKey($token)
}

# --- Файлы для проверки ----------------------------------------------------------------------

# Правила по ассетам стоят полный проход по .meta (~1 с), поэтому включаются только когда в
# изменениях есть ассеты. Триггером служит .meta: он переезжает вместе с любым переименованным
# ассетом, какого бы типа тот ни был.
function Test-AssetTrigger([string[]]$paths) {
    return [bool](@($paths | Where-Object { $_ -like "Assets/*" -and ($_ -like "*.asset" -or $_ -like "*.meta") }).Count)
}

$checkAssets = [bool]$All

if ($All) {
    $targets = Get-ChildItem $sourceRoot -Recurse -Filter *.cs -File | ForEach-Object { Normalize $_.FullName }
}
elseif ($Files) {
    $normalized = @($Files | ForEach-Object { Normalize $_ })
    $checkAssets = Test-AssetTrigger $normalized
    $targets = $normalized |
        Where-Object { $_ -like "*.cs" } |
        ForEach-Object {
            if ([System.IO.Path]::IsPathRooted($_)) { $_ } else { Normalize (Join-Path $project $_) }
        }
}
else {
    $changed = Invoke-Git @("diff", "--name-only", "--diff-filter=ACMR", $BaseRef)
    $changed += Invoke-Git @("ls-files", "--others", "--exclude-standard")
    $changed = @($changed | ForEach-Object { Normalize $_ } | Sort-Object -Unique)
    $checkAssets = Test-AssetTrigger $changed
    $targets = $changed |
        Where-Object { $_ -like "Assets/Framework/*" -and $_ -like "*.cs" } |
        ForEach-Object { Normalize (Join-Path $project $_) }
}

$targets = @($targets | Where-Object { Test-Path $_ })
if (-not $targets -and -not $checkAssets -and -not $exceptionErrors) { exit 0 }

# --- Правила ----------------------------------------------------------------------------------

$findings = @()

$serviceSegments = @("Scripts", "Content", "Editor")

$deadTerms = @(
    @{ Pattern = "\bDto\b"; Message = "dead epic term 'Dto' (use 'Config')" },
    @{ Pattern = "\bFast(View|Logger|Window|Popup)"; Message = "dead epic term 'Fast*' (use 'Auto*')" },
    @{ Pattern = "\bControlEntity\b"; Message = "dead epic term 'ControlEntity' (use 'LifecycleEntity')" },
    @{ Pattern = "\bLogManager\b"; Message = "dead epic term 'LogManager' (use 'LogChannel')" }
)

$typeDeclaration = "(?m)^(?<indent>[ \t]*)(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected)(?:\s+(?:static|sealed|abstract|partial|readonly|unsafe|new))*\s+(?<kind>class|interface|struct|enum|record)\s+(?<name>\w+)"

foreach ($target in $targets) {
    $relative = Normalize $target.Substring($project.Length + 1)
    $text = [System.IO.File]::ReadAllText($target)
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($target)

    function Add-Finding([string]$rule, [string]$detail, [string]$file = $relative) {
        $script:findings += "  ${file}: [$rule] $detail"
    }

    $declarations = [regex]::Matches($text, $typeDeclaration)
    $topLevelNames = @()
    $declaredNames = @()
    foreach ($declaration in $declarations) {
        $name = $declaration.Groups["name"].Value
        $kind = $declaration.Groups["kind"].Value
        $indent = $declaration.Groups["indent"].Value

        $declaredNames += $name
        if ($indent.Length -le 4) { $topLevelNames += $name }

        if (-not (Test-Excluded $name)) {
            if ($name -cmatch "(Manager|Helper|Utils|Handler)$") {
                Add-Finding "forbidden-suffix" "$kind '$name' uses a forbidden suffix (Manager/Helper/Utils/Handler)."
            }

            if ($kind -eq "class" -and $name -cmatch "^I[A-Z]") {
                Add-Finding "i-prefix-on-class" "class '$name' starts with 'I'. Use suffix 'Base' for an abstract base."
            }

            if ($name -cmatch "^On[A-Z]\w*Signal$") {
                Add-Finding "signal-on-prefix" "$kind '$name' starts with 'On'. A signal is a fact in the past tense."
            }
        }
    }

    # Тип реализует ISignal, но не назван *Signal.
    foreach ($match in [regex]::Matches($text, "(?m)^\s*(?:public|internal)[^\r\n]*?\b(?:class|record|struct)\s+(?<name>\w+)\s*:\s*(?<bases>[^\r\n{]+)")) {
        $name = $match.Groups["name"].Value
        $bases = $match.Groups["bases"].Value
        if (Test-Excluded $name) { continue }

        if ($bases -match "(^|[\s,])ISignal(\s*[,{]|\s*$)" -and $name -cnotmatch "Signal$") {
            Add-Finding "signal-suffix" "type '$name' implements ISignal but is not named *Signal."
        }

        if ($bases -match "(^|[\s,])Attribute(\s*[,{]|\s*$)" -and $name -cnotmatch "Attribute$") {
            Add-Finding "attribute-suffix" "attribute '$name' must be declared with the 'Attribute' suffix."
        }

        if ($bases -match "(^|[\s,])ScriptableObject(\s*[,{]|\s*$)" -and $name -cmatch "Config$") {
            Add-Finding "scriptableobject-config" "ScriptableObject '$name' must be named *Settings, not *Config."
        }
    }

    # Пустой маркер-интерфейс.
    foreach ($match in [regex]::Matches($text, "(?ms)^\s*(?:public|internal)\s+(?:partial\s+)?interface\s+(?<name>\w+)(?:<[^>]*>)?\s*(?::[^{]+)?\{\s*\}")) {
        $name = $match.Groups["name"].Value
        if (Test-Excluded $name) { continue }
        Add-Finding "empty-marker-interface" "interface '$name' has no members. Use an attribute for markers."
    }

    # Тестовый шов internal-ctor: VContainer (TypeAnalyzer) сканирует и NonPublic и без явной
    # пометки берёт конструктор с наибольшим числом параметров, то есть сам шов. Забытый [Inject]
    # не видят ни компилятор, ни fast-tests — только рантайм Unity.
    $ctorPattern = "(?m)^[ \t]*(?<attrs>(?:(?:\[[^\]]*\]|//[^\r\n]*)[ \t]*\r?\n[ \t]*)*)(?<mod>public|internal)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)"
    $constructors = @{}

    foreach ($match in [regex]::Matches($text, $ctorPattern)) {
        $name = $match.Groups["name"].Value
        if ($declaredNames -notcontains $name) { continue }

        # Комментарии из блока вычищаются: над таким ctor обычно стоит объяснение,
        # в котором слово [Inject] упомянуто текстом.
        $attributes = [regex]::Replace($match.Groups["attrs"].Value, "//[^\r\n]*", "")

        if (-not $constructors.ContainsKey($name)) { $constructors[$name] = @() }
        $constructors[$name] += [pscustomobject]@{
            Modifier = $match.Groups["mod"].Value
            HasParameters = [bool]$match.Groups["args"].Value.Trim()
            HasInject = [bool]($attributes -match "\[Inject(?:Attribute)?[\]\(]")
        }
    }

    foreach ($entry in $constructors.GetEnumerator()) {
        $typeName = $entry.Key
        if (Test-Excluded $typeName) { continue }

        $seam = @($entry.Value | Where-Object { $_.Modifier -eq "internal" -and $_.HasParameters })
        if (-not $seam) { continue }

        $production = @($entry.Value | Where-Object { $_.Modifier -eq "public" -and -not $_.HasParameters })
        if (-not $production) { continue }
        if (@($production | Where-Object { $_.HasInject })) { continue }

        Add-Finding "injectable-ctor-missing-attribute" "type '$typeName' has an internal test seam ctor, so its public parameterless ctor must be marked [Inject]. Without it VContainer resolves through the seam."
    }

    foreach ($term in $deadTerms) {
        foreach ($match in [regex]::Matches($text, $term.Pattern)) {
            if (Test-Excluded $match.Value) { continue }
            Add-Finding "dead-term" "$($term.Message): '$($match.Value)'."
            break
        }
    }

    # namespace: служебные сегменты и соответствие пути.
    $namespaceMatch = [regex]::Match($text, "(?m)^\s*namespace\s+(?<ns>[\w.]+)")
    if ($namespaceMatch.Success) {
        $namespace = $namespaceMatch.Groups["ns"].Value
        $segments = $namespace -split "\."

        foreach ($segment in $segments) {
            if ($serviceSegments -contains $segment) {
                Add-Finding "namespace-service-segment" "namespace '$namespace' contains service segment '$segment'."
                break
            }
        }

        $relativeDir = Split-Path (Normalize $target.Substring((Join-Path $project "Assets").Length + 1)) -Parent
        $expected = ($relativeDir -replace "\\", "/") -split "/" |
            Where-Object { $_ -and ($serviceSegments -notcontains $_) }
        $expectedNamespace = ($expected -join ".")

        if ($namespace -ne $expectedNamespace -and -not (Test-Excluded $namespace)) {
            Add-Finding "namespace-path-mismatch" "namespace '$namespace' does not match path (expected '$expectedNamespace')."
        }

        $lastSegment = $segments[-1]
        if ($topLevelNames -contains $lastSegment -and -not (Test-Excluded $lastSegment)) {
            Add-Finding "namespace-equals-type" "namespace '$namespace' ends with the name of a type declared inside it."
        }
    }

    # Имя файла = имя типа. Partial-части вида <Type>.<Suffix>.cs проверяются по первой части.
    if ($fileName -ne "AssemblyInfo" -and $declarations.Count -gt 0 -and -not (Test-Excluded $fileName)) {
        $expectedTypeName = ($fileName -split "\.")[0]
        if ($topLevelNames -notcontains $expectedTypeName) {
            Add-Finding "file-name-mismatch" "file declares no top-level type named '$expectedTypeName'."
        }
    }

    # Больше одного публичного типа верхнего уровня — предупреждение, не ошибка.
    $publicTopLevel = @()
    foreach ($declaration in $declarations) {
        if ($declaration.Groups["indent"].Value.Length -le 4 -and $declaration.Value -match "^\s*(?:\[[^\]]*\]\s*)*public") {
            $publicTopLevel += $declaration.Groups["name"].Value
        }
    }
    $distinctPublic = @($publicTopLevel | ForEach-Object { ($_ -split "``")[0] } | Sort-Object -Unique)
    if ($distinctPublic.Count -gt 1 -and -not (Test-Excluded $fileName)) {
        Add-Finding "multiple-public-types" "WARNING: file declares several public top-level types: $($distinctPublic -join ', ')."
    }
}

# --- Правила по ассетам -------------------------------------------------------------------------
# Адрес Addressables и имя файла ассета связаны только конвенцией: ссылки идут по GUID, поэтому
# рассинхрон никогда не падает сам. Индекс guid -> путь строится по .meta — другого способа
# развернуть GUID вне редактора нет.

function Add-AssetFinding([string]$rule, [string]$detail, [string]$file) {
    $script:findings += "  ${file}: [$rule] $detail"
}

function Get-MetaIndex {
    $index = @{}
    $assetsRoot = Join-Path $project "Assets"

    foreach ($meta in (Get-ChildItem $assetsRoot -Recurse -Filter *.meta -File)) {
        $second = @(Get-Content -LiteralPath $meta.FullName -TotalCount 2)[1]

        if ($second -match "^\s*guid:\s*(?<guid>[0-9a-fA-F]{32})") {
            $target = $meta.FullName.Substring(0, $meta.FullName.Length - ".meta".Length)
            $index[$Matches["guid"]] = Normalize $target.Substring($project.Length + 1)
        }
    }

    return $index
}

function Get-ScriptGuid([string]$assetPath) {
    $head = @(Get-Content -LiteralPath $assetPath -TotalCount 40) -join "`n"
    $match = [regex]::Match($head, "m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*(?<guid>[0-9a-fA-F]{32})")
    if ($match.Success) { return $match.Groups["guid"].Value }
    return $null
}

if ($checkAssets) {
    $metaIndex = Get-MetaIndex
    $groupsDirectory = Join-Path $project "Assets\AddressableAssetsData\AssetGroups"
    $entryPattern = "(?m)^\s*-\s*m_GUID:\s*(?<guid>[0-9a-fA-F]{32})[^\r\n]*\r?\n\s*m_Address:\s*(?<address>[^\r\n]*)"

    if (Test-Path $groupsDirectory) {
        foreach ($group in (Get-ChildItem $groupsDirectory -Filter *.asset -File)) {
            # Исключение по имени группы: группы, которые создаёт и переписывает пакет,
            # конвенции шаблона не подчиняются.
            if (Test-Excluded $group.BaseName) { continue }

            $groupRelative = Normalize $group.FullName.Substring($project.Length + 1)
            $text = [System.IO.File]::ReadAllText($group.FullName)

            foreach ($entry in [regex]::Matches($text, $entryPattern)) {
                $guid = $entry.Groups["guid"].Value
                $address = $entry.Groups["address"].Value.Trim()

                # GUID не разворачивается — ассет живёт вне Assets/ (пакет) либо запись висячая.
                # Второе — не про имена, поэтому молча пропускаем: иначе правило шумит на пакетах.
                if (-not $metaIndex.ContainsKey($guid)) { continue }

                $path = $metaIndex[$guid]
                $full = Join-Path $project $path

                if (Test-Path -LiteralPath $full -PathType Container) {
                    Add-AssetFinding "addressable-folder-entry" "address '$address' points at folder '$path'. Address the asset itself." $groupRelative
                    continue
                }

                if ($path -notlike "Assets/Framework/*") { continue }
                if (Test-Excluded $address) { continue }

                $expected = [System.IO.Path]::GetFileNameWithoutExtension($path)

                if ($address -cne $expected) {
                    Add-AssetFinding "addressable-address-mismatch" "address '$address' does not match asset file name '$expected' ($path)." $groupRelative
                }
            }
        }
    }

    foreach ($asset in (Get-ChildItem $sourceRoot -Recurse -Filter *.asset -File)) {
        $assetRelative = Normalize $asset.FullName.Substring($project.Length + 1)
        if (Test-Excluded $asset.BaseName) { continue }

        $scriptGuid = Get-ScriptGuid $asset.FullName
        if (-not $scriptGuid -or -not $metaIndex.ContainsKey($scriptGuid)) { continue }

        $scriptPath = $metaIndex[$scriptGuid]

        # Скрипт вне Assets/Framework — ScriptableObject породил пакет (Localization, TMP),
        # имя ему задаёт пакет. Так все такие ассеты отсекаются без строк в исключениях.
        if ($scriptPath -notlike "Assets/Framework/*" -or $scriptPath -notlike "*.cs") { continue }

        $expected = [System.IO.Path]::GetFileNameWithoutExtension($scriptPath)

        if ($asset.BaseName -cne $expected) {
            Add-AssetFinding "scriptableobject-file-type-mismatch" "asset file name does not match its script '$expected' ($scriptPath)." $assetRelative
        }
    }
}

# --- Отчёт --------------------------------------------------------------------------------------

if (-not $findings -and -not $exceptionErrors) { exit 0 }

if ($exceptionErrors) {
    Write-Output "Broken exception entries:"
    $exceptionErrors | ForEach-Object { Write-Output $_ }
    Write-Output ""
}

if ($findings) {
    Write-Output "Naming convention findings (Architecture/Naming.md):"
    $findings | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
    Write-Output ""
    Write-Output "Fix the name, or add an exception with a reason to Tools/naming-check.exceptions.txt."
}

exit 1
