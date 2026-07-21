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
- 2026-07-22 — Reviewed a competing product's (Nonica) MCP skill file the user pasted in full. Did NOT
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
- 2026-07-22 — **Built the Priority-1 "beat Nonica" item: 14 native, individually schema-validated MCP
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
