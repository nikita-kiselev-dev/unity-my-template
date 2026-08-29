# Гейт актуальности документации: сопоставляет изменённые файлы со статьями Obsidian-vault-а.
# Две проверки:
#   1. Обратный индекс source_paths: какие статьи описывают тронутые файлы.
#   2. Новые публичные типы в Foundation/, не упомянутые ни в одной статье.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список путей (относительно корня проекта) вместо git diff — для проверок и тестов.
    [string[]]$Files,
    # Корень vault-а; переопределяется, чтобы прогнать проверку против среза документации.
    [string]$DocsRoot,
    # База сравнения: и для списка изменений, и для ответа «этот тип новый».
    [string]$BaseRef = "HEAD",
    # Минимальное число файлов-потребителей, при котором класс/структура/enum считается механизмом.
    [int]$UsageThreshold = 3
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $DocsRoot) { $DocsRoot = Join-Path $project "unity-my-template-docs" }

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

# --- Статьи и их source_paths --------------------------------------------------------------

$articleDirs = @("Architecture", "Recipes") | ForEach-Object { Join-Path $DocsRoot $_ } | Where-Object { Test-Path $_ }
if (-not $articleDirs) { exit 0 }

$articles = @()
foreach ($file in (Get-ChildItem $articleDirs -Filter *.md -File)) {
    $lines = Get-Content $file.FullName
    $sources = @()
    $inBlock = $false
    foreach ($line in $lines) {
        if ($line -match "^source_paths:\s*$") { $inBlock = $true; continue }
        if ($inBlock) {
            if ($line -match "^\s+-\s+(.+?)\s*$") { $sources += (Normalize $Matches[1].Trim('"', "'")) }
            elseif ($line -match "^\S") { $inBlock = $false }
        }
    }
    $articles += [pscustomobject]@{
        Name    = Normalize $file.FullName.Substring($DocsRoot.Length + 1)
        Sources = $sources
        Text    = ($lines -join "`n")
    }
}

# --- Изменённые файлы ----------------------------------------------------------------------

if (-not $Files) {
    $Files = @(Invoke-Git @("diff", "--name-only", $BaseRef, "--")) +
             @(Invoke-Git @("ls-files", "--others", "--exclude-standard"))
}

$docsPrefix = Normalize ((Normalize $DocsRoot) + "/")
if ($docsPrefix.StartsWith((Normalize $project))) {
    $docsPrefix = $docsPrefix.Substring((Normalize $project).Length).TrimStart("/")
}

# Сериализованные ассеты и картинки документацию не описывают — их правки только шумят.
$trackedExtensions = @(".cs", ".asmdef", ".ps1", ".yml", ".yaml")

$changed = @($Files |
    ForEach-Object { Normalize $_ } |
    Where-Object { $_ -and -not $_.StartsWith($docsPrefix, [StringComparison]::OrdinalIgnoreCase) } |
    Where-Object { $trackedExtensions -contains [System.IO.Path]::GetExtension($_).ToLowerInvariant() } |
    Sort-Object -Unique)

if (-not $changed) { exit 0 }

$status = @{}
foreach ($file in $changed) {
    $status[$file] = if (-not (Test-Path (Join-Path $project $file))) { "D" }
    else {
        Invoke-Git @("cat-file", "-e", "${BaseRef}:$file") | Out-Null
        if ($LASTEXITCODE -eq 0) { "M" } else { "A" }
    }
}

# --- Проверка 1: тронуты файлы из source_paths ----------------------------------------------

$affected = @()
foreach ($article in $articles) {
    $hits = @()
    foreach ($source in $article.Sources) {
        # Корень слоя (Assets/Framework/Foundation/) в source_paths означает «статья про слой
        # целиком»: срабатывание на каждом новом файле проекта — гарантированный шум.
        if ($source -match "^Assets/Framework/[^/]+/$") { continue }

        foreach ($file in $changed) {
            $match = if ($source.EndsWith("/")) {
                # Папка в source_paths описывает состав каталога: её задевает появление и
                # исчезновение файлов, а не правка внутри уже описанного файла.
                $status[$file] -ne "M" -and $file.StartsWith($source, [StringComparison]::OrdinalIgnoreCase)
            }
            else {
                $file.Equals($source, [StringComparison]::OrdinalIgnoreCase)
            }
            if ($match) { $hits += "$($status[$file]) $file" }
        }
    }
    if ($hits) {
        $affected += [pscustomobject]@{ Name = $article.Name; Files = @($hits | Sort-Object -Unique) }
    }
}

