# AJ AI Brain — auto-loaded session rules

@START-HERE.md

## Where the open work is

**If [`docs/HANDOVER.md`](docs/HANDOVER.md) exists, read it before starting.** It carries what the last
session did and what is still outstanding — the one thing a fresh session cannot work out from disk,
because the repo shows its *state* but not its *direction*. It lives in `docs/`, which is deliberately
outside the search index, so nothing will surface it for you: this line is how you find it.

Treat it the way you treat any dated note here — the SessionStart hook's numbers are computed live and
win over anything written in a file. If the handover is finished, say so and delete it rather than
leaving a stale to-do list that reads as current.

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
`node tools/fragment-index.mjs --find <word>` searches all 293 by purpose and input field and shows each
one's proven status; `--show <path>` prints what a given fragment needs filled in. That is one lookup
instead of reading `scripts/README.md` end to end, which is the read that gets skipped when it feels
expensive — and skipping it is how fresh C# gets written for a job a proven fragment already covered.

**When you don't know the word to search for, ask in plain English instead** —
`semantic-index\ask-brain-hybrid.cmd "how do I stop ducts overlapping the ceiling"` searches all 464 files
(`skills/`, `knowledge/`, `scripts/`, the root docs, and the native-tools reference) by *meaning* as well as by exact words, and
returns real file paths. It exists because `fragment-index.mjs` only reads `scripts/*.cs` — it structurally
cannot surface the skill or knowledge note that answers a question, and a keyword tool needs you to already
know the keyword. Use whichever fits: keyword when you know the term, this when you only know the job.
Each result shows `found by: meaning #3 + words #1`. **You no longer have to judge that yourself** — the
search prints a `CAUTION` line when a file was found by one signal alone. Measured 2026-08-20 over 60 real
questions, that never happens in a normal top-5 answer (**0 of 300**), rises to 1.3% at top-20 and 27.6% at
top-50: the fusion is sinking one-sided hits on purpose, so silence there is the system working, and the
warning only matters once you ask for more results than usual. Setup and limits:
[`semantic-index/README.md`](semantic-index/README.md).

**Read the top 3–5, never just #1.** The one place this is measured is
[`semantic-index/score-history.md`](semantic-index/score-history.md) — every line stamped since
2026-08-20 with the model and settings that produced it. **Last run, on the 28-row set (2026-08-21):
5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20 of 28 answers retrievable at all, MRR 0.267.** The set
doubled that day, so those numbers are not comparable to any 14-row line above them. Quote that file, never a remembered figure:
three different numbers (75%, 60%, 29%) were once in circulation here because the earlier scores recorded
no model, no chunk size and no corpus size, and the Brain grew underneath them. It fails on **site vocabulary the files don't use**:
"add 4 more floor levels" returns `create-floor.cs` (the slab creator) instead of `create-levels.cs`, and
"how many light fitting" matches "light hazard". [`knowledge/glossary.md`](knowledge/glossary.md) is the
site-word → Revit-word map; when a search looks off, say the Revit word and re-run.

> **It rebuilds itself — but only for edits made inside a session** (2026-08-13).
> [`tools/reindex-mark.mjs`](tools/reindex-mark.mjs) flags any file edit and
> [`tools/reindex-run.mjs`](tools/reindex-run.mjs) does one rebuild at the end of the turn, so editing
> the Brain here no longer needs a separate step. **Edits made outside a session still go unnoticed** —
> a git checkout, a branch switch, a file changed in an editor — so `semantic-index\index-brain.cmd`
> stays for exactly those: **2–4 s**, since it only re-reads what changed (a full ~80 s rebuild triggers
> itself when the chunking rules change). **If a search prints a `STALE INDEX` banner, rebuild before
> trusting the results** — it compares file contents against what the index was built from, so it is
> telling you the Brain has moved on since. It warns rather than blocks, so the results underneath are
> still the old picture.

## When a script errors, or Revit changes version

