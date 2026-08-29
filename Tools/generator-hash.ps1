# Хэш исходников AutoDecorators.Generator: единственная связь между закоммиченной DLL и кодом,
# из которого она собрана. Без него забытая пересборка — тихий провал: generator-tests гоняют
# исходники и зелены, а Unity компилирует старым генератором.
# Вывод латиницей: его печатает Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке.
param(
    # Записать хэш рядом с DLL. Вызывается из build-generator.ps1 после успешной сборки.
    [switch]$Write,
    # Сверить хэш с исходниками. Exit 0 — совпало, exit 1 — рассинхрон (текст в stdout).
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$sourceDir = Join-Path $PSScriptRoot "AutoDecorators.Generator"
$hashPath = Join-Path $project "Assets\Framework\Analyzers\AutoDecorators.Generator.dll.hash"

# Тот же набор файлов, что компилирует build-generator.ps1: верхний уровень, без bin/obj.
# Сортировка — ради детерминизма: порядок Get-ChildItem не гарантирован.
$files = @(Get-ChildItem $sourceDir -Filter *.cs -File | Sort-Object Name)
if (-not $files) { Write-Error "No generator sources found in $sourceDir." }

# Переводы строк нормализуются: git может выдать файл с CRLF или LF в зависимости от настроек
# рабочей копии, а хэш обязан зависеть только от кода.
$builder = New-Object System.Text.StringBuilder
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName) -replace "`r`n", "`n"
    [void]$builder.Append($file.Name).Append("`n").Append($text).Append("`n")
}

$sha = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
$hash = [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()

if ($Write) {
    Set-Content -Path $hashPath -Value $hash -Encoding utf8
    Write-Output "OK: AutoDecorators.Generator.dll.hash updated ($hash)."
    exit 0
}

if ($Check) {
    if (-not (Test-Path $hashPath)) {
        Write-Output "Generator hash file is missing: Assets/Framework/Analyzers/AutoDecorators.Generator.dll.hash"
        Write-Output "Run: powershell -File Tools/build-generator.ps1"
        exit 1
    }

    $stored = ((Get-Content $hashPath -Raw) -replace "[^0-9a-f]", "")

    if ($stored -ne $hash) {
        Write-Output "Generator sources changed after the last build:"
        Write-Output "  sources: $hash"
        Write-Output "  dll:     $stored"
        Write-Output "The committed DLL in Assets/Framework/Analyzers/ is stale - Unity still compiles with the old generator."
        Write-Output "Run: powershell -File Tools/build-generator.ps1"
        exit 1
    }

    exit 0
}

Write-Output $hash
