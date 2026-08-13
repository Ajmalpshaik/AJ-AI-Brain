# Brain RAG — Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the assistant missing things Ajmal has already written, and stop lessons being lost when a session ends.

**Architecture:** A `UserPromptSubmit` hook runs Brain search on every substantive question and injects a *compact* result block before the assistant reads the message — gated so it never fires on "ok". A **Librarian** subagent files what a session learned, without touching Revit. Both lean on a shared rules file that **points at** the existing rules rather than copying them, because copies drift.

**Tech Stack:** Node.js ESM hooks in `tools/`, Python in `semantic-index/`, Claude Code agent definitions in `.claude/agents/`.

## Global Constraints

Carried from Phase 1 and `docs/superpowers/specs/2026-08-13-brain-rag-and-agents-design.md`. All still apply.

- **Never call any `.cmd` wrapper from a hook or tool.** `index-brain.cmd` and `score-brain.cmd` both end with `pause` and would block forever. Call the `.py` through the venv Python.
- **Never bulk-edit repo files with PowerShell `Get-Content`/`Set-Content`** — corrupted 41 files on 2026-07-26.
- **Nothing writes to the system temp folder.** Everything stays inside `semantic-index/`.
- **No agent touches Revit.** One bridge, one Revit session; a background agent running a script while Ajmal works in the same model means two transactions fighting.
- **The count "17 native tools" means 17 Revit bridge tools.** Non-Revit tools live in `mcp-server/brain-tools/`.
- **Score before and after.** `score_brain.py` must report the same numbers at the end of Phase 2 — this phase changes *when* search runs, not how it ranks.
- Commit style `Area: what changed`, ending with:
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`

---

## File Structure

| File | Responsibility |
|---|---|
| `semantic-index/brain_context.py` (create) | Print a **compact** search result block suitable for injecting into context |
| `tools/auto-search-hook.mjs` (create) | UserPromptSubmit — decide whether to search, then emit the block |
| `.claude/settings.json` (modify) | Wire the hook in |
| `.claude/agents/brain-agent-rules.md` (create) | The rules every Brain agent must follow — pointers, not copies |
| `.claude/agents/brain-librarian.md` (create) | The Librarian agent definition |

**Why `brain_context.py` rather than reusing the CLI:** the search's normal output carries long snippets — useful to read, far too heavy to prepend to every message. This prints paths, area, how-found and PROVEN status only, in about six lines. Parsing the existing CLI output instead would be fragile; calling `hybrid_search()` directly is not.

**Why the rules file points instead of copies:** the rules already exist in `CLAUDE.md`, `START-HERE.md` and `knowledge/`. A second copy would drift from the original, which is this repo's documented recurring failure. The file names where each rule lives and highlights the three most often forgotten.

---

## Task 1: Compact context output

**Files:**
- Create: `semantic-index/brain_context.py`

**Interfaces:**
- Consumes: `hybrid_search(query, top_k=5, area=None, use_fragment_tool=True)` from `brain_search_hybrid.py:345`, returning `(results, notes)`. Each result is a dict with `path`, `area`, `meaning_rank`, `word_rank`, and for fragments `status`.
- Produces: `brain_context.py "<query>"` printing a compact block to stdout, or nothing at all when there are no results.

- [ ] **Step 1: Write it**

Create `semantic-index/brain_context.py`:

```python
"""Print a COMPACT Brain search result block, for injecting into an AI session's context.

brain_search_hybrid.py's normal output carries a long snippet per result - right for a
person reading the answer, far too heavy to prepend to every message someone types. This
prints only what is needed to decide which file to open: the path, what kind of file it
is, how it was found, and whether a fragment is proven.

Reads only. Never changes the Brain, never touches Revit.

    brain_context.py "how do I stop ducts overlapping the ceiling"
    brain_context.py --top 3 "sprinkler spacing"
"""

