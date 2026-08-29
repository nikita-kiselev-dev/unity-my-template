# Сборка AutoDecorators.Generator без .NET SDK: Unity-овский Roslyn csc (как в fast-tests.ps1).
# Референсы — бандленный Microsoft.CodeAnalysis (4.3.x — требование Unity) и рантайм-фасады
# из поставки Unity. Результат копируется в Assets/Framework/Analyzers/.
# При установленном .NET SDK эквивалент: dotnet build Tools/AutoDecorators.Generator -c Release.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$versionLine = Get-Content (Join-Path $project "ProjectSettings\ProjectVersion.txt") -TotalCount 1
$version = ($versionLine -split ":\s*")[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor"
if (-not (Test-Path $unity)) { Write-Error "Unity $version не найден: $unity" }

$dotnetUnity = Join-Path $unity "Data\NetCoreRuntime\dotnet.exe"
if (-not (Test-Path $dotnetUnity)) { $dotnetUnity = "dotnet" }
$roslynDir = Join-Path $unity "Data\DotNetSdkRoslyn"
$csc = Join-Path $roslynDir "csc.dll"

$runtimeDir = Get-ChildItem (Join-Path $unity "Data\NetCoreRuntime\shared\Microsoft.NETCore.App") -Directory |
    Sort-Object Name | Select-Object -Last 1 -ExpandProperty FullName

$outDir = Join-Path $project "Temp\AutoDecorators"
New-Item -ItemType Directory -Force $outDir | Out-Null
$out = Join-Path $outDir "AutoDecorators.Generator.dll"

# GetAssemblyName отсекает нативные DLL (coreclr, clrjit и т.п.).
$refs = Get-ChildItem $runtimeDir -Filter "*.dll" | ForEach-Object {
    try {
        [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
        $_.FullName
    }
    catch { }
}
$refs += (Join-Path $roslynDir "Microsoft.CodeAnalysis.dll")
$refs += (Join-Path $roslynDir "Microsoft.CodeAnalysis.CSharp.dll")

$rsp = Join-Path $outDir "AutoDecorators.Generator.rsp"
$lines = @("-target:library", "-out:$out", "-langversion:latest", "-nologo", "-nostdlib", "-warn:0")
foreach ($r in $refs) { $lines += "-r:`"$r`"" }
foreach ($s in (Get-ChildItem (Join-Path $PSScriptRoot "AutoDecorators.Generator") -Filter *.cs)) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding utf8

& $dotnetUnity $csc "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Error "Сборка AutoDecorators.Generator провалилась." }

Copy-Item $out (Join-Path $project "Assets\Framework\Analyzers\AutoDecorators.Generator.dll") -Force
Write-Host "OK: Assets\Framework\Analyzers\AutoDecorators.Generator.dll обновлён."

# Хэш пишется только после успешного копирования: иначе он подтвердил бы сборку, которой нет.
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "generator-hash.ps1") -Write
if ($LASTEXITCODE -ne 0) { Write-Error "Запись Tools/generator-hash.ps1 -Write провалилась." }
