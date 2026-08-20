# Which Revit API this Brain actually uses

Generated from the 285 fragments on 2026-08-20 — **not** copied from documentation. Regenerate with the
command at the bottom; do not hand-edit.

## Why this file exists instead of a copy of the API docs

Ajmal asked (2026-08-20) whether the whole Revit API from `revitapidocs.com` should live in the Brain.
The instinct is right and the scale is the problem. Measured:

| | |
|---|---|
| Revit API | ~1,700 classes, 50 interfaces, 500 enumerations, **30,000+ documented members** |
| This Brain's whole index today | **3,786 chunks** from 341 files |
| Types the 285 fragments actually use | **230** |

Indexing the API into the main search would make the Brain roughly **11% of its own index** — every
question would land on reference pages instead of on the skill or fragment that answers it. That is the
same failure already recorded here: 604 chunks of external standards were indexed on 2026-08-13 and
reverted the same hour for being a 20% increase. This would be eight times worse.

**And the API docs are not the gap.** A reference page tells you a method's signature. It does not tell
you that `FilteredElementCollector.UnionWith()` silently drops quick filters, or that
`RBS_START_LEVEL_PARAM` is the only level parameter an MEP curve has — both of which this Brain already
knows because it learned them the hard way. The fragments are **285 proven working examples**; the value
is an index of our own usage, which is what this file is.

If the full API is genuinely wanted later, it belongs in a **separate index** that Brain searches never
touch — see [`semantic-index/README.md`](../semantic-index/README.md) for why the two must not share a collection.

## The types, by how much this library leans on them

`used in N` = number of fragments. Open the named fragment to see it used correctly in context.