import argparse
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("query", nargs="+")
    parser.add_argument("--top", type=int, default=5)
    args = parser.parse_args(argv)

    query = " ".join(args.query).strip()
    if not query:
        return 0

    sys.path.insert(0, str(HERE))
    from brain_search_hybrid import hybrid_search

    results, notes = hybrid_search(query, top_k=args.top)
    if not results:
        return 0

    lines = [f'Brain hits for "{query}":']
    for i, r in enumerate(results, start=1):
        path = str(r.get("path", "?")).replace("\\", "/")
        area = str(r.get("area", "")) or "?"
        status = r.get("status")
        tag = status if status in ("PROVEN", "unproven") else area
        meaning = r.get("meaning_rank")
        word = r.get("word_rank")
        found = []
        if meaning:
            found.append(f"meaning#{meaning}")
        if word:
            found.append(f"words#{word}")
        lines.append(f"  {i}. {path}  [{tag}]  {' '.join(found)}")

    lines.append(
        "  (top 3-5, not just #1 - open the file before answering; "
        "high in BOTH meaning and words is the strong signal)"
    )

    # A stale index means these hits describe an older copy of the Brain. Say so inline:
    # the whole point of injecting this is that nobody has to go looking for a warning.
    if notes and "stale" in repr(notes).lower():
        lines.append("  (WARNING: index is STALE - these paths may be out of date)")

    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 2: Run it and check the shape**

Run: `semantic-index/venv/Scripts/python.exe semantic-index/brain_context.py "how do I undo a mistake"`

Expected: about seven lines — a header, five numbered paths with `[PROVEN]`/`[skill]`/`[knowledge]` tags and `meaning#N words#N`, and the closing reminder. **No snippets.** If snippets appear, the wrong function is being called.

- [ ] **Step 3: Check it stays quiet on nonsense**

Run: `semantic-index/venv/Scripts/python.exe semantic-index/brain_context.py "zzzzqqqq"`

Expected: either nothing, or a short block. It must not error. An exception here would print a stack trace into every future prompt.

- [ ] **Step 4: Commit**

```bash
git add semantic-index/brain_context.py
git commit -m "Semantic index: compact result block for context injection

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: The auto-search hook

**Files:**
- Create: `tools/auto-search-hook.mjs`
- Modify: `.claude/settings.json`

**Interfaces:**
- Consumes: the `UserPromptSubmit` payload on stdin as JSON — same mechanism `tools/voice/narrate-hook.mjs:303` already uses (`fs.readFileSync(0, "utf8")` then `JSON.parse`), with the typed text in `payload.prompt`.
- Produces: the compact block on stdout, which Claude Code adds to the session's context before the assistant reads the message. Silence when the gate says no.

- [ ] **Step 1: Write the hook**

Create `tools/auto-search-hook.mjs`:

```javascript
#!/usr/bin/env node
// UserPromptSubmit hook - search the Brain for what Ajmal just asked, before the assistant
// reads it, and put the top hits into context.
//
// WHY: retrieval was optional. Nothing forced a search, so whether the Brain got consulted
// depended on the assistant remembering to run a command - and the answer came from general
// Revit knowledge instead of 269 proven fragments whenever it forgot. This removes the
// remembering.
//
// WHY IT IS GATED: a search costs ~3.5 s (it loads a 166 MB model). That is nothing when a
// real question was asked - the answer was going to take longer anyway - and pure waste on
// "ok" or "go ahead". So short confirmations are skipped outright.
//
// Emits the compact block from semantic-index/brain_context.py, never the full search output:
// full output carries a long snippet per hit and would bloat every single message.
//
// Never call ask-brain-hybrid.cmd from here - .cmd wrappers in this repo end with `pause`.
//
// Always exits 0. A hook that fails must never block what Ajmal typed.

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "semantic-index");

// Things that are an answer to the assistant, not a question for the Brain.
const CONFIRMATIONS =
  /^(ok(ay)?|k|yes|yep|yeah|no|nope|sure|fine|good|great|thanks|ta|go|go ahead|do it|proceed|continue|carry on|next|stop|wait|hold on|please|correct|right|exactly|merge it|start|begin)\b[\s.!]*$/i;

function shouldSearch(prompt) {
  const text = (prompt || "").trim();
  if (!text) return false;
  if (text.startsWith("/")) return false;             // a slash command, not a question
  if (CONFIRMATIONS.test(text)) return false;         // "ok", "go ahead", "merge it"
  if (text.split(/\s+/).length < 4) return false;     // too short to retrieve usefully
  return true;
}

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function venvPython() {
  const candidates = [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ];
  return candidates.find((p) => fs.existsSync(p)) || null;
}

let payload;
try {
  payload = JSON.parse(readStdin() || "{}");
} catch {
  process.exit(0);
}

const prompt = payload.prompt ?? "";
if (!shouldSearch(prompt)) process.exit(0);

const python = venvPython();
if (!python) process.exit(0);   // silent: a missing venv must not spam every message

const result = spawnSync(
  python,
  [path.join(semanticRoot, "brain_context.py"), "--top", "5", prompt],
  { encoding: "utf8", cwd: semanticRoot, maxBuffer: 4 * 1024 * 1024, timeout: 60000 }
);

