# AJ AI Brain — auto-loaded session rules

@START-HERE.md

## Maintaining this repo (the Brain itself)

- Every file edit in this repo triggers a PostToolUse hook ([`.claude/settings.json`](.claude/settings.json)
  → [`tools/verify-consistency-hook.ps1`](tools/verify-consistency-hook.ps1)) that runs the full
  consistency check — skill frontmatter, markdown link targets, scripts README sync. If it reports
  drift, fix the drift in the same turn, before finishing.
- Log structural changes (new skill, split file, new/retired script) in
  [`knowledge/brain-log.md`](knowledge/brain-log.md), 1–3 lines each.
- This repo doubles as an installable Claude Code **plugin** — manifest in `.claude-plugin/`, install
  steps in [`SETUP.md`](SETUP.md) step 1. Keep `skills/` at the repo root; that's where the plugin
  loader finds them.
