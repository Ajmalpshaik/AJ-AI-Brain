# Handover — pick this up on the Windows PC

Last updated: **2026-08-20**, updated again on the Windows PC — read the UPDATE section first. This replaces the 2026-08-14 handover, which
had gone stale (it said 270 fragments; there are now 287).

## The prompt

```text
Read D:\Ajmal\AJ AI Brain\docs\HANDOVER.md and CLAUDE.md first, then continue the work listed there.
```

---

## UPDATE — 2026-08-20, later the same day, ON the Windows PC

The remote session above was picked up on the PC. **Step 1 and step 3 below are now done, and both
were blocked by bugs that had to be fixed first.** Read this section instead of taking steps 1 and 3
at face value.

### Two things were broken on arrival, both now fixed

1. **The search was dead.** The remote session made `bge-small-en-v1.5` the *default* embedding model,
   but its weights are a separate ~127 MB download that had never been fetched. Every single
   `ask-brain-hybrid` call died with "model has not been downloaded yet". A default that requires a
   download before the tool runs at all is not a default, so `brain_common.py` now defaults to
   `all-MiniLM-L6-v2`, which ships inside chromadb and always works. BGE is still one env var away —
   which is all the A/B ever needed. Index rebuilt on MiniLM: **348 files, 3750 chunks**, search
   verified working.

2. **`tools/check-scripts.cmd` could not run at all** — the very command step 1 calls "the single
   command that answers the whole session". It was written in the Linux container as UTF-8 **without a
   BOM**, and Windows PowerShell 5.1 reads a BOM-less file as ANSI, so the eight em dashes in it
   corrupted and broke the string terminators. This is the exact trap `CLAUDE.md` warns about. Fixed by
   adding a UTF-8 BOM — which is what the three `.ps1` files that already worked all have.
   **Lesson for any future container session: a `.ps1` written there needs either a BOM or pure ASCII.**

### What step 1 actually found, once it could run

| Revit | Result |
|---|---|
| 2020 | **281 of 287 compile** — 6 real failures |
| 2024 | **274 of 287 compile** — 13 real failures |
| 2027 | 4 of 287 — **a harness bug, not 283 broken fragments. See below.** |

So the version-proofing largely held. It did not hold everywhere.

**Revit 2027 is a harness problem, and it is well understood.** Every failure is
`error CS0012: The type 'Object' is defined in an assembly that is not referenced. You must add a
reference to assembly 'System.Runtime, Version=10.0.0.0'`. Revit 2027 runs on **.NET 10**, so its
`RevitAPI.dll` references `System.Runtime 10.0.0.0`, while `verify-fragments-compile.ps1` still compiles
against the .NET Framework reference set. The checker's own advice covers this case exactly: *"unless the
error names a type the harness failed to supply"*. **Fix the harness to pass the .NET 10 reference
assemblies for 2027+; do not touch the 283 fragments.**

**The real fragment failures — these are genuine and worth fixing:**

Fails on **both 2020 and 2024** (fix these first):

- `recipes/mep-grayout.cs` — **this one matters most.** It is Ajmal's own standing "do the grayout" job.
- `actions/sheets-views/action-report-schedule-definition.cs`
- `actions/structural-changes/action-place-accessory-on-run.cs` — already a known open item, see below.
- `context/context-model-categories.cs`
- `recipes/connect-equipment-to-air-terminals.cs`
- `recipes/sprinkler-layout-options.cs`

Fails on **2024 only** (7 more): `action-add-project-parameter.cs`, `context-project-units.cs`,
`create-floor.cs`, `filter-by-tag-status.cs`, `create-equipment-family-from-datasheet.cs`,
`create-parametric-box-family-with-duct-connector.cs`, `tag-elements-in-active-view.cs`.

Error codes are `CS0122` (member not accessible in that version) and `CS1061` (member does not exist)
on 2024, plus `CS0308`/`CS0030`/`CS0019`/`CS0029`/`CS1503`/`CS1501` shared with 2020 — all genuine
per-version API differences, not unit or id problems. Re-run `tools\check-scripts.cmd` any time; the full
error text lands in `fragment-compile-failures.txt` at the repo root (gitignored).

### So the next job, in order

**Done on the PC, 2026-08-20 — Revit 2020 is now 287/287, Revit 2024 is 280/287.**

