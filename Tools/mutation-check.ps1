# Мутационное тестирование по дифу: независимое измерение силы тестов.
#
# Зелёный fast-tests означает «тесты прошли», а не «поведение проверено»: тест, который исполняет
# код без ассертов на следствия, неотличим от настоящего. Мутатор вносит в изменённый код одну
# точечную правку поведения и смотрит, покраснеет ли набор тестов. Покраснел — мутант убит,
# остался зелёным — выживший мутант: строка исполняется, но её поведение никто не проверяет.
#
# В Stop-цепочку сознательно не включён (см. unity-my-template-docs/Process/Hooks.md): прогон идёт
# минуты, а не секунды. Запускается руками после красно-зелёного цикла на нетривиальной логике.
#
#   powershell -File Tools/mutation-check.ps1              # изменения относительно HEAD
#   powershell -File Tools/mutation-check.ps1 -Files a.cs  # явный список
#   powershell -File Tools/mutation-check.ps1 -All         # весь файл, а не только изменённые члены
#
# Список из нескольких файлов — только в текущем процессе: `& .\Tools\mutation-check.ps1 -Files @(a,b)`.
# Через `powershell -File` массив не разбирается и пути молча уезжают в другие параметры; симптом —
# «Мутаций не найдено».
#
# Exit 0 — выживших нет, exit 1 — есть находки, exit 2 — прогон не состоялся.
param(
    # Явный список изменённых путей вместо git diff.
    [string[]]$Files,
    # База сравнения для списка изменений и для номеров изменённых строк.
    [string]$BaseRef = "HEAD",
    # Мутировать файл целиком, а не только члены, которых коснулся диф.
    [switch]$All,
    # Потолок числа мутантов за прогон. Усечение печатается явно: молчаливое читается как «покрыто всё».
    [int]$Limit = 40,
    # Мутация условия цикла умеет сделать тест вечным — такой прогон убивается и считается убитым мутантом.
    [int]$TimeoutSeconds = 120,
    # Самопроверка мутатора на синтетическом исходнике: все операторы на месте и ни один не ломает компиляцию.
    [switch]$SelfTest,
    # Машиночитаемый результат: по строке JSON на мутанта, дописывается. Нужен, когда прогон идёт
    # батчами и сводку считают по всем батчам сразу — парсить человеческий вывод нельзя.
    [string]$Json
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "fast-build.ps1")

$project = Split-Path -Parent $PSScriptRoot
$workDir = Join-Path $project "Temp\Mutation"

function Normalize([string]$path) { return ($path -replace "\\", "/").Trim() }

# ErrorActionPreference = Stop + stderr нативного процесса в PS 5.1 = NativeCommandError на ровном месте.
function Invoke-Git([string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { return @(& git -C $project @gitArgs 2>$null) }
    finally { $ErrorActionPreference = $previous }
}

# --- Сборка мутатора ---------------------------------------------------------------------------

# Тот же приём, что у Tools/build-generator.ps1: Roslyn из поставки Unity, .NET SDK не требуется.
function Build-Mutator($Context) {
    $toolDir = Join-Path $workDir "tool"
    New-Item -ItemType Directory -Force $toolDir | Out-Null
    $out = Join-Path $toolDir "Mutator.dll"

    $runtimeDir = Get-ChildItem (Join-Path $Context.Unity "Data\NetCoreRuntime\shared\Microsoft.NETCore.App") -Directory |
        Sort-Object Name | Select-Object -Last 1
    $refs = Get-ChildItem $runtimeDir.FullName -Filter "*.dll" | ForEach-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $_.FullName
        }
        catch { }
    }
    $refs += (Join-Path $Context.RoslynDir "Microsoft.CodeAnalysis.dll")
    $refs += (Join-Path $Context.RoslynDir "Microsoft.CodeAnalysis.CSharp.dll")

    $rsp = Join-Path $toolDir "Mutator.rsp"
    $lines = @("-target:exe", "-out:$out", "-langversion:latest", "-nologo", "-nostdlib", "-warn:0")
    foreach ($r in $refs) { $lines += "-r:`"$r`"" }
    foreach ($s in (Get-ChildItem (Join-Path $PSScriptRoot "Mutator") -Filter *.cs)) { $lines += "`"$($s.FullName)`"" }
    Set-Content -Path $rsp -Value $lines -Encoding utf8

    & $Context.Dotnet $Context.Csc "@$rsp" | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { Write-Error "Сборка Mutator провалилась." }

    # Roslyn лежит рядом с exe: своего каталога зондирования у мутатора нет.
    Copy-Item (Join-Path $Context.RoslynDir "Microsoft.CodeAnalysis.dll") $toolDir -Force
    Copy-Item (Join-Path $Context.RoslynDir "Microsoft.CodeAnalysis.CSharp.dll") $toolDir -Force

    $tfm = "net" + ($runtimeDir.Name -replace "^(\d+)\.(\d+).*$", '$1.$2')
    Set-Content -Path (Join-Path $toolDir "Mutator.runtimeconfig.json") -Encoding utf8 -Value @"
{
  "runtimeOptions": {
    "tfm": "$tfm",
    "framework": { "name": "Microsoft.NETCore.App", "version": "$($runtimeDir.Name)" }
  }
}
"@

    return $out
}

