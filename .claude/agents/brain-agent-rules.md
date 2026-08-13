# Rules every Brain agent must follow

**Read this before doing anything.** You start with an empty head. You were not present for the work
that produced these rules, and every one of them was paid for once already.

**These are pointers, not copies.** Where a rule lives in a real file, go and read that file. A second
copy of a rule drifts from the original, and documentation quietly getting ahead of reality is this
repo's documented recurring failure — the README once claimed 8 skills against 9, and AGENT-SPEC 206
fragments against 264.

## Read first, always

| File | Why |
|---|---|
| `CLAUDE.md` | The session rules. Non-optional. |
| `START-HERE.md` | How to work here: verify don't trust, fresh reads, plan → split → execute. |
| `skills/brain-self-maintain/SKILL.md` | Where a new lesson belongs, and the size/splitting rules. |
| `knowledge/INDEX.md` | What is already written down, so you do not write it a second time. |

## The three most often forgotten

1. **Never bulk-edit files here with PowerShell `Get-Content`/`Set-Content`.** Windows PowerShell 5.1
   reads UTF-8-without-BOM as ANSI, so a read-modify-write round trip double-encodes every em dash,
   ✓ and quote in the file. This corrupted **41 files** on 2026-07-26 before anyone noticed. Use the
   Edit/Write tools, which are always safe. Tell-tale sign something went wrong: a `git diff --stat`
   far larger than the edit you actually made.
2. **Search before you write.** `node tools/fragment-index.mjs --find <word>` searches all 269
   fragments by purpose and input field; `semantic-index\ask-brain-hybrid.cmd "<question>"` searches
   everything by meaning. Writing a fresh fragment for a job an existing one already covers is the most
   common waste in this repo, and it happens because the search feels expensive and gets skipped.
3. **Millimetres in, feet inside Revit.** Ajmal speaks mm; the Revit API is feet. Convert explicitly,
   both directions, every time. And every number — clearance, flow, height, margin — is a per-request
   input, never a default carried over from a previous job.

## What you must never do

- **Never touch Revit or the AJ AI bridge.** There is one bridge and one open Revit session, and Ajmal
  is working in it right now. Your transaction fighting his is how he loses work he never asked to
  lose. You are not given the bridge tools; do not try to reach them another way.
- **Never delete or replace an existing file** without Ajmal's explicit go-ahead. Adding is free.
  Removing is his decision, not yours.
- **Never report something as done that you did not verify.** Run the check, and quote the real output.
  If a step failed or you skipped it, say so plainly.
- **Never invent detail to fill a gap.** You were not in the session. File what is certain, and say
  clearly what you could not confirm.

## Before you finish

- Run `node tools/verify-consistency.mjs`. If it reports drift, fix the drift — do not hand back a repo
  you broke. It checks skill frontmatter, markdown links, the scripts README, skill coverage, fragment
  counts, encoding, cross-references, and the semantic-index coverage claims.
- **You do not need to rebuild the search index.** It rebuilds itself at the end of the turn
  (`tools/reindex-mark.mjs` + `tools/reindex-run.mjs`, added 2026-08-13). Only edits made outside a
  session need `semantic-index\index-brain.cmd` by hand.
