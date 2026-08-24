# Will these fragments run on Revit 2024 and above?

Every fragment in `scripts/` was written against **Revit 2020**. This note records what actually breaks
on a newer Revit — measured, never estimated.

> **2026-08-24 — the migration had a hole, and it was the tool that measures it.** Everything below
> was about `scripts/`. But roughly half the C# that reaches Revit in a normal session is never a
> fragment: the MCP server BUILDS it, line by line, in `mcp-server/tools/*.js`. `check-scripts` only
> ever read `scripts/*.cs`, so that half had **never been compiled against any Revit** — and it still
> held eight `DisplayUnitType.DUT_MILLIMETERS` calls, months after the same call was swept out of 93
> fragment files. `model_summary` and `move_elements` were dead on any Revit above 2020, and twelve
> more tools died the moment a mm filter was used, while this tool reported all green.
>
> Proved rather than argued, with the same harness: the old line compiles on Revit 2020 and fails on
> 2024 with `CS0122: 'DisplayUnitType' is inaccessible due to its protection level`.
>
> Fixed the same day, and the hole closed with it. `check-scripts` now runs
> `mcp-server/emit-generated-csharp.mjs` first, which writes out every distinct script the server can
> generate — **branches, not tools**, since three of those eight copies only appeared on a mm filter
> and one only on the numeric branch of `set_parameter_value`. Both halves then go through the same
> compiler. First full run after the fix: **393 scripts (360 fragments + 33 generated) compiled clean
> on Revit 2020, 2024 and 2027.**
>
> **The lesson is about the checker, not the API.** A green check is only as wide as what the checker
> reads. When code that reaches Revit lives somewhere new, the checker has to be told — nothing warns
> you that it is measuring a shrinking share of the truth.

**Short answer, re-measured 2026-08-22 against all 290 fragments: the migration worked.**
`tools\check-scripts.cmd` compiled the library clean on **Revit 2020, 2024 and 2027** on 2026-08-20, and
a fresh scan today finds **one** unmigrated call site left, in one file, described under "Keeping it
fixed" below. Everything else that still matches a legacy keyword is either the deliberate runtime
helper, a comment explaining it, or a different class with the same property name.

> **How to read the numbers below.** The counts in the sections that follow — *200 of 282*, *93 units*,
> *168 ElementId* — are the **pre-migration scan of 2026-08-20**, kept as the record of what was found.
> They are not a to-do list and not the current state. Whenever a number here and a fresh
> `tools\check-scripts.cmd` run disagree, **the tool wins**: it compiles against the Revit actually
> installed, which is the only thing that answers this question.

> **THE ONE COMMAND, if you read nothing else here:** `tools\check-scripts.cmd`. It compile-checks the whole
> library against every Revit installed on the PC, **without opening Revit**, in about a minute, and says in
> plain words which versions are safe. Run it after installing a new Revit — that is the whole worry,
> answered before you start rather than in the middle of a job.
>
> **DONE, 2026-08-20 — and compiled, not just written.** The migration below was applied to the whole
> library in one pass, then compile-checked: **287/287 fragments passed on Revit 2020, 2024 and 2027.**
> One source, three versions, no forks and no `#if`. What is left is under "The two deliberate
> exceptions" and "Keeping it fixed".

> **split-review: kept whole** (reviewed 2026-08-22, at 359 lines). Past the ~300-line rule and staying
> that way. This file answers exactly one question — *will my scripts still run after I install a newer
> Revit* — and the answer is a single argument that has to be read in order: what breaks, which failures
> shout and which stay silent, what the measured exposure was, what was actually changed, and how to
> write the next fragment so it does not undo the fix. The obvious seam is diagnosis (what breaks) versus
> record-and-how-to (what we did about it), and taking it would be the wrong move for a reason this file
> now documents in its own text: **a fragment written the day after the migration re-introduced the exact
> pattern the migration removed.** That happened because "what was changed" and "how to write new code"
> read as separate concerns. Putting them in separate files would make that permanent. Re-check this
> decision if the file passes ~450 lines, or if the pre-migration scan sections are ever cut down to a
> summary — that would remove roughly a third of it and change the calculation.

## Gate zero: the .NET question is NOT a fragment question

This is the part that gets confused, so it goes first.

Fragments are **C# source text** sent to `mcp__aj-tools-aj-ai__run_csharp` and compiled by the bridge
**inside the running Revit session**. They are not a compiled DLL. So a fragment has no .NET target of
its own, and nothing in `scripts/` needs rebuilding for a new .NET version.

