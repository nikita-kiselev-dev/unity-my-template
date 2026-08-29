# Быстрый прогон EditMode-тестов без Unity (агентский TDD-цикл).
# Компилирует Foundation/Features/Foundation.Tests/Features.Tests и запускает Tools/UnitTestRunner;
# сама компиляция — в Tools/fast-build.ps1, общая с Tools/mutation-check.ps1.
# Финальная истина — Unity Test Runner (Tools/run-tests.ps1 или окно Test Runner в редакторе).
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "fast-build.ps1")

$context = New-FastBuildContext
Invoke-FastBuild $context | Out-Null

$runner = Build-FastTestRunner $context

# Журнал прогонов — вход гейта tdd-check: он проверяет, что новый тест был красным до зелёного.
$journal = Join-Path $context.Project ".agent-state\FastTests\history.jsonl"
$result = Invoke-FastTestRunner $context $runner (Get-FastTestRunnerArgs $context $journal)

exit $result.ExitCode