| Type | used in | example fragments |
|---|---|---|
| `Document` | 233 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-create-selection-filter.cs` |
| `Transaction` | 159 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-create-selection-filter.cs` |
| `FilteredElementCollector` | 150 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-create-selection-filter.cs` |
| `ElementId` | 133 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-create-view-filter.cs` |
| `Element` | 126 | `actions/color-graphics/action-color-by-group.cs`, `actions/move-copy-rotate/action-align-elements.cs`, `actions/move-copy-rotate/action-move-elements.cs` |
| `View` | 72 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-highlight-vs-rest.cs` |
| `BuiltInParameter` | 45 | `actions/color-graphics/action-color-by-group.cs`, `actions/parameters-naming/action-set-element-phase.cs`, `actions/parameters-naming/action-set-workset.cs` |
| `BuiltInCategory` | 44 | `actions/color-graphics/action-create-view-filter.cs`, `actions/reporting/action-count-by-spatial-container.cs`, `actions/reporting/action-report-coverage.cs` |
| `LocationPoint` | 30 | `actions/move-copy-rotate/action-align-elements.cs`, `actions/move-copy-rotate/action-move-elements.cs`, `actions/move-copy-rotate/action-move-to-ray-hit.cs` |
| `FamilyInstance` | 29 | `actions/move-copy-rotate/action-flip-elements.cs`, `actions/parameters-naming/action-rename-family.cs`, `actions/reporting/action-count-by-group.cs` |
| `Level` | 25 | `context/context-levels-and-grids.cs`, `context/context-session-start.cs`, `creators/create-cable-tray.cs` |
| `LocationCurve` | 22 | `actions/move-copy-rotate/action-align-elements.cs`, `actions/move-copy-rotate/action-fillet-elements.cs`, `actions/move-copy-rotate/action-move-elements.cs` |
| `Line` | 19 | `actions/move-copy-rotate/action-fillet-elements.cs`, `actions/move-copy-rotate/action-trim-extend-elements.cs`, `actions/structural-changes/action-extract-cad-curves.cs` |
| `ElementType` | 18 | `actions/parameters-naming/action-add-parameter-prefix-suffix.cs`, `actions/parameters-naming/action-copy-parameter-value.cs`, `actions/parameters-naming/action-remove-parameter-value.cs` |
| `Connector` | 16 | `actions/move-copy-rotate/action-fillet-elements.cs`, `actions/reporting/action-report-connectors.cs`, `actions/structural-changes/action-place-accessory-on-run.cs` |
| `ViewSheet` | 16 | `actions/sheet-dates-revisions/action-assign-revisions-by-sheet-date.cs`, `actions/sheet-dates-revisions/action-extract-dates-from-textnotes.cs`, `actions/sheet-dates-revisions/action-remove-revision-from-sheet.cs` |
| `FamilySymbol` | 15 | `actions/parameters-naming/action-rename-family.cs`, `actions/sheets-views/action-set-sheet-title-block.cs`, `actions/sheets-views/action-tag-elements.cs` |
| `Parameter` | 14 | `actions/parameters-naming/action-add-parameter-prefix-suffix.cs`, `actions/parameters-naming/action-copy-parameter-value.cs`, `actions/parameters-naming/action-remove-parameter-value.cs` |
| `Category` | 13 | `actions/color-graphics/action-report-category-overrides.cs`, `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/qa-checks/action-check-surface-fit.cs` |
| `ViewSchedule` | 13 | `actions/sheets-views/action-add-schedule-calculated-field.cs`, `actions/sheets-views/action-add-schedule-field.cs`, `actions/sheets-views/action-export-schedule-to-csv.cs` |
| `MEPCurve` | 10 | `actions/move-copy-rotate/action-fillet-elements.cs`, `actions/reporting/action-report-connectors.cs`, `actions/structural-changes/action-place-accessory-on-run.cs` |
| `ViewPlan` | 10 | `actions/reporting/action-plan-shortest-route.cs`, `actions/reporting/action-report-coverage.cs`, `actions/visibility/action-set-view-range.cs` |
| `Curve` | 9 | `actions/structural-changes/action-extract-cad-curves.cs`, `creators/create-filled-region.cs`, `creators/create-revision-cloud.cs` |
| `Phase` | 9 | `actions/parameters-naming/action-delete-phase.cs`, `actions/parameters-naming/action-rename-phase.cs`, `actions/parameters-naming/action-report-phases.cs` |
| `FillPatternElement` | 8 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-color-by-group.cs`, `actions/color-graphics/action-highlight-vs-rest.cs` |
| `FilteredWorksetCollector` | 8 | `actions/parameters-naming/action-rename-workset.cs`, `actions/parameters-naming/action-set-workset.cs`, `actions/visibility/action-set-view-workset-visibility.cs` |
| `UNPLACED` | 8 | `recipes/generate-room-coverage-layout.cs`, `recipes/sprinkler-adjust-for-obstructions.cs`, `recipes/sprinkler-compliance-audit.cs` |
| `View3D` | 8 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/qa-checks/action-check-surface-fit.cs`, `actions/reporting/action-report-ray-hits.cs` |
| `WorksetKind` | 8 | `actions/parameters-naming/action-rename-workset.cs`, `actions/parameters-naming/action-set-workset.cs`, `actions/visibility/action-set-view-workset-visibility.cs` |
| `Revision` | 7 | `actions/sheet-dates-revisions/action-assign-revisions-by-sheet-date.cs`, `actions/sheet-dates-revisions/action-delete-revision.cs`, `actions/sheet-dates-revisions/action-edit-revision.cs` |
| `Wall` | 7 | `actions/reporting/action-count-by-group.cs`, `filters/by-identity/filter-by-category.cs`, `filters/by-location/filter-by-elements-on-level.cs` |
| `FilterElement` | 6 | `actions/color-graphics/action-apply-view-filter.cs`, `actions/color-graphics/action-remove-view-filter.cs`, `actions/color-graphics/action-report-view-filters.cs` |
| `Material` | 6 | `actions/reporting/action-report-material-takeoff.cs`, `actions/structural-changes/action-purge-unused.cs`, `creators/create-material.cs` |
| `RevitLinkInstance` | 6 | `actions/structural-changes/action-copy-from-link.cs`, `actions/structural-changes/action-reload-links.cs`, `actions/structural-changes/action-unload-remove-links.cs` |
| `ACTUAL` | 5 | `creators/create-cable-tray.cs`, `creators/create-callout-view.cs`, `creators/create-conduit.cs` |
| `BoundingBoxXYZ` | 5 | `actions/reporting/action-report-bounding-box.cs`, `actions/reporting/action-report-nearest-elements.cs`, `actions/visibility/action-section-box-and-zoom.cs` |
| `Color` | 5 | `creators/create-mep-system-type.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-mep-line-standards.cs` |
| `InvalidOperationException` | 5 | `actions/sheets-views/action-duplicate-view-template.cs`, `actions/structural-changes/action-split-elements.cs`, `examples/prelude-smoke-test.cs` |
| `OverrideGraphicSettings` | 5 | `actions/color-graphics/action-highlight-vs-rest.cs`, `actions/color-graphics/action-reset-category-graphics.cs`, `actions/color-graphics/action-reset-graphic-overrides.cs` |
| `Reference` | 5 | `actions/sheets-views/action-add-spot-elevations.cs`, `actions/sheets-views/action-tag-elements.cs`, `creators/create-dimension.cs` |
| `ReferenceIntersector` | 5 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/qa-checks/action-check-surface-fit.cs`, `actions/reporting/action-report-ray-hits.cs` |
| `SpatialElement` | 5 | `actions/reporting/action-plan-shortest-route.cs`, `actions/reporting/action-report-room-boundaries.cs`, `actions/reporting/action-report-room-space-data.cs` |
| `StringComparer` | 5 | `actions/reporting/action-report-space-airflow.cs`, `actions/visibility/action-set-view-workset-visibility.cs`, `creators/create-levels.cs` |
| `TextNote` | 5 | `actions/parameters-naming/action-find-replace-text-notes.cs`, `actions/sheet-dates-revisions/action-assign-revisions-by-sheet-date.cs`, `actions/sheet-dates-revisions/action-extract-dates-from-textnotes.cs` |
| `TransactionGroup` | 5 | `actions/sheets-views/action-duplicate-view-template.cs`, `actions/structural-changes/action-place-accessory-on-run.cs`, `lib/prelude.cs` |
| `ViewFamilyType` | 5 | `creators/create-callout-view.cs`, `creators/create-room-elevations.cs`, `creators/create-view.cs` |
| `BoundingBoxIntersectsFilter` | 4 | `filters/by-location/filter-by-region.cs`, `recipes/sprinkler-adjust-for-obstructions.cs`, `recipes/sprinkler-obstruction-check.cs` |
| `DesignOption` | 4 | `actions/parameters-naming/action-set-design-option.cs`, `context/context-design-options.cs`, `context/context-session-start.cs` |
| `ElementCategoryFilter` | 4 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/qa-checks/action-check-surface-fit.cs`, `actions/reporting/action-report-ray-hits.cs` |
| `ElementMulticategoryFilter` | 4 | `filters/by-location/filter-by-elements-on-level.cs`, `filters/by-status/filter-by-phase.cs`, `filters/by-status/filter-by-selection-filter.cs` |
| `Face` | 4 | `actions/qa-checks/action-check-surface-fit.cs`, `actions/sheets-views/action-add-spot-elevations.cs`, `recipes/create-equipment-family-from-datasheet.cs` |
| `Family` | 4 | `actions/parameters-naming/action-rename-family.cs`, `actions/structural-changes/action-purge-unused-families.cs`, `context/context-used-families.cs` |
| `FindReferenceTarget` | 4 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/qa-checks/action-check-surface-fit.cs`, `actions/reporting/action-report-ray-hits.cs` |
| `GeometryObject` | 4 | `actions/sheets-views/action-add-spot-elevations.cs`, `actions/structural-changes/action-extract-cad-curves.cs`, `recipes/create-equipment-family-from-datasheet.cs` |
| `Grid` | 4 | `context/context-levels-and-grids.cs`, `context/context-session-start.cs`, `creators/create-dimension.cs` |
| `Options` | 4 | `actions/sheets-views/action-add-spot-elevations.cs`, `actions/structural-changes/action-extract-cad-curves.cs`, `recipes/create-equipment-family-from-datasheet.cs` |
| `Outline` | 4 | `filters/by-location/filter-by-region.cs`, `recipes/sprinkler-adjust-for-obstructions.cs`, `recipes/sprinkler-obstruction-check.cs` |
| `ReferenceArray` | 4 | `actions/sheets-views/action-add-aligned-dimensions.cs`, `creators/create-dimension.cs`, `recipes/create-equipment-family-from-datasheet.cs` |
| `Revit` | 4 | `actions/parameters-naming/action-rename-phase.cs`, `actions/sheets-views/action-export-sheets-to-pdf.cs`, `creators/create-view.cs` |
| `RevitLinkType` | 4 | `actions/structural-changes/action-reload-links.cs`, `actions/structural-changes/action-unload-remove-links.cs`, `context/context-linked-models.cs` |
| `Area` | 3 | `actions/reporting/action-plan-shortest-route.cs`, `actions/reporting/action-report-space-airflow.cs`, `recipes/sprinkler-floor-scope.cs` |
| `BuiltInParameterGroup` | 3 | `actions/parameters-naming/action-add-project-parameter.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `Ceiling` | 3 | `recipes/build-test-fixtures.cs`, `recipes/place-fcu.cs`, `recipes/place-terminals-checkerboard.cs` |
| `Collaborate` | 3 | `actions/parameters-naming/action-rename-workset.cs`, `commands/command-sync-with-central.cs`, `creators/create-workset.cs` |
| `CurveArray` | 3 | `creators/create-floor.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `Group` | 3 | `actions/structural-changes/action-ungroup-elements.cs`, `filters/by-relationship/filter-by-group.cs`, `recipes/model-health-audit.cs` |
| `HIDDEN` | 3 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/reporting/action-report-ray-hits.cs`, `recipes/sprinkler-deflector-height.cs` |
| `ImportInstance` | 3 | `actions/structural-changes/action-extract-cad-curves.cs`, `filters/by-relationship/filter-by-links.cs`, `recipes/model-health-audit.cs` |
| `IndependentTag` | 3 | `actions/sheets-views/action-remove-tags.cs`, `filters/by-view-and-sheet/filter-by-tag-status.cs`, `recipes/tag-elements-in-active-view.cs` |
| `KeyValuePair` | 3 | `actions/reporting/action-report-nearest-elements.cs`, `actions/reporting/action-report-ray-hits.cs`, `recipes/fill-mm-document-register.cs` |
| `LinePatternElement` | 3 | `actions/color-graphics/action-set-category-line-style.cs`, `actions/color-graphics/action-set-line-style.cs`, `recipes/mep-grayout.cs` |
| `ParameterFilterElement` | 3 | `actions/color-graphics/action-create-view-filter.cs`, `actions/color-graphics/action-report-view-filters.cs`, `filters/by-status/filter-by-selection-filter.cs` |
| `ParameterType` | 3 | `actions/parameters-naming/action-add-project-parameter.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `PlanarFace` | 3 | `actions/sheets-views/action-add-spot-elevations.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `SelectionFilterElement` | 3 | `actions/color-graphics/action-create-selection-filter.cs`, `actions/color-graphics/action-report-view-filters.cs`, `filters/by-status/filter-by-selection-filter.cs` |
| `Solid` | 3 | `actions/sheets-views/action-add-spot-elevations.cs`, `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `TemporaryViewMode` | 3 | `actions/visibility/action-isolate-elements.cs`, `commands/unhide-all-active-view.cs`, `examples/color-isolate-select-by-size.cs` |
| `TextNoteType` | 3 | `creators/create-text-note.cs`, `recipes/create-mep-line-standards.cs`, `recipes/create-mep-text-standards.cs` |
| `CopyPasteOptions` | 2 | `actions/sheets-views/action-duplicate-view-template.cs`, `actions/structural-changes/action-copy-from-link.cs` |
| `CurveArrArray` | 2 | `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `CurveLoop` | 2 | `creators/create-filled-region.cs`, `filters/by-location/filter-by-solid-intersection.cs` |
| `Duct` | 2 | `actions/move-copy-rotate/action-fillet-elements.cs`, `recipes/connect-terminal-branch.cs` |
| `ElementIntersectsElementFilter` | 2 | `actions/qa-checks/action-report-clashes.cs`, `filters/by-location/filter-by-element-intersection.cs` |
| `FilledRegionType` | 2 | `creators/create-filled-region.cs`, `recipes/create-mep-line-standards.cs` |
| `Front` | 2 | `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `GeometryInstance` | 2 | `actions/sheets-views/action-add-spot-elevations.cs`, `actions/structural-changes/action-extract-cad-curves.cs` |
| `IdValue` | 2 | `actions/sheets-views/action-report-schedule-definition.cs`, `actions/sheets-views/action-report-view-template-status.cs` |
| `IsConnected` | 2 | `actions/reporting/action-report-connectors.cs`, `recipes/slice-trunk-for-sizing.cs` |
| `Left` | 2 | `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `MechanicalSystemType` | 2 | `recipes/connect-terminal-branch.cs`, `recipes/draw-main-duct-with-cap.cs` |
| `Name` | 2 | `actions/sheets-views/action-export-parameters-to-csv.cs`, `creators/create-room.cs` |
| `NOTHING` | 2 | `actions/move-copy-rotate/action-move-to-ray-hit.cs`, `actions/structural-changes/action-purge-unused-families.cs` |
| `ParameterElement` | 2 | `actions/sheets-views/action-report-schedule-definition.cs`, `actions/sheets-views/action-report-view-template-status.cs` |
| `Plane` | 2 | `actions/move-copy-rotate/action-fillet-elements.cs`, `actions/reporting/action-plan-shortest-route.cs` |
| `QCDD` | 2 | `recipes/sprinkler-nfpa-grid.cs`, `recipes/sprinkler-sidewall-layout.cs` |
| `Queue` | 2 | `filters/by-relationship/filter-by-subcomponents.cs`, `recipes/verify-duct-connectivity.cs` |
| `ReferencePlane` | 2 | `recipes/create-equipment-family-from-datasheet.cs`, `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `ReferenceWithContext` | 2 | `actions/reporting/action-report-ray-hits.cs`, `recipes/sprinkler-deflector-height.cs` |
| `ScheduleSortGroupField` | 2 | `actions/sheets-views/action-set-schedule-sort-group.cs`, `creators/create-sheet-list.cs` |
| `TagOrientation` | 2 | `actions/sheets-views/action-tag-elements.cs`, `recipes/tag-elements-in-active-view.cs` |
| `ViewDrafting` | 2 | `recipes/create-mep-line-standards.cs`, `recipes/create-mep-text-standards.cs` |
| `Viewport` | 2 | `actions/sheets-views/action-duplicate-sheet.cs`, `recipes/model-health-audit.cs` |
| `ViewSet` | 2 | `actions/sheets-views/action-export-sheets-to-pdf.cs`, `actions/sheets-views/action-manage-sheet-sets.cs` |
| `Angle` | 1 | `context/context-shared-coordinates.cs` |
| `AnnotationCropActive` | 1 | `actions/visibility/action-set-crop-box-settings.cs` |
| `Application` | 1 | `commands/native-undo.cs` |
| `AssemblyInstance` | 1 | `filters/by-relationship/filter-by-assembly.cs` |
| `BasePoint` | 1 | `context/context-shared-coordinates.cs` |
| `BOTH` | 1 | `recipes/sprinkler-deflector-height.cs` |
| `ColumnHeaders` | 1 | `actions/sheets-views/action-export-schedule-to-csv.cs` |
| `ConnectorElement` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `ConnectorProfileType` | 1 | `recipes/create-parametric-box-family-with-duct-connector.cs` |
| `Copy` | 1 | `actions/sheets-views/action-duplicate-view-template.cs` |
| `Created` | 1 | `filters/by-status/filter-by-phase.cs` |
| `CropBoxVisible` | 1 | `actions/visibility/action-set-crop-box-settings.cs` |
| `CURVED` | 1 | `recipes/sprinkler-sidewall-layout.cs` |
| `Dash` | 1 | `recipes/create-mep-line-standards.cs` |
| `Demolished` | 1 | `filters/by-status/filter-by-phase.cs` |
| `Design` | 1 | `recipes/build-test-fixtures.cs` |
| `Detail` | 1 | `actions/sheets-views/action-set-view-properties.cs` |
| `DetailLevel` | 1 | `actions/sheets-views/action-add-spot-elevations.cs` |
| `DetailLine` | 1 | `actions/move-copy-rotate/action-fillet-elements.cs` |
| `DisplayStyle` | 1 | `actions/sheets-views/action-set-view-properties.cs` |
| `DisplayUnitType` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `DWGExportOptions` | 1 | `actions/sheets-views/action-export-views-to-dwg.cs` |
| `Elbow` | 1 | `actions/move-copy-rotate/action-fillet-elements.cs` |
| `ElementBinding` | 1 | `actions/parameters-naming/action-add-project-parameter.cs` |
| `ElementIntersectsSolidFilter` | 1 | `filters/by-location/filter-by-solid-intersection.cs` |
| `ElementParameterFilter` | 1 | `actions/color-graphics/action-create-view-filter.cs` |
| `ElementTypeGroup` | 1 | `recipes/create-mep-line-standards.cs` |
| `Elev` | 1 | `context/context-shared-coordinates.cs` |
| `Elevation` | 1 | `context/context-shared-coordinates.cs` |
| `Error` | 1 | `filters/by-status/filter-by-warnings.cs` |
| `ExportRange` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `ExternalDefinition` | 1 | `actions/parameters-naming/action-add-project-parameter.cs` |
| `ExternalDefinitionCreationOptions` | 1 | `actions/parameters-naming/action-add-project-parameter.cs` |
| `Extrusion` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `FAMILY` | 1 | `context/context-active-view.cs` |
| `FamilyInstanceFilter` | 1 | `filters/by-identity/filter-by-category-and-family.cs` |
| `FamilyInstanceReferenceType` | 1 | `actions/sheets-views/action-add-aligned-dimensions.cs` |
| `FamilyParameter` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `FitDirection` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `FloorType` | 1 | `creators/create-floor.cs` |
| `Galvanized` | 1 | `filters/by-identity/filter-by-material.cs` |
| `GAPS` | 1 | `actions/parameters-naming/action-renumber-sequential.cs` |
| `Grand` | 1 | `actions/sheets-views/action-set-schedule-appearance.cs` |
| `GraphicsStyle` | 1 | `actions/structural-changes/action-extract-cad-curves.cs` |
| `GraphicsStyleType` | 1 | `recipes/create-mep-line-standards.cs` |
| `HIGHER` | 1 | `recipes/sprinkler-floor-scope.cs` |
| `HLRandWFViewsFileType` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `HostObjAttributes` | 1 | `actions/reporting/action-report-compound-structure.cs` |
| `IdOf` | 1 | `recipes/mep-grayout.cs` |
| `IFCExportOptions` | 1 | `actions/sheets-views/action-export-ifc.cs` |
| `ImageExportOptions` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `IMPOSSIBLE` | 1 | `creators/create-ceiling.cs` |
| `Install` | 1 | `actions/sheets-views/action-export-nwc.cs` |
| `InsulationLiningBase` | 1 | `actions/structural-changes/action-add-remove-insulation.cs` |
| `InternalDefinition` | 1 | `actions/reporting/action-report-parameter-inventory.cs` |
| `IsCuttable` | 1 | `actions/color-graphics/action-set-category-color.cs` |
| `LinePattern` | 1 | `recipes/create-mep-line-standards.cs` |
| `LinePatternSegment` | 1 | `recipes/create-mep-line-standards.cs` |
| `LinePatternSegmentType` | 1 | `recipes/create-mep-line-standards.cs` |
| `Lining` | 1 | `filters/by-relationship/filter-by-insulation-status.cs` |
| `MADE` | 1 | `recipes/sprinkler-obstruction-survey.cs` |
| `MEPSystem` | 1 | `actions/reporting/action-report-connectors.cs` |
| `MEPSystemType` | 1 | `creators/create-mep-system-type.cs` |
| `ModelLine` | 1 | `actions/move-copy-rotate/action-fillet-elements.cs` |
| `Multi` | 1 | `actions/sheets-views/action-report-schedule-definition.cs` |
| `NavisworksExportOptions` | 1 | `actions/sheets-views/action-export-nwc.cs` |
| `NEEDING` | 1 | `recipes/sprinkler-floor-scope.cs` |
| `PaperSize` | 1 | `actions/sheets-views/action-set-print-settings.cs` |
| `PartType` | 1 | `recipes/connect-equipment-to-air-terminals.cs` |
| `PhaseFilter` | 1 | `actions/sheets-views/action-set-view-properties.cs` |
| `PixelSize` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `PlanCircuit` | 1 | `creators/create-rooms-in-enclosed-regions.cs` |
| `PlanViewPlane` | 1 | `actions/visibility/action-set-view-range.cs` |
| `Plumbing` | 1 | `recipes/sprinkler-compliance-audit.cs` |
| `PolyLine` | 1 | `actions/structural-changes/action-extract-cad-curves.cs` |
| `PostableCommand` | 1 | `commands/native-undo.cs` |
| `PrintSetting` | 1 | `actions/sheets-views/action-set-print-settings.cs` |
| `Random` | 1 | `actions/color-graphics/action-color-by-group.cs` |
| `RelinquishOptions` | 1 | `commands/command-sync-with-central.cs` |
| `Reset` | 1 | `actions/visibility/action-hide-elements.cs` |
| `RevisionVisibility` | 1 | `actions/sheet-dates-revisions/action-edit-revision.cs` |
| `RoutingPreferenceRuleGroupType` | 1 | `recipes/draw-main-duct-with-cap.cs` |
| `Rule` | 1 | `recipes/sprinkler-deflector-height.cs` |
| `SaveAs` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `SaveOptions` | 1 | `commands/command-compact-save.cs` |
| `ScheduleField` | 1 | `actions/sheets-views/action-report-schedule-definition.cs` |
| `ScheduleFilter` | 1 | `actions/sheets-views/action-set-schedule-filters.cs` |
| `ScheduleFilterType` | 1 | `actions/sheets-views/action-set-schedule-filters.cs` |
| `ScheduleHorizontalAlignment` | 1 | `actions/sheets-views/action-set-schedule-field-format.cs` |
| `ScheduleSheetInstance` | 1 | `actions/sheets-views/action-duplicate-sheet.cs` |
| `ScheduleSortOrder` | 1 | `creators/create-sheet-list.cs` |
| `Scope` | 1 | `recipes/build-test-fixtures.cs` |
| `ShadowViewsFileType` | 1 | `actions/sheets-views/action-export-view-image.cs` |
| `SharedParameterElement` | 1 | `actions/sheets-views/action-report-schedule-definition.cs` |
| `ShowBlankLine` | 1 | `actions/sheets-views/action-set-schedule-sort-group.cs` |
| `ShowFooter` | 1 | `actions/sheets-views/action-set-schedule-sort-group.cs` |
| `ShowHeader` | 1 | `actions/sheets-views/action-set-schedule-sort-group.cs` |
| `SketchPlane` | 1 | `actions/move-copy-rotate/action-fillet-elements.cs` |
| `SortedDictionary` | 1 | `actions/reporting/action-compare-elements.cs` |
| `Space` | 1 | `recipes/create-mep-line-standards.cs` |
| `SpatialElementBoundaryOptions` | 1 | `actions/reporting/action-report-room-boundaries.cs` |
| `START` | 1 | `context/context-session-start.cs` |
| `SynchronizeWithCentralOptions` | 1 | `commands/command-sync-with-central.cs` |
| `TableCellCombinedParameterData` | 1 | `actions/sheets-views/action-add-schedule-calculated-field.cs` |
| `Title` | 1 | `actions/sheets-views/action-export-schedule-to-csv.cs` |
| `TransactWithCentralOptions` | 1 | `commands/command-sync-with-central.cs` |
| `Transform` | 1 | `actions/sheets-views/action-duplicate-view-template.cs` |
| `Type` | 1 | `context/harvest-revit-api.cs` |
| `UIApplication` | 1 | `commands/native-undo.cs` |
| `UIDocument` | 1 | `actions/selection/action-select-elements.cs` |
| `UNCONFIRMED` | 1 | `recipes/sprinkler-adjust-for-obstructions.cs` |
| `UNCONSTRAINED` | 1 | `recipes/generate-room-coverage-layout.cs` |
| `UNDER` | 1 | `recipes/generate-room-coverage-layout.cs` |
| `UnitType` | 1 | `context/context-project-units.cs` |
| `UnitUtils` | 1 | `recipes/create-equipment-family-from-datasheet.cs` |
| `UNOBSTRUCTED` | 1 | `recipes/sprinkler-obstruction-survey.cs` |
| `ViewDetailLevel` | 1 | `actions/sheets-views/action-set-view-properties.cs` |
| `ViewScheduleExportOptions` | 1 | `actions/sheets-views/action-export-schedule-to-csv.cs` |
| `ViewSheetSet` | 1 | `actions/sheets-views/action-manage-sheet-sets.cs` |
| `Visual` | 1 | `actions/sheets-views/action-set-view-properties.cs` |
| `WallType` | 1 | `creators/create-wall.cs` |
| `WARNING` | 1 | `actions/sheets-views/action-duplicate-sheet.cs` |
| `Worksets` | 1 | `creators/create-workset.cs` |
| `WorksetTable` | 1 | `recipes/build-test-fixtures.cs` |
| `WorksetVisibility` | 1 | `actions/visibility/action-set-view-workset-visibility.cs` |
| `YOUR` | 1 | `actions/reporting/action-report-coverage.cs` |
| `ZoomType` | 1 | `actions/sheets-views/action-export-view-image.cs` |