The .NET target belongs to the **AJ AI Bridge add-in** — the compiled thing on the Revit side:

| Revit | .NET the add-in must target |
|---|---|
| 2020 | .NET Framework 4.7.2 |
| 2021 – 2024 | .NET Framework 4.8 |
| 2025 – 2026 | .NET 8 |
| 2027 | .NET 10 |
| 2028+ | Verify against the official Autodesk SDK — do not assume |

A DLL built for one family will not load in another, so 2020-2024 / 2025-2026 / 2027 are three separate
builds. **If the bridge does not load, no fragment runs at all** — check that before blaming a script.

**The check is one call, not an investigation.** Open the model in the new Revit and run the native
`ping` tool (`mcp__aj-tools-aj-ai__ping`):

- **It answers** → the add-in loaded, so the .NET target is right for this Revit. Forget .NET entirely and
  move on to the API changes below; nothing in `scripts/` is affected by it.
- **It does not answer** → stop. This is not a script problem and no fragment will run. The add-in needs
  rebuilding for that Revit's .NET family, which is a different codebase and out of this Brain's scope
  (`START-HERE.md`). Whether a retarget alone is enough, or the bridge's run-time C# compilation needs
  work too, is a bridge-side question — **verify, do not assume.**

So .NET is not 282 problems. It is one yes/no question, asked once per Revit upgrade, before anything
else.
Building the add-in is deliberately out of this Brain's scope (see `START-HERE.md`); the version rules
live in the `revit-version-matrix` skill.

## The two failure modes, and why one is far more dangerous

### 1. Units — LOUD. Fails immediately, every time, on every model. 93 fragments.

Nearly every fragment converts the user's millimetres into Revit's internal feet, and they all do it the
2020 way:

```csharp
UnitUtils.ConvertToInternalUnits(550.0, DisplayUnitType.DUT_MILLIMETERS)   // 2020
UnitUtils.ConvertToInternalUnits(550.0, UnitTypeId.Millimeters)            // 2021+
```

`DisplayUnitType` was deprecated at Revit 2021 and the version matrix treats it as **2020-only**.
Because the bridge compiles the fragment at run time, a fragment using it comes back as a **compile
error from the bridge** — instantly, with the line number, before touching the model. Tedious to fix
across 93 files, but it cannot corrupt anything and it cannot be missed.

**Needs verifying on the machine, not assumed:** whether `DisplayUnitType` still compiles at all on 2024.
Deprecated is not the same as removed. Either way it is the wrong call for 2024+, so the fix is the same.

### 2. ElementId — SILENT. Compiles fine, works on small models, breaks on big ones. 168 fragments.

Revit 2024 widened `ElementId` from 32-bit to 64-bit. `IntegerValue` still **exists** on 2024+, so the
code compiles and runs — it only **throws when an id needs more than 32 bits**.

That is the whole danger. A small test model has low ids and everything looks fine. A real project model
that has been worked on for months has high ids, and the same fragment throws — or worse, an id written
into a schedule or CSV as an `int` silently overflows.

```csharp
elem.Id.IntegerValue      // 2020-2023. On 2024+ throws above 32 bits
elem.Id.Value             // 2024+, returns long
new ElementId(idInt)      // constructor became ElementId(long) at 2024
```

In this library ids are used two ways, and both are affected: printed into reports
(`$"(Id {ws.Id.IntegerValue})"`) and fed back in to re-find an element (`doc.GetElement(new ElementId(roomIdInt))`).
Also at 2024: `BuiltInCategory` and `BuiltInParameter` became 64-bit, so `(int)BuiltInCategory.X` casts
can fail.

**Rule that matters for this Brain's reports:** never write an ElementId into a report, schedule or CSV
as `int`. Use `long`, or a string.

### 3. ParameterType — 3 fragments only

Deprecated at 2022 in favour of `SpecTypeId`. Small enough to fix by hand.

**Still not done as of 2026-08-20**, and the compiler now names the three:
`actions/parameters-naming/action-add-project-parameter.cs`,
`recipes/create-equipment-family-from-datasheet.cs` (which also uses `DisplayUnitType`), and
`recipes/create-parametric-box-family-with-duct-connector.cs`. All three compile on 2020 and fail on
2024 with `CS0122: 'ParameterType' is inaccessible due to its protection level`.

