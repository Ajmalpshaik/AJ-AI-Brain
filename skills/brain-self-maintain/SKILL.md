---
name: brain-self-maintain
description: Create or modify anything in this Brain — skills, knowledge files/topics, reusable script fragments, and the index files that route between them — and do a deliberate whole-session sweep to check nothing worth keeping got left in chat. Use whenever the user asks to save something as reusable, "make this a skill", "split this file", "add an index", "update/sync the brain", "did we save everything", or "did we miss anything" — including broken-English/dictated versions. Also fires proactively, without being asked, whenever a recurring task pattern, a script worth saving, or a file that's grown too big is noticed mid-work — create it and report it in the same reply ("created X because Y — say delete if you don't want it"); deleting or replacing something that already exists still needs the user's explicit OK first. This is the skill that makes the Brain get stronger over time instead of starting from scratch every session — the lightweight, always-on version of the same habit is in START-HERE.md; this is the deliberate, deeper pass.
---

# Brain Self-Maintenance

This Brain grows by capturing what worked, in exactly one right place, instead of duplicating it or
losing it back into chat history. This skill is how that happens on purpose, not by accident.

## Two ways this fires

1. **The user asks directly** — "save this", "make this a skill", "split this file", "sync the brain",
   "did we miss anything." Go to Step 1.
2. **Noticed mid-task, unprompted** — a pattern that keeps recurring, a script worth keeping, a file
   that's grown painful to read. **Create it, then report it in the same reply**: *"Created [X] because
   [Y] — say delete if you don't want it."* The report is mandatory. Before creating, check no existing
   skill/file already covers the pattern — a colliding skill poisons routing for every future request.
   **Deleting or replacing something that already exists still needs the user's explicit OK first.**

## Step 1 — Route: what kind of thing is this?

Pick one home. Never write the same fact twice — duplication is what makes an index untrustworthy.

| What surfaced | Where it belongs |
|---|---|
| A fact, gotcha, or technique learned from real work | The one matching **knowledge** topic file — route via [`../../knowledge/INDEX.md`](../../knowledge/INDEX.md) |
| Reusable C# that runs on the live model | A fragment in [`../../scripts/`](../../scripts/README.md) — see Step 3 |
| A recurring, bounded, multi-step **task pattern** | A new **skill** — see Step 2 |
| A habit that must apply to *every* task, no matter what's running | [`../../START-HERE.md`](../../START-HERE.md) — **not** a skill (see the trap below) |
| A file that's grown too big, or is hard to navigate | **Split it and index it** — see Step 4 |

**The trap worth naming.** A standing preference ("always answer counts in one line") is *not*
skill-worthy — a skill only fires when it's decided the request needs it, but a universal habit has to
apply regardless of which skill is running, so it belongs in START-HERE.md instead. Getting this wrong
produces either a skill that never triggers, or a preference applied inconsistently.

## Step 2 — Building a new skill

1. Quick check: what should trigger it, what it does, does it need its own knowledge file or reuse an
   existing one.
2. Create `skills/<name>/SKILL.md` — one skill per folder, matching the existing ones.
3. Write the description a little pushy — what it does *and* the situations that trigger it, including
   phrasings that don't use the obvious keyword. State what it must NOT fire on, naming the skill that
   owns that instead.
4. Bake in plan-split-execute — a short visible plan, one step at a time, verified before the next.
5. Point at shared knowledge (`glossary.md`, `reply-style.md`), don't duplicate it.
6. Skills describe workflow; scripts hold the code — don't paste working C# into a SKILL.md.
7. Obey the size rule below, then show the user the result.

## Step 3 — Saving reusable C#

Read [`../../scripts/README.md`](../../scripts/README.md) first. "Find some elements, then do something
to them" composes from existing `filters/` + `actions/` (or `creators/`) — don't give a skill its own
script for this. A genuinely bespoke, order-dependent, multi-stage build gets its own file in `recipes/`.
Write the fragment first, then reference it from the skill, and add it to the scripts README table in
the same step.

## Step 4 — Splitting a big file and indexing it

1. **Measure first, don't assume** — check real line counts before restructuring.
2. **Cut mechanically at the section seams** — never retype or summarize; rewriting a section silently
   drops content.
3. **Prove it lossless** — diff the new files against a backup of the original.
4. **Leave the old filename as a short signpost** pointing at the new index, so existing links resolve.
5. **Write the index to route by request shape** — "if the request is about X → open this file" — never
   by filename or today's noun.
6. **Retarget pointers to the topic file, not the folder.**
7. **Run the checker**: `powershell -ExecutionPolicy Bypass -File tools\verify-consistency.ps1` — or,
   in a session without PowerShell (e.g. Claude Code on the web), `node tools/verify-consistency.mjs`
   (same three checks, portable Node).

## The size rule

- A `SKILL.md` stays ~60–150 lines — *when to trigger* and *the steps*, not reference material or code.
- A knowledge file past ~300 lines is a split candidate, not a mandate — if it's one coherent job read as
  a unit, splitting adds hops and makes things worse; say so and leave it.
- Anything long goes beside a file, not inside it.
- Never duplicate a fact across two files.
- When anything is added, split, or retired, update its index in the same step.

## Whole-session sweep (the deliberate, deeper pass)

When the user asks "did we save everything" / "sync the brain" / a session is ending:
1. Re-read the session for: new techniques, new ambiguous terms, new gotchas, reply-format corrections,
   bugs found+fixed, reusable C# worth saving, anything about the user/project that should persist across
   sessions generally (that's cross-session memory, not this Brain, if the harness has one).
2. Read the current state of every likely destination before writing — don't duplicate something already
   captured.
3. Route each new item to exactly one place (Step 1 table above).
4. If something looks like a new skill, don't scaffold it inline — follow Step 2.
5. Run the consistency checker; fix anything it flags.
6. Report back a short list of what got added/updated and where.

## After finishing

Add one short dated line to [`../../knowledge/brain-log.md`](../../knowledge/brain-log.md): what was
created or changed and why. Then tell the user plainly what changed and where, in plain language — they
should not have to open the files to know what happened.
