# Тесты source generator-а AutoDecorators: snapshot генерируемого кода и диагностики ADG001-ADG004.
# Нужен только .NET SDK 8 — Unity не требуется, поэтому именно эти тесты гоняет CI.
# После правок генератора: этот скрипт + Tools/build-generator.ps1 (пересобрать DLL в Assets).
$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "AutoDecorators.Generator.Tests\AutoDecorators.Generator.Tests.csproj"

dotnet test $project --nologo
exit $LASTEXITCODE