### 4. Three more the scan missed — found by compiling, 2026-08-20

The table below said `IndependentTag` affected **0 fragments**. It affects **two**, and two other
removed APIs were not listed at all. This is the same lesson the repo keeps relearning: a scan of the
source is a guess, compiling against the real `RevitAPI.dll` is a measurement.

| Fragment | Uses | Gone since |
|---|---|---|
| `filters/by-view-and-sheet/filter-by-tag-status.cs` | `IndependentTag.TaggedLocalElementId` | 2022 — use `GetTaggedLocalElementIds()` |
| `recipes/tag-elements-in-active-view.cs` | `TaggedLocalElementId`, `LeaderElbow` | 2022 — also `GetLeaderElbow(reference)` |
| `context/context-project-units.cs` | `UnitType`, `DisplayUnits`, `UnitSymbol`, `UnitSymbolType` | 2021/22 — `ForgeTypeId` / `UnitTypeId` |
| `creators/create-floor.cs` | `Document.NewFloor` | 2022 — use `Floor.Create` |

These are **real API removals, not migration slips** — which is why they compile happily on 2020. Each
now uses the reflection dispatch described above, so one source keeps serving 2020 *and* 2024, and each
still needs a live run: swapping to a differently-shaped API can compile and still tag the wrong element.

**All done 2026-08-20. Every fragment now compiles on Revit 2020, 2024 and 2027 — 287/287 on all three.**

### 5. What Revit 2027 removed on top — measured, once the harness could see it

2027 was reporting 283 false failures because `verify-fragments-compile.ps1` was compiling against the
.NET **Framework** reference set while 2027 runs on **.NET 10**. Fixed (the harness now reads the
`RevitAPI.runtimeconfig.json` Autodesk ships beside `RevitAPI.dll` and uses the matching ref pack with
`/nostdlib+`), and six genuine 2027 removals appeared underneath:

| Gone at 2027 | Replacement | Fragments |
|---|---|---|
| `BuiltInParameterGroup` | `GroupTypeId` — strip `PG_`, Title-case the rest | 3 |
| `Definition.ParameterGroup` | `Definition.GetGroupTypeId()` | 1 |
| string-rule `caseSensitive` arg | dropped at **2023**; comparison is case-insensitive by definition | 1 |
| `Document.Create.NewZone`, `Zone.AddSpaces` | **nothing — the capability is gone** | 1 |

The last row is the one that matters. `creators/create-hvac-zone.cs` cannot be made to work on 2027 by
any amount of reflection: the whole `Autodesk.Revit.Creation.Document` class has no zone method left,
`Zone.AddSpaces` does not exist, and `Space.Zone` is read-only. **On Revit 2027 an HVAC Zone must be
created and filled through the Revit UI.** The fragment compiles there and says exactly that.

That was settled by reading 2027's own `RevitAPI.dll` rather than guessing, using the new
[`tools/probe-revit-api`](../tools/probe-revit-api/README.md) — which exists because Windows PowerShell
5.1 **cannot load a .NET 10 assembly at all**, so the usual `Assembly::LoadFrom` one-liner fails on
exactly the versions whose API has changed most.

## What else lands later

| Revit | Change | Does this Brain use it? |
|---|---|---|
| 2022 | `IndependentTag` tag members renamed for multi-reference | **YES — 2 fragments.** The original "0" was wrong; see §4 |
| 2025 | `Dimension` split into `LinearDimension` / `RadialDimension` / `ArcLengthDimension`; exact-type checks fail | **No** — scanned, 0 fragments |
| 2026 | Add-in isolation via `<ManifestSettings>` | Bridge-side only |
| 2027 | .NET 10; all-user add-ins move to `Program Files` (needs admin) | **Also 6 API removals — see §5** |

## The measured picture

| | Fragments | Share |
|---|---|---|
| Clean — should run as-is | 82 | 29% |
| Touch a changed API | **200** | **71%** |
| — of those, unit conversion (loud) | 93 | 33% |
| — of those, ElementId handling (silent) | 168 | 60% |
| — of those, ParameterType | 3 | 1% |

The two groups overlap, so the sub-rows do not add to 200. **There is not one version guard anywhere in
the library** — no `#if`, no runtime feature check.

## What to do about it

1. **Check the bridge loads first.** No add-in, no fragments. Nothing below matters until it does.
2. **Fix the silent one before the loud one.** The unit errors announce themselves; the id errors wait for
   a big model. `IntegerValue` → `Value` and `new ElementId(int)` → `new ElementId(long)`.