if (result.error || result.status !== 0) process.exit(0);

const block = (result.stdout || "").trim();
if (block) console.log(block);

process.exit(0);
```

- [ ] **Step 2: Test the gate directly, without the hook system**

Run each of these and check the decision:

```bash
cd "D:/Ajmal/AJ AI Brain"
for p in "ok" "go ahead" "merge it" "yes" "/graphify" "how do I stop ducts overlapping the ceiling" "how many diffusers in room 5"; do
  printf '%s -> ' "$p"
  echo "{\"prompt\":\"$p\"}" | node tools/auto-search-hook.mjs | head -1 | grep -q . && echo "SEARCHED" || echo "skipped"
done
```

Expected: the first five print `skipped`, the last two print `SEARCHED`. Any confirmation that searches is a wasted 3.5 s on every occurrence; any real question that is skipped defeats the whole task.

- [ ] **Step 3: Check the emitted block is small**

Run: `echo '{"prompt":"how do I stop ducts overlapping the ceiling"}' | node tools/auto-search-hook.mjs | wc -l`
Expected: roughly 7 lines. If it is 30+, the full search output is leaking through instead of the compact block.

- [ ] **Step 4: Wire it in**

In `.claude/settings.json`, add a new top-level entry inside `hooks`, alongside the existing ones:

```json
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"tools/auto-search-hook.mjs\""
          }
        ]
      }
    ],
```

- [ ] **Step 5: Verify the JSON**

Run: `node -e "const c=JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8')); console.log(Object.keys(c.hooks).join(', '))"`
Expected: includes `UserPromptSubmit`.

- [ ] **Step 6: Prove it live, across a turn**

Ask a real Revit question in the session and check whether the Brain hits appear in context before the answer. If they do not, check whether the hook fires at all by adding a temporary marker file write, exactly as `reindex-mark.mjs` proved itself in Phase 1.

- [ ] **Step 7: Commit**

```bash
git add tools/auto-search-hook.mjs .claude/settings.json
git commit -m "Reflex: every real question now searches the Brain before the answer

Gated - short confirmations and slash commands skip it, so 'ok' costs nothing.
Emits the compact block, never the full search output.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: The shared rules file and the Librarian agent

**Files:**
- Create: `.claude/agents/brain-agent-rules.md`
- Create: `.claude/agents/brain-librarian.md`

**Interfaces:**
- Consumes: the existing rule sources — `CLAUDE.md`, `START-HERE.md`, `skills/brain-self-maintain/SKILL.md`, `knowledge/INDEX.md`.
- Produces: an agent type `brain-librarian`, invocable through the Agent tool, with no Revit tools available to it.

- [ ] **Step 1: Write the rules file**

Create `.claude/agents/brain-agent-rules.md`:

```markdown
# Rules every Brain agent must follow

**Read this before doing anything.** You start with an empty head — you were not present for the
work that produced these rules, and each one was paid for once already.

**These are pointers, not copies.** Where a rule lives in a real file, go read that file. A second
copy of a rule drifts from the original, and documentation getting ahead of reality is this repo's
documented recurring failure — the README once claimed 8 skills against 9, and AGENT-SPEC 206
fragments against 264.

## Read first, always

| File | Why |
|---|---|
| `CLAUDE.md` | The session rules. Non-optional. |
| `START-HERE.md` | How to work: verify don't trust, fresh reads, plan-split-execute. |
| `skills/brain-self-maintain/SKILL.md` | Where a new lesson belongs, and the size/splitting rules. |
| `knowledge/INDEX.md` | What is already written down, so you do not write it twice. |

## The three most often forgotten

1. **Never bulk-edit files here with PowerShell `Get-Content`/`Set-Content`.** Windows PowerShell 5.1
   reads UTF-8-without-BOM as ANSI, so a read-modify-write double-encodes every em dash and quote.
   This corrupted 41 files on 2026-07-26. Use the Edit/Write tools. Tell-tale sign: a `git diff --stat`
   far larger than the edit you made.
2. **Search before you write.** `node tools/fragment-index.mjs --find <word>` searches all 269
   fragments; `semantic-index\ask-brain-hybrid.cmd "<question>"` searches everything by meaning. Writing
   a fresh fragment for a job an existing one already covers is the most common waste in this repo.
3. **Millimetres in, feet inside Revit.** Ajmal speaks mm; the Revit API is feet. Convert explicitly,
   both ways, every time.

## What you must never do

- **Never touch Revit or the AJ AI bridge.** There is one bridge and one open Revit session, and Ajmal
  is working in it. Your transaction against his is how he loses work he did not ask to lose.
- **Never delete or replace an existing file** without Ajmal's explicit go-ahead. Adding is free;
  removing is not yours to decide.
- **Never report something as done that you did not verify.** Run the check and quote the real output.

## Before you finish

- Run `node tools/verify-consistency.mjs`. If it reports drift, fix the drift — do not hand back a
  repo you broke.
- The index rebuilds itself at the end of the turn, so you do not need to run it. If you edited files
  outside a session, run `semantic-index\index-brain.cmd`.
```

