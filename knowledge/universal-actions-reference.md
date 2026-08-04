# Universal Revit Actions — reference list (v3 — full Revisions lifecycle added)

<!-- split-review: kept whole 2026-08-04 — reviewed against the ~300-line rule and deliberately NOT split.
     This is a MENU, not a topic file: knowledge/INDEX.md routes "what actions are available" here, and
     answering that means scanning the whole list. Split by category and the one question it exists to
     answer would need every piece opened. The 182-action count is also anchored to this single file.
     It is 8 lines over a guideline that the brain-self-maintain skill states is "a split candidate, not a
     mandate". brain-status.mjs reads this marker and stops flagging the file. -->

Plain-language index of generic, category-agnostic Revit actions available (or genuinely buildable)
through the bridge. Every action works on **any category/element** — variables in `[brackets]` are
always supplied per request, never hardcoded. See
[`../scripts/README.md`](../scripts/README.md) for the ones already built as fragments. **NEEDS_REVIEW**
= real question mark on how cleanly the standard Revit API supports it, or genuinely risky/complex — not
something to build without checking further first.

**14 of these (2026-07-22) are also available as real, individually schema-validated MCP tools** —
`list_elements`, `count_elements`, `hide_elements`, `unhide_elements`, `isolate_elements`,
`reset_isolation`, `set_color`, `reset_graphic_overrides`, `set_transparency`, `select_elements`,
`set_parameter_value`, `report_parameters`, `move_elements`, `delete_elements` — see
[`../AGENT-SPEC.md`](../AGENT-SPEC.md) §3.4 for the full spec. Prefer these over composing the matching
fragment when one exists — faster, protocol-validated, no code generation needed.

**Note on the brief's own numbers** (from the request that produced v2): "minimum 100" in one place,
"minimum 200" in the output format. This list totals **182** real, distinct, non-duplicate actions —
clears the 100 minimum, short of 200. Padding to 200 would mean inventing filler, which the same brief
ruled out. Honest stopping point over a round number.

**What changed in v3**: pulled Revisions out of the old combined "Revisions & Phases" group into its own
full lifecycle — create, edit, delete, add/remove from a sheet, revision schedules, the works — per a
direct follow-up request. Phases stays separate. **Read
[`live-model/revisions.md`](live-model/revisions.md) before building Create/Delete Revision** — there's a
real, confirmed gotcha there: an unattached revision can silently vanish (auto-purged) the next time
something touches sheet-revision association, and its Sequence Number isn't stable to rely on either.

---

