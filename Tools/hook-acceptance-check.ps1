# Stop-хук: прогоняет Tools/acceptance-check.ps1 и возвращает ход, если тикет ждёт только
# ответа пользователя — агент обязан спросить через AskUserQuestion, а не закончить ход молча.
# Спрашивают только про тикеты, которые правили в этой сессии: их список хук копит сам и
# передаёт скрипту через -Files (раздел «Тикеты этой сессии»).
# В хэш идёт не только отчёт, но и отпечаток рабочего дерева: после ответа «нет» и доработки
# набор пунктов совпадает с прежним, и по одному отчёту вопрос больше никогда не задался бы.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

$payload = $null
$stdin = [Console]::In.ReadToEnd()
if ($stdin) {
    try { $payload = ConvertFrom-Json $stdin }
    catch {}
}
# Ход, уже продолженный этим хуком, вопрос задал — второй блок задал бы его дважды.
if ($payload -and $payload.stop_hook_active) { exit 0 }

$script = Join-Path $project "Tools\acceptance-check.ps1"
if (-not (Test-Path $script)) { exit 0 }

$stampDir = Join-Path $project ".agent-state\AcceptanceCheck"
$stamp = Join-Path $stampDir ".last-report"

# ErrorActionPreference = Stop + stderr нативного процесса в PS 5.1 = NativeCommandError на ровном месте.
function Invoke-Git([string[]]$gitArgs) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { return @(& git -C $project @gitArgs 2>$null) }
    finally { $ErrorActionPreference = $previous }
}

# --- Тикеты этой сессии ---------------------------------------------------------------------

# Спрашивать про приёмку имеет смысл только по той работе, которую вели в этом чате: тикетов
# In Progress бывает три плюс эпики, и вопрос про чужой тикет пользователю нечем закрыть.
# Признак «работа шла тут» — файл тикета меняли в этой сессии: статус, чекбоксы и updated
# правятся по ходу работы.
$tasksPrefix = "unity-my-template-docs/Tasks/"

# Предсуществующие грязные тикеты (правки прошлой сессии, ещё не закоммиченные) — не наша работа,
# пока их не тронули; отсев по mtime тот же, что у scope-check.
$preexisting = @{}
$baseline = Join-Path $project ".agent-state\ScopeCheck\baseline.txt"
if (Test-Path $baseline) {
    foreach ($line in (Get-Content $baseline)) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith("#")) { continue }
        $parts = $value.Split("|", 2)
        if ($parts.Count -eq 2) { $preexisting[($parts[1] -replace "\\", "/").Trim()] = $parts[0] }
    }
}

$touched = @(@(Invoke-Git @("diff", "--name-only", "HEAD")) + @(Invoke-Git @("ls-files", "--others", "--exclude-standard")) |
    ForEach-Object { ($_ -replace "\\", "/").Trim() } |
    Where-Object { $_.StartsWith($tasksPrefix) -and $_.EndsWith(".md") } |
    Where-Object {
        if (-not $preexisting.ContainsKey($_)) { return $true }
        $full = Join-Path $project ($_ -replace "/", "\")
        $ticks = if (Test-Path $full) { (Get-Item $full).LastWriteTimeUtc.Ticks.ToString() } else { "-" }
        return $ticks -ne $preexisting[$_]
    })

# Журнал накопительный: тикет, закоммиченный пользователем в середине сессии, из git diff уходит,
# а работа по нему шла. Ключ — session_id, а не baseline: SessionStart-хук срабатывает и на
# компакции контекста, и снапшот, переписанный в середине сессии, обнулил бы список.
$ledgerFile = Join-Path $stampDir "session-tickets.txt"
$sessionKey = if ($payload -and $payload.session_id) { [string]$payload.session_id } else { "unknown" }

$ledger = @()
if (Test-Path $ledgerFile) {
    $lines = @(Get-Content $ledgerFile)
    if ($lines.Count -gt 0 -and $lines[0].Trim() -eq "# session: $sessionKey") {
        $ledger = @($lines | Select-Object -Skip 1 | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
}

$ledger = @(@($ledger) + @($touched) | Sort-Object -Unique)
New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $ledgerFile -Value (@("# session: $sessionKey") + $ledger) -Encoding utf8

if (-not $ledger) {
    if (Test-Path $stamp) { Remove-Item $stamp -Force }
    exit 0
}

# Список идёт файлом, а не -Files: powershell.exe -File массив не передаёт — второй и
# последующие пути молча теряются, и вопрос задавался бы только про первый тикет.
$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $script -FilesFrom $ledgerFile 2>&1
$found = $LASTEXITCODE -eq 1

if (-not $found) {
    if (Test-Path $stamp) { Remove-Item $stamp -Force }
    exit 0
}

$report = ($output | ForEach-Object { $_.ToString() }) -join "`n"

# Untracked-файлы в git diff не видны, поэтому доработка «только новыми файлами» иначе
# не сдвинула бы отпечаток. .agent-state/, Temp/ и .obsidian/ пишет не агент — они дали бы шум.
$untracked = @(Invoke-Git @("ls-files", "--others", "--exclude-standard") |
    Where-Object { $_ -and -not $_.StartsWith(".agent-state/") -and -not $_.StartsWith("Temp/") -and $_ -notmatch "\.obsidian/" } |
    ForEach-Object {
        $path = Join-Path $project $_
        if (Test-Path $path) {
            $item = Get-Item $path
            "$_|$($item.Length)|$($item.LastWriteTimeUtc.Ticks)"
        }
    })

$fingerprint = (@($report) + @(Invoke-Git @("diff", "HEAD")) + $untracked) -join "`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($fingerprint)
$hash = [System.BitConverter]::ToString([System.Security.Cryptography.MD5]::Create().ComputeHash($bytes))

if ((Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $hash) { exit 0 }

New-Item -ItemType Directory -Force $stampDir | Out-Null
Set-Content -Path $stamp -Value $hash -Encoding utf8

# Латиница намеренно: PS 5.1 пишет stderr в OEM-кодировке, кириллица дойдёт мусором.
[Console]::Error.WriteLine(@"
acceptance-check: work is done, only user-verification items are left.
$report
Do not end the turn silently. Ask via AskUserQuestion with exactly these two options
(the tool requires at least two; findings go through the built-in "Other" free text):
  1. Yes, everything is fine - close the ticket
  2. Not verified yet - I will come back to it
On option 1: tick every @user item, set status: Done, bump the updated field.
On option 2: change nothing, leave the ticket In Progress, end the turn.
On "Other" (free text): add the findings to the ticket as new items without the @user
marker and keep working. Do not add a "No" option - "Other" already covers it.
"@)
exit 2
