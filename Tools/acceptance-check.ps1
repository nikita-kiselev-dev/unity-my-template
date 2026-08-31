# Гейт приёмки: находит тикеты In Progress, у которых незакрытыми остались ТОЛЬКО пункты
# с маркером @user — то есть работа агента доведена до конца и дальше нужен ответ пользователя.
# Технический незакрытый пункт рядом означает, что работа не закончена, и тикет молчит.
# Какие тикеты смотреть, решает вызывающий: хук передаёт -FilesFrom со списком тикетов текущей
# сессии, потому что спрашивать про чужую работу бессмысленно — пользователю нечем её закрыть.
# Exit 0 — тишина, exit 1 — есть находки (текст в stdout). Вывод латиницей: его печатает
# Stop-хук в stderr, а PS 5.1 отдаёт stderr в OEM-кодировке и кириллица дойдёт мусором.
param(
    # Явный список тикетов вместо полного скана: хук передаёт только те, которые трогали
    # в этой сессии. Без ключа скан идёт по всему TasksRoot — это отладочный прогон.
    [string[]]$Files,
    # Тот же список файлом, по строке на путь (#-комментарии допустимы). Нужен потому, что
    # powershell.exe -File массив в -Files не передаёт: лишние значения молча теряются.
    [string]$FilesFrom,
    # Корень тикетов; переопределяется в искусственных проверках.
    [string]$TasksRoot
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
if (-not $TasksRoot) { $TasksRoot = Join-Path $project "unity-my-template-docs\Tasks" }
if (-not (Test-Path $TasksRoot)) { exit 0 }

if ($FilesFrom) {
    if (-not (Test-Path $FilesFrom)) { exit 0 }
    $Files = @(@($Files) + @(Get-Content $FilesFrom | ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and -not $_.StartsWith("#") }))
    # Список задан, но пуст — трогали ноль тикетов, и это не повод сканировать всё подряд.
    if (-not $Files) { exit 0 }
}

if ($Files) {
    # Путь может прийти относительным (от корня проекта) или полным; несуществующий молча
    # выпадает — тикет мог быть удалён или переименован после того, как попал в список.
    $tickets = @($Files |
        ForEach-Object {
            $path = $_.Trim()
            if (-not $path) { return }
            if ([System.IO.Path]::IsPathRooted($path)) { $path } else { Join-Path $project ($path -replace "/", "\") }
        } |
        Where-Object { $_ -and (Test-Path $_ -PathType Leaf) } |
        ForEach-Object { Get-Item $_ } |
        Sort-Object -Property FullName -Unique)
}
else {
    $tickets = @(Get-ChildItem $TasksRoot -Recurse -Filter *.md -File)
}

$found = @()

foreach ($file in $tickets) {
    $lines = @(Get-Content $file.FullName)

    $inProgress = $false
    foreach ($line in ($lines | Select-Object -First 30)) {
        if ($line -match "^status:\s*[""']?In Progress[""']?\s*$") {
            $inProgress = $true
            break
        }
    }
    if (-not $inProgress) { continue }

    $pending = @()
    $technical = 0
    # Чеклист внутри ``` — это пример в тексте тикета, а не пункт работы.
    $inFence = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) { continue }
        if ($line -notmatch '^\s*-\s+\[\s\]\s*(.+)$') { continue }

        $text = $Matches[1].Trim()
        if ($text -match '^@user\b') { $pending += $text } else { $technical++ }
    }

    if ($pending.Count -eq 0 -or $technical -gt 0) { continue }

    $found += [PSCustomObject]@{
        Ticket = $file.BaseName
        Items  = $pending
    }
}

if (-not $found) { exit 0 }

# Текст пунктов русский, а отчёт уходит в stderr в OEM-кодировке — печатаются только имя
# тикета и число пунктов, сами формулировки агент читает в файле тикета.
foreach ($ticket in $found) {
    Write-Output "$($ticket.Ticket): $($ticket.Items.Count) unchecked item(s), all marked @user"
}

Write-Output "Ask the user whether everything is fine and the ticket can be closed."
Write-Output "Use structured input when available; otherwise ask the same choices in plain text:"
Write-Output "  'Yes, close the ticket' -> tick the @user items, set status: Done, bump updated."
Write-Output "  'Not verified yet' -> change nothing, leave the ticket In Progress."
Write-Output "Free-text findings -> add them as items and keep working."
exit 1