# --- Проверка 2: новые публичные типы в Foundation без упоминания в статьях -------------------

$typeRegex = "^\s*public\s+(?:(?:sealed|abstract|static|partial|readonly|ref|unsafe|new)\s+)*(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)"

function Get-PublicTypes([string[]]$lines) {
    $found = @{}
    foreach ($line in $lines) {
        if ($line -match $typeRegex) { $found[$Matches[2]] = $Matches[1] }
    }
    return $found
}

$candidates = @($changed | Where-Object {
        $_ -like "Assets/Framework/Foundation/*" -and
        $_.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase) -and
        $_ -notlike "*/Tests/*" -and $_ -notlike "*/Editor/*" -and
        (Test-Path (Join-Path $project $_))
    })

$undocumented = @()
if ($candidates) {
    $articleText = ($articles | ForEach-Object { $_.Text }) -join "`n"
    $frameworkFiles = $null

    foreach ($file in $candidates) {
        $current = Get-PublicTypes (Get-Content (Join-Path $project $file))
        if ($current.Count -eq 0) { continue }

        $headContent = Invoke-Git @("show", "${BaseRef}:$file")
        # Файла нет в базе (новый или untracked) — все его типы считаем новыми.
        $previous = if ($LASTEXITCODE -eq 0 -and $headContent) { Get-PublicTypes $headContent } else { @{} }

        foreach ($type in $current.Keys) {
            if ($previous.ContainsKey($type)) { continue }
            if ($articleText -match "(?<![\w])$([regex]::Escape($type))(?![\w])") { continue }

            $kind = $current[$type]
            $usage = $null
            if ($kind -ne "interface") {
                if ($null -eq $frameworkFiles) {
                    $frameworkFiles = Get-ChildItem (Join-Path $project "Assets\Framework") -Recurse -Filter *.cs -File
                }
                $declaring = Join-Path $project ($file -replace "/", "\")
                $usage = @($frameworkFiles |
                        Where-Object { $_.FullName -ne $declaring } |
                        Select-String -Pattern "(?<![\w])$([regex]::Escape($type))(?![\w])" -List).Count
                # Порог отсекает вспомогательные типы: механизм виден за пределами пары файлов.
                if ($usage -lt $UsageThreshold) { continue }
            }

            $undocumented += [pscustomobject]@{ Type = $type; Kind = $kind; File = $file; Usage = $usage }
        }
    }
}

# --- Отчёт -----------------------------------------------------------------------------------

if (-not $affected -and -not $undocumented) { exit 0 }

$report = New-Object System.Collections.Generic.List[string]

if ($affected) {
    $report.Add("Changed files are covered by $($affected.Count) article(s) - verify they are still accurate:")
    foreach ($item in $affected) {
        $report.Add("  $($item.Name)")
        foreach ($file in ($item.Files | Select-Object -First 5)) { $report.Add("      $file") }
        if ($item.Files.Count -gt 5) { $report.Add("      ... and $($item.Files.Count - 5) more") }
    }
}

if ($undocumented) {
    if ($report.Count -gt 0) { $report.Add("") }
    $report.Add("New public types in Foundation/ with no mention in Architecture/ or Recipes/:")
    foreach ($item in $undocumented) {
        $usage = if ($null -ne $item.Usage) { ", used in $($item.Usage) file(s)" } else { "" }
        $report.Add("  $($item.Type) ($($item.Kind)$usage) - $($item.File)")
    }
}

$report.Add("")
$report.Add("Update the article(s), or state explicitly in the answer why no update is needed.")

$report -join "`n" | Write-Output
exit 1
