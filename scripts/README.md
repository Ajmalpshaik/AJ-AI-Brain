# AJ Tools — Reusable AJ AI Bridge Scripts (index — start here)

This folder holds **working C# fragments**, not just descriptions of them, for jobs that come up
repeatedly on the live model via the AJ AI Bridge (`mcp__aj-tools-aj-ai__run_csharp`).
The point: the next session runs code that already worked, instead of re-deriving it from prose and
risking a small mistake creeping back in.

They're composed per request rather than rewritten each time. **Most requests split into "which elements"
(a `filters/` fragment — or `creators/` if they don't exist yet) + "what to do to them" (one or more
`actions/`).** Genuinely bespoke, order-dependent multi-stage builds live in `recipes/` instead.

**Read this file, pick the fragment, open that one file — don't read the whole folder.** Background lives
beside this index, not in it:

- **Why the folder is shaped this way** (the filter+action idea, a worked example, the local-AI workflow,
  how the library grows) → [`architecture.md`](architecture.md)

Everything below is what an actual script task needs: the routing table, the rules, and the checkpoints.

## Current fragments

### Filters (produce `elements`)
Grouped into subfolders under `filters/` by what you already know about the elements you want —
the same job-grouping idea used for `actions/`. Split from one flat 49-file folder on 2026-07-26.

