# Live Model — View visibility & section views

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## View visibility patterns (used a lot — isolate/hide requests are common)
- **Isolate by category**: `view.IsolateCategoriesTemporary(List<ElementId> categoryIds)` inside a
  `Transaction`. Only works on `View3D`/plan/section-type views, not schedules — check
  `view is View3D` (or the relevant type) first and bail out clearly if it's the wrong kind of view.
- **Isolate specific elements** (not a whole category): `view.IsolateElementsTemporary(List<ElementId> ids)`
  — use when the user wants to see only a subset of a category (e.g. one size of VCD), not the whole category.
- **Reset before re-isolating to a *different* subset**, so the new isolation fully replaces the old one
  rather than assuming it's additive: `view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate)`
  then apply the new isolate call. In practice a fresh `IsolateElementsTemporary`/`IsolateCategoriesTemporary`
  call already replaces the prior state on its own, but reset-then-apply is the clearest way to reason
  about it when the user describes it as "unhide X, then show only Y."
- These are **temporary** (dashed cyan border in Revit), not permanent visibility overrides — always tell
  the user that "Reset Temporary Hide/Isolate" clears it, in case they want it made permanent instead
  (`view.SetCategoryHidden(catId, true)` per category, inside a transaction, if they ever ask for that).

**The native `isolate_elements`/`hide_elements` MCP tools take ONE category — there is no multi-category
form.** Its `category` field is a single string, so "show only the ducts, the fittings and the walls"
cannot be one native call. Two working routes, confirmed live 2026-08-13:
1. **`list_elements` per category, then one `isolate_elements` with the combined `elementIds`.** Best
   when the user is building the set up conversationally one category at a time ("also the wall") and the
   counts are small — each addition is one list call plus one re-isolate, and `elementIds` takes priority
   over `category` when both are given.
2. **Compose [`filter-by-multiple-categories.cs`](../../scripts/filters/by-identity/filter-by-multiple-categories.cs)
   + [`action-isolate-elements.cs`](../../scripts/actions/visibility/action-isolate-elements.cs)** and run
   it as one `run_csharp` call. Best when the category list is known up front or large enough that
   round-tripping IDs is wasteful — and it survives the model changing between calls, which a captured
   ID list does not.
**Re-isolating replaces, it does not accumulate.** Each `isolate_elements` call resets the prior
temporary state first, so when the user adds a category you must pass the *whole* set again, not just the
new one — passing only the addition silently drops everything they already had on screen.

**Don't assume view state from an earlier turn is still there — verify, don't recall.** Isolation and
per-element color overrides (`SetElementOverrides`) can be cleared between messages — by the user resetting
Temporary Hide/Isolate themselves, using Revit's Undo, or editing Visibility/Graphics directly. Confirmed
in practice: color overrides applied in one turn were gone by a later turn with no error or warning — a
`view.GetElementOverrides(id).ProjectionLineColor.IsValid` check caught it before wrongly reporting
"already colored, nothing to do." Cheap insurance: for anything you set on the view in a previous turn
(isolation, colors, hidden categories) that a later request depends on, do a quick read-back check before
assuming it survived, rather than trusting your own earlier tool-call result as still-current truth.

**Reading overrides back: the pattern-visibility getters lie by default, colors don't.** Confirmed
2026-07-14 building `action-report-graphic-overrides.cs`: `OverrideGraphicSettings.IsSurfaceForegroundPatternVisible`
/ `IsCutForegroundPatternVisible` return `true` even on a completely untouched element — they mean
"would a pattern show if one were set," not "an override exists." Using them as the "has an override"
signal produced false positives on 5 real, genuinely clean duct terminals. The real signal is color/pattern
validity — `ProjectionLineColor.IsValid`, `CutLineColor.IsValid`, `SurfaceForegroundPatternColor.IsValid`,
`CutForegroundPatternColor.IsValid`, or a real (non-`InvalidElementId`) pattern Id — same check already
proven above for staleness detection. Also confirmed via reflection, not memory: the getters are named
`IsSurfaceForegroundPatternVisible`/`IsCutForegroundPatternVisible` (bool properties), not
`SurfaceForegroundPatternVisible` — guessing the un-prefixed name is a compile error, not a silent bug.

