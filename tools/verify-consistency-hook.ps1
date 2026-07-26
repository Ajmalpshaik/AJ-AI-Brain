# PostToolUse hook wrapper around verify-consistency.ps1 (wired up in .claude/settings.json).
# Quiet exit 0 when the Brain is clean; on drift, exit 2 with the checker's report on stderr so the
# AI session sees it immediately and fixes the drift in the same turn (exit 2 is the only PostToolUse
# exit code whose stderr is fed back to the model; the checker's own exit 1 would reach the user only).
$ErrorActionPreference = 'Continue'
$checker = Join-Path $PSScriptRoot 'verify-consistency.ps1'
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File $checker 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Brain consistency drift detected (tools/verify-consistency.ps1):`n$out")
    exit 2
}
exit 0