The six that failed on both versions are fixed and compile-verified on 2020 and 2024. Five were a single
wrong word left by the migration — `int` where its own new code returns `ElementId`, each time on the line
*directly after* a correctly migrated one (`mep-grayout.cs` is the clearest: `ElementId wallId = IdOf(...)`
then `int doorId = IdOf(...)`). The three `(BuiltInCategory)someId` casts became
`(BuiltInCategory)IdValue(id)`, which is what the prelude's helper exists for. `sprinkler-layout-options.cs`
was a different bug entirely — 9 fields in a `System.Tuple`, which stops at 8, so it had **never compiled
on any version**; it is now a named ValueTuple.

**What is left is 7 fragments on Revit 2024, and they are NOT the same kind of problem.** They compile on
2020 precisely because they use APIs Autodesk removed afterwards, so they need reflection dispatch to keep
one source serving both — and a live run after, because a differently-shaped API can compile and still act
on the wrong element:

| Fragment | Removed API | Replacement |
|---|---|---|
| `action-add-project-parameter.cs` | `ParameterType` | `SpecTypeId` |
| `create-equipment-family-from-datasheet.cs` | `ParameterType`, `DisplayUnitType` | `SpecTypeId`, `UnitTypeId` |
| `create-parametric-box-family-with-duct-connector.cs` | `ParameterType` | `SpecTypeId` |
| `filter-by-tag-status.cs` | `IndependentTag.TaggedLocalElementId` | `GetTaggedLocalElementIds()` |
| `tag-elements-in-active-view.cs` | `TaggedLocalElementId`, `LeaderElbow` | + `GetLeaderElbow(reference)` |
| `context-project-units.cs` | `UnitType`, `DisplayUnits`, `UnitSymbol` | `ForgeTypeId` / `UnitTypeId` |
| `creators/create-floor.cs` | `Document.NewFloor` | `Floor.Create` |

`knowledge/revit-version-compatibility.md` had said `IndependentTag` affected **0 fragments** and did not
mention `UnitType` or `NewFloor` at all. Corrected — see its §4. **A source scan is a guess; compiling
against the real `RevitAPI.dll` is a measurement.**

Still outstanding, unchanged:

1. Teach `verify-fragments-compile.ps1` the .NET 10 reference set so Revit 2027 gives a real answer
   (currently reports 283 false failures — a harness fault, not the fragments).
2. Step 2 below (live Revit run) is still untouched and is what actually proves correctness.

**Still true and unchanged: compiling is a floor, not a ceiling.** None of the 2026-08-20 work has been
run against a real model yet.

## What happened on 2026-08-20 — and the one thing that matters about it

A lot changed, and **none of it has been compiled or run.** The whole session happened in a Linux
container with no Revit, no C# compiler and no access to Autodesk's sites. Everything was verified
statically — carefully, but statically.

So the brain-status line saying **83% verified describes the state BEFORE this session.** Treat every
fragment touched on 2026-08-20 as unproven until step 1 below says otherwise.

What changed:

1. **All 287 fragments were made version-proof** — one source now runs on Revit 2020 through 2027. 202
   unit conversions became arithmetic (`mm / 304.8`, which no Revit version can deprecate) and element
   ids stayed as `ElementId` instead of being turned into numbers. No `#if`, no fork.
2. **A new opening check** — `scripts/context/context-session-start.cs`, one call that reports the Revit
   version, which API generation is live, the document, what unit the project displays, unloaded links,
   closed worksets, warnings.
3. **The search was measured properly for the first time** and a real bug fixed (RRF was fusing chunk
   ranks while ranking files). The embedding model is now selectable. **Superseded the same day:** BGE was
   briefly the default and broke search on the PC — see item 1 above. The default is `all-MiniLM-L6-v2`
   again, and **BGE still has no score line at all.**
4. **A separate Revit API index** in `api-index/`, fed by reflecting over the running Revit's own
   `RevitAPI.dll`. Deliberately a different database — the Brain's search never reads it.