**Current selection can include elements not present in the active view's collector.** Confirmed
2026-07-19 running `action-highlight-vs-rest.cs` (color selection red, rest of active 3D view gray): 22
elements were selected but only 19 showed up in `new FilteredElementCollector(doc, view.Id)` — the other
3 were `IndependentTag`s (Equipment Tags) that belonged to a different open plan view, not the active 3D
view. Revit's selection (`UIDocument.Selection.GetElementIds()`) is document-wide, not scoped to the
active view, and tags in particular don't exist/display in 3D views at all. Don't treat a mismatch
between "selected count" and "highlighted count" as a bug — cross-check which of the selected ids are
actually `inViewCollector` before reporting a discrepancy as an error.

## Creating a section view (`ViewSection.CreateSection`) — the Transform must be right-handed
- `ViewSection.CreateSection(doc, viewFamilyTypeId, sectionBox)` takes a `BoundingBoxXYZ` whose
  `Transform` needs `BasisZ` to equal `BasisX cross BasisY` (a proper right-handed orthonormal basis).
  Get the sign of any basis vector wrong and Revit throws — but the exception's `.Message` comes back
  **empty string**, not a helpful description, so it silently looks like "no reason given" unless you
  also print `ex.GetType().Name` and `ex.StackTrace` to diagnose it.
- Concretely: `BasisX` = the section's left-right (in-view) axis, `BasisY` = up (usually global
  `XYZ.BasisZ`), `BasisZ` = the direction the camera looks (into the cut). Pick `BasisX` and the look
  direction independently for each of two perpendicular sections through the same plan and check the
  cross product by hand — flipping which world axis maps to `BasisX` to get the "look" direction you want
  (e.g. looking west instead of east) also flips whether the resulting transform is still right-handed;
  it isn't automatic, you may need to negate `BasisX` (not `BasisZ`) to restore a valid basis while
  keeping the same look direction.
- After creating a section (or any new view), do a fresh read-back (`FilteredElementCollector(doc,
  view.Id)` element count) before calling it done — confirms the crop/depth actually captures real model
  content instead of an accidentally-empty slice.

## Duplicating a VIEW TEMPLATE — `View.Duplicate()` does not work on templates
Confirmed live 2026-08-01: calling `.Duplicate(ViewDuplicateOption.Duplicate)` on a `View` where
`IsTemplate == true` throws `"View cannot be duplicated"` — and `CanViewBeDuplicated(...)` returns `false`
for every `ViewDuplicateOption` on a template, so there's no supported combination that makes the direct
call work. Use an element copy instead, inside a `Transaction`:
```csharp
var copiedIds = ElementTransformUtils.CopyElements(
    doc, new List<ElementId> { sourceTemplateId }, doc, Transform.Identity, new CopyPasteOptions());
var newTemplate = doc.GetElement(copiedIds.First()) as View;
```
Revit auto-suffixes the copy's name (e.g. `SourceName1`) since template names must be unique — read
`newTemplate.Name` back and set it to the real target name in a second transaction rather than assuming
the suffix. The copy carries over all filters, overrides and visibility settings from the source
unchanged; the source template itself is untouched, so anything else still using it is unaffected.

## Viewports and view titles on a sheet — Revit 2020 API limits

Moved here from [`core.md`](core.md) on 2026-08-13 when that file passed the ~300-line rule for the
second time. It belongs here on subject, not only on size: none of it is about units or bridge basics,
and all of it is about what a viewport on a sheet will and will not let a script do.

- **View title EXTENSION LINE length has no API lever at all on Revit 2020 — confirmed from an external
  library's own source, not assumed.** A Viewport's title-line length (the line under the view title on a
  sheet, distinct from the label text) is not exposed as a Viewport Type parameter (checked every
  parameter live, project 4355: only `Show Title`/`Show Extension Line` on/off exist, no numeric length)
  nor as a parameter on the title family itself (checked `M_View Title`'s own type params too — none).
  Confirmed why: **the underlying Revit API for setting a view-title line length did not exist before
  2022** — the same 2022 boundary recorded in
  [`../revit-version-compatibility.md`](../revit-version-compatibility.md). Nothing on 2020 can set it,
  which is why no amount of hunting for the right parameter finds one. On 2020, the only working lever is the on/off `Show Extension Line` toggle
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

