# Handover — pick this up on the Windows PC

Last updated: **2026-08-22.** The 2026-08-22 section directly below is a cloud session with no Revit; everything under it is from the 2026-08-20 Windows session and is still the live bridge work. Read this section, then THE BRIDGE section, then the UPDATE section.

---

## 2026-08-22 — a documentation-only session, and what it left for you

No Revit, no bridge, no model touched. The whole session was one job: **find every place this Brain states
a number about itself and check it against disk.** It found a lot, and the durable outcome is not the
fixes — it is four new checks that make the same drift impossible to ship again.

### What was wrong

| Where | What it claimed | Truth |
|---|---|---|
| 8 documents | 267 / 269 / 282 / 285 fragments | **290** |
| 3 places | 17 or 19 native tools | **20** |
| 19 fragment headers | "NOT YET LIVE-VERIFIED" | proven live 2026-08-06/07, recorded in `scripts/README.md` only |
| 7 fragment headers + 4 docs | named outside tools | stripped, per the 2026-08-20 rule that never reached `scripts/` |
| `README.md` | search scores `3/14 at #1` | **5/28 at #1, MRR 0.267** — and `6/14 in top 5` matched no run at all |
| `tools/api-surface.mjs` | stamped every regeneration `2026-08-20` | a hardcoded literal; now reads the clock |
| this file | 270 / 283 / 287 fragments, `3/14` | stamped or corrected above |

### The four checks added, all tested by deliberately breaking a file first

- **9** — every fragment / skill / native-tool count stated anywhere in markdown, against disk.
- **10** — each fragment's own header status against its `scripts/README.md` row (241 verified fragments).
- **11** — outside-source names in `scripts/`, `skills/` and `knowledge/` (344 files). Found 3 more on its
  first run, in a file the hand-written grep had never looked at.
- **12** — any `N/M at #1` retrieval score against the last run in `semantic-index/score-history.md`.

`tools/verify-consistency.mjs` now runs **12 checks** and the edit hook runs it on every file change. The
PowerShell copy trails at 8 and the docs say so — do not port checks 9–12 from a Linux container, that is
the `.ps1` encoding trap this repo has already been bitten by twice.

### Decisions taken by Ajmal this session, so they are not re-argued

1. **KV cache / prompt caching — nothing to build.** Every layer that pays is already on by default or
   already built and measured here. Full record in `semantic-index/rag-architecture-decisions.md`.
2. **`knowledge/dynamo-vocabulary-map.md` deleted** — *"we dont have anyting related to dynamo."* Git has it.
3. **The NFPA source links stay** — *"keep the nfpa links."* Fire-code values whose own heading warns they
   are secondary summaries; a blockquote in that file now records the decision, and check 11 skips it.
4. **brain-log.md keeps its full detail** — it is 11% of the searchable corpus; at 20%, move entries older
   than 60 days to `docs/brain-log-archive.md` (outside the index). `brain-status.mjs --full` prints the
   share every session. Move, never shorten.

### What Ajmal still has to do — none of it possible from a cloud session

1. **Merge PR #30** — https://github.com/Ajmalpshaik/AJ-AI-Brain/pull/30 ("Ready for review", then "Merge").
2. **Run `tools\check-scripts.cmd`.** One fragment was rewritten this session without a compiler:
   `filters/by-identity/filter-by-wrong-category.cs` used `ElementId.IntegerValue`, removed at Revit 2024.
   It was written on 2026-08-21, *the day after* the whole library was migrated off that API — which is why
   check 11's lesson is that a sweep fixes what exists and does nothing about the next file somebody writes.
3. **Two more test questions, in his own words.** The set is at **28 of 30**. Two finished features — the
   cross-encoder re-ranker and skill weighting — are switched off only because the set is too small to
   judge them. `semantic-index/rag-architecture-decisions.md` records that assistant-written questions do
   not count, so this one genuinely cannot be done for him.

Everything below this line is the 2026-08-20 Windows session and is unchanged.

---

