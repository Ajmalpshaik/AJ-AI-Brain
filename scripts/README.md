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
| Fragment | Job |
|---|---|
| [`filter-by-category.cs`](filters/filter-by-category.cs) | Every instance of one category, optional level scope |
| [`filter-by-category-and-family.cs`](filters/filter-by-category-and-family.cs) | Category narrowed to a family name (VCD-style) |
| [`filter-by-category-and-numeric-param.cs`](filters/filter-by-category-and-numeric-param.cs) | Category narrowed by a numeric parameter vs. an mm value (the "500mm duct" filter) |
| [`filter-by-length.cs`](filters/filter-by-length.cs) | Category narrowed by Length (mm) vs. an mm value — bound to `CURVE_ELEM_LENGTH` directly |
| [`filter-by-size.cs`](filters/filter-by-size.cs) | Category narrowed by size — round (Diameter) and rectangular (Width x Height) handled together, or a plain "Size" text match |
| [`filter-by-room.cs`](filters/filter-by-room.cs) | Category narrowed to instances physically inside one room, matched by Id, Name, and/or Number |
| [`filter-by-system-type.cs`](filters/filter-by-system-type.cs) | Pipes/ducts/fittings narrowed by MEP System TYPE/classification (e.g. "CDP", "Supply Air") |
| [`filter-by-system-name.cs`](filters/filter-by-system-name.cs) | Pipes/ducts/fittings narrowed to one specific System instance's own name (e.g. "DXS 1") |
| [`filter-by-current-selection.cs`](filters/filter-by-current-selection.cs) | Whatever's currently selected in Revit |
| [`filter-by-category-name.cs`](filters/filter-by-category-name.cs) | Category resolved by plain display name, not the BuiltInCategory enum |
| [`filter-by-region.cs`](filters/filter-by-region.cs) | Category narrowed to instances whose bounding box intersects a given mm region |
| [`filter-by-multiple-categories.cs`](filters/filter-by-multiple-categories.cs) | Several categories collected as one group, e.g. duct system / pipe system / cable tray system |
| [`filter-by-parameter-text.cs`](filters/filter-by-parameter-text.cs) | Category or whole-model scan narrowed by text in family/type/parameter values |
| [`filter-by-workset.cs`](filters/filter-by-workset.cs) | Elements on one user workset, optional category scope |
| [`filter-by-sheets.cs`](filters/filter-by-sheets.cs) | Every ViewSheet, optional sheet-number substring |
| [`filter-by-phase.cs`](filters/filter-by-phase.cs) | Elements matching a named Phase Created and/or Phase Demolished, optional category scope |
| [`filter-by-id-list.cs`](filters/filter-by-id-list.cs) | A specific list of Element Ids the user already has — "what is this element / what are its parameters" |
| [`filter-by-space.cs`](filters/filter-by-space.cs) | Category narrowed to instances physically inside one MEP Space (not a Room), matched by Id, Name, and/or Number — not yet live-verified |
| [`filter-by-family.cs`](filters/filter-by-family.cs) | Family name matched across the WHOLE model, no category picked first |
| [`filter-by-family-type.cs`](filters/filter-by-family-type.cs) | A specific Type inside a Family, matched by name (e.g. one exact fitting size) |
| [`filter-by-view.cs`](filters/filter-by-view.cs) | Category narrowed to instances actually visible in a given view (any view, not just active) |
| [`filter-by-element-intersection.cs`](filters/filter-by-element-intersection.cs) | Elements whose real geometry intersects one specific target element (`ElementIntersectsElementFilter`) |
| [`filter-by-solid-intersection.cs`](filters/filter-by-solid-intersection.cs) | Elements whose real geometry intersects a custom 3D box/clearance solid (`ElementIntersectsSolidFilter`) — not yet live-verified |
| [`filter-by-host.cs`](filters/filter-by-host.cs) | Elements hosted on a specific parent (`FamilyInstance.Host` or insulation/lining `HostElementId`) — not yet live-verified |
| [`filter-by-assembly.cs`](filters/filter-by-assembly.cs) | Member elements of a specific Revit Assembly (`AssemblyInstance`) — not yet live-verified |
| [`filter-by-group.cs`](filters/filter-by-group.cs) | Member elements of a specific Model Group instance |
| [`filter-by-parameter-exists.cs`](filters/filter-by-parameter-exists.cs) | Elements that have a given parameter attached, whether blank or not — QA sweep, distinct from `filter-by-parameter-text.cs`'s value match |
| [`filter-by-design-option.cs`](filters/filter-by-design-option.cs) | Elements in a named Design Option, or the Main Model when left unset — not yet live-verified |
| [`filter-by-material.cs`](filters/filter-by-material.cs) | Elements using a specific Revit Material, category-scoped |
| [`filter-by-level.cs`](filters/filter-by-level.cs) | Everything on a given Level across the WHOLE model, optional category scope |
| [`filter-by-tag-status.cs`](filters/filter-by-tag-status.cs) | Category elements that ARE or ARE NOT tagged in a given view |
| [`filter-by-connection-status.cs`](filters/filter-by-connection-status.cs) | Category elements with at least one open connector end, or fully connected |
| [`filter-by-pin-status.cs`](filters/filter-by-pin-status.cs) | Category elements that ARE or ARE NOT pinned |
| [`filter-by-views.cs`](filters/filter-by-views.cs) | Every View (not ViewSheet), optional ViewType + name filter |
| [`filter-by-view-templates.cs`](filters/filter-by-view-templates.cs) | View Templates themselves, optional name filter + usage mode (all/used/unused) — makes templates composable with any action (rename, report, delete, ...) instead of needing a bespoke fragment |
| [`filter-by-warnings.cs`](filters/filter-by-warnings.cs) | Elements flagged by a current model warning, as an actionable set |
| [`filter-by-electrical-system.cs`](filters/filter-by-electrical-system.cs) | Elements in a specific Electrical System (circuit), by Circuit Type and/or circuit name — not yet live-verified |
| [`filter-by-insulation-status.cs`](filters/filter-by-insulation-status.cs) | Pipe/duct elements that HAVE insulation/lining applied, or don't — not yet live-verified |
| [`filter-by-insulation-type.cs`](filters/filter-by-insulation-type.cs) | The insulation/lining elements themselves, by kind/type/material/thickness — not yet live-verified |
| [`filter-by-grid.cs`](filters/filter-by-grid.cs) | Every Grid, optional name substring — feeds `creators/create-dimension.cs` |
| [`filter-by-levels.cs`](filters/filter-by-levels.cs) | Every Level ELEMENT itself (not elements sitting on one — that's `filter-by-level.cs`), ordered by elevation |
| [`filter-by-schedules.cs`](filters/filter-by-schedules.cs) | Every ViewSchedule, optional name substring — feeds `action-export-schedule-to-csv.cs`/`action-place-schedule-on-sheet.cs` |
| [`filter-by-selection-filter.cs`](filters/filter-by-selection-filter.cs) | Read back the actual elements behind an existing named Selection Filter, or re-evaluate a View Filter's rule in a given view — not yet live-verified |
| [`filter-by-unenclosed-spatial-elements.cs`](filters/filter-by-unenclosed-spatial-elements.cs) | QA sweep — every Room/Space in the model with zero Area ("Not Enclosed") |
| [`filter-by-types.cs`](filters/filter-by-types.cs) | The TYPE elements themselves (FamilySymbol or system-family type), matched by family/type name — reaches a type with zero placed instances, unlike the instance-derived type actions |

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
| [`action-create-view-filter.cs`](actions/color-graphics/action-create-view-filter.cs) | Create a Revit VIEW FILTER (`ParameterFilterElement`, the Visibility/Graphics > Filters tab rule mechanism — NOT this repo's `filters/` folder) — persists, auto-applies to future elements too; every rule kind (contains/equals/begins/ends + not-variants, numeric eq/gt/gte/lt/lte/noteq, has-value/has-no-value) — does NOT consume `elements` — not yet live-verified |
| [`action-create-selection-filter.cs`](actions/color-graphics/action-create-selection-filter.cs) | Save `elements` as a named Revit SELECTION FILTER (`SelectionFilterElement`) — an explicit element list instead of a rule, for when the set doesn't share one clean parameter condition — not yet live-verified |
| [`action-apply-view-filter.cs`](actions/color-graphics/action-apply-view-filter.cs) | Add an existing filter (View Filter OR Selection Filter — looked up by the shared `FilterElement` base) to a view with a color/visibility, or update it if already applied — does NOT consume `elements` |
| [`action-remove-view-filter.cs`](actions/color-graphics/action-remove-view-filter.cs) | Take a filter (either kind) off a view, optionally delete it from the document entirely — does NOT consume `elements` |
| [`action-set-halftone.cs`](actions/color-graphics/action-set-halftone.cs) | Turn halftone on/off per element — read-modify-write, preserves any existing color override |
| [`action-set-category-halftone.cs`](actions/color-graphics/action-set-category-halftone.cs) | Turn halftone on/off for one or more ENTIRE categories — does NOT consume `elements` |
| [`action-set-line-style.cs`](actions/color-graphics/action-set-line-style.cs) | Override line weight and/or line pattern (dashed, dotted, ...) per element — every other action here only ever touches color |
| [`action-set-category-line-style.cs`](actions/color-graphics/action-set-category-line-style.cs) | Override line weight/pattern for one or more ENTIRE categories — does NOT consume `elements` |
| [`action-report-view-filters.cs`](actions/color-graphics/action-report-view-filters.cs) | List every View/Selection Filter in the document and which views use each — does NOT consume `elements` |
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

**Selection** — [`actions/selection/`](actions/selection/)
| Fragment | Job |
|---|---|
| [`action-select-elements.cs`](actions/selection/action-select-elements.cs) | Set, add to, or remove from the active Revit selection (`mode`) |

**Parameters & Naming** — [`actions/parameters-naming/`](actions/parameters-naming/)
| Fragment | Job |
|---|---|
| [`action-set-parameter-value.cs`](actions/parameters-naming/action-set-parameter-value.cs) | Bulk-set one parameter across the set — falls back to the Type if it's not an instance parameter |
| [`action-add-parameter-prefix-suffix.cs`](actions/parameters-naming/action-add-parameter-prefix-suffix.cs) | Add a prefix/suffix, or find/replace a substring, INSIDE a parameter's existing text (any String parameter, any category) — falls back to Type, deduped so a shared type isn't stacked; not yet live-verified |
| [`action-copy-parameter-value.cs`](actions/parameters-naming/action-copy-parameter-value.cs) | Copy one parameter's value into a different parameter, storage-type-aware — source and target each independently fall back to Type |
| [`action-remove-parameter-value.cs`](actions/parameters-naming/action-remove-parameter-value.cs) | Clear one parameter's value — genuinely empty for String/ElementId, zeroed (not truly unset) for Double/Integer — falls back to Type |
| [`action-renumber-sequential.cs`](actions/parameters-naming/action-renumber-sequential.cs) | Assign a sequential value (prefix/number/padding/suffix) to a String parameter, sorted by position or existing value |
| [`action-rename-element.cs`](actions/parameters-naming/action-rename-element.cs) | Rename each element via `Element.Name` (views, sheets, levels, types — not most instance geometry); not yet live-verified |
| [`action-rename-family.cs`](actions/parameters-naming/action-rename-family.cs) | Bulk-rename the FAMILY behind a set of instances (resolves instance → Symbol → Family, dedupes so each family is renamed once) — prefix, suffix, find/replace, or flat replace modes, e.g. add `AJ_` in front of every Duct Accessory family name; not yet live-verified |
| [`action-create-phase.cs`](actions/parameters-naming/action-create-phase.cs) | Create one or more new project Phases, appended after the current last one — does NOT consume `elements` — not yet live-verified |
| [`action-rename-phase.cs`](actions/parameters-naming/action-rename-phase.cs) | Rename one or more existing project Phases — does NOT consume `elements` — not yet live-verified |
| [`action-set-element-phase.cs`](actions/parameters-naming/action-set-element-phase.cs) | Assign the filtered set's Phase Created and/or Phase Demolished to a named Phase — `action-set-parameter-value.cs` can't do this (no ElementId support) |
| [`action-report-phases.cs`](actions/parameters-naming/action-report-phases.cs) | List every project Phase in order — does NOT consume `elements` |
| [`action-delete-phase.cs`](actions/parameters-naming/action-delete-phase.cs) | Permanently delete one or more Phases by name — completes the phase lifecycle — does NOT consume `elements` — not yet live-verified |
| [`action-set-workset.cs`](actions/parameters-naming/action-set-workset.cs) | Assign elements to a named user workset (write counterpart to `filter-by-workset.cs`) — not yet live-verified |
| [`action-set-design-option.cs`](actions/parameters-naming/action-set-design-option.cs) | Add elements to a named Design Option via the copy-while-active-option workaround (write counterpart to `filter-by-design-option.cs`, no direct public setter exists) — higher-uncertainty, not yet live-verified |

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
| [`action-material-takeoff.cs`](actions/reporting/action-material-takeoff.cs) | Material area/volume quantities across `elements`, grouped by material |
| [`action-length-by-size.cs`](actions/reporting/action-length-by-size.cs) | Count + total length per size group, for linear MEP elements (duct/pipe/cable tray) |
| [`action-report-room-space-data.cs`](actions/reporting/action-report-room-space-data.cs) | Area/Volume/Level/Occupancy table for Rooms or Spaces; read-only |

**QA Checks** — [`actions/qa-checks/`](actions/qa-checks/)
| Fragment | Job |
|---|---|
| [`action-find-duplicates.cs`](actions/qa-checks/action-find-duplicates.cs) | QA check — flag elements whose insertion points sit within a tolerance of each other (duplicate LOCATION); read-only, optional select |
| [`action-find-duplicate-values.cs`](actions/qa-checks/action-find-duplicate-values.cs) | QA check — flag elements sharing the same value in a named parameter, e.g. duplicate Mark (duplicate DATA, not location); read-only, optional select |
| [`action-find-blank-parameter.cs`](actions/qa-checks/action-find-blank-parameter.cs) | QA check — flag elements where a named parameter is blank/unset (falls back to Type); read-only, optional select |
| [`action-report-clashes.cs`](actions/qa-checks/action-report-clashes.cs) | Basic clash report — real geometry intersection between `elements` (set A) and a second category (set B); read-only, not yet live-verified |

**Move / Copy / Rotate** — [`actions/move-copy-rotate/`](actions/move-copy-rotate/)
| Fragment | Job |
|---|---|
| [`action-move-elements.cs`](actions/move-copy-rotate/action-move-elements.cs) | Translate every element by one mm offset vector |
| [`action-copy-elements.cs`](actions/move-copy-rotate/action-copy-elements.cs) | Duplicate every element, offset by one mm vector; produces `newElementIds` for chaining |
| [`action-rotate-elements.cs`](actions/move-copy-rotate/action-rotate-elements.cs) | Rotate every element around a vertical axis by one angle, about its own location or a given pivot |
| [`action-mirror-elements.cs`](actions/move-copy-rotate/action-mirror-elements.cs) | Mirror every element across a vertical plane through two plan points — copy or in-place |
| [`action-offset-elements.cs`](actions/move-copy-rotate/action-offset-elements.cs) | Offset each linear element sideways by a perpendicular distance (mm) — copy or in-place; not yet live-verified |
| [`action-trim-extend-elements.cs`](actions/move-copy-rotate/action-trim-extend-elements.cs) | Trim or extend exactly 2 linear elements to meet at their computed corner — not yet live-verified |
| [`action-fillet-elements.cs`](actions/move-copy-rotate/action-fillet-elements.cs) | Round the corner between exactly 2 elements — real elbow fitting for MEP curves, or a geometric tangent arc for Model/Detail lines; not yet live-verified |
| [`action-array-elements.cs`](actions/move-copy-rotate/action-array-elements.cs) | Multiple evenly-spaced copies — linear (fixed mm spacing) or radial (swept around a center); not yet live-verified |
| [`action-align-elements.cs`](actions/move-copy-rotate/action-align-elements.cs) | Snap every element to match one reference element's X/Y/Z position — Revit's "Align" tool; not yet live-verified |

**Structural Changes** — [`actions/structural-changes/`](actions/structural-changes/)
| Fragment | Job |
|---|---|
| [`action-change-element-type.cs`](actions/structural-changes/action-change-element-type.cs) | Bulk-swap every element's type to a different named type within the same family |
| [`action-delete-elements.cs`](actions/structural-changes/action-delete-elements.cs) | Permanently delete every element in the set — highest-risk fragment, explorer-first is mandatory, needs `allowDestructive: true` on the bridge call too |
| [`action-group-elements.cs`](actions/structural-changes/action-group-elements.cs) | Bundle the filtered set into a new Model Group |
| [`action-ungroup-elements.cs`](actions/structural-changes/action-ungroup-elements.cs) | Dissolve Group instances in the set back into their members — paired undo for `action-group-elements.cs` |
| [`action-join-geometry.cs`](actions/structural-changes/action-join-geometry.cs) | Join (or unjoin) every element in the set with one target element — many-to-one |
| [`action-split-elements.cs`](actions/structural-changes/action-split-elements.cs) | Split each Duct/Pipe at a point along its own length (fraction or mm from start), auto-reconnects the joint — generalized from `recipes/split-duct-near-equipment.cs`; not yet live-verified |
| [`action-purge-unused.cs`](actions/structural-changes/action-purge-unused.cs) | Delete unused View Templates, Filters, or Materials — the subset of native Purge Unused provably correct from the public API; does NOT consume `elements`, dry-run by default |
| [`action-duplicate-type.cs`](actions/structural-changes/action-duplicate-type.cs) | Duplicate the distinct TYPE(s) behind a set of instances under a new name (prefix/suffix/fixed) — Type-level counterpart to `action-rename-family.cs`; not yet live-verified |

**Sheets & Views** — [`actions/sheets-views/`](actions/sheets-views/)
| Fragment | Job |
|---|---|
| [`action-place-viewport-on-sheet.cs`](actions/sheets-views/action-place-viewport-on-sheet.cs) | Place each view in the set onto one sheet as a Viewport (views can only sit on one sheet at a time) |
| [`action-place-schedule-on-sheet.cs`](actions/sheets-views/action-place-schedule-on-sheet.cs) | Place each schedule onto one sheet — same schedule can be placed on multiple sheets, no duplication needed |
| [`action-duplicate-views.cs`](actions/sheets-views/action-duplicate-views.cs) | Duplicate/duplicate-with-detailing/dependent-view each view in the set; produces `newViewIds` |
| [`action-apply-view-template.cs`](actions/sheets-views/action-apply-view-template.cs) | Apply an existing View Template to one or more views — does NOT consume `elements` |
| [`action-create-view-template-from-view.cs`](actions/sheets-views/action-create-view-template-from-view.cs) | Save a configured view's current settings as a new named View Template — does NOT consume `elements` — not yet live-verified |
| [`action-set-view-template-controlled-params.cs`](actions/sheets-views/action-set-view-template-controlled-params.cs) | Include/Exclude which parameters a View Template controls on one view — does NOT consume `elements` — not yet live-verified |
| [`action-remove-view-template.cs`](actions/sheets-views/action-remove-view-template.cs) | Detach a View Template from one or more views, optionally delete it from the document entirely — the paired undo for `action-apply-view-template.cs` — does NOT consume `elements` |
| [`action-duplicate-view-template.cs`](actions/sheets-views/action-duplicate-view-template.cs) | Duplicate an existing View Template (by name) into a new, separately-named template — does NOT consume `elements` |
| [`action-report-view-template-status.cs`](actions/sheets-views/action-report-view-template-status.cs) | Report whether one or more views have a View Template applied — which one, and which parameters are excluded from its control; read-only — does NOT consume `elements` |
| [`action-export-sheets-to-pdf.cs`](actions/sheets-views/action-export-sheets-to-pdf.cs) | Batch-export ViewSheets to PDF, combined or one file per sheet; not yet live-verified |
| [`action-set-view-properties.cs`](actions/sheets-views/action-set-view-properties.cs) | Batch-set Scale, Detail Level, and/or Visual Style across views — the lightweight direct version of applying a View Template; not yet live-verified |
| [`action-tag-elements.cs`](actions/sheets-views/action-tag-elements.cs) | Simple tag placement (fixed offset, optional leader) in one view — reuses the proven `IndependentTag.Create` pattern from `recipes/tag-elements-in-active-view.cs` without its clash-scoring; not yet live-verified |
| [`action-remove-tags.cs`](actions/sheets-views/action-remove-tags.cs) | Delete IndependentTag elements in the set — paired undo for `action-tag-elements.cs` |
| [`action-export-schedule-to-csv.cs`](actions/sheets-views/action-export-schedule-to-csv.cs) | Export ViewSchedules to CSV via Revit's native `ViewSchedule.Export` — one file per schedule; not yet live-verified |

**Sheet Dates & Revisions** — [`actions/sheet-dates-revisions/`](actions/sheet-dates-revisions/)
| Fragment | Job |
|---|---|
| [`action-extract-dates-from-textnotes.cs`](actions/sheet-dates-revisions/action-extract-dates-from-textnotes.cs) | Scan every TextNote on each sheet for date-like text, report distinct dates + source sheet(s), read-only |
| [`action-assign-revisions-by-sheet-date.cs`](actions/sheet-dates-revisions/action-assign-revisions-by-sheet-date.cs) | Attach each sheet's matching project Revision(s) via `SetAdditionalRevisionIds`, matched by date found in that sheet's TextNotes — writes the model, see gotcha note in `../knowledge/live-model/revisions.md` |
| [`action-remove-revision-from-sheet.cs`](actions/sheet-dates-revisions/action-remove-revision-from-sheet.cs) | Detach named Revision(s) from each sheet, matched by Description — the reverse of `action-assign-revisions-by-sheet-date.cs` |

### Creators (produce `elements` by creating new ones)
| Fragment | Job |
|---|---|
| [`create-levels.cs`](creators/create-levels.cs) | Batch-create levels, evenly spaced or at explicit elevations |
| [`create-material.cs`](creators/create-material.cs) | Create one or more Materials with a set colour and transparency |
| [`create-point-based-element.cs`](creators/create-point-based-element.cs) | Place a family instance at one or more points on a level |
| [`create-room.cs`](creators/create-room.cs) | Place a Room at one or more points on a level |
| [`create-space.cs`](creators/create-space.cs) | Place an MEP Space at one or more points on a level — Space-category equivalent of create-room.cs |
| [`create-sheet.cs`](creators/create-sheet.cs) | Create one or more new sheets with a chosen title block |
| [`create-schedule.cs`](creators/create-schedule.cs) | Create a bare schedule for a category with chosen fields — chain into `action-place-schedule-on-sheet.cs`; not yet live-verified |
| [`create-text-note.cs`](creators/create-text-note.cs) | Place one or more Text Notes at given points in a view |
| [`create-dimension.cs`](creators/create-dimension.cs) | Create a dimension string across 2+ Grids/Levels — deliberately scoped to Grid/Level references only, not arbitrary element geometry; higher-uncertainty, not yet live-verified |
| [`create-grid.cs`](creators/create-grid.cs) | Create one or more straight Grids from mm endpoint pairs |
| [`create-view.cs`](creators/create-view.cs) | Create a Floor Plan, 3D, or Section view — the three simple/reliable ViewFamily cases, not Callout/Elevation/Drafting |
| [`create-revision.cs`](creators/create-revision.cs) | Create one or more Revisions directly (date/description/issued-to/by already known) — plain version of `recipes/create-revisions-from-sheet-dates.cs` |
| [`create-workset.cs`](creators/create-workset.cs) | Create one or more new user Worksets — feeds `action-set-workset.cs`; produces no `elements` (Workset isn't an Element) |

### Recipes (bespoke multi-stage builds, not filter+action shaped)
| Recipe | Job | Source |
|---|---|---|
| [`recipes/trace-mep-circuits.cs`](recipes/trace-mep-circuits.cs) | Bulk-cluster a filtered pipe/duct system into physical circuits and find real endpoints | `../knowledge/live-model/mep-trace.md` § Tracing real MEP connectivity |
| [`recipes/set-space-airflow.cs`](recipes/set-space-airflow.cs) | Create/find each room's MEP Space, set Supply/Return Airflow, cascade to existing terminals | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-terminals-checkerboard.cs`](recipes/place-terminals-checkerboard.cs) | Place a room's supply/return terminals in a near-square checkerboard grid | `../knowledge/live-model/hvac-terminals.md` § HVAC air terminal layout |
| [`recipes/place-fcu.cs`](recipes/place-fcu.cs) | Place an FCU, reposition toward the door, rotate to face terminals | `../knowledge/live-model/hvac-ducts.md` § Placing equipment relative to a door |
| [`recipes/draw-main-duct-with-cap.cs`](recipes/draw-main-duct-with-cap.cs) | Draw a sized main duct from the FCU and cap every open end correctly | `../knowledge/live-model/hvac-ducts.md` § Drawing a duct, § cap-end recipe |
| [`recipes/connect-terminal-branch.cs`](recipes/connect-terminal-branch.cs) | Riser + real elbow + takeoff tee connecting a terminal to the main duct | `../knowledge/live-model/hvac-ducts.md` § Branch duct from a terminal |
| [`recipes/verify-duct-connectivity.cs`](recipes/verify-duct-connectivity.cs) | Trace every terminal's full connector chain to its FCU | `../knowledge/live-model/hvac-ducts.md` (orphan-recovery trace) |
| [`recipes/slice-trunk-for-sizing.cs`](recipes/slice-trunk-for-sizing.cs) | HIGH RISK — slice a main trunk at each takeoff (grouped, checkerboard-aware), offset past the fitting body, for later per-segment sizing | `../knowledge/live-model/hvac-ducts.md` § Slicing a main trunk into segments for duct sizing |
| [`recipes/split-duct-near-equipment.cs`](recipes/split-duct-near-equipment.cs) | Split a duct at a fixed gap from an equipment connector (e.g. a future flex-duct gap at an FCU) and reconnect the joint — NOT a standing default, only on explicit request | `../knowledge/live-model/hvac-ducts.md` § Splitting an existing duct into two segments at a given point |
| [`recipes/create-revisions-from-sheet-dates.cs`](recipes/create-revisions-from-sheet-dates.cs) | Scan sheet TextNotes for dates, create one project-level Revision per distinct date, oldest first | `ajtools-conventions.md` (Revision API) |
| [`recipes/tag-elements-in-active-view.cs`](recipes/tag-elements-in-active-view.cs) | Tag every element of one category in the active view with a working L-shaped leader — direct live-model alternative to clicking Smart MEP Tags; simplified placement, not full clash-scoring | `../knowledge/live-model/tagging.md` § AJTools internal classes unreachable from scripts |
| [`recipes/ray-trace-to-ceiling.cs`](recipes/ray-trace-to-ceiling.cs) | Ray-cast straight up from each element to the nearest ceiling above it and snap the element's height to the hit point | the user's own idea (2026-07-14); positive case not yet live-verified — no Ceiling exists in this model yet |
| [`recipes/create-parametric-box-family-with-duct-connector.cs`](recipes/create-parametric-box-family-with-duct-connector.cs) | Family Editor authoring (not project-doc editing): set category, build a parametric box body extrusion + optional rectangular neck stub + duct connector, all resizable via Length/Width/Height/Neck Width/Neck Height/Neck Depth parameters | `../knowledge/live-model/families.md` § Building a parametric family from scratch |

### Commands (no element set)
| Command | Job |
|---|---|
| [`commands/native-undo.cs`](commands/native-undo.cs) | Revert the last transaction via Revit's own Undo |
| [`commands/unhide-all-active-view.cs`](commands/unhide-all-active-view.cs) | Restore permanently hidden elements and clear Temporary Hide/Isolate in the active view |
| [`commands/command-regenerate.cs`](commands/command-regenerate.cs) | Force `Document.Regenerate()` — for a composed script where a later step depends on geometry/properties an earlier step just changed |
| [`commands/command-clear-selection.cs`](commands/command-clear-selection.cs) | Clear the active Revit selection |
| [`commands/command-activate-view.cs`](commands/command-activate-view.cs) | Switch the active view to a given View/ViewSheet |
| [`commands/command-zoom-to-fit.cs`](commands/command-zoom-to-fit.cs) | Zoom the active view's open UI window to fit its current content |

### Context (whole-document, read-only orientation — no element set, model never changes)
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

"Current selection" is already covered by [`filters/filter-by-current-selection.cs`](filters/filter-by-current-selection.cs) — not duplicated here.

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
[`filters/filter-by-id-list.cs`](filters/filter-by-id-list.cs)). The `action-report-*` fragments already
do this by default — keep that default on when writing a new one, and don't drop it just to shorten
output.

## Modular-by-default rule

A direct one-off snippet is fine for a quick live test, but if the idea is worth saving in
`scripts/`, convert it into reusable modules instead of saving the one-off shape.


## Explorer first, invoker second — for anything bulk or hard to reverse

For a request that's large in scope or not cheaply undone, **run the filter fragment alone first**
(paste just the filter, add your own `return sb.ToString();`, run it) to see the real count before
appending any action. Confirm that count matches what the user expects, *then* re-run the full composed
script with the action(s) attached. This is the same "confirm before bulk" rule already in `CLAUDE.md`
and every HVAC skill — the filter/action split just makes the two steps literally separable instead of
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
verify a fragment's result against the real model with a fresh read-back after running it (Modeler
mindset in `CLAUDE.md` applies here too).


## After adding, updating, or retiring a fragment

Add one short dated line to `ajtools-conventions.md`'s Log — same as any other AJ Tools decision. If a
fragment is retired because the job it did doesn't come up anymore, say so and delete it rather than
leaving a stale file that looks current.