**Ajmal is not a coder** (his own words, 2026-08-20: *"am not a coder or programmer i dont know the
programing side anything but i know how to work in revit"*). Every programming decision is yours to
make — do not ask him to choose between technical options he has no way to judge. Ask him Revit
questions; make the code decisions yourself and tell him what you did.

He named two problems. Both have an answer that already exists:

- **"It errors on a newer Revit."** `tools\check-scripts.cmd` compile-checks all 388 fragments — **and,
  since 2026-08-24, the C# the MCP server builds at run time** — against every Revit installed on the
  PC **without opening Revit**, in about a minute. Offer it the moment a version change is mentioned.
  It catches the whole "worked in 2020, errors in 2024" class before he hits it mid-job. That second
  half was added because it was missing: the server had carried a unit call Revit 2024 rejects outright
  for months while this tool reported green, because it only ever read `scripts/*.cs`. **A green check
  is only as wide as what the checker reads** — when generated C# gains a new branch, add a case to
  [`mcp-server/emit-generated-csharp.mjs`](mcp-server/emit-generated-csharp.mjs) or that branch goes
  unchecked in silence.
- **"It's not covered, so fresh code gets written, and that is slow and goes wrong."** Search first —
  `node tools/fragment-index.mjs --find <word>` then `semantic-index\ask-brain-hybrid.cmd`. When fresh
  C# genuinely is needed, **compile-check it before he runs it** (same tool, `-DryRun` builds the
  wrapper) rather than discovering the mistake through him. Each round trip through Revit costs him
  real time; a compile costs a minute and no attention.

## Maintaining this repo (the Brain itself)

- Every file edit in this repo triggers a PostToolUse hook ([`.claude/settings.json`](.claude/settings.json)
  → [`tools/verify-consistency-hook.mjs`](tools/verify-consistency-hook.mjs)) that runs the full
  consistency check — skill frontmatter, markdown link targets, scripts README sync, skill coverage in
  the entry docs, AGENT-SPEC's fragment counts, text encoding, the `// SOURCE:` cross-references
  inside script fragments, the "searches all N files" semantic-index coverage claims, and **every live
  fragment/skill/native-tool count stated anywhere in markdown** (check 9, added 2026-08-22 after an
  audit found nine wrong ones — including one on line 79 of this file), **each fragment's own header
  status against its `scripts/README.md` row** (check 10 — nineteen headers still said "NOT YET
  LIVE-VERIFIED" for fragments proven on 2026-08-06/07, because the campaign updated the README and never
  the file), **outside-source names in `scripts/`, `skills/` and `knowledge/`** (check 11 — the 2026-08-20 strip
  never reached the fragments), and **any `N/M at #1` retrieval score against the last run in
  `semantic-index/score-history.md`** (check 12 — `README.md` was quoting a top-1 figure from the
  retired 14-row era next to a top-5 figure that matched no run that ever happened, i.e. a remembered
  composite; the check fired on this very sentence when it first carried the literal numbers, which is
  the check working, so quote the file instead of the digits). and **unresolved merge-conflict markers in any tracked file** (check 13, added the
  same day after a conflicted `CLAUDE.md` passed all twelve checks above — every other check reads
  content for a specific claim, none asks whether the file is coherent at all). **Thirteen checks in the
  Node version.** If it reports
  drift, fix the drift in the same turn, before finishing. The hook runs the **Node** checker on purpose: the PowerShell wrapper
  it replaced fired only on Windows and silently did nothing everywhere else, so a whole session on
  Claude Code for web got no checking at all (2026-08-04). If Node isn't on PATH on some machine, run
  [`tools/verify-consistency.ps1`](tools/verify-consistency.ps1) by hand instead — but know that it
  **trails the Node checker and is not its equal**: the Node version is what the hook runs and is the
  authority, and check 9 has not been ported to PowerShell. Porting it from a Linux container is exactly
  the `.ps1` encoding trap described below, so it waits for a session on Windows that can run the result.