# --- Самопроверка мутатора -----------------------------------------------------------------------

# Мутатор — измерительный прибор, и его собственная поломка выглядит как «мутантов нет» или
# «все убиты»: молча и правдоподобно. Синтетический исходник содержит по площадке на каждый
# оператор, прогон проверяет две вещи — оператор сработал и мутант компилируется.
if ($SelfTest) {
    $context = New-FastBuildContext (Join-Path $workDir "bin")
    $mutator = Build-Mutator $context

    $selfDir = Join-Path $workDir "selftest"
    New-Item -ItemType Directory -Force $selfDir | Out-Null
    $samplePath = Join-Path $selfDir "Sample.cs"

    Set-Content -Path $samplePath -Encoding utf8 -Value @'
using System.Collections.Generic;

namespace MutatorSelfTest
{
    public static class Sample
    {
        public static int Compare(int left, int right)
        {
            if (left > right) { return 1; }
            if (left < right) { return 0; }
            if (left == right && left != 0) { return 2; }
            if (left >= right || left <= 0) { return 3; }
            return left + right;
        }

        public static bool Flag(bool first, bool second)
        {
            if (first) { return true; }
            if (second) { return false; }
            return first;
        }

        public static int Drain(List<int> sink, int left, int right)
        {
            sink.Add(left);
            sink.Clear();
            return left - right;
        }
    }
}
'@

    $planPath = Join-Path $selfDir "plan.jsonl"
    & $context.Dotnet $mutator @("plan", "--source", $samplePath, "--out", $planPath) | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Построение плана самопроверки провалилось." }

    $plan = @()
    foreach ($line in (Get-Content $planPath -Encoding UTF8)) {
        $value = $line.Trim()
        if ($value) { $plan += (ConvertFrom-Json $value) }
    }

    # Восемь операторов из тикета: пары «что было → чем заменили». Обратные направления
    # (>= → >, != → ==, || → &&, false → true, - → +) проверяются тем же способом ниже.
    $expected = @(
        @{ Name = "> -> >="; From = ">"; To = ">=" },
        @{ Name = "< -> <="; From = "<"; To = "<=" },
        @{ Name = "== -> !="; From = "=="; To = "!=" },
        @{ Name = "&& -> ||"; From = "&&"; To = "||" },
        @{ Name = "true -> false"; From = "true"; To = "false" },
        @{ Name = "+ -> -"; From = "+"; To = "-" },
        @{ Name = "удаление вызова"; Operator = "statement-removal" },
        @{ Name = "return x -> return default"; Operator = "return-default" }
    )
    $reverse = @(
        @{ Name = ">= -> >"; From = ">="; To = ">" },
        @{ Name = "<= -> <"; From = "<="; To = "<" },
        @{ Name = "!= -> =="; From = "!="; To = "==" },
        @{ Name = "|| -> &&"; From = "||"; To = "&&" },
        @{ Name = "false -> true"; From = "false"; To = "true" },
        @{ Name = "- -> +"; From = "-"; To = "+" }
    )

    $missing = @()
    foreach ($rule in ($expected + $reverse)) {
        $found = @($plan | Where-Object {
                if ($rule.Operator) { $_.operator -eq $rule.Operator }
                else { $_.original -eq $rule.From -and $_.mutated -eq $rule.To }
            })
        if (-not $found) { $missing += $rule.Name }
    }

    # Компиляция мутанта: библиотека против рантайма Unity, без движка — синтетический исходник
    # ничего из Unity не трогает.
    $runtimeDir = Get-ChildItem (Join-Path $context.Unity "Data\NetCoreRuntime\shared\Microsoft.NETCore.App") -Directory |
        Sort-Object Name | Select-Object -Last 1
    $compileRefs = Get-ChildItem $runtimeDir.FullName -Filter "*.dll" | ForEach-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $_.FullName
        }
        catch { }
    }

    $brokenMutants = @()
    foreach ($mutation in $plan) {
        $mutatedFile = Join-Path $selfDir "mutant.cs"
        & $context.Dotnet $mutator @(
            "apply", "--source", $samplePath, "--index", $mutation.index, "--out", $mutatedFile) | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error "Применение мутанта $($mutation.index) провалилось." }

        $rsp = Join-Path $selfDir "mutant.rsp"
        $lines = @("-target:library", "-out:$selfDir\mutant.dll", "-nologo", "-nostdlib", "-warn:0")
        foreach ($r in $compileRefs) { $lines += "-r:`"$r`"" }
        $lines += "`"$mutatedFile`""
        Set-Content -Path $rsp -Value $lines -Encoding utf8

        & $context.Dotnet $context.Csc "@$rsp" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $brokenMutants += "$($mutation.operator) '$($mutation.original)' -> '$($mutation.mutated)' (строка $($mutation.line))"
        }
    }

    # Отсев по конвенции проверяется на синтетике по той же причине, что и операторы: выключившийся
    # фильтр выглядит как «в изменениях нет непроверяемого кода» — молча и правдоподобно. Unity-типы
    # объявлены прямо в исходнике, поэтому проверка обходится без движковых DLL, а наследование
    # берётся цепочкой (WidgetView -> MeshEffectBase -> MonoBehaviour -> Object), как у GradientColor.
    $scanDir = Join-Path $selfDir "scan"
    New-Item -ItemType Directory -Force $scanDir | Out-Null

    Set-Content -Path (Join-Path $scanDir "UnityStub.cs") -Encoding utf8 -Value @'
