---
name: brain-update-layers
description: Bring the Brain's three derived layers up to date and prove they are — the vector index (plain-English search), the knowledge graph (both its code side and its document side), and the Obsidian vault. Use at the end of a working day or week, after a git pull or branch switch, when a search is slow or returns stale results, or whenever Ajmal says "update the brain", "update everything", "end of day", "update the index / graphify / obsidian", "is everything up to date", or a dictated version of any of those. NOT for saving what a session learned — that is brain-self-maintain; this skill runs the mechanical refresh and verifies it, and it exists because exactly one part of that refresh cannot be automated by a hook.
---

# Brain — update the derived layers

Three layers are derived from the Brain's files and are gitignored, so they live on this machine and
never travel in git: the **vector index** (the plain-English search), the **knowledge graph**, and the
**Obsidian vault**.

**Most of this is already automatic.** `tools/stop-hooks.mjs` refreshes the vector index, the graph's
**code** side and the Obsidian vault at the end of any turn that changed a file. This skill exists for
the one piece that a hook fundamentally cannot do, plus the cases where no hook ever fired.

## What is NOT automatic, and why

| Gap | Why a hook cannot do it |
|---|---|
| **The graph's DOCUMENT side** — the `.md` files: `CLAUDE.md`, `START-HERE.md`, `knowledge/*` | graphify's semantic pass **dispatches LLM subagents**. A hook is a plain script and cannot call a model. This is architectural, not effort. |
| **Edits made outside a session** — `git pull`, branch switch, a file changed in an editor, a folder copied in | No hook fires, because no tool call happened. |
| **A corrupted vector index** | The incremental rebuild cannot repair it; only `--full` can. |

Everything else on the list below is verification — confirming the automatic parts really did run,
because *silently not running* is this repo's worst failure mode.

## Run these in order

Do not skip the checks. Each step's real output is the evidence; do not report a step as done from the
fact that the command exited 0.

### 1. Read the true state first

```
node tools/brain-status.mjs
```

Read the **Derived layers** block. Each line says when the layer was built and how many source files are
newer than it. A `~` means the date is a file mtime — a hint, not a verdict.

Note what is already current. If all three say today with nothing newer, steps 2 and 3 may be no-ops —
say so rather than running them for show.

### 2. Catch anything changed outside a session

```
semantic-index\index-brain.cmd
```

Two to four seconds when nothing moved. **Run it even if you think nothing changed** — that is exactly
the case it exists for, since a git checkout leaves no trace a hook can see.

If a search has been slow, or the index is suspected corrupt, use `--full` instead (155 s). The failure
signature is in [`../../semantic-index/README.md`](../../semantic-index/README.md): a healthy query is
**under 4 seconds**; anything past ~15 s means broken, not slow.

### 3. The graph's document side — the part only a session can do

```
/graphify . --update
```

This is the whole reason this skill exists. It re-reads the markdown files whose cached extraction is
behind and rebuilds the graph from them. It uses subagents, so it takes minutes, not seconds.

**First, find out whether it is even needed.** This reports it by name:

```
node tools/graph-rebuild.mjs
```

If it prints *"The code side below is current; the document side is not"* followed by a list of files,
those are the stale ones — run `/graphify . --update`. If it does not print that, **skip this step and
say you skipped it and why.** Running a minutes-long semantic pass that changes nothing is not thorough,
it is waste, and it costs Ajmal real time.

### 4. Rebuild the Obsidian vault from the refreshed graph

```
node tools/obsidian-export.mjs --force
```

`--force` because step 3 may have changed `graph.json` in a way the stamp guard has already seen. Takes
about 4 s and reports the note count. If the count **fell**, look for the "skipped pre-existing file(s)"
line — graphify refuses to overwrite files it did not create, so a stray file in the vault folder
silently shrinks it.

### 5. Prove the search actually works

Do not assume. Ask it something and read the answer:

```
semantic-index\ask-brain-hybrid.cmd "a question about something changed recently"
```

Check three things: it returns in **under 4 seconds**, it prints **no `STALE INDEX` banner**, and the
top hits are **files that actually answer the question**. A fast empty answer is still a failure.

### 6. Consistency, and fix what it finds

```
node tools\verify-consistency.mjs
```

All 13 checks must pass. If any drift is reported, **fix it in this same turn** — that is the standing
rule in [`../../CLAUDE.md`](../../CLAUDE.md). The usual causes are a new fragment missing from
`scripts/README.md`, and live counts stated across ~14 files that all move together when the fragment
count changes.

### 7. Report the real numbers

Re-run `node tools/brain-status.mjs` and state what actually changed — the three layer dates, the note
count, and anything deliberately skipped **with the reason**. If something failed, say so with its
output. Do not report "all up to date" unless step 5 genuinely returned good hits.

## What this skill does NOT do

**It does not decide what to save.** Capturing what a session learned — a new fragment, a knowledge
note, one of Ajmal's own words for the glossary — is
[`../brain-self-maintain/SKILL.md`](../brain-self-maintain/SKILL.md), and it is judgement, not
mechanics. Run that one first if the session did real work; this one refreshes the layers afterwards so
the new material is actually findable.

## The one decision that is Ajmal's, not yours

Step 3 can be made fully automatic by setting a **`GEMINI_API_KEY`**, which lets graphify's semantic
pass run headless from a hook. **Do not set that up on your own initiative.** It sends Brain content to
an outside service, and that is a deliberate decision for Ajmal to make, not a convenience default. Say
the option exists, say what it costs, and let him choose.
