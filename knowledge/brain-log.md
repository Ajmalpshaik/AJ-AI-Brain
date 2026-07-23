# Brain — Change Log

Dated, one-line entries for changes to the Brain itself — a new skill created, a knowledge file split,
a script fragment added or retired, a technique that finally got solved. This is a record of the Brain's
own growth, separate from `live-model/log.md` (which is a record of what was done *to a Revit model*).

Add a line here whenever [`skills/brain-self-maintain/SKILL.md`](../skills/brain-self-maintain/SKILL.md)
creates, splits, or retires something, or whenever any other skill's "after finishing" step says to log
here.

## Log

- 2026-07-22 — Added the "Element ID" glossary entry and a standing rule in `scripts/README.md`: any
  report on specific elements (not a bare count) must include each element's Element ID. Audited the
  `report-*` action fragments — already compliant by default (`includeElementId = true` etc.), no script
  changes needed, just made the convention explicit for future additions. Same change mirrored into
  `.claude`'s copy.
- 2026-07-22 — New reply-style rule: a request narrowed to one specific value ("the 300x300 VCDs") gets
  the actual item list with Element ID (`action-report-parameters.cs`), not a bare count or aggregate
  breakdown table (`action-count-and-report.cs`) — because the next request likely acts on that exact
  set. Updated `reply-style.md`, `ajtools-live-model/SKILL.md`, and the routing table in
  `scripts/architecture.md`. Mirrored into `.claude`'s `scripts/architecture.md` too (its
  `ajtools-live-model` skill isn't there anymore, so only the architecture.md row applies there).
- 2026-07-22 — Audited the full action/filter/creator library against a requested "universal action"
  brief (every named example: visibility, graphics, parameter edit, selection/filter, type swap, delete,
  move/copy, rename, tag, schedule). Found 3 real gaps and built them: `action-delete-elements.cs`,
  `action-rename-element.cs`, `create-schedule.cs` (bare schedule + fields only, no sort/filter/format
  yet). None live-tested — bridge wasn't connected this session; test each on one element before trusting
  a batch. Everything else in the brief was already covered by existing fragments. Wrote
  `knowledge/universal-actions-reference.md` as the plain-language index (mirrored into `.claude` too).
  Deliberately left NEEDS_REVIEW and un-built: mirror, array, align, group/ungroup, join geometry,
  dimension, text note — real gaps, not urgent enough to build unprompted.
- 2026-07-22 — Expanded `universal-actions-reference.md` to v2: 175 distinct, non-duplicate universal
  actions (brief asked for 100 minimum in one place, 200 in another — 175 real ones without padding was
  the honest stopping point). Added worksharing/worksets, links, views/view-templates, rule-based view
  filters, sheets, schedules, revisions/phases, export, model-health, project-level, and a proper
  annotation/dimensions group. Corrected two mistakes from v1: Create Dimension and Create Text Note were
  wrongly marked NEEDS_REVIEW — both are real, standard Revit API calls
  (`Document.Create.NewDimension`, `TextNote.Create`) — now listed as real actions. Mirrored into
  `.claude`. This is a reference list only — none of these 175 have C# fragments written yet except the
  ~70 already covered by existing/newly-built scripts from the earlier pass; building code for the rest
  is separate work, only on request.
- 2026-07-22 — v3: pulled Revisions out of the combined "Revisions & Phases" group into its own full
  lifecycle (create/edit/delete, add/remove from sheet, revision schedule, numbering — 12 items, 2
  NEEDS_REVIEW) per direct follow-up request. List now 182 items. Mirrored into `.claude`.
- 2026-07-22 — Reviewed a competing product's (an external tool) MCP skill file the user pasted in full. Did NOT
  copy/rebrand it (their tool names/structure are their own product's design, not general Revit facts —
  copying it wouldn't be right even with names stripped, and wouldn't fit our different backend anyway).
  Instead extracted the genuinely factual, non-proprietary Revit-API lessons and folded them into our own
  words: `Element.UniqueId` as a more sync-stable identifier than the integer `Id` (added to the Element
  ID glossary entry), discover-parameters-before-bulk-write discipline, large-query overflow caution,
  transient "Revit UI blocked" retry note, re-check model identity if a session runs long, and
  linked/face-hosted elements reporting `LevelId == InvalidElementId` (infer level from Z instead) — all
  added to `core.md`. Mirrored into `.claude`'s own core.md/glossary.md (kept in their existing
  Ajmal-voiced form, not overwritten with the Brain's genericized text).
  **Concrete capability upgrade, also inspired by the comparison**: all 11 graphics/visibility action
  fragments (hide, unhide, isolate, set-color, color-by-group, highlight-vs-rest, reset-overrides,
  report-overrides, set-transparency, section-box, set-view-crop) previously hardcoded `Document.ActiveView`
  — now accept an optional `targetViewIdInt` INPUTS variable, defaulting to active view but able to target
  any view directly (matches the "never hardcode, always a variable" rule the user set from the start —
  view was the one hardcoded exception). Verified brace-balanced (no live Revit this session to compile
  fully — test on one element before trusting a batch, same caveat as the other unverified additions).
  Mirrored into `.claude`.
- 2026-07-22 — Created `AGENT-SPEC.md` at Brain root: a full, self-contained, 11-section operating-manual
  spec (Purpose, Core Principles, Tool Reference, Workflows, Advanced Workflows, Lessons Learned, Best
  Practices, Anti-Patterns, Quick Reference, Response Standards, Future Extensions) per direct request,
  modeled on professional API documentation. Deliberate exception to the "small routed files" rule — this
  one document is meant to be read start-to-finish, not routed through. Linked from `START-HERE.md`. Not
  mirrored into `.claude` (scoped to Brain's live-model/bridge work, not the plugin-debugging skills kept
  there). Verified: all file references resolve, zero personal mentions, passes the consistency checker.