namespace UnityEngine
{
    public class Object { }
    public class MonoBehaviour : Object { }
    public abstract class MeshEffectBase : MonoBehaviour { }
}
'@
    Set-Content -Path (Join-Path $scanDir "WidgetView.cs") -Encoding utf8 -Value @'
namespace MutatorSelfTest
{
    public sealed class WidgetView : UnityEngine.MeshEffectBase
    {
        public int Blend(int left, int right) { return left + right; }
    }
}
'@
    Set-Content -Path (Join-Path $scanDir "WidgetCore.cs") -Encoding utf8 -Value @'
namespace MutatorSelfTest
{
    public sealed class WidgetCore
    {
        public int Blend(int left, int right) { return left + right; }
    }
}
'@
    Set-Content -Path (Join-Path $scanDir "WidgetModel.cs") -Encoding utf8 -Value @'
namespace MutatorSelfTest
{
    public sealed class WidgetModel
    {
        public int Blend(int left, int right) { return left + right; }
    }
}
'@

    $scanRsp = Join-Path $scanDir "scan.rsp"
    $scanLines = @("-target:library", "-out:$scanDir\scan.dll", "-nologo", "-nostdlib", "-warn:0")
    foreach ($r in $compileRefs) { $scanLines += "-r:`"$r`"" }
    foreach ($s in (Get-ChildItem $scanDir -Filter *.cs)) { $scanLines += "`"$($s.FullName)`"" }
    Set-Content -Path $scanRsp -Value $scanLines -Encoding utf8

    $scanOut = Join-Path $scanDir "excluded.txt"
    & $context.Dotnet $mutator @("scan", "--rsp", $scanRsp, "--out", $scanOut) | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Скан самопроверки провалился." }

    $scanned = @{}
    foreach ($line in (Get-Content $scanOut -Encoding UTF8)) {
        $parts = $line -split "`t"
        if ($parts.Count -ge 3) { $scanned[(Split-Path $parts[2] -Leaf)] = $parts[0] }
    }

    $scanErrors = @()
    foreach ($case in @(
            @{ File = "WidgetView.cs"; Reason = "unity-object" },
            @{ File = "WidgetCore.cs"; Reason = "core-suffix" })) {
        if ($scanned[$case.File] -ne $case.Reason) {
            $scanErrors += "$($case.File) должен быть исключён как $($case.Reason), получено '$($scanned[$case.File])'"
        }
    }
    # UnityStub.cs объявляет и сам UnityEngine.Object: файл исключается, только если исключены
    # все его типы, иначе живая логика рядом с MonoBehaviour выпала бы из измерения.
    foreach ($file in @("WidgetModel.cs", "UnityStub.cs")) {
        if ($scanned.ContainsKey($file)) { $scanErrors += "$file исключён ошибочно ($($scanned[$file]))" }
    }

    Write-Host "Самопроверка мутатора: мутантов $($plan.Count), операторов проверено $(($expected + $reverse).Count), файлов в скане $((Get-ChildItem $scanDir -Filter *.cs).Count)."

    if (-not $missing -and -not $brokenMutants -and -not $scanErrors) {
        Write-Host "OK: все операторы сработали, ни один мутант не сломал компиляцию, отсев по конвенции работает."
        exit 0
    }

    foreach ($name in $missing) { Write-Host "  оператор не сработал: $name" }
    foreach ($broken in $brokenMutants) { Write-Host "  мутант не компилируется: $broken" }
    foreach ($scanError in $scanErrors) { Write-Host "  отсев по конвенции: $scanError" }
    exit 1
}

