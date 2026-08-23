# AJ AI Brain — every fragment, and what it does

**359 fragments**, generated from the files on 2026-08-24. Status comes from each fragment’s row in `scripts/README.md`, which is this repo’s single source of truth for it.

✅ proven on a real model · ⚠️ written, not yet run · ❓ no status recorded · ⛔ blocked · 🚫 impossible on this Revit

| Status | Count |
|---|---|
| ✅ PROVEN | 247 |
| ⚠️ not run | 62 |
| ❓ unproven | 38 |
| ⛔ blocked | 12 |

## actions/color-graphics  *(25)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-apply-view-filter.cs` | Add an existing filter (by name) to a view, set its color/fill override, and set its visibility — the "put it on a view and make it do something" step after action-create-view-filter.cs... |
| ✅ | `action-color-by-group.cs` | Apply a DISTINCT color per sub-group within `elements`, grouped by the actual value of any named parameter (e.g. |
| ⚠️ | `action-copy-view-filters.cs` | Copy the view filters from ONE view (or view template) onto every view in `elements`, keeping each filter's OVERRIDES and its visibility — "I set the colours up on this plan, now put the... |
| ✅ | `action-create-selection-filter.cs` | Save `elements` as a named Revit SELECTION FILTER (SelectionFilterElement) — an explicit list of specific elements, not a rule. |
| ✅ | `action-create-view-filter.cs` | Create a Revit VIEW FILTER (ParameterFilterElement) — Revit's own Visibility/Graphics > Filters tab rule-based mechanism, NOT this repo's `filters/` folder. |
| ⚠️ | `action-create-view-filters-by-value.cs` | Read every distinct value of one parameter across `elements`, then build ONE PERSISTENT VIEW FILTER PER VALUE, each in its own colour, and apply them to a view — "a filter per system",... |
| ✅ | `action-highlight-vs-rest.cs` | Color every element ALREADY IN THE ACTIVE VIEW gray, except `elements` (the filtered highlight subset, from a filter fragment above), which gets its own highlight color instead. |
| ❓ | `action-match-graphics.cs` | Copy the graphic overrides OFF one source element and onto every element in `elements` — Revit's "match this one's look" job, done in bulk. |
| ⚠️ | `action-remap-line-styles.cs` | Move every line off one line style and onto another, across the whole model — "everything on <Thin Lines> should be MEP_DUCT", "we renamed the standard, re-point the old lines". |
| ✅ | `action-remove-view-filter.cs` | Take a filter OFF a view (view.RemoveFilter) — the filter definition itself still exists in the document and can be re-applied to this or another view later. |
| ✅ | `action-report-category-overrides.cs` | Read back which categories currently have a CATEGORY-LEVEL graphic override set in a view — the reverse lookup for action-set-category-color.cs/action-set-category-halftone.cs/... |
| ✅ | `action-report-graphic-overrides.cs` | Read back the current graphic overrides on every element in `elements`, in the active view — projection/cut line color, surface fill pattern color + visibility, transparency, halftone. |
| ✅ | `action-report-view-filters.cs` | List every filter (View Filter AND Selection Filter — the shared FilterElement base) in the whole document, and which real views currently have each one applied. |
| ✅ | `action-reset-category-graphics.cs` | Clear a category-wide graphic override in a view for one or more categories at once — the paired "undo" for action-set-category-color.cs. |
| ✅ | `action-reset-graphic-overrides.cs` | Clear graphic overrides (color, fill pattern) on every element in `elements` — the paired "undo" for action-set-color-uniform.cs / action-color-by-group.cs when the user wants the... |
| ✅ | `action-set-category-color.cs` | Override one or more ENTIRE categories' line/fill color in a view — Revit's own Visibility/ Graphics > Model Categories per-category override, not a per-element one. |
| ✅ | `action-set-category-halftone.cs` | Turn halftone ON or OFF for one or more ENTIRE categories in a view — the category-level sibling of action-set-halftone.cs, same relationship action-set-category-color.cs has to... |
| ✅ | `action-set-category-line-style.cs` | Override line WEIGHT and/or line PATTERN for one or more ENTIRE categories in a view — the category-level sibling of action-set-line-style.cs, same relationship... |
| ✅ | `action-set-category-transparency.cs` | Set surface transparency (0-100%) for one or more ENTIRE categories in a view — the category-level sibling of action-set-transparency.cs, completing the same element/category pairing... |
| ✅ | `action-set-color-uniform.cs` | Apply ONE color to every element in `elements` — both line color AND a solid surface fill (the user explicitly wants both, not just line color, so it reads as that color in shaded/... |
| ✅ | `action-set-halftone.cs` | Turn halftone ON or OFF for every element in `elements` — a distinct override from color (OverrideGraphicSettings.SetHalftone), covered nowhere else in this group. |
| ✅ | `action-set-line-style.cs` | Override line WEIGHT and/or line PATTERN (dashed, dotted, ...) for every element in `elements` — every other action in this group only ever touches line/fill COLOR, never weight or pattern. |
| ⚠️ | `action-set-link-overrides.cs` | Grey / halftone / fade a whole LINKED MODEL in one view — what the "Revit Links" tab of Visibility-Graphics does, reached from code. |
| ✅ | `action-set-transparency.cs` | Set surface transparency (0-100%) on every element in `elements` in the active view — for "make these see-through" requests, e.g. |
| ⚠️ | `action-show-analysis-heatmap.cs` | Paint a GRADIENT HEATMAP over the model from a number per element — pipes by pressure drop, spaces by airflow, ducts by velocity, rooms by occupancy. |

## actions/move-copy-rotate  *(13)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-align-elements.cs` | Move every element in `elements` so it matches ONE reference element's position along the chosen axis/axes — Revit's own "Align" tool. |
| ❓ | `action-align-mep-elevation.cs` | Line up the MEP runs in `elements` vertically by their TOP, their BOTTOM or their CENTRE — "get all the services in this corridor level with each other", "align the bottoms so the... |
| ✅ | `action-array-elements.cs` | Create multiple evenly-spaced copies of every element in `elements` — the "AutoCAD Array" operation. |
| ✅ | `action-copy-elements.cs` | Duplicate every element in `elements`, offset by one vector (mm, X/Y/Z) — e.g. |
| ✅ | `action-fillet-elements.cs` | Round the corner between exactly TWO linear elements in `elements` — the "AutoCAD Fillet" operation. |
| ✅ | `action-flip-elements.cs` | Flip the hand and/or facing orientation of every FamilyInstance in `elements` (a door swinging the wrong way, equipment facing the wrong direction) — Revit's own "Flip" arrows, scripted. |
| ✅ | `action-mirror-elements.cs` | Mirror every element in `elements` across a vertical plane defined by two plan points (mm) — Revit's "Mirror - Draw Axis". |
| ✅ | `action-move-elements.cs` | Translate every element in `elements` by one offset vector (mm, X/Y/Z) — e.g. |
| ✅ | `action-move-to-ray-hit.cs` | Fire ONE ray out of each element in `elements` and move that element to whatever the ray hits, plus an optional offset. |
| ✅ | `action-offset-elements.cs` | Offset each linear element in `elements` sideways by a perpendicular distance — the "AutoCAD Offset" operation. |
| ✅ | `action-rotate-elements.cs` | Rotate every element in `elements` around a vertical axis by one angle (degrees) — e.g. |
| ❓ | `action-snap-to-ceiling-grid.cs` | Move each point-based element in `elements` sideways onto the nearest CEILING TILE CENTRE — diffusers, sprinklers, light fittings, smoke detectors sitting neatly on the grid instead of... |
| ✅ | `action-trim-extend-elements.cs` | Trim or extend exactly TWO linear elements in `elements` so they meet cleanly at one corner — the "AutoCAD Trim/Extend to a corner" operation. |

## actions/parameters-naming  *(21)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-add-parameter-prefix-suffix.cs` | Edit the EXISTING text of one named parameter across `elements` — add a prefix, add a suffix, or find/replace a substring inside it — instead of overwriting it with a flat new value. |
| ✅ | `action-add-project-parameter.cs` | Create a NEW parameter (via a shared parameter definition) and bind it to one or more categories — "add a project parameter called AJ_Status to Duct Accessories" — genuinely different... |
| ❓ | `action-assign-location-data.cs` | WRITE where each element actually IS onto its own parameters — the Room or Space it sits in, its Level, and its X/Y/Z coordinates. |
| ✅ | `action-copy-parameter-value.cs` | Copy one parameter's value into a different parameter, across every element in `elements` — e.g. |
| ⛔ | `action-create-phase.cs` | Create one or more new project Phases, each appended in order after whatever the current last Phase is. |
| ✅ | `action-delete-phase.cs` | Permanently delete one or more project Phases by name — completes the phase Create/Rename/Delete lifecycle. |
| ✅ | `action-find-replace-element-name.cs` | Find/replace, prefix, or suffix EACH element's own Name (`Element.Name`) in `elements` — works on ANY nameable element, not just one category: Rooms, Sheets, Views, Levels, Grids,... |
| ✅ | `action-find-replace-text-notes.cs` | Find (report) or find-and-replace a string inside the TEXT of the TextNotes in `elements` — annotation QA across views/sheets. |
| ⚠️ | `action-import-parameters-from-csv.cs` | Read a CSV (ElementId first column + one column per parameter — the exact shape action-export-parameters-to-csv.cs writes) and set those parameter values back onto the model. |
| ✅ | `action-remove-parameter-value.cs` | Clear one named parameter's value across every element in `elements` — completes the Set/Copy pair already here (action-set-parameter-value.cs, action-copy-parameter-value.cs) with an... |
| ✅ | `action-rename-element.cs` | Rename each element in `elements` to `newName` via Element.Name (works for most nameable elements — views, sheets, levels, families/types, groups, materials — not for elements that don't... |
| ✅ | `action-rename-family.cs` | Bulk-rename the FAMILY behind each element in `elements` — not the instance. |
| ⛔ | `action-rename-phase.cs` | Rename one or more existing project Phases, old name -> new name. |
| ✅ | `action-rename-workset.cs` | Rename an existing user Workset — completes workset management alongside create-workset.cs / action-set-workset.cs / context-workset-info.cs. |
| ✅ | `action-renumber-sequential.cs` | Assign a sequential value to one parameter across `elements` — e.g. |
| ⚠️ | `action-report-global-parameters.cs` | List every GLOBAL PARAMETER in the project — name, value, whether it is driven by a formula, whether it is reporting, and WHAT IT DRIVES: the dimensions and element parameters labelled... |
| ✅ | `action-report-phases.cs` | List every project Phase in order — name, Element Id, and position. |
| ✅ | `action-set-design-option.cs` | Add every element in `elements` (from the Main Model) into a named Design Option — the write counterpart to filter-by-design-option.cs, which can find elements already IN an option but... |
| ✅ | `action-set-element-phase.cs` | Assign every element in `elements` to a named Phase — sets Phase Created and/or Phase Demolished (independently optional) via the BuiltInParameter.PHASE_CREATED/PHASE_DEMOLISHED... |
| ✅ | `action-set-parameter-value.cs` | Bulk-set one named parameter to one value across every element in `elements` — a generic version of the Flow-parameter-refresh / any other bulk parameter edit. |
| ⛔ | `action-set-workset.cs` | Assign every element in `elements` to a named user workset — the write counterpart to filter-by-workset.cs, which can find elements ON a workset but has no way to put them there. |

## actions/qa-checks  *(10)*

| | Fragment | What it does |
|---|---|---|
| ⚠️ | `action-audit-view-filters.cs` | Answer "the filter is on the view, so why is nothing coloured?" — reads every filter on a view and reports the four separate states that each have to be right before a filter draws... |
| ⚠️ | `action-check-open-pipe-ends.cs` | Find the pipes in `elements` that still have an OPEN END — a connector joined to nothing — and optionally cap them. |
| ✅ | `action-check-surface-fit.cs` | QA check before (or after) snapping elements to a surface — for each element, fire rays from its FOOTPRINT (centre + corners, or a 3x3 grid) toward a target and report whether the... |
| ⚠️ | `action-compare-models.cs` | Compare the OPEN model against another .rvt on disk — what was added, what was removed, and which parameters changed on the elements present in both. |
| ✅ | `action-find-blank-parameter.cs` | QA check — flag elements in `elements` where a named parameter is BLANK (no value, or an empty string) — the standards-compliance sweep "which of these are missing Mark/Comments/whatever". |
| ✅ | `action-find-duplicate-values.cs` | Flag elements that share the SAME value in a named parameter — e.g. |
| ✅ | `action-find-duplicates.cs` | Flag likely duplicate elements within `elements` — instances whose insertion points sit within a small tolerance of each other (e.g. |
| ⚠️ | `action-find-overlapping-lines.cs` | Find LINES DRAWN ON TOP OF EACH OTHER in `elements` — exact duplicates, and partly-overlapping collinear segments — and on request delete the redundant ones. |
| ✅ | `action-report-clashes.cs` | Basic clash/overlap report — real geometry intersection (not just bounding box, via Revit's own ElementIntersectsElementFilter) between every element in `elements` (set A) and every... |
| ⚠️ | `action-report-constraints.cs` | Answer "why won't this element move?" — list every dimension/alignment CONSTRAINT attached to each element in `elements`, say what it is locked to, and on request remove them. |

## actions/reporting  *(36)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-compare-elements.cs` | Side-by-side parameter comparison of the elements in `elements` (2 or more) — reports which parameters DIFFER between them (or every parameter with showAll). |
| ✅ | `action-count-and-report.cs` | Report on `elements` — bare count (default, per reply-style.md) or a size-breakdown table when asked for sizes. |
| ✅ | `action-count-by-group.cs` | Count `elements`, broken down by the actual value of ANY named parameter — "count by Level", "count by System Type", "count by Family", "count by Comments". |
| ✅ | `action-count-by-spatial-container.cs` | Count `elements`, broken down by which Room, MEP Space, or HVAC Zone physically contains each one. |
| ✅ | `action-plan-shortest-route.cs` | Work out the CHEAPEST WAY TO CONNECT a set of elements — "wire these 40 light fixtures using the least cable", "chain these terminals off that FCU", "which order do I run this loop in". |
| ⚠️ | `action-report-addin-data.cs` | What OTHER add-ins have written into this model, and onto which elements. |
| ⚠️ | `action-report-areas.cs` | Report AREAS — the Area-plan kind, not Rooms — broken down by Area Scheme, level and name, with totals. |
| ✅ | `action-report-bounding-box.cs` | Report each element's bounding box (min corner + size) in mm, plus the combined bounding box of the whole set. |
| ⚠️ | `action-report-ceiling-heights.cs` | The CLEAR HEIGHT under the ceiling in each room — which ceilings are over it, how high the underside of each one sits above the finished floor, and which is the main one. |
| ✅ | `action-report-compound-structure.cs` | Report the layer build-up (compound structure) of the wall/floor/roof/ceiling TYPES behind `elements` — function, material, thickness per layer, core boundaries, total thickness. |
| ⚠️ | `action-report-connector-loads.cs` | The ENGINEERING VALUES set on each connector — demand, assigned flow, K-factor, fixture units, loss coefficient, pressure drop, flow direction and loss method. |
| ✅ | `action-report-connectors.cs` | Report every MEP connector on the elements in `elements` — domain, shape, size (mm), origin (mm), facing direction (BasisZ), and what each is REALLY connected to. |
| ✅ | `action-report-coverage.cs` | How much floor does each element actually serve? Reports the coverage RADIUS and AREA per element plus min / max / average / total, optionally draws the coverage circles so gaps and... |
| ⚠️ | `action-report-curtain-elements.cs` | Break a curtain wall down into what it is actually made of — panels, mullions and grid lines — with type, size and area/length per piece, and a total per type. |
| ⚠️ | `action-report-door-room-links.cs` | Which room each door leads FROM and TO — verified against the geometry, not just read off Revit's own properties — and optionally written into parameters so a door schedule can show them. |
| ❓ | `action-report-duct-weight.cs` | SHEET-METAL TAKEOFF for the ducts in `elements` — gauge/thickness from the size band, the developed sheet area, and the fabrication weight in kg with allowances. |
| ⚠️ | `action-report-element-dependencies.cs` | For each element in `elements`, list everything that WOULD GO WITH IT — the tags, dimensions, hosted families, sketch lines and openings Revit would take away too, plus what it is joined... |
| ✅ | `action-report-element-ownership.cs` | Worksharing ownership report for `elements` — who created each element, who owns/borrows it right now, who changed it last (the Worksharing tooltip, in bulk). |
| ⚠️ | `action-report-external-references.cs` | Every FILE this model depends on, in one list — RVT links, CAD links AND imports, point clouds, keynote tables, decal images, IFC links. |
| ⚠️ | `action-report-geometry-complexity.cs` | Find the families that are making the model SLOW — how much actual geometry each family type carries, measured as triangle count, at each detail level. |
| ✅ | `action-report-length-by-size.cs` | Report count AND total length per size group for linear MEP elements (ducts, pipes, cable trays — anything with a "Size" string parameter and a Length parameter). |
| ✅ | `action-report-location.cs` | Report each element's position — a point for point-based elements (equipment, terminals, rooms), or the endpoints for line-based elements (ducts, pipes, walls). |
| ✅ | `action-report-material-takeoff.cs` | Sum material area/volume across every element in `elements`, grouped by material name — a quantities/takeoff report. |
| ⚠️ | `action-report-mep-pressure-drop.cs` | Revit's OWN calculated pressure loss through each duct or pipe system, section by section — flow, velocity, pressure drop, friction, and WHICH SECTIONS ARE ON THE CRITICAL PATH. |
| ✅ | `action-report-nearest-elements.cs` | For each element in `elements`, find the NEAREST element(s) from a target set — a category (same or different), several categories, or a fixed list of Ids. |
| ✅ | `action-report-parameter-inventory.cs` | Discover every parameter an element ACTUALLY HAS — name, storage type, Instance vs Type, kind (Built-in/System, Shared, or Project/Family — see the honesty note below), parameter group,... |
| ✅ | `action-report-parameters.cs` | Report selected parameter values for `elements` as a small table. |
| ✅ | `action-report-ray-hits.cs` | Fire rays out of each element in `elements` and report WHAT EACH RAY HITS — direction, the hit element (name/Id/category), and the distance in mm. |
| ✅ | `action-report-room-boundaries.cs` | Report each Room/Space's boundary loops as mm segments — start/end coordinates + the wall (or other element) that generates each segment. |
| ⚠️ | `action-report-room-dimensions.cs` | Each room's WIDTH x LENGTH measured on the ROOM'S OWN AXES — not on project north — plus how far the room is rotated, its area, and how much bigger its project-aligned bounding box is... |
| ✅ | `action-report-room-space-data.cs` | Area/Volume/Level table for every Room or Space in `elements` — read-only, no transaction. |
| ⚠️ | `action-report-routing-preferences.cs` | Read the ROUTING PREFERENCES table off a Pipe Type or Duct Type — which elbow, tee, cross, transition, union and cap Revit will insert, and at which sizes. |
| ✅ | `action-report-space-airflow.cs` | Schedule-style report of MEP Spaces — Number, Name, Level, Area, Volume, and the Design vs Actual Supply/Return/Exhaust airflows, in metric. |
| ⚠️ | `action-report-tags-and-targets.cs` | Every tag in a view and WHAT IT IS ACTUALLY POINTING AT — including targets that live inside a LINKED model, and tags that point at nothing at all. |
| ⚠️ | `action-report-views-showing-element.cs` | For each element in `elements`, list every VIEW it is visible in and every SHEET those views are on — "which drawings show this valve", "if I move this, what has to be re-checked",... |
| ❓ | `action-test-view-filter-match.cs` | Dry-run an existing View Filter against `elements` and report, per element, whether it MATCHES, does not match the rule, or is N/A because the element's category was never in the... |

## actions/selection  *(1)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-select-elements.cs` | Set, ADD TO, or REMOVE FROM the active Revit selection using `elements` — so after the script finishes, the user sees exactly the intended set highlighted in the Revit UI. |

## actions/sheet-dates-revisions  *(6)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-assign-revisions-by-sheet-date.cs` | For every sheet in `elements` (from filter-by-sheets.cs), re-scan its TextNotes for date-like text, and attach (via ViewSheet.SetAdditionalRevisionIds — NOT a cloud) every project... |
| ✅ | `action-delete-revision.cs` | Permanently delete one or more project Revisions by SequenceNumber — completes the revision Create/Edit/Delete lifecycle. |
| ✅ | `action-edit-revision.cs` | Update one existing project Revision's fields — description, date, issued by/to, the issued flag, and/or cloud/tag visibility. |
| ✅ | `action-extract-dates-from-textnotes.cs` | Read every TextNote placed on each sheet in `elements` (from filter-by-sheets.cs), find date-like text (formats like "22-JUL-2025", "22 Jul 2025", "22/Jul/2025" — day, 3+ letter month,... |
| ✅ | `action-remove-revision-from-sheet.cs` | Detach one or more named Revisions from every sheet in `elements` (from filter-by-sheets.cs) — the reverse of action-assign-revisions-by-sheet-date.cs, which only ever adds. |
| ✅ | `action-report-revisions.cs` | List every project Revision in order — sequence number, date, description, issued by/to, issued flag, visibility. |

## actions/sheets-views  *(51)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-add-aligned-dimensions.cs` | Dimension between the elements in `elements` — one aligned dimension string through all of them along a chosen axis. |
| ✅ | `action-add-schedule-calculated-field.cs` | Add a COMBINED PARAMETER field (several existing fields merged into one column with prefix/suffix/separator per part) to every ViewSchedule in `elements`. |
| ✅ | `action-add-schedule-field.cs` | Add one or more columns/fields to every ViewSchedule in `elements` — same GetSchedulableFields()/AddField() call creators/create-schedule.cs uses, split out so it can be run again later... |
| ✅ | `action-add-spot-elevations.cs` | Place a Spot Elevation annotation on each element in `elements` in one view — the "annotate the levels of these" job, same annotation family as action-tag-elements.cs but reporting... |
| ⚠️ | `action-align-viewports-across-sheets.cs` | Make the plan sit in exactly the SAME position on every sheet — pick one sheet as the master and every other sheet's viewport moves to match it. |
| ✅ | `action-apply-view-template.cs` | Apply an existing View Template (by name) to one or more views — bundles ALL of a view's graphic/visibility settings (V/G overrides, View Filters, Category overrides, Phase, Detail... |
| ⚠️ | `action-arrange-tags-to-view-edges.cs` | Clear the middle of a congested view by parking every tag in a neat column down the LEFT and RIGHT edges of the crop, each still leadered back to its own element, with the leaders fanned... |
| ✅ | `action-center-room-tags.cs` | Move each ROOM TAG in `elements` so its head sits on the CENTRE of the room it tags — the tidy-up after tags have been dragged about, or after rooms were re-shaped and the tags stayed put. |
| ⚠️ | `action-create-assembly-views.cs` | Turn each ASSEMBLY in `elements` into a set of shop-drawing views in one go — a 3D orthographic, detail sections from the sides/top/front you ask for, a part list, a material takeoff,... |
| ✅ | `action-create-view-template-from-view.cs` | Save a fully-configured view's current settings (V/G overrides, Filters, Category overrides, Phase, Detail Level, Scale, Discipline, ...) as a brand new named View Template — Revit's own... |
| ❓ | `action-dimension-mep-runs.cs` | Dimension between DUCTS, PIPES, CONDUIT and CABLE TRAYS in one view — the spacing string across a corridor of services. |
| ✅ | `action-dimension-rooms.cs` | Put real Revit dimensions on each ROOM in `elements` — its overall width and depth, measured wall face to wall face, drawn just outside the room. |
| ❓ | `action-dimension-wall-openings.cs` | Dimension ALONG each wall in `elements`, picking up every DOOR and WINDOW opening in it — a running string (wall end -> jamb -> jamb -> wall end) plus a second OVERALL dimension outside it. |
| ✅ | `action-duplicate-sheet.cs` | Duplicate each ViewSheet in `elements` — new sheet with the same title block, plus (on request) duplicates of every placed view dropped at the SAME viewport positions, and schedules... |
| ✅ | `action-duplicate-view-template.cs` | Duplicate an existing View Template (by name) into a new, separately-named template with the same settings — a starting point for a variant without hand-rebuilding it. |
| ✅ | `action-duplicate-views.cs` | Duplicate each view in `elements` — plain duplicate, duplicate-with-detailing (keeps annotations/details), or as a dependent view (stays linked to the parent's crop/changes). |
| ⚠️ | `action-export-3d-to-fbx.cs` | Export a 3D view to FBX — the handover format for Navisworks, 3ds Max, Twinmotion and any game engine — and, optionally, build a clean 3D view to export FROM rather than shipping... |
| ⚠️ | `action-export-families.cs` | Pull every loadable family OUT of the open project and save each one as its own .rfa in a folder. |
| ⚠️ | `action-export-ifc.cs` | Export the model to an IFC file — File > Export > IFC, scripted. |
| ⚠️ | `action-export-nwc.cs` | Export the model to a Navisworks NWC file — the coordination handoff format. |
| ⚠️ | `action-export-parameters-to-csv.cs` | Write chosen parameter values of `elements` to a CSV file — ElementId first column always, one row per element. |
| ✅ | `action-export-schedule-to-csv.cs` | Export every ViewSchedule in `elements` to a CSV file via Revit's own native ViewSchedule.Export/ScheduleExportOptions — the same mechanism as File > Export > Reports > Schedule, scripted. |
| ❓ | `action-export-sheets-to-pdf.cs` | Batch-export every ViewSheet in `elements` to PDF — combined into one PDF (default) or one PDF per sheet. |
| ⚠️ | `action-export-view-image.cs` | Export each View/ViewSheet in `elements` as a PNG image — the "screenshot this view properly" job, at a chosen pixel width instead of whatever the screen shows. |
| ⚠️ | `action-export-views-to-dwg.cs` | Export each View/ViewSheet in `elements` to its own DWG file — Revit's File > Export > CAD Formats > DWG, scripted. |
| ❓ | `action-force-tag-leader-lshape.cs` | Force every TAG in `elements` to draw its leader as an L-SHAPE (a bent leader with a horizontal last leg into the tag) instead of a straight diagonal — the drafting standard on most... |
| ✅ | `action-manage-sheet-sets.cs` | Manage named Sheet/View Sets (`ViewSheetSet` — the saved sets in Print/Export dialogs). |
| ❓ | `action-place-flow-arrows.cs` | Place FLOW-DIRECTION ARROW annotations along the ducts in `elements`, at a spacing, pointing the way the air actually goes — the "show the flow direction on the drawing" job. |
| ✅ | `action-place-schedule-on-sheet.cs` | Place each schedule in `elements` onto one target sheet. |
| ✅ | `action-place-viewport-on-sheet.cs` | Place each view in `elements` onto one target sheet as a Viewport, at a given point (or centered on the sheet's title block if no point given). |
| ⚠️ | `action-place-views-on-new-sheets.cs` | Give every view in `elements` its OWN new sheet — create the sheet, number it from a running series, name it after the view, and drop the view on it. |
| ✅ | `action-remove-schedule-field.cs` | Remove one or more columns/fields (matched by current column heading / field name) from every ViewSchedule in `elements` — the paired undo for action-add-schedule-field.cs. |
| ✅ | `action-remove-tags.cs` | Delete every IndependentTag element in `elements` — the paired undo for action-tag-elements.cs (and a cleanup for tags placed by the scored recipe). |
| ✅ | `action-remove-view-template.cs` | Detach the View Template from one or more views (sets View.ViewTemplateId back to InvalidElementId) — the paired "undo" for action-apply-view-template.cs. |
| ✅ | `action-report-schedule-definition.cs` | Read back HOW a schedule is built, not what is in it — the category it collects (including the -1 "<Multi-Category>" case), its filter rules with the category/element IDs resolved to... |
| ✅ | `action-report-schedule-fields.cs` | List every field/column on each ViewSchedule in `elements`, IN ORDER — position, field name, column heading (if overridden), field type... |
| ✅ | `action-report-sheet-title-blocks.cs` | Report which title block (Family + Type) is currently placed on each ViewSheet in `elements` — "what title block is on this sheet" / "what title blocks do we have across all sheets". |
| ⚠️ | `action-report-view-references.cs` | For each section mark, callout bubble, elevation mark or reference view in `elements`, report WHICH VIEW IT POINTS AT — and optionally re-point it at a different one. |
| ✅ | `action-report-view-template-status.cs` | Report whether one or more views currently have a View Template applied — which template, and which parameters (if any) are excluded from its control on that view. |
| ❓ | `action-revision-cloud-around-elements.cs` | Draw revision clouds AROUND the elements in `elements` — "cloud what changed", where the cloud comes from the model rather than from you typing corner coordinates. |
| ✅ | `action-set-print-settings.cs` | Configure print settings — paper size (by name), orientation, zoom-to-fit — and save them as a named Print Setting. |
| ✅ | `action-set-schedule-appearance.cs` | Set the two schedule-level appearance options this fragment has solid API confidence on — "Itemize every instance" and the Grand Total row — across every ViewSchedule in `elements`. |
| ✅ | `action-set-schedule-field-format.cs` | Format ONE existing column (matched by current field name) on every ViewSchedule in `elements` — override heading text, hide/show it, horizontal alignment, and/or sheet column width. |
| ✅ | `action-set-schedule-filters.cs` | Replace ALL filter rules on every ViewSchedule in `elements` with the ones given here — clears whatever filters already existed first, then adds the new list. |
| ✅ | `action-set-schedule-sort-group.cs` | Replace ALL sort/group fields on every ViewSchedule in `elements` with the ones given here, in order — clears whatever sort/group setup already existed first, then adds the new list... |
| ✅ | `action-set-sheet-title-block.cs` | Change the title block on each ViewSheet in `elements` to a different named Type — "update the title block on this sheet to X" / bulk-swap every sheet's title block at once. |
| ✅ | `action-set-view-properties.cs` | Batch-set Scale, Detail Level, Visual Style (Display Style), and/or Phase/Phase Filter across every View in `elements` — the lightweight direct version of what applying a View Template... |
| ✅ | `action-set-view-template-controlled-params.cs` | Set which parameters a View Template controls vs. |
| ❓ | `action-stack-tags.cs` | Arrange the TAGS in `elements` into a neat VERTICAL STACK at one point — the tidy column of tags at the side of a busy area, each still leadered back to its own element. |
| ✅ | `action-tag-elements.cs` | Tag every element in `elements` in one given view — simple placement (each tag head offset from the element's own point/curve-midpoint by a fixed vector, straight leader optional), NOT... |
| ❓ | `action-transfer-views-between-documents.cs` | Copy SCHEDULES, LEGENDS, DRAFTING VIEWS or VIEW TEMPLATES from another open Revit document into this one — "bring the standard schedules over from the template project", "copy the... |

## actions/structural-changes  *(28)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-add-remove-insulation.cs` | Add or remove insulation (or duct lining) on the ducts/pipes in `elements` — the WRITE counterpart to filter-by-insulation-status.cs / filter-by-insulation-type.cs, which can only find it. |
| ⚠️ | `action-batch-upgrade-revit-files.cs` | Upgrade a WHOLE FOLDER of families and templates to the Revit version that is currently running — open each file, save a copy into a destination folder, close it, and keep going when one... |
| ✅ | `action-change-element-type.cs` | Bulk-swap every element in `elements` from its current type to a different named type within the SAME family — e.g. |
| ⚠️ | `action-change-wall-constraints.cs` | Move the walls in `elements` onto a DIFFERENT base and/or top Level — WITHOUT the walls themselves moving or changing height. |
| ⚠️ | `action-connect-air-terminals.cs` | Connect the air terminals in `elements` to the duct running past them — Revit cuts the tap into the duct and makes the connection itself. |
| ⚠️ | `action-convert-cad-to-directshape.cs` | Turn an imported CAD solid into REAL Revit elements — one DirectShape per solid, in a category you choose. |
| ⚠️ | `action-copy-from-link.cs` | Copy elements FROM a linked RVT model INTO this host model, placed at their true linked position (the link's full transform applied) — "bring those walls/fixtures from the arch link into... |
| ❓ | `action-create-from-room-boundaries.cs` | Build a Floor, a Ceiling, a Filled Region or detail lines ON each Room/Space in `elements`, taking the shape from the room's OWN boundary — "put a ceiling in every room", "give me a slab... |
| ✅ | `action-delete-elements.cs` | Permanently delete every element in `elements`. |
| ⚠️ | `action-disallow-join.cs` | Turn OFF automatic end-joining on the walls and structural framing in `elements` — the fix for walls that clean up into each other where they should read as separate, and for beams whose... |
| ✅ | `action-duplicate-type.cs` | Duplicate the TYPE(s) behind `elements` into new, separately-named type(s) — e.g. |
| ⚠️ | `action-extract-cad-curves.cs` | Trace a linked/imported CAD file into real Revit lines — read the curves on chosen DWG layer(s) from the ImportInstances in `elements` and recreate them as Model Lines or Detail Lines. |
| ✅ | `action-group-elements.cs` | Bundle every element in `elements` into a new Model Group — e.g. |
| ✅ | `action-join-geometry.cs` | Join (or unjoin) the geometry of every element in `elements` with ONE specific target element — e.g. |
| ✅ | `action-maximize-datum-extents.cs` | Make the GRIDS and LEVELS in `elements` span the WHOLE model — "the grids only go half way", "maximize the grids to the entire model", "some grids are short and they all end in a... |
| ❓ | `action-place-accessory-on-run.cs` | Insert a duct/pipe ACCESSORY (VCD, fire damper, valve, strainer...) INTO each existing run in `elements` — the run is cut in two and both cut ends are connected to the accessory, so the... |
| ❓ | `action-purge-unplaced-views.cs` | Find (and on request DELETE) views that are not on any sheet — 3D views, sections, schedules, legends and drafting views. |
| ✅ | `action-purge-unused-families.cs` | Find (and on request delete) loadable FAMILY TYPES with zero placed instances, and whole families where every type is unused — the biggest file-size win in native Purge Unused, which... |
| ✅ | `action-purge-unused.cs` | Delete unused View Templates, unused View/Selection Filters, or unused Materials — the subset of Revit's native "Purge Unused" that's actually provably correct from the PUBLIC API (each... |
| ❓ | `action-reassign-level.cs` | Re-point every element in `elements` to a DIFFERENT reference level WITHOUT MOVING IT — the fix for "these ducts are modelled on Level 1 but they belong to Level 2", where the schedule... |
| ⛔ | `action-reload-links.cs` | Reload every distinct RVT link TYPE behind the link instance(s) in `elements` (from filter-by-links.cs) — pulls in the latest saved version of a coordination link from disk, Revit's own... |
| ⚠️ | `action-replace-material.cs` | Swap one material for another EVERYWHERE it is used by the elements in `elements` — inside the layers of walls/floors/roofs/ceilings, and on the plain material parameters of family... |
| ❓ | `action-reset-datum-extents.cs` | Put the GRIDS and LEVELS in `elements` back on their shared 3D (Model) extent, discarding the per-view 2D override — the fix for "someone dragged a grid end and now it's short in this... |
| ❓ | `action-set-datum-bubbles.cs` | Show, hide or FLIP the bubble (the head with the grid/level name in it) on the GRIDS and LEVELS in `elements`, in one view — "put the grid bubbles on the other side", "turn the bubbles... |
| ✅ | `action-split-elements.cs` | Split each Duct or Pipe in `elements` into two elements at one point along its own length — the "AutoCAD Break" operation, generalized from recipes/split-duct-near-equipment.cs's... |
| ✅ | `action-ungroup-elements.cs` | Dissolve every Group instance in `elements` back into its individual members — the paired undo for action-group-elements.cs. |
| ⛔ | `action-unload-remove-links.cs` | Unload (keep the link, drop it from memory/view) or REMOVE (delete from the project entirely) the distinct RVT link TYPE(s) behind the link instances in `elements` — the two Manage Links... |
| ⛔ | `action-update-scope-box.cs` | Resize an existing Scope Box (by name) to new mm box corners. |

## actions/visibility  *(17)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `action-assign-scope-box-to-view.cs` | Assign a named Scope Box to every View in `elements` (the view's own "Scope Box" property) — leave scopeBoxName empty to clear the assignment instead. |
| ✅ | `action-hide-elements.cs` | Hide exactly `elements` in the active view (the opposite of isolate — everything else stays visible, these disappear). |
| ✅ | `action-isolate-elements.cs` | Temporary-isolate exactly `elements` in the active view, resetting any prior isolation first so it's never additive to a stale state. |
| ❓ | `action-manage-named-set.cs` | Name a set of elements once, then re-select / isolate / hide / show it by that name later — the "park the beams while I run the ducts, then bring them back" job. |
| ✅ | `action-report-category-visibility.cs` | Report which categories are currently OFF (category-level, via View.GetCategoryHidden) in a view — the reverse lookup for action-set-category-visibility.cs, which only ever SETS. |
| ✅ | `action-section-box-and-zoom.cs` | Build a bounding box around every element in `elements`, apply it as a 3D view's section box (finds/creates a usable default 3D view if the active view isn't one), and zoom/show the... |
| ✅ | `action-set-category-visibility.cs` | Turn one or more ENTIRE categories on/off in a view — the Visibility checkbox column in Visibility/Graphics > Model Categories, NOT a per-element hide. |
| ✅ | `action-set-crop-box-settings.cs` | Turn Crop Region on/off, its boundary line visibility on/off, and/or Annotation Crop on/off across every View in `elements` — independent flag toggles, NOT resizing (for resizing/fitting... |
| ✅ | `action-set-pin-state.cs` | Pin or unpin every element in `elements`. Generic live-script version of AJ Tools' Pin Elements operation, but driven by any reusable filter. |
| ❓ | `action-set-section-mark-visibility.cs` | Show only the SECTION MARKS whose section view is actually ON A SHEET, and hide the rest — the pre-issue tidy-up so a plan is not covered in markers pointing at working sections nobody... |
| ⚠️ | `action-set-view-crop-to-shape.cs` | Give a view a NON-RECTANGULAR crop — an L-shape round a wing, a stepped boundary along a match line, the outline of a room or a zone. |
| ✅ | `action-set-view-crop.cs` | Set a view's crop region to fit `elements`' combined extent plus a margin, turning crop on if it's off. |
| ✅ | `action-set-view-range.cs` | Read or set a plan view's View Range — cut plane, top, bottom and view depth, each as a level plus an mm offset. |
| ⚠️ | `action-set-view-underlay.cs` | Set (or clear) the UNDERLAY on plan views — the ghosted level shown behind the one you are working on. |
| ⛔ | `action-set-view-workset-visibility.cs` | Make one view show ONLY the named workset(s) — the "Workset 3D View" pattern (this project's own AJ Tools "Workset 3D Views" convention, scripted): every OTHER user workset gets turned... |
| ✅ | `action-show-elements.cs` | Zoom/show the current `elements` in Revit, optionally also making them the active selection. |
| ✅ | `action-unhide-elements.cs` | Reverse a PERMANENT per-element hide (View.HideElements) on every element in `elements` — distinct from resetting temporary isolate/hide, which is a separate view-mode toggle, not an... |

## commands  *(8)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `command-activate-view.cs` | Switch Revit's active view to a given View/ViewSheet — useful before a screenshot, before a view-scoped filter (filter-by-elements-in-view.cs, action-tag-elements.cs) that's meant to run... |
| ✅ | `command-clear-selection.cs` | Clear the active Revit selection — the reverse of action-select-elements.cs's "set" mode with an empty list, split out as its own one-line command since clearing comes up on its own... |
| ⛔ | `command-compact-save.cs` | Save the active document with Compact = true — Revit's "Compact File" checkbox, which rewrites the file without the accumulated dead space a normal incremental save leaves behind. |
| ✅ | `command-regenerate.cs` | Force Document.Regenerate() — useful after a composed script chains several actions where a later one depends on geometry/parameters the earlier one just changed (new elements' real... |
| ⛔ | `command-sync-with-central.cs` | Synchronize a workshared local with its central model and relinquish everything — Revit's "Synchronize with Central" + "Relinquish all mine", scripted. |
| ✅ | `command-zoom-to-fit.cs` | Zoom the active view's open UI window to fit its current content — useful right after an isolate/section-box/crop action so the next screenshot actually shows the result instead of... |
| ✅ | `native-undo.cs` | Revert the last transaction using Revit's own native Undo command — never hand-write a delete/fix script for a flagged "mistake". |
| ✅ | `unhide-all-active-view.cs` | Restore permanently hidden elements in the active view and clear Temporary Hide/Isolate. |

## context  *(12)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `context-active-view.cs` | Quick session snapshot — the active MODEL (Revit version, document title, family vs project, worksharing, all open documents) and the active VIEW (name, type, scale, associated level,... |
| ✅ | `context-all-warnings.cs` | List every warning currently in the model — severity, description, failing element Ids, and the failure GUID. |
| ✅ | `context-design-options.cs` | List every Design Option in the document — name, Id, and whether it's the Primary option in its set. |
| ✅ | `context-levels-and-grids.cs` | Quick list of every Level (name + elevation, ordered bottom-up) and every Grid (name) in the document. |
| ✅ | `context-linked-models.cs` | List every RVT link in the document — name, loaded/unloaded/not-found status, pinned, and workset (if workshared). |
| ✅ | `context-model-categories.cs` | List model categories (the OST_ ones elements actually belong to), optionally narrowed by a keyword. |
| ✅ | `context-project-units.cs` | Report every unit spec that's actually valid for this document's discipline (Length, Area, HVAC Airflow, Piping Flow, etc.) and what display unit each is currently set to — e.g. |
| ✅ | `context-session-start.cs` | THE opening call. One bridge round-trip that answers everything a session needs to know before it touches the model: which Revit, which API generation, which document, what units the... |
| ✅ | `context-shared-coordinates.cs` | Report the document's coordinate setup — Project Base Point, Survey Point, active Project Location name, and the True North rotation. |
| ✅ | `context-used-families.cs` | List every loadable family in the model (component families brought in from .rfa files) — NOT system families like Wall/Floor/Roof types, and not in-place families. |
| ✅ | `context-workset-info.cs` | Report whether the document is workshared, and if so, list every user workset with its open/closed state and owner. |
| ❓ | `harvest-revit-api.cs` | Dump the ENTIRE Revit API of the running Revit to disk, as a corpus for the SEPARATE API index in `api-index/`. |

## creators  *(36)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `create-cable-tray.cs` | Draw ONE straight cable tray between two mm points — electrical containment twin of create-duct.cs. |
| ✅ | `create-callout-view.cs` | Create a Callout view inside a parent view over an mm rectangle — the last of the common view types create-view.cs excludes (elevations are create-room-elevations.cs). |
| ⛔ | `create-ceiling.cs` | Create a Ceiling from a closed boundary of mm plan points on a given Level. |
| ✅ | `create-conduit.cs` | Draw ONE straight conduit between two mm points — the round electrical containment twin of create-cable-tray.cs. |
| ✅ | `create-dimension.cs` | Create one dimension string across 2+ Grids and/or Levels, in a given view. |
| ✅ | `create-duct.cs` | Draw ONE straight duct between two mm points — the plain, general-purpose version of what the HVAC recipes do inside their multi-stage builds. |
| ✅ | `create-filled-region.cs` | Create a Filled Region (a filled/hatched polygon annotation) in a view from a closed loop of mm points. |
| ✅ | `create-floor.cs` | Create one flat Floor from a closed boundary of mm plan points on a given Level — the basic slab case (no slope arrows, no openings, no shape editing). |
| ⚠️ | `create-grid-series.cs` | Set out a WHOLE STRUCTURAL GRID from a start point and a list of bay spacings — both directions, named automatically A/B/C and 1/2/3, in one pass. |
| ✅ | `create-grid.cs` | Create one or more straight Grid lines from mm endpoint pairs. |
| ✅ | `create-hvac-zone.cs` | Create an HVAC Zone on a Level and add existing MEP Spaces to it — the grouping layer above Spaces that create-space.cs / set-space-airflow.cs stop at. |
| ✅ | `create-key-schedule.cs` | Create a KEY schedule for a category — the lookup table where you define a few named keys (e.g. |
| ✅ | `create-legend-view.cs` | Create a new Legend view by DUPLICATING an existing one — the only route the Revit API offers. |
| ✅ | `create-levels.cs` | Batch-create Levels, either at even spacing from a start elevation, or at an explicit list of elevations. |
| ✅ | `create-line.cs` | Create one or more plain Model Lines or Detail Lines between mm point pairs — the standalone version of the line-creation technique action-fillet-elements.cs (mode="arc") already uses... |
| ✅ | `create-material.cs` | Create one or more new Materials, setting color and transparency. |
| ✅ | `create-mep-system-type.cs` | Create a new MEP SYSTEM TYPE (duct or pipe) by duplicating an existing one, then set its name, abbreviation and graphic colour — how a project gets "Supply Air - Zone 1", "CHWS", "CHWR"... |
| ✅ | `create-pipe.cs` | Draw ONE straight pipe between two mm points — Plumbing twin of create-duct.cs. |
| ✅ | `create-point-based-element.cs` | Place a family instance (door, window, piece of equipment, furniture, anything point-placed) at one or more points on a level. |
| ✅ | `create-revision-cloud.cs` | Draw a rectangular Revision Cloud in a view (or on a sheet) tied to an existing project Revision — the annotation half the Revision lifecycle (create/edit/delete/assign) was missing. |
| ✅ | `create-revision.cs` | Create one or more project-level Revisions (Manage > Revisions) directly, in the order given — the plain, non-date-scanning version of recipes/create-revisions-from-sheet-dates.cs, for... |
| ✅ | `create-room-elevations.cs` | Place an ElevationMarker at a room's center (or an explicit mm point) in a plan view and create 1–4 interior elevation views around it — the "room elevations" job create-view.cs... |
| ✅ | `create-room.cs` | Place a Room at one or more points on a level. |
| ✅ | `create-rooms-in-enclosed-regions.cs` | Fill every enclosed region on a level with a Room, in one pass — and REUSE the project's existing UNPLACED rooms before creating new ones, so you don't end up with orphaned "Room 1"... |
| ✅ | `create-schedule.cs` | Create one new schedule (ViewSchedule) for a category, with a chosen set of fields/columns. |
| ⛔ | `create-scope-box.cs` | Create one or more Scope Boxes from mm box corners. |
| ⚠️ | `create-section-at-element.cs` | Cut a SECTION VIEW through each element in `elements`, aimed at it and sized around it — "give me a section at every FCU", "section through each of these walls", "I need a cut at every... |
| ✅ | `create-sheet-list.cs` | Create a Sheet List (drawing index) schedule — the table of every sheet in the project, which goes on the cover sheet. |
| ✅ | `create-sheet.cs` | Create one or more new sheets with a chosen title block, setting sheet number and name. |
| ✅ | `create-space.cs` | Place an MEP Space at one or more points on a level — the Space-category equivalent of create-room.cs (Space is a separate element type from Room even when it covers the same area;... |
| ✅ | `create-text-note.cs` | Place one or more Text Notes at given points in a view. |
| ✅ | `create-view.cs` | Create a new View — Floor Plan (at a given Level), 3D (isometric), or Section (through a given mm box). |
| ✅ | `create-wall.cs` | Create one straight Wall between two mm plan points on a Level with a given height — the basic line-based case (no profiles, no arcs, no openings). |
| ❓ | `create-workset-3d-views.cs` | Create one isometric 3D view PER USER WORKSET, each showing only its own workset and hiding all the others — the coordination set-up job: "give me a 3D view for each workset so I can see... |
| ⛔ | `create-workset.cs` | Create one or more new user Worksets — feeds action-set-workset.cs (assign elements onto a workset that didn't exist yet). |
| ✅ | `load-family.cs` | Load one or more .rfa family files from disk into the project — File > Load Family, scripted. |

## examples  *(3)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `color-isolate-select-by-size.cs` | (no PURPOSE line) |
| ✅ | `prelude-smoke-test.cs` | Verify every helper in ../lib/prelude.cs in ONE bridge call. |
| ❓ | `purge-unused-view-templates.cs` | (no PURPOSE line) |

## filters/by-identity  *(12)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-category-and-family.cs` | One category, narrowed to instances whose family name contains a string. |
| ✅ | `filter-by-category-name.cs` | Every instance of a category, resolved by its plain display NAME ("Ducts", "Duct Accessories", "Mechanical Equipment") instead of requiring the exact BuiltInCategory enum member — more... |
| ✅ | `filter-by-category.cs` | Every instance of one category, optionally scoped to a level. |
| ✅ | `filter-by-family-type.cs` | A specific Type (FamilySymbol) inside a Family, matched by name — e.g. |
| ✅ | `filter-by-family.cs` | Every instance belonging to a specific Family name, scanned across the WHOLE model — no category picked first. |
| ✅ | `filter-by-grid.cs` | Every Grid in the model, optionally narrowed by a name substring — feeds creators/create-dimension.cs (a dimension string across named Grids) or any report/rename action that needs the... |
| ✅ | `filter-by-id-list.cs` | Look up a specific list of Element Ids the user already has (pasted from a warning, read off a tag, remembered from an earlier answer) and produce them as `elements` — for "what is this... |
| ✅ | `filter-by-levels.cs` | Every Level element in the model ITSELF, optionally narrowed by a name substring — NOT the same job as filter-by-elements-on-level.cs, which finds elements SITTING ON a given level. |
| ✅ | `filter-by-material.cs` | Elements that use a specific Revit Material — compound-layer/structural material by default; set includePaint = true to also catch paint overrides. |
| ✅ | `filter-by-multiple-categories.cs` | Collect instances from several categories into one reusable element set. |
| ✅ | `filter-by-types.cs` | The TYPE elements themselves (FamilySymbol, or a system-family type like DuctType/PipeType/ WallType), matched by family name and/or type name — NOT the placed instances. |
| ❓ | `filter-by-wrong-category.cs` | Find elements that ARE one thing but were MODELLED as another — the family name, type name or equipment-tag prefix says "louvre" / "damper" / "fan", but the element sits in the wrong... |

## filters/by-location  *(7)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-element-intersection.cs` | Elements physically touching/clashing a specific target element — real geometric solid intersection (Revit's own ElementIntersectsElementFilter), not just an overlapping bounding box... |
| ✅ | `filter-by-elements-on-level.cs` | Everything on a given Level, across the WHOLE model — no category picked first. |
| ✅ | `filter-by-region.cs` | Every instance of a category whose bounding box intersects a given 3D region — for "elements in this area" when there's no Room to filter by (or the area spans multiple rooms/outdoors). |
| ✅ | `filter-by-room.cs` | One category, narrowed to instances physically inside a given room. |
| ✅ | `filter-by-solid-intersection.cs` | Elements whose real geometry (not just its bounding box) intersects a custom 3D solid — a clearance zone, an equipment-access envelope, a maintenance zone. |
| ✅ | `filter-by-space.cs` | One category, narrowed to instances physically inside a given MEP Space. |
| ✅ | `filter-by-unenclosed-spatial-elements.cs` | QA sweep — every Room and/or Space in the model that came out unbounded (zero Area, "Not Enclosed") — the systematic version of the single-creation-time warning creators/create-room.cs... |

## filters/by-property  *(5)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-category-and-numeric-param.cs` | One category, narrowed to instances where a numeric parameter matches a comparison against an mm value. |
| ✅ | `filter-by-length.cs` | Category narrowed by LENGTH (mm) vs. a comparison — the dedicated, discoverable version of what filter-by-category-and-numeric-param.cs can already do with parameterName = "Length", but... |
| ✅ | `filter-by-parameter-exists.cs` | Elements that HAVE a specific parameter attached, whether or not it holds a value — a QA sweep ("find everything missing the Fire Rating parameter"). |
| ✅ | `filter-by-parameter-text.cs` | Collect elements whose instance/type/family text matches a requested value. |
| ✅ | `filter-by-size.cs` | Category narrowed by SIZE, handling round (Diameter) and rectangular (Width x Height) MEP sizing together in one pass — "give me the ø150 OR 300x200 ones" without knowing ahead of time... |

## filters/by-relationship  *(13)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-assembly.cs` | Every member element of a specific Revit Assembly (AssemblyInstance) — for prefabrication / assembly-based workflows. |
| ✅ | `filter-by-connection-status.cs` | Category elements that HAVE at least one open (unconnected) Connector, or are FULLY connected — MEP QA sweep ("find loose pipe/duct ends"). |
| ✅ | `filter-by-electrical-system.cs` | Every element belonging to a specific Electrical System (circuit) — matched by its Circuit Type (Power/Lighting/Data/etc.) and/or its own circuit name. |
| ✅ | `filter-by-group.cs` | Every member element of a specific Model Group instance — for "everything inside this group", e.g. |
| ✅ | `filter-by-host.cs` | Elements hosted on a specific parent — a true Revit Host relationship, not just physical proximity. |
| ✅ | `filter-by-insulation-status.cs` | Pipe/duct/fitting elements that HAVE insulation (and/or lining) applied, or DON'T — QA sweep ("which ducts still need insulation"). |
| ✅ | `filter-by-insulation-type.cs` | The insulation/lining elements THEMSELVES (Duct Insulation, Duct Lining, Pipe Insulation — all covered, not just Lining), narrowed by Type name, Material, and/or thickness (size in mm). |
| ✅ | `filter-by-linked-model-elements.cs` | Elements of one category INSIDE a specific linked RVT model (not the link instance itself — use filter-by-links.cs for that) — "how many beams are in the structural link", "what ducts... |
| ✅ | `filter-by-links.cs` | Every linked model instance in the document — RVT links (RevitLinkInstance) and/or CAD links (ImportInstance: DWG/DXF/etc.), optionally narrowed by a name substring. |
| ⚠️ | `filter-by-openings.cs` | Every OPENING cut through the building — shafts, floor openings, wall openings, roof openings — as an actionable `elements` set. |
| ⚠️ | `filter-by-subcomponents.cs` | Produce the NESTED sub-components of one or more parent FamilyInstances (shared nested families inside MEP equipment, multi-part fixtures...) — the members a category filter never finds... |
| ✅ | `filter-by-system-name.cs` | Every pipe/fitting/duct/duct-fitting whose MEP System NAME (one specific System instance's own name, e.g. |
| ✅ | `filter-by-system-type.cs` | Every pipe/fitting/duct/duct-fitting whose MEP System TYPE (the system's classification — "Supply Air", "Domestic Cold Water", or whatever short code this project renamed it to, e.g. |

## filters/by-status  *(7)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-current-selection.cs` | Use whatever the user currently has selected in Revit as the element set — for "do this to what I've got selected" requests, no category/family/size logic needed at all. |
| ✅ | `filter-by-design-option.cs` | Elements belonging to one named Design Option instead of the Main Model — for comparing or acting on a specific design alternative. |
| ✅ | `filter-by-phase.cs` | Elements whose Phase Created and/or Phase Demolished matches a named project Phase — e.g. |
| ✅ | `filter-by-pin-status.cs` | Category elements that ARE or ARE NOT pinned — QA sweep ("show me everything pinned"). |
| ✅ | `filter-by-selection-filter.cs` | Retrieve the actual elements referenced by an existing named Selection Filter (or View Filter's rule-matched result in the given view) — the read-back counterpart to... |
| ✅ | `filter-by-warnings.cs` | Elements flagged by a current model warning, as an actionable `elements` set — for "select/ highlight/isolate everything with a warning". |
| ✅ | `filter-by-workset.cs` | Collect elements that belong to one user workset, optionally limited to one or more categories. |

## filters/by-view-and-sheet  *(7)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `filter-by-elements-in-view.cs` | Every instance of a category that is actually VISIBLE in a given view (view-specific graphics/filters/crop/phase all apply) — not just "exists in the model". |
| ✅ | `filter-by-schedules.cs` | Every ViewSchedule in the model, optionally narrowed by a name substring — feeds actions/sheets-views/action-export-schedule-to-csv.cs or... |
| ❓ | `filter-by-scope-box.cs` | Every Scope Box in the model, optionally narrowed by a name substring — feeds action-assign-scope-box-to-view.cs, action-rename-element.cs (rename it), or action-delete-elements.cs... |
| ✅ | `filter-by-sheets.cs` | Every ViewSheet in the model, optionally narrowed to sheet numbers containing a substring. |
| ✅ | `filter-by-tag-status.cs` | Category elements that ARE or ARE NOT tagged in a given view — QA sweep ("show me what's not tagged yet"). |
| ✅ | `filter-by-view-templates.cs` | View Templates themselves (IsTemplate == true) — the gap every other View Template fragment had to work around with its own by-name lookup instead of composing through the normal... |
| ✅ | `filter-by-views.cs` | Every View in the model (Floor Plans, Sections, 3D, Elevations, etc. |

## lib  *(1)*

| | Fragment | What it does |
|---|---|---|
| ✅ | `prelude.cs` | The helper functions that 150+ fragments currently each re-implement — transactions with rollback, mm/feet conversion, view targeting, parameter lookup, level resolution. |

## recipes  *(40)*

| | Fragment | What it does |
|---|---|---|
| ❓ | `audit-flex-curves.cs` | Audit every FLEX DUCT and FLEX PIPE in the model (or one view): size, how long each run really is, how much slack it is carrying, whether both ends are actually connected, and which runs... |
| ⚠️ | `build-test-fixtures.cs` | Create, in an empty scratch model, the fixtures that several fragments cannot be tested without. |
| ✅ | `connect-equipment-to-air-terminals.cs` | Connect ONE mechanical equipment's supply-air connector to ALL free air terminals as a proper branched system: main trunk out of the equipment connector -> tap per terminal -> 300x300... |
| ✅ | `connect-terminal-branch.cs` | Connect one air terminal to the main duct — vertical riser up to the main duct's height, a real elbow fitting at the turn, then a horizontal run tapped into the main duct via a takeoff tee. |
| ❓ | `create-equipment-family-from-datasheet.cs` | Build a manufacturer equipment family (.rfa) from a product datasheet, in one pass: a parametric box cabinet, any number of round connector stubs carrying PIPE or ELECTRICAL connectors... |
| ❓ | `create-mep-line-standards.cs` | One-click setup of the full MEP drafting line standard in any project: 1) Line patterns (dash-dot section, hidden, centre, phantom, demo, match, existing) 2) Line styles (MEP_ prefix,... |
| ❓ | `create-mep-openings.cs` | Cut real OPENINGS (sleeves) in walls, floors and beams wherever the MEP runs in `elements` pass through them — "put the sleeves in", "cut the holes for the ducts". |
| ❓ | `create-mep-text-standards.cs` | One-click setup of Ajmal's MEP text annotation standard in any project: 1) 120 text note types — the full matrix: 6 sizes x 10 colours x box/no-box, named... |
| ❓ | `create-parametric-box-family-with-duct-connector.cs` | Build a fully parametric box-shaped family (e.g. |
| ❓ | `create-revisions-from-sheet-dates.cs` | Scan every sheet's TextNotes for date-like text (e.g. |
| ✅ | `draw-main-duct-with-cap.cs` | Draw a single main duct piece from the FCU's supply connector along the room's long axis, sized to the FCU connector, connected at the FCU end, and cap the open far end with the full... |
| ✅ | `drone-shot-flythrough.cs` | A walkthrough / "drone shot" through the model, exported as numbered PNG frames on disk — "fly from Room 1 to Room 2", "follow this path". |
| ✅ | `fill-mm-document-register.cs` | Fill the MM_ document/handover register on one category in one pass — the fixed-value columns (CWA, MM_NP System Type, MM_Discipline Code, MM_Main Document Definition, MM_Main Drawing... |
| ✅ | `generate-room-coverage-layout.cs` | Lay out the devices needed so a fixed coverage radius leaves no gap in a room, and draw the circles. |
| ❓ | `maximize-level-extents.cs` | Stretch every LEVEL's 3D extent so it spans the active 3D view's SECTION BOX, written into every elevation, section and 3D view that shows each level — "my level lines are short in the... |
| ✅ | `mep-grayout.cs` | The whole "grayout for MEP" job in one pass — Ajmal's own drawing standard for making a coordination view read: the architectural/structural background drops back to flat grey, the... |
| ✅ | `model-health-audit.cs` | One read-only health report for the whole model — the "audit model health" job. |
| ✅ | `place-fcu.cs` | Place an FCU (Mechanical Equipment) in a room at the given ceiling-void height, optionally shift it toward the room's door (perpendicular-to-wall axis only), and rotate its real... |
| ⚠️ | `place-sleeves-at-wall-penetrations.cs` | Find every point where a duct or pipe crosses a straight wall, and (on request) place a sleeve family instance at each crossing, rotated to the run's direction, sized to the service +... |
| ✅ | `place-terminals-checkerboard.cs` | Place a room's brand-new supply/return air terminals in a near-square checkerboard grid with matched supply/return counts, and set each instance's own Flow parameter. |
| ⚠️ | `ray-trace-to-ceiling.cs` | Snap each element in `elements` (e.g. a diffuser/air terminal) to the nearest ceiling directly above it — casts a ray straight up from the element's current point using Revit's real... |
| ✅ | `set-space-airflow.cs` | Create/find the MEP Space for each room on a level, set its Specified Supply/Return Airflow from a thumb-rule, and cascade the new total to any air terminals already placed in that room... |
| ❓ | `size-domestic-water-pipe.cs` | Size a COLD or HOT WATER SUPPLY run the standard way — water supply fixture units (WSFU) -> probable demand in GPM off Hunter's curve -> smallest pipe that holds the velocity limit ->... |
| ✅ | `slice-trunk-for-sizing.cs` | Slice a main HVAC trunk duct into separate segments at each terminal-branch takeoff point, offset downstream past the takeoff's own body + a clearance margin, so each resulting segment... |
| ✅ | `split-duct-near-equipment.cs` | Split a duct at a given gap distance from an equipment connector (e.g. |
| ⚠️ | `sprinkler-adjust-for-obstructions.cs` | For each sprinkler head that fails an obstruction rule, find the SMALLEST move that clears it without breaking anything else — still inside the room, still off the walls, still far... |
| ✅ | `sprinkler-compliance-audit.cs` | Audit the sprinkler heads that are ALREADY in a room — whoever placed them, however they are arranged — against every spacing limit at once, and print one row per rule with the measured... |
| ⚠️ | `sprinkler-deflector-height.cs` | Answer "how high does this head sit" by READING what is really above it, not by assuming a void depth. |
| ⚠️ | `sprinkler-floor-scope.cs` | The FIRST pass over a whole architectural plan. |
| ⚠️ | `sprinkler-layout-options.cs` | Give SEVERAL genuinely different compliant sprinkler layouts for one room, ranked, instead of one answer. |
| ✅ | `sprinkler-nfpa-grid.cs` | Work out how many sprinkler heads a room needs and exactly where they go, by DERIVING the grid from the code limits instead of from a chosen coverage radius. |
| ⚠️ | `sprinkler-obstruction-check.cs` | Take a set of sprinkler head positions — proposed centres from recipes/sprinkler-nfpa-grid.cs, or the heads already in the model — and test every one of them against every beam, column,... |
| ✅ | `sprinkler-obstruction-survey.cs` | Look INSIDE a room before laying out a single sprinkler head — is there a ceiling, what is the deck above, which beams and columns are in here, how deep do they hang, is there a... |
| ❓ | `sprinkler-pipe-schedule-size.cs` | Size sprinkler pipe by the PIPE SCHEDULE method — walk the modelled pipe network, count how many sprinklers each segment feeds, look the required size up in the schedule table, and... |
| ✅ | `sprinkler-place-heads.cs` | Place real sprinkler family instances at a list of computed centres, at a stated height, and then READ THE PLACED HEADS BACK OUT OF THE MODEL and report what is actually there. |
| ⚠️ | `sprinkler-set-room-hazard.cs` | Record the decided hazard class ON EACH ROOM, in the model, and read it back. |
| ⚠️ | `sprinkler-sidewall-layout.cs` | Lay out SIDEWALL sprinkler heads along a room's walls — the corridor / small-room / no-void case, where pipe cannot get above the ceiling. |
| ❓ | `tag-elements-in-active-view.cs` | Tag qualifying elements of one category visible in the active view, deciding each tag's side (above/below/left/right) and leader by SCORING candidates against everything already placed —... |
| ✅ | `trace-mep-circuits.cs` | Trace real physical MEP circuits for a filtered pipe/duct system type, when tags/naming and Connector.IsConnected can't be trusted. |
| ✅ | `verify-duct-connectivity.cs` | Trace every terminal's full connector chain out to its FCU (riser -> elbow -> branch -> takeoff -> main trunk -> FCU), reporting exactly where any silent break is. |