- 2026-07-22 — Caught and fixed 2 real gaps in `AGENT-SPEC.md` when asked directly whether it was
  actually aligned: fragment count said "71" (that's the checker-scoped subset, excluding `context/`) —
  corrected to "77 (71 + 6 context)". More importantly, the view-targeting upgrade to the 11 graphics
  actions (same session, earlier) wasn't mentioned anywhere in the spec — added a dedicated note in §3.4
  naming all 11 fragments. Lesson: a consolidated spec document needs an explicit re-check against recent
  changes, not just against file existence — passing the link/frontmatter checker doesn't mean the
  content is current.
- 2026-07-22 — **Built the Priority-1 "beat an external tool" item: 14 native, individually schema-validated MCP
  tools**, added to `mcp-server/index.js` (list/count/hide/unhide/isolate/reset-isolation/set-color/
  reset-overrides/transparency/select/set-parameter/report-parameters/move/delete). Key discovery: this
  needed ZERO changes to the Revit-side listener (`McpBridgeService.cs` — that's in this project's
  `.claude`-scoped `src/`, not the Brain) — it already accepts any C# generically via the same
  `{token, code, allowDestructive}` protocol `run_csharp` uses. `model_summary` was already proof this
  pattern works; these 14 extend it. Each tool builds the same proven C# as its matching `scripts/`
  fragment, sharing one `buildElementsClause()` generator (elementIds-priority, else category + optional
  family/numeric-param filter — mirrors `filter-by-category-and-numeric-param.cs`). `delete_elements`
  requires `confirm: true` as a literal in its own schema — refuses the call structurally, not just by
  convention. Bumped `mcp-server/package.json` and the server's own version string to 1.3.0. `node --check`
  passed on both copies (`.claude`'s original + Brain's mirror) — **not live-tested, no Revit connection
  this session**; verify each on one element before trusting a batch. Updated `AGENT-SPEC.md` (§3.4/§3.5
  split, §11 moved from reserved to done) and `universal-actions-reference.md` (both copies) in the same
  pass — caught and avoided the exact staleness mistake from the AGENT-SPEC.md episode.
- 2026-07-22 — **Split `mcp-server/index.js` (had grown to 822 lines) into one-file-per-tool**, mirroring
  the `scripts/` fragment pattern per direct request: `bridge-connection.js` (pipe plumbing),
  `shared/tool-result.js` + `shared/element-filter.js` (the generator all 14 native tools reuse),
  `tools/*.js` (17 files, one per tool: 3 original + 14 native), `tools/README.md` (routing index),
  `index.js` now a ~40-line entry point. Pure reorganization, no behavior change — verified two ways this
  time, not just `node --check`: (1) syntax-checked all 20 files, (2) a throwaway smoke test that imports
  every tool module against a fake server and confirms all 17 register with the right names/schemas, then
  a second test that actually INVOKES every handler with representative args (no live Revit — each
  cleanly hit the expected "bridge not connected" error, proving the C# generation path itself never
  throws). **Caught a real bug while doing this**: the `connectionKey()` function's null-character
  separator got corrupted into a literal raw NUL byte during the file write — `grep` flagged the file as
  "binary," but `node --check` still passed (a NUL is legal inside a JS template literal), so this would
  NOT have been caught without the extra scrutiny. Fixed via a byte-level buffer replace, re-verified
  clean — worth remembering: `node --check` alone is not sufficient proof a refactor preserved exact
  behavior. Also fixed the consistency checker not scanning `mcp-server/`'s new `tools/README.md` — added
  it scoped narrowly (not recursive over the whole `mcp-server` folder), after a first attempt broke on
  `node_modules`' own bundled README files. Mirrored the entire new structure into this Brain's copy;
  both `.claude`'s original and the Brain copy verified identically.
- 2026-07-21 — Added 13 new `filters/` fragments per direct request, after reviewing a user-proposed list
  against the existing library and flagging exact duplicates (`filter-by-phase.cs` and a bounding-box
  filter already covered by `filter-by-region.cs`) and overlaps (`filter-by-category.cs` already has a
  level scope; `filter-by-category-and-family.cs` already has category+family) before building anything.
  `filter-by-linked-model.cs` and `filter-by-mep-domain.cs` were declined — the former needs a different
  read-only/clash-source contract since actions can't Transaction against a linked document's elements,
  the latter was judged redundant with `filter-by-multiple-categories.cs`. Built: `filter-by-space.cs`,
  `filter-by-family.cs` (whole-model, no category), `filter-by-family-type.cs`, `filter-by-view.cs`,
  `filter-by-element-intersection.cs` and `filter-by-solid-intersection.cs` (real geometric clash via
  `ElementIntersectsElementFilter`/`ElementIntersectsSolidFilter`, not just bounding-box overlap),
  `filter-by-host.cs`, `filter-by-assembly.cs`, `filter-by-group.cs`, `filter-by-parameter-exists.cs`
  (presence/absence, distinct from `filter-by-parameter-text.cs`'s value match), `filter-by-design-option.cs`,
  `filter-by-material.cs`, `filter-by-level.cs` (whole-model, no category). Marked
  `filter-by-space.cs`/`filter-by-host.cs`/`filter-by-assembly.cs`/`filter-by-design-option.cs`/
  `filter-by-solid-intersection.cs` as not yet live-verified — each leans on a less-common API surface
  (`Space.IsPointInSpace`, `InsulationLiningBase.HostElementId`, `AssemblyInstance`, `DesignOption`,
  `GeometryCreationUtilities`) with no live Revit connection this session to confirm against. Updated the
  filters table in `scripts/README.md`. No `.claude` mirror exists in this repo to update.
- 2026-07-21 — Follow-up request for "filter by system type and filter by system name" surfaced a real
  bug in the existing `filter-by-system-type.cs`: it read `RBS_SYSTEM_NAME_PARAM` first with a `??`
  fallback to the Type parameter, but the Name parameter exists on nearly every pipe/duct element — so
  the fallback never actually ran and the fragment was silently matching System NAME while labeled and
  documented as System TYPE. Fixed in place (per the "update in place, don't fork -v2" rule):
  `filter-by-system-type.cs` now correctly reads `RBS_PIPING_SYSTEM_TYPE_PARAM`/
  `RBS_DUCT_SYSTEM_TYPE_PARAM` (ElementId → `AsValueString()`, since these point at the
  PipingSystemType/MechanicalSystemType element rather than storing a string directly). Added
  `filter-by-system-name.cs` as a new fragment carrying the old (correct, just mislabeled) behavior —
  matches one specific System instance's own name instead of its shared Type/classification. Updated the
  `scripts/README.md` row and re-pointed `skills/ajtools-mep-trace/SKILL.md`'s reference to the new
  system-name fragment, since that's what `trace-mep-circuits.cs`'s inline filtering step actually
  matches. Left `trace-mep-circuits.cs` and `action-color-by-group.cs` (both already correctly targeting
  System Name for their own purposes) unchanged — only the doc pointer was wrong, not their behavior.
- 2026-07-22 — Added 8 more `filters/` fragments per direct follow-up ("add this whatever also filter by
  insulation..."), covering the 6 real gaps flagged in the prior filter-list review plus insulation:
  `filter-by-tag-status.cs` (tagged/untagged in a view — confirmed 2020-era
  `IndependentTag.TaggedLocalElementId`, not the 2022+ `GetTaggedLocalElementIds()`, per
  `knowledge/live-model/tagging.md`), `filter-by-connection-status.cs` (open connector ends, reusing the
  `ConnectorManager` access pattern already verified live in `recipes/verify-duct-connectivity.cs`),
  `filter-by-pin-status.cs`, `filter-by-views.cs` (general Views, not just Sheets — feeds
  `action-duplicate-views.cs`/`action-place-viewport-on-sheet.cs`), `filter-by-warnings.cs` (promotes
  `context-all-warnings.cs`'s read-only report into an actionable `elements` set),
  `filter-by-electrical-system.cs` (Electrical Systems use an enum Circuit Type, not a document element
  like Piping/Duct SystemType, so this is deliberately its own fragment, not an extension of
  `filter-by-system-type.cs`). Insulation, per the user's explicit "not only insulation lining also" —
  covers DuctInsulation + DuctLining + PipeInsulation, not lining alone: `filter-by-insulation-status.cs`
  (is a pipe/duct insulated or bare, via `InsulationLiningBase.HostElementId` reverse lookup) and
  `filter-by-insulation-type.cs` (the insulation/lining elements themselves, by kind/type/material/
  thickness, with an optional `resolveToHost` to act on the underlying pipe/duct instead). Marked
  `filter-by-electrical-system.cs`, `filter-by-insulation-status.cs`, `filter-by-insulation-type.cs` as
  not yet live-verified — no electrical work in this model yet, and the insulation ones share
  `filter-by-host.cs`'s existing `InsulationLiningBase` uncertainty. Updated the filters table in
  `scripts/README.md`.
- 2026-07-22 — Follow-up request for "filter by length, filter by size": flagged that
  `filter-by-category-and-numeric-param.cs` already technically covers both (set `parameterName =
  "Length"` or `"Diameter"`), so built genuinely differentiated versions rather than near-duplicates.
  `filter-by-length.cs` binds directly to `BuiltInParameter.CURVE_ELEM_LENGTH` (guaranteed-correct, not a
  display-name guess) and gets its own discoverable name. `filter-by-size.cs`'s real value: handles round
  (Diameter) and rectangular (Width x Height) sizing TOGETHER in one pass — "the ø150 OR 300x200 ones"
  without knowing ahead of time which candidates are round vs. rectangular, which the single-parameter
  generic fragment can't do — plus an optional plain-text match against Revit's own calculated "Size"
  parameter. Updated `scripts/README.md`.
- 2026-07-22 — Full filter-list balance review found one real inconsistency: `filter-by-assembly.cs` and
  `filter-by-group.cs` both accept an Id OR a name string, but `filter-by-room.cs`/`filter-by-space.cs`
  required the Id already known — no way to say "elements in room 'Office 101'" directly. Fixed both in
  place (same "update in place" rule) to accept roomId/roomName/roomNumber (spaceId/spaceName/
  spaceNumber for the Space version), Id checked first, falling back to a Name+Number match (AND
  semantics per non-empty field, so a repeated number across levels can be disambiguated by adding the
  name). Backward-compatible — existing Id-only usage unchanged. Updated the `scripts/README.md`
  descriptions for both.
- 2026-07-22 — Reorganized `scripts/actions/` (35 files, flat, hard to scan) into 10 job-grouped
  subfolders per direct request, mirroring the grouping already used whenever these are listed out loud:
  `color-graphics/` (6), `visibility/` (7, including `action-set-pin-state.cs` — same "reversible
  display/protection toggle" class as isolate/hide/crop), `selection/` (1), `parameters-naming/` (4),
  `reporting/` (6), `qa-checks/` (1), `move-copy-rotate/` (3), `structural-changes/` (2), `sheets-views/`
  (3), `sheet-dates-revisions/` (2) — all moved with `git mv` to preserve history. Then fixed every
  reference across the repo so nothing broke: rewrote `scripts/README.md`'s Actions section into matching
  grouped subsections (verified programmatically — every `.cs` link in the file resolves to a real path),
  updated the composition examples and routing table in `scripts/architecture.md` and `AGENT-SPEC.md`,
  fixed the real markdown links in `knowledge/live-model/core.md` and `knowledge/reply-style.md`, and the
  path-prefixed comments in `scripts/creators/create-schedule.cs` and
  `scripts/examples/color-isolate-select-by-size.cs`. Bare filename mentions with no folder prefix
  (`knowledge/live-model/views.md`, `skills/ajtools-live-model/SKILL.md`, `scripts/filters/*.cs`) needed
  no change — they don't encode a path. Bonus fix caught along the way: 6 moved action files had
  `../knowledge/...` SOURCE comments that were already one level short even at the OLD flat depth
  (`scripts/actions/file.cs` needs `../../knowledge/`, not `../knowledge/`) — corrected to the true
  3-level path (`../../../knowledge/`) now that they're one level deeper, fixing a pre-existing
  inaccuracy, not just preserving it.
- 2026-07-22 — Direct follow-up on the color-graphics group found a real gap and a real bug. **Gap**:
  every existing color action (`action-set-color-uniform.cs`, `action-color-by-group.cs`, etc.) overrides
  PER-ELEMENT graphics only — there was no way to override an entire CATEGORY's line/fill color in one go
  (Revit's own Visibility/Graphics > Model Categories tab, `View.SetCategoryOverrides`/
  `GetCategoryOverrides` — a genuinely different API from the per-element `SetElementOverrides` every
  other action here uses). Built `action-set-category-color.cs` and `action-reset-category-graphics.cs` —
  both deliberately do NOT consume `elements` (a category override has no "which elements" step), so
  they're self-contained fragments, not chained after a filter. **Bug**: `action-color-by-group.cs`'s
  "random" mode picked each group's R/G/B independently, which can land two different groups on
  near-identical colors purely by chance — exactly what the user flagged ("its not be identical visually
  also i need different colors and randomize"). Fixed by hue-stepping evenly around the color wheel
  (360°/groupCount apart) instead of independent RGB randomization — GUARANTEES every group is visually
  distinct no matter how many groups there are, while a random starting hue re-rolled each run still
  gives real variety between runs. Added a plain HSV→RGB conversion function (no `System.Drawing`
  dependency assumed) and two new modes reusing the same guaranteed-distinct hue-stepping at different
  saturation/brightness bands: `"pastel"` and `"neon"`. Also built
  `knowledge/live-model/color-vocabulary.md` — a plain-language color-style reference (pastel/neon/muted/
  bold/jewel-tone → HSV saturation-brightness bands, plus ready-to-use RGB swatches) so a request like "I
  need pastel colors" resolves to real numbers instead of a guess; explains when to pick one swatch
  (`action-set-color-uniform.cs`/`action-set-category-color.cs`) vs. when to use `action-color-by-group.cs`'s
  new pastel/neon modes (multi-group requests — hue-stepping beats hand-picking N swatches). Added the new
  file to the `knowledge/live-model/README.md` index and updated the `scripts/README.md` color-graphics
  section (verified every link still resolves).
- 2026-07-22 — Closed out the color-graphics review per direct follow-up. Extended
  `action-set-category-color.cs` and `action-reset-category-graphics.cs` from a single
  `targetCategory` to a `targetCategories` array, so a whole system group (e.g. "Duct System" — Ducts +
  Fittings + Accessories + Flex Ducts + Insulation + Lining) gets the category-wide treatment in one run
  instead of one call per category; reports per-category skips (category not present in this document)
  rather than failing the whole batch. Confirmed with the user that `action-highlight-vs-rest.cs` already
  covers "color X red, gray out everything else in the view" — no new fragment needed there, already
  live-verified. `color-graphics/` is now 8 fragments, considered complete for this pass.
- 2026-07-22 — User asked about Revit's own View Filters (Visibility/Graphics > Filters tab,
  `ParameterFilterElement`) — a real, different mechanism from this repo's `filters/` folder (persists in
  the document, auto-applies to future elements too, no script re-run needed) — clarified the naming
  collision before building. Added the full create/apply/remove lifecycle to `color-graphics/`:
  `action-create-view-filter.cs` (builds a `ParameterFilterElement` + one `FilterRule` via
  `ParameterFilterRuleFactory` — text or numeric comparison, not yet live-verified — the rule-building
  surface has more moving parts than anything else in this group), `action-apply-view-filter.cs` (add to
  a view + set color/visibility, or update if already applied — mirrors the already-proven pattern
  recorded in `mep-color-standard.md` from real project work, so no caution flag needed here),
  `action-remove-view-filter.cs` (detach from a view, optional full delete from the document — gated
  like any other delete). Cross-referenced all three from `mep-color-standard.md` so the existing
  know-how there points at reusable fragments instead of describing the pattern to hand-write again.
  `color-graphics/` is now 11 fragments.
- 2026-07-22 — Direct follow-up: expanded `action-create-view-filter.cs` from 6 rule kinds to all 16
  `ParameterFilterRuleFactory` offers (added notequals/notbeginswith/notendswith, strict gt/lt, noteq, and
  value-presence hasvalue/hasnovalue — the two that need no comparison value at all). Added
  `action-create-selection-filter.cs` for Revit's OTHER real filter mechanism — `SelectionFilterElement`,
  an explicit element list instead of a rule ("adding elements to filter list" per the user's own words) —
  usable in the Filters tab exactly like a rule-based View Filter. Refactored
  `action-apply-view-filter.cs`/`action-remove-view-filter.cs` to look up by the shared `FilterElement`
  base class instead of `ParameterFilterElement` specifically, so both filter kinds work with the same
  apply/remove fragments without duplicating them. `color-graphics/` is now 13 fragments.
  Also built `knowledge/live-model/graphic-override-precedence.md` from a 9-level override-priority list
  the user supplied (Linework > per-element > Halftone/Underlay > View Filters > Phase overrides > View
  Depth/Beyond > Category overrides > MEP System overrides > Object Styles). Verified rather than
  transcribed blindly: the core skeleton (rows 1/2/4/7/9) matches well-established Revit behavior with
  high confidence; the three inserted rows (Halftone/Underlay, Phase overrides, View Depth/Beyond) are
  real, distinct mechanisms but their exact rank isn't independently confirmable from memory alone —
  marked moderate-confidence and kept as given rather than silently reordered, per this project's
  "verify, don't trust" rule. Cross-referenced from `knowledge/live-model/README.md`.
- 2026-07-22 — Built View Templates, a real gap already flagged (unbuilt) in
  `universal-actions-reference.md` items 100-102 since that audit — direct follow-up confirmed it. Added
  to `actions/sheets-views/` (view-management already lives there via `action-duplicate-views.cs`):
  `action-apply-view-template.cs` (apply an existing template to one or many views in one call),
  `action-create-view-template-from-view.cs` (capture a configured view's current settings as a new named
  template, explicitly re-applying it to the source view rather than assuming `View.CreateViewTemplate()`
  does that automatically — not yet live-verified), `action-set-view-template-controlled-params.cs`
  (Include/Exclude which parameters a template controls on a given view, via
  `GetNonControlledTemplateParameterIds`/`SetNonControlledTemplateParameterIds` — not yet live-verified).
  All three self-contained (operate on views, not `elements`) like the category/view-filter actions.
  Marked items 100-102 BUILT in `universal-actions-reference.md` with links to the new fragments.
- 2026-07-22 — Direct follow-up caught the missing paired undo: added `action-remove-view-template.cs`
  (detach a template from one or more views via `View.ViewTemplateId = ElementId.InvalidElementId`,
  optional `deleteTemplateElement` to remove the template definition from the document entirely — gated
  like any other delete). Same multi-view-array shape as `action-apply-view-template.cs`.
  `sheets-views/` is now 7 fragments, View Template lifecycle complete: apply, create-from-view,
  controlled-params, remove.
- 2026-07-22 — Direct follow-up: added `action-duplicate-view-template.cs`. Real gap —
  `action-duplicate-views.cs` consumes `elements` from `filter-by-views.cs`, which deliberately excludes
  `IsTemplate` views, so templates had no duplicate path at all. Self-contained by-name lookup instead of
  a filter (same shape as the other View Template fragments), duplicates via the same
  `View.Duplicate(ViewDuplicateOption.Duplicate)` mechanism `action-duplicate-views.cs` already uses for
  regular views, then renames the copy. `sheets-views/` is now 8 fragments.
- 2026-07-22 — Direct follow-up caught the last real gap in the View Template group: nothing could check
  whether a view already has a template applied before acting on it. Added
  `action-report-view-template-status.cs` — read-only, reports per view: no template / which template (by
  name) / which parameters are excluded from its control. Resolves excluded-parameter names via
  `LabelUtils.GetLabelFor((BuiltInParameter)id)` first (most of the Include/Exclude list are built-in
  parameters, negative Ids, not real `ParameterElement`s), falling back to `ParameterElement.Name` for
  the rare custom/shared-parameter case. Optional `checkAllViews` to report every real view in the
  project at once instead of just the active view. `sheets-views/` is now 9 fragments — View Template
  group considered complete: apply, create-from-view, controlled-params, remove, duplicate, status.
- 2026-07-22 — Fixed the architectural gap flagged in the last review: `filter-by-views.cs` deliberately
  excludes `IsTemplate` views, and nothing filled that hole, so every View Template fragment needed its
  own bespoke by-name lookup instead of composing through the normal filter+action system. Built
  `filter-by-view-templates.cs` — name-contains + a `usage` mode (`all`/`used`/`unused`, "used" meaning
  applied to at least one real view via `View.ViewTemplateId`) — covering "get all templates", "get used
  templates", and "get unused templates" as one fragment rather than three, since they're the same query
  with the mode flipped. For "purge unused templates": didn't write a new delete action since
  `actions/structural-changes/action-delete-elements.cs` already covers deletion generically — instead
  added `scripts/examples/purge-unused-view-templates.cs`, a fully-assembled
  filter-by-view-templates.cs(usage="unused") + action-delete-elements.cs composition with the same
  MANDATORY explorer-first safety note the delete action itself carries. Cross-linked from
  `filter-by-views.cs`'s own comment. Updated the `scripts/README.md` filters and examples tables.
- 2026-07-22 — Closed the remaining 4 color-graphics gaps from the last balance review, all in one pass:
  `action-set-halftone.cs` + `action-set-category-halftone.cs` (per-element and category-level, paired
  the same way `action-set-color-uniform.cs`/`action-set-category-color.cs` already are — both
  read-modify-write via `view.GetElementOverrides`/`GetCategoryOverrides` first so an existing color
  override isn't wiped out just by toggling halftone). `action-set-line-style.cs` +
  `action-set-category-line-style.cs` (line WEIGHT 1-16 and line PATTERN by name, incl. a `"solid"`
  shortcut via `LinePatternElement.GetSolidPatternId()` — every prior action in this group only ever
  touched color, never weight/pattern; combined both properties into one fragment per level rather than
  four separate ones, since they're both "line style" and genuinely small). `action-report-view-filters.cs`
  (lists every `FilterElement` — View Filter or Selection Filter, distinguished by type — and which real
  views currently have each applied via `IsFilterApplied`). `action-report-category-overrides.cs` (the
  reverse lookup for category-level Set actions — scoped by default to categories actually present in the
  view, not every category in the document, to avoid an unbounded scan; reused the
  `hasAnyOverride`/`IsSurfaceForegroundPatternVisible`-defaults-true detection technique from
  `action-report-graphic-overrides.cs` verbatim, extended with the new weight/pattern properties).
  `color-graphics/` is now 18 fragments — this group considered fully complete, no further gaps
  identified.
- 2026-07-22 — Final completeness pass caught one asymmetry: `action-set-transparency.cs` (per-element)
  never got a category-level sibling, unlike color/halftone/line-style which were just paired at both
  levels in the same session. Added `action-set-category-transparency.cs` to close it. `color-graphics/`
  is now 19 fragments — genuinely complete, every per-element Set action now has its category-level
  counterpart.
- 2026-07-22 — Started the `visibility/` group review, caught a gap already flagged earlier in the
  session but deferred: category-level visibility on/off (`View.SetCategoryHidden`, the checkbox column
  in Visibility/Graphics > Model Categories) is a completely different mechanism from
  `action-hide-elements.cs` (per-element) and had nothing built, even after the identical element/category
  split was just done for every color-graphics action. Added `action-set-category-visibility.cs`
  (checks `Category.get_AllowsVisibilityControl(view)` first — some categories can't be toggled in some
  view types) and its reverse lookup `action-report-category-visibility.cs`. The report fragment scopes
  to categories with an element ANYWHERE in the model by default, not just in the view — scoping to
  "what's visible in the view" would be self-defeating for a hidden-category check, since a hidden
  category's elements don't appear in a view-scoped collector at all (different from
  `action-report-category-overrides.cs`, where view-scoping is correct). `visibility/` is now 9
  fragments.
- 2026-07-22 — `parameters-naming/` review: user asked for Set/Get/Copy/Remove — Set and Copy already
  existed, Get was already covered by `action-report-parameters.cs`, so the one real gap was Remove.
  Added `action-remove-parameter-value.cs`, honest about a real Revit API limit: String/ElementId can be
  genuinely cleared (empty string / `InvalidElementId`), but Double/Integer have no public "unset" —
  only reset to 0 — and the report line says so explicitly rather than calling both cases "cleared".
  Bigger ask, a real and valuable gap: "what parameters does this element even have" — distinct from
  `action-report-parameters.cs`, which only reports VALUES for names already known. Built
  `action-report-parameter-inventory.cs` (`reporting/`): walks `Element.Parameters` (+ the Type's, if
  `includeTypeParameters`), classifies each as Built-in / Shared / "Project or Family (not shared)" — the
  third bucket is an honest limit, not a guess: Revit's public API genuinely cannot tell a Family Editor
  parameter from a Project Parameter once both are loaded onto a project element, no provenance flag
  exists — plus parameter group, storage type, Instance vs Type, read-only, and current value.
  `sampleOnly` (default true) inventories just the first element for speed; set false to union across a
  mixed set that might vary by family/type. This is the discovery step `core.md` already calls for
  ("don't guess a parameter name from a plausible name") but never had a dedicated fragment until now.
- 2026-07-22 — Double-checked the parameters-naming work per direct request and caught a real, meaningful
  gap: `action-set-parameter-value.cs`, `action-copy-parameter-value.cs`, and the just-added
  `action-remove-parameter-value.cs` all used `e.LookupParameter(name)` — INSTANCE parameters only. A
  Type-level name (Manufacturer, Model, Type Comments, and plenty of others genuinely live there) silently
  skipped every element with no indication why, even though `action-report-parameters.cs`/
  `action-report-parameter-inventory.cs` already fall back to Type. Fixed all three to fall back the same
  way (source AND target independently, for the copy action), reporting Instance-level vs Type-level
  counts separately since a Type edit applies to every instance sharing that type, not just the ones in
  `elements` — a count that didn't distinguish the two would be misleading either way. **Deliberately did
  NOT apply the same fix to `action-renumber-sequential.cs`** — checked it specifically and a Type
  fallback there would be an actual bug: renumbering needs each element to get a DIFFERENT value, but a
  shared Type parameter would just get overwritten repeatedly as the loop progresses, so only the last
  element processed would stick, silently corrupting the intended 101/102/103 sequence. Added a comment
  explaining why it's deliberately instance-only, so this doesn't get flagged as a gap again later.
  `action-rename-element.cs` uses `Element.Name` directly, not `LookupParameter`, so it was never affected.
- 2026-07-22 — `reporting/` gap check found one real item: `action-count-and-report.cs`'s breakdown table
  is hardcoded to size-related parameters (Width/Height/Diameter/Size/Nominal Diameter) — no way to count
  by an arbitrary parameter ("how many per level", "how many per system type", "how many per family").
  Added `action-count-by-group.cs`, reusing the proven storage-type-aware `groupKey` logic from
  `action-color-by-group.cs` verbatim (Double via `AsValueString`, ElementId resolved to the referenced
  element's name, Integer with Yes/No detection, String, "None" fallback), plus the Family/Family-and-Type/
  Category special cases already used in `action-report-parameters.cs`, plus the Type-parameter fallback
  from the same session's earlier fix. `reporting/` is now 8 fragments.
- 2026-07-22 — Direct follow-up: "how many per room, zone, phase, space" split into two different answers.
  Phase already works with `action-count-by-group.cs` (`groupByParameterName = "Phase Created"`) —
  Phase Created/Demolished really is a normal ElementId-storage parameter, so the existing generic
  fragment resolves it fine via its existing ElementId-to-name branch. Room/Space/Zone are structurally
  different — most MEP elements carry no "Room"/"Space" parameter at all, so `action-count-by-group.cs`'s
  plain `LookupParameter` can't find anything to group by; needs the same spatial `IsPointInRoom`/
  `IsPointInSpace` test `filter-by-room.cs`/`filter-by-space.cs` already use. Built
  `action-count-by-spatial-container.cs` covering all three via a `containerType` mode ("room"/"space"/
  "zone") — Zone is one hop past Space (`Space.Zone`), so it reuses the same Space-containment test rather
  than needing its own spatial pass. Carries filter-by-space.cs's existing not-yet-live-verified caveat on
  `Space.IsPointInSpace` for the space/zone modes; the room mode reuses the already-verified
  `Room.IsPointInRoom` path. `reporting/` is now 9 fragments.
- 2026-07-22 — Direct follow-up: full Phase management, a real gap — nothing created, renamed, or
  assigned elements to a Phase anywhere in this library. Added 3 fragments to `parameters-naming/`
  (closest thematic fit — Phase Created/Demolished are genuinely parameters):
  `action-create-phase.cs` (`Document.Phases.Insert()` after the current last phase, skips existing
  names), `action-rename-phase.cs` (old-name -> new-name pairs, batch-capable) — both self-contained, not
  yet live-verified. `action-set-element-phase.cs` — the one that actually needed a NEW mechanism, not
  just a phase-specific wrapper: `action-set-parameter-value.cs` can't set Phase Created/Demolished at
  all, since those are ElementId-storage parameters and that action only ever handles String/Double.
  Pairs with the already-existing reverse lookup, `filter-by-phase.cs` (finds elements BY their assigned
  phase). `parameters-naming/` is now 8 fragments.
- 2026-07-22 — "Best and best" full sweep per direct request: closed out the phase lifecycle (discovery +
  delete were still missing after the previous pass) and reviewed every action group that hadn't had its
  own gap-check yet this session (`selection/`, `qa-checks/`, `move-copy-rotate/`, `structural-changes/`,
  `sheet-dates-revisions/`). Found real gaps in all of them — three (mirror, group/ungroup, join geometry)
  had already been explicitly identified as real in a PAST session's "universal actions" audit and
  deliberately deferred as "not urgent enough to build unprompted"; this pass was exactly the moment to
  build them.
  - `parameters-naming/`: `action-report-phases.cs` (list, in order — the missing discovery step for
    rename/delete/set-element-phase, all of which need an exact existing name) and `action-delete-phase.cs`
    (destructive, gated the same way as `action-delete-elements.cs`) — completes Create/Rename/Delete.
  - `selection/`: `action-select-elements.cs` only ever REPLACED the whole selection — added a `mode`
    input (`replace`/`add`/`remove`) so "add these to what I've got" and "deselect these" both work now.
  - `qa-checks/`: `action-find-duplicate-values.cs` — flags elements sharing the same VALUE in a named
    parameter (duplicate Mark, duplicate Equipment Tag), a genuinely different check from
    `action-find-duplicates.cs`'s duplicate LOCATION.
  - `move-copy-rotate/`: `action-mirror-elements.cs` — `ElementTransformUtils.MirrorElements` across a
    vertical plane through two plan points, copy or in-place.
  - `structural-changes/`: `action-group-elements.cs`/`action-ungroup-elements.cs`
    (`Document.Create.NewGroup`/`Group.UngroupMembers`) — Model Groups are their own category
    (`OST_IOSModelGroups`), so `filter-by-category.cs` already finds them for `action-ungroup-elements.cs`,
    no new filter needed. `action-join-geometry.cs` (`JoinGeometryUtils`) — many-to-one (join a batch to
    ONE target element), the common real case; join/unjoin both driven by a `mode` input.
  - `sheet-dates-revisions/`: `action-remove-revision-from-sheet.cs` — the reverse of
    `action-assign-revisions-by-sheet-date.cs`, matched by Revision Description rather than date, carries
    the same auto-purge gotcha noted in `revisions.md`.
  9 new fragments plus the 1 modified (`action-select-elements.cs`) across 6 groups. Every group in
  `scripts/actions/` has now had a dedicated gap-check pass this session.
- 2026-07-23 — Full live-verification pass against the connected Revit 2020 bridge: ran every fragment for
  real (composed with its filter, fresh read-back after, throwaway test fixtures cleaned up after). Found
  and fixed several real bugs, and confirmed three genuine Revit-2020 API gaps worth remembering so nobody
  re-discovers them the hard way:
  - **No API to activate a Design Option** — only a read-only `DesignOption.GetActiveDesignOptionId`
    exists anywhere in the assembly. `action-set-design-option.cs` now requires the option be activated
    manually first.
  - **No API to create a Scope Box** — confirmed via exhaustive reflection, nothing anywhere. UI-only
    (View tab > Scope Box). `create-scope-box.cs` and the resize half of `action-update-scope-box.cs` now
    report this instead of attempting it (the old resize workaround would have permanently destroyed a box
    with no way to recreate it).
  - **`Document.Phases` is read-only** — Insert/Append both throw "Collection is read-only" at runtime, and
    no other Phase-creation API exists. UI-only (Manage > Phases). `action-create-phase.cs` now reports
    this instead of throwing a compile error.
  - Also version-specific, not impossible, just wrong-API-for-this-version: `SpecTypeId`/`GroupTypeId`
    (2022+) don't exist on 2020 — use legacy `ParameterType`/`BuiltInParameterGroup`
    (`action-add-project-parameter.cs`). `PDFExportOptions` doesn't exist on 2020 at all — real PDF export
    here goes through `Document.PrintManager` routed through a virtual printer driver
    (`action-export-sheets-to-pdf.cs`, rewritten, not yet fired for real — this system has a physical
    printer in its device list too). The assumed `CombinedParameterRule` class for schedule Combined
    Parameter fields never existed at all, in any version — real class is `TableCellCombinedParameterData`
    (`action-add-schedule-calculated-field.cs`, fixed and verified).
  - Real logic bug, not a version issue: `action-fillet-elements.cs` mode="arc" trimmed its two source
    lines by reassigning `LocationCurve.Curve` in place — silently a no-op (no exception, clean commit, but
    the geometry never actually changes) whenever the two lines already share a coincident endpoint, which
    is the normal case for filleting an existing corner. Fixed via delete+recreate instead.
    `action-trim-extend-elements.cs` shares the same technique and likely the same exposure for Model/
    Detail Lines specifically (Ducts confirmed unaffected) — flagged in its header, not yet forced.
  - `filter-by-space.cs` matched on `Element.Name`, which Revit auto-combines as "{name} {number}" for
    Space (Room too, near-certainly) the moment any Number exists — which is always, one gets auto-assigned
    at creation even if never touched. Fixed to read `BuiltInParameter.ROOM_NAME` instead, which holds the
    plain name. Noted in `create-space.cs` too.
  - Everything else checked (~150 fragments: all of `filters/`, `context/`, `commands/`, most of
    `creators/`, `actions/move-copy-rotate/`, and the Tier 1/2 fragments) came back zero-bug on first real
    run. Full per-file results in `scripts/README.md`'s per-fragment notes.
  - Continuing the same pass, through `recipes/` and the remaining pre-existing fragments — found more:
  - **`FilteredElementCollector.UnionWith()` does not preserve either side's own quick-filters** —
    `.WhereElementIsNotElementType()` applied before `.OfCategory(...).UnionWith(...)` on each piece
    silently loses that filter in the merged result (confirmed empirically: 52 elements returned instead of
    the real 4, every extra one a TYPE element). Fix: apply `.WhereElementIsNotElementType()` ONCE, after
    all `UnionWith` calls, on the combined collector. Hit `filter-by-system-type.cs`,
    `filter-by-system-name.cs`, and `recipes/trace-mep-circuits.cs` — the only 3 fragments in the library
    using `UnionWith`. This retroactively invalidated an earlier "VERIFIED" claim for the first two — their
    original test only exercised a simplified single-category reproduction that never hit the broken
    multi-category code path. Lesson: a simplified re-test can pass while the real file still has a bug, if
    the simplification drops the exact broken shape.
  - `MechanicalUtils.BreakCurve` reassigns which element Id keeps which physical segment after a split —
    don't trust an Id across a cut. `recipes/split-duct-near-equipment.cs` assumed the original `duct.Id`
    was always the equipment-side piece; confirmed live it can come back as the far piece instead, backwards
    from what the script reported. Fixed by determining near/far geometrically.
    `recipes/slice-trunk-for-sizing.cs` already defended against this correctly (re-locates each cut target
    geometrically every time); flagged a separate, still-open, unconfirmed risk in its header instead —
    `trunkDir`'s sign depends on which end of an arbitrary input piece Revit calls "0", so `skipLastTakeoff`
    could silently protect the wrong end if that piece was drawn backwards.
  - Chased a false alarm through several fixture rebuilds: thought BreakCurve was also silently dropping the
    equipment-side *connection* itself (not just the label), based on a test duct that kept behaving
    unreliably. Root cause, caught by the user: the test fixture drew the duct along an assumed axis
    (`XYZ.BasisX`) instead of the equipment connector's own real outward direction
    (`Connector.CoordinateSystem.BasisZ`) — once the fixture read the connector's real direction first, the
    connection survived the split fine, every time. Lesson worth keeping: always read a connector's own
    `CoordinateSystem.BasisZ` before drawing toward or from it — never assume an axis, in a test fixture or
    anywhere else.
  - `SpatialElement.Volume` does not exist as a property on Revit 2020 (only `Area` does) — a genuine
    compile error, not a version-string issue. `action-report-room-space-data.cs` fixed to read
    `get_Parameter(BuiltInParameter.ROOM_VOLUME)` instead.
  - A folder-count cross-check (actual Glob/`find` results vs. this pass's own summary claims) caught 2
    fragments that had been counted as "done" without ever actually being tested:
    `action-flip-elements.cs` and `action-report-room-space-data.cs` (the Volume fix above). Worth doing
    this kind of recount on any large verification pass — a summary tally is not itself verification.
  - `action-flip-elements.cs`: checked 13 loaded families across Mechanical Equipment and Duct Terminal —
    none support flip in this project. Graceful skip-path confirmed correct; the positive flip path remains
    genuinely blocked on a flip-capable family (door/window or similar) being loaded.
- 2026-07-23 — Consistency + safety pass across the Brain (no live Revit connection this session — code-
  reviewed and pattern-matched against already-proven sibling code, not live-executed; treat the same as
  any other NOT-live-verified addition):
  - `tools/verify-consistency.ps1` check #3 was scanning `scripts/actions/` non-recursively — since the
    2026-07-22 subfolder reorg (color-graphics/, move-copy-rotate/, etc.), it was silently checking 0 of
    112 nested fragments instead of all of them. Added `-Recurse` and fixed the relative-path build.
  - `mcp-server/tools/set-parameter-value.js` accepted a call with both `stringValue` and `numericValueMm`
    set, or neither — silently preferred numeric. Added an explicit exactly-one-of check in the handler.
  - `AGENT-SPEC.md` §3.4/§3.5 had drifted: "12 tools share the filter shape / 9 take targetViewId" is
    really 13/7 (`reset_isolation` is the one tool with no element filter); "77 C# fragments (71 + 6
    context/)" is really 206 (197 + 9 context/) — counted directly off disk, not off an old summary.
  - `scripts/README.md` had two dead links to an `ajtools-conventions.md` that doesn't exist (an old name
    for this file) — repointed both to `brain-log.md`.
  - `universal-actions-reference.md`: flagged items 142 (Create Phase) and 176 (Set Active Design Option)
    as CONFIRMED IMPOSSIBLE via API, matching what this log already established above (Document.Phases is
    read-only; no DesignOption activation API exists) — they were listed as normal buildable actions with
    no warning. Corrected items 64/65/101/102, stale in the *safe* direction (marked NEEDS_REVIEW / not
    yet live-verified when `scripts/README.md` already shows them live-verified 2026-07-22). Added an
    implementation-index appendix listing the 34 real `scripts/filters/` fragments and the 3
    `action-set-halftone.cs`/`action-set-line-style.cs`/`action-set-category-line-style.cs` fragments that
    had no entry anywhere in this catalog.
  - Did NOT touch the `SpatialElement.Volume` "does not exist on Revit 2020" claim above (this same log,
    2026-07-23 entry) — it's specifically sourced from a live compile against the real bridge, which is
    stronger evidence than a training-data recollection with no way to re-verify it here. Flagging it as
    worth a second look next time someone's live against the bridge, not correcting it blind.
  - 25 script fragments had a stale in-file "NOT YET LIVE-VERIFIED" header contradicted by
    `scripts/README.md` already showing them live-verified 2026-07-22 (mostly the sheets-views/ and
    parameters-naming/ groups, plus a handful of others) — updated each header to match, carrying over the
    specific verification detail from the README rather than a generic stamp. Left the 3 files where the
    header and README already agreed (`action-set-view-workset-visibility.cs`, `action-reload-links.cs`,
    `ray-trace-to-ceiling.cs` — each blocked on a real environment gap, not a documentation gap).
  - Added try/catch/RollBack to every bare `using (var t = new Transaction(...))` that had none: the 8
    sequential transactions in `create-parametric-box-family-with-duct-connector.cs`, the 3 in
    `place-fcu.cs`, and `place-terminals-checkerboard.cs`/`set-space-airflow.cs` (2 and 1 respectively).
    Same failure mode in all of them: a mid-script exception left the `using` block's implicit Dispose as
    the only safety net, which the Revit API does not guarantee cleanly rolls back — now each stage reports
    plainly which named stage failed and rolls back cleanly instead.
  - Added the missing null checks after an `as`-cast in `draw-main-duct-with-cap.cs` (`fcu`/`room`, used
    immediately with no check, unlike every sibling recipe) and `split-duct-near-equipment.cs`
    (`equipment.MEPModel` — the file's own INPUTS comment documents `equipmentId` as "or any element",
    which includes non-MEP FamilyInstances that would NRE on the old code).
