# AJ AI Brain — auto-loaded session rules

@START-HERE.md

## Know the real state before acting

A SessionStart hook runs [`tools/brain-status.mjs`](tools/brain-status.mjs) and prints the Brain's true
state — skill/fragment counts, how much of the library has actually been run against a real model, open
items, and whether anything has drifted. **It is computed from disk every time, never read from a stored
summary**, because the recurring failure in this repo has been its own documentation quietly getting
ahead of reality (README said 8 skills against 9; AGENT-SPEC said 206 fragments against 264).

Treat the "no status either way" number as unproven, not as broken: **warn and keep working.** Run a
never-verified fragment on one element, check the real result, then use it for the batch — say plainly
that's what you're doing. It is never a reason to refuse the job. Run
`node tools/brain-status.mjs --capabilities` when you need what this Brain can actually do.

## Maintaining this repo (the Brain itself)

- Every file edit in this repo triggers a PostToolUse hook ([`.claude/settings.json`](.claude/settings.json)
  → [`tools/verify-consistency-hook.mjs`](tools/verify-consistency-hook.mjs)) that runs the full
  consistency check — skill frontmatter, markdown link targets, scripts README sync, skill coverage in
  the entry docs, AGENT-SPEC's fragment counts, text encoding, and the `// SOURCE:` cross-references
  inside script fragments. If it reports drift, fix the drift in the same turn, before finishing. The hook runs the **Node** checker on purpose: the PowerShell wrapper
  it replaced fired only on Windows and silently did nothing everywhere else, so a whole session on
  Claude Code for web got no checking at all (2026-08-04). If Node isn't on PATH on some machine, run
  [`tools/verify-consistency.ps1`](tools/verify-consistency.ps1) by hand instead — same seven checks.
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
