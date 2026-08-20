# Handover — pick this up on the Windows PC

Last updated: **2026-08-20**, end of a long remote session. This replaces the 2026-08-14 handover, which
had gone stale (it said 270 fragments; there are now 285).

## The prompt

```text
Read D:\Ajmal\AJ AI Brain\docs\HANDOVER.md and CLAUDE.md first, then continue the work listed there.
```

---

## What happened on 2026-08-20 — and the one thing that matters about it

A lot changed, and **none of it has been compiled or run.** The whole session happened in a Linux
container with no Revit, no C# compiler and no access to Autodesk's sites. Everything was verified
statically — carefully, but statically.

So the brain-status line saying **83% verified describes the state BEFORE this session.** Treat every
fragment touched on 2026-08-20 as unproven until step 1 below says otherwise.

What changed:

1. **All 285 fragments were made version-proof** — one source now runs on Revit 2020 through 2027. 202
   unit conversions became arithmetic (`mm / 304.8`, which no Revit version can deprecate) and element
   ids stayed as `ElementId` instead of being turned into numbers. No `#if`, no fork.
2. **A new opening check** — `scripts/context/context-session-start.cs`, one call that reports the Revit
   version, which API generation is live, the document, what unit the project displays, unloaded links,
   closed worksets, warnings.
3. **The search was measured properly for the first time** and a real bug fixed (RRF was fusing chunk
   ranks while ranking files). The embedding model is now selectable and defaults to `bge-small-en-v1.5`,
   **which has never been downloaded or scored.**
4. **A separate Revit API index** in `api-index/`, fed by reflecting over the running Revit's own
   `RevitAPI.dll`. Deliberately a different database — the Brain's search never reads it.
5. **`recipes/audit-flex-curves.cs`** — flex duct and flex pipe had no fragment at all.
6. **`tools/check-scripts.cmd`** — see step 1.

---

## What I want you to do, in this order

### 1. FIRST, AND IT TAKES A MINUTE — does the migration hold?

```
tools\check-scripts.cmd
```

Double-click it. Revit does **not** need to be open and nothing is changed. It finds every Revit on the
PC and compile-checks all 285 fragments against each, then says in plain words which versions are safe.

**This single command answers the whole session.** Green everywhere means the version-proofing worked.
Any FAIL list is a small fix — send it to Claude, because every change follows one of three patterns, so
one fix usually applies across the library.

Do this before anything else. Everything below is wasted effort if step 1 is red.

### 2. Then open Revit and prove three things on a real model

```
ping
scripts/context/context-session-start.cs      <- read the UNITS line and the LINKS line carefully
scripts/context/context-levels-and-grids.cs   <- the pilot: do ids still print as numbers?
```

The pilot exists to check one assumption the whole migration rests on: that `{l.Id}` prints the id
number the way `{l.Id.IntegerValue}` used to. If the ids look wrong in that output, say so — it changes
211 places and I will fix them a different way.

Then run something heavier — `mep-grayout.cs` or the tagging recipe — because those exercise the parts
the simple ones do not.

### 3. Is the new search model actually better?

```
cd semantic-index
venv\Scripts\python.exe embed_bge.py --download     (~127 MB, once)
venv\Scripts\python.exe embed_bge.py                (smoke test, should print PASS)
index-brain.cmd --full                              (~80 s)
score-brain.cmd
```

**The number to beat is `3/14 at #1, MRR 0.321` on `all-MiniLM-L6-v2`** — that line is already in
`semantic-index/score-history.md`, stamped with the model that produced it.

If BGE loses, revert in one line: `set AJ_BRAIN_EMBED_MODEL=all-MiniLM-L6-v2`. That is exactly why the
model was made selectable rather than replaced.

### 4. Optional — build the API reference index

```
run scripts/context/harvest-revit-api.cs through the bridge   (edit outputRoot first)
api-index\index-api.cmd
api-index\ask-api.cmd "how do I collect every element of a category"
```

Nice to have, not blocking. Ask it about signatures; ask the Brain about jobs.

### 5. Only Ajmal can do this — and it unlocks the most

Add about **15 more rows to `semantic-index/test-questions.md`**, in your own site words. Two things are
switched OFF and waiting on it: the cross-encoder re-ranker, and a skill-weighting that measured well but
could not be trusted on 14 questions. Both are built. Neither can be proven at this sample size.

Write them the way you would say them out loud. The file explains why they must be yours and not the
assistant's.

---

## Still open from before — carried forward, still true

- `actions/structural-changes/action-place-accessory-on-run.cs` — METHOD proven, but the file as one
  uninterrupted run threw an unisolated null reference. Read its STATUS block. Suspect: an element handle
  reused across a transaction boundary after `BreakCurve`.
- `recipes/ray-trace-to-ceiling.cs` — **an ASK, not a wait.** `Ceiling.Create` has 0 overloads before
  2022. Draw ONE ceiling by hand, ten seconds, then it runs.
- A nested shared family and a sleeve family — the last two genuine fixture blocks. Both could be
  authored via `Application.NewFamilyDocument`, which is proven to work.
- Graphify's markdown half needs a subagent pass or `GEMINI_API_KEY`. The authoritative staleness count
  is `python tools/graph-rebuild.py --check` **on Windows** — it cannot be run from a container. Note
  `brain-log.md` reports stale on nearly every run because it is written every session; judge by the
  other files.

## Housekeeping

- A stray remote branch **`claude/revit-api-surface`** needs deleting by hand. It was created by mistake
  and its content is already on `main`. The container's git proxy refuses branch deletion, so it could
  not be removed remotely.
- `knowledge/live-model/core.md` is at 303 lines, just past the ~300-line split rule, and has not been
  reviewed for splitting.

## The two rules that matter most here

**Ajmal is not a coder** — his own words, 2026-08-20: *"am not a coder or programmer i dont know the
programing side anything but i know how to work in revit"*. Every programming decision is the
assistant's. Ask him Revit questions; make the code decisions and report them.

**Compiling is a floor, not a ceiling.** A fragment can compile perfectly on every Revit version and
still act on the wrong elements. Step 1 proves nothing crashes. Step 2 is what proves it is right — one
element first, check the real result, then trust it on a batch.
