# The Revit members that need an argument nobody can guess — and the ones that lie

Written 2026-08-24 from a targeted read of two independent model-inspection tools: **119 hand-written
per-type descriptors** in one, **52 per-member overrides** in the other. Both had to answer the same
question — *which members can you just call, and which need help* — and they answered it separately.

**The intersection is the useful part.** A member both teams had to special-case is one that genuinely
bites. This note is that list, filtered to the types this Brain's work actually touches. It exists
because none of it is in a signature: the API tells you `GetMaterialArea(ElementId, bool)` takes a bool,
and never that the bool selects between two different sets of materials.

## The pattern: a member that needs an argument you have to go and find

Most of the list is one shape. The member takes an argument that is not derivable from the object, so
calling it blind either throws or — much worse — answers for the wrong one.

| Member | The argument it needs | What goes wrong without it |
|---|---|---|
| `Element.GetMaterialIds(bool)` | **paint or geometry** — two different sets | The classic. Cost this Brain two real defects; see [`../brain-log.md`](../brain-log.md), 2026-08-23 |
| `Element.GetMaterialArea(id, bool)` | the SAME flag the id came from | A mismatched flag gives a wrong area, silently |
| `Element.GetPhaseStatus(phaseId)` | a phase | There is no "the" status — it is per phase |
| `Element.IsHidden(view)` / `CanBeHidden(view)` | a view | Visibility is never a property of an element alone |
| `Element.get_BoundingBox(view)` | a view, **or null for the model** | The two differ, and the view one follows the crop |
| `Element.GetDependentElements(filter)` | **null means everything** | Passing null is the useful call and looks like a mistake |
| `Element.GetEntity(schema)` | one schema, from `Schema.ListSchemas()` | You must enumerate schemas to find your own data |
| `Category.get_Visible(view)` / `get_AllowsVisibilityControl(view)` | a view | Same as element visibility |
| `View.GetCategoryHidden(catId)` / `GetCategoryOverrides(catId)` | a category | One call per category; there is no "all" |
| `View.GetFilterOverrides(id)` / `GetFilterVisibility(id)` / `GetIsFilterEnabled(id)` | a filter id from `GetFilters()` | Three separate states per filter, and all three must be right before a filter draws anything |
| `View.GetWorksetVisibility(worksetId)` | a workset | Per workset, per view |
| `SpatialElement.GetBoundarySegments(options)` | **four locations x free-faces on/off** | See [`../../scripts/actions/reporting/action-report-room-boundaries.cs`](../../scripts/actions/reporting/action-report-room-boundaries.cs) |
| `HostObject.GetSideFaces(shellLayer)` | Interior or Exterior | And neither is necessarily the face a room sees |
| `FamilyInstance.get_Room(phase)` / `get_FromRoom(phase)` / `get_ToRoom(phase)` | a phase | The bare property answers for the LAST phase only |
| `MEPSection.GetPressureDrop(id)` / `GetCoefficient(id)` / `GetSegmentLength(id)` / `IsMain(id)` | an id from that section's own `GetElementIds()` | Any other id throws |
| `Curve.GetEndPoint(i)` / `GetEndParameter(i)` | 0 or 1 | |
| `ConnectorManager.Lookup(i)` | 0 to `Connectors.Size - 1` | |

## Index is not number, and number is not index

`MEPSystem` exposes **both** `GetSectionByIndex(i)` and `GetSectionByNumber(n)`, and `SectionsCount`
bounds the **index**. The section's `Number` is a property of the section.

Looping `for (n = 1; n <= SectionsCount; n++)` and calling `GetSectionByNumber(n)` assumes the numbers
run exactly 1..N with no gaps. **This Brain made that mistake and shipped it for two hours** in
`action-report-mep-pressure-drop.cs` on 2026-08-24. On a system with sparse numbering that loop reads
some sections twice and misses others — and being a report, it looks plausible either way.

**Iterate by index; read `.Number` off each section.** Use `GetSectionByNumber` only when something else
handed you a number — which is exactly what `GetCriticalPathSectionNumbers()` does.

