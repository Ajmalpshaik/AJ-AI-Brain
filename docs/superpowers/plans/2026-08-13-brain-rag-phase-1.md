# Brain RAG — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the AJ AI Brain's retrieval measurable, self-refreshing, and callable from any session — the three things that must exist before any other RAG or agent work can be trusted.

**Architecture:** Three independent pieces, each shippable alone. A **score card** turns retrieval quality into a repeatable number. A **dirty-flag hook pair** rebuilds the index at the end of any turn that edited the Brain, so it can never go stale. A **`search_brain` MCP tool** makes Brain search a real tool call instead of a Windows batch file, so it works in web sessions where `.cmd` silently does nothing.

**Tech Stack:** Python 3 (existing `semantic-index/venv`, ChromaDB), Node.js ESM (hooks in `tools/`, MCP server in `mcp-server/`), Windows batch wrappers.

## Global Constraints

Copied from `docs/superpowers/specs/2026-08-13-brain-rag-and-agents-design.md`. Every task must honour these.

- **Never call `semantic-index/index-brain.cmd` from a hook or a tool.** It ends with `pause`, which blocks forever waiting for a keypress. Always invoke `brain_index.py` through the venv Python directly.
- **Never bulk-edit repo files with PowerShell `Get-Content`/`Set-Content`.** Windows PowerShell 5.1 double-encodes UTF-8; this corrupted 41 files on 2026-07-26. Use the Edit/Write tools.
- **Nothing may write to the system temp folder.** Every path stays inside `semantic-index/`. Sophos flagged `%TEMP%` DLLs as `ML/PE-A` once already.
- **Nothing in this plan touches Revit or the AJ AI bridge.** All three tasks are read-only with respect to the model.
- **The count "17 native tools" means 17 Revit bridge tools.** `tools/brain-status.mjs:90` counts every `.js` in `mcp-server/tools/`. A non-Revit tool must not live there or that number silently becomes a lie.
- **After anything in `skills/`, `knowledge/`, `scripts/` or the six root docs changes, the index must be rebuilt.** Task 2 automates this; until it lands, do it by hand.
- **Every file edit fires `tools/verify-consistency-hook.mjs`.** If it reports drift, fix the drift in the same turn before continuing.
- Commit messages follow the repo's existing style — `Area: what changed` — and end with:
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`

---

## File Structure

| File | Responsibility |
|---|---|
| `.gitignore` (modify) | Stop silently ignoring new `.md` files inside `semantic-index/` |
| `semantic-index/test-questions.md` (create) | The questions and their correct answers. Human-edited by Ajmal. |
| `semantic-index/score_brain.py` (create) | Parse the questions, run each through search, print a score |
| `semantic-index/score-brain.cmd` (create) | Double-clickable wrapper |
| `semantic-index/score-history.md` (generated) | One line per scoring run, so changes can be compared |
| `tools/reindex-mark.mjs` (create) | PostToolUse — mark the index dirty. Must be instant and never fail an edit. |
| `tools/reindex-run.mjs` (create) | Stop — if dirty, rebuild the index and clear the flag |
| `.claude/settings.json` (modify) | Wire both hooks in |
| `mcp-server/brain-tools/search-brain.js` (create) | The `search_brain` MCP tool. Separate folder so the Revit tool count stays honest. |
| `mcp-server/index.js` (modify) | Import and register it |
| `mcp-server/tools/README.md` (modify) | Point to the new non-Revit tool |

---

## Task 1: The score card

Makes retrieval quality a repeatable number. Nothing else in Phase 2 or 3 can be trusted without it.

**Files:**
- Modify: `.gitignore`
- Create: `semantic-index/test-questions.md`
- Create: `semantic-index/score_brain.py`
- Create: `semantic-index/score-brain.cmd`

**Interfaces:**
- Consumes: `hybrid_search(query, top_k=5, area=None, use_fragment_tool=True)` from `semantic-index/brain_search_hybrid.py:345`, which returns a `(results, notes)` tuple.
- Produces: `score-brain.cmd` printing a score block; `semantic-index/score-history.md` appended one line per run.

- [ ] **Step 1: Fix the .gitignore hole that would silently lose the questions**

`git check-ignore -v semantic-index/test-questions.md` currently reports `.gitignore:19: semantic-index/*` — a new `.md` file there would never be committed, and would vanish the moment the Brain is copied to another machine. `.cmd`, `.py` and `.txt` are already un-ignored; `.md` was missed.

In `.gitignore`, find the line `!semantic-index/*.cmd` and add directly beneath it:

```gitignore
!semantic-index/*.md
```

- [ ] **Step 2: Verify the hole is closed**

Run: `git check-ignore -v semantic-index/test-questions.md`
Expected: no output, exit code 1 (meaning **not** ignored). If it still prints a rule, the un-ignore line is in the wrong place — it must come after the `semantic-index/*` line.

- [ ] **Step 3: Commit the fix on its own**

```bash
git add .gitignore
git commit -m "Semantic index: stop ignoring new .md files, they were being silently dropped

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Discover the shape of a search result**

The scorer needs the relative file path out of each result, and the field name is not documented.

Run from the repo root:

```bash
semantic-index/venv/Scripts/python.exe -c "import sys; sys.path.insert(0,'semantic-index'); from brain_search_hybrid import hybrid_search; r,n = hybrid_search('how do I undo a mistake', top_k=2); print(type(r)); print(r[0])"
```

Expected: prints the container type and the first result. Note which field holds the relative path (something like `knowledge/live-model/core.md`). The code in Step 6 handles the common shapes automatically, but confirm one of them matches.

- [ ] **Step 5: Create the question file with the recoverable questions**

Only about seven of the original 24 survive, quoted in `knowledge/brain-log.md` and `semantic-index/README.md`. Seed the file with those; Ajmal writes the rest.

Create `semantic-index/test-questions.md`:

```markdown
# Brain search test questions

Each row is a question in a modeller's own words, and the file that should come back.
`score-brain.cmd` runs every row and prints a score.

**Ajmal writes these, not the assistant.** Questions written by whoever is tuning the
search prove nothing — they get unconsciously shaped into questions it can already
answer. The 2026-08-06 run was worth trusting precisely because independent testers
wrote it. That run scored 13/24 right at #1, but the questions themselves were never
saved; only the seven below survive, quoted inside knowledge/brain-log.md.

Rules for adding a row:
- Write the question the way you would say it out loud, site words and all.
- The expected file is the one you would be happy to be handed. One file per row.
- A question that is currently answered WRONG is the most valuable kind. Add it anyway.

| Question | Should return |
|---|---|
| how many diffusers do I need in this room | skills/ajtools-hvac-terminal-layout/SKILL.md |
| add 4 more floor levels | scripts/creators/create-levels.cs |
| how many light fitting | scripts/actions/action-count-by-group.cs |
| take my door schedule out to excel | scripts/actions/action-export-schedule-to-csv.cs |
| how do I undo a mistake | knowledge/INDEX.md |
| sprinkler spacing rules | skills/ajtools-fire-sprinkler-layout/SKILL.md |
| how do I stop ducts overlapping the ceiling | knowledge/live-model/mep-ducts.md |
```

- [ ] **Step 6: Verify every seeded expected file actually exists**

A test set pointing at files that do not exist scores zero forever and looks like a search failure.

Run from the repo root:

```bash
for f in skills/ajtools-hvac-terminal-layout/SKILL.md scripts/creators/create-levels.cs scripts/actions/action-count-by-group.cs scripts/actions/action-export-schedule-to-csv.cs knowledge/INDEX.md skills/ajtools-fire-sprinkler-layout/SKILL.md knowledge/live-model/mep-ducts.md; do [ -f "$f" ] && echo "OK   $f" || echo "MISSING $f"; done
```

Expected: seven `OK` lines. For any `MISSING`, find the real path with `node tools/fragment-index.mjs --find <word>` or `ls knowledge/live-model/`, and correct the row in `test-questions.md`. Do not proceed with a broken row.

- [ ] **Step 7: Write the scorer with its self-test**

Create `semantic-index/score_brain.py`:

```python
"""Score the Brain's search against a fixed set of questions.

Retrieval was measured once, on 2026-08-06 - 24 questions, 13 right at #1. The score
was written down; the questions were not. So the single most useful measurement this
Brain ever made could not be repeated, and every later change to the model, the
chunking or the files would have been made blind.

This file exists so that number can be produced again on demand.

Reads only. Never changes the Brain, never touches Revit.

    score-brain              score every question
    score-brain --self-test  check the parser and scorer, no model needed
"""

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
QUESTIONS = HERE / "test-questions.md"
HISTORY = HERE / "score-history.md"

# The key holding a result's repo-relative path. hybrid_search's result shape is not
# documented, so try the plausible names and fail loudly rather than scoring zero and
# looking like a search problem.
_REL_KEYS = ("rel", "rel_path", "relpath", "path", "file", "source")


def result_path(item):
    """Pull the repo-relative path out of one search result."""
    if isinstance(item, str):
        return item.replace("\\", "/")
    if isinstance(item, dict):
        for key in _REL_KEYS:
            value = item.get(key)
            if isinstance(value, str):
                return value.replace("\\", "/")
        meta = item.get("metadata")
        if isinstance(meta, dict):
            for key in _REL_KEYS:
                value = meta.get(key)
                if isinstance(value, str):
                    return value.replace("\\", "/")
    if isinstance(item, (tuple, list)) and item and isinstance(item[0], str):
        return item[0].replace("\\", "/")
    raise SystemExit(
        f"Cannot find the file path in a search result: {item!r}\n"
        f"Add its key to _REL_KEYS at the top of {Path(__file__).name}."
    )


def parse_questions(text):
    """Read the markdown table into [(question, expected_path)]."""
    rows = []
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) != 2:
            continue
        question, expected = cells
        if not question or not expected:
            continue
        if question.lower() == "question":          # header row
            continue
        if set(question) <= set("-: "):             # separator row
            continue
        rows.append((question, expected.replace("\\", "/")))
    return rows


def rank_of(expected, paths):
    """1-based position of expected in paths, or None if absent."""
    for i, path in enumerate(paths, start=1):
        if path == expected:
            return i
    return None


def self_test():
    table = (
        "# heading\n"
        "some prose\n"
        "| Question | Should return |\n"
        "|---|---|\n"
        "| how many diffusers | skills/a/SKILL.md |\n"
        "| add 4 more floor levels | scripts/creators/create-levels.cs |\n"
    )
    rows = parse_questions(table)
    assert rows == [
        ("how many diffusers", "skills/a/SKILL.md"),
        ("add 4 more floor levels", "scripts/creators/create-levels.cs"),
    ], rows

    assert rank_of("b.md", ["a.md", "b.md", "c.md"]) == 2
    assert rank_of("z.md", ["a.md", "b.md"]) is None
    assert rank_of("a.md", ["a.md"]) == 1

    assert result_path("knowledge\\a.md") == "knowledge/a.md"
    assert result_path({"rel": "knowledge/a.md"}) == "knowledge/a.md"
    assert result_path({"metadata": {"path": "scripts/b.cs"}}) == "scripts/b.cs"
    assert result_path(("skills/c/SKILL.md", 0.9)) == "skills/c/SKILL.md"

    print("self-test passed: parser, ranker and path extraction all OK")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()

    if not QUESTIONS.exists():
        raise SystemExit(f"No question file at {QUESTIONS}")

    rows = parse_questions(QUESTIONS.read_text(encoding="utf-8"))
    if not rows:
        raise SystemExit(f"No question rows found in {QUESTIONS}")

    sys.path.insert(0, str(HERE))
    from brain_search_hybrid import hybrid_search

    print(f"Scoring {len(rows)} questions against the current index...\n")

    at1 = at3 = at5 = 0
    misses = []
    for question, expected in rows:
        results, _notes = hybrid_search(question, top_k=5)
        paths = [result_path(r) for r in results]
        rank = rank_of(expected, paths)
        if rank == 1:
            at1 += 1
        if rank is not None and rank <= 3:
            at3 += 1
        if rank is not None and rank <= 5:
            at5 += 1
        if rank is None:
            misses.append((question, expected, paths[:1]))

    total = len(rows)
    print(f"  #1 correct    {at1:3} / {total}   ({round(100 * at1 / total)}%)")
    print(f"  in top 3      {at3:3} / {total}   ({round(100 * at3 / total)}%)")
    print(f"  in top 5      {at5:3} / {total}   ({round(100 * at5 / total)}%)")

    if misses:
        print(f"\n  Not found at all ({len(misses)}):")
        for question, expected, got in misses:
            print(f'    "{question}"')
            print(f"        wanted {expected}")
            print(f"        got    {got[0] if got else '(nothing)'}")

    previous = ""
    if HISTORY.exists():
        lines = [l for l in HISTORY.read_text(encoding="utf-8").splitlines()
                 if l.startswith("- ")]
        if lines:
            previous = f"\n  previous run: {lines[-1][2:]}"
    print(previous)

    entry = f"- {at1}/{total} at #1, {at3}/{total} in top 3, {at5}/{total} in top 5\n"
    if not HISTORY.exists():
        HISTORY.write_text(
            "# Score history\n\nOne line per `score-brain` run, oldest first.\n\n",
            encoding="utf-8",
        )
    with HISTORY.open("a", encoding="utf-8") as fh:
        fh.write(entry)

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 8: Run the self-test and watch it pass**

Run: `semantic-index/venv/Scripts/python.exe semantic-index/score_brain.py --self-test`
Expected: `self-test passed: parser, ranker and path extraction all OK`

If it raises `AssertionError`, the parser is wrong — fix `parse_questions` until the assertion holds. Do not weaken the assertion.

- [ ] **Step 9: Create the wrapper**

Create `semantic-index/score-brain.cmd`, matching the style of `ask-brain-hybrid.cmd`:

```bat
@echo off
REM Score the Brain's search against semantic-index\test-questions.md.
REM
REM   score-brain              score every question
REM   score-brain --self-test  check the scorer itself, no model needed
REM
REM Run this BEFORE and AFTER any change to the model, the chunking, or the
REM files - so the change can be measured instead of guessed.
REM
REM Reads only. Never changes the Brain, never touches Revit.

"%~dp0venv\Scripts\python.exe" "%~dp0score_brain.py" %*
echo.
pause
```

- [ ] **Step 10: Take the first real score**

Run: `semantic-index\score-brain.cmd`
Expected: a score block over the seven seeded questions, plus any that missed. **Whatever the number is, it is the baseline — do not tune anything to improve it in this task.**

- [ ] **Step 11: Commit**

```bash
git add .gitignore semantic-index/test-questions.md semantic-index/score_brain.py semantic-index/score-brain.cmd semantic-index/score-history.md
git commit -m "Semantic index: a score card, so retrieval changes can be measured not guessed

The 2026-08-06 run recorded 13/24 but threw the questions away. Seven are
recoverable from brain-log.md; the rest are for Ajmal to write.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Auto re-index — the index can never go stale again

Two hooks. `PostToolUse` only marks a flag (instant, cannot fail an edit); `Stop` does the rebuild once per turn rather than once per file.

**Files:**
- Create: `tools/reindex-mark.mjs`
- Create: `tools/reindex-run.mjs`
- Modify: `.claude/settings.json`

**Interfaces:**
- Consumes: `semantic-index/brain_index.py`, run through the venv Python.
- Produces: flag file at `semantic-index/run-temp/.reindex-needed`, created by `reindex-mark.mjs` and deleted by `reindex-run.mjs`.

- [ ] **Step 1: Write the marker hook**

Create `tools/reindex-mark.mjs`:

```javascript
#!/usr/bin/env node
// PostToolUse hook - mark the semantic index as needing a rebuild.
//
// This does NOT rebuild. It writes a zero-byte flag and exits, because PostToolUse
// fires once per file edit and a rebuild is ~2.8s: twenty edits in a turn would cost
// nearly a minute of waiting for one index that is only read at the end anyway.
// tools/reindex-run.mjs does the actual rebuild once, on Stop.
//
// It deliberately does not parse the hook's stdin to check WHICH file was edited.
// Over-marking is free - a rebuild with nothing changed is ~2.3s and correct - while
// mis-parsing an undocumented payload would silently stop marking altogether, which
// is the exact failure this whole task exists to remove.
//
// Must never fail an edit. Every path exits 0.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

try {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const flagDir = path.join(here, "..", "semantic-index", "run-temp");
  fs.mkdirSync(flagDir, { recursive: true });
  fs.writeFileSync(path.join(flagDir, ".reindex-needed"), "");
} catch {
  // A missing flag costs one stale search and a STALE INDEX warning.
  // A thrown hook costs the user their edit. Stay silent.
}

process.exit(0);
```

- [ ] **Step 2: Verify the marker creates the flag**

Run: `node tools/reindex-mark.mjs && ls -la semantic-index/run-temp/.reindex-needed`
Expected: the file is listed, size 0.

- [ ] **Step 3: Write the rebuild hook**

Create `tools/reindex-run.mjs`:

```javascript
#!/usr/bin/env node
// Stop hook - rebuild the semantic index if anything was edited this turn.
//
// The index is a snapshot; nothing used to refresh it. A session that edited the
// Brain and forgot `index-brain.cmd` left every later session searching an older
// copy - answering confidently out of text that no longer exists.
//
// NEVER call semantic-index/index-brain.cmd from here. That wrapper ends with
// `pause`, which blocks forever waiting for a keypress. Call brain_index.py through
// the venv Python directly.
//
// Exits 0 in every case. A failed rebuild is worth a warning, never a blocked turn -
// the search prints its own STALE INDEX banner if this did not run.

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "semantic-index");
const flag = path.join(semanticRoot, "run-temp", ".reindex-needed");

if (!fs.existsSync(flag)) process.exit(0);

// Windows puts the venv interpreter in Scripts/, everywhere else in bin/.
const candidates = [
  path.join(semanticRoot, "venv", "Scripts", "python.exe"),
  path.join(semanticRoot, "venv", "bin", "python"),
];
const python = candidates.find((p) => fs.existsSync(p));

if (!python) {
  console.error(
    `Semantic index not rebuilt: no venv Python found at\n  ${candidates.join("\n  ")}\n` +
      `Searches will warn STALE INDEX until semantic-index\\index-brain.cmd is run by hand.`
  );
  process.exit(0);
}

const result = spawnSync(python, [path.join(semanticRoot, "brain_index.py")], {
  encoding: "utf8",
  cwd: semanticRoot,
});

if (result.error || result.status !== 0) {
  const detail = result.error ? result.error.message : `${result.stdout || ""}${result.stderr || ""}`;
  console.error(`Semantic index rebuild failed:\n${detail}`);
  process.exit(0);   // warn, never block
}

try {
  fs.unlinkSync(flag);
} catch {
  // Flag already gone; the next run is a ~2.3s no-op.
}

process.exit(0);
```

- [ ] **Step 4: Verify the rebuild runs and clears the flag**

The flag was created in Step 2, so it should fire now.

Run: `node tools/reindex-run.mjs; echo "exit=$?"; ls semantic-index/run-temp/.reindex-needed 2>&1`
Expected: `exit=0`, then a "No such file" style message proving the flag was cleared. Takes roughly 2–4 seconds.

- [ ] **Step 5: Verify it is a no-op when nothing was edited**

Run: `node tools/reindex-run.mjs; echo "exit=$?"`
Expected: `exit=0`, returning instantly with no output — the flag is gone, so nothing runs.

- [ ] **Step 6: Wire both hooks in**

In `.claude/settings.json`, add `reindex-mark.mjs` to the **existing** `PostToolUse` block (alongside the consistency checker, not replacing it), and `reindex-run.mjs` to the **existing** `Stop` block (alongside the voice narrator).

The `PostToolUse` block becomes:

```json
    "PostToolUse": [
      {
        "matcher": "Edit|Write|NotebookEdit",
        "hooks": [
          {
            "type": "command",
            "command": "node \"tools/verify-consistency-hook.mjs\""
          },
          {
            "type": "command",
            "command": "node \"tools/reindex-mark.mjs\""
          }
        ]
      }
    ]
```

The `Stop` block becomes:

```json
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"tools/voice/narrate-hook.mjs\""
          },
          {
            "type": "command",
            "command": "node \"tools/reindex-run.mjs\""
          }
        ]
      }
    ]
```

- [ ] **Step 7: Verify the settings file is still valid JSON**

Run: `node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8')); console.log('settings.json is valid')"`
Expected: `settings.json is valid`

- [ ] **Step 8: Prove the whole loop end-to-end**

Append a throwaway line to a real indexed file, then confirm search picks it up without anyone running the rebuild by hand.

1. Add the line `<!-- reindex proof, delete me -->` to the end of `knowledge/INDEX.md` using the Edit tool.
2. Confirm the flag appeared: `ls semantic-index/run-temp/.reindex-needed`
3. End the turn (the Stop hook fires).
4. Next turn, run: `semantic-index\ask-brain-hybrid.cmd "reindex proof delete me"`

Expected: **no `STALE INDEX` banner.** That banner appearing means the Stop hook did not fire — check the settings wiring before continuing.

5. Remove the throwaway line from `knowledge/INDEX.md`.

- [ ] **Step 9: Commit**

```bash
git add tools/reindex-mark.mjs tools/reindex-run.mjs .claude/settings.json
git commit -m "Reflex: the semantic index now rebuilds itself, so it cannot go stale

PostToolUse marks a flag; Stop does one rebuild per turn instead of one per
edited file. Never calls index-brain.cmd - that wrapper ends in \`pause\` and
would block a hook forever.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 10: Record it in the Brain's own log**

Per `CLAUDE.md`, structural changes go in `knowledge/brain-log.md`. Append one entry at the end of the list:

```markdown
- 2026-08-13 — **The semantic index now rebuilds itself.** It was a snapshot that only
  refreshed when someone remembered `index-brain.cmd`, so any session that forgot left
  every later session searching an older copy of the Brain — answering confidently out
  of text that no longer existed. `tools/reindex-mark.mjs` (PostToolUse) writes a flag
  and returns instantly; `tools/reindex-run.mjs` (Stop) does one rebuild per turn rather
  than one per edited file. Neither calls `index-brain.cmd`: that wrapper ends with
  `pause` and would have blocked the hook forever waiting for a keypress. The marker
  deliberately does not parse the hook payload to check *which* file changed — a rebuild
  with nothing changed costs 2.3 s and is correct, while a mis-parsed payload would
  silently stop marking, which is the exact failure this removes.
```

- [ ] **Step 11: Commit the log entry**

```bash
git add knowledge/brain-log.md
git commit -m "Log: the index now rebuilds itself on the Stop hook

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: `search_brain` as a native MCP tool

Brain search currently only works where Windows batch files run. On Claude Code for web it silently does nothing — the same trap that cost a whole unchecked session on 2026-08-04.

**Files:**
- Create: `mcp-server/brain-tools/search-brain.js`
- Modify: `mcp-server/index.js`
- Modify: `mcp-server/tools/README.md`

**Interfaces:**
- Consumes: `semantic-index/brain_search_hybrid.py` via the venv Python; `asToolResult` from `mcp-server/shared/tool-result.js`.
- Produces: an MCP tool named `search_brain`, registered by `export function register(server)` — the same contract every tool in `mcp-server/tools/` uses.

- [ ] **Step 1: Confirm how a tool with parameters declares its schema**

`ping.js` takes no arguments, so it does not show the schema style. Check one that does.

Run: `grep -n "zod\|server.tool\|z\." mcp-server/tools/count-elements.js | head -20`
Expected: shows whether the schema is a Zod raw shape (`{ query: z.string() }`) and how `zod` is imported. **Match that style exactly in Step 2** — if it differs from the code below, change the code below, not the existing tools.

- [ ] **Step 2: Write the tool**

Create `mcp-server/brain-tools/search-brain.js`:

```javascript
// search_brain - ask the AJ AI Brain a question in plain English.
//
// WHY THIS IS NOT IN mcp-server/tools/:
// tools/brain-status.mjs:90 counts every .js in mcp-server/tools/ and reports the
// total as "native tools", meaning Revit bridge tools. This one never touches Revit,
// so putting it there would quietly turn a true number into a false one - the exact
// documentation-ahead-of-reality failure this repo keeps having.
//
// WHY IT EXISTS AT ALL:
// ask-brain-hybrid.cmd is a Windows batch file. On Claude Code for web it does not
// fail - it silently does nothing, which is how a whole session ran unchecked on
// 2026-08-04. A tool call works everywhere.
//
// Reads only. Never touches Revit, so it works with Revit closed.

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { z } from "zod";
import { asToolResult } from "../shared/tool-result.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "..", "semantic-index");

function venvPython() {
  const candidates = [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ];
  return candidates.find((p) => fs.existsSync(p)) || null;
}

export function register(server) {
  server.tool(
    "search_brain",
    "Search the AJ AI Brain in plain English for the skill, knowledge note or C# " +
      "fragment that answers a question. Matches meaning as well as exact words. " +
      "Use this before writing any new C# or answering any Revit how-to question. " +
      "Read the top 3-5 results, not just the first.",
    {
      query: z.string().describe("The question, in plain English, in the user's own words"),
      top: z.number().int().min(1).max(20).optional()
        .describe("How many results to return (default 5)"),
      area: z.enum(["fragment", "knowledge", "skill", "guide"]).optional()
        .describe("Restrict to one part of the Brain"),
    },
    async ({ query, top, area }) => {
      try {
        const python = venvPython();
        if (!python) {
          return asToolResult({
            success: false,
            error:
              "No Python found in semantic-index/venv. Set it up per " +
              "semantic-index/requirements.txt, then retry.",
          });
        }

        const args = [path.join(semanticRoot, "brain_search_hybrid.py"), query];
        if (top) args.push("--top", String(top));
        if (area) args.push("--area", area);

        const result = spawnSync(python, args, {
          encoding: "utf8",
          cwd: semanticRoot,
          maxBuffer: 10 * 1024 * 1024,
        });

        if (result.error) {
          return asToolResult({ success: false, error: result.error.message });
        }
        if (result.status !== 0) {
          return asToolResult({
            success: false,
            error: `${result.stdout || ""}${result.stderr || ""}`.trim() || "search failed",
          });
        }

        return asToolResult({ success: true, results: result.stdout.trim() });
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
```

- [ ] **Step 3: Register it**

In `mcp-server/index.js`, add the import beneath the last existing import (after the `registerDeleteElements` line):

```javascript
import { register as registerSearchBrain } from "./brain-tools/search-brain.js";
```

And add the registration call beneath `registerDeleteElements(server);`:

```javascript
registerSearchBrain(server);
```

- [ ] **Step 4: Verify the server still starts**

Run: `node --check mcp-server/brain-tools/search-brain.js && node --check mcp-server/index.js && echo "both parse"`
Expected: `both parse`

- [ ] **Step 5: Verify the tool actually returns results**

The registration path is not exercised by `--check`, so call the underlying search the same way the tool does.

Run: `semantic-index/venv/Scripts/python.exe semantic-index/brain_search_hybrid.py "how do I undo a mistake" --top 3`
Expected: three ranked results with `found by:` lines. If this fails, the tool will fail identically — fix it here first.

- [ ] **Step 6: Confirm the Revit tool count did not move**

Run: `node tools/brain-status.mjs | grep -i "native tools"`
Expected: still **17**. If it says 18, `search-brain.js` was put in `mcp-server/tools/` by mistake — move it to `mcp-server/brain-tools/`.

- [ ] **Step 7: Document it**

In `mcp-server/tools/README.md`, add a short section at the end. Keep it outside the numbered list of the 17 Revit tools, and **do not write a file count into it** — `tools/verify-consistency.mjs` check 8 verifies any "searches all N files" claim, and a hardcoded number here becomes one more thing to drift.

```markdown
## Not a Revit tool: `search_brain`

`mcp-server/brain-tools/search-brain.js` registers one more tool, `search_brain`, which
asks the AJ AI Brain a plain-English question and returns the skills, knowledge notes and
C# fragments that answer it. It never touches Revit, so it works with Revit closed.

It lives outside this folder on purpose: `tools/brain-status.mjs` counts every `.js` here
and reports the total as native **Revit** tools. A non-Revit tool in this folder would
quietly make that number wrong.

It is the portable replacement for `semantic-index\ask-brain-hybrid.cmd`, which silently
does nothing in any session without Windows batch — Claude Code for web included.
```

- [ ] **Step 8: Run the consistency checker**

Run: `node tools/verify-consistency.mjs`
Expected: `All checks passed - no drift found.` Fix any drift now, in this task — that is the repo's standing rule.

- [ ] **Step 9: Rebuild the index**

`mcp-server/tools/README.md` is one of the indexed documents, so it must be re-read. Task 2's hook handles this automatically once wired, but run it explicitly here in case Task 3 was done first.

Run: `semantic-index/venv/Scripts/python.exe semantic-index/brain_index.py`
Expected: completes in a few seconds, reporting the files it re-read.

- [ ] **Step 10: Commit**

```bash
git add mcp-server/brain-tools/search-brain.js mcp-server/index.js mcp-server/tools/README.md
git commit -m "Brain search is now an MCP tool, so it works outside Windows

ask-brain-hybrid.cmd silently does nothing on Claude Code for web - the same
trap that cost an unchecked session on 2026-08-04. Kept out of mcp-server/tools/
so brain-status keeps reporting 17 Revit tools truthfully.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 11: Re-score, and confirm nothing regressed**

Run: `semantic-index\score-brain.cmd`
Expected: the same score as Task 1 Step 10. Phase 1 changes **when** search runs, not **how well** it ranks — a moved number here means something unintended changed.

---

## Definition of done for Phase 1

- [ ] `score-brain.cmd` prints a score, and `semantic-index/score-history.md` has at least two entries
- [ ] Editing any Brain file and ending the turn leaves the index fresh, with no `STALE INDEX` banner and nobody running a command
- [ ] `search_brain` is callable as a tool, with Revit closed
- [ ] `node tools/brain-status.mjs` still reports **17** native tools
- [ ] `node tools/verify-consistency.mjs` reports no drift
- [ ] `knowledge/brain-log.md` records the auto-reindex change

## What Phase 1 deliberately does not do

- **It does not make search more accurate.** The score after Phase 1 should equal the score before it. Accuracy is Phase 3 — a bigger embedding model, splitting the two oversized knowledge files, and the self-writing vocabulary. Phase 1 makes those measurable.
- **It does not add auto-search on every prompt.** That is Phase 2 (§4.2 of the spec) and needs the gating design settled first.
- **It creates no agents.** No `.claude/agents/` folder is added.

## Open item carried from the spec

`semantic-index/test-questions.md` ships with seven questions. **The other 17 are Ajmal's to write**, and must not be written by whoever is tuning the search. Until they exist, the score is real but thin — treat it as a regression guard, not a quality verdict.