## Visibility & Graphics
1. Hide Elements – [element/elements], [view], temp/permanent
2. Unhide Elements – reverse permanent hide
3. Reset Temporary Hide/Isolate – [view]
4. Isolate Elements – [element/elements] in [view]
5. Show/Zoom to Elements
6. Set Color – [color] on [element/elements], optional [view] (defaults to active — targets any view directly, not just what's on screen)
7. Color by Group – by [parameter], optional [view]
8. Highlight vs Rest – optional [view]
9. Reset Graphic Overrides – optional [view]
10. Set Transparency – [value]%, optional [view]
11. Report Graphic Overrides – optional [view]
12. Section Box & Zoom – optional [target 3D view]
13. Set View Crop – optional [view]
14. Toggle Category Visibility – [category], [view]
15. Set Visibility/Graphics Override by Category
16. Set Halftone/Transparency by Category

## Parameters & Data
17. Set Parameter Value
18. Copy Parameter Value
19. Report Parameters
20. Rename Element
21. Renumber Sequentially
22. Change Type
23. Pin/Unpin
24. Count & Report
25. Report Location
26. Report Bounding Box
27. Length by Size
28. Material Takeoff
29. Find Duplicates
30. Set Type Parameter
31. Report Family/Type Usage Count
32. Set Room-Bounding Flag
33. Set Workset of Element
34. Report Element Owner (worksharing)

## Selection & Filtering
35. Filter by Category
36. Filter by Category + Family
37. Filter by Category + Numeric Parameter
38. Filter by Category Name
39. Filter by Room
40. Filter by System Type
41. Filter by Current Selection
42. Filter by Region
43. Filter by Multiple Categories
44. Filter by Parameter Text
45. Filter by Workset
46. Filter by Sheets
47. Filter by Phase
48. Filter by Element ID List
49. Select Elements
50. Save Selection Set
51. Load Selection Set
52. Update Selection Set

## Geometry / Edit
53. Move Elements
54. Copy Elements
55. Rotate Elements
56. Delete Elements – confirm count first, needs destructive access allowed
57. Mirror Elements
58. Create Group from Elements
59. Ungroup
60. Place Group Instance
61. Report Group Members
62. Join Geometry
63. Unjoin Geometry
64. Array – BUILT: `actions/move-copy-rotate/action-array-elements.cs`
65. Align Elements – BUILT: `actions/move-copy-rotate/action-align-elements.cs`
66. Edit Group Contents – NEEDS_REVIEW

## Creation
67. Create Levels
68. Create Room
69. Create Point-Based Element
70. Create Material
71. Create Grid
72. Create Grid System
73. Create Area Plan
74. Create Area Boundary Lines
75. Load Family

## Annotation
76. Tag Elements in View
77. Create Text Note
78. Create Dimension
79. Create Detail Line
80. Create Spot Elevation
81. Create Spot Coordinate
82. Create Keynote
83. Create Revision Cloud – [region/points], [revision], [view] (the cloud itself; see Revisions group for the revision record it references)
84. Add/Remove Leader on Tag
85. Set Annotation Type
86. Report Annotations in View

## Dimensions & Constraints
87. Lock/Unlock Dimension
88. Create EQ Constraint
89. Report Dimension Value
90. Override Dimension Text
91. Create Alignment (Reference Lock)
92. Delete Constraint

## Levels & Grids
93. Set Level Elevation
94. Set Grid Extents
95. Toggle Grid Bubble
96. Report Levels List
97. Report Grids List

## Views & View Templates
98. Create View (Plan/Section/3D/Elevation/Ceiling)
99. Duplicate View
100. Apply View Template — BUILT: `actions/sheets-views/action-apply-view-template.cs`
101. Create View Template from View — BUILT: `actions/sheets-views/action-create-view-template-from-view.cs`
102. Set View Template Parameter Include/Exclude — BUILT: `actions/sheets-views/action-set-view-template-controlled-params.cs`
103. Set View Scale
104. Set View Detail Level
105. Set View Discipline
106. Set View Phase
107. Set View Phase Filter
108. Set View Range
109. Set Underlay
110. Report View List

## View Filters (rule-based)
111. Create View Filter
112. Edit View Filter Rule
113. Delete View Filter
114. Duplicate View Filter
115. Apply Filter to View with Override
116. Report View Filters in Project

## Sheets & Titleblocks
117. Create Sheet – [number], [name], [title block]
118. Set Sheet Number – [sheet], [new number]
119. Set Sheet Parameter – [sheet], [parameter], [value]
120. Place Viewport on Sheet – [view] → [sheet]
121. Place Schedule on Sheet – [schedule] → [sheet]
122. Report Sheet List – [number]/[name]/[revision] per sheet
123. Report Sheets with No Placed Views

## Schedules
124. Create Schedule – [category], [field list]
125. Add/Remove Schedule Field
126. Set Schedule Sort/Group Field
127. Set Schedule Filter
128. Export Schedule to Text/CSV
129. Report Schedule Row Count

## Revisions (full lifecycle)
130. Create Revision – [description], [date] (defaults to today if not given), [issued to], [issued by] — read `live-model/revisions.md` first (auto-purge gotcha if not attached to a sheet)
131. Edit Revision – [revision], [field: description/date/issued to/issued by], [new value]
132. Delete Revision – [revision] — confirm first; if it was never attached to any sheet, deleting it (or even just touching another sheet's revisions afterward) can already be moot — check `live-model/revisions.md`
133. Reorder Revision Sequence – NEEDS_REVIEW (no confirmed simple "set order" API distinct from date/numbering)
134. Set Revision Numbering/Sequence Type – NEEDS_REVIEW (project-wide setting, exact API surface varies by Revit version)
135. Report Revisions List – every project revision with its properties
136. Add Revision to Sheet – [revision], [sheet/sheets]
137. Remove Revision from Sheet – [revision], [sheet/sheets]
138. Assign Revisions by Sheet Date – scan [sheets] TextNotes for dates, auto-attach the matching [revision]
139. Report Revisions on Sheet – [sheet]
140. Show/Hide Revision Cloud on Sheet – [sheet/view], [revision], [visible/hidden]
141. Create Revision Schedule – the titleblock-style schedule listing every revision, [placement]

## Phases
142. Create Phase – [phase name], [insert position] – **CONFIRMED IMPOSSIBLE via API** (live-verified 2026-07-23): `Document.Phases` is read-only, `Insert`/`Append` both throw "Collection is read-only" at runtime, and no other Phase-creation API exists anywhere in the assembly. UI-only (Manage > Phases). `action-create-phase.cs` reports this instead of throwing a compile error. See [`brain-log.md`](brain-log.md) 2026-07-23.
143. Set Element Phase Created/Demolished – [element/elements], [phase]
144. Reorder Phases – [phase], [new position]
145. Report Elements by Phase – [phase], [category]

## Worksharing & Worksets
146. Create Workset – [name]
147. Rename Workset – [workset], [new name]
148. Open/Close Workset – [workset], [open/closed]
149. Set Workset Visibility in View – [workset], [view], [visible/hidden]
150. Checkout Elements (borrow) – [element/elements]
151. Relinquish Ownership – [element/elements] or [worksets]
152. Report Worksharing Status – on/off, worksets, owners
153. Synchronize with Central – NEEDS_REVIEW (real API, high-risk/slow — confirm explicitly first)

## Links
154. List Linked Models
155. Reload Link
156. Unload Link
157. Pin/Unpin Link
158. Move/Rotate Link
159. Set Link Visibility
160. Remove Link
161. Report Link Status
162. Bind Link – NEEDS_REVIEW
163. Copy/Monitor Link Elements – NEEDS_REVIEW

## Export
164. Export to DWG
165. Export to IFC
166. Export to PDF
167. Export Image from View
168. Export Room/Area Report

## Model Health & Cleanup
169. Report All Warnings
170. Report Unused Elements
171. Purge Unused – BUILT (partially): `actions/structural-changes/action-purge-unused.cs` — the subset provably correct from the public API (unused View Templates, Filters, Materials), dry-run by default; there is still no single clean API equivalent to the full UI command, so the rest stays unbuilt

## Project / Document Level
172. Report Project Information
173. Set Project Information Parameter
174. Report Project Location/Coordinates
175. Report Design Options
176. Set Active Design Option – **CONFIRMED IMPOSSIBLE via API** (live-verified 2026-07-23): only a read-only `DesignOption.GetActiveDesignOptionId` exists anywhere in the assembly — no method to set/activate one. UI-only. `action-set-design-option.cs` requires the option be activated manually first. See [`brain-log.md`](brain-log.md) 2026-07-23.
177. Set Shared Coordinates – NEEDS_REVIEW (multi-step, order-sensitive, genuinely risky to automate blind)

## Model Info & Orientation (read-only)
178. Active View Snapshot
179. Project Units
180. Workset Info
181. Model Categories
182. Used Families

---

## Implementation index — granular fragments not folded into the count above
Some `scripts/` fragments are real, working, narrower variants of a conceptual action already listed
above — cataloguing each one as its own numbered item would inflate the "182 distinct actions" count with
near-duplicates rather than genuinely new capabilities. Listed here so they're not invisible to anyone
reading this file top-to-bottom. See [`../scripts/README.md`](../scripts/README.md) for the authoritative
descriptions and live-verification notes.

**Filters** (`scripts/filters/`, 48 real fragments total — items 35-48 above cover 14 of them):
- `filter-by-elements-on-level.cs` — everything on a given Level across the whole model, optional category scope
- `filter-by-levels.cs` — every Level ELEMENT itself (not elements sitting on one), ordered by elevation
- `filter-by-electrical-system.cs` — elements in a specific Electrical System (circuit), by Circuit Type and/or name
- `filter-by-system-name.cs` — pipes/ducts/fittings narrowed to one specific System instance's own name
- `filter-by-tag-status.cs` — category elements that ARE or ARE NOT tagged in a given view
- `filter-by-views.cs` — every View (not ViewSheet), optional ViewType + name filter
- `filter-by-elements-in-view.cs` — category narrowed to instances actually visible in a given view
- `filter-by-view-templates.cs` — View Templates themselves, optional name filter + usage mode
- `filter-by-element-intersection.cs` — elements whose real geometry intersects one specific target element
- `filter-by-solid-intersection.cs` — elements whose real geometry intersects a custom 3D box/clearance solid
- `filter-by-connection-status.cs` — category elements with at least one open connector end, or fully connected
- `filter-by-scope-box.cs` — every Scope Box, optional name substring
- `filter-by-length.cs` — category narrowed by Length (mm) vs. an mm value
- `filter-by-size.cs` — category narrowed by round (Diameter) or rectangular (Width x Height) size, or plain "Size" text
- `filter-by-pin-status.cs` — category elements that ARE or ARE NOT pinned
- `filter-by-material.cs` — elements using a specific Revit Material, category-scoped
- `filter-by-schedules.cs` — every ViewSchedule, optional name substring
- `filter-by-group.cs` — member elements of a specific Model Group instance
- `filter-by-unenclosed-spatial-elements.cs` — every Room/Space in the model with zero Area ("Not Enclosed")
- `filter-by-parameter-exists.cs` — elements that have a given parameter attached, whether blank or not
- `filter-by-space.cs` — category narrowed to instances physically inside one MEP Space (not a Room)
- `filter-by-selection-filter.cs` — read back elements behind a named Selection Filter, or re-evaluate a View Filter's rule
- `filter-by-types.cs` — the TYPE elements themselves, matched by family/type name
- `filter-by-family-type.cs` — a specific Type inside a Family, matched by name
- `filter-by-host.cs` — elements hosted on a specific parent (`FamilyInstance.Host` or insulation/lining `HostElementId`)
- `filter-by-assembly.cs` — member elements of a specific Revit Assembly
- `filter-by-family.cs` — family name matched across the whole model, no category picked first
- `filter-by-insulation-type.cs` — the insulation/lining elements themselves, by kind/type/material/thickness
- `filter-by-insulation-status.cs` — pipe/duct elements that HAVE insulation/lining applied, or don't
- `filter-by-design-option.cs` — elements in a named Design Option, or the Main Model when left unset
- `filter-by-links.cs` — every RVT link and/or CAD link instance, optional name substring
- `filter-by-grid.cs` — every Grid, optional name substring
- `filter-by-linked-model-elements.cs` — elements of a category inside a specific linked RVT model
- `filter-by-warnings.cs` — elements flagged by a current model warning, as an actionable set

**Graphic overrides** (`scripts/actions/color-graphics/`, beyond item 16's category halftone):
- `action-set-halftone.cs` — turn halftone on/off per element (read-modify-write, preserves color override)
- `action-set-line-style.cs` — override line weight and/or line pattern per element
- `action-set-category-line-style.cs` — override line weight/pattern for one or more entire categories

---

## Not included here on purpose
The bespoke, multi-stage HVAC/MEP recipes (FCU placement, duct routing, MEP tracing, terminal layout,
family creation) are real and working, but they're **not** universal/category-agnostic actions — fixed
workflows for one specific job. They live in `scripts/recipes/` and their own skills, not this list.