This replaces the 2026-08-14 handover, which
had gone stale (it said 270 fragments; there were 287 that day, and 290 now — which is the point).

## The prompt

```text
Read D:\Ajmal\AJ AI Brain\docs\HANDOVER.md and CLAUDE.md first, then continue the work listed there.
```

---

## THE BRIDGE NOW HANDLES SEVERAL REVITS AND SEVERAL PROJECTS — 2026-08-20, end of session

**Do this first: Ajmal must close and reopen Revit.** The add-in only loads at Revit startup, so
**AJ Tools 1.56.0 is built and deployed but not yet running** in the session that was open.

### What changed, and why it was two problems

| Question | Fix | Version |
|---|---|---|
| Which **Revit session**? | one named pipe per process id | AJ Tools **1.55.0** |
| Which **project inside it**? | optional `document` title on the request | AJ Tools **1.56.0** |

The bridge used to assume exactly one Revit: every session hosted the same pipe name, which has room for
two server instances — and one Revit uses BOTH (one servicing the chat, one listening so preemption is
instant). **Measured, not inferred:** creating four servers on one name gives two, then
`All pipe instances are busy`. So a second Revit's bridge simply refused to start.

The second half is subtler and was still live after the first fix: `RevitExecutionService` builds its
globals from `app.ActiveUIDocument`, so `Document` means **the front window**. With two projects open in
one Revit, clicking the other one silently moved where the next script landed.

**The rule both halves share: a name that does not resolve is an ERROR listing the real choices, never a
fall back to whatever is in front.** Falling back IS the failure.

**Ajmal's rule for choosing, asked and answered:** *ask, don't guess.* One Revit, one project — no
prompt, behaves exactly as it always did. More than one — nothing is sent until he names one.

### Three new tools (native tools 17 → 20)

`list_revit_instances` · `use_revit_instance` · `use_revit_document`. They read
`%APPDATA%\AJTools\bridges\<pid>.json`, which each Revit writes with its version and open document.

### Proven live on 2026-08-20, and what is NOT

**Proven:** two Revits (`school.rvt` pid 1320, `vila.rvt` pid 23876) hosted bridges **simultaneously**,
and 10 sheets were created in each — `school 01..10`, `vila 01..10` — switching between them mid-job and
verifying each by reading it back. That is the thing that was impossible before.

**NOT yet proven, and it needs Ajmal:**

1. **Document targeting has never run.** 1.56.0 was built after that test, so the running Revit did not
   have it. Open **two projects in ONE Revit**, then pin one with `use_revit_document` and confirm a
   write lands in the named project no matter which window is in front.
2. **The three tools have never been called.** They were added after this session's mcp-server had
   already loaded, so the switching on 2026-08-20 was done by hand — by rewriting `ajai-bridge.json` to
   point at the other instance file. A fresh chat gets the real tools.

### Known limit, recorded rather than hidden

A `UIDocument` for a background project still carries **that project's** active view. So view-scoped work
(isolate, colour, **grayout**, crop) follows Revit, not the caller. Model work — create, rename,
parameters, schedules — is unaffected. Do not quietly widen this claim.

### Small thing worth doing

Both `school.rvt` and `vila.rvt` still have **Project Name and Project Number unset** (Revit's literal
placeholder text). Title blocks normally read those fields, so sheets print with blanks.

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
assemblies for 2027+; do not touch the fragments themselves.**

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

**DONE on the PC, 2026-08-20. Every Revit installed on this machine now compiles the whole library:**

| Revit | Result |
|---|---|
| 2020 | **SAFE — 287/287** |
| 2024 | **SAFE — 287/287** |
| 2027 | **SAFE — 287/287** |

Three separate problems were behind the original failures, and they needed three different answers.