## BuiltInParameter values in use (68)

`RBS_CURVE_HEIGHT_PARAM`, `RBS_CURVE_WIDTH_PARAM`, `EXTRUSION_END_PARAM`, `FAMILY_LEVEL_PARAM`, `INSTANCE_REFERENCE_LEVEL_PARAM`, `LEVEL_PARAM`, `RBS_START_LEVEL_PARAM`, `SCHEDULE_LEVEL_PARAM`, `CEILING_HEIGHTABOVELEVEL_PARAM`, `EXTRUSION_START_PARAM`, `RBS_CURVE_DIAMETER_PARAM`, `RBS_DUCT_SYSTEM_TYPE_PARAM`, `RBS_DUCT_FLOW_PARAM`, `RBS_SYSTEM_NAME_PARAM`, `ROOM_NAME`, `ALL_MODEL_FAMILY_NAME`, `ALL_MODEL_MARK`, `BASEPOINT_ANGLETON_PARAM`, `CURVE_ELEM_LENGTH`, `RBS_CABLETRAY_HEIGHT_PARAM`, `RBS_CABLETRAY_WIDTH_PARAM`, `RBS_CONDUIT_DIAMETER_PARAM`, `RBS_PIPE_DIAMETER_PARAM`, `ROOM_DESIGN_RETURN_AIRFLOW_PARAM`, `ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM`, `SYMBOL_FAMILY_NAME_PARAM`, `VIEWER_VOLUME_OF_INTEREST_CROP`, `BASEPOINT_EASTWEST_PARAM`, `BASEPOINT_ELEVATION_PARAM`, `BASEPOINT_NORTHSOUTH_PARAM`, `CLIENT_NAME`, `CONNECTOR_DIAMETER`, `CONNECTOR_HEIGHT`, `CONNECTOR_WIDTH`, `ELEM_PARTITION_PARAM`, `FAMILY_CONTENT_PART_TYPE`, `INVALID`, `IS_VISIBLE_PARAM`, `LEADER_OFFSET_SHEET`, `LEVEL_ELEV`, `LINE_COLOR`, `OPTION_SET_ID`, `PHASE_CREATED`, `PHASE_DEMOLISHED`, `PROJECT_NAME`, `PROJECT_NUMBER`, `PROJECT_STATUS`, `RBS_CALCULATED_SIZE`, `RBS_ELEC_APPARENT_LOAD`, `RBS_ELEC_NUMBER_OF_POLES`, `RBS_ELEC_VOLTAGE`, `RBS_PIPE_FLOW_DIRECTION_PARAM`, `RBS_PIPING_SYSTEM_TYPE_PARAM`, `RBS_SYSTEM_ABBREVIATION_PARAM`, `ROOM_NUMBER`, `ROOM_VOLUME`, `TEXT_BACKGROUND`, `TEXT_BOX_VISIBILITY`, `TEXT_FONT`, `TEXT_SIZE`, `TEXT_STYLE_BOLD`, `TEXT_STYLE_ITALIC`, `TEXT_STYLE_UNDERLINE`, `TEXT_WIDTH_SCALE`, `VIEW_PHASE`, `VIEW_PHASE_FILTER`, `VIEW_SCALE`, `VIEWER_ANNOTATION_CROP_ACTIVE`

