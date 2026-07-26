# AJ AI Brain — auto-loaded session rules

@START-HERE.md

## Maintaining this repo (the Brain itself)

- Every file edit in this repo triggers a PostToolUse hook ([`.claude/settings.json`](.claude/settings.json)
  → [`tools/verify-consistency-hook.ps1`](tools/verify-consistency-hook.ps1)) that runs the full
  consistency check — skill frontmatter, markdown link targets, scripts README sync. If it reports
  drift, fix the drift in the same turn, before finishing.
- Log structural changes (new skill, split file, new/retired script) in
  [`knowledge/brain-log.md`](knowledge/brain-log.md), 1–3 lines each.
- **Never bulk-edit files here with PowerShell `Get-Content`/`Set-Content`.** Windows PowerShell 5.1 reads
  UTF-8-without-BOM as ANSI, so a read-modify-write round trip double-encodes every em dash, ✓ and quote in
  the file. This corrupted 41 files on 2026-07-26 before being caught. For any scripted multi-file edit use
  `[System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)` and
  `[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))`. The Edit/Write
  tools are always safe. Tell-tale sign something went wrong: a `git diff --stat` far larger than the edit
  you actually made.
- This repo doubles as an installable Claude Code **plugin** — manifest in `.claude-plugin/`, install
  steps in [`SETUP.md`](SETUP.md) step 1. Keep `skills/` at the repo root; that's where the plugin
  loader finds them.