3. **Prove one fragment end to end on the target Revit before trusting the batch** — this Brain's standing
   rule for anything unverified. One element, check the real result, then run the batch.
4. **One fragment CAN serve every version — see the section below.** An earlier draft of this note said
   you had to pick one Revit; that was wrong. `#if` is genuinely unavailable (the bridge defines no
   `REVIT20XX` symbols), but plain arithmetic and runtime reflection both work, and between them they
   cover everything this library does.
5. **Re-scan rather than trust this note.** The counts here are from 2026-08-20 against 282 fragments:

   ```
   grep -rlE "IntegerValue|new ElementId\(" scripts --include=*.cs | wc -l
   grep -rl  "DisplayUnitType"              scripts --include=*.cs | wc -l
   ```

Version rules themselves (what changed when, .NET per release, helper patterns) belong to the
`revit-version-matrix` skill, not here. This note only records **what this Brain's own library uses**.

## Writing one fragment that runs on every version

`#if REVIT2024` does **not** work here. The bridge compiles a bare source string with no version symbols
defined, so every `#if` silently takes the `#else` branch. That is not a reason to fork the library —
there are two techniques that need no compile symbols at all.

### Units — delete the API call, don't guard it

Revit's internal length unit is decimal feet, and 1 foot is **exactly** 304.8 mm by definition. So the
whole unit problem disappears if the conversion is written as arithmetic instead of an API call. There is
no API left to deprecate.

| Was | Count | Version-proof |
|---|---|---|
| `DUT_MILLIMETERS` | 184 | `mm / 304.8` |
| `DUT_SQUARE_METERS` | 12 | `m2 * 10.763910416709722` |
| `DUT_LITERS_PER_SECOND` | 5 | `ls / 28.316846592` |
| `DUT_CUBIC_FEET_PER_MINUTE` | 3 | `cfm / 60.0` |
| `DUT_CUBIC_METERS` | 2 | `m3 * 35.314666721488595` |
| `DUT_VOLTS`, `DUT_VOLT_AMPERES` | 2 | **needs verification** — electrical internals are not a plain factor |

That is 206 of 208 conversions solved with no version code whatsoever.

### ElementId — ask at runtime, the same trick the Python side uses

Reflection looks the member up by name instead of naming it in code, so the source compiles even on the
version where the member does not exist:

```csharp
static long IdOf(ElementId id) {
    var p = typeof(ElementId).GetProperty("Value");          // exists 2024+
    if (p != null) return (long)p.GetValue(id);
    return (int)typeof(ElementId).GetProperty("IntegerValue").GetValue(id);
}
static ElementId MakeId(long v) {
    var c = typeof(ElementId).GetConstructor(new[]{typeof(long)});   // exists 2024+
    if (c != null) return (ElementId)c.Invoke(new object[]{v});
    return (ElementId)typeof(ElementId).GetConstructor(new[]{typeof(int)}).Invoke(new object[]{(int)v});
}
```

Feature detection beats a version number, exactly as `revit-version-matrix` says for pyRevit: it keeps
working when the version list grows.

### The constructor was never the real problem — the INPUT DECLARATIONS are

`new ElementId(myInt)` already compiles on 2024+, because C# widens an `int` to a `long` implicitly. The
actual defect is that this library declares its id inputs as `int`:

    int viewIdInt   x11
    int roomIdInt   x11
    int levelIdInt  x11

An `int` cannot hold a 2024+ id regardless of what it is passed to. **Change the declaration to `long`**
and build the id with `MakeId`. Passing a `long` to `new ElementId(...)` directly would break 2020, which
is exactly what `MakeId` exists to avoid.

---

## What was actually changed (2026-08-20)

One pass over the whole library as it stood on 2026-08-20 — 282 fragments then, 290 now. Applied by a
balanced-paren parser rather than regex, because the value arguments contain nested calls.

| | Sites | Change |
|---|---|---|
| Unit conversions | 202 | `UnitUtils.Convert*InternalUnits(x, DUT_*)` → arithmetic |
| Id printing | 195 | `{x.Id.IntegerValue}` → `{x.Id}`, `.IntegerValue.ToString()` → `.ToString()` |
| Id collections | ~60 | `HashSet<int>` / `List<int>` / `Dictionary<int,…>` → keyed by `ElementId` |
| Id in GroupBy / OrderBy / tuples | ~20 | carry the `ElementId` itself |
| Genuinely numeric | 8 | the `IdValue` helper, in 4 files |

