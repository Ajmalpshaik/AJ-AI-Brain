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

**Before writing any new C#, look for an existing fragment first** —
`node tools/fragment-index.mjs --find <word>` searches all 267 by purpose and input field and shows each
one's proven status; `--show <path>` prints what a given fragment needs filled in. That is one lookup
instead of reading `scripts/README.md` end to end, which is the read that gets skipped when it feels
expensive — and skipping it is how fresh C# gets written for a job a proven fragment already covered.

**When you don't know the word to search for, ask in plain English instead** —
`semantic-index\ask-brain-hybrid.cmd "how do I stop ducts overlapping the ceiling"` searches all 310 files
(`skills/`, `knowledge/`, `scripts/`, the root docs, and the native-tools reference) by *meaning* as well as by exact words, and
returns real file paths. It exists because `fragment-index.mjs` only reads `scripts/*.cs` — it structurally
cannot surface the skill or knowledge note that answers a question, and a keyword tool needs you to already
know the keyword. Use whichever fits: keyword when you know the term, this when you only know the job.
Each result shows `found by: meaning #3 + words #1` — **high in both is the strong signal; only one firing
means check before trusting it.** Setup and limits: [`semantic-index/README.md`](semantic-index/README.md).

**Read the top 3–5, never just #1.** Measured 2026-08-06 against 24 questions written by independent
testers in a modeller's own words: #1 was right or useful in about three-quarters, and wrong in the rest —
the correct file was usually still in the top 3. It fails on **site vocabulary the files don't use**:
"add 4 more floor levels" returns `create-floor.cs` (the slab creator) instead of `create-levels.cs`, and
"how many light fitting" matches "light hazard". [`knowledge/glossary.md`](knowledge/glossary.md) is the
site-word → Revit-word map; when a search looks off, say the Revit word and re-run.

> **It is a snapshot, not a live index.** After adding or changing anything in `skills/`, `knowledge/`,
> `scripts/` or the root docs, run `semantic-index\index-brain.cmd` — **2–4 s**, since it only re-reads
> what changed (a full ~80 s rebuild triggers itself when the chunking rules change). Treat this like updating
> `scripts/README.md`: part of finishing the edit, not a separate chore. **If a search prints a
> `STALE INDEX` banner, rebuild before trusting the results** — it is comparing file contents against
> what the index was built from, so it is telling you the Brain has moved on since. It warns rather than
> blocks, so the results underneath are still the old picture.

## Maintaining this repo (the Brain itself)

- Every file edit in this repo triggers a PostToolUse hook ([`.claude/settings.json`](.claude/settings.json)
  → [`tools/verify-consistency-hook.mjs`](tools/verify-consistency-hook.mjs)) that runs the full
  consistency check — skill frontmatter, markdown link targets, scripts README sync, skill coverage in
  the entry docs, AGENT-SPEC's fragment counts, text encoding, the `// SOURCE:` cross-references
  inside script fragments, and the "searches all N files" semantic-index coverage claims. If it reports drift, fix the drift in the same turn, before finishing. The hook runs the **Node** checker on purpose: the PowerShell wrapper
  it replaced fired only on Windows and silently did nothing everywhere else, so a whole session on
  Claude Code for web got no checking at all (2026-08-04). If Node isn't on PATH on some machine, run
  [`tools/verify-consistency.ps1`](tools/verify-consistency.ps1) by hand instead — same eight checks.
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
