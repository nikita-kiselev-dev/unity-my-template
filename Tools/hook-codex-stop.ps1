# Codex launches multiple matching Stop handlers concurrently. The project gates depend on order,
# so Codex registers this single orchestrator and it forwards the same payload to each existing hook.
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$stdin = [Console]::In.ReadToEnd()

if ($stdin) {
    try {
        if ((ConvertFrom-Json $stdin).stop_hook_active) { exit 0 }
    }
    catch {}
}

$hooks = @(
    "hook-fast-tests.ps1",
    "hook-tdd-check.ps1",
    "hook-naming-check.ps1",
    "hook-interaction-check.ps1",
    "hook-generator-hash.ps1",
    "hook-docs-coverage.ps1",
    "hook-scope-check.ps1",
    "hook-ticket-check.ps1",
    "hook-acceptance-check.ps1"
)

foreach ($hook in $hooks) {
    $path = Join-Path $project "Tools\$hook"
    $stdin | & powershell -NoProfile -ExecutionPolicy Bypass -File $path
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

exit 0