## Ask before you act — the validators nobody knows exist

Each of these turns a bare exception into a sentence a person can act on.

| Ask this | Before this |
|---|---|
| `ViewSchedule.IsValidCategoryForSchedule(catId)` | `CreateSchedule` |
| `ViewSchedule.IsValidCategoryForKeySchedule(catId)` | `CreateKeySchedule` |
| `ViewSchedule.IsValidCategoryForMaterialTakeoff(catId)` | `CreateMaterialTakeoff` |
| `ScheduleDefinition.CanFilterByValue(fieldId)` and its siblings | adding a schedule filter |
| `ScheduleDefinition.CanSortByField(fieldId)` | adding a sort or group field |
| `ElementIntersectsFilter.IsElementSupported(e)` / `IsCategorySupported(e)` | any clash test — this one fixed a silent under-report |
| `DocumentValidation.CanDeleteElement(doc, id)` | deleting |
| `ElementTransformUtils.CanMirrorElement(doc, id)` | mirroring |
| `ViewCropRegionShapeManager.IsCropRegionShapeValid(loop)` | setting a non-rectangular crop |
| `BoundaryValidation.IsValidHorizontalBoundary(loops)` | building from a boundary |
| `ParameterFilterUtilities.GetFilterableParametersInCommon(doc, catIds)` | building a `ParameterFilterElement` — a category that lacks the parameter gets the whole filter rejected |
| `Viewport.CanAddViewToSheet(doc, sheetId, viewId)` | placing a viewport |
| `LoadedFamilyIntegrityCheck.CheckFamily(doc, familyId)` | trusting a family from outside |

## Members that are simply not there — settled by compiling, not by reading

Names that exist in `RevitAPI.dll` on some *other* type and are not on the one you want. Each cost a
failed compile here:

- `MEPSection` has **no** `PressureDrop`, `PressureLoss`, `HydraulicDiameter`, `Size`, `Diameter`,
  `Length` or `Area`. The section total is **`TotalPressureLoss`**.
- `GlobalParameter` has no `GetLabels()`. It is **`GetLabeledDimensions()`**.
- `DataStorage` is a public-looking type a fragment may **not name at all** — "inaccessible due to its
  protection level". Recognise it by `GetType().Name`.

How to settle one of these, and why each probing technique lies on its own, is in
[`../revit-version-compatibility.md`](../revit-version-compatibility.md).

## Members with a version boundary in the middle

- `Document.GetUnusedElements(...)` / `GetAllUnusedElements(...)` — **not on Revit 2020**, present on
  2024 and 2027. These are the public API behind the native Purge Unused dialog, and their absence on
  2020 is exactly where this Brain's wrong "no public API equivalent" claim came from.
- `IndependentTag.LeaderEnd` / `LeaderElbow` / `TaggedLocalElementId` — replaced by the per-reference
  API, probably at 2022.
- `BoundaryValidation.IsValidBoundaryOnView` / `IsValidBoundaryOnSketchPlane` — 2023+.
  `IsValidHorizontalBoundary` is on every version.

## What "not supported" in a descriptor means, and why it matters less than it looks

Both tools mark long lists of members as not-callable — `JoinGeometry`, `AddInstanceVoidCut`,
`SetLeaderEnd`, every `GeometryCreationUtilities.Create*`. **That is not "these are dangerous."** It is
"an inspector must not mutate the model or fabricate geometry." A fragment's whole job is to do exactly
those things, in a transaction, on purpose.

**Read those lists as a map of what MUTATES**, not as a warning. The genuinely dangerous ones are few:
`Document.Close()`, which one of them marks forbidden outright — and
`Document.get_PlanTopologies(phase)`, which **modifies the document and therefore needs a transaction
even though it only reads.** That last one is the single member here that breaks the read/write
intuition completely, and `create-rooms-in-enclosed-regions.cs` gets it right only because it happens to
already be inside a transaction.
