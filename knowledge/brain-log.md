# Brain — Change Log

Dated, short entries for changes to the Brain itself — a new skill created, a knowledge file split, a
script fragment added or retired, a technique that finally got solved. This is a record of the Brain's
own growth, separate from `live-model/log.md` (which is a record of what was done *to a Revit model*).

Add a line here whenever [`skills/brain-self-maintain/SKILL.md`](../skills/brain-self-maintain/SKILL.md)
creates, splits, or retires something, or whenever any other skill's "after finishing" step says to log
here. **Keep entries to 1–3 lines** — the full story lives in the git commit history and in the files the
change touched (fragment headers, `scripts/README.md` rows, `AGENT-SPEC.md`); this log is the index, not
the archive. (Compressed from long-form entries 2026-07-23, with the user's OK — see git history for the
original full text.)

## Open items — the single current list (supersedes any "Next" list in older entries)

**Doable in any session:** none right now — the multi-session health pass (Parts 1–6, 2026-07-23) is
complete. New items land here as they surface.

**Needs a live Revit session (bridge connected, on the Windows machine):**
1. Run `tools/verify-consistency.ps1` once to confirm the 2026-07-23 recursion fix works on real
   PowerShell (the fix mirrors the proven `.mjs` logic but has never executed). Also run
   `tools/invoke-bridge.ps1 -Ping` once — same session found it sent a UTF-8 BOM the Node client never
   sends (fixed to no-BOM 2026-07-23, matching the proven client byte-for-byte, but the fallback caller
   itself has never been ping-tested live).
