# Dynamo vocabulary → what this setup uses instead

The user knows Dynamo, so requests may arrive in Dynamo node names ("do a FilterByBoolMask", "get the
Element.Location", "List.GroupByKey by size"). This setup does NOT use Dynamo — everything goes through
the AJ AI Bridge in C#. **Every Dynamo node has a native equivalent here; never tell the user something
is unavailable because "we don't have that node."** Translate and route:

| Dynamo node family | This setup's equivalent |
|---|---|
| `Element.GetParameterValueByName` / `SetParameterByName` | native `report_parameters` / `set_parameter_value` tools, or `action-report-parameters.cs` / `action-set-parameter-value.cs` |
| `Element.Name/Id/Category/Level/OwnerView` | any filter fragment — reports always include these (Element-ID rule) |
| `Element.Room` / `Element.Space` (which room holds it) | `action-count-by-spatial-container.cs`; the reverse (elements in a room) is `filter-by-room.cs` / `filter-by-space.cs` |
| `All Elements of Category` / `All Elements in Active View` | `filter-by-category.cs` / `filter-by-view.cs` |
| `Select Model Element(s)` | `filter-by-current-selection.cs` (read) / `select_elements` tool (write) |
| `FamilyInstance.ByPoint` / `.Symbol` / `.Host` | `create-point-based-element.cs` / `filter-by-family-type.cs` / `filter-by-host.cs` |
| `ElementType.Duplicate` | `action-duplicate-type.cs` |
| `Element.MoveByVector/Rotate/Copy/Delete` | the `actions/move-copy-rotate/` group + `action-delete-elements.cs` |
| `Element.SetWorkset` / `SetPhase` / `GetMaterials` / `BoundingBox` | `action-set-workset.cs` / `action-set-element-phase.cs` / `action-material-takeoff.cs` + `filter-by-material.cs` / `action-report-bounding-box.cs` |
| `View.*` (create, duplicate, template, scale, crop, isolate/hide) | `create-view.cs`, `action-duplicate-views.cs`, `action-apply-view-template.cs`, `action-set-view-properties.cs`, `action-set-view-crop.cs`, isolate/hide actions |
| `Sheet.*` / `Viewport.*` / `TitleBlock.ByName` | `create-sheet.cs`, `action-place-viewport-on-sheet.cs`, `action-report-sheet-title-blocks.cs` / `action-set-sheet-title-block.cs`; viewports are Elements — move them with `action-move-elements.cs` |
| `Room.*` / `Space.*` (area, volume, number, center) | `action-report-room-space-data.cs`; boundaries are used inside the HVAC recipes |
| `Point/Line/Curve/Solid.*` (geometry building blocks) | plain Revit-API C# inside any fragment — `XYZ`, `Line.CreateBound`, curve methods, `BooleanOperationsUtils`; not fragments because they're one-liners, not jobs |
| `List.*` (all 20+ list nodes) | plain LINQ in any fragment — `Where` (FilterByBoolMask), `GroupBy` (GroupByKey), `Distinct` (UniqueItems), `OrderBy` (Sort), `SelectMany` (Flatten), indexers (GetItemAtIndex)... |

The one-sentence version: **Dynamo packages the Revit API as visual nodes; the bridge exposes the same
API as C# — so the translation is always "which fragment does that job", never "we lack that node."**
Checked against Dynamo's standard node list 2026-07-26 (user supplied 100 nodes; all 100 mapped).

## Top Dynamo PACKAGES → this setup (harvested 2026-07-26)

The standard-node table above covers out-of-the-box Dynamo. The famous community packages were compared
too; their flagship jobs now map like this:

| Package | Its flagship job | Here |
|---|---|---|
| **Clockwork** | Element.SubComponents; Wall/FloorType.Layers | `filter-by-subcomponents.cs`; `action-report-compound-structure.cs` |
| **Rhythm** | Duplicate sheet WITH views at same positions | `action-duplicate-sheet.cs` |
| **MEPover** | Duct/pipe from lines; connector queries | `create-duct.cs`/`create-pipe.cs`; `action-report-connectors.cs` |
| **Bimorph** | Clash detection; curves from CAD layers | `action-report-clashes.cs`; `action-extract-cad-curves.cs` |
| **Genius Loci** | Room boundaries, view filters, link utilities | `action-report-room-boundaries.cs`; view-filter + link fragments |
| **archi-lab** | Bulk get/set parameters, sheets from data, cross-doc copy | parameter actions + CSV round-trip; `create-sheet.cs`; `action-copy-from-link.cs` |
| **Spring Nodes** | FamilyInstance.ByGeometry (family from raw geometry) | deliberately NOT built — proper family authoring via the family-creation skill beats geometry-dump families |
| **Data-Shapes** | UI input forms for scripts | no tool needed — the conversation IS the input form |
| **Orchid** | Open/close/background-process documents | out of scope — the bridge works on the OPEN document by design |

The last three rows are deliberate decisions, not gaps — don't "fix" them without a real case.

## The user's mental model (2026-07-26) — use it when explaining

The user thinks of fragments AS Dynamo nodes: fragment = node, the shared `elements`+`sb` contract =
the wire, a composed script = a graph, a recipe = a saved .dyn. This is accurate and endorsed — build on
it when explaining compositions. The one correction to keep making: composing proven blocks reduces
errors, it does not remove them — wrong INPUTS, stale model state, and never-yet-run fragments are the
three error sources composition can't fix; the standing rules (fresh inputs, explorer-first, verify
after) close that gap.