## BuiltInCategory values in use (41)

`OST_DuctCurves`, `OST_DuctTerminal`, `OST_PipeCurves`, `OST_Rooms`, `OST_DuctAccessory`, `OST_DuctFitting`, `OST_PipeFitting`, `OST_TitleBlocks`, `OST_FlexDuctCurves`, `OST_MEPSpaces`, `OST_VolumeOfInterest`, `OST_MechanicalEquipment`, `OST_StructuralFraming`, `OST_CableTray`, `OST_Ceilings`, `OST_Sprinklers`, `OST_Columns`, `OST_Conduit`, `OST_Doors`, `OST_LightingFixtures`, `OST_StructuralColumns`, `OST_DuctInsulations`, `OST_DuctLinings`, `OST_DuctTags`, `OST_FlexPipeCurves`, `OST_Floors`, `OST_Lines`, `OST_PipeAccessory`, `OST_Walls`, `OST_CableTrayFitting`, `OST_CalloutBoundary`, `OST_Callouts`, `OST_CLines`, `OST_Coupler`, `OST_Furniture`, `OST_Grids`, `OST_Levels`, `OST_Matchline`, `OST_PipeInsulations`, `OST_Rebar`, `OST_Windows`

## Regenerate

Run after adding or retiring fragments — the counts go stale the moment `scripts/` changes:

```
node tools/api-surface.mjs
```
