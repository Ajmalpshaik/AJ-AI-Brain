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