2. Live-verify the 14 native MCP tools (structurally tested via `npm test` only).
3. `action-reload-links.cs`: confirm `LinkLoadResultType.LinkNotNeeded` is a real enum member (see the
   file's header flag — suspected wrong identifier, would be a compile error).
4. `action-add-project-parameter.cs`: quick compile check — the Revit-2020 legacy-API fix was re-applied
   here 2026-07-23 after being found only in the `.claude` mirror copy.
5. The fixture-blocked positive paths (worksharing, Assembly, Design Option, insulation, electrical,
   links, Ceilings, a flip-capable family, the PDF print go-ahead) — each listed with its exact blocker
   in `scripts/README.md`'s per-fragment notes.
6. The 2026-07-23 transaction/null-check safety fixes to `create-parametric-box-family-with-duct-
   connector.cs`, `place-fcu.cs`, `place-terminals-checkerboard.cs`, `set-space-airflow.cs`,
   `draw-main-duct-with-cap.cs`, `split-duct-near-equipment.cs` — code-reviewed only, none live-executed.

## Log

- 2026-07-22 — Element ID glossary entry + standing rule: element reports always include Element ID.
- 2026-07-22 — Reply-style rule: a narrowed request ("the 300x300 VCDs") gets an item list with IDs, not
  a bare count. Updated `reply-style.md`, live-model skill, `scripts/architecture.md`.
- 2026-07-22 — Universal-actions audit: built `action-delete-elements.cs`, `action-rename-element.cs`,
  `create-schedule.cs`; wrote `knowledge/universal-actions-reference.md`.
- 2026-07-22 — Expanded the reference to 175 actions (v2), then 182 (v3, full Revisions lifecycle).
- 2026-07-22 — Reviewed a competitor's MCP skill text: did NOT copy it; folded the factual Revit lessons
  into `core.md`/glossary (UniqueId stability, discover-params-first, overflow caution, linked-element
  LevelId). Upgrade inspired by it: all 11 graphics/visibility fragments got optional `targetViewIdInt`.
- 2026-07-22 — Created `AGENT-SPEC.md` (11-section operating manual); follow-up caught 2 staleness gaps
  in it same-day. Lesson: a consolidated spec needs a deliberate re-check after changes, not just a link
  check.
- 2026-07-22 — Built 14 native schema-validated MCP tools (Node-side only — the Revit listener needed
  zero changes); split `mcp-server/` one-file-per-tool. Caught a NUL-byte corruption `node --check`
  missed — lesson: `node --check` alone is not sufficient proof of a refactor.
- 2026-07-21 — Added 13 `filters/` fragments after de-duplicating a proposed list; declined 2 as
  redundant/wrong-contract.
- 2026-07-21 — Found `filter-by-system-type.cs` silently matching system NAME, not TYPE (a `??` fallback
  that never ran). Fixed in place; split the old behavior into `filter-by-system-name.cs`.
- 2026-07-22 — Added 8 more filters (tag status, connection status, pin, views, warnings, electrical
  system, insulation status/type), then `filter-by-length.cs` + `filter-by-size.cs` (round + rectangular
  handled together).
- 2026-07-22 — Balance fix: `filter-by-room.cs`/`filter-by-space.cs` now accept Name/Number, not just Id.
- 2026-07-22 — Reorganized `scripts/actions/` (35 flat files) into 10 job-grouped subfolders with
  `git mv`; fixed every cross-reference; corrected 6 pre-existing wrong SOURCE paths found on the way.
- 2026-07-22 — color-graphics pass: added category-level color/reset (different API from per-element);
  fixed `action-color-by-group.cs` random mode to hue-step (guaranteed-distinct colors) + pastel/neon
  modes; wrote `color-vocabulary.md`. Later passes completed the group to 19 fragments (halftone,
  line style, filters/overrides reporting, category transparency) — every per-element Set now has a
  category-level counterpart.
- 2026-07-22 — View Filters lifecycle (create/apply/remove, all 16 rule kinds, Selection Filters too) +
  `graphic-override-precedence.md` (9-level priority list, moderate-confidence rows marked).
- 2026-07-22 — View Template lifecycle complete: apply, create-from-view, controlled-params, remove,
  duplicate, status report + `filter-by-view-templates.cs` and a purge-unused-templates example.
- 2026-07-22 — visibility/: added category-level visibility on/off + report (different mechanism from
  per-element hide; report deliberately scopes to whole model, not the view).
- 2026-07-22 — parameters-naming/: added `action-remove-parameter-value.cs` (honest about Double/Integer
  having no true "unset") + `action-report-parameter-inventory.cs` (discover what parameters exist).
- 2026-07-22 — Type-parameter fallback fix on set/copy/remove parameter actions (was Instance-only,
  silently skipping Type-level names like Manufacturer). Deliberately NOT applied to
  `action-renumber-sequential.cs` — a Type fallback there would corrupt the sequence (documented why).
- 2026-07-22 — reporting/: `action-count-by-group.cs` (count by ANY parameter) +
  `action-count-by-spatial-container.cs` (Room/Space/Zone containment — spatial test, not a parameter).
- 2026-07-22 — Phase management (create/rename/report/delete/assign) + final gap sweep across
  selection/qa/move/structural/sheet-dates groups: 9 new fragments (mirror, group/ungroup, join,
  duplicate-value QA, select modes, remove-revision-from-sheet).
- 2026-07-23 — **Full live-verification pass of `scripts/` against the real Revit 2020 model** (~150
  fragments run for real; per-fragment results in `scripts/README.md`). Confirmed 3 hard API gaps
  (no Scope Box creation, no Phase creation, no Design Option activation) and multiple version traps
  (PDF via PrintManager, no `SpatialElement.Volume`, `TableCellCombinedParameterData`). Real bugs found
  and fixed: `UnionWith()` losing quick-filters (3 fragments), `BreakCurve` Id reassignment,
  fillet's in-place `LocationCurve` no-op, Space name matching. Key lessons now permanent in
  `AGENT-SPEC.md` §6. Meta-lessons: a simplified re-test can miss the broken shape; recount summary
  tallies; read a connector's real `BasisZ` before drawing.
- 2026-07-23 — **Part 1** (multi-session health pass, no Revit): built `tools/verify-consistency.mjs`
  (portable checker; found the ps1's recursion blind spot — 0 of 112 actions/ files checked since the
  reorg); static re-audit of all known pitfalls (clean); added `mcp-server/test/smoke.test.js` (`npm
  test`, all 17 tools); pinned `@hono/node-server` — audit now 0 vulnerabilities.
- 2026-07-23 — **Part 2**: mcp-server review. Fixed `model_summary`'s inconsistent error casing;
  `set_parameter_value` now rejects both/neither of stringValue/numericValueMm (was silently wrong);
  hardened `readDiscoveryInfo()` against a TOCTOU race and corrupt JSON. Documented (not built) the
  known reuse-a-dead-socket 90s-timeout limitation on `getConnection()`.
- 2026-07-23 — **Part 3**: static review of the 14 README-flagged unverified fragments — 13 clean, 1
  precise flag (`LinkNotNeeded` enum member in `action-reload-links.cs`). Found 18 fragments whose file
  headers still said "NOT YET LIVE-VERIFIED" though README recorded them live-verified — replaced each
  stale banner with a pointer to README as the single source of truth (root-cause fix, not a re-sync).
- 2026-07-23 — **Part 4**: self-audit found Part 1 never back-ported the recursion fix to the ps1 checker
  (fixed; untested — no PowerShell here) and the skill only instructed the ps1 (fixed). `AGENT-SPEC.md`
  staleness pass: corrected fragment count (77 → 206), tool counts (12/9 → 13/6), added the live-pass
  lessons to §6, un-listed Purge Unused from "unbuilt", stamped a re-check date. Fixed same-class drift
  in `universal-actions-reference.md` (items 101/102/171). Bumped mcp-server to 1.3.1.
- 2026-07-23 — **Part 5**: finished the static sweep (the 11 remaining fragments). **Real find**:
  `action-add-project-parameter.cs` still used `SpecTypeId`/`GroupTypeId` (2022+-only) in live code while
  README claimed it "fixed for Revit 2020" — git history shows the fix only ever landed in the `.claude`
  mirror, never here; re-applied (legacy `ParameterType`/`BuiltInParameterGroup`). Also exposed that
  Part 1's pitfall grep counted that hit as "the fix is present" when it was the bug — comment-mention
  vs live-code distinction matters. Found 5 more stale "NOT YET LIVE-VERIFIED" headers Part 3's regex
  missed (export-schedule-to-csv, duplicate-type, split-elements, create-dimension, create-filled-region)
  — same pointer fix. Corrected `action-split-elements.cs`'s "far-side pieces" claim (BreakCurve can swap
  sides — reconnect is side-agnostic, the label wasn't). Flagged connect-terminal-branch's by-design
  vertical-riser assumption. 4 recipes + set-workset/duplicate-type/export-csv otherwise clean.
  Compressed this log from ~600 lines of essays to this form (user-authorized; full text in git).
- 2026-07-23 — **Part 6** (final part): measured the whole reading path — `START-HERE.md` + `INDEX.md`
  are already lean (~1,000 words, left alone); the real weight was `scripts/README.md` (6,270 words, read
  nearly every scripting session). Compressed ~45 verbose verification narratives in its rows to compact
  status markers (✓/BLOCKED/IMPOSSIBLE + date + active blocker), stories staying in fragment headers —
  −538 words. Fixed 4 stale references found on the way: two `CLAUDE.md` mentions (that file doesn't
  exist in this Brain — rules live in `START-HERE.md`) and two `ajtools-conventions.md` mentions
  (doesn't exist here either — fragment changes log to THIS file; the recipes-table source now points at
  `live-model/revisions.md`). Added the missing verification story to `action-delete-phase.cs`'s header
  so its README pointer resolves. Checker + tests clean.
- 2026-07-23 — Coverage question follow-up: reviewed `tools/invoke-bridge.ps1`, the one pipeline file the
  Parts 1–6 pass never opened. Protocol matches the Node client exactly (same discovery file, same
  `{token, code, allowDestructive}` newline-delimited shape, same ping payload) — one real difference
  found and fixed: it wrote UTF-8 WITH a BOM (StreamWriter + `Encoding.UTF8` default), which the proven
  Node client never sends; now no-BOM, byte-for-byte matching. Added a ping-test line to the live
  checklist since the fallback caller itself has never been exercised live.
- 2026-07-23 — Parallel consistency pass (independent of Parts 1–6 above, same day): found and fixed
  the same ps1 recursion bug, the same set_parameter_value validation gap, the same AGENT-SPEC.md
  staleness, and most of the same stale headers — reconciled to Parts 1–6's versions on merge (the
  same fix or a better one in each case). Net-new: flagged Create Phase / Set Active Design Option as
  CONFIRMED IMPOSSIBLE in `universal-actions-reference.md` (matches the Phase/Design Option findings
  above, wasn't reflected in that catalog yet); added the 37 real filter/graphic-override fragments
  missing from that same catalog; added try/catch/RollBack to the 8+3+2+1 bare transactions in
  `create-parametric-box-family-with-duct-connector.cs`/`place-fcu.cs`/`place-terminals-checkerboard.cs`/
  `set-space-airflow.cs` (none of which this pass touched), and the missing null checks in
  `draw-main-duct-with-cap.cs`/`split-duct-near-equipment.cs`. Full text in git history (PR #3).