**1. Migration slips (6 fragments).** The version-proofing pass left `int` where its own new code returns
`ElementId`, each time on the line *directly after* a correctly migrated one — `mep-grayout.cs` is the
clearest: `ElementId wallId = IdOf(...)` then `int doorId = IdOf(...)`. One word each. Plus three
`(BuiltInCategory)someId` casts that needed `(BuiltInCategory)IdValue(id)`, which is what the prelude's
helper is for. `sprinkler-layout-options.cs` was not a version bug at all — 9 fields in a `System.Tuple`,
which stops at 8, so it had **never compiled on any version**; now a named ValueTuple.

**2. Real API removals (13 fragments).** These compiled on the old Revit *because* they used the old
surface. Every one is now resolved **by name at run time**, so a single source still serves 2020:

| Removed | Replacement |
|---|---|
| `ParameterType` | `SpecTypeId` |
| `DisplayUnitType` | `UnitTypeId` (electrical — the one conversion that cannot be arithmetic) |
| `UnitType`, `FormatOptions.DisplayUnits`/`UnitSymbol` | `GetAllMeasurableSpecs()`, `GetUnitTypeId()`, `GetSymbolTypeId()` |
| `IndependentTag.TaggedLocalElementId`, `.LeaderElbow` | `GetTaggedLocalElementIds()`, `Get`/`SetLeaderElbow(Reference)` |
| `Document.Create.NewFloor` | `Floor.Create` |
| `BuiltInParameterGroup`, `Definition.ParameterGroup` | `GroupTypeId`, `GetGroupTypeId()` |
| string-rule `caseSensitive` argument | dropped at 2023 — comparison is case-insensitive by definition |

The overload is always chosen from the **real method's own parameter type**, never by testing whether
`ForgeTypeId` exists — Revit 2021 ships `ForgeTypeId` while those methods there still take the old enum.

**3. A harness fault that looked like 283 broken fragments.** `verify-fragments-compile.ps1` compiled
against the .NET **Framework** reference set while Revit 2027 runs on **.NET 10**, so every fragment
failed with `CS0012 ... System.Runtime 10.0.0.0`. It now detects a .NET-based Revit from the
`RevitAPI.runtimeconfig.json` beside `RevitAPI.dll` and uses the matching reference pack with
`/nostdlib+`. **If a future Revit reports every fragment failing, suspect this first.**

### The one thing that could NOT be fixed, and should not be re-attempted

**`creators/create-hvac-zone.cs` cannot work on Revit 2027.** Not a naming change — the capability is
gone: `Autodesk.Revit.Creation.Document` has no zone method left, `Zone.AddSpaces` does not exist, and
`Space.Zone` is read-only. On 2027 an HVAC Zone must be created and filled through the Revit UI. The
fragment compiles there and reports exactly that. It still works normally on 2020 and 2024.

That was settled by **reading 2027's own `RevitAPI.dll`**, using the new
[`tools/probe-revit-api`](../tools/probe-revit-api/README.md). It exists because Windows PowerShell 5.1
cannot load a .NET 10 assembly at all, so the usual `Assembly::LoadFrom` one-liner fails on exactly the
versions whose API has moved most. Use it before assuming a member was renamed.

### What is still open

1. **Nothing here has been run against a live model.** Step 2 below is untouched and is the only thing
   that proves correctness. The reflection work deserves it most: a differently-shaped API can compile
   perfectly and still act on the wrong element.
2. Ajmal's 15 test questions for the search (step 5 below) — still the highest-value thing only he can do.

**Still true and unchanged: compiling is a floor, not a ceiling.** None of the 2026-08-20 work has been
run against a real model yet.

## What happened on 2026-08-20 — and the one thing that matters about it

A lot changed, and **none of it has been compiled or run.** The whole session happened in a Linux
container with no Revit, no C# compiler and no access to Autodesk's sites. Everything was verified
statically — carefully, but statically.

So the brain-status line saying **83% verified describes the state BEFORE this session.** Treat every
fragment touched on 2026-08-20 as unproven until step 1 below says otherwise.

What changed:

1. **The whole library was made version-proof** (287 fragments that day; 290 now) — one source now runs on Revit 2020 through 2027. 202
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
PC and compile-checks every fragment against each, then says in plain words which versions are safe.