5. **`recipes/audit-flex-curves.cs`** — flex duct and flex pipe had no fragment at all.
6. **`tools/check-scripts.cmd`** — see step 1.
7. **Two sprinkler fragments merged in from PR #17** — `sprinkler-pipe-schedule-size.cs` (pipe sizing by
   the schedule method) and `sprinkler-set-room-hazard.cs` (hazard class recorded per Room). They were
   written before the migration, so they were brought in line the same way when the branch merged:
   arithmetic units, `ElementId` keys throughout. Step 1 covers them like everything else. Both also
   need a live run — the pipe one needs modelled sprinkler pipe connected to heads, and the hazard one
   needs three text project parameters bound to Rooms through the Revit UI, which no script can create:
   `FF_Hazard_Class`, `FF_Hazard_Source`, `FF_Standard`.

---

## What I want you to do, in this order

### 1. FIRST, AND IT TAKES A MINUTE — does the migration hold?

```
tools\check-scripts.cmd
```

Double-click it. Revit does **not** need to be open and nothing is changed. It finds every Revit on the
PC and compile-checks all 287 fragments against each, then says in plain words which versions are safe.

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

**Still owed. It is the one measurement this Brain has never taken.**

```
cd semantic-index
venv\Scripts\python.exe embed_bge.py --download     (~127 MB, once, needs huggingface.co)
venv\Scripts\python.exe embed_bge.py                (smoke test, should print PASS)
echo bge-small-en-v1.5 > embed-model.txt            (pin it, so search and index agree)
index-brain.cmd --full                              (~80 s)
score-brain.cmd
```

**The number to beat is `3/14 at #1, MRR 0.325` on `all-MiniLM-L6-v2`** — in
`semantic-index/score-history.md`, stamped with the model that produced it.

**Read the note at the bottom of `score-history.md` before you judge the result.** That row is
knife-edge: measured 2026-08-20, `what does duck mean` flips between #1 and #6 on a **2-chunk** corpus
change, which is 7% of a 14-row score all by itself. **A 1-point move either way proves nothing.** If BGE
wins or loses by one, it is a tie — say so rather than adopting or rejecting on noise.

If BGE loses, delete `embed-model.txt` and rebuild. That is exactly why the model was made selectable
rather than replaced.

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

## Since this handover was written — merged from the review branch (PR #22)

- **`semantic-index/setup.sh`** — one command sets the search up on Linux/macOS. Nothing to do on the
  PC; it exists so a container session is not searching blind. Proven on a wiped venv.
- **Model choice can be pinned** in `semantic-index/embed-model.txt` (git-ignored, per-machine), so an
  upgrade to BGE survives without setting an env var on every command — the failure item 1 above
  describes, in its other direction.
- **`docs/superpowers/` is gone** — two plans and a spec with 60 unticked boxes for work that is
  entirely built. The five genuinely live items moved into `semantic-index/rag-architecture-decisions.md`.
- **`semantic-index/rag-architecture-decisions.md`** is new: why the retrieval layer is not being
  rewritten, the six standard upgrades already measured and reverted here, and the folder-structure
  verdict. Read it before anyone proposes rebuilding the RAG.

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

- ~~A stray remote branch `claude/revit-api-surface` needs deleting by hand.~~ **Done on the PC,
  2026-08-20.** Verified first rather than trusted: `tools/api-surface.mjs` was byte-identical to `main`,
  and `knowledge/revit-api-surface.md` differed only by being the *older* generated snapshot (283
  fragments vs 285 on `main`), so nothing unique was lost. Two other fully-merged branches went with it
  — `claude/rag-folder-structure-4pui5e` and `claude/sprinkler-spacing-coverage-lp914z`, both zero
  unique commits. **`claude/rag-architecture-review-bqpjah` was kept**: it carries open **draft PR #22**
  ("Answer the 'rebuild the RAG properly' question with the measurements"), which is Ajmal's to merge or
  close. Remote is now `main` + that one branch.
- `knowledge/live-model/core.md` is at 303 lines, just past the ~300-line split rule, and has not been
  reviewed for splitting.

## The two rules that matter most here

**Ajmal is not a coder** — his own words, 2026-08-20: *"am not a coder or programmer i dont know the
programing side anything but i know how to work in revit"*. Every programming decision is the
assistant's. Ask him Revit questions; make the code decisions and report them.

**Compiling is a floor, not a ceiling.** A fragment can compile perfectly on every Revit version and
still act on the wrong elements. Step 1 proves nothing crashes. Step 2 is what proves it is right — one
element first, check the real result, then trust it on a batch.
