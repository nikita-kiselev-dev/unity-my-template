# Компиляция сборок проекта вне Unity компилятором Roslyn из поставки Unity: референсы и дефайны
# берутся из сгенерированных Unity csproj, ссылки тестовых сборок — строго по .asmdef.
#
# Скрипт дот-сорсится, а не вызывается: . (Join-Path $PSScriptRoot "fast-build.ps1")
# Потребители — Tools/fast-tests.ps1 (обычный прогон) и Tools/mutation-check.ps1 (прогон мутантов).
# Различаются они только каталогом сборки и подменой исходников, поэтому логика тут одна: разъехаться
# двум копиям компиляции нельзя — иначе мутант компилировался бы не тем, чем компилируются тесты.

function New-FastBuildContext {
    param(
        # Каталог артефактов сборки. Пересобираемые файлы, поэтому Temp/, а не .agent-state/.
        [string]$OutDir
    )

    $project = Split-Path -Parent $PSScriptRoot

    $versionLine = Get-Content (Join-Path $project "ProjectSettings\ProjectVersion.txt") -TotalCount 1
    $version = ($versionLine -split ":\s*")[1].Trim()
    $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor"
    if (-not (Test-Path $unity)) { Write-Error "Unity $version не найден: $unity" }

    if (-not (Test-Path (Join-Path $project "Foundation.csproj"))) {
        Write-Error "Нет сгенерированных csproj. В Unity: Assets -> Open C# Project (или Preferences -> External Tools -> Regenerate project files)."
    }

    if (-not $OutDir) { $OutDir = Join-Path $project "Temp\FastTests" }
    New-Item -ItemType Directory -Force $OutDir | Out-Null

    $dotnetUnity = Join-Path $unity "Data\NetCoreRuntime\dotnet.exe"
    if (-not (Test-Path $dotnetUnity)) { $dotnetUnity = "dotnet" }

    # Пул precompiled-DLL: HintPath-ы всех сгенерированных csproj — это ровно те файлы, которые Unity
    # видит как плагины. Дешевле рекурсивного обхода Assets/Packages и Library/PackageCache.
    $pool = @{}
    foreach ($csproj in (Get-ChildItem $project -Filter *.csproj -File)) {
        try { [xml]$xml = Get-Content $csproj.FullName } catch { continue }
        $ns = @{ m = "http://schemas.microsoft.com/developer/msbuild/2003" }
        foreach ($node in (Select-Xml -Xml $xml -XPath "//m:Reference/m:HintPath" -Namespace $ns)) {
            $path = $node.Node.InnerText
            if ($path -like "$unity*") { continue }
            $key = Split-Path $path -Leaf
            if (-not $pool.ContainsKey($key)) { $pool[$key] = $path }
        }
    }

    return @{
        Project         = $project
        Unity           = $unity
        Dotnet          = $dotnetUnity
        Csc             = Join-Path $unity "Data\DotNetSdkRoslyn\csc.dll"
        RoslynDir       = Join-Path $unity "Data\DotNetSdkRoslyn"
        OutDir          = $OutDir
        PrecompiledPool = $pool
    }
}

function Get-FastBuildCsprojInfo {
    param($Context, [string]$Name)

    [xml]$xml = Get-Content (Join-Path $Context.Project "$Name.csproj")
    $ns = @{ m = "http://schemas.microsoft.com/developer/msbuild/2003" }
    $defines = (Select-Xml -Xml $xml -XPath "//m:DefineConstants" -Namespace $ns | Select-Object -First 1).Node.InnerText
    $refs = Select-Xml -Xml $xml -XPath "//m:Reference/m:HintPath" -Namespace $ns | ForEach-Object { $_.Node.InnerText }
    $projRefs = Select-Xml -Xml $xml -XPath "//m:ProjectReference/m:Name" -Namespace $ns | ForEach-Object { $_.Node.InnerText }
    return @{ Defines = $defines; Refs = $refs; ProjectRefs = $projRefs }
}