# --- Что мутируем ------------------------------------------------------------------------------

# Тесты и Editor-код не мутируются: мутация теста ничего не измеряет, а Editor-сборка в прогон не входит.
$mutableCode = "^Assets/Framework/(Foundation|Features)/.+\.cs$"
$excluded = @("/Tests/", "/Editor/")

if ($Files) {
    $changed = @($Files | ForEach-Object { Normalize $_ })
}
else {
    $changed = @(Invoke-Git @("diff", "--name-only", $BaseRef)) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
    $changed = @($changed | ForEach-Object { Normalize $_ })
}

$targets = @($changed | Where-Object {
        $path = $_
        ($path -match $mutableCode) -and -not ($excluded | Where-Object { $path -like "*$_*" })
    } | Sort-Object -Unique)

if (-not $targets) {
    Write-Host "Мутировать нечего: в изменениях нет рантайм-кода Foundation/Features."
    exit 0
}

# Номера изменённых строк текущей версии файла — из hunk-заголовков diff. Мутатор развернёт их
# до объемлющих членов: мутировать сборку целиком не влезает ни в какой разумный таймаут.
function Get-ChangedRanges([string]$path) {
    $ranges = @()
    foreach ($line in (Invoke-Git @("diff", "-U0", $BaseRef, "--", $path))) {
        if ($line -notmatch "^@@ .* \+(\d+)(?:,(\d+))? @@") { continue }
        $start = [int]$Matches[1]
        $count = if ($Matches[2]) { [int]$Matches[2] } else { 1 }
        if ($count -le 0) { continue }
        $ranges += "$start-$($start + $count - 1)"
    }
    return $ranges
}

# --- Отсев кода, который по конвенции не тестируется ---------------------------------------------

# Выживший мутант обязан означать дыру в ассертах. В коде, который проект тестировать и не
# собирался (View, composition root, адаптеры к внешним системам), он не означает ничего — и
# вытесняет из отчёта настоящие находки ровно в той пропорции, в какой такого кода больше.
function Get-MutationExceptions {
    $path = Join-Path $PSScriptRoot "mutation-check.exceptions.txt"
    if (-not (Test-Path $path)) { return @() }

    $patterns = @()
    $lineNumber = 0

    foreach ($line in (Get-Content $path -Encoding UTF8)) {
        $lineNumber++
        $value = $line.Trim() -replace "^﻿", ""
        if (-not $value -or $value.StartsWith("#")) { continue }

        # Причина обязательна: строка без неё — ошибка автора, а не разрешение.
        if ($value -notmatch "^(?<path>[^#]+?)\s+#\s*(?<reason>\S.*)$") {
            Write-Error "mutation-check.exceptions.txt:${lineNumber}: нет причины. Формат: <путь> # <причина>."
        }

        $patterns += (Normalize $Matches["path"])
    }

    return $patterns
}