- **Never name an outside source in anything written here — no other people's repos, tools, products,
  websites or personal names.** Ajmal's instruction, 2026-08-20: *"do not mention any thing that we took
  from this web site or repo... the words also remove... remove his name and do not use like that."*
  This covers `// SOURCE:` headers, knowledge notes, log entries and skill text alike. A `SOURCE:` line
  points at a **file inside this Brain** and nothing else. If a technique is worth having, write it in
  your own words as this Brain's own knowledge and verify it here — do not record where it came from,
  and do not create a file whose subject is somebody else's tool. Everything of that kind was stripped
  on 2026-08-20; putting any of it back undoes a deliberate decision.
- Log structural changes (new skill, split file, new/retired script) in
  [`knowledge/brain-log.md`](knowledge/brain-log.md), 1–3 lines each.
- **Never bulk-edit files here with PowerShell `Get-Content`/`Set-Content`.** Windows PowerShell 5.1 reads
  UTF-8-without-BOM as ANSI, so a read-modify-write round trip double-encodes every em dash, ✓ and quote in
  the file. This corrupted 41 files on 2026-07-26 before being caught. For any scripted multi-file edit use
  `[System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)` and
  `[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))`. The Edit/Write
  tools are always safe. Tell-tale sign something went wrong: a `git diff --stat` far larger than the edit
  you actually made.
- **Any `.ps1` you write here must have a UTF-8 BOM, or contain no non-ASCII character at all.** Same
  root cause as the rule above, but it breaks *running* a script rather than editing one: PS 5.1 reads a
  BOM-less file as ANSI, so an em dash's last byte becomes cp1252 `0x94` — a smart quote, **which
  PowerShell accepts as a string delimiter**. One em dash opens an unterminated string and cascades into
  dozens of parse errors that look like syntax. It has now happened twice:
  `tools/verify-fragments-compile.ps1` had never run once (2026-08-04), and `tools/check-scripts.ps1`
  — written in a Linux container and pulled in — could not start (2026-08-20). **The container is the
  live vector: it has no PowerShell to fail on, so nothing catches it before the file reaches Windows.**
  Check with `head -c3 file.ps1` (want `ef bb bf`). Diagnose a suspect file by parsing its bytes decoded
  as UTF-8 — 0 errors that way but many via `ParseFile` means encoding, not syntax.
- This repo doubles as an installable Claude Code **plugin** — manifest in `.claude-plugin/`, install
  steps in [`SETUP.md`](SETUP.md) step 1. Keep `skills/` at the repo root; that's where the plugin
  loader finds them.
- **More than one session can be working in this repo at the same time** — observed 2026-08-23, when two
  peer sessions were adding fragments during a harvest and files appeared on disk that nobody in that
  conversation had written. The consistency hook and every count are computed from disk on each edit, so
  a second session's work shows up as drift in yours. **Do the private per-file work first** (new
  fragments, upgrades, knowledge notes — those never collide), and leave everything touching a SHARED
  file to one pass at the very end: `scripts/README.md` rows, `sync-counts`, `verify-consistency`,
  `brain-log.md`, `plugin-release`. Running the checks earlier just reports someone else's half-finished
  work. And **a `check-scripts` FAIL naming a file you did not write belongs to another session — report
  it, do not fix it**; editing a file another session is mid-way through corrupts both.
- **Before pushing anything a plugin user should receive, run
  [`node tools/plugin-release.mjs`](tools/plugin-release.mjs)** and commit the bumped version with the
  change. Claude Code pins an installed plugin to the `version` string in `plugin.json` — its own
  documentation says "users only receive updates when you change this field" — so a push without a
  bump publishes the work and delivers it to nobody. There is no error and no warning; the colleague
  simply never sees the new fragments. This is the one release step that cannot be inferred from the
  files, which is why it is written here rather than left to memory. Their **search index re-syncs
  itself**: `tools/brain-setup.mjs --check` runs at every SessionStart, and when the files are newer
  than the index it starts an incremental rebuild in the background (detached, ~200 ms to fire,
  lock-guarded against two sessions racing). That half is automatic; the version bump is not.
