# Harvesting the pyRevit platform's own tool library — the ledger

**2026-08-23.** Ajmal supplied the pyRevit platform repository and asked for all of it to be gone
through. This is the second pyRevit harvest of the day; the first covered a single third-party extension
and is recorded in [`pyrevit-harvest.md`](pyrevit-harvest.md).

Working ledger, not knowledge — it lives in `docs/`, outside the search index. Per the standing rule
nothing here names a source beyond the repository Ajmal himself pointed at; everything kept was
rewritten as this Brain's own C#.

## What is actually in the repository

3806 files, 191 MB. Only a fraction is harvestable, and the split matters:

| Part | Size | Verdict |
|---|---|---|
| `extensions/pyRevitTools.extension` | **203 tools** | **The harvest target.** Analysis 19, Drawing Set 65, Modify 37, Project 34, Selection 40, Toggles 8 |
| `pyrevitlib/pyrevit/revit/` | ~25 modules | Read for technique — `db/failure.py` earned its own finding, below |
| `extensions/pyRevitDevTools.extension` | 87 tools | **SKIP** — tests for the platform itself, not Revit work |
| `extensions/pyRevitCore.extension` | 15 tools | **SKIP** — pyRevit's own settings/about/reload UI |
| `dev/` (the C# engine and loader) | large | **SKIP — explicitly out of scope.** [`START-HERE.md`](../START-HERE.md) rules out working on the add-in that provides the bridge; this is that, for a different bridge |
| Other 4 extensions | 16 tools | **SKIP** — bundle authoring, tags demo, templates, tutorials |

## Method

Two passes, and the second one is the reason this ledger grew.

**Pass 1 — survey.** All 203 tools measured mechanically: name, size, every Revit API call actually made,
every BuiltInCategory touched. Nothing judged by its button label. That produced the first three builds.

**Pass 2 — full read, after Ajmal pushed back** (*"each and everyting hard chek"*). He was right to. The
survey had produced good verdicts but it could only see what a tool *calls*, never what it *knows*, and
the gap between those two is exactly where the value sits. The whole 22,149 lines of real code were then
read in size bands: **140 tools under 60 code lines, 40 between 60 and 200, 23 over 200** (the largest
being 1972, 1149 and 330 lines of genuine logic once XAML and window state are stripped).

**What the second pass found that the first could not** — and this is the case for doing it:

- A **defect in this Brain's own fragment written four hours earlier** (below).
- The **`ChangeTypeId` parameter-loss trap**, which is invisible in an API list because the whole point
  is what *doesn't* get called.
- **Model geometry weight** as a measurable thing.

Three real findings that a survey structurally could not surface. The lesson is not "always read
everything"; it is that **an API surface tells you what a tool does and never what it had to learn.**

## BUILD — 5 new fragments, 1 upgrade, 1 self-correction

| Built | Why the Brain had nothing |
|---|---|
| [`filter-by-openings.cs`](../scripts/filters/by-relationship/filter-by-openings.cs) | **Openings are not one category and not one class.** They are spread across `OST_ShaftOpening`, `OST_FloorOpening`, `OST_SWallRectOpening`, `OST_RoofOpening` **and** a separate `Opening` class — so any single query reports clean while the shafts are still there. Plus holes cut by a family (a cast-in sleeve, a void component), which are in none of those categories. `create-mep-openings.cs` could make openings; nothing could find the ones already there, which is the actual coordination question |
| [`action-report-constraints.cs`](../scripts/actions/qa-checks/action-report-constraints.cs) | **"Why won't this move?"** had two answers here (`filter-by-pin-status.cs`, `filter-by-group.cs`) and was missing the third. A constraint is not a property of the element — it is a separate element in `OST_Constraints` holding References back to it, so there is no `wall.Constraints` and the only way to find them is to walk every constraint in the model. `OST_Constraints` appeared nowhere in the library |
| [`action-find-overlapping-lines.cs`](../scripts/actions/qa-checks/action-find-overlapping-lines.cs) | Lines drawn on top of each other — the "why does this view print heavy" and exploded-CAD cleanup. `action-find-duplicates.cs` finds coincident elements; it cannot do this, because **two lines can overlap without being duplicates**. The technique is worth the fragment on its own: key each line by the *infinite line it sits on* (normalised direction + perpendicular offset), then overlap is 1-D interval arithmetic |
| [`action-report-geometry-complexity.cs`](../scripts/actions/reporting/action-report-geometry-complexity.cs) | **Which families are making the model slow**, measured as triangle count per family type at each detail level. Nothing here measured geometry weight at all — `model-health-audit.cs` covers warnings and purge candidates, not what Revit has to draw. **The comparison across detail levels is the actionable part**: 40 triangles Coarse against 40,000 Fine is a family behaving; the same big number at all three means no detail-level control and full price in every working view. Multiplied by instance count, because 400 instances of a 12,000-triangle family is 4.8 million triangles from one library choice |

**UPGRADE — [`action-change-element-type.cs`](../scripts/actions/structural-changes/action-change-element-type.cs)
was quietly losing data.** It called `ChangeTypeId` and nothing else. **A type change drops every instance
parameter value the new type does not also carry — Mark, Comments, a shared parameter only the old family
had — silently, with the element still looking fine.** On a batch of 200 nobody notices until much later.
It now captures every writable instance parameter before the swap and writes it back after, matching by
NAME because the parameter Id changes with the type, and deliberately never restoring the six identity
parameters (Family, Type, Family and Type, Type Name, Family Name, Image) since those *are* the change.
It reports how many values went back and how many the new type refused. **This is invisible to an API
survey**, because the whole finding is about a call that was never made.

**SELF-CORRECTION — [`action-place-views-on-new-sheets.cs`](../scripts/actions/sheets-views/action-place-views-on-new-sheets.cs),
written four hours earlier in the first harvest, had the wrong rule.** It treated LEGENDS as the only view
type that may appear on more than one sheet, and would therefore have wrongly skipped views that are
perfectly placeable. The full list is **Legend, Schedule, DraftingView, ColumnSchedule, PanelSchedule,
CostReport, LoadsReport**; everything else — plans, sections, elevations, 3D, callouts — is one sheet only.
Fixed. Worth recording as a pattern rather than a typo: **the first harvest produced a fragment whose rule
was a guess dressed as a fact, and only reading a second implementation caught it.**

## The finding that mattered most — and it was not a tool

`pyrevitlib/pyrevit/revit/db/failure.py` is a mature, general-purpose failure swallower. Reading it
**corrected an assumption in the note written earlier the same day**
([`failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md)).

That note recorded that a fragment cannot implement `IFailuresPreprocessor`, and framed it as a harness
limitation. Half right. What the platform's own handler shows is that a **blanket swallower is dangerous
in any harness**:

- With no resolution available it dismisses the warning — harmless.
- Otherwise it asks Revit for `GetDefaultResolutionType()` and applies it. The available resolutions
  include `DeleteElements`, `DetachElements` and `UnlockConstraints`.
- Handlers keep an ordered list "least destructive to most" — but **the default is checked first and used
  if it matches, wherever it sits in that list.** So for any failure whose Revit default is
  `DeleteElements`, "just swallow the warnings" deletes model elements, silently, in a transaction that
  then commits.

So the honest framing is not "the harness stops us doing the right thing". What is nearly always wanted
is narrower — ignore *this* known warning during *this* bulk operation — and
`SetForcedModalHandling(false)` plus a per-item try/catch gives exactly that without handing Revit
permission to resolve by deleting. The note now says so.

## KEEP OURS — already covered, and in several cases better

| Their tools | Ours |
|---|---|
| Wipe Unused Filters, Wipe Unused View Templates, Wipe SubCategories, Wipe Arrowheads, Wipe Unpurgable Viewport Types, Wipe Family Parameters, + 8 more Wipe* | `action-purge-unused.cs` (three modes, and correctly counts view templates as filter users), `action-purge-unused-families.cs`, `action-purge-unplaced-views.cs` |
| Who Did That, Select Owned By Me, Select Last Edited By Me, Find Sheets With Elements Owned By Me, Keep Editable | `action-report-element-ownership.cs` — same `WorksharingUtils` data, one fragment |
| Compare Properties | `action-compare-elements.cs` |
| Match, Match Properties, Match Paint, Override VG, Override 2D | `action-match-graphics.cs`, `action-set-line-style.cs`, the whole `color-graphics/` folder |
| ColorSplasher (2246 lines) | `action-color-by-group.cs`, `action-set-category-color.cs`. Theirs colours by parameter value with a generated legend — a genuine extra, recorded below, not built |
| ReNumber, Increment/Decrement Sheet Numbers | `action-renumber-sequential.cs` |
| Rename Selected Views/Sheets, Replace_Fonts (partly) | `action-find-replace-element-name.cs`, `action-rename-element.cs` |
| XLS Import, Create Schedule from CSV | `action-import-parameters-from-csv.cs`, `create-schedule.cs`, `create-key-schedule.cs` |
| Preflight Checks | `model-health-audit.cs` |
| Select All Objects Of Selected Type / Passing Filter / Same Family, Select Element Types, Invert Selection, Discard Grouped/Pinned, + ~12 more Selection tools | `filter-by-category.cs`, `filter-by-family.cs`, `filter-by-types.cs`, `filter-by-selection-filter.cs`, `filter-by-pin-status.cs`, `filter-by-group.cs` plus the native `select_elements` tool — roughly 20 buttons for what is one filter plus one call here |
| ViewRange | `action-set-view-range.cs` |
| Set View Template Controlled Parameters | `action-set-view-template-controlled-params.cs` |
| Copy Views, Move Views, Add Views to Sheets, Batch Sheet Maker, Reorder Selected Viewport | `action-place-viewport-on-sheet.cs`, `action-place-views-on-new-sheets.cs`, `action-duplicate-sheet.cs`, `create-sheet.cs` |
| Copy Sheets / Legends / View Templates to Open Documents | `action-transfer-views-between-documents.cs` |
| Find All Revised Sheets, Find Sheets With Selected Revision, Set/Remove Revision On Sheets, Turn Off All Revisions, Generate Revision Report | the `actions/sheet-dates-revisions/` folder |
| Find And Select Entities Without Tags, Tag All in All Views | `filter-by-tag-status.cs`, `tag-elements-in-active-view.cs` (ours does scored placement — better per view, narrower in scope) |
| Get Centroid | `action-report-bounding-box.cs`, `action-report-location.cs` |
| Mass Pin References | `filter-by-pin-status.cs` + `action-set-parameter-value.cs` |

## SKIP — nothing transferable to a bridge with no UI

- **The big WPF applications** — Keynotes (3232 lines), ColorSplasher (2246), Section Box Navigator
  (2041), Print Sheets (1700), ViewRange (1149), Toggle Grid Bubbles (1026). Their bulk is window state.
- **View navigation and toggles** — Top/Bottom/Left/Right/Front/Back/Section/3D, Next/Prev, MinifyUI,
  Tab Coloring, Sync Views, Close Views. These move the user's screen; the bridge has no screen.
- **Memory/clipboard tools** — MRead, MWrite, MAppend, MClear, MDeduct, Copy State, Paste State, Match
  History Clipboard. Session scratch state, meaningless across a bridge call.
- **File and environment management** — Get Central Path, Get RVT Info, Reload Links, Relink Textures,
  Wipe Collab Cache, Purge Memory Files, Open Keynotes File, Wipe External Services.
- **Drafting/annotation niche** — Make Pattern, Batch Import PAT, Shake Filled Regions, Place Origin
  Marker, Wipe Empty Elevation Tags, Move Viewport Label, Rename PDF Sheets.
- **pyRevit's own plumbing** — Custom Properties, Preflight Checks framework, Wipe Data Schema.

## Round 3 — the "not built" list, built

Ajmal, on being shown the list above: *"is that ll you take??"* Fair. **The reasoning behind that list was
a misapplied rule.** "Wait for evidence before adding" is written in [`START-HERE.md`](../START-HERE.md)
about **indexing external documents**, where 600 unchecked chunks would measurably wreck retrieval. A
fragment is nothing like that: it is additive, compile-checked, invisible until searched for, and costs
nothing to carry. Applying the document rule to code was wrong, and five real capabilities sat unbuilt
because of it.

| Built in round 3 | The thing it knows |
|---|---|
| [`action-create-view-filters-by-value.cs`](../scripts/actions/color-graphics/action-create-view-filters-by-value.cs) | One PERSISTENT view filter per distinct parameter value, each in its own colour. **Not what `action-color-by-group.cs` does** — that writes per-element overrides, which live in one view, do not follow the parameter, and give an element drawn tomorrow nothing. Colour-by-group to investigate; this to establish a standard. The filter's category set is taken from the elements, because a `ParameterFilterElement` handed a category that lacks the parameter is rejected whole |
| [`action-remap-line-styles.cs`](../scripts/actions/color-graphics/action-remap-line-styles.cs) | Move every line off one style onto another, model-wide. **The reason the old style still would not purge**: lines inside SKETCHES — floor boundaries, ceiling edges, filled-region outlines — are `CurveElement`s owned by a Sketch, never selectable in a view, and always the culprit |
| [`action-report-views-showing-element.cs`](../scripts/actions/reporting/action-report-views-showing-element.cs) | Which views and sheets show this element — the reissue list. **There is no such API**: visibility is the result of crop, view range, filters, categories, phase and discipline together, so the only truthful answer is a collector scoped to each view |
| [`action-set-view-crop-to-shape.cs`](../scripts/actions/visibility/action-set-view-crop-to-shape.cs) | A non-rectangular crop. `action-set-view-crop.cs` sets `CropBox`, which is a box and can only be a rectangle however well rotated. Revit demands the loop be closed, flat and non-self-intersecting, and returns the same unhelpful error for all three — so this chains and snaps the joints itself and reports the worst gap it bridged |
| [`action-convert-cad-to-directshape.cs`](../scripts/actions/structural-changes/action-convert-cad-to-directshape.cs) | Imported CAD into real elements. **An `ImportInstance` is ONE element holding everything** — no category per piece, nothing to schedule, and a clash check can only say "something in the DWG". The geometry is one level down (`GetInstanceGeometry()`), and `IncludeNonVisibleObjects` must be true or you convert a fraction and never know |

**Still not built, and these ones have a reason that holds:**

- **Batch PAT import** — a full AutoCAD pattern-file parser into `FillPattern`/`FillGrid`. Substantial,
  and unlike the five above it is a file-format problem rather than a Revit one.
- **Family parameter export/import** — needs a decided file format first, which is a conversation, not a
  fragment.
- **Transfer view templates with re-assignment after overwrite** — the addition belongs on
  `action-transfer-views-between-documents.cs`, which has **never been run against a real model**.
  Building on unproven ground is the one case where waiting is right.

## Small facts worth keeping, found only in the full read

Each of these is a one-line trap that costs a wasted round trip if you meet it cold. None justifies its
own fragment; all are cheap to record.

- **Flipping a wall about its centreline moves it** unless you first set the location line
  (`WALL_KEY_REF_PARAM`) to Core Centerline, flip, then restore the original value. Also: flipping the
  location line itself means swapping Exterior↔Interior, which is a *paired* mapping, not an increment.
- **A revision reaches a sheet through two separate lists** — `GetAllRevisionIds()` (picked up from
  clouds) and `GetAdditionalRevisionIds()` (added by hand). Reading one misses revisions. *This Brain
  already does the union correctly* in `action-assign-revisions-by-sheet-date.cs` — checked, not assumed.
- **There is no "move a viewport to another sheet" API.** You `DeleteViewport` and `Viewport.Create` on
  the target, carrying `GetBoxCenter()` and `GetTypeId()` across yourself. Schedules are worse — a
  separate `ScheduleSheetInstance.Create` plus a delete.
- **Picking a face inside a LINKED model needs the link's transform** — `RevitLinkInstance.GetTransform()`
  then `transform.OfVector(normal)`. Skipping it puts the answer in the wrong place while looking right.
  Same family as the linked-model ray-casting gap found on 2026-08-22.
- **A view filter can be used as an element query**, not just as graphics: `ParameterFilterElement`
  `.GetElementFilter()` feeds straight into a `FilteredElementCollector`. Combine several with
  `LogicalOrFilter`. `el.SuperComponent` excludes nested family instances from the result.
- **Legend component orientation is a magic negative integer** in `LEGEND_COMPONENT_VIEW`: -3 3D,
  -4 Bottom, -5 Section, -6 Back, -7 Front, -9 Right.
- **Orphan elevation markers** are `ElevationMarker.CurrentViewCount == 0` — the cleanup counterpart to
  `create-room-elevations.cs`, which creates them.
- **`ExtensibleStorage.Schema.EraseSchemaAndAllEntities` moved** — a static on `Schema` up to Revit 2020,
  a method on `Document` from 2021.
- **View underlay parameters split at Revit 2016** — `VIEW_UNDERLAY_ID` before,
  `VIEW_UNDERLAY_BOTTOM_ID` + `VIEW_UNDERLAY_TOP_ID` after.
- **CSV export must respect the Windows list separator** — a machine set to `;` produces a file that
  reads as one column everywhere else.

## State at the end

All 13 consistency checks pass. **All 351 fragments compile on Revit 2020, 2024 and 2027** — first pass,
no failures, which is the yesterday's-lessons (`ElementId.IntegerValue`, `Definition.ParameterType`)
staying learned. **None of the 3 new fragments has been run against a real model**; each is
report-or-dry-run by default and says so in its own header.