$context = New-FastBuildContext (Join-Path $workDir "bin")
$mutator = Build-Mutator $context

Write-Host "Базовый прогон (без мутаций)..."
# Сборка идёт до отсева: .rsp с реальными ссылками и дефайнами пишет именно она, а семантика
# базовых типов обязана считаться тем же набором ссылок, каким собирается мутант.
Invoke-FastBuild $context -Quiet | Out-Null

$scanned = @()
foreach ($assembly in @("Foundation", "Features")) {
    if ($targets | Where-Object { $_ -match "^Assets/Framework/$assembly/" }) {
        $scanned += (Join-Path $context.OutDir "$assembly.rsp")
    }
}

$excludedByScan = @{}
if ($scanned) {
    $scanPath = Join-Path $workDir "excluded.txt"
    $scanArgs = @("scan", "--out", $scanPath)
    foreach ($rsp in $scanned) { $scanArgs += @("--rsp", $rsp) }

    & $context.Dotnet $mutator @scanArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Скан типов провалился: фильтр по конвенции нельзя пропускать молча." }

    foreach ($line in (Get-Content $scanPath -Encoding UTF8)) {
        $parts = $line -split "`t"
        if ($parts.Count -lt 3) { continue }
        $relative = Normalize ($parts[2].Substring($project.Length + 1))
        $excludedByScan[$relative] = $parts[0]
    }
}

$exceptions = Get-MutationExceptions
$skippedByRule = @{}

$targets = @($targets | Where-Object {
        $path = $_
        $reason = $excludedByScan[$path]
        if (-not $reason -and ($exceptions | Where-Object { $path -like $_ })) { $reason = "exceptions" }
        if ($reason) { $skippedByRule[$path] = $reason }
        -not $reason
    })

if ($skippedByRule.Count -gt 0) {
    $byReason = $skippedByRule.Values | Group-Object | ForEach-Object { "$($_.Name): $($_.Count)" }
    Write-Host "Вне мутации $($skippedByRule.Count) файлов ($($byReason -join ', ')) — код, который по конвенции не тестируется."
}

if (-not $targets) {
    Write-Host "Мутировать нечего: все изменённые файлы вне мутации."
    exit 0
}

$runner = Build-FastTestRunner $context
# Журнал прогонов не пишем: мутанты — искусственные падения, в истории TDD-цикла им не место.
$runnerArgs = Get-FastTestRunnerArgs $context
$baseline = Invoke-FastTestRunner $context $runner $runnerArgs $TimeoutSeconds

if ($baseline.ExitCode -ne 0) {
    Write-Host "Базовый прогон красный — мутации измерять нечем. Сначала зелёный Tools/fast-tests.ps1."
    exit 2
}

# --- План мутаций ------------------------------------------------------------------------------

$planPath = Join-Path $workDir "plan.jsonl"
$planArgs = @("plan", "--out", $planPath)

# Диапазоны запоминаются: apply не читает план, а пересчитывает его теми же входами, поэтому
# набор строк обязан совпасть с тем, на котором план построен — иначе индексы разъедутся.
$rangesByFile = @{}