function Resolve-FastBuildPrecompiledRef {
    param($Context, [string]$FileName, [string]$Owner)

    if ($Context.PrecompiledPool.ContainsKey($FileName)) { return $Context.PrecompiledPool[$FileName] }

    foreach ($root in @("Assets\Packages", "Assets\Plugins", "Library\PackageCache")) {
        $full = Join-Path $Context.Project $root
        if (-not (Test-Path $full)) { continue }
        $found = Get-ChildItem $full -Recurse -Filter $FileName -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($found) { return $found }
    }

    Write-Error "$Owner ссылается на '$FileName' (precompiledReferences), но DLL не найдена ни в одном csproj, ни в Assets/Packages, Assets/Plugins, Library/PackageCache. Проверь имя в .asmdef или переустанови пакет."
}

function Resolve-FastBuildAssemblyRef {
    param($Context, [string]$Name, [string]$Owner)

    # GUID-форма ссылки в asmdef: разворачивается по .meta соседнего .asmdef.
    if ($Name -like "GUID:*") {
        $guid = $Name.Substring("GUID:".Length)
        $meta = Get-ChildItem (Join-Path $Context.Project "Assets") -Recurse -Filter *.asmdef.meta -File |
            Where-Object { (Get-Content $_.FullName -Raw) -match "guid:\s*$guid" } |
            Select-Object -First 1
        if (-not $meta) {
            Write-Error "$Owner ссылается на сборку по GUID $guid, но .asmdef с таким GUID не найден под Assets/."
        }
        $Name = (Get-Content ($meta.FullName -replace "\.meta$", "") -Raw | ConvertFrom-Json).name
    }

    $built = Join-Path $Context.OutDir "$Name.dll"
    if (Test-Path $built) { return $built }
    $scriptAsm = Join-Path $Context.Project "Library\ScriptAssemblies\$Name.dll"
    if (Test-Path $scriptAsm) { return $scriptAsm }

    Write-Error "$Owner ссылается на сборку '$Name' (references), но её DLL нет ни в $($Context.OutDir), ни в Library/ScriptAssemblies. Скомпилируй проект в Unity или поправь .asmdef."
}

# Ссылки тестовых сборок строго по .asmdef: тестовые asmdef стоят с overrideReferences: true,
# поэтому недостающая ссылка — ошибка компиляции в Unity. Суперсет референсов её прятал: агент
# рапортовал зелёное, а Test Runner краснел. Из csproj берутся только движковые и рантаймовые
# сборки (Unity даёт их сама, в asmdef их нет).
function Get-FastBuildAsmdefRefs {
    param($Context, [string]$AsmdefPath, $CsprojInfo)

    $asmdef = Get-Content $AsmdefPath -Raw | ConvertFrom-Json
    $owner = "$($asmdef.name).asmdef"
    $refs = @($CsprojInfo.Refs | Where-Object { $_ -like "$($Context.Unity)*" })

    foreach ($reference in @($asmdef.references)) {
        if ($reference) { $refs += Resolve-FastBuildAssemblyRef $Context $reference $owner }
    }

    foreach ($precompiled in @($asmdef.precompiledReferences)) {
        if ($precompiled) { $refs += Resolve-FastBuildPrecompiledRef $Context $precompiled $owner }
    }

    return $refs
}

