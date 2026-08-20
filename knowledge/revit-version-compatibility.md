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