**Nothing needs `#if`, and nothing forked.** Every replacement is correct on Revit 2020 as well, so one
source now runs 2020 → 2027.

### Why `ElementId` rather than a helper, nearly everywhere

`ElementId.GetHashCode()` returns the id value, so it works directly as a `Dictionary` key, a `HashSet`
member, a `GroupBy` key and an `OrderBy` key. That is version-proof **with no reflection at all**, which
matters because several of these sit inside loops over every element in the model. The reflection helper
is reserved for the 8 places that need an actual number, and it caches the property lookup once.

### The two deliberate exceptions

1. **1 `IntegerValue` in `action-set-workset.cs`** is on a `WorksetId`, not an `ElementId`. Different
   class, untouched by the 2024 change. It is annotated in place so nobody "fixes" it.
2. **`prelude.cs` `ResolveView` now takes an `ElementId`, not an `int?`.** Any future fragment calling it
   must pass `someView.Id` or `null`.

> **There used to be a third, and it was solved the same day.** The 2 electrical conversions in
> `create-equipment-family-from-datasheet.cs` were listed here as blocked, because Revit does not store
> voltage as volts and the conversion factor could not be confirmed without the Autodesk docs — so
> converting by hand risked silently corrupting a voltage. The fix sidestepped the factor entirely:
> **ask the API which unit type its own method takes**, and hand it that. `UnitUtils.ConvertToInternalUnits`
> is located by reflection, preferring the `ForgeTypeId` overload and falling back to the
> `DisplayUnitType` one, so the same source is correct on every version and nothing had to be guessed.
> That is the general move whenever a factor is unknown: don't estimate it, ask the API. Corrected here
> 2026-08-22 — the list had gone on saying "verify the factor, then convert" after it was already done.

### Keeping it fixed — the migration does not defend itself

A one-pass sweep fixes the library as it stood; it does nothing about the next fragment somebody writes.
**That has already happened once.** `filter-by-wrong-category.cs` was added on **2026-08-21, the day
after the migration**, and compared categories with `f.Category.Id.IntegerValue != (int)expectedCategory`
— the exact pattern the whole library had just been moved off. It was found by a fresh scan on
2026-08-22 and rewritten to compare `ElementId` to `ElementId` (`new ElementId(expectedCategory)` and the
`==` / `!=` operators), which is correct on 2020 through 2027 and needs no reflection — worth having
here, because that comparison runs once per `FamilyInstance` in the model.

Two things follow, and they are cheap:

1. **Writing new C#? Never take a number out of an `ElementId`.** Compare, key, group and sort with the
   `ElementId` itself — `GetHashCode()` returns the id value, so it works directly as a `Dictionary` key,
   a `HashSet` member and a `GroupBy` / `OrderBy` key. Reach for the reflection helper only when an
   actual integer has to be printed or stored, which is rare.
2. **Re-run the scan after adding fragments, not only after a Revit upgrade.** It takes seconds:

   ```
   grep -rn "\.IntegerValue" scripts --include=*.cs | grep -v GetProperty
   grep -rn "DisplayUnitType\|UnitUtils.Convert" scripts --include=*.cs
   ```

   Anything the two exceptions above do not explain is a regression. `tools\check-scripts.cmd` catches
   the same thing properly by compiling, but only on a PC with Revit installed — these two lines work
   anywhere, including from a container.

### Checked before and after, since no C# compiler was available

- No `something / UnitUtils.Convert…` anywhere, which would have inverted the arithmetic when the call
  was replaced in place. Zero found.
- No method chained onto a conversion call, which would have bound to the numeric literal. Zero found.
- No numeric format specifier on an `IntegerValue`, which would throw once the value became an
  `ElementId`. Zero found.
- A scan for `new ElementId(x)` where `x` had itself become an `ElementId` found **2 real bugs**
  (`tag-elements-in-active-view.cs`, `fill-mm-document-register.cs`), both fixed. This scan is worth
  repeating after any similar sweep.
- `git diff --stat` stayed proportional to the edit, so none of the UTF-8 double-encoding this repo has
  suffered before.

### What is proven, and what still is not

**Compiled: yes, on three versions.** The claim that once stood here — *"none of this has been compiled
or run"* — was written mid-migration and was already out of date by the end of the same day.
`tools\check-scripts.cmd` took the migrated library to **287/287 on Revit 2020, 2024 and 2027**.
Corrected 2026-08-22.

