# Harvesting a pyRevit extension into the Brain — the ledger

Started and completed **2026-08-23**. Ajmal supplied a pyRevit extension folder and asked for all of it
to be gone through, with anything worth having converted from Python to C#.

Same method as the add-in harvest in [`addin-harvest.md`](addin-harvest.md), and the same four-way
verdict: **BUILD** / **UPGRADE** / **KEEP OURS** / **SKIP**. This is a working ledger, not knowledge —
it lives in `docs/`, outside the search index. Its job is so a fresh session never re-reads a tool that
was already judged.

**Per the standing rule, nothing here names the source** — not the extension, not its author, not where
it came from. Everything kept was rewritten as this Brain's own C# and documented in its own words.

## Why Python was not kept

Settled before reading a line of it, and worth recording so it is not re-argued:

- **The bridge runs C# only.** `run_csharp` takes a C# string and nothing else
  ([`run-csharp.js:16`](../mcp-server/tools/run-csharp.js)); `run_fragment` compiles `.cs` against
  `lib/prelude.cs`. There is no Python path in `mcp-server/` at all. A harvested `.py` would sit in
  `scripts/` looking useful and be unrunnable.
- **The version check cannot see Python.** `tools/check-scripts.cmd` compile-checks the library against
  every Revit on the PC without opening Revit. Python has no compile step, so any Python fragment would
  be the one part of the library with no safety net — failing the way Ajmal specifically named as the
  problem: mid-job, on a newer Revit.
- **The search index only collects `.cs`** ([`fragment-lib.mjs:25`](../tools/fragment-lib.mjs)), so a
  `.py` file is invisible to `fragment-index.mjs`, to the counts, and to the most-used-fragments hook.
- **Converting costs almost nothing.** pyRevit calls the same Revit API — same classes, same methods.
  The algorithm carries over one-for-one; only the syntax around it changes.

## What was in the folder

67 ribbon buttons across 7 panels, **69 `script.py` files**, plus a shared `lib/` of snippets. The other
**1580 `.py` files are vendored geometry stubs** bundled inside one button's folder — a third-party
dependency, nothing authored, nothing to harvest. Do not re-scan them.

## BUILD — 8 new fragments, 1 new knowledge note