function Invoke-FastBuildCsc {
    param($Context, [string]$Name, [string[]]$Sources, [string[]]$Refs, [string]$Defines, [switch]$Quiet, [switch]$NoThrow)

    $project = $Context.Project
    $outDir = $Context.OutDir
    $rsp = Join-Path $outDir "$Name.rsp"
    $lines = @(
        "-target:library",
        "-out:$outDir\$Name.dll",
        "-langversion:9",
        "-nologo",
        "-warn:0",
        "-nostdlib",
        "-define:$Defines",
        "-analyzer:`"$project\Assets\Packages\MemoryPack.Generator.1.21.4\analyzers\dotnet\cs\MemoryPack.Generator.dll`"",
        "-analyzer:`"$project\Assets\Framework\Analyzers\AutoDecorators.Generator.dll`""
    )
    foreach ($r in ($Refs | Where-Object { $_ } | Sort-Object -Unique)) { $lines += "-r:`"$r`"" }
    foreach ($s in $Sources) { $lines += "`"$s`"" }
    Set-Content -Path $rsp -Value $lines -Encoding utf8

    # Вывод csc уходит в Write-Host, а не в поток функции: иначе он смешался бы с возвращаемым
    # признаком успеха. Без 2>&1: в PS 5.1 перенаправление stderr нативного процесса даёт
    # NativeCommandError при ErrorActionPreference = Stop, а диагностику csc пишет в stdout.
    & $Context.Dotnet $Context.Csc "@$rsp" | ForEach-Object { if (-not $Quiet) { Write-Host $_ } }

    if ($LASTEXITCODE -ne 0) {
        if ($NoThrow) { return $false }
        Write-Error "Компиляция $Name провалилась."
    }

    if (-not $Quiet) { Write-Host "compiled: $Name" }
    return $true
}

function Get-FastBuildSources {
    param([string]$Root, [string[]]$ExcludePatterns, [hashtable]$SourceOverrides)

    Get-ChildItem -Path $Root -Recurse -Filter *.cs | Where-Object {
        $path = $_.FullName
        -not ($ExcludePatterns | Where-Object { $path -like $_ })
    } | ForEach-Object {
        # Подмена исходника для мутанта: в компиляцию уходит мутированная копия вместо оригинала.
        if ($SourceOverrides -and $SourceOverrides.ContainsKey($_.FullName)) { $SourceOverrides[$_.FullName] }
        else { $_.FullName }
    }
}

# Описание четырёх сборок в одном месте: имя, корень исходников, исключения и способ получить ссылки.
function Get-FastBuildAssemblies {
    param($Context)

    $project = $Context.Project

    return @(
        @{ Name = "Foundation"; Root = Join-Path $project "Assets\Framework\Foundation"; Exclude = @("*\Tests\*", "*\SaveLoad\Editor\*"); Asmdef = $null },
        @{ Name = "Features"; Root = Join-Path $project "Assets\Framework\Features"; Exclude = @("*\Tests\*"); Asmdef = $null },
        @{ Name = "Foundation.Tests"; Root = Join-Path $project "Assets\Framework\Foundation\Tests"; Exclude = @(); Asmdef = Join-Path $project "Assets\Framework\Foundation\Tests\Foundation.Tests.asmdef" },
        @{ Name = "Features.Tests"; Root = Join-Path $project "Assets\Framework\Features\Tests"; Exclude = @(); Asmdef = Join-Path $project "Assets\Framework\Features\Tests\Features.Tests.asmdef" }
    )
}

function Invoke-FastBuild {
    param(
        $Context,
        # Подмножество сборок; по умолчанию все четыре. Мутанту нужна только его собственная:
        # мутации живут в телах методов, метаданные сборки не меняются, и зависимые DLL остаются валидными.
        [string[]]$Only,
        [hashtable]$SourceOverrides,
        [switch]$Quiet,
        [switch]$NoThrow
    )

    foreach ($assembly in (Get-FastBuildAssemblies $Context)) {
        if ($Only -and ($Only -notcontains $assembly.Name)) { continue }

        $info = Get-FastBuildCsprojInfo $Context $assembly.Name
        $refs = if ($assembly.Asmdef) {
            Get-FastBuildAsmdefRefs $Context $assembly.Asmdef $info
        }
        else {
            @($info.Refs) + @($info.ProjectRefs | ForEach-Object { Resolve-FastBuildAssemblyRef $Context $_ "$($assembly.Name).csproj" })
        }

        $sources = Get-FastBuildSources $assembly.Root $assembly.Exclude $SourceOverrides
        $built = Invoke-FastBuildCsc $Context $assembly.Name $sources $refs $info.Defines -Quiet:$Quiet -NoThrow:$NoThrow
        if (-not $built) { return $false }
    }

    return $true
}

# --- Раннер -------------------------------------------------------------------------------------

# Собираем тем же csc, что и тестовые сборки: .NET SDK на машине не требуется,
# нужен только установленный .NET runtime для запуска (dotnet.exe).
function Build-FastTestRunner {
    param($Context)

    $outDir = $Context.OutDir
    $runner = Join-Path $outDir "UnitTestRunner.dll"
    $netRuntimeDir = Get-ChildItem "$env:ProgramFiles\dotnet\shared\Microsoft.NETCore.App" -Directory |
        Where-Object { $_.Name -like "8.*" } | Sort-Object Name | Select-Object -Last 1 -ExpandProperty FullName
    if (-not $netRuntimeDir) { Write-Error ".NET 8 runtime не найден в $env:ProgramFiles\dotnet." }

    # Референсим весь runtime-каталог: без SDK нет reference-assemblies, а выбирать
    # фасады поштучно хрупко (type forwards тянут System.Private.CoreLib и соседей).
    # GetAssemblyName отсекает нативные DLL (coreclr, clrjit и т.п.).
    $runnerRefs = Get-ChildItem $netRuntimeDir -Filter "*.dll" | ForEach-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $_.FullName
        }
        catch { }
    }

    $runnerRsp = Join-Path $outDir "UnitTestRunner.rsp"
    $runnerLines = @("-target:exe", "-out:$runner", "-langversion:latest", "-nologo", "-nostdlib")
    foreach ($r in $runnerRefs) { $runnerLines += "-r:`"$r`"" }
    foreach ($s in (Get-ChildItem (Join-Path $PSScriptRoot "UnitTestRunner") -Filter *.cs)) { $runnerLines += "`"$($s.FullName)`"" }
    Set-Content -Path $runnerRsp -Value $runnerLines -Encoding utf8

    # Вывод csc уходит в Write-Host, а не в поток функции: иначе он смешался бы с возвращаемым путём.
    & $Context.Dotnet $Context.Csc "@$runnerRsp" | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) { Write-Error "Сборка UnitTestRunner провалилась." }

    Set-Content -Path (Join-Path $outDir "UnitTestRunner.runtimeconfig.json") -Encoding utf8 -Value @'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" }
  }
}
'@

    return $runner
}