**Run against a real model: no, not since the migration.** Compiling proves the API calls exist on that
version; it cannot prove the arithmetic that replaced `UnitUtils` gives the same answer, or that an
`ElementId`-keyed dictionary groups the same way the `int`-keyed one did. So the standing rule still
applies unchanged: **run one element, check the real result, then trust it for the batch.** Start with a
unit-heavy fragment and one of the heavier recipes — those are where a silent arithmetic slip would show.

The fragment status counts in `tools/brain-status.mjs` describe the **pre-migration** verification state:
a fragment marked verified was verified before its source was rewritten. That does not make the counts
wrong — the method was proven — but it does mean the first live run on each rewritten fragment is worth
watching rather than assuming.

## Two more breaks, from the add-in's own compat shims (2026-08-22)

Harvested from `RevitCompat`, `TagCompat`, `FilterRuleCompat` and `CeilingGridApiCompat` — four classes
that each exist because a Revit version removed something. Both of the ones below are **invisible to a
compile check when the call is made by reflection**, which is exactly how this Brain reaches tag
properties.

### `IndependentTag` lost its single-reference members in Revit 2023

**Removed:** `LeaderElbow`, `LeaderEnd`, `GetTaggedReference()`, `TaggedLocalElementId`.
**Replaced by** the per-reference API: `SetLeaderElbow(Reference, XYZ)`, `GetLeaderEnd(Reference)`,
`GetTaggedReferences()`, `GetTaggedLocalElements()`.

**Proved here, not taken on trust** — a probe fragment referencing all three was compiled against the
installed assemblies on 2026-08-22: **PASS on 2020, FAIL on 2024 and on 2027**, with
*"'IndependentTag' does not contain a definition for 'LeaderElbow'"*.

**The boundary is more likely 2022 than 2023 — corrected 2026-08-23.** The heading above said 2023 on the
strength of one shim, with the note "2023 itself is not installed on this machine; nothing contradicts
that". Something now does. Two independent implementations that have to compile against every release
split it at **2022**: one uses `LeaderEnd` under `REVIT2019 || REVIT2020 || REVIT2021` and
`GetLeaderEnd(Reference)` from 2022; and this Brain's own
[`action-report-tags-and-targets.cs`](../scripts/actions/reporting/action-report-tags-and-targets.cs)
independently records the target members as changing "from 2022".

**A third source, and it splits the members apart (2026-08-24).** The second explorer's overrides guard
`GetLeaderElbow`, `GetLeaderEnd` and `HasLeaderElbow` behind a **2022-minimum** conditional, and
`IsLeaderVisible` behind a **2023-minimum** one. So "the IndependentTag API changed at X" is too coarse a
statement to be true: **the members moved in at least two waves**, 2022 for the leader geometry and 2023
for leader visibility. Anything reaching for `IsLeaderVisible` by name needs its own null check, not the
same one used for `GetLeaderEnd`.

**It still cannot be PROVED on this PC** — 2021, 2022 and 2023 are not installed, and all of those
sources are somebody's conditional rather than a compile. What is proved here is only: present on 2020,
gone by 2024. Recorded this way on purpose: **the exact year is the part that was guessed, and a guess
that has been contradicted should say so rather than quietly change from one confident number to
another.** No code depends on it — every fragment reaches these members by name at run time.

**Why this is worse than a normal break: reflection hides it completely.** A fragment that reads
`GetType().GetProperty("LeaderElbow")` **compiles perfectly on every version** and simply finds nothing
on 2023+ — so every duct, pipe and equipment tag reports "no writable LeaderElbow" and the run does
nothing while looking like it worked. It was caught here only because the shim existed to compare
against. **`RoomTag` and `SpaceTag` were NOT changed** and still carry the plain properties, so a
general tag fragment needs BOTH routes, chosen at runtime.

Fixed in [`../scripts/actions/sheets-views/action-force-tag-leader-lshape.cs`](../scripts/actions/sheets-views/action-force-tag-leader-lshape.cs)
and [`../scripts/actions/sheets-views/action-stack-tags.cs`](../scripts/actions/sheets-views/action-stack-tags.cs):
try the property first, then fall back to `GetTaggedReferences()` + the per-reference method.

### `ParameterFilterRuleFactory` string rules lost `caseSensitive`