- [ ] **Step 2: Write the Librarian agent**

Create `.claude/agents/brain-librarian.md`:

```markdown
---
name: brain-librarian
description: Files what a session learned into the AJ AI Brain - checks it is not already written down, routes it to the right folder, updates the indexes, and logs it. Use at the end of a working session, or whenever Ajmal says "save it" / "save what we learned". Never touches Revit.
tools: Read, Write, Edit, Glob, Grep, Bash
---

You are the Librarian for the AJ AI Brain. You file what a session learned so it is not lost.

**First, read `.claude/agents/brain-agent-rules.md` and follow it.** It is short and every rule in it
was paid for once already.

## Why you exist

`START-HERE.md` says the habit of saving what surfaced is what makes this Brain stronger instead of
static. It is also the step most likely to be skipped, because it happens after the real work is
finished and everyone has stopped paying attention. A lesson not written down is gone forever; a
script not written can be written tomorrow.

## What you are given

A description of what happened in the session — techniques discovered, gotchas hit, scripts that
worked or failed, words Ajmal used. You were not there. Work only from what you are told and what you
can read on disk. **Do not invent detail to fill gaps**; if something is unclear, file what is certain
and say plainly what you could not confirm.

## How to file

1. **Check it is not already there.** `semantic-index\ask-brain-hybrid.cmd "<the lesson>"` and
   `node tools/fragment-index.mjs --find <word>`. If it already exists, improve that file rather than
   adding a second one saying the same thing.
2. **Route it** using `skills/brain-self-maintain/SKILL.md`. Knowledge notes to `knowledge/`, reusable
   C# to `scripts/`, a repeating task pattern to a skill. One place only — the same lesson in two
   files becomes two lessons that disagree.
3. **Record Ajmal's own words.** If he named something in his own phrasing — a site term, an
   abbreviation, the way he describes a whole job — add it to `knowledge/glossary.md` as *his words →
   the Revit meaning*, and consider a row in `knowledge/site-vocabulary.md` if a search missed because
   of it. This is a standing instruction of his, not an optional nicety: the measured weak spot of the
   search layer is exactly the site vocabulary that appears in no file.
4. **Log it** in `knowledge/brain-log.md`, 1–3 lines, dated, saying what changed and *why*.
5. **Verify**: `node tools/verify-consistency.mjs` must pass before you finish.

## What you report back

A short list: what you filed, where, and why. Then anything you deliberately did **not** file, and the
reason. Do not pad it — the value is in the files you wrote, not the summary.
```

- [ ] **Step 3: Verify the agent is registered**

Run: `ls .claude/agents/` and confirm both files exist. The agent type `brain-librarian` becomes available to the Agent tool on the next session; a running session may not see it until reloaded.

- [ ] **Step 4: Commit**

```bash
git add .claude/agents/
git commit -m "Agents: the Librarian, and one shared rules file it must follow

The rules file points at CLAUDE.md/START-HERE.md rather than copying them - a
second copy drifts, which is this repo's documented recurring failure.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Definition of done for Phase 2

- [ ] Typing a real Revit question puts Brain hits into context before the answer, with no command run
- [ ] Typing "ok" costs nothing — no search, no delay
- [ ] `score_brain.py` reports the same numbers as at the end of Phase 1
- [ ] `node tools/verify-consistency.mjs` reports no drift
- [ ] `brain-librarian` exists as an agent type and has no Revit tools
- [ ] `knowledge/brain-log.md` records both changes

## Deliberately not doing

- **No warm search service.** Gating is the cheap fix; build speed only if the delay is actually felt.
- **No Script Writer or Investigator agent.** Those are Phase 4, after the Librarian has earned trust.
- **No accuracy work.** The glossary discount, the oversized-file split and the bigger embedding model
  are Phase 3. This phase must leave the score unchanged.
