---
name: brain-update-layers
description: Refresh the Brain's derived layers and prove they are current. TWO MODES, and the words Ajmal uses decide which. "update the brain" / "update brain" / "end of day" / "is everything up to date" = FAST mode — the vector index (the plain-English search) only, about 30 seconds. "update the ALL brain" / "update all the brain" / "update everything" / "full update" = FULL mode — the vector index PLUS the knowledge graph's document side and the Obsidian vault, which needs LLM subagents and takes the best part of an hour. Never run FULL unless he said one of the ALL words. Also use after a git pull or branch switch, or when a search feels slow or stale. NOT for deciding what to save from a session — that is brain-self-maintain.
---

# Brain — update the derived layers

Three layers are derived from the Brain's files and are gitignored, so they live on this machine and
never travel in git: the **vector index** (the plain-English search), the **knowledge graph**, and the
**Obsidian vault**.

They are not worth the same, and this skill exists to stop treating them as if they were.

## Which mode — read his words first

| He says | Mode | What runs | Cost |
|---|---|---|---|
| "update the brain", "update brain", "end of day", "is everything up to date", "refresh the search" | **FAST** | Vector index + checks | ~30 seconds |
| "update the **all** brain", "update all the brain", "update **everything**", "full update" | **FULL** | Everything, including the graph's document side and the vault | **~1 hour, ~800k tokens** |

**FAST is the default. When in doubt, run FAST and say so** — he can always ask for the full one. The
reverse is not true: an unasked-for FULL run costs him an hour and a large token bill for layers he does
not use.

**Ajmal's decision, 2026-08-23, on the evidence below.** Measured over his real usage: the vector index
answered **247 of 247** questions and runs on every message. The knowledge graph's `search_graph` had
been called **zero** times ever, and the Obsidian vault had never been opened — the app on this PC has
no vault registered at all. Tested head to head, the graph also lost on its own home ground: it found
nothing for a dependency question, and no path between two related ideas. So the expensive half stopped
being automatic and became something he asks for by name.

## FAST mode — "update the brain"

### 1. Read the true state

```
node tools/brain-status.mjs
```

Read the **Derived layers** block: when each was built, and how many source files are newer. A `~` means
the date is an mtime — a hint, not a verdict.

### 2. Refresh the search

```
semantic-index\index-brain.cmd
```

Two to four seconds when nothing moved. **Run it even when you think nothing changed** — that is exactly
what it is for, since a git checkout leaves no trace a hook can see.

If a search has been slow, or the index is suspected corrupt, use `--full` instead (~155 s). The failure
signature is in [`../../semantic-index/README.md`](../../semantic-index/README.md): a healthy query is
**under 4 seconds**; past ~15 s means broken, not slow.

### 3. Prove the search actually works

Do not assume. Ask it something and read the answer:

```
semantic-index\ask-brain-hybrid.cmd "a question about something changed recently"
```

Three things must hold: under **4 seconds**, **no `STALE INDEX` banner**, and top hits that genuinely
answer the question. A fast empty answer is still a failure.

### 4. Consistency, and fix what it finds

```
node tools\verify-consistency.mjs
```

All checks must pass. If drift is reported, **fix it in the same turn** — the standing rule in
[`../../CLAUDE.md`](../../CLAUDE.md). Usual causes: a new fragment missing from `scripts/README.md`, and
live counts stated across ~14 files that move together. `node tools/sync-counts.mjs` re-trues the counts
from the checker's own report.

### 5. Report the real numbers

Re-run `node tools/brain-status.mjs` and say what actually changed. **Say plainly that the graph and the
vault were not touched, and that "update the all brain" is the phrase that would touch them.** Reporting
"the Brain is updated" without that line is the kind of half-truth this repo keeps getting caught by.

## FULL mode — "update the ALL brain"

Everything in FAST first, then these. **Tell him the cost before starting** — roughly an hour and around
800k tokens — and let him confirm if he seems to have said it in passing.

### 6. The graph's document side

**First find out whether it is even needed:**

```
node tools/graph-rebuild.mjs
```

If it prints *"The code side below is current; the document side is not"* with a list of files, those are
stale — run the semantic pass:

```
/graphify . --update
```

It dispatches LLM subagents, so it takes minutes and real tokens. If the check does **not** report stale
documents, **skip this and say you skipped it and why.** A minutes-long pass that changes nothing is not
thoroughness, it is waste.

### 7. Rebuild the Obsidian vault from the refreshed graph

```
node tools/obsidian-export.mjs --force
```

`--force` because step 6 may have moved `graph.json` past the stamp guard. About 5 s, and it reports the
note count. If the count **fell**, look for the "skipped pre-existing file(s)" line — graphify refuses to
overwrite files it did not create, so a stray file in the vault folder silently shrinks it.

### 8. Re-verify and report

Re-run `node tools/graph-rebuild.py --check` — it should say all documents cached — then
`node tools/brain-status.mjs`, and report all three layer dates with the note count.

## What this skill does NOT do

**It does not decide what to save.** Capturing what a session learned — a new fragment, a knowledge note,
one of Ajmal's own words for the glossary — is
[`../brain-self-maintain/SKILL.md`](../brain-self-maintain/SKILL.md), and it is judgement, not mechanics.
Run that one first if the session did real work; this one refreshes the layers afterwards so the new
material is findable.

## The one decision that is Ajmal's, not yours

Step 6 can be made fully automatic with a **`GEMINI_API_KEY`**, which lets graphify's semantic pass run
headless from a hook. **Do not set that up on your own initiative.** It sends Brain content to an outside
service, and that is his decision, not a convenience default. Say the option exists, say what it costs,
and let him choose.