function Get-FastTestRunnerArgs {
    param($Context, [string]$Journal)

    $project = $Context.Project
    $newtonsoft = Resolve-FastBuildPrecompiledRef $Context "Newtonsoft.Json.dll" "runner probes"
    $nunit = Resolve-FastBuildPrecompiledRef $Context "nunit.framework.dll" "runner probes"

    $probes = @(
        $Context.OutDir,
        (Join-Path $project "Library\ScriptAssemblies"),
        (Join-Path $project "Assets\Packages"),
        # Тесты, которые читают атрибуты рефлексией, тянут и Inspector-атрибуты полей.
        (Join-Path $project "Assets\Plugins"),
        (Split-Path $newtonsoft),
        (Split-Path $nunit),
        (Join-Path $Context.Unity "Data\Managed\UnityEngine"),
        (Join-Path $Context.Unity "Data\Managed")
    )

    $runnerArgs = @()
    foreach ($probe in $probes) { $runnerArgs += @("--probe", $probe) }
    if ($Journal) { $runnerArgs += @("--journal", $Journal) }
    $runnerArgs += @((Join-Path $Context.OutDir "Foundation.Tests.dll"), (Join-Path $Context.OutDir "Features.Tests.dll"))

    return $runnerArgs
}

# TimeoutSeconds = 0 — прогон в текущей консоли с живым выводом (обычный fast-tests).
# Больше нуля — отдельный процесс с убийством по таймауту: мутация условия цикла умеет
# превратить тест в вечный, и такой мутант обязан считаться убитым, а не подвесить прогон.
function Invoke-FastTestRunner {
    param($Context, [string]$Runner, [string[]]$RunnerArgs, [int]$TimeoutSeconds = 0)

    if ($TimeoutSeconds -le 0) {
        dotnet $Runner @RunnerArgs | ForEach-Object { Write-Host $_ }
        return @{ ExitCode = $LASTEXITCODE; TimedOut = $false; Output = "" }
    }

    $stdout = Join-Path $Context.OutDir "runner.out.txt"
    $stderr = Join-Path $Context.OutDir "runner.err.txt"
    $quoted = @("`"$Runner`"") + ($RunnerArgs | ForEach-Object { "`"$_`"" })

    $process = Start-Process -FilePath "dotnet" -ArgumentList $quoted -NoNewWindow -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr

    # Обращение к Handle кэширует хэндл процесса до его завершения. Без него Start-Process -PassThru
    # отдаёт пустой ExitCode, и любой прогон читался бы как «тесты зелёные».
    $null = $process.Handle

    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill() } catch { }
        $process.WaitForExit()
        return @{ ExitCode = -1; TimedOut = $true; Output = "" }
    }

    $output = ""
    if (Test-Path $stdout) { $output = (Get-Content $stdout -Raw) }
    return @{ ExitCode = $process.ExitCode; TimedOut = $false; Output = $output }
}