`CreateEqualsRule(id, text, caseSensitive)` and its seven siblings (`NotEquals`, `Contains`,
`NotContains`, `BeginsWith`, `NotBeginsWith`, `EndsWith`, `NotEndsWith`): the three-argument form was
**deprecated in Revit 2023** — string comparison became case-insensitive only — and **removed outright
in Revit 2026**.

**This Brain already handles it.** `action-create-view-filter.cs` reflects over the factory and picks
whichever overload the running Revit actually offers, with the `caseSensitive` input accepted and then
ignored on 2023+. Nothing to change; recorded so the next person meeting it does not re-derive it.

### The other two shims — nothing owed

- **`RevitCompat`** covers units and `SpecTypeId`/`ParameterType`, both already in this file, plus
  `GroupTypeId.Data` vs `BuiltInParameterGroup.PG_DATA` — the same 2022 ForgeTypeId migration.
- **`CeilingGridApiCompat`** wraps `Ceiling.GetCeilingGridLines` (2025.3+), already recorded in
  [`live-model/ceiling-grid.md`](live-model/ceiling-grid.md) as deliberately not used, because the
  type-pattern route works on every version.

### The general lesson

**A compile check cannot see a break that is reached by reflection.** Where a fragment reflects on a
member name to stay version-agnostic, it has bought compilation safety at the cost of silent failure —
so the member's own version history has to be checked by hand, or a probe compiled against each
installed Revit as was done here.

### Settling "what is this member actually called, and does this version have it" in one command

Reflection makes the compiler stop telling you when a name is wrong, so the name has to be checked some
other way — and opening Revit to find out is the slow route. **The member names are plain text inside
`RevitAPI.dll`** (they live in the metadata string heap), so a grep answers it directly, per version,
without opening Revit or loading the assembly:

```bash
grep -aoE "GetLabel[A-Za-z]*|LabelDimension" "/c/Program Files/Autodesk/Revit 2027/RevitAPI.dll" | sort -u
```

That is how `GlobalParameter.GetLabels()` — which does not exist — was corrected to
**`GetLabeledDimensions()`** on 2026-08-23, and how it was confirmed present on Revit 2020 as well by
running the same grep against that version's DLL. Two greps, no Revit.

Note `-a` (treat the binary as text) and `-o` (print only the match). It proves a name EXISTS in the
assembly; it does not prove which type owns it or what its signature is — for that, write the call and let
`tools\check-scripts.cmd` answer, which is one minute and no attention.

## Why reflection, and not a version check — the mechanism (2026-08-23)

This file already says "reach it by reflection" in several places, and a fragment written here in a hurry
will reach instead for the obvious-looking thing:

```csharp
if (int.Parse(Document.Application.VersionNumber) >= 2023)
{
    BoundaryValidation.IsValidBoundaryOnView(doc, viewId, loops);   // 2023+ only
}
```

**That does not work, and the reason is worth knowing rather than memorising.** The .NET runtime prepares
a method before it executes any of it: entering the method resolves the types and members its whole body
refers to. So on Revit 2020 the missing type is looked for the moment the enclosing method is entered —
**before the `if` is ever evaluated** — and it throws there. The guard is written inside the thing it was
meant to guard. A `try/catch` around the call has the same problem for the same reason, and if the fragment
is compiled against 2020 in the first place it never even gets that far: a missing member is a compile
error, not a runtime one.

Two ways out, and this library uses the first:

1. **Reflection.** `typeof(X).GetMethod("Name")` names nothing at compile time and resolves nothing at
   method entry — so one source compiles and runs on every version, and a `null` back from `GetMethod` is
   the version answer. This is what [`load-family.cs`](../scripts/creators/load-family.cs) does for
   `GetRevitUIFamilyLoadOptions`, and what `action-replace-material.cs` does for `ParameterType`.
2. **Put the version-gated call in its own method** that the guard calls, so preparing the outer method
   never touches the missing type. Real code does this and marks the inner method "do not inline" so the
   compiler cannot fold it back into the caller and undo the isolation. **A fragment cannot use this at
   all** — it has no methods of its own to put the call in. Recorded because it explains why library code
   looks the way it does, not as something to copy here.

### A major version number is not enough any more

`Application.VersionNumber` gives "2026". Revit's API has changed **within** a major version: parts of the
2026 API surface exist only from 2026.3, and `Application.VersionBuild` / the version's minor component is
what distinguishes them. Anything gated on a 2026-or-later member therefore has to be reached by
reflection like everything else — the major number alone will say yes on a build that does not have it.
Nothing in this library depends on a 2026.3 member today; this is here so the first fragment that wants
one does not gate on the wrong number.