| Built | From the job | Why the Brain had nothing |
|---|---|---|
| [`action-create-from-room-boundaries.cs`](../scripts/actions/structural-changes/action-create-from-room-boundaries.cs) | rooms → floors / ceilings / filled regions / detail lines | `create-floor.cs` takes a **typed** boundary and its own header says "no openings". Room boundaries come as a LIST OF LOOPS where only the first is the outside edge and the rest are holes — shafts, cores, columns. Building from loop [0] alone paves over the shaft |
| [`create-section-at-element.cs`](../scripts/creators/create-section-at-element.cs) | a section view per element, aimed at it | `create-view.cs`'s section mode takes a typed mm box and is **axis-aligned only** — it cannot look at anything running at an angle, because a section is defined by a Transform, not a box |
| [`action-align-viewports-across-sheets.cs`](../scripts/actions/sheets-views/action-align-viewports-across-sheets.cs) | put the plan in the same place on every sheet | Nothing in the Brain aligned viewports. The two preconditions (shared title-block origin, matching scale) are what make it correct rather than plausible |
| [`action-place-views-on-new-sheets.cs`](../scripts/actions/sheets-views/action-place-views-on-new-sheets.cs) | one new sheet per view | `action-place-viewport-on-sheet.cs` puts many views on ONE sheet — the opposite job |
| [`action-change-wall-constraints.cs`](../scripts/actions/structural-changes/action-change-wall-constraints.cs) | re-host walls to another level without them moving | Only `create-wall.cs` touched walls. The real content is that setting the level alone leaves the offset, so every wall silently jumps |
| [`action-disallow-join.cs`](../scripts/actions/structural-changes/action-disallow-join.cs) | turn off end-joining in bulk | Nothing covered it. A pinned element accepts the call and changes nothing — the silent-no-op class this Brain keeps finding |
| [`action-copy-view-filters.cs`](../scripts/actions/color-graphics/action-copy-view-filters.cs) | copy filters between views/templates | `action-create-view-filter.cs` makes one; nothing copied one. `AddFilter` alone applies EMPTY overrides, so a naive copy produces views that carry the filters and look unchanged |
| [`action-replace-material.cs`](../scripts/actions/structural-changes/action-replace-material.cs) | swap a material everywhere | `action-report-compound-structure.cs` could read layers, nothing could change them. Three hiding places, and `<By Category>` is `InvalidElementId`, not null |
| [`knowledge/live-model/failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md) | swallowing Revit's warnings during bulk work | **The documented answer cannot be used here** — see below |

## UPGRADE — 1

**[`create-ceiling.cs`](../scripts/creators/create-ceiling.cs)** was a stub reading *"CONFIRMED
IMPOSSIBLE on Revit 2020 — `Ceiling.Create` only exists from Revit 2022"*. The API fact is right. The
verdict was wrong: it was written against 2020 and then stated without a version qualifier, so it read
as "ceilings cannot be made, full stop". **This PC has Revit 2020, 2024 and 2027.** On 2024 and 2027
ceiling creation works normally and the Brain was refusing a job it could do. Now creates the ceiling by
reflection where the method exists and reports the limitation only where it genuinely applies.

**The lesson is bigger than the fragment: an "impossible" recorded against one version must say which
version, or it becomes a lie the day another Revit is installed.**

## What the harvest found in OUR code — the more valuable half, again

- **`action-maximize-datum-extents.cs` had never compiled on any Revit.** It reads `d.Curve` off a
  `DatumPlane`, which has no such property on any version — only `Grid` does. It carries a detailed
  `✓ LIVE-VERIFIED 2026-08-22` note with real measured before/after numbers, and that note is almost
  certainly honest: it was proved while the list was typed as `Grid`, then widened to `DatumPlane` to
  accept Levels, and the reporting helpers were never re-checked. **A verified fragment can be broken by
  a later edit and keep its verified badge.** Fixed by casting for the reporting only; `Maximize3DExtents`
  — the actual work — was always fine.
- **`action-dimension-wall-openings.cs` could not compile on Revit 2027** — `ElementId.IntegerValue`,
  removed in 2027.
- **`ElementId.IntegerValue` is removed in Revit 2027**, and it is an easy reflex to type. Six of the
  eight new fragments used it and failed their first compile check. `ElementId.ToString()` prints the
  same number on every version and needs no reflection; the `_idValueProp` helper in
  [`prelude.cs`](../scripts/lib/prelude.cs) stays the answer where an actual number is needed.
- **`Definition.ParameterType` is removed after Revit 2023** (`GetDataType()` replaced it), and a
  `try/catch` around it does not help — it is a COMPILE error, not a runtime one. Both names now reached
  by reflection in `action-replace-material.cs`.
- **A fragment cannot implement `IFailuresPreprocessor`** — the standard answer to "swallow the warning
  dialog during bulk creation". The bridge wraps every fragment in one method, so no callback interface
  can be declared. This is the SECOND instance of that structural limit (the first was
  `IDuplicateTypeNamesHandler`, 2026-08-22), which is what makes it a general rule worth its own note
  rather than a footnote: **any technique whose answer is "implement this interface" is out of a
  fragment's reach.** `FailureHandlingOptions.SetForcedModalHandling(false)` is settings rather than an
  interface and covers most of the need.

## KEEP OURS — ours is as good or better

| Their tool | Ours | Why ours stays |
|---|---|---|
| Display / Isolate Warnings | `context-all-warnings.cs`, `filter-by-warnings.cs`, the grayout skill | Already grouped, filterable by description, and `action-report-element-ownership.cs` covers who last touched it |
| Purge View Filters, Purge View Templates (2 tools) | `action-purge-unused.cs` | One fragment, three modes, and it correctly counts **view templates** as filter users — a purge that walks only non-template views would delete filters still in use |
| Rotate multiple elements | `action-rotate-elements.cs` | Already rotates each element about its own point when no pivot is given, **and** detects the silent no-op on pinned/grouped elements, which theirs does not |
| Match Graphic Overrides | `action-match-graphics.cs` | Same job |
| Find/Replace ×6 (views, sheets, types, rooms, materials, filters) | `action-find-replace-element-name.cs`, `action-rename-element.cs` | One general fragment instead of six near-identical ones |
| Duplicate Sheets | `action-duplicate-sheet.cs` | Ours was hardened live — it deletes the part-built sheet on a number clash, a failure theirs does not handle |
| Revisions ×3 | the whole `actions/sheet-dates-revisions/` folder | Broader |
| Unhide All Elements | `commands/unhide-all-active-view.cs` | Same job |
| Renumber by category | `action-renumber-sequential.cs` | Same job |
| Create Workset Views | `create-workset-3d-views.cs` | Same job |
| Transfer View Templates | `action-transfer-views-between-documents.cs` | Already does cross-document template copying. **Noted, not built:** theirs additionally re-assigns views after overwriting a same-named template. Ours is not yet live-run, so adding to it would be building on unproven ground — recorded here as the next improvement when it is proved |
| Select By Category / Select Similar ×8 / Select on Sheets ×2 | `filter-by-category.cs` + the native `select_elements` tool | Eleven buttons for what is one filter plus one tool call here |
| Duplicate Views, List All Levels | existing view/level fragments | Same job |

## SKIP — nothing transferable

- All WPF/XAML windows, pyRevit forms, ribbon wiring, icons — the bridge has no UI.
- `UI_BG_BWG`, `UI_ShortenRibbonNames` — pyRevit's own interface settings; no bridge equivalent exists.
- 2 tools the author had already marked OBSOLETE.
- Filled-region split/merge (7 tools) — annotation drafting, not modelling. **One idea was worth
  noting** and is recorded here rather than built: their merge extrudes each 2D outline into a solid,
  boolean-unions the solids, then takes the top faces' edge loops back as one outline. That is a sound
  general trick for unioning arbitrary 2D shapes if this Brain ever needs it. Nothing currently asks for
  it, so per the standing rule it waits for evidence rather than becoming an unproven fragment.
- DWG open/relink (2 tools) — external file management, not model work.
- Attached detail groups (3 tools) — `GetAvailableAttachedDetailGroupTypeIds()` noted; no job has asked.
- Sheet generator, graphics overviews, view filter legend — large WPF tools whose logic is project
  bookkeeping, already covered by the sheet and filter fragments above.

## State at the end

All 13 consistency checks pass. Compile-checked against **Revit 2020, 2024 and 2027**.
**None of the 8 new fragments has been run against a real model** — every one is dry-run by default and
says so in its own header. Run each on ONE element first and check the real result before a batch.

## Round 2 — the full read, 2026-08-23 (same day)

The first pass surveyed all 69 tools and read about 21 in full. Ajmal asked whether that was everything:
*"is there anyting balance ??"* It was not. All 5,702 lines of real code have now been read.

**It found three defects in fragments this Brain had written the same day** — all three of the class that
an API survey cannot see, because each is about a call that was *not* made.

| Fixed | What was wrong |
|---|---|
| [`action-remap-line-styles.cs`](../scripts/actions/color-graphics/action-remap-line-styles.cs) | **It missed filled-region borders completely** — they are not `CurveElement`s and are not reached through `LineStyle`. Walking CurveElements alone, it would have reported "nothing left to move" while every region border still held the old style and the style still refused to purge: **the exact false-clean the fragment exists to prevent, reproduced by the fragment itself.** Fixing it then exposed a second fact worth more than the fix — **filled regions are WRITE-ONLY.** Revit has `SetLineStyleId()` and **no getter** (proved on the compile check: `GetLineStyleId` exists on no version installed here), so there is no way to ask which regions use the old style, and therefore no way to change only those. The fragment now says so plainly and offers the three choices the API actually allows — report / set them all / skip — instead of claiming a precision that does not exist |
| [`action-replace-material.cs`](../scripts/actions/structural-changes/action-replace-material.cs) | **No guard for elements inside a GROUP.** An instance parameter on a grouped element can only vary per instance if it is flagged "Varies across groups" (`Definition.VariesAcrossGroups`). If it is not, the write either refuses or **changes every instance of that group across the model** — silently. Also added: Curtain Wall, Sloped Glazing and Basic Ceiling report a CompoundStructure that is not a real layer stack and must be skipped by name |
| [`action-change-element-type.cs`](../scripts/actions/structural-changes/action-change-element-type.cs) | Same group trap, on the parameter *restore* pass added earlier that day. Worse here: the restore was written to prevent data loss, and without the guard it could have propagated one element's values to every instance of its group |

**The pattern worth keeping:** all three were written confidently, all three compiled on 2020/2024/2027,
and all three were wrong in a way no compile check can see. **Compiling is a floor, not a ceiling** — the
library already says so, and this is what it looks like in practice.

### Also found, recorded not built

- **Renaming a sheet number does not refresh the Project Browser.** The tool forces it by hiding and
  re-showing the dockable pane (`DockablePanes.BuiltInDockablePanes.ProjectBrowser`). Worth knowing before
  concluding a rename failed.
- **A filled region can be built straight from another region's boundary** — `GetBoundaries()` returns
  `IList<CurveLoop>` usable directly in `Ceiling.Create`, with none of the loop-assembly a Room needs.
- **Matching wall constraints FROM a picked reference wall** (rather than to a named level) is a genuine
  variant of `action-change-wall-constraints.cs` — and for an unconnected-height wall, matching the top
  means solving the unconnected height so the two tops land at the same world Z.
- **Attached detail groups are hidden per view** and need `ShowAttachedDetailGroups(view, id)`.
- **Duplicating a sheet properly means copying eleven separate things** — views, legends, schedules,
  images, lines, text, clouds, DWGs, symbols, dimensions and additional revisions — because
  `ViewSheet.Create` copies none of them. Ours (`action-duplicate-sheet.cs`) covers the main ones and
  says in its header which it leaves; theirs is more complete, and that is the honest comparison.

### Closing state of this extension

**All 69 tools read in full. Nothing outstanding here.**
