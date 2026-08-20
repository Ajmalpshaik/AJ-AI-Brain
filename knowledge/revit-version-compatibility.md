# Will these fragments run on Revit 2024 and above?

Every fragment in `scripts/` was proven against **Revit 2020**. This note records what actually breaks
on a newer Revit, measured by scanning all 282 fragments on 2026-08-20 — not estimated.

**Short answer: 200 of the 282 fragments (71%) touch at least one API that changed after 2020.**
Nothing here is unfixable, and the fixes are mechanical. What matters is knowing *which* failures shout
and which stay quiet.

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

## What else lands later

| Revit | Change | Does this Brain use it? |
|---|---|---|
| 2022 | `IndependentTag` tag members renamed for multi-reference | **No** — scanned, 0 fragments |
| 2025 | `Dimension` split into `LinearDimension` / `RadialDimension` / `ArcLengthDimension`; exact-type checks fail | **No** — scanned, 0 fragments |
| 2026 | Add-in isolation via `<ManifestSettings>` | Bridge-side only |
| 2027 | .NET 10; all-user add-ins move to `Program Files` (needs admin) | Bridge-side only |

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
4. **A fragment fixed for 2024+ stops working on 2020.** Because these are source, not a DLL, there is no
   `#if` to lean on — the bridge compiles one text against one Revit. Decide whether this Brain still has
   to support 2020 at all, or move the whole library forward to 2024+ and say so in `scripts/README.md`.
   Do not leave the library half-migrated; that is the worst of both.
5. **Re-scan rather than trust this note.** The counts here are from 2026-08-20 against 282 fragments:

   ```
   grep -rlE "IntegerValue|new ElementId\(" scripts --include=*.cs | wc -l
   grep -rl  "DisplayUnitType"              scripts --include=*.cs | wc -l
   ```

Version rules themselves (what changed when, .NET per release, helper patterns) belong to the
`revit-version-matrix` skill, not here. This note only records **what this Brain's own library uses**.
