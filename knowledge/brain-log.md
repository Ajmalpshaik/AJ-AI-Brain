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
1. Run `tools/invoke-bridge.ps1 -Ping` once — a 2026-07-23 session found it sent a UTF-8 BOM the Node
   client never sends (fixed to no-BOM that day, matching the proven client byte-for-byte, but the
   fallback caller itself has never been ping-tested live). (The other half of this item —
   `verify-consistency.ps1` on real PowerShell — was proven 2026-07-26: it ran live, caught real
   drift, and passed after the fix; no Revit needed for that part after all.)
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
- 2026-07-26 — Union-joint consistency audit (user-requested after the live 4-FCU build proved the
  correct workflow): the day's discovery — bare `ConnectTo` joints after `BreakCurve` can be silently
  re-merged by Revit, losing the split; real Union fittings (`NewUnionFitting`) are what preserve it —
  clashed with 3 scripts and 1 knowledge line, all fixed: `slice-trunk-for-sizing.cs`,
  `split-duct-near-equipment.cs`, `action-split-elements.cs` (all now NewUnionFitting + header notes,
  README rows updated, union fix live-proven only via the inline build so scripts marked "union fix not
  yet live-run"), hvac-ducts.md § slicing reconnect line, plus the new gotcha added to AGENT-SPEC.md.
  Cap/equipment/terminal `ConnectTo` uses checked and left alone — correct there (fitting-to-duct, no
  merge risk). Skills checked: no clashes.
- 2026-07-26 — New recipe `connect-equipment-to-air-terminals.cs` (the user's connection method
  end-to-end, live-proven same day) + new hvac-ducts.md section (connection method, connector-overload
  size/system inheritance, end-cap-by-script technique) + first live-model log.md entries. New standing
  rule from the user recorded in START-HERE.md: the Brain is the only portable memory — everything the
  assistant saves to its local machine memory must ALSO be written into Brain files, because moving to
  another system means copying the Brain folder only.
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
- 2026-07-26 — Audit pass: `verify-consistency.ps1` first real PowerShell run caught genuine drift —
  the 2 MEP standards recipes (`create-mep-line-standards.cs`, `create-mep-text-standards.cs`) were on
  disk but missing from `scripts/README.md`; rows added, checker green.
- 2026-07-26 — Brain became an installable Claude Code plugin: `.claude-plugin/plugin.json` (8 skills +
  bundled MCP relay via `${CLAUDE_PLUGIN_ROOT}`) + `marketplace.json` so the repo itself is the
  marketplace — SETUP.md step 1 Option A has the two install commands. Also new: root `CLAUDE.md`
  (auto-imports START-HERE.md when a session opens in this folder) and a PostToolUse hook
  (`.claude/settings.json` → `tools/verify-consistency-hook.ps1`) that re-runs the consistency checker
  after every edit in this repo — drift now surfaces same-turn (exit-2/stderr path live-tested with a
  planted unlisted script, then removed).
- 2026-07-26 — Harvested 6 principles from a third-party Revit-MCP doctrine the user found (an external tool
  connector — different product, its tool names don't apply here) into `live-model/core.md` Bridge
  basics: empty-result-is-valid, never-invent-ElementIds, resolve view-relative direction words before
  moving, one-composed-script-over-many-calls, verify-small, workshared-sync reminder. Its
  "reuse cached state, don't re-query" rule was rejected — conflicts with our proven fresh-reads rule.
- 2026-07-26 — Tool-gap backlog build: user compared this setup against another Revit MCP server's tool
  list; 9 gaps found became 14 new fragments — room elevations, floors, sheet sets, compare-elements,
  parameter CSV round-trip (Excel via agent-side xlsx), model-health-audit recipe, compact save,
  sync-with-central, link unload/remove, and DWG/IFC/NWC/image exports. All marked NOT live-verified yet.
- 2026-07-26 — Round 2 (self-proposed gaps, user approved all): 17 more fragments — load-family,
  create duct/pipe/cable-tray/conduit/wall, create-ceiling recorded as IMPOSSIBLE on 2020 (API is 2022+),
  revision clouds, HVAC zones, insulation add/remove, sleeve-at-wall-penetrations recipe (dry-run first),
  spot elevations, print settings, workset rename (delete = impossible on 2020), element ownership
  report, shared-coordinates context, TextNote find/replace. All NOT live-verified yet.
- 2026-07-26 — User's third comparison list was Dynamo's 100 standard nodes — all 100 already covered
  natively (fragments + LINQ + raw API). No fragments built; saved the translation as
  `knowledge/dynamo-vocabulary-map.md` + INDEX row so Dynamo-vocabulary requests route instantly.
- 2026-07-26 — Round 3, Dynamo PACKAGE harvest (Clockwork/Rhythm/MEPover/Bimorph/Genius Loci/archi-lab):
  7 new fragments — connector report (packages the user's connection-method steps 1-3, cross-linked from
  hvac-ducts.md), sub-components filter, compound-structure report, room boundaries, CAD-layer curve
  extraction (dry-run first), duplicate-sheet-with-views, copy-from-link. Spring Nodes/Data-Shapes/Orchid
  recorded as deliberate non-builds in the map's package table. All NOT live-verified yet.
- 2026-07-26 — Round 4, gap search after the package harvest: 9 fragments — accessory-into-run (VCD/valve,
  breaks the run and reconnects), purge unused FAMILIES/types (the file-size half `action-purge-unused.cs`
  omits), view range, MEP system TYPE creation, aligned dimensions on family instances, callout views,
  legend duplication, sheet list, key schedule. Auto duct SIZING was explicitly NOT built — hvac-ducts.md
  records it as the user's own step. Legends and workset delete recorded as partial/absent APIs.
  All NOT live-verified yet. Library now 256 fragments.
- 2026-07-26 — First live-verification pass on the new fragments (bridge up, Revit 2020 / Project1):
  11 confirmed working — shared-coordinates, model-health-audit, purge-unused-families (dry-run),
  compound-structure, view-range (report), room-boundaries (graceful), compare-elements,
  print-settings (report), sheet-list (created + undone), native-undo, legend (graceful). Two real bugs
  found and fixed in place: view-range printed raw sentinel Ids ("Id -2") instead of "(level above)", and
  a pre-existing wrong relative path in filter-by-room.cs. MEP/link/CAD/worksharing fragments remain
  fixture-blocked — this model has walls, rooms and levels only.
- 2026-07-26 — **Bridge gotcha found live: the destructive-op guard reads the whole script as TEXT and is
  CUMULATIVE.** A 100% read-only audit was refused because two OUTPUT strings together mentioned purging
  and deleting. Fix is to soften read-only scripts' wording, never to pass allowDestructive to get a read
  through. Recorded in `live-model/core.md`.
- 2026-07-26 — Architecture pass (assessed first: restructure NOT needed, targeted fixes were):
  `filters/` split from 49 flat files into 6 job-grouped subfolders (by-identity / by-property /
  by-location / by-relationship / by-view-and-sheet / by-status), matching the 2026-07-22 actions/ split
  precedent. Renamed the two dangerous singular-plural pairs to say what they RETURN
  (`filter-by-elements-on-level.cs`, `filter-by-elements-in-view.cs`), gave the two noun-first reports
  their `report` verb, moved the stray parameters-CSV export in with the other exports, and wrote the
  naming rules into `scripts/README.md` so they stop being folklore.
- 2026-07-26 — **Self-inflicted incident worth remembering: PowerShell `Get-Content`/`Set-Content` for
  bulk edits double-encoded UTF-8 in 41 files** (em dashes and ✓ became mojibake) because 5.1's
  Get-Content reads UTF-8-without-BOM as ANSI. Caught by noticing the README diff was 285 lines when the
  real edit was ~90. Repaired with a targeted per-character map. **Rule: for bulk text edits across this
  repo use `[System.IO.File]::ReadAllText/WriteAllText` with an explicit UTF8Encoding($false), never
  Get-Content/Set-Content.**
- 2026-07-26 — **Generalised ray-tracing (the user's idea, extending his own 2026-07-14 one).** His point:
  don't build "ray to ceiling", build "ray to whatever I name today" — slab, wall, beam, or simply the
  nearest thing — and fire in every direction, not just up. Two fragments:
  `actions/reporting/action-report-ray-hits.cs` (LOOK — up/down/sideways/plan-diagonals or all 26 cube
  directions, target category a per-request input, read-only) and
  `actions/move-copy-rotate/action-move-to-ray-hit.cs` (MOVE — one direction, signed offset, dry-run by
  default). `recipes/ray-trace-to-ceiling.cs` stays as the ceiling-only shortcut, now cross-referenced.
  Report fragment ✓ live-verified same day. **Bug caught by that test: `ReferenceIntersector.FindNearest`
  returns the SOURCE element's own face when the ray starts inside it, so dropping the self-hit left
  "nothing found" — 1 hit reported where there were 11.** Always Find-all → drop-self → take-nearest;
  FindNearest is only safe when a category filter makes a self-hit impossible (which is why the old
  ceiling recipe never showed the bug). Geometry cross-checked: diagonals came back at axis x root-2.
- 2026-07-26 — `action-move-to-ray-hit.cs` ✓ live-verified on its first real job: the user added 17 air
  terminals and 3 ceilings, and asked for the terminals to be moved up to the ceiling. One pass moved all
  17, 0 misses. **The ceilings were at three different heights (2100 / 2400 / 3000 mm) and each terminal
  found the ceiling above ITSELF** (6 / 6 / 5) — the case that makes ray-casting worth having, since a
  fixed-Z set would have put 11 of them in the wrong slab. Dry run first, matched exactly; read-back
  confirmed each Z equals its ceiling underside. Model state changes between messages — the fresh-read
  rule earned its keep here too: an earlier query in the SAME session had found 0 terminals and 0 ceilings.
- 2026-07-26 — **Better ray algorithm, after the user asked whether ours was simple and whether volume
  would break it.** Measured first: ~0.07 ms per category-filtered ray, ~0.11 ms unfiltered, on a
  3239-instance model. So 1000 elements x 5 footprint rays is under a second — **ray count is NOT the
  bottleneck; OUTPUT VOLUME is** (1000 elements x 26 directions = 26k lines). New
  `actions/qa-checks/action-check-surface-fit.cs` therefore reports BY EXCEPTION: always a summary, detail
  only for failures, capped and never silently truncated. It supersamples the element's footprint
  (centre + corners, or 3x3) and flags STRADDLING / OVERHANGING / UNEVEN / SLOPED. ✓ Both paths verified
  live — and the detection path was proven by deliberately parking a terminal on a real ceiling boundary,
  where the footprint said OVERHANGING and a single centre ray said "ceiling 920058", confidently wrong.
  **Lesson from a false start in that test: never infer which surface is above a point from BOUNDING
  BOXES** — two of these ceilings had overlapping boxes but non-overlapping real shapes, which produced a
  misleading "OK" until the rays were read directly. Probe, don't infer from extents.
- 2026-07-26 — **Proximity + route planning (user's idea): "nearest element" and "least wire".** Two
  fragments, both ✓ live-verified same day. `action-report-nearest-elements.cs` — nearest target(s) per
  source across any categories or a fixed Id list, with THREE metrics that deliberately disagree: `gap`
  (real clearance between bounding boxes), `centre` (straight line), `manhattan` (orthogonal,
  cable-realistic). Verified: 17 terminals -> 6 FCUs, zoning 4/4/3/2/2/2.
  `action-plan-shortest-route.cs` — `tree` mode is **Prim's minimum spanning tree**, EXACT, the real shape
  of a branching homerun; `chain` mode is nearest-neighbour + 2-opt, a HEURISTIC (travelling salesman has
  no fast exact answer) and the output says which guarantee applies. Verified on 17 terminals: manhattan
  tree 111.7 m vs best chain 132.9 m (tree 16% shorter); 2-opt earned its keep, taking 3.9 m off the
  straight-line chain. **Manhattan ran ~30% longer than straight line — that is the orthogonal penalty,
  and why manhattan is the honest default for cable.** Both headers state plainly that these are
  point-to-point estimates, NOT routed cable schedules. Obstacle-aware A* routing deliberately NOT built —
  named in the header as out of scope until a concrete case exists.
- 2026-07-26 — **The user spotted the real flaw in nearest-neighbour routing: it does not know walls
  exist.** "If the nearest element is in the next room it will go there and come back." Measured on the
  chain actually drawn: it changed zone **6 times where 2 would do** — it genuinely leaves a zone and
  returns, which nobody would pull cable like. Answer was grouping, not a separate tool:
  `action-plan-shortest-route.cs` gained `groupBy` (none/room/space/level/parameter), `connectGroups`
  (second-level feeder tree between groups) and a view-aware `drawRoute`.
  **The counter-intuitive result, now written into the header so nobody "fixes" it: grouping reports a
  LONGER total — 134.3 m grouped vs 106.8 m ungrouped, 26% up.** Grouping is a constraint and constraints
  cost length; the ungrouped figure is only shorter because it cheats by running back and forth through
  walls. The ungrouped number is unbuildable, the grouped one is real.
  Also fixed the same day: `drawRoute` drew MODEL lines only, which are invisible in a plan view when the
  geometry sits above the cut plane (cost a real head-scratch when the user asked to see the chain in
  "1 - Mech" at a 1200 mm cut, with terminals at 2100-3000). It now takes a target view and draws DETAIL
  lines for plan/section/elevation, model lines otherwise.
- 2026-07-26 — **Bridge gotcha: `PostCommand(Undo)` does NOT fire while a run_csharp script holds Revit's
  UI thread.** Posted an undo to remove 16 stray model lines, and the very next query showed the count
  unchanged (32 before, 32 after) — caught only because the script's own hardcoded "so they're gone"
  message contradicted the number printed beside it. For cleanup INSIDE a script, delete explicitly by Id;
  native Undo is for the user's own "that was a mistake", between calls, not mid-script.
- 2026-07-26 — **The user designed a better routing algorithm than the one I had built, and the numbers
  prove it.** His method (now `mode="continuous"`): finish a room completely, then from its LAST fitting
  jump to the genuinely nearest fitting in any room not yet done — that jump decides both which room comes
  next and where you enter it. His sharp insight was that **where a room ENDS must be optimised, not
  accepted**, because the exit determines the jump; so every candidate exit is tried and scored on
  (path through the room + jump out of it). Measured on Project1's 17 terminals in 3 placed rooms:
  **109.0 m (95.1 inside + 13.9 across, 2 jumps) vs 134.3 m for my independent-groups-plus-centroid-feeders
  version — 25 m better, and within ~2% of the 106.8 m unconstrained minimum that cheats through walls.**
  Prototyped live first, then transcribed into the fragment and re-run to confirm the transcription
  matched (identical 109.0 m / 16 segments).
  Also verified that day, once the user's 3 rooms were placed (239/251/271 m2): room grouping is correct —
  6/6/5 terminals, and 14 of 16 drawn lines stayed inside their room, the only 2 crossings being the
  deliberate feeders. **Lesson on presentation, not maths: feeders drawn mixed in with room runs READ as
  errors even when correct** — the user flagged the picture as wrong when it was right. Draw them
  separately (own colour/weight), or leave them out.