**This single command answers the whole session.** Green everywhere means the version-proofing worked.
Any FAIL list is a small fix — send it to Claude, because every change follows one of three patterns, so
one fix usually applies across the library.

Do this before anything else. Everything below is wasted effort if step 1 is red.

### 2. DONE 2026-08-20 — the migration holds on a live model, and the id assumption is confirmed

Ran on Revit 2020, model `Project1` (3,262 elements, 4 rooms, 2 levels). **`{l.Id}` prints the bare id
number exactly as `{l.Id.IntegerValue}` used to** — observed as `Id 316` for the active view and
`Id 918776`…`918785` for the rooms, with no `Autodesk.Revit.DB.ElementId` wrapper text and no braces.
**That settles the assumption the whole 211-site migration rests on: no further change needed.**
`context-session-start.cs` ran clean and reported Revit 2020 / 32-bit ElementId / DisplayUnitType, no
links, 0 warnings. UNITS printed `level 'Level 1' elevation displays as: 0` — a bare `0` with no unit
suffix, which is what this project displays; every mm figure in the session was converted explicitly
with `/ 304.8` regardless, per START-HERE rule 3.

Also proven the same session, well beyond the pilot: the four fire-sprinkler fragments (survey, grid,
place, audit) — 38 heads placed and independently audited. See the foot of `knowledge/brain-log.md`.
**Still true: none of the 2024-only reflection fixes has been run live.**

<details><summary>The original step 2 instructions, kept for the parts not yet done</summary>

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

</details>

**Still owed from step 2:** `mep-grayout.cs` and the tagging recipe have not been run live. They were the
two named as exercising the parts the simple fragments do not, and `mep-grayout.cs` is Ajmal's own
standing "do the grayout" job — it was one of the six fixed on 2026-08-20 and has only been compiled.

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

**The number to beat, as of 2026-08-21, is `5/28 at #1, MRR 0.267` on `all-MiniLM-L6-v2`** (the test set doubled from 14 rows to 28 that day, so it is not comparable to the `3/14, MRR 0.325` this line used to quote) — in
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
- **Two new fragments need one live run each — nothing else is outstanding on them.** Added 2026-08-20
  after Ajmal asked for the useful capabilities from an outside tool evaluation to be kept "in our part".
  Both are written as this Brain's own, carry no outside reference, and are **289/289 compile-clean on
  Revit 2020, 2024 and 2027** — but compiling is a floor, see the rule at the bottom of this file.
  - [`action-test-view-filter-match.cs`](../scripts/actions/reporting/action-test-view-filter-match.cs)
    — dry-runs a View Filter against `elements` without applying it. **To verify:** point it at a filter
    you already know the answer for, with a deliberate mix — some elements it catches, and some from a
    category the filter was never scoped to. The third verdict (*N/A — category not in scope*) is the
    whole reason it exists; confirm those land as N/A and not as "no match".
  - [`action-manage-named-set.cs`](../scripts/actions/visibility/action-manage-named-set.cs) — name a
    set once, then select / isolate / hide / show it by name. **To verify:** `mode="list"` first (it is
    read-only), then `create` from a small selection, then `isolate`, then `show`. Also worth proving
    the staleness report: delete one element of a saved set and re-run — it should say so out loud
    rather than quietly acting on a shorter list.

- `knowledge/live-model/core.md` is at 303 lines, just past the ~300-line split rule, and has not been
  reviewed for splitting.

## The two rules that matter most here

**Ajmal is not a coder** — his own words, 2026-08-20: *"am not a coder or programmer i dont know the
programing side anything but i know how to work in revit"*. Every programming decision is the
assistant's. Ask him Revit questions; make the code decisions and report them.

**Compiling is a floor, not a ceiling.** A fragment can compile perfectly on every Revit version and
still act on the wrong elements. Step 1 proves nothing crashes. Step 2 is what proves it is right — one
element first, check the real result, then trust it on a batch.