**What the element IS — category, family, type, material, datum** — [`filters/by-identity/`](filters/by-identity/)
| Fragment | Job |
|---|---|
| [`filter-by-category.cs`](filters/by-identity/filter-by-category.cs) | Every instance of one category, optional level scope |
| [`filter-by-category-and-family.cs`](filters/by-identity/filter-by-category-and-family.cs) | Category narrowed to a family name (VCD-style) |
| [`filter-by-category-name.cs`](filters/by-identity/filter-by-category-name.cs) | Category resolved by plain display name, not the BuiltInCategory enum |
| [`filter-by-family.cs`](filters/by-identity/filter-by-family.cs) | Family name matched across the WHOLE model, no category picked first |
| [`filter-by-family-type.cs`](filters/by-identity/filter-by-family-type.cs) | A specific Type inside a Family, matched by name (e.g. one exact fitting size) |
| [`filter-by-grid.cs`](filters/by-identity/filter-by-grid.cs) | Every Grid, optional name substring — feeds `creators/create-dimension.cs` |
| [`filter-by-id-list.cs`](filters/by-identity/filter-by-id-list.cs) | A specific list of Element Ids the user already has — "what is this element / what are its parameters" |
| [`filter-by-levels.cs`](filters/by-identity/filter-by-levels.cs) | Every Level ELEMENT itself (not elements sitting on one — that's `filter-by-elements-on-level.cs`), ordered by elevation |
| [`filter-by-material.cs`](filters/by-identity/filter-by-material.cs) | Elements using a specific Revit Material, category-scoped |
| [`filter-by-multiple-categories.cs`](filters/by-identity/filter-by-multiple-categories.cs) | Several categories collected as one group, e.g. duct system / pipe system / cable tray system |
| [`filter-by-types.cs`](filters/by-identity/filter-by-types.cs) | The TYPE elements themselves (FamilySymbol or system-family type), matched by family/type name — reaches a type with zero placed instances, unlike the instance-derived type actions |

**A parameter's VALUE — size, length, text, presence** — [`filters/by-property/`](filters/by-property/)
| Fragment | Job |
|---|---|
| [`filter-by-category-and-numeric-param.cs`](filters/by-property/filter-by-category-and-numeric-param.cs) | Category narrowed by a numeric parameter vs. an mm value (the "500mm duct" filter) |
| [`filter-by-length.cs`](filters/by-property/filter-by-length.cs) | Category narrowed by Length (mm) vs. an mm value — bound to `CURVE_ELEM_LENGTH` directly |
| [`filter-by-parameter-exists.cs`](filters/by-property/filter-by-parameter-exists.cs) | Elements that have a given parameter attached, whether blank or not — QA sweep, distinct from `filter-by-parameter-text.cs`'s value match |
| [`filter-by-parameter-text.cs`](filters/by-property/filter-by-parameter-text.cs) | Category or whole-model scan narrowed by text in family/type/parameter values |
| [`filter-by-size.cs`](filters/by-property/filter-by-size.cs) | Category narrowed by size — round (Diameter) and rectangular (Width x Height) handled together, or a plain "Size" text match |

**WHERE it sits — room, space, region, real geometry intersection** — [`filters/by-location/`](filters/by-location/)
| Fragment | Job |
|---|---|
| [`filter-by-element-intersection.cs`](filters/by-location/filter-by-element-intersection.cs) | Elements whose real geometry intersects one specific target element (`ElementIntersectsElementFilter`) |
| [`filter-by-elements-on-level.cs`](filters/by-location/filter-by-elements-on-level.cs) | Everything on a given Level across the WHOLE model, optional category scope |
| [`filter-by-region.cs`](filters/by-location/filter-by-region.cs) | Category narrowed to instances whose bounding box intersects a given mm region |
| [`filter-by-room.cs`](filters/by-location/filter-by-room.cs) | Category narrowed to instances physically inside one room, matched by Id, Name, and/or Number |
| [`filter-by-solid-intersection.cs`](filters/by-location/filter-by-solid-intersection.cs) | Elements whose real geometry intersects a custom 3D box/clearance solid (`ElementIntersectsSolidFilter`) — live-verified 2026-07-22 |
| [`filter-by-space.cs`](filters/by-location/filter-by-space.cs) | Category narrowed to instances physically inside one MEP Space (not a Room), matched by Id, Name, and/or Number — ✓ verified 2026-07-22 (name-matching fix in header) |
| [`filter-by-unenclosed-spatial-elements.cs`](filters/by-location/filter-by-unenclosed-spatial-elements.cs) | QA sweep — every Room/Space in the model with zero Area ("Not Enclosed") |

**What it's ATTACHED TO — host, group, link, MEP system, insulation** — [`filters/by-relationship/`](filters/by-relationship/)
| Fragment | Job |
|---|---|
| [`filter-by-assembly.cs`](filters/by-relationship/filter-by-assembly.cs) | Member elements of a specific Revit Assembly (`AssemblyInstance`) — ✓ graceful path only 2026-07-22 (no Assembly fixture) |
| [`filter-by-connection-status.cs`](filters/by-relationship/filter-by-connection-status.cs) | Category elements with at least one open connector end, or fully connected |
| [`filter-by-electrical-system.cs`](filters/by-relationship/filter-by-electrical-system.cs) | Elements in a specific Electrical System (circuit), by Circuit Type and/or circuit name — ✓ graceful path only 2026-07-22 (no electrical fixture) |
| [`filter-by-group.cs`](filters/by-relationship/filter-by-group.cs) | Member elements of a specific Model Group instance |
| [`filter-by-host.cs`](filters/by-relationship/filter-by-host.cs) | Elements hosted on a specific parent (`FamilyInstance.Host` or insulation/lining `HostElementId`) — ✓ graceful path only 2026-07-22 (no hosted fixture) |
| [`filter-by-insulation-status.cs`](filters/by-relationship/filter-by-insulation-status.cs) | Pipe/duct elements that HAVE insulation/lining applied, or don't — ✓ graceful path only 2026-07-22 (no insulation fixture) |
| [`filter-by-insulation-type.cs`](filters/by-relationship/filter-by-insulation-type.cs) | The insulation/lining elements themselves, by kind/type/material/thickness — ✓ graceful path only 2026-07-22 (no insulation fixture) |
| [`filter-by-linked-model-elements.cs`](filters/by-relationship/filter-by-linked-model-elements.cs) | Elements of a category INSIDE a specific linked RVT model, not the link instance itself — read-only composition only, see the file's own GOTCHA |
| [`filter-by-links.cs`](filters/by-relationship/filter-by-links.cs) | Every RVT link and/or CAD link instance, optional name substring — feeds `action-set-workset.cs` ("move the links onto a workset") |
| [`filter-by-subcomponents.cs`](filters/by-relationship/filter-by-subcomponents.cs) | NESTED sub-components inside parent FamilyInstances (optionally recursive) — the members a category filter never finds; reverse direction of `filter-by-host.cs` — NOT yet live-verified (2026-07-26 round 3, Clockwork-equivalent) |
| [`filter-by-system-name.cs`](filters/by-relationship/filter-by-system-name.cs) | Pipes/ducts/fittings narrowed to one specific System instance's own name (e.g. "DXS 1") — ✓ verified 2026-07-23 (same UnionWith fix) |
| [`filter-by-system-type.cs`](filters/by-relationship/filter-by-system-type.cs) | Pipes/ducts/fittings narrowed by MEP System TYPE/classification (e.g. "CDP", "Supply Air") — ✓ verified 2026-07-23 (UnionWith fix, story in header) |

**Its role in DOCUMENTATION — views, sheets, schedules, tags** — [`filters/by-view-and-sheet/`](filters/by-view-and-sheet/)
| Fragment | Job |
|---|---|
| [`filter-by-elements-in-view.cs`](filters/by-view-and-sheet/filter-by-elements-in-view.cs) | Category narrowed to instances actually visible in a given view (any view, not just active) — live-verified 2026-07-23, zero bugs |
| [`filter-by-schedules.cs`](filters/by-view-and-sheet/filter-by-schedules.cs) | Every ViewSchedule, optional name substring — feeds `action-export-schedule-to-csv.cs`/`action-place-schedule-on-sheet.cs` |
| [`filter-by-scope-box.cs`](filters/by-view-and-sheet/filter-by-scope-box.cs) | Every Scope Box, optional name substring — feeds `action-assign-scope-box-to-view.cs`; delete via `action-delete-elements.cs`, no dedicated fragment needed |
| [`filter-by-sheets.cs`](filters/by-view-and-sheet/filter-by-sheets.cs) | Every ViewSheet, optional sheet-number substring — live-verified 2026-07-23, zero bugs |
| [`filter-by-tag-status.cs`](filters/by-view-and-sheet/filter-by-tag-status.cs) | Category elements that ARE or ARE NOT tagged in a given view |
| [`filter-by-views.cs`](filters/by-view-and-sheet/filter-by-views.cs) | Every View (not ViewSheet), optional ViewType + name filter |
| [`filter-by-view-templates.cs`](filters/by-view-and-sheet/filter-by-view-templates.cs) | View Templates themselves, optional name filter + usage mode (all/used/unused) — makes templates composable with any action (rename, report, delete, ...) instead of needing a bespoke fragment |

**Project STATE — workset, phase, design option, pin, warnings, selection** — [`filters/by-status/`](filters/by-status/)
| Fragment | Job |
|---|---|
| [`filter-by-current-selection.cs`](filters/by-status/filter-by-current-selection.cs) | Whatever's currently selected in Revit |
| [`filter-by-design-option.cs`](filters/by-status/filter-by-design-option.cs) | Elements in a named Design Option, or the Main Model when left unset — ✓ Main Model path 2026-07-22; named-option path blocked (no Design Option exists, none creatable via API — see action-set-design-option.cs) |
| [`filter-by-phase.cs`](filters/by-status/filter-by-phase.cs) | Elements matching a named Phase Created and/or Phase Demolished, optional category scope |
| [`filter-by-pin-status.cs`](filters/by-status/filter-by-pin-status.cs) | Category elements that ARE or ARE NOT pinned |
| [`filter-by-selection-filter.cs`](filters/by-status/filter-by-selection-filter.cs) | Read back the actual elements behind an existing named Selection Filter, or re-evaluate a View Filter's rule in a given view — live-verified 2026-07-22, both branches |
| [`filter-by-warnings.cs`](filters/by-status/filter-by-warnings.cs) | Elements flagged by a current model warning, as an actionable set |
| [`filter-by-workset.cs`](filters/by-status/filter-by-workset.cs) | Elements on one user workset, optional category scope |

### Actions (consume `elements`)
Grouped into subfolders under `actions/` by job — same grouping used whenever these are listed out loud.

**Color & Graphics** — [`actions/color-graphics/`](actions/color-graphics/)
| Fragment | Job |
|---|---|
| [`action-set-color-uniform.cs`](actions/color-graphics/action-set-color-uniform.cs) | One color (line + solid fill) on every element |
| [`action-color-by-group.cs`](actions/color-graphics/action-color-by-group.cs) | Distinct color per group, grouped by any parameter's actual value; palette/gradient/random/pastel/neon modes — random/pastel/neon hue-step evenly around the color wheel so groups are GUARANTEED visually distinct, not independently randomized |
| [`action-highlight-vs-rest.cs`](actions/color-graphics/action-highlight-vs-rest.cs) | Highlight `elements` in one color, gray out every OTHER element in the active view |
| [`action-reset-graphic-overrides.cs`](actions/color-graphics/action-reset-graphic-overrides.cs) | Clear PER-ELEMENT color/fill overrides |
| [`action-report-graphic-overrides.cs`](actions/color-graphics/action-report-graphic-overrides.cs) | Read back current view-specific graphic overrides per element — line color, fill color, transparency, halftone; read-only |
| [`action-set-transparency.cs`](actions/color-graphics/action-set-transparency.cs) | Set surface transparency (0-100%) |
| [`action-set-category-transparency.cs`](actions/color-graphics/action-set-category-transparency.cs) | Set surface transparency for one or more ENTIRE categories — does NOT consume `elements` |
| [`action-set-category-color.cs`](actions/color-graphics/action-set-category-color.cs) | Override one or more ENTIRE categories' line/fill color in a view (Visibility/Graphics > Model Categories) — does NOT consume `elements`, categories are a direct array input |
| [`action-reset-category-graphics.cs`](actions/color-graphics/action-reset-category-graphics.cs) | Clear one or more CATEGORY-level graphic overrides — the paired undo for `action-set-category-color.cs` — does NOT consume `elements` |
| [`action-create-view-filter.cs`](actions/color-graphics/action-create-view-filter.cs) | Create a Revit VIEW FILTER (`ParameterFilterElement`, the Visibility/Graphics > Filters tab rule mechanism — NOT this repo's `filters/` folder) — persists, auto-applies to future elements too; every rule kind (contains/equals/begins/ends + not-variants, numeric eq/gt/gte/lt/lte/noteq, has-value/has-no-value) — does NOT consume `elements` — ✓ verified 2026-07-22 (contains-rule path) |
| [`action-create-selection-filter.cs`](actions/color-graphics/action-create-selection-filter.cs) | Save `elements` as a named Revit SELECTION FILTER (`SelectionFilterElement`) — an explicit element list instead of a rule, for when the set doesn't share one clean parameter condition — live-verified 2026-07-22 |
| [`action-apply-view-filter.cs`](actions/color-graphics/action-apply-view-filter.cs) | Add an existing filter (View Filter OR Selection Filter — looked up by the shared `FilterElement` base) to a view with a color/visibility, or update it if already applied — does NOT consume `elements` |
| [`action-remove-view-filter.cs`](actions/color-graphics/action-remove-view-filter.cs) | Take a filter (either kind) off a view, optionally delete it from the document entirely — does NOT consume `elements` |
| [`action-set-halftone.cs`](actions/color-graphics/action-set-halftone.cs) | Turn halftone on/off per element — read-modify-write, preserves any existing color override |
| [`action-set-category-halftone.cs`](actions/color-graphics/action-set-category-halftone.cs) | Turn halftone on/off for one or more ENTIRE categories — does NOT consume `elements` |
| [`action-set-line-style.cs`](actions/color-graphics/action-set-line-style.cs) | Override line weight and/or line pattern (dashed, dotted, ...) per element — every other action here only ever touches color |
| [`action-set-category-line-style.cs`](actions/color-graphics/action-set-category-line-style.cs) | Override line weight/pattern for one or more ENTIRE categories — does NOT consume `elements` |
| [`action-report-view-filters.cs`](actions/color-graphics/action-report-view-filters.cs) | List every View/Selection Filter in the document and which views use each — does NOT consume `elements` — ✓ verified 2026-07-23 (ProjectBrowser pseudo-view fix in header) |
| [`action-report-category-overrides.cs`](actions/color-graphics/action-report-category-overrides.cs) | Reverse lookup for `action-set-category-color.cs`/halftone/line-style — which categories have a category-level override set in a view — does NOT consume `elements` |

**Visibility** — [`actions/visibility/`](actions/visibility/)
| Fragment | Job |
|---|---|
| [`action-isolate-elements.cs`](actions/visibility/action-isolate-elements.cs) | Temporary isolate, reset-then-apply |
| [`action-hide-elements.cs`](actions/visibility/action-hide-elements.cs) | Hide (temporary by default, or permanent) |
| [`action-unhide-elements.cs`](actions/visibility/action-unhide-elements.cs) | Reverse a permanent hide |
| [`action-show-elements.cs`](actions/visibility/action-show-elements.cs) | Zoom/show the filtered elements, optionally selecting them |
| [`action-set-view-crop.cs`](actions/visibility/action-set-view-crop.cs) | Crop the active view to fit the filtered element set + margin |
| [`action-section-box-and-zoom.cs`](actions/visibility/action-section-box-and-zoom.cs) | Section-box a 3D view around `elements` and zoom to them |
| [`action-set-pin-state.cs`](actions/visibility/action-set-pin-state.cs) | Pin or unpin the filtered element set (same "reversible display/protection toggle" class as the rest of this group) |
| [`action-set-category-visibility.cs`](actions/visibility/action-set-category-visibility.cs) | Turn one or more ENTIRE categories on/off in a view (Visibility/Graphics > Model Categories checkbox) — does NOT consume `elements` |
| [`action-report-category-visibility.cs`](actions/visibility/action-report-category-visibility.cs) | Which categories are currently OFF in a view — the reverse lookup for `action-set-category-visibility.cs` — does NOT consume `elements` |
| [`action-set-view-workset-visibility.cs`](actions/visibility/action-set-view-workset-visibility.cs) | Make one view show ONLY named workset(s) — the "Workset 3D View" pattern, every other user workset turned off in that view; does NOT consume `elements` — BLOCKED (model isn't workshared); graceful path ✓ |
| [`action-assign-scope-box-to-view.cs`](actions/visibility/action-assign-scope-box-to-view.cs) | Assign (or clear) a named Scope Box as a view's own Scope Box property — ✓ clear-mode 2026-07-22; assign path blocked (no Scope Box can exist — see create-scope-box.cs) |
| [`action-set-crop-box-settings.cs`](actions/visibility/action-set-crop-box-settings.cs) | Turn Crop Region on/off, its boundary visibility on/off, and/or Annotation Crop on/off across views — independent flags, pairs with `action-set-view-crop.cs` for resizing |
| [`action-set-view-range.cs`](actions/visibility/action-set-view-range.cs) | Report or set a plan view's View Range — cut plane, top, bottom, view depth, each as level + mm offset; the "why don't my ducts show" fix; report-mode first (plane-order rule + ceiling-plan gotcha in header); does NOT consume `elements` — ✓ report mode verified 2026-07-26 (4 plan views; a real sentinel-level bug was found and fixed that run); set mode unexercised |

**Selection** — [`actions/selection/`](actions/selection/)
| Fragment | Job |
|---|---|
| [`action-select-elements.cs`](actions/selection/action-select-elements.cs) | Set, add to, or remove from the active Revit selection (`mode`) |

**Parameters & Naming** — [`actions/parameters-naming/`](actions/parameters-naming/)
| Fragment | Job |
|---|---|
| [`action-set-parameter-value.cs`](actions/parameters-naming/action-set-parameter-value.cs) | Bulk-set one parameter across the set — falls back to the Type if it's not an instance parameter |
| [`action-add-parameter-prefix-suffix.cs`](actions/parameters-naming/action-add-parameter-prefix-suffix.cs) | Add a prefix/suffix, or find/replace a substring, INSIDE a parameter's existing text (any String parameter, any category) — falls back to Type, deduped so a shared type isn't stacked — ✓ verified 2026-07-22 |
| [`action-add-project-parameter.cs`](actions/parameters-naming/action-add-project-parameter.cs) | Create a NEW shared-parameter-backed project parameter and bind it to categories — different job from every other fragment here, which only edit values of parameters that already exist; does NOT consume `elements` — ✓ verified 2026-07-22, Revit-2020 legacy API (version note in header) |
| [`action-find-replace-element-name.cs`](actions/parameters-naming/action-find-replace-element-name.cs) | Find/replace, prefix, or suffix `Element.Name` itself — works on ANY nameable element (Room, Sheet, View, Level, Grid, Group, Material, Type...), just pair with the matching filter — ✓ verified 2026-07-22 |
| [`action-copy-parameter-value.cs`](actions/parameters-naming/action-copy-parameter-value.cs) | Copy one parameter's value into a different parameter, storage-type-aware — source and target each independently fall back to Type |
| [`action-remove-parameter-value.cs`](actions/parameters-naming/action-remove-parameter-value.cs) | Clear one parameter's value — genuinely empty for String/ElementId, zeroed (not truly unset) for Double/Integer — falls back to Type |
| [`action-renumber-sequential.cs`](actions/parameters-naming/action-renumber-sequential.cs) | Assign a sequential value (prefix/number/padding/suffix) to a String parameter, sorted by position or existing value |
| [`action-rename-element.cs`](actions/parameters-naming/action-rename-element.cs) | Rename each element via `Element.Name` (views, sheets, levels, types — not most instance geometry); live-verified 2026-07-22, zero bugs |
| [`action-rename-family.cs`](actions/parameters-naming/action-rename-family.cs) | Bulk-rename the FAMILY behind a set of instances (resolves instance → Symbol → Family, dedupes so each family is renamed once) — prefix, suffix, find/replace, or flat replace modes — ✓ verified 2026-07-22 (renamed + restored a real family) |
| [`action-create-phase.cs`](actions/parameters-naming/action-create-phase.cs) | Create new project Phases — does NOT consume `elements` — CONFIRMED IMPOSSIBLE on Revit 2020 (`Document.Phases` is read-only; UI-only, Manage > Phases) — fragment reports this instead of throwing |
| [`action-rename-phase.cs`](actions/parameters-naming/action-rename-phase.cs) | Rename existing project Phases — does NOT consume `elements` — CONFIRMED IMPOSSIBLE on Revit 2020 (Phase Name is read-only; UI-only, Manage > Phases) — fragment reports this instead of throwing |
| [`action-set-element-phase.cs`](actions/parameters-naming/action-set-element-phase.cs) | Assign the filtered set's Phase Created and/or Phase Demolished to a named Phase — `action-set-parameter-value.cs` can't do this (no ElementId support) |
| [`action-report-phases.cs`](actions/parameters-naming/action-report-phases.cs) | List every project Phase in order — does NOT consume `elements` |
| [`action-delete-phase.cs`](actions/parameters-naming/action-delete-phase.cs) | Permanently delete Phases by name — completes the phase lifecycle — does NOT consume `elements` — ✓ verified 2026-07-22 (rollback-probe technique, detail in header) |
| [`action-set-workset.cs`](actions/parameters-naming/action-set-workset.cs) | Assign elements to a named user workset (write counterpart to `filter-by-workset.cs`) — BLOCKED (model isn't workshared); graceful path ✓ |
| [`action-set-design-option.cs`](actions/parameters-naming/action-set-design-option.cs) | Add elements to a named Design Option via the copy-while-active-option workaround (write counterpart to `filter-by-design-option.cs`) — Revit 2020 has NO API to activate a Design Option, so the option must be activated manually first (story in header) — graceful paths ✓ 2026-07-22, success path blocked |
| [`action-import-parameters-from-csv.cs`](actions/parameters-naming/action-import-parameters-from-csv.cs) | Read a CSV (ElementId + parameter columns — the shape `action-export-parameters-to-csv.cs` writes) and bulk-set the values back — write half of the Excel round-trip; does NOT consume `elements` (rows resolve by Id); explorer-first applies — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-rename-workset.cs`](actions/parameters-naming/action-rename-workset.cs) | Rename a user Workset (`WorksetTable.RenameWorkset`); workset DELETE is CONFIRMED IMPOSSIBLE on 2020 (API is 2022+) — reported, not thrown; does NOT consume `elements` — BLOCKED (not workshared), NOT yet live-verified (2026-07-26 round 2) |
| [`action-find-replace-text-notes.cs`](actions/parameters-naming/action-find-replace-text-notes.cs) | Find (report) or find-and-replace inside TextNote TEXT — the words on the drawing, not parameter values; find-first then replace per the explorer-first rule — NOT yet live-verified (2026-07-26 round 2) |

**Reporting** — [`actions/reporting/`](actions/reporting/)
| Fragment | Job |
|---|---|
| [`action-count-and-report.cs`](actions/reporting/action-count-and-report.cs) | Bare count or size-breakdown table |
| [`action-count-by-group.cs`](actions/reporting/action-count-by-group.cs) | Count broken down by ANY parameter's value (Level, System Type, Family, Comments, Phase Created, ...) — the general case beyond size |
| [`action-count-by-spatial-container.cs`](actions/reporting/action-count-by-spatial-container.cs) | Count broken down by which Room/Space/Zone physically contains each element — spatial test, not a parameter lookup (Room/Space have no such parameter on most MEP elements) |
| [`action-report-parameters.cs`](actions/reporting/action-report-parameters.cs) | Parameter table (values) for parameter names you already know |
| [`action-report-parameter-inventory.cs`](actions/reporting/action-report-parameter-inventory.cs) | Discover what parameters an element actually HAS — name, kind (Built-in/Shared/Project-Family), group, storage type, Instance vs Type, read-only, value — before you know the names to ask for |
| [`action-report-location.cs`](actions/reporting/action-report-location.cs) | Report each element's position (point, line endpoints, or bounding-box-center fallback); read-only |
| [`action-report-bounding-box.cs`](actions/reporting/action-report-bounding-box.cs) | Report each element's bounding box + the combined extents of the set; read-only |
| [`action-report-material-takeoff.cs`](actions/reporting/action-report-material-takeoff.cs) | Material area/volume quantities across `elements`, grouped by material |
| [`action-report-length-by-size.cs`](actions/reporting/action-report-length-by-size.cs) | Count + total length per size group, for linear MEP elements (duct/pipe/cable tray) |
| [`action-report-room-space-data.cs`](actions/reporting/action-report-room-space-data.cs) | Area/Volume/Level/Occupancy table for Rooms or Spaces; read-only — ✓ verified 2026-07-23 (ROOM_VOLUME fix in header) |
| [`action-compare-elements.cs`](actions/reporting/action-compare-elements.cs) | Side-by-side parameter diff of 2-8 elements — only differing values by default, instance + [T] type params; read-only — ✓ verified 2026-07-26 (3 walls: surfaced the 3 differing params, suppressed ~50 identical) |
| [`action-export-parameters-to-csv.cs`](actions/sheets-views/action-export-parameters-to-csv.cs) | Write chosen parameters of `elements` to a CSV (ElementId first column) — Revit half of the Excel round-trip, agent converts CSVâ†”xlsx outside Revit; paired with `action-import-parameters-from-csv.cs` — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-report-element-ownership.cs`](actions/reporting/action-report-element-ownership.cs) | Worksharing ownership per element — checkout status, creator, current owner, last changed by (the Worksharing tooltip in bulk); read-only — BLOCKED (not workshared), NOT yet live-verified (2026-07-26 round 2) |
| [`action-report-coverage.cs`](actions/reporting/action-report-coverage.cs) | How much floor each element serves — radius + area per element, min/max/average/total, optional coverage circles drawn as detail arcs and grouped, optional write-back to a parameter. **There is usually no coverage parameter to read** (checked: the standard supply diffuser has none), so pick the source deliberately — `spacing` (half the gap to the nearest neighbour, pure geometry, best default), `flow` (needs YOUR L/s per m2 and refuses to invent one), `fixed` (a stated radius). The three disagree ~6x; header explains why, and why ~45% floor coverage is normal not a fault — ✓ verified 2026-07-26 (17 terminals: radii 1883/2492/3520 mm, 344.1 m2 total) |
| [`action-report-nearest-elements.cs`](actions/reporting/action-report-nearest-elements.cs) | Nearest element(s) from each source to a target set — one category, several, or a fixed Id list. Three metrics that give different answers on purpose: `gap` (real clearance between bounding boxes, best default), `centre` (straight line), `manhattan` (orthogonal, cable-realistic). Answers "which FCU serves this terminal", "nearest exit", "nearest light"; groups by target so you see the zoning — ✓ verified 2026-07-26 (17 terminals -> 6 FCUs, 4/4/3/2/2/2) |
| [`action-plan-shortest-route.cs`](actions/reporting/action-plan-shortest-route.cs) | Cheapest way to CONNECT a set — `tree` = Prim's minimum spanning tree (branching, provably least total, the real homerun shape), `chain` = single unbranched daisy-chain (nearest-neighbour + 2-opt, heuristic not proven optimal), **`continuous` = one run that FINISHES each group before moving on, jumping from a room's optimised exit to the genuinely nearest element in any room not yet done — the user's method and the best of the three on real geometry (109.0 m vs 134.3 m for independent groups + feeders, within ~2% of the unbuildable unconstrained minimum)**. **`groupBy`** (none/room/space/level/parameter) routes each room or zone as its own run — without it the algorithm doesn't know walls exist and hops into the next room and back (measured: 6 zone crossings where 2 would do). `connectGroups` adds feeders between groups. Manhattan metric by default because cable runs orthogonally; `drawRoute` picks DETAIL lines for a plan view and model lines otherwise. Point-to-point estimate, NOT a routed cable schedule — header is explicit, and explains why grouping reports a LONGER total (134.3 m vs 106.8 m) yet is the buildable answer — ✓ verified 2026-07-26 (17 terminals: tree 111.7 m vs chain 132.9 m manhattan; grouping paths smoke-tested, room branch needs placed rooms) |
| [`action-report-ray-hits.cs`](actions/reporting/action-report-ray-hits.cs) | Fire rays out of each element and report WHAT THEY HIT — element, category, distance in mm; up/down/sideways/plan-diagonals or all 26 cube directions at once; target category is a per-request input (Ceilings, Floors/slab, Walls...) or blank for nearest-anything. The LOOK step before `action-move-to-ray-hit.cs` — ✓ verified 2026-07-26 (a FindNearest self-hit bug was caught and fixed that run; diagonal distances cross-check to axis x root-2) |
| [`action-report-connectors.cs`](actions/reporting/action-report-connectors.cs) | Every MEP connector per element — domain, shape, size, origin, facing (BasisZ), REAL connected partners (not just the IsConnected flag) — packages step 1 of the user's connection method (`../knowledge/live-model/hvac-ducts.md`); optional open-only mode — reads are the live-proven core of the connect recipe, fragment itself NOT yet run (2026-07-26 round 3, MEPover-equivalent) |
| [`action-report-compound-structure.cs`](actions/reporting/action-report-compound-structure.cs) | Layer build-up of wall/floor/roof/ceiling TYPES — function, material, thickness per layer, core marks, total; deduped per type; read-only — ✓ verified 2026-07-26 (9 walls -> 1 type, core layer marked) |
| [`action-report-room-boundaries.cs`](actions/reporting/action-report-room-boundaries.cs) | Room/Space boundary loops as mm segments + the wall behind each segment (finish or centerline); outer loop vs holes marked; read-only — geometry feed for `create-wall.cs`/`create-line.cs` — ✓ graceful path verified 2026-07-26 (3 unplaced rooms -> honest "0 loops"); positive path still needs an enclosed room |

**QA Checks** — [`actions/qa-checks/`](actions/qa-checks/)
| Fragment | Job |
|---|---|
| [`action-find-duplicates.cs`](actions/qa-checks/action-find-duplicates.cs) | QA check — flag elements whose insertion points sit within a tolerance of each other (duplicate LOCATION); read-only, optional select |
| [`action-find-duplicate-values.cs`](actions/qa-checks/action-find-duplicate-values.cs) | QA check — flag elements sharing the same value in a named parameter, e.g. duplicate Mark (duplicate DATA, not location); read-only, optional select |
| [`action-find-blank-parameter.cs`](actions/qa-checks/action-find-blank-parameter.cs) | QA check — flag elements where a named parameter is blank/unset (falls back to Type); read-only, optional select |
| [`action-check-surface-fit.cs`](actions/qa-checks/action-check-surface-fit.cs) | QA before/after snapping to a surface — fires rays from each element's FOOTPRINT (centre + corners, or 3x3) and flags STRADDLING two surfaces, OVERHANGING an edge, UNEVEN across the footprint, or SLOPED (can't sit flush). Reports BY EXCEPTION (summary + only the failures) so it scales to thousands; ~0.07 ms/ray measured — ✓ verified 2026-07-26, both the clean path (17 terminals OK) and the detection path (a boundary-parked terminal correctly caught as OVERHANGING where a single centre ray was confidently wrong) |
| [`action-report-clashes.cs`](actions/qa-checks/action-report-clashes.cs) | Basic clash report — real geometry intersection between `elements` (set A) and a second category (set B); read-only — ✓ verified 2026-07-22 |

**Move / Copy / Rotate** — [`actions/move-copy-rotate/`](actions/move-copy-rotate/)
| Fragment | Job |
|---|---|
| [`action-move-elements.cs`](actions/move-copy-rotate/action-move-elements.cs) | Translate every element by one mm offset vector |
| [`action-copy-elements.cs`](actions/move-copy-rotate/action-copy-elements.cs) | Duplicate every element, offset by one mm vector; produces `newElementIds` for chaining |
| [`action-rotate-elements.cs`](actions/move-copy-rotate/action-rotate-elements.cs) | Rotate every element around a vertical axis by one angle, about its own location or a given pivot |
| [`action-mirror-elements.cs`](actions/move-copy-rotate/action-mirror-elements.cs) | Mirror every element across a vertical plane through two plan points — copy or in-place |
| [`action-flip-elements.cs`](actions/move-copy-rotate/action-flip-elements.cs) | Flip hand and/or facing orientation of FamilyInstances — Revit's own Flip arrows — ✓ graceful skip paths 2026-07-23; positive path blocked (no flip-capable family loaded) |
| [`action-offset-elements.cs`](actions/move-copy-rotate/action-offset-elements.cs) | Offset each linear element sideways by a perpendicular distance (mm) — copy or in-place — ✓ verified 2026-07-22, both modes exact |
| [`action-trim-extend-elements.cs`](actions/move-copy-rotate/action-trim-extend-elements.cs) | Trim or extend exactly 2 linear elements to meet at their computed corner — ✓ verified 2026-07-22 exact (coincident-endpoint caution in header) |
| [`action-fillet-elements.cs`](actions/move-copy-rotate/action-fillet-elements.cs) | Round the corner between exactly 2 elements — real elbow fitting for MEP curves, or a geometric tangent arc for Model/Detail lines — ✓ verified 2026-07-22 both modes (arc-mode bug found+fixed, story in header) |
| [`action-array-elements.cs`](actions/move-copy-rotate/action-array-elements.cs) | Multiple evenly-spaced copies — linear (fixed mm spacing) or radial (swept around a center) — ✓ verified 2026-07-22, both modes exact |
| [`action-move-to-ray-hit.cs`](actions/move-copy-rotate/action-move-to-ray-hit.cs) | Fire ONE ray per element and move it to whatever the ray hits, plus a signed offset — "terminal up to the slab", "sprinkler up to the soffit", "bracket across to the wall". Direction AND target category are per-request inputs; dry-run by default. The general form of `recipes/ray-trace-to-ceiling.cs` — ✓ verified 2026-07-26 (17 air terminals snapped to 3 ceilings at 3 different heights, each finding the one above itself, 0 misses) |
| [`action-align-elements.cs`](actions/move-copy-rotate/action-align-elements.cs) | Snap every element to match one reference element's X/Y/Z position — Revit's "Align" tool — ✓ verified 2026-07-22 exact |

**Structural Changes** — [`actions/structural-changes/`](actions/structural-changes/)
| Fragment | Job |
|---|---|
| [`action-change-element-type.cs`](actions/structural-changes/action-change-element-type.cs) | Bulk-swap every element's type to a different named type within the same family |
| [`action-delete-elements.cs`](actions/structural-changes/action-delete-elements.cs) | Permanently delete every element in the set — highest-risk fragment, explorer-first is mandatory, needs `allowDestructive: true` on the bridge call too — ✓ verified 2026-07-22 |
| [`action-group-elements.cs`](actions/structural-changes/action-group-elements.cs) | Bundle the filtered set into a new Model Group |
| [`action-ungroup-elements.cs`](actions/structural-changes/action-ungroup-elements.cs) | Dissolve Group instances in the set back into their members — paired undo for `action-group-elements.cs` |
| [`action-join-geometry.cs`](actions/structural-changes/action-join-geometry.cs) | Join (or unjoin) every element in the set with one target element — many-to-one |
| [`action-split-elements.cs`](actions/structural-changes/action-split-elements.cs) | Split each Duct/Pipe at a point along its own length (fraction or mm from start), joint held by a real Union fitting (2026-07-26 fix: bare ConnectTo joints can be silently re-merged by Revit, losing the split) — generalized from `recipes/split-duct-near-equipment.cs` — ✓ verified 2026-07-22, union fix not yet live-run |
| [`action-purge-unused.cs`](actions/structural-changes/action-purge-unused.cs) | Delete unused View Templates, Filters, or Materials — the subset of native Purge Unused provably correct from the public API; does NOT consume `elements`, dry-run by default — ✓ dry-run 2026-07-22 all 3 modes; real delete not yet exercised |
| [`action-reload-links.cs`](actions/structural-changes/action-reload-links.cs) | Reload the distinct RVT link type(s) behind a set of link instances — Manage Links' Reload, scripted — BLOCKED (0 links in this model; see also the enum flag in the header) |
| [`action-unload-remove-links.cs`](actions/structural-changes/action-unload-remove-links.cs) | Unload (keep, drop from memory) or REMOVE (delete from project — destructive, `allowDestructive: true` required) the link type(s) behind a set of link instances — completes Manage Links alongside `action-reload-links.cs` — BLOCKED (0 links), graceful path only (2026-07-26 gap backlog) |
| [`action-add-remove-insulation.cs`](actions/structural-changes/action-add-remove-insulation.cs) | Add or remove insulation (ducts+pipes) or lining (ducts only) — the WRITE counterpart to the two insulation filters; one insulation per element, already-insulated skipped+reported — BLOCKED (no insulation fixture), NOT yet live-verified (2026-07-26 round 2) |
| [`action-extract-cad-curves.cs`](actions/structural-changes/action-extract-cad-curves.cs) | Trace linked/imported CAD into Revit Model/Detail lines, filtered by DWG layer — dry-run first reports curve counts PER LAYER so the layer filter comes from reality; polylines exploded, splines skipped+counted — NOT yet live-verified, needs a CAD fixture (2026-07-26 round 3, Bimorph-equivalent) |
| [`action-place-accessory-on-run.cs`](actions/structural-changes/action-place-accessory-on-run.cs) | Insert a duct/pipe ACCESSORY (VCD, damper, valve) INTO each run — Revit breaks the run and connects both cut ends; position by fraction or mm from start; domain-mismatched families and too-close-to-end runs skipped+reported; produces `newAccessoryIds` — MODIFIES existing runs, explorer-first applies — NOT yet live-verified (2026-07-26 round 4, break-in overload FLAGGED in header) |
| [`action-purge-unused-families.cs`](actions/structural-changes/action-purge-unused-families.cs) | Find/delete loadable family TYPES with zero placed instances, and whole families where every type is unused — the file-size half `action-purge-unused.cs` leaves out; nested symbols protected; dry-run by default, `allowDestructive: true` to really delete — ✓ dry-run verified 2026-07-26 (found 184/184 types and 107 whole families unused); real delete unexercised |
| [`action-copy-from-link.cs`](actions/structural-changes/action-copy-from-link.cs) | Copy elements FROM a linked model INTO the host at true position (link transform applied) — source Ids are LINKED-doc Ids from `filter-by-linked-model-elements.cs`; copies never update with the link (say so every time) — BLOCKED (0 links), NOT yet live-verified (2026-07-26 round 3) |
| [`action-duplicate-type.cs`](actions/structural-changes/action-duplicate-type.cs) | Duplicate the distinct TYPE(s) behind a set of instances under a new name (prefix/suffix/fixed) — Type-level counterpart to `action-rename-family.cs` — ✓ verified 2026-07-22 |
| [`action-update-scope-box.cs`](actions/structural-changes/action-update-scope-box.cs) | Report a named Scope Box's dependent views (does NOT consume `elements`) — resize CONFIRMED IMPOSSIBLE on Revit 2020 (needs Scope Box creation, which has no API — see create-scope-box.cs); the destructive resize step was removed entirely |

**Sheets & Views** — [`actions/sheets-views/`](actions/sheets-views/)
| Fragment | Job |
|---|---|
| [`action-place-viewport-on-sheet.cs`](actions/sheets-views/action-place-viewport-on-sheet.cs) | Place each view in the set onto one sheet as a Viewport (views can only sit on one sheet at a time) |
| [`action-place-schedule-on-sheet.cs`](actions/sheets-views/action-place-schedule-on-sheet.cs) | Place each schedule onto one sheet — same schedule can be placed on multiple sheets, no duplication needed |
| [`action-duplicate-views.cs`](actions/sheets-views/action-duplicate-views.cs) | Duplicate/duplicate-with-detailing/dependent-view each view in the set; produces `newViewIds` |
| [`action-apply-view-template.cs`](actions/sheets-views/action-apply-view-template.cs) | Apply an existing View Template to one or more views — does NOT consume `elements` |
| [`action-create-view-template-from-view.cs`](actions/sheets-views/action-create-view-template-from-view.cs) | Save a configured view's current settings as a new named View Template — does NOT consume `elements` — ✓ verified 2026-07-22 (`CreateViewTemplate()` returns a `View`, not an `ElementId`) |
| [`action-set-view-template-controlled-params.cs`](actions/sheets-views/action-set-view-template-controlled-params.cs) | Include/Exclude which parameters a View Template controls on one view — does NOT consume `elements` — ✓ verified 2026-07-22 |
| [`action-remove-view-template.cs`](actions/sheets-views/action-remove-view-template.cs) | Detach a View Template from one or more views, optionally delete it from the document entirely — the paired undo for `action-apply-view-template.cs` — does NOT consume `elements` |
| [`action-duplicate-view-template.cs`](actions/sheets-views/action-duplicate-view-template.cs) | Duplicate an existing View Template (by name) into a new, separately-named template — does NOT consume `elements` |
| [`action-report-view-template-status.cs`](actions/sheets-views/action-report-view-template-status.cs) | Report whether one or more views have a View Template applied — which one, and which parameters are excluded from its control; read-only — does NOT consume `elements` |
| [`action-add-schedule-field.cs`](actions/sheets-views/action-add-schedule-field.cs) | Add one or more columns/fields to existing schedules — same call `create-schedule.cs` uses, for a schedule that already exists — ✓ verified 2026-07-22 |
| [`action-remove-schedule-field.cs`](actions/sheets-views/action-remove-schedule-field.cs) | Remove one or more columns/fields by name — paired undo for `action-add-schedule-field.cs` — ✓ verified 2026-07-22 |
| [`action-set-schedule-field-format.cs`](actions/sheets-views/action-set-schedule-field-format.cs) | Format one existing column — heading override, hide/show, alignment, sheet column width — ✓ verified 2026-07-22 |
| [`action-set-schedule-filters.cs`](actions/sheets-views/action-set-schedule-filters.cs) | Replace ALL filter rules on a schedule (field/type/value list) — clears old filters first, not additive — ✓ verified 2026-07-22 |
| [`action-set-schedule-sort-group.cs`](actions/sheets-views/action-set-schedule-sort-group.cs) | Replace ALL sort/group fields on a schedule, in order — clears old sort/group first, not additive — ✓ verified 2026-07-22 |
| [`action-set-schedule-appearance.cs`](actions/sheets-views/action-set-schedule-appearance.cs) | Set "Itemize every instance" and Grand Total row — the 2 appearance settings with solid API confidence; the rest of the Appearance tab deliberately NOT covered — ✓ verified 2026-07-22 (`ShowGrandTotal` is a plain `bool` on 2020) |
| [`action-report-schedule-fields.cs`](actions/sheets-views/action-report-schedule-fields.cs) | List every field/column on a schedule IN ORDER — name, heading, type, hidden, alignment; read-only |
| [`action-add-schedule-calculated-field.cs`](actions/sheets-views/action-add-schedule-calculated-field.cs) | Add a Combined Parameter field to a schedule — ✓ verified 2026-07-22, Revit-2020 API (`TableCellCombinedParameterData` — story in header); mode="formula" is a confirmed 2020 gap (UI-only), reports instead of throwing |
| [`action-export-sheets-to-pdf.cs`](actions/sheets-views/action-export-sheets-to-pdf.cs) | Batch-export ViewSheets to PDF via `Document.PrintManager` (Revit 2020 has no PDF export API — story in header) with a safety guard against this system's physical printer — reflection-verified, NOT live-executed; needs a user go-ahead for the first real print |
| [`action-set-view-properties.cs`](actions/sheets-views/action-set-view-properties.cs) | Batch-set Scale, Detail Level, Visual Style, and/or Phase/Phase Filter across views — the lightweight direct version of applying a View Template — ✓ verified 2026-07-22 |
| [`action-tag-elements.cs`](actions/sheets-views/action-tag-elements.cs) | Simple tag placement (fixed offset, optional leader) in one view — reuses the proven `IndependentTag.Create` pattern from `recipes/tag-elements-in-active-view.cs` without its clash-scoring — ✓ verified 2026-07-22 |
| [`action-remove-tags.cs`](actions/sheets-views/action-remove-tags.cs) | Delete IndependentTag elements in the set — paired undo for `action-tag-elements.cs` |
| [`action-export-schedule-to-csv.cs`](actions/sheets-views/action-export-schedule-to-csv.cs) | Export ViewSchedules to CSV via Revit's native `ViewSchedule.Export` — one file per schedule — ✓ verified 2026-07-22 (real CSV content confirmed) |
| [`action-report-sheet-title-blocks.cs`](actions/sheets-views/action-report-sheet-title-blocks.cs) | Report which title block (Family + Type) is on each sheet — read-only |
| [`action-set-sheet-title-block.cs`](actions/sheets-views/action-set-sheet-title-block.cs) | Change each sheet's title block to a named Type — in-place `ChangeTypeId` if same family, delete+replace at origin if a different family — ✓ verified 2026-07-22 |
| [`action-manage-sheet-sets.cs`](actions/sheets-views/action-manage-sheet-sets.cs) | Named Sheet/View Sets (`ViewSheetSet`, the Print/Export saved sets) — report / create-from-`elements` / delete / rename; pairs with `action-export-sheets-to-pdf.cs` — NOT yet live-verified (2026-07-26 gap backlog, delete/rename FLAGGED in header) |
| [`action-export-views-to-dwg.cs`](actions/sheets-views/action-export-views-to-dwg.cs) | Export each View/Sheet in `elements` to its own DWG (optional saved export setup by name) — writes files on disk, go-ahead first — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-export-ifc.cs`](actions/sheets-views/action-export-ifc.cs) | Export the model (or one 3D view's scope) to IFC — IFC2x3CV2/IFC4/IFC2x3; does NOT consume `elements`; transaction-wrapped on purpose (FLAGGED note in header) — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-export-nwc.cs`](actions/sheets-views/action-export-nwc.cs) | Export the model (or one 3D view's scope) to Navisworks NWC — detects the exporter add-in first, reports gracefully if absent; does NOT consume `elements` — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-export-view-image.cs`](actions/sheets-views/action-export-view-image.cs) | Export each View/Sheet in `elements` as a PNG at a chosen pixel width — the "screenshot this view properly" job — NOT yet live-verified (2026-07-26 gap backlog) |
| [`action-add-spot-elevations.cs`](actions/sheets-views/action-add-spot-elevations.cs) | Place a Spot Elevation on each element in one view — reference dug from element geometry (the fragile part, FLAGGED in header); no-reference elements skipped+reported — NOT yet live-verified (2026-07-26 round 2) |
| [`action-set-print-settings.cs`](actions/sheets-views/action-set-print-settings.cs) | Report the current driver's paper sizes + saved Print Settings, or save a named Print Setting (paper/orientation/zoom) — completes the print chain with `action-manage-sheet-sets.cs` + PDF export; does NOT consume `elements` — ✓ report mode verified 2026-07-26 (34 paper sizes off a PHYSICAL Kyocera 6008ci); save mode still unexercised |
| [`action-duplicate-sheet.cs`](actions/sheets-views/action-duplicate-sheet.cs) | Duplicate each sheet — same title block, views duplicated and placed at the SAME viewport positions, schedules re-placed; produces `newSheetIds`; loose sheet annotations/guide grids/revisions NOT copied (header) — NOT yet live-verified (2026-07-26 round 3, Rhythm-equivalent) |
| [`action-add-aligned-dimensions.cs`](actions/sheets-views/action-add-aligned-dimensions.cs) | One aligned dimension string through 2+ FamilyInstances along X or Y — uses the families' own centre references so the dimension holds when elements move; sorted into drafting order; reference-less families skipped+reported — extends `create-dimension.cs` past Grid/Level — NOT yet live-verified (2026-07-26 round 4, family-authoring dependency FLAGGED) |

**Sheet Dates & Revisions** — [`actions/sheet-dates-revisions/`](actions/sheet-dates-revisions/)
| Fragment | Job |
|---|---|
| [`action-extract-dates-from-textnotes.cs`](actions/sheet-dates-revisions/action-extract-dates-from-textnotes.cs) | Scan every TextNote on each sheet for date-like text, report distinct dates + source sheet(s), read-only |
| [`action-assign-revisions-by-sheet-date.cs`](actions/sheet-dates-revisions/action-assign-revisions-by-sheet-date.cs) | Attach each sheet's matching project Revision(s) via `SetAdditionalRevisionIds`, matched by date found in that sheet's TextNotes — writes the model, see gotcha note in `../knowledge/live-model/revisions.md` |
| [`action-remove-revision-from-sheet.cs`](actions/sheet-dates-revisions/action-remove-revision-from-sheet.cs) | Detach named Revision(s) from each sheet, matched by Description — the reverse of `action-assign-revisions-by-sheet-date.cs` |
| [`action-report-revisions.cs`](actions/sheet-dates-revisions/action-report-revisions.cs) | List every project Revision in order — Seq/Date/Description/Issued By/To/Issued/Visibility — does NOT consume `elements` |
| [`action-edit-revision.cs`](actions/sheet-dates-revisions/action-edit-revision.cs) | Update an existing Revision's fields (description/date/issued by/to/issued flag/visibility), matched by SequenceNumber — does NOT consume `elements` |
| [`action-delete-revision.cs`](actions/sheet-dates-revisions/action-delete-revision.cs) | Permanently delete Revision(s) by SequenceNumber — completes the Create/Edit/Delete lifecycle alongside `creators/create-revision.cs`; renumbers later revisions — does NOT consume `elements` — ✓ verified 2026-07-22 (deletion + renumbering confirmed) |

### Creators (produce `elements` by creating new ones)
| Fragment | Job |
|---|---|
| [`create-levels.cs`](creators/create-levels.cs) | Batch-create levels, evenly spaced or at explicit elevations |
| [`create-material.cs`](creators/create-material.cs) | Create one or more Materials with a set colour and transparency |
| [`create-point-based-element.cs`](creators/create-point-based-element.cs) | Place a family instance at one or more points on a level |
| [`create-room.cs`](creators/create-room.cs) | Place a Room at one or more points on a level |
| [`create-space.cs`](creators/create-space.cs) | Place an MEP Space at one or more points on a level — Space-category equivalent of create-room.cs — ✓ verified 2026-07-22 (Name/Number GOTCHA — see filter-by-space.cs) |
| [`create-sheet.cs`](creators/create-sheet.cs) | Create one or more new sheets with a chosen title block — ✓ verified 2026-07-22 |
| [`create-schedule.cs`](creators/create-schedule.cs) | Create a bare schedule for a category with chosen fields — chain into `action-place-schedule-on-sheet.cs` — ✓ verified 2026-07-22 |
| [`create-text-note.cs`](creators/create-text-note.cs) | Place one or more Text Notes at given points in a view |
| [`create-dimension.cs`](creators/create-dimension.cs) | Create a dimension string across 2+ Grids/Levels — deliberately scoped to Grid/Level references only, not arbitrary element geometry — ✓ verified 2026-07-22 exact |
| [`create-line.cs`](creators/create-line.cs) | Create one or more plain Model Lines or Detail Lines between mm point pairs — ✓ verified 2026-07-22 |
| [`create-filled-region.cs`](creators/create-filled-region.cs) | Create a filled/hatched polygon annotation from a closed loop of mm points — ✓ verified 2026-07-22 |
| [`create-grid.cs`](creators/create-grid.cs) | Create one or more straight Grids from mm endpoint pairs |
| [`create-view.cs`](creators/create-view.cs) | Create a Floor Plan, 3D, or Section view — the three simple/reliable ViewFamily cases, not Callout/Elevation/Drafting |
| [`create-room-elevations.cs`](creators/create-room-elevations.cs) | Place an `ElevationMarker` at a room's center (or an mm point) in a plan view and create 1-4 interior elevation views — fills the Elevation slot `create-view.cs` excludes; marker not auto-rotated to walls (gotcha in header) — NOT yet live-verified (2026-07-26 gap backlog) |
| [`create-floor.cs`](creators/create-floor.cs) | Create one flat Floor from a closed mm boundary on a Level — legacy `NewFloor` (static `Floor.Create` is 2022+, don't "modernize") — NOT yet live-verified (2026-07-26 gap backlog) |
| [`create-revision.cs`](creators/create-revision.cs) | Create one or more Revisions directly (date/description/issued-to/by already known) — plain version of `recipes/create-revisions-from-sheet-dates.cs` — ✓ verified 2026-07-22 |
| [`create-workset.cs`](creators/create-workset.cs) | Create one or more new user Worksets — feeds `action-set-workset.cs`; produces no `elements` (Workset isn't an Element) — BLOCKED (model isn't workshared); graceful path ✓ |
| [`create-scope-box.cs`](creators/create-scope-box.cs) | CONFIRMED IMPOSSIBLE on Revit 2020 — no Scope Box creation API exists (reflection-confirmed; UI-only, View tab > Scope Box) — fragment reports this instead of a compile error |
| [`load-family.cs`](creators/load-family.cs) | Load .rfa files from disk into the project (File > Load Family) — produces the loaded FamilySymbols; deliberately does NOT overwrite an existing same-name family — NOT yet live-verified (2026-07-26 round 2) |
| [`create-duct.cs`](creators/create-duct.cs) | Draw ONE straight duct between two mm points (system type, duct type, level, W/H or dia) — the plain version of what the HVAC recipes do; unconnected segment, check open ends after — NOT yet live-verified (2026-07-26 round 2) |
| [`create-pipe.cs`](creators/create-pipe.cs) | Draw ONE straight pipe between two mm points — Plumbing twin of `create-duct.cs`; diameter snaps to the type's allowed sizes — NOT yet live-verified (2026-07-26 round 2) |
| [`create-cable-tray.cs`](creators/create-cable-tray.cs) | Draw ONE straight cable tray between two mm points (type, level, W/H) — NOT yet live-verified (2026-07-26 round 2) |
| [`create-conduit.cs`](creators/create-conduit.cs) | Draw ONE straight conduit between two mm points (type, level, dia) — NOT yet live-verified (2026-07-26 round 2) |
| [`create-wall.cs`](creators/create-wall.cs) | Create one straight basic Wall between two mm plan points (type, level, unconnected height, structural flag) — NOT yet live-verified (2026-07-26 round 2) |
| [`create-ceiling.cs`](creators/create-ceiling.cs) | CONFIRMED IMPOSSIBLE on Revit 2020 — `Ceiling.Create` only exists from Revit 2022 (UI-only: Architecture > Ceiling) — fragment reports this instead of a compile error; existing ceilings still workable (ray-trace recipe, filters) |
| [`create-revision-cloud.cs`](creators/create-revision-cloud.cs) | Draw a rectangular Revision Cloud in any view/sheet tied to an existing Revision (rectangle in the view's own plane, so the same mm numbers work in plan/section/sheet) — completes the Revision lifecycle's annotation half — NOT yet live-verified (2026-07-26 round 2) |
| [`create-hvac-zone.cs`](creators/create-hvac-zone.cs) | Create an HVAC Zone on a Level and add existing Spaces to it — the grouping layer above `create-space.cs`; one-zone-per-space + phase-match gotchas in header — NOT yet live-verified (2026-07-26 round 2) |
| [`create-mep-system-type.cs`](creators/create-mep-system-type.cs) | New duct/pipe SYSTEM TYPE by duplicating an existing one + name/abbreviation/colour — how a project gets separately-filterable systems (CHWS, CHWR, Supply Air - Zone 1); no create-from-nothing API exists, hence the required source; feeds `filter-by-system-type.cs` — NOT yet live-verified (2026-07-26 round 4) |
| [`create-callout-view.cs`](creators/create-callout-view.cs) | Callout view inside a parent view over an mm MODEL-coordinate rectangle — the last common view type `create-view.cs` excluded; inherits the parent's type by default — NOT yet live-verified (2026-07-26 round 4) |
| [`create-legend-view.cs`](creators/create-legend-view.cs) | New Legend by DUPLICATING an existing one — the only route the API offers (no `ViewLegend.Create` on any version, and placing NEW legend components has no API either); reports that plainly when the project has zero legends — ✓ graceful path verified 2026-07-26 (zero-legend project); duplicate path needs a project that has one |
| [`create-sheet-list.cs`](creators/create-sheet-list.cs) | Sheet List / drawing index schedule (`ViewSchedule.CreateSheetList`) — schedules SHEETS not model elements, so `create-schedule.cs` can't make one; unschedulable field names reported with the available list — ✓ verified 2026-07-26 (created with both columns + sort, then reverted via native-undo) |
| [`create-key-schedule.cs`](creators/create-key-schedule.cs) | KEY schedule for a category (`ViewSchedule.CreateKeySchedule`) — the preset-lookup table; ALSO adds the key parameter to every element of that category, a model-wide change (say so first); key ROWS stay a manual job — NOT yet live-verified (2026-07-26 round 4) |

### Recipes (bespoke multi-stage builds, not filter+action shaped)
| Recipe | Job | Source |
|---|---|---|
| [`recipes/trace-mep-circuits.cs`](recipes/trace-mep-circuits.cs) | Bulk-cluster a filtered pipe/duct system into physical circuits and find real endpoints — ✓ verified 2026-07-23 (UnionWith fix) | `../knowledge/live-model/mep-trace.md` § Tracing real MEP connectivity |
| [`recipes/set-space-airflow.cs`](recipes/set-space-airflow.cs) | Create/find each room's MEP Space, set Supply/Return Airflow, cascade to existing terminals — ✓ verified 2026-07-23 incl. the cascade branch | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-terminals-checkerboard.cs`](recipes/place-terminals-checkerboard.cs) | Place a room's supply/return terminals in a near-square checkerboard grid — ✓ verified 2026-07-23 exact | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-fcu.cs`](recipes/place-fcu.cs) | Place an FCU, reposition toward the door, rotate to face terminals — API surface reflection-verified 2026-07-23, NOT live-executed (needs a Room+FCU+terminal layout beyond this model's current fixtures) | `../knowledge/live-model/hvac-ducts.md` § Placing equipment relative to a door |
| [`recipes/draw-main-duct-with-cap.cs`](recipes/draw-main-duct-with-cap.cs) | Draw a sized main duct from the FCU and cap every open end correctly — API surface reflection-verified 2026-07-23, NOT live-executed (same layout gap as place-fcu.cs) | `../knowledge/live-model/hvac-ducts.md` § Drawing a duct, § cap-end recipe |
| [`recipes/connect-terminal-branch.cs`](recipes/connect-terminal-branch.cs) | Riser + real elbow + takeoff tee connecting a terminal to the main duct — API surface reflection-verified 2026-07-23, NOT live-executed (same layout gap as place-fcu.cs); core pattern since live-proven via connect-equipment-to-air-terminals.cs | `../knowledge/live-model/hvac-ducts.md` § Branch duct from a terminal |
| [`recipes/connect-equipment-to-air-terminals.cs`](recipes/connect-equipment-to-air-terminals.cs) | Full system in one pass: equipment supply connector → main trunk → tap+branch+elbow+drop per terminal → extend main past last branch → end cap → verify — the user's connection method end-to-end — ✓ verified live 2026-07-26 (6 terminals, 0 failures, warning-free) | `../knowledge/live-model/hvac-ducts.md` § The user's connection method |
| [`recipes/verify-duct-connectivity.cs`](recipes/verify-duct-connectivity.cs) | Trace every terminal's full connector chain to its FCU — ✓ mechanism verified 2026-07-23 (0-FCU state correctly detected; positive path needs a real FCU) | `../knowledge/live-model/hvac-ducts.md` (orphan-recovery trace) |
| [`recipes/slice-trunk-for-sizing.cs`](recipes/slice-trunk-for-sizing.cs) | HIGH RISK — slice a main trunk at each takeoff (grouped, checkerboard-aware), offset past the fitting body, for later per-segment sizing; joint held by a real Union fitting (2026-07-26 fix, technique live-proven that day on 4 trunks via inline build — bare ConnectTo joints get silently re-merged) — code-reviewed 2026-07-23; defends the BreakCurve Id-swap gotcha, see header for the open `trunkDir` sign caution | `../knowledge/live-model/hvac-ducts.md` § Slicing a main trunk into segments for duct sizing |
| [`recipes/split-duct-near-equipment.cs`](recipes/split-duct-near-equipment.cs) | Split a duct at a fixed gap from an equipment connector, joint held by a real Union fitting (2026-07-26 fix: bare ConnectTo joints can be silently re-merged) — NOT a standing default, only on explicit request — ✓ verified 2026-07-23 (BreakCurve Id-swap bug found+fixed, story in header), union fix not yet live-run | `../knowledge/live-model/hvac-ducts.md` § Splitting an existing duct into two segments at a given point |
| [`recipes/create-revisions-from-sheet-dates.cs`](recipes/create-revisions-from-sheet-dates.cs) | Scan sheet TextNotes for dates, create one project-level Revision per distinct date, oldest first | `../knowledge/live-model/revisions.md` |
| [`recipes/tag-elements-in-active-view.cs`](recipes/tag-elements-in-active-view.cs) | Tag every element of one category in the active view with a working L-shaped leader — direct live-model alternative to clicking Smart MEP Tags; simplified placement, not full clash-scoring | `../knowledge/live-model/tagging.md` § AJTools internal classes unreachable from scripts |
| [`recipes/ray-trace-to-ceiling.cs`](recipes/ray-trace-to-ceiling.cs) | Ray-cast straight up from each element to the nearest ceiling above it and snap the element's height to the hit point | the user's own idea (2026-07-14); positive case not yet live-verified — no Ceiling exists in this model yet |
| [`recipes/create-parametric-box-family-with-duct-connector.cs`](recipes/create-parametric-box-family-with-duct-connector.cs) | Family Editor authoring (not project-doc editing): set category, build a parametric box body extrusion + optional rectangular neck stub + duct connector, all resizable via Length/Width/Height/Neck Width/Neck Height/Neck Depth parameters — code-reviewed 2026-07-23, NOT live-executed (requires activating a Family Editor document, a visible workspace change not made without asking first) | `../knowledge/live-model/families.md` § Building a parametric family from scratch |
| [`recipes/create-mep-line-standards.cs`](recipes/create-mep-line-standards.cs) | One-click MEP drafting line standard: line patterns (ISO 128 / ASME Y14.2 basis), MEP_-prefixed line styles capped at weight 3, object styles (matchline/callout/scope box/ref planes/grids), 2 filled region types, and a `MEP_Line_Styles_Legend` drafting view — idempotent, safe to re-run; deliberately NO system/service styles (the user's rule) | office standard — full rules in the script header |
| [`recipes/create-mep-text-standards.cs`](recipes/create-mep-text-standards.cs) | One-click MEP text standard: 120 Arial text note types (6 sizes x 10 colours x box/no-box, `MEP_Anno_Arial_…` naming, Black = no suffix) plus a `MEP_Text_Styles_Legend` drafting view — idempotent, only missing types are created | office standard — full rules (exact RGB per colour) in the script header |
| [`recipes/model-health-audit.cs`](recipes/model-health-audit.cs) | One read-only whole-model health report — warnings by severity + top offenders, in-place families, embedded CAD imports, unenclosed Rooms/Spaces, views not on sheets, groups, unused templates/filters (dry-run counts); each section names its drill-down fragment — ✓ verified 2026-07-26 (found 3 unenclosed rooms, 16 views off sheets, 16 unused templates); its output wording had to be softened so the destructive-op guard stops refusing a read-only script | tool-gap backlog 2026-07-26 |
| [`recipes/place-sleeves-at-wall-penetrations.cs`](recipes/place-sleeves-at-wall-penetrations.cs) | Find every duct/pipe crossing through straight host-model walls (centerline method) and — after the dry-run count is approved — place a sleeve family at each, rotated to the run, sized service + clearance; curved walls / non-straight runs skipped+counted — NOT yet live-verified, needs a sleeve family fixture (2026-07-26 round 2) | round-2 suggestions 2026-07-26 |

### Commands (no element set)
The original 6 live-verified 2026-07-22; the 2 marked below are newer and not yet run.
| Command | Job |
|---|---|
| [`commands/native-undo.cs`](commands/native-undo.cs) | Revert the last transaction via Revit's own Undo — ✓ verified precisely (throwaway-element round trip) |
| [`commands/unhide-all-active-view.cs`](commands/unhide-all-active-view.cs) | Restore permanently hidden elements and clear Temporary Hide/Isolate in the active view |
| [`commands/command-regenerate.cs`](commands/command-regenerate.cs) | Force `Document.Regenerate()` — for a composed script where a later step depends on geometry/properties an earlier step just changed — ✓ (standalone-transaction fix in header) |
| [`commands/command-clear-selection.cs`](commands/command-clear-selection.cs) | Clear the active Revit selection |
| [`commands/command-activate-view.cs`](commands/command-activate-view.cs) | Switch the active view to a given View/ViewSheet |
| [`commands/command-zoom-to-fit.cs`](commands/command-zoom-to-fit.cs) | Zoom the active view's open UI window to fit its current content |
| [`commands/command-compact-save.cs`](commands/command-compact-save.cs) | Save with Compact = true (Revit's "Compact File") + before/after file size — writes the file on disk, go-ahead first — NOT yet live-verified (2026-07-26 gap backlog) |
| [`commands/command-sync-with-central.cs`](commands/command-sync-with-central.cs) | Synchronize with Central + relinquish all (comment, save-local flags) — BLOCKED (model isn't workshared), graceful path only; sync writes the central, go-ahead first (2026-07-26 gap backlog) |

### Context (whole-document, read-only orientation — no element set, model never changes)
The original 9 live-verified 2026-07-22, zero bugs; newer ones marked individually.
| Fragment | Job |
|---|---|
| [`context/context-active-view.cs`](context/context-active-view.cs) | Session snapshot — Revit version, active model (family/project, worksharing, open docs) + active view name/type/scale/level, screen Right/Up directions, open views, selection count. Standing follow-up to every successful ping (core.md rule) |
| [`context/context-project-units.cs`](context/context-project-units.cs) | Every unit spec valid for this document and its current display unit (mm/m, CFM/L/s, etc.) |
| [`context/context-all-warnings.cs`](context/context-all-warnings.cs) | Every model warning — severity, description, failing element Ids; optional Error-only filter |
| [`context/context-workset-info.cs`](context/context-workset-info.cs) | Worksharing on/off, and every user workset with open/closed state and owner |
| [`context/context-model-categories.cs`](context/context-model-categories.cs) | Model categories, keyword-filterable (avoid an unfiltered full-model dump) |
| [`context/context-used-families.cs`](context/context-used-families.cs) | Every loadable family in the model, excluding system and in-place families |
| [`context/context-design-options.cs`](context/context-design-options.cs) | Every Design Option — name, Id, Primary flag — orientation step before `filter-by-design-option.cs`/`action-set-design-option.cs` |
| [`context/context-levels-and-grids.cs`](context/context-levels-and-grids.cs) | Every Level (name + elevation) and Grid (name) — feeds `create-dimension.cs`, `filter-by-grid.cs`, `filter-by-levels.cs` |
| [`context/context-linked-models.cs`](context/context-linked-models.cs) | Every RVT link — loaded/unloaded status, pinned, workset — orientation step before `filter-by-links.cs`/`filter-by-linked-model-elements.cs`/`action-reload-links.cs` |
| [`context/context-shared-coordinates.cs`](context/context-shared-coordinates.cs) | Project Base Point, Survey Point, active Project Location, True North rotation — reported in m + mm — the "is this model sitting/rotated right" orientation step — ✓ verified 2026-07-26 (note in header: a project can report more than one non-shared BasePoint) |

"Current selection" is already covered by [`filters/by-status/filter-by-current-selection.cs`](filters/by-status/filter-by-current-selection.cs) — not duplicated here.

### Examples (fully assembled)
| Example | Demonstrates |
|---|---|
| [`examples/color-isolate-select-by-size.cs`](examples/color-isolate-select-by-size.cs) | filter-by-category-and-numeric-param + 3 chained actions, the user's own worked scenario |
| [`examples/purge-unused-view-templates.cs`](examples/purge-unused-view-templates.cs) | filter-by-view-templates.cs (usage="unused") + action-delete-elements.cs — a destructive composition, run the filter alone first per the file's own MANDATORY note |


## The rules that apply to every script

## Always report the Element ID for specific elements

Any time output names/reports on **specific elements** (not a bare count) — a report table, a "here's
what I found/changed" list, a list of elements needing a decision — include each one's **Element ID** in
the output. It's the one identifier guaranteed unique per element in a model (see the "Element ID" entry
in [`../knowledge/glossary.md`](../knowledge/glossary.md)), so it's what lets the user re-select, verify,
or reference that exact element later (including via
[`filters/by-identity/filter-by-id-list.cs`](filters/by-identity/filter-by-id-list.cs)). The `action-report-*` fragments already
do this by default — keep that default on when writing a new one, and don't drop it just to shorten
output.

## Naming conventions (audited 2026-07-26 — keep new files inside these rules)

| Folder | Filename pattern | Example |
|---|---|---|
| `filters/` | `filter-by-<what>.cs` | `filter-by-size.cs` |
| `actions/**/` | `action-<verb>-<what>.cs` — always a VERB first | `action-set-view-range.cs` |
| `creators/` | `create-<what>.cs`, or the verb that's actually true | `create-duct.cs`, `load-family.cs` |
| `commands/` | `command-<what>.cs` | `command-activate-view.cs` |
| `context/` | `context-<what>.cs` | `context-project-units.cs` |
| `recipes/` | free — a plain-language description of the job | `place-sleeves-at-wall-penetrations.cs` |

Two rules matter more than the patterns:

- **Singular vs plural must never be the ONLY difference between two names.** Picking the wrong one of
  such a pair returns a confidently wrong answer instead of an error, which is the worst failure mode
  this library can have. `filter-by-level.cs`/`filter-by-levels.cs` and `filter-by-view.cs`/
  `filter-by-views.cs` were exactly that trap; they are now
  [`filter-by-elements-on-level.cs`](filters/by-location/filter-by-elements-on-level.cs) and
  [`filter-by-elements-in-view.cs`](filters/by-view-and-sheet/filter-by-elements-in-view.cs) — the name says what the
  fragment RETURNS. Keep doing that.
- **Read-only reporting actions use the `report` verb** so they sort together and read as safe at a
  glance: `action-report-*`. Nouns-first names (`action-material-takeoff.cs`) were renamed for this.

Three deliberate exceptions, kept on purpose — do not "fix" them:
[`load-family.cs`](creators/load-family.cs) (it loads from disk, it does not create — a `create-` prefix
would make the name lie), [`native-undo.cs`](commands/native-undo.cs) and
[`unhide-all-active-view.cs`](commands/unhide-all-active-view.cs) (both older, both more findable as they
are than with a `command-` prefix bolted on).

## Modular-by-default rule

A direct one-off snippet is fine for a quick live test, but if the idea is worth saving in
`scripts/`, convert it into reusable modules instead of saving the one-off shape.


## Explorer first, invoker second — for anything bulk or hard to reverse

For a request that's large in scope or not cheaply undone, **run the filter fragment alone first**
(paste just the filter, add your own `return sb.ToString();`, run it) to see the real count before
appending any action. Confirm that count matches what the user expects, *then* re-run the full composed
script with the action(s) attached. This is the same "confirm before bulk" rule already in
`START-HERE.md` and every HVAC skill — the filter/action split just makes the two steps literally separable instead of
having to mentally simulate the filter's result before running a monolithic script.

For a small, cheap, easily-undone request, just run the composed script directly — don't add ceremony
that doesn't earn its cost.

## Transaction safety — explicit rollback, never a silent throw

Every action fragment (and every `recipes/` script) wraps its `Transaction` in a try/catch that calls
`.RollBack()` and appends a clear reason to `sb` on failure, instead of letting an exception propagate
as a bare, uninformative error through the bridge. For a `recipes/` script with multiple dependent
transactions (draw a duct, then cap it), the whole sequence runs inside one `TransactionGroup` —
`group.Assimilate()` only on full success, `group.RollBack()` on any failure — so a mid-sequence error
can never leave a half-built result behind (e.g. a duct drawn but never capped). This came directly out
of a real incident: `draw-main-duct-with-cap.cs` once left an inconsistent model state after a partial
failure, which is exactly the failure mode a `TransactionGroup` prevents. Apply this same pattern
whenever a `recipes/` script is next touched, even if it hasn't been updated yet.

## Every number is a per-request input — never a default

Same rule as everywhere else in this project. Every fragment's `INPUTS` block (size in mm, color,
parameter name, room id, comparison operator) came from a specific request. Restate/confirm the current
one before running — the pre-filled values exist so there's one obvious place to edit, not because
they're safe to reuse blindly.


## How to compose two or more fragments into one script

1. Pick the filter fragment that matches the request; open it and read its `INPUTS` block.
2. Pick one or more action fragments; read each one's `INPUTS` block too.
3. Paste the filter fragment's body first, then each action fragment's body in the order they should
   run, into one script. Every fragment shares the same two variable names — `elements` and `sb` — so
   they chain without any glue code. None of them end in `return`; you add exactly one
   `return sb.ToString();` as the very last line of the whole composed script.
4. Fill in every `INPUTS` block with today's actual values — nothing pre-filled in these files is a
   default, per the rule below.
5. Run the composed script via `mcp__aj-tools-aj-ai__run_csharp`.

If the native MCP tool is not exposed in the current agent session, do not spend time re-reading
`mcp-server/index.js` or hand-writing a named-pipe wrapper. Use the checked-in fallback helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ..\tools\invoke-bridge.ps1 -Ping
powershell -NoProfile -ExecutionPolicy Bypass -File ..\tools\invoke-bridge.ps1 -CodeFile <composed-script.cs>
```

This is a fallback only. If `mcp__aj-tools-aj-ai__run_csharp` is available, use the native MCP tool
directly.


## Before writing new AJ AI Bridge C#

Check `filters/` and `actions/` first — compose from what's there rather than writing a filter or an
action from scratch. Only write a new fragment if nothing existing covers the job; only write a
one-off, non-fragment script if it's genuinely not going to repeat (and even then, consider whether it's
actually a `recipe` in disguise).

## After running something new

If what you wrote (or composed) used a new *kind* of filter or action not covered here, save it as its
own fragment — or update the closest existing one — following the naming pattern
(`filter-by-<what>.cs`, `action-<verb>-<what>.cs`). If it was a true one-off, don't save it. Always
verify a fragment's result against the real model with a fresh read-back after running it (same
verify-don't-trust rule as `START-HERE.md`).


## After adding, updating, or retiring a fragment

Add one short dated line to [`../knowledge/brain-log.md`](../knowledge/brain-log.md) — same as any other
change to this Brain. If a fragment is retired because the job it did doesn't come up anymore, say so and
delete it rather than leaving a stale file that looks current.
