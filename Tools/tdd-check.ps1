# Гейт TDD-цикла: проверяет по журналу прогонов (.agent-state/FastTests/history.jsonl), что процесс
# соблюдён, а не только что тесты зелёные.
#
#   test-never-failed     — тест-метод, добавленный в этом дифе, ни разу не падал до того, как
#                           стал зелёным. Значит он написан после реализации и подогнан под неё:
#                           тавтологический тест проходит весь остальной Stop-контур молча.
#   green-test-rewritten  — тело тест-метода, который в HEAD был зелёным, изменено. Позеленевший
#                           тест — зафиксированный контракт; правка его вместо реализации это
#                           отладка за счёт спецификации.
#
# Тест, созданный в этом же ходу, под второе правило не попадает: рефакторинг только что
# написанного теста — часть цикла red-green-refactor, а не подмена контракта.
#
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список изменённых путей вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # Журнал прогонов; переопределяется в проверках.
    [string]$Journal,
    # Файл исключений; переопределяется в проверках.
    [string]$Exceptions,
    # База сравнения для списка изменений и для «каким тест был до правки».
    [string]$BaseRef = "HEAD"
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $Journal) { $Journal = Join-Path $project ".agent-state\FastTests\history.jsonl" }
if (-not $Exceptions) { $Exceptions = Join-Path $project "Tools\tdd-check.exceptions.txt" }

# Красный по этим исключениям — не фаза red, а отсутствующая реализация: тест упал, не дойдя
# до проверки поведения. Такой прогон не считается доказательством, что тест что-то проверяет.
$notAnAssert = @(
    "NullReferenceException",
    "MissingMethodException",
    "MissingFieldException",
    "TypeInitializationException",
    "TypeLoadException",
    "NotImplementedException",
    "FileNotFoundException"
)

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

# --- Изменённые тестовые файлы -------------------------------------------------------------

if ($Files) {
    $changed = @($Files | ForEach-Object { Normalize $_ })
}
else {
    $changed = @(Invoke-Git @("diff", "--name-only", $BaseRef)) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
    $changed = @($changed | ForEach-Object { Normalize $_ })
}

$changed = @($changed | Where-Object { $_ -match "/Tests/.+\.cs$" } | Sort-Object -Unique)
if (-not $changed) { exit 0 }

# --- Журнал прогонов -----------------------------------------------------------------------

# Порядок строк в журнале хронологический (раннер только дописывает), поэтому «падал до того,
# как стал зелёным» — это сравнение позиций, а не разбор дат.
$history = @{}
if (Test-Path $Journal) {
    $index = 0
    foreach ($line in (Get-Content $Journal)) {
        $value = $line.Trim()
        if (-not $value) { continue }

        try { $entry = ConvertFrom-Json $value } catch { continue }
        if (-not $entry.test) { continue }

        if (-not $history.ContainsKey($entry.test)) { $history[$entry.test] = @() }
        $history[$entry.test] += [pscustomobject]@{
            Order     = $index
            Outcome   = $entry.outcome
            ErrorType = $entry.errorType
        }
        $index++
    }
}

# --- Исключения ----------------------------------------------------------------------------

$allowed = @{}
if (Test-Path $Exceptions) {
    foreach ($line in (Get-Content $Exceptions)) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith("#")) { continue }

        # Формат строки: <правило>:<токен> # <причина>. Причина обязательна — строка без неё
        # это ошибка скрипта, а не разрешение (та же политика, что в naming-check).
        $parts = $value -split "#", 2
        $token = $parts[0].Trim()
        $reason = if ($parts.Count -eq 2) { $parts[1].Trim() } else { "" }
        if (-not $reason) {
            Write-Output "Exception without a reason in $(Normalize $Exceptions): '$value'."
            Write-Output "Add '# <reason>' or remove the line."
            exit 1
        }

        $allowed[$token] = $reason
    }
}

function Test-Allowed([string]$rule, [string]$fullName, [string]$shortName) {
    if ($allowed.ContainsKey("${rule}:${fullName}")) { return $true }
    if ($allowed.ContainsKey("${rule}:${shortName}")) { return $true }

    # Исключение можно задать на классе целиком: у категории «класс тестов-инвариантов» (property-based
    # на существующий код, SaveBlob roundtrip, скан DI) красного прогона не бывает по своей природе,
    # и строка на каждый метод превратила бы файл исключений в свалку.
    $separator = $fullName.LastIndexOf(".")
    if ($separator -le 0) { return $false }

    $classFull = $fullName.Substring(0, $separator)
    $classShort = $classFull.Split(".")[-1]

    return $allowed.ContainsKey("${rule}:${classFull}") -or $allowed.ContainsKey("${rule}:${classShort}")
}

# --- Разбор тест-методов -------------------------------------------------------------------