### Two ways to probe for an API member, and each one lies on its own

Both are needed, because they fail in opposite directions (learned 2026-08-24 while writing
`action-report-mep-pressure-drop.cs`, which named four members that do not exist).

**The DLL string grep** proves a NAME appears somewhere in the assembly. It does not prove which type
owns it. `PressureDrop`, `HydraulicDiameter` and `Diameter` all appear in `RevitAPI.dll` — on other
types. Grepping found them and the fragment failed to compile four times.

**The compile probe** — write the candidate members into a throwaway fragment and let `csc` name the bad
ones — is authoritative about ownership. But **the harness prints a TRUNCATED error list.** A probe with
nine candidates printed four failures, and the five it did not mention were assumed to exist. Three of
them did not. The complete list is written to `fragment-compile-failures.txt`, and that is the file to
read:

```bash
grep "does not contain a definition for" fragment-compile-failures.txt
```

**The rule: grep the DLL to find candidate names, compile-probe to find which type owns them, and read
the FAILURES FILE rather than the console summary.** Either half alone produces a confident wrong
answer, which is the expensive kind.

## An API name that does not exist is a COMPILE error, and no try/catch reaches it (2026-08-24)

Three fragments from the 28-fragment MEP coordination batch failed to compile. A parallel session found
them while compiling all 394 against the real shipped `RevitAPI.dll` for 2020, 2024 and 2027, and
reported them rather than repairing another session's files — which is the rule in `CLAUDE.md` working
exactly as intended.

| Fragment | 2020 | 2024 | 2027 | The name |
|---|---|---|---|---|
| `action-check-flow-direction.cs` | ✗ | ✗ | ✗ | `BuiltInParameter.RBS_SYSTEM_TYPE_PARAM` |
| `action-connect-open-connectors.cs` | ✗ | ✗ | ✗ | same |
| `action-check-plumbing-fixture-connectivity.cs` | ✗ | ✓ | ✓ | `BuiltInCategory.OST_PlumbingEquipment` |

**`RBS_SYSTEM_TYPE_PARAM` does not exist on any Revit version.** It was written from memory. The system
type parameter is DOMAIN-SPECIFIC — `RBS_DUCT_SYSTEM_TYPE_PARAM` and `RBS_PIPING_SYSTEM_TYPE_PARAM`,
both present 2020–2027 — and there is no generic one. Two of these fragments could therefore not run on
**any** Revit at all. Fixed by trying duct, then pipe, then falling back to the system NAME.

**`OST_PlumbingEquipment` arrived at Revit 2024.** Fixed by resolving it with `Enum.TryParse` at run
time, the same version-proof route this library already uses for `ElementId.Value`/`IntegerValue` and
the `IndependentTag` rename — on 2020 the parse fails, that one category is skipped, the rest works.

**The distinction that matters, and it is the whole point of this note.** A wrong VALUE gives a wrong
answer you can argue with. A wrong NAME is not a runtime failure at all — the file never becomes code,
so there is nothing to catch, nothing to log, and no defensive coding that helps. It is invisible to
review, invisible to a careful read of the logic, and invisible to every consistency check in this repo.
**Only a compiler finds it.**

Three things would each have caught it before it shipped:

1. **Read [`revit-api-surface.md`](revit-api-surface.md) first.** It is generated from the fragments and
   lists the 245 types this library really uses. `RBS_DUCT_SYSTEM_TYPE_PARAM` and
   `RBS_PIPING_SYSTEM_TYPE_PARAM` are both in it; `RBS_SYSTEM_TYPE_PARAM` is not, and never was.
2. **Copy the call from a proven fragment.** Every existing fragment that reads a system type
   (`filter-by-system-type.cs`, `trace-mep-circuits.cs`, `connect-terminal-branch.cs`,
   `action-color-by-group.cs`) uses the domain-specific names. The library already had the answer in
   four places.
3. **`tools\check-scripts.cmd`** — a minute on the PC, and the only one of the three that is proof
   rather than diligence.

A fragment written where no compiler exists is not "probably fine" — it is unproven in a way that
reading it cannot fix, and this is the second time that has been recorded here (see
`tools/verify-fragments-compile.ps1`, which had never run once when it was written).
