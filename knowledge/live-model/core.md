# Live Model Notes — AJ AI Bridge scripting

> Entry point of the live-model knowledge set. Index: [`README.md`](README.md) — route from there to the topic you need.

Technical notes specific to writing C# snippets run via the `mcp__aj-tools-aj-ai__run_csharp` /
`ping` MCP tools against the user's live, open Revit document. This is about the ad-hoc bridge scripts
only — a separate concern from the Revit add-in's own compiled source code, which this Brain doesn't
cover (a gotcha in one doesn't necessarily apply to the other).

**This file explains the recipes; [`scripts/`](../../scripts/README.md) holds the actual working
code.** Check the scripts folder before writing new C#, and when a recipe here changes, update its
script too so the two never drift apart. Two shapes live there: `filters/` + `actions/` — small,
element-type-agnostic fragments composed per request (e.g. "which elements" + "what to do to them", see
the scripts README for the user's own worked example) — and `recipes/`, for the genuinely bespoke,
order-dependent, multi-stage builds below (HVAC placement/routing, MEP trace) that create new elements
rather than just act on existing ones and don't fit the filter+action shape.

**Contents** (this file is long — jump to the section you need, don't re-derive what's already here):
- Bridge basics — ping first, report version+model, script globals, what's blocked
- Revit version + unit conversion — 2020 `DisplayUnitType`, mm↔feet, fully-qualified types
- View visibility patterns — isolate/hide/reset, verify view state fresh each turn
- Tracing real MEP connectivity — bulk clustering, geometric trace, color-coding
- Undoing a mistake — native Revit Undo via PostCommand, never a delete script
- HVAC air terminal layout — Space airflow params, matched counts, checkerboard `(row+col)%2`,
  near-square row formula, grid orientation, Flow-parameter gotcha, multi-FCU zoning, `IsPointInRoom` Z
- Rotating equipment to face a target — connector identification (Fresh Air decoy), rotation math
- Drawing a duct between two points — sizing to the source connector, BreakCurve + explicit reconnect
- Branch duct from terminal to main duct — riser + real elbow + takeoff, cap-end recipe (7 steps)
- Slicing a main trunk for duct sizing — HIGH RISK, offset-cut recipe, orphaned-branch recovery
- Posting AJ Tools' own ribbon commands — doesn't work, don't re-attempt

## Bridge basics
- For a common category count with one optional parameter breakdown, prefer the native
  `model_summary` MCP tool when it is exposed. It performs one read-only bridge call and returns the
  Revit version and model title, so a separate ping is unnecessary. Keep `run_csharp` for complex,
  multi-parameter, geometry, and model-changing work.
- Always `mcp__aj-tools-aj-ai__ping` first if it's been a while — if it fails, Revit is closed or
  the AJ AI pane's Connect AJ AI Bridge toggle is off. Ask the user to reconnect rather than guessing.
- **Whenever reporting a successful ping, always also report the session snapshot** — the user wants this
  every time, not just on request (rule extended 2026-07-16: active view added to the original
  version+model rule). Get it in one follow-up `run_csharp` call by running
  [`scripts/context/context-active-view.cs`](../../scripts/context/context-active-view.cs), which returns
  everything the report needs: Revit version, model title (+ family vs project, worksharing), active view
  name/type, open views, and current selection count. Report compactly, e.g. "Connected — Revit 2020,
  model: MODEL PROJECT, active view: {3D} (3D), nothing selected." A bare "pong" with no snapshot is an
  incomplete ping report.
- Globals available directly in scripts: `Document`, `UIDocument`, `Application`, `UIApplication`. No
  `using AJTools...` — the script isn't compiled with a reference to AJTools.dll.
- Destructive ops (Delete/Purge/file writes) are refused unless `allowDestructive: true` is explicitly
  passed. This is deliberate — don't route around it.
- **The destructive-op guard reads the whole script as TEXT, including plain output strings, and it is
  CUMULATIVE** (found live 2026-07-26 while verifying `recipes/model-health-audit.cs`). That read-only
  audit — no `Document.Delete` anywhere, only counts — was refused because two of its output lines together
  said "Purgeable (dry-run)" and "delete via ... action-purge-unused.cs". Each line alone passed; together
  they crossed the threshold. **The fix is to soften the OUTPUT WORDING of genuinely read-only scripts
  ("Unused, removable later", "see X") — never to pass `allowDestructive: true` just to get a read past
  the guard.** Doing that would train away the one protection that catches a real mistake.
- **`ReferenceIntersector` (ray-casting) ONLY FINDS WHAT ITS 3D VIEW SHOWS — a silent, dangerous
  failure mode** (found live 2026-07-26). It runs inside a `View3D` and obeys that view's hidden
  categories, section box, view filters and closed worksets. A hidden category is invisible to a ray, so a
  probe reports "nothing there" with a wall standing right in front of it — no error, no warning, just a
  confident wrong answer. Proven with identical code on the same element: view `{3D}` with Walls hidden
  returned **0** hits; view `3D Plumbing` with Walls visible returned **4**. Always check
  `view.GetCategoryHidden(catId)` before trusting an empty ray result, prefer a full-visibility
  coordination view, and never let a ray-driven MOVE run against a partially hidden model — it will snap
  elements onto whatever happens to be visible behind the real surface. The ray fragments now warn (and
  `action-move-to-ray-hit.cs` refuses) when the target category is hidden.
- **Reflection / assembly-loading is hard-blocked** ("Loads assemblies or uses reflection to bypass normal
  API usage") — cannot reach into the add-in's own internal (non-public) classes this way. Only plain
  Revit API calls work. If a task seems to need this, do it with plain Revit API calls instead, or ask the
  user to run the real tool themselves.
- Multi-statement scripts need an explicit `return` — a trailing expression-without-semicolon (Roslyn
  scripting convention) does not reliably produce output here; the last line should be `return sb.ToString();`
  not just `sb.ToString();`.
- **A bridge call can transiently fail with "Revit UI was blocked by another command/tool or window"**
  even with no user action in between — this is Revit being momentarily busy, not a real error. Simply
  retry the same call; it recovers on its own. Don't treat one blocked response as a reason to change
  approach or report a failure.
- **Discover a category's real parameter names/IDs before bulk reading or writing on it, don't guess from
  a plausible name.** Run [`action-report-parameters.cs`](../../scripts/actions/reporting/action-report-parameters.cs)
  (or a one-off parameter dump) against one representative element of a category the first time it comes
  up in a session — parameter names vary by family/template, and a guessed name that happens to work on
  one project can silently miss or fail on another.
- **Watch for unbounded output on a large/complex query** — collecting or reporting every element in a
  big 3D view, or a whole-model dump with no category/region filter, can produce a very large response.
  Prefer a targeted filter (category, region, selection) over a blanket collector, and cap row counts on
  report actions (`maxRows` INPUTS already do this on the `report-*` fragments) rather than dumping
  everything.
- **Re-check the model/document identity if a session runs long.** The user can close, switch, or open a
  different Revit document without saying so. If a later call's `context-active-view.cs` snapshot shows a
  different model title than earlier in the same conversation, treat every earlier element ID / view ID /
  family name from before the switch as invalid — re-orient before continuing, don't assume continuity.
- **An empty result (zero elements, `[]`) is a valid answer, not an error.** If a correctly-scoped
  filter returns nothing, report the honest zero — don't assume the script failed, silently loosen the
  filter, or retry until something appears. Only re-check the scope if the user's wording suggests the
  filter itself was wrong.
- **Never invent or guess an ElementId.** Every id a script acts on must come from a query in this same
  session, recent enough to still trust (fresh-reads rule) — ids remembered from an earlier conversation
  or "probably still the same" are how a script silently acts on the wrong element.
- **Spatial words ("move it left / up / north") are view-relative — resolve the real direction before
  acting.** Left/right/up/down depend on the active view's orientation (and north can mean true vs
  project north); never guess a sign or an axis. Read the active view's orientation and a real reference
  (grids, levels, a named target element) first, restate the resolved direction plainly ("left in this
  view = −X, toward Grid A"), then move.
- **One composed script beats many bridge calls.** When many elements need the same change, run one
  filter+action script in one transaction — not a per-element loop of separate `run_csharp` calls.
  Fewer round-trips, a single undo step, one thing to verify.
- **Verify small after changing.** Read back fresh (never skip that), but scope the read-back to what
  changed — the count, or the changed elements' new values — not a whole-category re-dump (pairs with
  the unbounded-output rule above).
- **A same-script inline verification is not enough proof — re-check with a SEPARATE later call.** Found
  live 2026-08-01 (project 4355, filter fill-color cleanup across 6 view templates): a script cleared
  `TRG_Accessories_Duct`'s fill override, reported "removed" for all 6, and even an immediate same-script
  read-back confirmed unset. A later script (after an unrelated restore pass touching 5 sibling filters,
  which by inspection never referenced this filter's name) found all 6 back to their pre-clear fill color.
  Root cause not confirmed — the restore script's logic looked correct on inspection, so this may be a
  bridge/transaction-commit timing quirk rather than a code bug. Re-clearing and verifying in a THIRD,
  independent call held. **Practical rule: after any filter/graphic-override mutation across multiple
  elements, don't trust a same-call verification alone — issue one more read-only call afterward,
  separately, before reporting success**, especially when other mutating calls run in between.
- **On a workshared model, the bridge can't sync or relinquish.** After bulk changes (the context
  snapshot reports worksharing), remind the user to Synchronize with Central themselves — edited
  elements stay borrowed by them until they do.
- **A view-scoped `FilteredElementCollector(Document, viewId)` UNDER-REPORTS right after a create+group
  transaction — it can miss elements that are genuinely there and fully visible.** Measured 2026-07-27:
  immediately after drawing + grouping 20 detail circles in `1 - Mech`, the query
  `FilteredElementCollector(Document, view.Id).OfClass(typeof(CurveElement))` returned **20** curve
  elements and **1** group; the byte-identical query re-run moments later returned **74** and **3**. Nothing
  had been created, deleted or hidden in between — checked: `IsHidden(view)` false for every member, Lines
  category not hidden, no crop box, no temporary hide/isolate. The first read simply didn't see the
  pre-existing elements. This is dangerous precisely because the *wrong* answer looks like a clean fact and
  invites the conclusion "the user's earlier work was deleted." **Never conclude something is gone from a
  view-scoped read alone.** Confirm existence document-wide first (`Document.GetElement(id)`, or an
  unscoped collector grouped by `OwnerViewId`) — those were correct and complete on the first try — and
  only then, if you truly need visibility, re-run the view-scoped query.
- **An element hosted on a linked model's face (not this document's own levels) reports `LevelId ==
  InvalidElementId` — this is expected, not a bug.** Grouping such elements by level via the normal
  `LevelId`/level-parameter lookup silently fails for them. If level-grouping matters for an element like
  this, read its real Z coordinate (`get_Location`-style bounding-box or `LocationPoint.Point.Z`) and
  compare against known level elevations instead.
- **A parameter report by display name gives a BLANK column for a parameter that doesn't exist — it never
  says "no such parameter".** Verified live 2026-08-04: `report_parameters` asked for `Level` on Ducts
  returned an empty cell on every row, which reads exactly like "the parameter is there but unset." The real
  name on a duct is **`Reference Level`** (`BuiltInParameter.RBS_START_LEVEL_PARAM`); with that name every
  row filled in immediately. So a blank column is ambiguous between "empty value" and "wrong parameter
  name", and on a takeoff or a schedule that ambiguity is a silent wrong answer, not a visible error.
  **Before reporting a blank column as missing data, confirm the name exists on one sample element** — loop
  `element.Parameters` and print the definition names — rather than concluding the value is unset. Applies
  to any name-based parameter read, the native tool and hand-written script alike.

## Revit version + unit conversion
- **Check which Revit version is actually open before assuming a unit API** — `UnitTypeId.Millimeters`
  only exists from 2021 onward; on 2020 or earlier use
  `UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS)` instead. Don't assume the
  version from a past session; a different project, or a future session on the same project, may be
  running a different Revit year.
- The user always speaks in **mm**, Revit's internal API is always **feet** — convert both ways explicitly,
  don't leave raw feet in a reply.
- `Autodesk.Revit.DB.Structure.StructuralType` must be **fully qualified** when calling
  `Document.Create.NewFamilyInstance(...)` — a bare `StructuralType` fails to compile in this script
  context ("inaccessible due to its protection level").
- Same fully-qualify rule hits MEP types in this script context: `Autodesk.Revit.DB.Mechanical.Duct` and
  `Autodesk.Revit.DB.Mechanical.MechanicalSystemType` — a bare `Duct`/`MechanicalSystemType` fails with
  "type or namespace not found". `Connector.DuctSystemType`'s enum type (`Autodesk.Revit.DB.Mechanical.
  DuctSystemType`) goes further — even fully qualified it's "inaccessible due to its protection level" (the
  enum itself isn't public in this script context, only the property that returns it is) — compare via
  `connector.DuctSystemType.ToString() == "SupplyAir"` instead of referencing the enum type directly.
- `new ElementId(someLong)` fails to compile with a confusing error — "cannot convert from 'long' to
  'Autodesk.Revit.DB.BuiltInParameter'" — because this Revit version's `ElementId` only has an `(int)` and
  a legacy `(BuiltInParameter)` constructor, no `(long)` overload; `long` doesn't implicitly narrow to
  `int` so the compiler falls through to the wrong overload. Cast explicitly: `new ElementId((int)someLong)`.
- **View title EXTENSION LINE length has no API lever at all on Revit 2020 — confirmed from an external
  library's own source, not assumed.** A Viewport's title-line length (the line under the view title on a
  sheet, distinct from the label text) is not exposed as a Viewport Type parameter (checked every
  parameter live, project 4355: only `Show Title`/`Show Extension Line` on/off exist, no numeric length)
  nor as a parameter on the title family itself (checked `M_View Title`'s own type params too — none).
  Confirmed why: the Rhythm-for-Dynamo package's own `Viewport.SetViewTitleLength` source throws `"This
  node only works in Revit 2022 as that is when this API was added"` — the underlying Revit API for this
  didn't exist before 2022. On 2020, the only working lever is the on/off `Show Extension Line` toggle
  (type-level — duplicate the viewport type before touching it if only some sheets should change, per the
  blast-radius check below). A true "auto-fit line to text width" needs either Revit 2022+, or a from-scratch
  parametric rebuild of the title family (formula-driven dimension tied to the label width) — not a script.
- **View title POSITION is also unsettable on 2020 — but the offsets can still be CALCULATED for the user
  to drag.** Reflection on Revit 2020's `Viewport` (checked live, not from memory) shows no `LabelOffset`
  property and no title-moving method at all — only the read-only `GetLabelOutline()`. So "center all the
  view titles" cannot be scripted here. What *is* possible, and turned out to be the useful deliverable:
  compute each title's exact centering offset so the manual drag is a known number instead of eyeballing.
  `vp.GetBoxOutline()` = the view content area on the sheet, `vp.GetLabelOutline()` = the title assembly
  (text + extension line). Both are sheet-space `Outline`s in feet:
  ```csharp
  double boxCenterX = (box.MinimumPoint.X + box.MaximumPoint.X) / 2.0;
  double lblCenterX = (lbl.MinimumPoint.X + lbl.MaximumPoint.X) / 2.0;
  double deltaX = boxCenterX - lblCenterX;   // + = drag right, - = drag left
  double titleAboveBoxBottom = lbl.MaximumPoint.Y - box.MinimumPoint.Y;  // vertical consistency check
  ```
  Note `GetLabelOutline()` spans text AND extension line, so this centers the whole assembly — centering
  only the text would need a different measure. The vertical figure is worth reporting alongside: it
  exposes titles sitting at inconsistent heights across sheets, which is invisible when eyeballing one
  sheet at a time. Legends have no label outline — skip `ViewType.Legend` or guard the call.
- **Measuring a hypothetical state without changing the model: mutate inside a Transaction, then
  `RollBack()`.** `GetLabelOutline()` includes the extension line, so it can't directly tell you the text
  width — but toggling `VIEWPORT_ATTR_SHOW_EXTENSION_LINE` off, calling `doc.Regenerate()`, re-measuring,
  then rolling back yields the text-only extents with zero persistent change. The width delta between the
  two states IS the line's horizontal overhang past the text — i.e. exactly how far the user must drag the
  grip to make the line fit the text, which is otherwise pure guesswork on a 2020 model:
  ```csharp
  var withLine = vp.GetLabelOutline();
  using (var t = new Transaction(doc, "measure only")) {
      t.Start();
      vpType.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_EXTENSION_LINE).Set(0);
      doc.Regenerate();                       // REQUIRED - outline is stale without it
      var textOnly = vp.GetLabelOutline();
      // overhang = withLine.width - textOnly.width
      t.RollBack();                            // nothing persists
  }
  ```
  Verified live 2026-08-01 (project 4355): read back `SHOW_EXTENSION_LINE == 1` after rollback, confirming
  no change leaked. Generalises to any "what would this look like if…" question — safer than change-then-undo,
  because it never enters the user's undo stack at all. Note this still needs the type-level blast-radius
  check below when reading which viewports share the type, even though nothing is committed.
  **Caveat found the same day: not every property refreshes mid-transaction.** `GetLabelOutline()` DID
  update after `Regenerate()` when toggling the extension line, but `GetBoxOutline()` did NOT update after
  a `view.CropBox` change (crop verifiably halved, reported box width unchanged). So a rollback-measure
  test must assert that the INPUT actually changed AND that some output moved — otherwise "no change"
  reads as a real finding when it's really a stale read. Always print both, and label the test inconclusive
  rather than concluding from an unrefreshed value.
- **A viewport placed BY SCRIPT gets its view-title line defaulted to the full viewport width** — measured
  exactly `boxWidth + 6.4mm`, identical across 5 script-placed viewports (project 4355, 2026-08-01), versus
  a hand-set constant 92.6mm on the hand-tidied originals regardless of their differing box widths. Since
  Revit 2020 has no API to set that length (see above), **script-placed viewports on a sheet will always
  need a manual line-drag afterwards if the project's convention is line-fits-text** — deleting and
  re-placing just reproduces the same default. Worth telling the user up front when bulk-placing viewports,
  rather than letting it surface as "why are all these lines too long?" later.
  Related: a viewport's sheet box width is driven by ANNOTATION extents, not the crop region — two views
  with an identical 158.4mm crop had 202.1mm and 215.7mm box widths. Don't expect tightening a crop to
  shrink the viewport's footprint or its title line.
- **A Viewport's "type" (title-block-with-scale, etc.) is a `Show Extension Line`/`Show Title`-level
  ElementType, and it can be SHARED by dozens of viewports across the whole document, not just the sheet
  you're looking at.** Before changing any Viewport Type parameter, count
  `new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Count(v => v.GetTypeId() ==
  typeId)` — found live (project 4355): one viewport type was shared by 77 viewports across the entire
  document, including the formal issued sheet set, when the user only wanted 3 new sheets changed. Fix:
  `sourceType.Duplicate("new name")`, edit only the duplicate, then `viewport.ChangeTypeId(newTypeId)` on
  just the intended viewports — leaves every other sheet's title style untouched.

### Reading an element's level, workset or design option — see element-identity.md

Those four traps were split out on 2026-08-06 when this file passed the ~300-line rule:
[`element-identity.md`](element-identity.md) — why a Duct's level is not where you expect, why
`ELEM_PARTITION_PARAM` reads as null, and the two things Design Options will not let the API do.

### Category ID quick reference (for reading raw output only — never hardcode these in scripts)
Verified live (2026-07-14) against the real installed RevitAPI.dll — all 27 matched exactly, none wrong:

| Category | Id | Category | Id |
|---|---|---|---|
| Walls | -2000011 | Sheets | -2003100 |
| Doors | -2000023 | Schedules | -2000573 |
| Windows | -2000014 | Levels | -2000240 |
| Floors | -2000032 | Grids | -2000220 |
| Roofs | -2000035 | Views | -2000279 |
| Ceilings | -2000038 | Viewports | -2000510 |
| Rooms | -2000160 | MEP Spaces | -2003600 |
| Stairs | -2000120 | Plumbing Fixtures | -2001160 |
| Columns | -2000100 | Lighting Fixtures | -2001120 |
| Structural Framing | -2001320 | Mechanical Equipment | -2001140 |
| Curtain Wall Panels | -2000170 | Electrical Equipment | -2001040 |
| Curtain Wall Mullions | -2000171 | Generic Model | -2000151 |
| Furniture | -2000080 | Casework | -2001000 |
| Planting | -2001360 | | |

**Why this is a reference, not something scripts should use directly**: every fragment in `scripts/`
writes the symbolic name (`BuiltInCategory.OST_Walls`), never the raw negative number — a typo in the enum
name is a compile error, a typo in a raw int (e.g. transposing `-2001320` and `-2001360`) would silently
point at the wrong category with no warning. This table is only useful for recognizing a bare category Id
when it shows up in raw output (a warning, an export, a debug dump) — converting int→enum in a script is
always a live one-line cast (`(BuiltInCategory)someInt`) or `Category.GetCategory(doc, id).Name`, which is
authoritative for every category, not just these 27 common ones.