# Диапазон метода — по балансу фигурных скобок от объявления. Скобки внутри строковых литералов
# теоретически сбивают счёт; в тестовом коде это не встречается, а альтернатива — тащить Roslyn
# в powershell-гейт.
function Get-TestMethods([string[]]$lines) {
    $methods = @()

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch "\bvoid\s+([A-Za-z_]\w*)\s*\(") { continue }

        $name = $Matches[1]

        # Тест-метод опознаётся по атрибуту выше объявления: [Test], [TestCase(...)].
        $hasAttribute = $false
        for ($back = $i - 1; $back -ge 0 -and $back -ge $i - 10; $back--) {
            $previous = $lines[$back].Trim()
            if ($previous -match "^\[Test") { $hasAttribute = $true; break }
            if ($previous -and -not $previous.StartsWith("[") -and -not $previous.StartsWith("//")) { break }
        }
        if (-not $hasAttribute) { continue }

        $depth = 0
        $started = $false
        $end = $i

        for ($j = $i; $j -lt $lines.Count; $j++) {
            $open = ([regex]::Matches($lines[$j], "\{")).Count
            $close = ([regex]::Matches($lines[$j], "\}")).Count
            if ($open -gt 0) { $started = $true }
            $depth += $open - $close

            if ($started -and $depth -le 0) { $end = $j; break }
            $end = $j
        }

        $methods += [pscustomobject]@{
            Name  = $name
            Start = $i + 1
            End   = $end + 1
            Body  = (($lines[$i..$end] | ForEach-Object { $_.Trim() }) -join "`n")
        }
    }

    return $methods
}

# Полное имя в журнале — Namespace.Class.Method; namespace и класс берутся из файла (в проекте
# один публичный тип на файл, см. Naming).
function Get-TypePrefix([string[]]$lines) {
    $namespace = ""
    $class = ""

    foreach ($line in $lines) {
        if (-not $namespace -and $line -match "^\s*namespace\s+([\w\.]+)") { $namespace = $Matches[1] }
        if (-not $class -and $line -match "^\s*(?:public|internal)?\s*(?:sealed\s+)?(?:partial\s+)?class\s+(\w+)") { $class = $Matches[1] }
        if ($namespace -and $class) { break }
    }

    if (-not $class) { return "" }
    if (-not $namespace) { return $class }
    return "$namespace.$class"
}

# Номера изменённых строк текущей версии файла — из hunk-заголовков diff.
function Get-ChangedLines([string]$path) {
    $lines = @{}
    foreach ($line in (Invoke-Git @("diff", "-U0", $BaseRef, "--", $path))) {
        if ($line -notmatch "^@@ .* \+(\d+)(?:,(\d+))? @@") { continue }
        $start = [int]$Matches[1]
        $count = if ($Matches[2]) { [int]$Matches[2] } else { 1 }
        for ($n = $start; $n -lt $start + $count; $n++) { $lines[$n] = $true }
    }
    return $lines
}

# --- Проверка ------------------------------------------------------------------------------

$findings = @()

foreach ($path in $changed) {
    $full = Join-Path $project ($path -replace "/", "\")
    if (-not (Test-Path $full)) { continue }

    $current = @(Get-Content $full)
    $prefix = Get-TypePrefix $current
    if (-not $prefix) { continue }

    $baseText = @(Invoke-Git @("show", "${BaseRef}:${path}"))
    $baseMethods = if ($baseText) { Get-TestMethods $baseText } else { @() }
    $baseByName = @{}
    foreach ($method in $baseMethods) { $baseByName[$method.Name] = $method }

    $changedLines = if ($Files) { $null } else { Get-ChangedLines $path }

    foreach ($method in (Get-TestMethods $current)) {
        $fullName = "$prefix.$($method.Name)"
        $shortName = "$($prefix.Split('.')[-1]).$($method.Name)"
        $runs = if ($history.ContainsKey($fullName)) { @($history[$fullName]) } else { @() }

        if (-not $baseByName.ContainsKey($method.Name)) {
            # --- Правило 1: новый тест обязан иметь красный прогон до зелёного ---
            if (Test-Allowed "test-never-failed" $fullName $shortName) { continue }

            $firstPassed = @($runs | Where-Object { $_.Outcome -eq "passed" } | Select-Object -First 1)
            $failedBefore = @($runs | Where-Object {
                    $_.Outcome -eq "failed" -and (-not $firstPassed -or $_.Order -lt $firstPassed[0].Order)
                })

            if (-not $runs) {
                $findings += "test-never-failed: $fullName has no runs in the journal at all. Run Tools/fast-tests.ps1."
                continue
            }

            if (-not $failedBefore) {
                $findings += "test-never-failed: $fullName was green on its first run and never failed. A test written after the implementation proves nothing - write the test first, see it red, then implement."
                continue
            }

            $meaningful = @($failedBefore | Where-Object { $notAnAssert -notcontains $_.ErrorType })
            if (-not $meaningful) {
                $types = (($failedBefore | ForEach-Object { $_.ErrorType } | Sort-Object -Unique) -join ", ")
                $findings += "test-never-failed: $fullName only ever failed with $types - that is missing code, not a failing assertion. The red phase must fail on an assert, otherwise the test does not pin any behaviour."
            }
        }
        elseif ($changedLines) {
            # --- Правило 2: позеленевший тест не переписывают вместо реализации ---
            if ($method.Body -eq $baseByName[$method.Name].Body) { continue }
            if (Test-Allowed "green-test-rewritten" $fullName $shortName) { continue }

            $touched = $false
            for ($n = $method.Start; $n -le $method.End; $n++) {
                if ($changedLines.ContainsKey($n)) { $touched = $true; break }
            }
            if (-not $touched) { continue }

            if (@($runs | Where-Object { $_.Outcome -eq "passed" })) {
                $findings += "green-test-rewritten: $fullName was green in $BaseRef and its body changed. Fix the implementation, not the test. If the contract itself changed, say so in the ticket and add an exception with a reason."
            }
        }
    }
}

if (-not $findings) { exit 0 }

Write-Output "TDD cycle findings ($($findings.Count)):"
$findings | ForEach-Object { Write-Output "  $_" }
Write-Output "Journal: $(Normalize $Journal). Exceptions: $(Normalize $Exceptions) (format '<rule>:<test> # <reason>')."
exit 1