foreach ($target in $targets) {
    $full = [IO.Path]::GetFullPath((Join-Path $project ($target -replace "/", "\")))
    if (-not (Test-Path $full)) { continue }

    $planArgs += @("--source", $full)
    if ($All) { continue }

    $ranges = Get-ChangedRanges $target
    # Файл без hunk-ов (новый, ещё не в индексе) мутируется целиком: ограничивать нечем.
    if ($ranges) {
        $rangesByFile[$full] = ($ranges -join ",")
        $planArgs += @("--lines", $rangesByFile[$full])
    }
}

& $context.Dotnet $mutator @planArgs | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Построение плана мутаций провалилось." }

$plan = @()
foreach ($line in (Get-Content $planPath -Encoding UTF8)) {
    $value = $line.Trim()
    if ($value) { $plan += (ConvertFrom-Json $value) }
}

if (-not $plan) {
    Write-Host "Мутаций не найдено: в изменённых членах нет ни одной точки, к которой применим оператор."
    exit 0
}

$skippedCount = 0
if ($plan.Count -gt $Limit) {
    $skippedCount = $plan.Count - $Limit
    $plan = $plan[0..($Limit - 1)]
}

# --- Прогон мутантов ---------------------------------------------------------------------------

function Get-AssemblyName([string]$fullPath) {
    if ((Normalize $fullPath) -match "/Assets/Framework/Features/") { return "Features" }
    return "Foundation"
}

$srcDir = Join-Path $workDir "src"
if (Test-Path $srcDir) { Remove-Item $srcDir -Recurse -Force }

# Строка результата дописывается сразу после исхода, а не в конце: прогон батчами обязан переживать
# обрыв на середине, иначе сводку придётся собирать заново с нуля.
function Write-JsonOutcome($Mutation, [string]$Relative, [string]$Outcome) {
    if (-not $Json) { return }

    $directory = Split-Path $Json -Parent
    if ($directory -and -not (Test-Path $directory)) { New-Item -ItemType Directory -Force $directory | Out-Null }

    $record = [ordered]@{
        file     = $Relative
        line     = $Mutation.line
        column   = $Mutation.column
        operator = $Mutation.operator
        original = $Mutation.original
        mutated  = $Mutation.mutated
        preview  = $Mutation.preview
        outcome  = $Outcome
    }

    # Не Add-Content -Encoding UTF8: в PS 5.1 он ставит BOM при создании файла, и строгий
    # JSONL-парсер падает на первой же записи — символ ﻿ оказывается перед открывающей скобкой.
    [IO.File]::AppendAllText($Json, (ConvertTo-Json $record -Compress) + "`n", (New-Object Text.UTF8Encoding($false)))
}

$killed = 0
$broken = 0
$survivors = @()
$timeouts = 0
$number = 0

Write-Host "Мутантов к проверке: $($plan.Count)."

foreach ($mutation in $plan) {
    $number++
    $relative = Normalize ($mutation.file.Substring($project.Length + 1))
    $address = "${relative}:$($mutation.line)"

    $mutatedDir = Join-Path $srcDir $number
    $mutatedFile = Join-Path $mutatedDir (Split-Path $mutation.file -Leaf)
    $applyArgs = @("apply", "--source", $mutation.file)
    if ($rangesByFile.ContainsKey($mutation.file)) { $applyArgs += @("--lines", $rangesByFile[$mutation.file]) }
    $applyArgs += @("--index", $mutation.index, "--out", $mutatedFile)

    & $context.Dotnet $mutator @applyArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Применение мутанта $($mutation.index) в $relative провалилось." }

    $assembly = Get-AssemblyName $mutation.file
    $overrides = @{ ([IO.Path]::GetFullPath($mutation.file)) = $mutatedFile }

    # Пересобирается только сборка мутанта: мутации живут в телах методов, метаданные не меняются,
    # поэтому зависимые DLL из базового прогона остаются валидными.
    $compiled = Invoke-FastBuild $context -Only @($assembly) -SourceOverrides $overrides -Quiet -NoThrow

    if (-not $compiled) {
        $broken++
        $mutation | Add-Member -Force -NotePropertyName outcome -NotePropertyValue "broken"
        Write-Host "  [$number/$($plan.Count)] BROKEN  $address  $($mutation.operator)"
        Write-JsonOutcome $mutation $relative "broken"
        continue
    }

    $run = Invoke-FastTestRunner $context $runner $runnerArgs $TimeoutSeconds

    if ($run.TimedOut) {
        $timeouts++
        $killed++
        $mutation | Add-Member -Force -NotePropertyName outcome -NotePropertyValue "timeout"
        Write-Host "  [$number/$($plan.Count)] TIMEOUT $address  $($mutation.operator)"
        Write-JsonOutcome $mutation $relative "timeout"
    }
    elseif ($run.ExitCode -eq 0) {
        $survivors += $mutation
        $mutation | Add-Member -Force -NotePropertyName outcome -NotePropertyValue "survived"
        Write-Host "  [$number/$($plan.Count)] ВЫЖИЛ   $address  $($mutation.operator)"
        Write-JsonOutcome $mutation $relative "survived"
    }
    else {
        $killed++
        $mutation | Add-Member -Force -NotePropertyName outcome -NotePropertyValue "killed"
        Write-Host "  [$number/$($plan.Count)] убит    $address  $($mutation.operator)"
        Write-JsonOutcome $mutation $relative "killed"
    }
}

# Каталог сборки остаётся с последним мутантом внутри — возвращаем чистый код, чтобы следующий
# прогон не стартовал с мутированной DLL.
Invoke-FastBuild $context -Quiet | Out-Null

# --- Отчёт -------------------------------------------------------------------------------------

# Поведенческие операторы меняют результат вычисления, и их выживший — почти всегда дыра.
# Слабые (удаление вызова, булев литерал, return default) сносят действие, наблюдаемое только
# через Unity или через соседний объект, поэтому их выживаемость систематически выше и в одну
# цифру с поведенческими не складывается: сложенные, они дают метрику, которая ничего не измеряет.
$behavioralOperators = @("relational-boundary", "equality", "logical", "arithmetic")

function Write-OutcomeSummary([string]$Title, $Items) {
    $counts = @{ killed = 0; survived = 0; broken = 0; timeout = 0 }
    foreach ($item in $Items) {
        if ($item.outcome) { $counts[$item.outcome] = $counts[$item.outcome] + 1 }
    }
    # Таймаут — тоже замеченное изменение поведения: мутация условия цикла сделала тест вечным.
    $counts.killed += $counts.timeout

    $measured = $counts.killed + $counts.survived
    $rate = if ($measured -gt 0) { [math]::Round(100.0 * $counts.survived / $measured) } else { 0 }

    Write-Host "$Title — проверено $($Items.Count), убито $($counts.killed), выжило $($counts.survived), не скомпилировалось $($counts.broken); выживаемость $rate%."
}

$behavioral = @($plan | Where-Object { $behavioralOperators -contains $_.operator })
$weak = @($plan | Where-Object { $behavioralOperators -notcontains $_.operator })

Write-Host ""
if ($behavioral.Count -gt 0) {
    Write-OutcomeSummary "Поведенческие операторы (границы, равенство, логика, арифметика)" $behavioral
}
if ($weak.Count -gt 0) {
    Write-OutcomeSummary "Слабые операторы (удаление вызова, булев литерал, return default)" $weak
    Write-Host "  Выживший слабого оператора может быть ненаблюдаемым по построению: снесённое действие видно только через Unity или соседний объект."
}
Write-Host "Всего: убито $killed (из них по таймауту $timeouts), выжило $($survivors.Count), не скомпилировалось $broken."

if ($skippedCount -gt 0) {
    Write-Host "Не проверено мутаций: $skippedCount (потолок -Limit $Limit). Это не «покрыто всё»."
}

if (-not $survivors) {
    Write-Host "Выживших нет: изменённое поведение замечает хотя бы один тест."
    exit 0
}

function Write-Survivors([string]$Title, $Items) {
    if (-not $Items) { return }

    Write-Host ""
    Write-Host "${Title} ($($Items.Count)):"
    foreach ($mutation in $Items) {
        $relative = Normalize ($mutation.file.Substring($project.Length + 1))
        Write-Host "  ${relative}:$($mutation.line):$($mutation.column)  $($mutation.operator)"
        $shown = if ($mutation.mutated) { $mutation.mutated } else { "<удалено>" }
        Write-Host "    '$($mutation.original)' -> '$shown'"
        Write-Host "    $($mutation.preview)"
    }
}

Write-Survivors "Выжившие по поведенческим операторам — тесты не заметили изменения результата" `
    @($survivors | Where-Object { $behavioralOperators -contains $_.operator })
Write-Survivors "Выжившие по слабым операторам — разбирать после поведенческих" `
    @($survivors | Where-Object { $behavioralOperators -notcontains $_.operator })

Write-Host ""
Write-Host "Каждый — либо дыра в ассертах, либо эквивалентная мутация (поведение не изменилось)."
Write-Host "Второе нужно проверять глазами и фиксировать в тикете, а не считать по умолчанию."
exit 1
