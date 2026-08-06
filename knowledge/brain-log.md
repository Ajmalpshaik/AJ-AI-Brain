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

Split three ways 2026-08-04, because the old flat list mixed "do it next session", "can never be done"
and "already done" together and so always read as unfinished.

**Doable in any session (no Revit):** none right now.

**Needs Ajmal's machine, but NOT a bridge or an open model** — only Revit *installed*, for its DLLs:
1. **DONE 2026-08-04 — all 267 fragments now compile against Revit 2020** (VS 2022 Roslyn `csc.exe`).
   Nothing outstanding here. Re-run `tools\verify-fragments-compile.ps1` after any fragment edit; it
   takes about a minute. **Heads-up:** it writes ~267 unsigned `out.dll` files into `%TEMP%` in quick
   succession, and Sophos flags that heuristically as `ML/PE-A`. It is the compiler's output, not
   malware, and the folder is deleted at the end — but on a company-managed endpoint the detection is
   reported to IT, so tell them before running it rather than after.

**Needs a live bridge — ANY model will do:**
1. Run `tools/invoke-bridge.ps1 -Ping` once — a 2026-07-23 session found it sent a UTF-8 BOM the Node
   client never sends (fixed to no-BOM that day, matching the proven client byte-for-byte, but the
   fallback caller itself has never been ping-tested live). (The other half of this item —
   `verify-consistency.ps1` on real PowerShell — was proven 2026-07-26: it ran live, caught real
   drift, and passed after the fix; no Revit needed for that part after all.) NOTE: run this LAST in a
   session — the bridge allows one active connection at a time, so the helper may disturb the MCP one.
2. `delete_elements` — the only one of the 17 native tools still unverified live (2026-08-04 verified the
   other 16; the user stopped before the confirm-and-remove step). Needs any throwaway element.

**Needs a live bridge AND a model that actually contains the fixture** — this is the real blocker, not
effort. An empty scratch model cannot move any of these:
3. The fixture-blocked positive paths (worksharing, Assembly, Design Option, insulation, electrical,
   links, Ceilings, a flip-capable family, a sleeve family, a CAD import, the PDF print go-ahead) — each
   listed with its exact blocker in `scripts/README.md`'s per-fragment notes.
4. The 2026-07-23 transaction/null-check safety fixes to `create-parametric-box-family-with-duct-
   connector.cs`, `place-fcu.cs`, `place-terminals-checkerboard.cs`, `set-space-airflow.cs`,
   `draw-main-duct-with-cap.cs`, `split-duct-near-equipment.cs` — code-reviewed only, none live-executed.
   `place-fcu.cs` additionally needs a Room + FCU + terminal layout to exist.
5. `action-reload-links.cs` positive path — the compile bug is fixed and the graceful path ran live
   2026-08-04, but which `LinkLoadResultType` value `Reload()` returns for an already-current link is
   still unverified. Needs a model with at least one RVT link.

**CONFIRMED IMPOSSIBLE on Revit 2020 — closed, do not re-attempt.** These are answered, not outstanding:
Set Active Design Option (no setter exists anywhere in the assembly), Create Phase (`Document.Phases` is
read-only), workset delete (API is 2022+), Scope Box creation, and view-title extension-line length
(API landed in 2022 — confirmed from Rhythm-for-Dynamo's own source). See
[`universal-actions-reference.md`](universal-actions-reference.md) and `live-model/core.md`.

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
- 2026-07-26 — Harvested 6 principles from a third-party Revit-MCP doctrine the user found (AUTOM8LABS
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
- 2026-07-26 — `action-plan-shortest-route.cs` gained `groupDrawnRoute` + `drawnGroupName` after the user
  asked for the route "in single stretch". **Revit has NO polyline element** — every curve element is one
  straight line or one arc — so a continuous route is always stored as joined segments. A GROUP is what
  makes it behave as a single thing: one click selects the whole run, and it moves/deletes as a unit.
  Verified live on the 17-terminal run (16 segments -> one detail group named `MEP_Terminal_Run`, matching
  the office MEP_ prefix).
- 2026-07-26 — **Coverage analysis** (user: "if we draw a circle, that is coverage") →
  `actions/reporting/action-report-coverage.cs`. **Key discovery: the standard M_Supply Diffuser has NO
  coverage or throw parameter at all** — it carries Flow, Pressure Drop, Diffuser/Duct sizes, Max/Min
  Flow, nothing about reach. So coverage must be DERIVED, and the fragment offers three sources that
  **disagree by ~6x on the same 17 terminals**: `spacing` (half the gap to the nearest neighbour — pure
  geometry, 344 m2 total) vs `flow` (Flow / design L/s-per-m2 — 1,998 m2 at an invented 2 L/s/m2) vs
  `fixed`. The flow path REFUSES to run without the user's design rate rather than defaulting, because
  2 vs 10 L/s/m2 changes the answer fivefold. ✓ verified live: radii 1883/2492/3520 mm min/avg/max,
  circles drawn and grouped as `MEP_Terminal_Coverage`.
  Gotcha recorded: **~45% floor coverage is geometrically NORMAL** (circles cannot tile a plane — even
  perfect packing reaches only ~78%), so it must not be read as "half the room is unserved".
- 2026-07-26 — **CORRECTION, and the lesson matters more than the fact.** The entry above originally also
  claimed "a full-circle Arc is rejected by Revit, so every circle is two half arcs". **That is FALSE.**
  `Arc.Create(centre, r, 0, 2*PI, X, Y)` returns a closed unbound arc that `NewDetailCurve` accepts as ONE
  element (`Ellipse.CreateCurve` with equal radii works too) — probe-tested and rolled back. I had written
  it from assumption without ever testing it, and it went into a fragment header, this log, and 932 real
  arcs in the user's model, until he noticed the halves selected separately. Two rules from this:
  **(1) never write an untested claim as a GOTCHA** — an unverified gotcha is worse than none, because
  every later session obeys it; if it is a guess, mark it FLAGGED like the other honest unknowns.
  **(2) clean-up must be verified too** — the replacement pass deleted one group but orphaned the other's
  932 arcs; counting curves afterwards (1431 where 499 was expected) is what caught it.
- 2026-07-26 — **Session sweep at the user's request ("did you forget to save anything") found three real
  gaps** — things built live and demonstrated, then left in chat:
  (1) the room COVERAGE LAYOUT generator, run three times live and never saved → now
  `recipes/generate-room-coverage-layout.cs` (sample the real room shape → hexagonal or square covering
  lattice → greedy set-cover reduction → VERIFY by counting uncovered points → draw). Verified: Room 4
  287.7 m², r=3000 → 20 heads hexagonal / 21 square, zero gaps; r=500 → 466, zero gaps.
  (2) placing UNPLACED rooms into enclosed regions, done live to unblock room grouping → now
  `creators/create-rooms-in-enclosed-regions.cs`, which reuses existing Area-0 rooms rather than orphaning
  them.
  (3) `sampleMode="fan"` on `action-report-ray-hits.cs` — demonstrated, explicitly offered, never built.
  Now implemented and verified: 36 rays from 24 distinct start points found 5 neighbours where the single
  centre ray found 4. **The header had claimed "4 vs 7" from the earlier ad-hoc test; that number was not
  reproducible and has been corrected to the measured 4 vs 5** — same discipline as the circle correction.
  Lesson: *demonstrating* something in chat is not *saving* it, and an offer made and not taken is a gap.
- 2026-07-26 — **MAJOR bridge gotcha, found while verifying the fan: `ReferenceIntersector` ONLY FINDS
  WHAT ITS 3D VIEW SHOWS.** Identical code, same element, same direction: view `{3D}` (Walls category
  hidden) → **0** hits; view `3D Plumbing` (Walls visible) → **4**. Hidden categories, section boxes, view
  filters and closed worksets all silently remove geometry from ray results — no error, just a confident
  wrong "nothing there". This affects every ray fragment, and is genuinely dangerous in
  `action-move-to-ray-hit.cs`, which would snap elements onto whatever is visible behind the real surface.
  Recorded in `live-model/core.md`; the report fragments now WARN and the move fragment REFUSES when the
  target category is hidden in the chosen view.
- 2026-07-27 — First real *use* of `recipes/generate-room-coverage-layout.cs` (Room 4, r=3000, square, drawn
  into `1 - Mech`): 21 circles, 3,243/3,243 points covered, 0 gaps, grouped
  `MEP_Room4_Coverage_R3000_Square`. Two improvements folded back in. (1) The recipe reported *how many*
  centres but never *where* they are, so the output could not be used to place the devices — it now prints
  `CENTRES (mm, project X,Y)`. (2) Recorded the buildability payoff as a measured number: square spacing is
  4,243 mm vs hexagonal 5,196 mm, so square costs one extra device but PASSES a 4,600 mm max-spacing cap
  that hexagonal fails. Lesson: a saved recipe can be correct and still not be *actionable* — the first
  live use is what exposes that.
- 2026-07-27 — Drew the hexagonal coverage alongside the square one in Room 4 (both kept, separate groups:
  21 red square / 20 green hexagonal, both 0 gaps). The read-back nearly produced a false alarm: a
  view-scoped `FilteredElementCollector(Document, viewId)` run right after the create+group transaction
  returned 20 curve elements and 1 group, so the square set looked deleted — the identical query moments
  later returned 74 and 3, with nothing changed in between. Recorded in `live-model/core.md`: never
  conclude an element is gone from a view-scoped read; confirm document-wide first. The pair also gave the
  spacing trade-off as measured fact — square 4,243 mm PASSES a 4,600 mm cap, hexagonal 5,196 mm FAILS,
  for one device less.
- 2026-07-27 — **The user looked at the two drawn layouts and asked "did you find any mistake?" — there were
  three, and the recipe was wrong, not just the run.** (1) `cover` mode optimises coverage OF THE FLOOR, and a
  circle centred beyond the wall still covers floor, so it returned centres OUTSIDE the room: 6 of 21 (square,
  a whole row at Y=17,733 past the wall at Y=17,663) and 8 of 20 (hexagonal). Both reported "FULL COVERAGE, no
  gaps" and both were unbuildable — coverage was true and useless. (2) Greedy set-cover never re-checks its
  picks, so the hexagonal 20 contained a fully redundant circle; the real number was 19. I over-reported.
  (3) I checked device-to-device spacing against 4,600 mm but never distance-to-wall — half a code check.
  Fix is PHASING, not more circles: a wall-inset grid on the same room needs 20 devices, 0 outside, 0 gaps,
  spacing 4,140 x 3,475 mm, wall 2,070 mm — fewer devices than the 21 `cover` found, and passes both caps.
  Recipe restructured into `inset` (default, buildable) vs `cover` (theoretical), with a prune pass, an
  inside-room constraint, a BUILDABLE line in every report, and a wall-distance check. Lesson worth keeping:
  **a verified metric can still be the wrong metric — "no gaps" said nothing about whether a device could
  physically be mounted, and the drawings showed it before any check did.**
- 2026-07-27 — Buildable hexagonal added to `recipes/generate-room-coverage-layout.cs` (`inset` + `hexagonal`),
  drawn as `MEP_Room4_Coverage_R3000_Staggered`: 19 devices, 0 gaps, 19/19 inside. Two findings worth more
  than the layout. (1) **The shifted-row construction swings the answer 40%** — giving shifted rows nx-1
  devices (the obvious "it's offset, drop one" instinct) needed 32 devices; giving them nx+1 with the ends
  pulled inside the wall needed 19. Same room, radius and idea of "staggered". So nx/ny are now found by a
  verified SEARCH, never a spacing formula. (2) **Hexagonal's efficiency edge is a plane result and four
  walls mostly destroy it**: square inset 20 with both code caps PASSING, vs staggered 19 that FAILS both by
  ~20-30 mm, vs compliant staggered 22. One device out of twenty, bought by breaking spacing — so the
  textbook "hexagonal is ~30% fewer" must never be quoted for a room. Both recorded in the recipe header.
- 2026-07-27 — Session sweep (user: "did you forget anything to update?"). Five gaps found, all outside the
  files the work had naturally touched: (1) `START-HERE.md` had NO route row for device coverage layouts —
  the request type the user had just made twice was unroutable from the entry point; added one pointing at
  the recipe. (2) `glossary.md` had no "coverage" entry, though the Brain holds two different coverage jobs
  (report what existing elements serve vs generate where devices go) that a request can't distinguish —
  added, plus hexagonal/staggered as the same thing. (3) `AGENT-SPEC.md` §5.2 recipe table didn't list the
  coverage recipe. (4) §6.2 lacked the view-scoped-collector staleness gotcha. (5) §6.7 (new) + §8 now carry
  the layout/optimisation lessons — verified-but-wrong metric, lattice phasing, prune greedy, plane-optimum
  doesn't survive walls, try more than one construction. Lesson about the sweep itself: the files a task
  edits naturally are not the files that route FUTURE requests to it — an entry-point route row and a
  glossary disambiguation are the two most commonly missed, because the work never has to touch them.
- 2026-07-27 — **New skill `ajtools-fire-sprinkler-layout` + new knowledge file `nfpa13-sprinkler-spacing.md`**
  (user: fire fighting follows its own rules, study NFPA first). Researched NFPA 13 spacing from secondary
  sources (the standard itself is copyrighted and edition-specific, so the file is a cited paraphrase that
  must be confirmed against the adopted edition — sources disagreed: light hazard reported as 225 / 200 /
  "130-200" ft²). Recorded: max area per head by hazard class, max head-to-head, max/min distance to wall,
  min head spacing, small-room rule, deflector position, obstruction rules incl. the three-times rule,
  sidewall limits, extended coverage. AHJ note: the user's projects answer to **QCDD**, which enforces NFPA
  plus its own requirements — an NFPA-only check must never be called "compliant".
  **Two findings that invalidate part of the earlier coverage work.** (1) 15 ft = **4,572 mm, not 4,600** —
  the cap used all session was 28 mm LENIENT. (2) There are **four** code limits, not two: the missing one
  is **max floor area per head**, which a covering algorithm never looks at and which sets a hard MINIMUM
  head count. Room 4's 20-head zero-gap layout passes spacing, min-spacing and wall checks against real NFPA
  numbers but FAILS the area rule on ordinary hazard — it needs at least 24. So it is a light-hazard layout
  only. Recipe now takes `minSpacingMm` and `maxAreaPerDeviceM2` and reports all four; verified live.
  Lesson: **the drawn geometry was never the deliverable — the governing rule set was, and nobody had asked
  which one applied.** Three sessions of "verified, zero gaps" said nothing about the code that governs it.
- 2026-07-27 — First layout produced through `ajtools-fire-sprinkler-layout` instead of the generic coverage
  recipe: Room 4, Ordinary Hazard I/II, 6 x 4 = 24 heads, all seven code checks PASS, drawn as
  `MEP_Room4_Sprinkler_OH_24Heads`. The method inverted — the grid is derived FROM the limits (smallest
  nx x ny satisfying max/min spacing, max/min wall distance and max area per head simultaneously) and the
  drawn radius falls out of the grid (half the cell diagonal, 2,448 mm), instead of a radius being chosen
  first and the grid falling out of it. Also recorded: **area per head is `A_s = S x L` from the grid
  dimensions, NOT room area / head count** — the two agree only when the grid tiles the room exactly with
  half-spacing insets, and S x L is what the code means. Worked example appended to
  `nfpa13-sprinkler-spacing.md`.
- 2026-07-27 — Sweep before committing caught the worst kind of drift: **numbers that were correct when
  written and became wrong later in the same session.** Three claims in the coverage recipe header still
  judged layouts against the assumed 4,600 mm cap after NFPA had established 4,572 — so the header said
  "over by 33 mm" where the truth is 61 mm, and "FAILS 4,600" where the real cap is 4,572. Corrected. Also
  added to `AGENT-SPEC.md`: §1.4 now states the engineering boundary (no hydraulics, no hazard-class calls,
  no compliance declarations — AHJ/QCDD and a licensed engineer own those), and §6.7 carries the governing
  rule-set lesson. Lesson: **when a session later learns the real value of a number it had assumed, grep the
  whole repo for the old one.** A superseded figure written into a header reads as authoritative next
  session, and the file it sits in was already marked verified.
- 2026-07-27 — `creators/create-sheet.cs` gained a SEQUENCE mode (prefix + running number + zero padding,
  sheet names counting 01, 02, 03...). Hand-typing 26 tuples into the explicit-list mode is where
  transcription errors come from. Explicit-list mode unchanged. Live-verified: 26 sheets in one transaction.
  **Lesson, caught by the user the same session:** the mode was first committed with that job's real prefix
  and start number as the defaults — the exact thing this fragment's own INPUTS header and START-HERE rule 3
  forbid. A real value left in a fragment reads as a project standard next session. Replaced with deliberately
  fake placeholders (`XXX-000-`, `SHEET `) so an unfilled input shows up in the created sheets instead of
  looking plausible. Applies to every fragment, not just this one.
- 2026-07-27 — Post-commit sweep found the recipe CONTRADICTING its own knowledge file: the area-per-device
  check computed `room area / device count`, while `nfpa13-sprinkler-spacing.md` records that NFPA means
  `A_s = S x L` from the grid dimensions. Fixed to report A_s as the governing value, with the average shown
  beside it and an explicit NOTE when they diverge. **The divergence is real, not theoretical** — proven the
  same hour on the staggered layout: A_s 15.99 m² vs average 15.14 m², 0.84 m² apart, because shifted rows
  with clamped ends do not tile the room exactly. The old method understated the governing figure by 5%,
  which on a tighter room is the difference between PASS and FAIL. Also closed a verification gap: the recipe
  had only ever been exercised by hand-written equivalents of its logic, never as the file itself — now run
  end-to-end from disk via `tools/invoke-bridge.ps1 -CodeFile` for both inset modes. Lesson: **writing a rule
  into a knowledge file does not make the script obey it** — when a method is corrected in prose, grep the
  scripts that implement it, and run the file, not a paraphrase of it.
- 2026-07-27 — Same trap found in `recipes/generate-room-coverage-layout.cs` immediately after the
  create-sheet one: `radiusMm = 3000` was a PAST JOB's figure sitting in the INPUTS block as a default, in a
  file whose own header says "edit every time". A plausible number is worse than a blank one — it runs, it
  looks deliberate, and nobody re-asks. Now `radiusMm = 0` with a guard that refuses to run and says to state
  the radius for this job (and, for sprinklers, not to pick one at all — derive the grid from the NFPA
  spacing limits and let the drawn radius fall out as half the cell diagonal). Both paths verified live from
  disk: 0 refuses, 3000 runs. A grep of all 264 fragments found no other real project value left in an INPUTS
  block — only a commented `e.g.` example, which is fine. **General rule: a fragment's default must be
  either neutral (0, null, "") or a guard, never a working value from the job that created it.**
- 2026-08-01 — **Gotcha found live (project 4355): view-filter names can lie about their real category.**
  Two filters named `..._Cable Trays_Service Type_Refrigerant Pipes Tray` sound mechanical but
  `ParameterFilterElement.GetCategories()` showed they actually target `Cable Trays`/`Cable Tray Fittings`
  — Revit's Electrical discipline — because the project routes refrigerant-pipe support trays on a Cable
  Tray category element. Classifying a template's filters as mechanical/electrical now means resolving
  real categories, not reading names. Documented in `live-model/mep-color-standard.md`. Also duplicated a
  view TEMPLATE for the first time: `View.Duplicate()` throws "View cannot be duplicated" on templates
  (`CanViewBeDuplicated` returns false for all options) — `ElementTransformUtils.CopyElements(doc, ids,
  doc, Transform.Identity, new CopyPasteOptions())` works instead and Revit auto-suffixes the name, which
  then gets set to the real target name in a second step. Not yet promoted to a reusable fragment — only
  done inline twice so far.
- 2026-08-01 — **Correction from the user (project 4355): "change filter color" defaulted to line+fill,
  should be line-only.** Applied black to 4 pipe filters' line AND fill together (matching
  `action-apply-view-filter.cs`'s `includeFill=true`, meant for the full MEP Color Data Standard sync);
  user undid it in Revit and clarified fill should stay untouched for a plain color-change request. Fixed
  by reusing the existing `OverrideGraphicSettings` and setting only `SetProjectionLineColor`/
  `SetCutLineColor`. Documented in `live-model/mep-color-standard.md`.
- 2026-08-01 — Built a 3-sheet site shaft-coordination set for project 4355 (Duct/Piping/Electrical Cable
  Tray) — 6 duplicated view templates, one hero-system-full-color + rest-gray(80,80,80) scheme per sheet,
  the mislabeled Cable Tray filters grouped with Piping (their real function) not Electrical (their Revit
  category). First real multi-system use of the template-duplicate technique from the same day. Took two
  passes to get the 3x3 color matrix confirmed exactly against the user's wording before trusting it was
  right. Documented in `live-model/mep-color-standard.md`.
- 2026-08-01 — Fill-color cleanup on `TRG_Accessories_Duct` silently reverted between two script calls
  despite a same-script verification passing right after the change — root cause not confirmed (the next
  script's logic looked correct on inspection, never referenced this filter by name). Caught by an
  independent later re-check, re-cleared, re-verified in a third separate call, held. New standing rule
  added to `core.md`: don't trust same-call verification alone for multi-element graphic-override
  mutations — check again in a separate later call before reporting success.
- 2026-08-01 — View title extension-line length: confirmed no API lever exists on Revit 2020 (this
  project's version) — traced to Rhythm-for-Dynamo's own source throwing "only works in Revit 2022."
  Also found and fixed a blast-radius miss along the way: the viewport type holding the title style was
  shared by 77 viewports document-wide, not just the 3 new site sheets — duplicated the type before
  touching it, per the user's explicit choice. Documented both in `live-model/core.md`.
- 2026-08-01 — View title POSITION also unsettable on Revit 2020 (reflection on `Viewport` shows only
  read-only `GetLabelOutline()`, no `LabelOffset`). Turned the dead end into a deliverable: computed each
  title's exact centering offset from `GetBoxOutline()` vs `GetLabelOutline()` so the user's manual drag
  is a known mm figure per view (23 views across the 3 site sheets). The same read exposed inconsistent
  title HEIGHTS between sheets, invisible when checking one sheet at a time. Technique in
  `live-model/core.md`. Also caught during the same pass: `Piping only section 05` had been deleted from
  the model entirely (lost in one of the user's undos) — found only because a fresh full-sheet read was
  done rather than trusting the earlier "3 plans + 5 sections" verification.
- 2026-08-01 — **New technique: measure-by-rollback.** User pushed back on the "extension line can't be
  adjusted" answer, so re-verified exhaustively (full `Viewport` reflection + every viewport-related
  BuiltInParameter enumerated + Rhythm source) — no API lever on 2020, confirmed. But the re-check turned
  up something better: toggling `SHOW_EXTENSION_LINE` off inside a Transaction, `Regenerate()`, measuring,
  then `RollBack()` gives the text-only label width with zero persistent change — and the delta vs the
  normal measurement is the line's exact overhang past the text, i.e. the precise mm to drag each grip.
  Found 10 of 23 titles had over-long lines (58–169mm overhang), all on the duplicated Piping/Cable Tray
  views; the original Ducting ones were already correct. Also corrected the earlier centering figures,
  which had wrongly centered text+line rather than the visible text. Technique in `live-model/core.md` —
  generalises to any "what would this look like if…" question, and never enters the user's undo stack.
- 2026-08-01 — Root cause of the over-long title lines, found when the user pushed back a third time:
  **a script-placed viewport defaults its title line to `boxWidth + 6.4mm`** (exact, all 5 measured),
  vs a hand-set 92.6mm constant on the originals — so bulk `Viewport.Create` on Revit 2020 always leaves
  lines needing a manual drag, and re-placing doesn't help. Also learned that a viewport's sheet box width
  comes from ANNOTATION extents not the crop (two views, identical 158.4mm crop, 202.1 vs 215.7mm boxes),
  killing the "tighten the crop to shorten the line" theory. And a caveat on the rollback-measure technique:
  `GetBoxOutline()` does NOT refresh mid-transaction after a `CropBox` change even though
  `GetLabelOutline()` does after a parameter change — so such a test must assert the input really changed
  before trusting a "nothing moved" result. All in `live-model/core.md`.
- 2026-08-04 — Built a `/graphify` knowledge graph over the whole Brain (624 nodes, 728 edges, 334 files).
  `graphify-out/` is gitignored — it is derived from `knowledge/`, `scripts/`, `skills/` and goes stale on
  any edit, so it is rebuilt on demand, never committed.
- 2026-08-04 — The graph named `AGENT-SPEC.md` the most-connected cross-topic node, and the reason is the
  duplication its own header has always declared ("intentionally duplicates summary-level facts… the topic
  file wins"). What was missing was *which* rows and *whether they're still true*: 8 of them, in §5.2, §6.4,
  §9.1, §9.2 and §9.3, owned by `live-model/hvac-ducts.md`, `families.md`, `hvac-terminals.md` and `core.md`.
  Now enumerated as a table in `AGENT-SPEC.md`'s header, all 8 verified in sync. Re-check that table when
  any of those four files changes — far cheaper than a full staleness pass over the spec.
- 2026-08-04 — Health check with no Revit available: `npm test` in `mcp-server/` green (3/3), and both
  consistency checkers (`verify-consistency.mjs` and the `.ps1`) agree exactly — 9 skills, 504 links across
  38 files, 264 scripts, no drift. Also confirmed the graph's "45 isolated nodes" warning is structural
  noise, not a documentation gap: it is mostly the ~250 script fragments, each correctly referenced once by
  `scripts/README.md`. Not worth chasing.
- 2026-08-04 — **First live bridge session in a while, and it caught a real bug.**
  `LinkLoadResultType.LinkNotNeeded` does NOT exist on Revit 2020 — the 2026-07-23 static-review flag on
  `action-reload-links.cs` was right, and the fragment could never have compiled ("CS0117"). Fixed to
  `UsedExisting`, with all 13 real enum members recorded in the header and the still-unverified part
  (which value `Reload()` returns for an up-to-date link) marked as such rather than assumed. Lesson worth
  keeping: a fragment that has never been executed even once can carry a plain compile error indefinitely —
  static review flagged it correctly but could not settle it, and only a live compile could.
- 2026-08-04 — `action-add-project-parameter.cs`: every legacy Revit-2020 API surface it uses compiles
  live (`ParameterType.Text`, `BuiltInParameterGroup.PG_DATA`, `ExternalDefinitionCreationOptions`,
  `NewInstanceBinding`/`NewTypeBinding`, both `ParameterBindings` overloads) — checked with method-group
  binding so nothing was invoked and the document was never touched. A reusable technique: bind a method
  group to a `Func<>` to prove an overload exists without calling it.
- 2026-08-04 — 16 of the 17 native MCP tools verified live against a synthetic fixture (4 ducts created in
  an empty scratch model, 2×300x300 and 2×500x400), each mutation re-checked in a SEPARATE later call per
  the `core.md` rule. All passed. Only `delete_elements` is unverified — the session ended before it. New
  gotcha found and filed in `core.md`: a name-based parameter report returns a BLANK column for a parameter
  that does not exist rather than an error, so "Level" on a duct looks like missing data when the real name
  is "Reference Level".
- 2026-08-04 — Open-items list split three ways (any-model / needs-a-fixture / confirmed-impossible). The
  old flat list mixed answered-and-closed items with genuinely-outstanding ones, so it always read as more
  unfinished than it was.
- 2026-08-04 — **Comprehension audit: three things a fresh session would have read wrong.** (1) The fire
  sprinkler skill was missing from `README.md`'s table, `SETUP.md` and both plugin manifests — 9 skills on
  disk, "8 skills" everywhere a reader looks. (2) `AGENT-SPEC.md` §3.5 claimed 206 fragments against 264
  real ones, every bucket but `examples/` wrong. (3) Four files carried double-encoded characters, and one
  was not cosmetic: `action-report-length-by-size.cs` did `s.Replace("<corrupted ø>", "")`, so round-duct
  sizes never parsed and every one of them sorted as 0 — quietly breaking the user's own standing
  "never sort a size breakdown by qty" rule. All fixed; the sort now strips non-numeric characters
  generally, so no non-ASCII literal is load-bearing there.
- 2026-08-04 — **`tools/fragment-index.mjs` — makes "reuse before writing new C#" a lookup instead of a
  500-line read.** The rule was never the problem; finding the fragment was. `--find <word>` searches
  every fragment's purpose AND its input fields and reports each hit's proven status; `--show <path>`
  prints one fragment's purpose, what it needs first, what it gives back, and the exact list of values to
  fill in — the "form fields" view that turns using a fragment into filling a form rather than reading
  code. `--json` for other tools. Computed from the fragments every run, stored nowhere, same rule as
  `brain-status.mjs`.
  Two real bugs caught while testing it, both worth remembering. **`process.exit()` right after a large
  `console.log` truncates stdout when piped** — Node's pipe writes are async and exit does not wait, so
  `--json | python3` produced an unterminated string; the fix is `process.exitCode` + `return`, never
  `process.exit()`, after output. And **matching a README row with `.includes(path)` is wrong**: 8
  fragments are named inside a *different* fragment's row as prose ("feeds `creators/create-dimension.cs`"),
  so those 8 inherited a neighbour's verification status and the tool reported 75 proven against
  brain-status's 73. Match the markdown link target `](path)` instead. Both tools now agree exactly —
  which is the point: two tools disagreeing about the same number is the drift this repo keeps producing.
- 2026-08-04 — **`scripts/recipes/build-test-fixtures.cs` — removes the "needs a fixture" blocker.** The
  open items had a whole category that no amount of effort could clear: fragments that can only be tested
  against a model containing something this scratch model doesn't have. This builds what an API can build
  — ducts, insulation, an Assembly, a model Group, and (opt-in) worksharing + worksets — unblocking the 3
  insulation fragments, `filter-by-assembly`, `filter-by-group`/`action-ungroup-elements`, and the 5
  worksharing ones.
  It deliberately does NOT re-attempt what is already settled (Ceilings are 2022+, Scope Box, Phase
  create/rename, Design Option activation, workset delete) — re-testing a closed answer is how a session
  wastes an hour — and it names what still needs a real file nobody can generate (RVT link, CAD import,
  PDF driver, sleeve and flip-capable families).
  Two design decisions worth keeping: `Document.EnableWorksharing` runs OUTSIDE the TransactionGroup,
  because it is a document-level call that cannot be made with a transaction open and cannot be rolled
  back — putting it inside would either throw or promise an undo that does not exist. And it refuses to
  run at all unless explicitly confirmed AND the model looks empty, since its whole job is creating
  elements.
  NOT run. Three calls have no prior use anywhere in this library and are the least proven part:
  `AssemblyInstance.Create` (the assembly fragment only ever read assemblies), `EnableWorksharing`, and
  the `GroupType.Name` setter. Header says to run the groups one at a time first and leave worksharing
  off until the rest is proven.
- 2026-08-04 — **`tools/verify-fragments-compile.ps1` — compile-check all 266 fragments without opening
  Revit.** Closes the gap that let `action-reload-links.cs` carry `LinkLoadResultType.LinkNotNeeded` — an
  enum member that does not exist on 2020 — for months: static review flagged it and could not settle it,
  only a compile could. Each fragment is wrapped in a harness class supplying what the bridge supplies
  (`Document`/`doc`/`UIDocument`/`uidoc`/`Application`/`UIApplication`) plus `sb`/`elements` when the
  fragment doesn't declare them, then compiled as a library and thrown away. **Whether to inject is
  decided from the CODE, not the header prose** — `ASSUMES:` lines come in a dozen wordings, but "does
  this file declare `sb`?" is unambiguous; comment lines are stripped first, because several fragments
  (including the prelude) quote `var sb = new ...` inside a comment and would otherwise be mis-detected.
  A `#line` directive maps every error back to the fragment's own line numbers.
  Two things it deliberately refuses to fudge: it requires a **Roslyn** `csc.exe` and explains why the
  C# 5 Framework one would produce hundreds of false failures (69 fragments use C# 7 pattern matching),
  and its header states plainly that compiling is a floor, not a ceiling — a fragment that compiles can
  still delete the wrong elements.
  Validated as far as possible without Revit: parses clean under PowerShell, and `-DryRun` generated all
  266 wrappers with zero duplicate `sb` declarations and correct per-fragment injection. The compile
  itself is unrun — that needs a machine with Revit installed.
- 2026-08-04 — **`scripts/lib/prelude.cs` — the shared toolkit the library never had.** Measured first:
  150 of 264 fragments carry their own `Transaction`+rollback, 136 their own collector setup, 80 their
  own `DisplayUnitType` call, 38 their own parameter lookup. The prelude holds `InTransaction`/
  `InTransactionGroup`, `ToFeet`/`ToMm`, `ResolveView`, `ParamOf`/`ParamText`, `LevelIdOf`, `CollectOf`
  and `SizeSortKey` once. The point is NOT shorter code (~10-15% of 20,948 lines) — it is **one place to
  be wrong**: supporting Revit 2021+ today means editing 80 files correctly, and with the prelude it is
  a two-line edit, with the replacement lines sitting in the file already.
  Deliberately ADDITIVE: it declares no name any fragment declares — in particular not `sb` or
  `elements`, since the filters declare those themselves — so it composes with all 264 un-migrated
  fragments unchanged, and every reporting helper takes the StringBuilder as an argument rather than
  capturing one. Nothing is migrated yet, and nothing has to be.
  **NOT compiled or run** — written with no Revit and no C# compiler. Every construct is copied from a
  fragment already proven through this bridge (`Func<>` lambdas, bare `Transaction`/`TransactionGroup`/
  `View`/`Wall`/`StorageType`, the `LevelIdOf` chain lifted verbatim from `filter-by-category.cs`), but
  assembled-from-proven-parts is not proven. `examples/prelude-smoke-test.cs` exercises every helper in
  one read-only call, including BOTH the commit and rollback paths of the transaction wrapper — run it
  before trusting any of it. `lib/` is now a tracked bucket in both consistency checkers and in
  brain-status, so the prelude cannot drift out of the index (verified by breaking the count).
  Sequencing decision recorded: migrating existing fragments waits for the C# compile check, because
  refactoring the 147 that have never been run would leave no way to tell a new break from an old one.
- 2026-08-04 — **`hvac-ducts.md` split three ways; `tagging.md` and `universal-actions-reference.md`
  reviewed and deliberately kept whole.** The 379-line duct file was three different jobs sharing a
  filename, so it became `hvac-ducts.md` (drawing/branching/connecting, 228), `hvac-duct-sizing.md`
  (slicing a trunk + why, 112) and `hvac-equipment-placement.md` (rotating/placing an FCU, 47). Cut
  mechanically at section seams and proved lossless — all 347 content lines accounted for, 0 missing.
  The dominant topic kept the original filename so most inbound references stayed valid; the 11 that
  pointed at moved sections were retargeted (fragments, `scripts/README.md`, `AGENT-SPEC.md`'s
  duplication table, `log.md`, the duct-routing skill), including one already-stale `§ Applying the
  sizes by script` that named a section which had not existed for some time.
  The other two files were NOT split, per the brain-self-maintain rule that 300 lines is "a split
  candidate, not a mandate": `tagging.md`'s sections are interlocking lessons about one algorithm (scale
  → clearances → overlap → leader side), and "Registry-based scored placement" explicitly supersedes an
  earlier section, so splitting would separate a lesson from what it replaces. `universal-actions-
  reference.md` is a menu that `knowledge/INDEX.md` routes "what actions are available" to — answering
  that means scanning all of it. Both now carry a `split-review: kept whole` marker recording the
  reasoning, which `brain-status.mjs` reads so the file stops being flagged and the decision doesn't get
  re-argued every time someone notices the line count.
- 2026-08-04 — **`tools/brain-status.mjs` — one honest answer to "what is the state of this Brain?"**
  Counts, how much of the library has actually been run against a real model, open items, oversized
  files, and drift, all computed from disk on every run and stored nowhere, because the recurring
  failure in this repo has been its own documentation getting ahead of reality. Wired as a SessionStart
  hook, so a fresh session knows before it acts instead of trusting a summary. `--full`,
  `--capabilities` (what this Brain can actually do, generated not written down) and `--json`. Stance is
  the user's call, 2026-08-04: **warn and keep working** — report what's unproven, never block. First run
  surfaced three knowledge files past the repo's own ~300-line split rule (`hvac-ducts.md` 379,
  `tagging.md` 325, `universal-actions-reference.md` 309).
- 2026-08-04 — **19 fragment `// SOURCE:` headers pointed at nothing.** Every recipe/command/context/
  creator fragment used `../knowledge/...` where its folder depth needed `../../knowledge/...`, and two
  `actions/*/` ones needed `../../../`. That header is how an agent gets from a piece of code to the
  reasoning behind it, so following one landed on a missing file. They are plain C# comments rather than
  markdown links, which is exactly why check 2 never saw them. All 19 repaired by recomputing each path
  from its real location, and check 7 added to both verifiers — 50 refs now checked every run.
- 2026-08-04 — **The edit hook had never once fired in a non-Windows session.** `.claude/settings.json`
  hardcoded `powershell`, so on Claude Code for web (and any Linux/macOS container) the PostToolUse hook
  silently did nothing — no warning, no output. An entire session of ~18 edits went through with zero
  automatic checking; the drift found that day was caught only because the checker was run by hand.
  Now wired to `tools/verify-consistency-hook.mjs`, which runs the portable checker with identical
  semantics (quiet exit 0 when clean, exit 2 + stderr on drift so the model sees it in the same turn).
  Node is the safer dependency: this repo already requires it for the MCP relay, and it exists on all
  three platforms. The `.ps1` wrapper is kept for a Windows machine with no Node on PATH. Lesson worth
  keeping: a guard that fails silently on a platform is worse than no guard, because it reads as passing.
- 2026-08-04 — Routing spot-check by walking one real question ("what is the maximum duct size used?")
  end to end. The routing itself is fine — START-HERE → `ajtools-live-model` → `model_summary` fast path,
  2-3 files read out of 264 scripts and 38 documents, no folder scan. But the question is ambiguous in a
  way nothing recorded: round ducts carry Diameter, rectangular carry Width × Height, no single number
  ranks them together, and the size-breakdown table's last row is the largest FIRST dimension rather than
  the largest duct. The skill sends the agent to `glossary.md` for exactly this, and `glossary.md` had no
  "size" entry — so a fresh session would have routed fast and then answered confidently wrong. Entry
  added. Worth repeating as a technique: pick a real question, walk it, and see what the router actually
  lands on.
- 2026-08-04 — `verify-consistency.ps1`/`.mjs` grew checks 4-6, because checks 1-3 could not see any of
  the three problems above: skill coverage in the entry docs, AGENT-SPEC's fragment counts vs disk, and a
  mojibake scan. The encoding check builds its patterns by *simulating* the corruption (UTF-8 encode, then
  cp1252 decode) instead of hand-typing them — so the list can't drift, and the checker doesn't flag its
  own source. Each of the six checks was proven to fire by deliberately re-introducing the defect, in both
  the Node and PowerShell versions, then reverting. Also found while testing: `Get-ChildItem -Recurse`
  needs `-Force`, or PowerShell skips dot-directories on Linux/macOS and never scans `.claude-plugin/`.
- 2026-08-04 — Node **is** installed on Ajmal's Windows machine (v24.18.0, at
  `~/Documents/node-v24.18.0-win-x64/node.exe`) but was **not on PATH**, so both hooks in
  `.claude/settings.json` failed to launch and the per-edit drift check silently never ran. Both `.mjs`
  tools worked fine when called with the full path — the fix was a PATH entry, not an install.
  **Resolved same day:** that folder was appended to the user PATH, so the hooks work in any terminal
  started afterwards. The `.ps1` wrapper stays as the fallback for a machine without Node on PATH.
- 2026-08-04 — graphify's AST pass returns **0 nodes** on this machine: every `ProcessPool` worker dies
  ("terminated abruptly") and the failure reads as an empty corpus rather than an error. Call
  `graphify.extract.extract(..., parallel=False)` — serial extraction gave 94 nodes from the same 34
  files that had just produced zero. Same shape as the hook bug above, and the recurring lesson of this
  whole log: a silent zero looks exactly like a clean pass.
- 2026-08-04 — **A `.ps1` saved as UTF-8 *without* a BOM does not run on Windows PowerShell 5.1.** PS 5.1
  assumes ANSI for a BOM-less script, so an em dash (`—`, UTF-8 `E2 80 94`) is read as `â€"` — and that
  final byte is cp1252 `0x94`, a **smart right double-quote, which PowerShell accepts as a string
  delimiter**. One em dash inside one string therefore opens an unterminated string and cascades: 33
  parse errors, most of them nonsense about C# `using` lines inside a here-string. `verify-fragments-compile.ps1`
  had never run once for this reason. Proof it was encoding, not syntax: the same file parsed with
  **0 errors** when its bytes were handed to the parser as UTF-8. Fixed by writing a BOM onto the three
  `.ps1` files that contain non-ASCII (`invoke-bridge`, `verify-consistency`, `verify-fragments-compile`);
  content is byte-identical otherwise. This is the same PS 5.1 ANSI assumption already logged for
  `Get-Content`/`Set-Content` bulk edits — but it bites *executing* a script, not just editing one. Rule:
  **any `.ps1` in this repo that contains a non-ASCII character must be saved with a UTF-8 BOM.**
- 2026-08-04 — With that fixed, `verify-fragments-compile.ps1` ran for the first time (Revit 2020 + VS 2022
  Roslyn): **259 of 267 fragments compile.** It immediately earned its keep by finding two more Revit 2020
  API gaps of exactly the `LinkNotNeeded` kind — `View.AnnotationCropActive` and
  `ScheduleDefinition.SetKeyName` — plus a variable-shadowing bug and a plain syntax error that had sat in
  the library unnoticed. 4 of the 8 failures are harness artifacts, not fragment bugs; see the Open items
  list above for which is which before touching anything.
- 2026-08-04 — **267 of 267 fragments now compile.** The 4 real bugs are fixed: `t` renamed to
  `tIntersect` in `action-fillet-elements` (the Transaction `t` sat inside its scope), a stray
  `using Autodesk.Revit.DB;` deleted from `create-parametric-box-family-...` (a using DIRECTIVE is illegal
  mid-script, and a fragment is always pasted mid-script), and the two Revit 2020 API gaps rerouted —
  `View.AnnotationCropActive` → the `VIEWER_ANNOTATION_CROP_ACTIVE` built-in parameter, and
  `ScheduleDefinition.SetKeyName` → `ViewSchedule.KeyScheduleParameterName`. Worth naming the pattern:
  **both API gaps were already wrapped in a `try/catch` that could never help**, because a missing member
  is a compile error, not a runtime exception — the fragment never built at all. A `try` around an API you
  are unsure of buys nothing; only a compile proves it exists.
- 2026-08-04 — The harness had 2 bugs of its own, which is why 4 "failures" were never real. Its
  `declaresElements` test required an `=`, so `List<Element> elements;` (no initialiser — 3 filters use
  exactly that) read as "does not declare", and it injected a second one on top: CS0128 three times. Now
  matches `[=;]`. And `examples/prelude-smoke-test.cs` was compiled alone even though its own header says
  to paste `lib/prelude.cs` first; the harness now prepends the prelude for that one file, which turns a
  guaranteed false failure into the first real proof that the prelude and its smoke test agree. Lesson:
  when a checker reports failures, confirm the checker is right before touching the code it accuses.
- 2026-08-06 — New `semantic-index/`: plain-English (semantic) search over `skills/`, `knowledge/`,
  `scripts/` and the 5 root docs (incl. `CLAUDE.md`) — 306 files, Python + ChromaDB, fully offline after
  a one-time model download. **Additive** — it sits beside `tools/fragment-index.mjs` (keyword) rather
  than replacing it; use keyword when you know the word, semantic when you only know the job. Ask with
  `semantic-index/ask-brain.cmd "question"` (add `--area fragment` for C# — prose files outrank fragments
  otherwise); rebuild with `semantic-index/index-brain.cmd`, ~80 s.
- 2026-08-06 — **The semantic index is a snapshot and goes stale silently** — it keeps returning the old
  text after any edit to `skills/`, `knowledge/` or `scripts/`, with no warning. Rebuilding is part of
  finishing a Brain edit, the same way updating `scripts/README.md` is. Every run rebuilds from scratch
  on purpose, so renamed and deleted files leave no ghosts (same reasoning `.gitignore` already applies
  to `graphify-out/`). Only its `.py`/`.cmd`/`.md`/`.txt` are committed; venv, model and database are
  ignored.
- 2026-08-06 — `semantic-index/` deliberately writes **nothing** to `%TEMP%`: the venv, ChromaDB, the
  embedding model and Python's own temp are all forced inside the folder by `brain_common.py`, and this
  was verified after the build. Reason: `tools/verify-fragments-compile.ps1` writing ~267 unsigned DLLs
  into `%TEMP%` is what Sophos flags as `ML/PE-A`. Any future tool added to this repo should assume the
  same constraint rather than rediscover it — on a company-managed endpoint, a temp folder full of
  freshly written binaries is the trap, not the specific tool that wrote them.
- 2026-08-06 — `semantic-index/` got a **hybrid** search (`ask-brain-hybrid.cmd`,
  `brain_search_hybrid.py`): meaning + exact words merged by Reciprocal Rank Fusion, because closeness
  (0-100) and BM25 (unbounded) cannot be added meaningfully — each list votes by position instead.
  `brain_search.py` is deliberately left untouched as the semantic-only baseline to compare against.
  Routed from `CLAUDE.md` and `START-HERE.md` so a session actually finds it.
- 2026-08-06 — **What hybrid fixed, and the rule behind it.** Semantic alone answered "how many diffusers
  do I need in this room" with the *sprinkler* files: the shape of the question ("count devices in a
  room") outweighed the single word naming the device. Adding exact-word matching weighted by **rarity**
  put `ajtools-hvac-terminal-layout` first — "diffuser" is in 12 files, "room" in 57, so diffuser earns
  ~2x the weight. Generalise: when two jobs share a shape, the discriminating word is rare, so rarity is
  the signal to trust, not similarity.
- 2026-08-06 — **`tools/fragment-index.mjs` only reads `scripts/*.cs`** — it can never surface a skill or
  a knowledge note. Any ranking signal built on it therefore promotes fragments *only*, and must be gated
  to genuinely rare words. Ungated it buried the answering skill under `create-rooms-in-enclosed-regions.cs`
  for the diffuser question. Two separate bugs in the gate were also worth recording: stripping plurals
  *before* removing stopwords let "this" survive as "thi" and match inside "within"/"something"; and a
  threshold derived from per-FILE counts (room = 19%) was applied to per-CHUNK counts (room = 8%) and let
  it through. Fixed with a self-tuning rule — only the rarest words in a given question qualify.
- 2026-08-06 — **Hybrid search measured by independent testers, not by its author: 24 questions written
  in a modeller's own words → 13 good, 3 acceptable, 8 wrong.** Tuning a retrieval tool on the questions
  you invented while building it proves nothing; this is the number that counts. Three mechanical causes
  were found and fixed — a bare number in the question matched a fragment's default value (`near 40 rooms`
  → `maxSegmentsPerRoom = 40`), a word matched an input FIELD NAME (`duplicate option` → `duplicateOption`),
  and an ordinary English verb matched a fragment path (`copy` → `action-copy-elements.cs`). Fix: score a
  match by WHERE it landed (`PURPOSE`/prose full weight, `INPUTS` 0.45, code body 0.35), drop bare numbers,
  and match the fragment-index signal against PURPOSE only. Re-tested: 2 failures fixed, 1 borderline
  improved to correct, **0 regressions** across 6 known-good questions.
- 2026-08-06 — **The remaining failures are vocabulary, not ranking, and reranking cannot fix them.**
  "add 4 more floor levels" → `create-floor.cs` (the slab creator, actively wrong); "how many light
  fitting" → matched "light hazard"; "take my door schedule out to excel" → missed
  `action-export-schedule-to-csv.cs`. In each case the site word the user typed simply is not in the file
  that answers them, so no amount of re-scoring reaches it. The fix already exists in this Brain unused:
  [`glossary.md`](glossary.md) IS the user's-terms → Revit-terms map. Expanding a query through it before
  searching is the obvious next build. Until then the honest instruction is **read the top 3-5, not just
  #1** — the right file was usually still in that window.
- 2026-08-06 — **The semantic index now tells you when it has gone stale**, instead of answering
  confidently from an older copy of the Brain. `brain_index.py` writes `index-manifest.json` (a content
  hash per indexed file) after every successful build, and every hybrid search compares the Brain on disk
  against it, printing a `STALE INDEX` banner naming what changed/appeared/vanished. It hashes CONTENT,
  not modified-dates: a git checkout or a folder copy changes dates without changing a word, and a
  warning that cries wolf is one you learn to ignore. It warns rather than blocks — the results still
  print, they are just from the old picture. Verified by adding a file, changing a file, and confirming
  silence when current.
- 2026-08-06 — **Worth being clear about what is NOT automatic here.** Nothing in this Brain rebuilds
  itself on a timer, and no mechanism decides that a finished job was "worth saving" — that judgement is
  the standing discipline in `START-HERE.md`, carried out by whoever is working, not by machinery. The
  staleness banner is deliberately the smaller, reliable thing: it cannot make you rebuild, it can only
  make forgetting impossible to do quietly. Auto-rebuilding on every file edit was considered and
  rejected — at ~80 s a rebuild, a session touching ten files would spend fifteen minutes re-indexing.
- 2026-08-06 — **Nine `context/` fragments live-verified** against a real open model (Revit 2020,
  `Project1`): active-view, project-units, all-warnings, workset-info, model-categories, used-families,
  design-options, levels-and-grids, linked-models. Proven count 73 → 82. Real finding worth keeping:
  **`Document.GetWarnings()` returns rows with an EMPTY failing-element list** — system-level warnings
  such as "flow direction mismatch" and "No Loss Defined" name no element, so never assume every warning
  row points at something you can select. Four of the nine only exercised their *empty* branch (no
  worksets, no links, no design options); their README rows say so rather than claiming full proof.
- 2026-08-06 — New [`site-vocabulary.md`](site-vocabulary.md): a plain table of site word → Revit word,
  read **live** by the hybrid search (no rebuild needed, so a new row works immediately). It rewrites the
  question before searching — "out to excel" → "export csv schedule". Measured on the 4 vocabulary
  failures: 1 fixed outright (the Excel one, now #1), 1 moved to #2, 1 had its *harmful* top hit removed
  (`create-floor.cs`, the slab creator, no longer answers "add 4 more floor levels"), 1 unchanged. Zero
  regressions across 6 known-good questions.
- 2026-08-06 — **Two rules learned building that table, both recorded inside it.** (1) The matched phrase
  must be REPLACED, not merely supplemented: adding "level" while leaving "floor" in the question still
  returned the slab creator, because the misleading word goes on misleading. (2) **A row earns its place
  by being narrow.** `drawing → view sheet` looked reasonable and made things worse — it fires on almost
  any question, and it buried `filter-by-tag-status.cs` under a view-template fragment. Rejected rows are
  kept in the file with the reason, so they are not helpfully re-added later.
- 2026-08-06 — **Rebuilding the semantic index is now 2-4 s instead of 80 s.** `index-brain.cmd` re-reads
  only the files whose content hash moved (nothing changed 2.3 s · one changed 2.8 s · one added 3.9 s ·
  one deleted 2.7 s · full 79 s). `--full` forces a complete rebuild. This is what makes rebuilding cheap
  enough to do every single time, which was the whole objection to it being a manual step.
- 2026-08-06 — **Two safeguards make the fast path trustworthy, and both are the interesting part.**
  (1) *Ghosts*: a file that made 12 chunks and now makes 8 would leave 4 orphans — text existing nowhere
  in the Brain, still answering questions. Every chunk of a changed file is deleted (by its `path`
  metadata, so the old count does not matter) before new ones are written. (2) *Wrong-shaped chunks*: if
  the chunking rules change, every stored chunk is stale but the FILES are untouched, so a file-by-file
  comparison would skip all of them. A build fingerprint over the settings plus the source of
  `brain_index.py`/`brain_common.py` catches this and forces a full rebuild — deliberately blunt, since a
  needless 80 s beats a silently half-migrated index. Proven by shrinking a 30-chunk file to 2 and
  confirming the removed text was gone, and by an incremental index reporting the **same 2,540 chunks as
  a full rebuild**. Generalise: any cache keyed on "what changed" needs a second key for "what the rules
  were", or it will happily serve results built under rules that no longer exist.
- 2026-08-06 — **Live-verifying `filter-by-category.cs` found a real silent-zero bug in 4 files.** The
  "which level is this element on?" fallback chain never tried `RBS_START_LEVEL_PARAM`, and on a Duct the
  other four parameters are **not present on the element at all** — so every MEP curve resolved to
  `InvalidElementId`, which never equals the level being filtered for. Result: "ducts on Level 1" returned
  **0 and reported success**. Proved side by side on one model: fixed chain 3 ducts, old chain 0, walls
  unaffected at 8. Fixed in `lib/prelude.cs` (the shared toolkit, so it propagates),
  `filters/by-identity/filter-by-category.cs`, `filters/by-location/filter-by-elements-on-level.cs`,
  `actions/reporting/action-report-parameters.cs`. Full probe table in
  [`live-model/core.md`](live-model/core.md).
- 2026-08-06 — **The lesson is about how it failed, not what was wrong.** A missing level resolves to
  `InvalidElementId`, and a comparison against it is simply false — so the bug could only ever produce an
  empty result, never an error. That is why it survived a compile check, a 267-fragment compile pass and
  months of review: nothing looks wrong about "0 elements found". **Any filter that can return zero needs
  its zero questioned once against real elements you know exist** — running it and seeing "0" is not
  verification, it is the bug.
- 2026-08-06 — **Content hashes in this repo must flatten line endings first.** The new incremental index
  keys off content hashes; committing the indexer flipped its own line endings LF→CRLF (git
  `core.autocrlf`, this repo is CRLF), every byte-level hash moved without a word changing, and the next
  rebuild wasted 92 s doing a full pass for nothing. Fixed by normalising `\r\n`→`\n` before hashing, in
  both the per-file and the build fingerprint. Worth generalising: **on this repo, any "did this change?"
  check that reads raw bytes will fire spuriously the first time git touches the file** — the same class
  of trap as the PowerShell UTF-8 round-trip already recorded above, and it also shows up as a diff far
  larger than the edit actually made.
- 2026-08-06 — **Filters live-verified against a real model** (`Project1`, Revit 2020): `filter-by-levels`
  (case-insensitivity confirmed with a lowercase term, ascending order asserted), `filter-by-id-list`
  (4 real Ids + 1 bogus — reports found/missing, does not throw), `filter-by-warnings` (9 elements via a
  description filter), `filter-by-connection-status` (**both** branches across 3 categories). Proven count
  73 → 87 across the session.
- 2026-08-06 — **The verification method that actually catches things, now used as standard.** Two habits
  earned their place: (1) **prove a zero is real** — every filter run alongside a deliberately
  non-matching term, so "0 results" is demonstrated rather than assumed (this is what the
  `RBS_START_LEVEL_PARAM` bug hid behind); (2) **assert a partition** — for
  `filter-by-connection-status`, open + fullyConnected + noConnectors had to sum exactly to the category
  total, which no single-branch test would have shown. Best of all, two unrelated fragments agreed
  independently: its 9 open-ended elements were exactly the 9 that `filter-by-warnings` found via
  "open connector". **Two fragments reaching the same physical fact by different routes is worth more
  than either one returning a plausible number.**
- 2026-08-06 — Branches only exercised on their empty/absent path are now recorded as such in
  `scripts/README.md` rather than being marked proven — `filter-by-warnings`'s `errorsOnly` (this model
  has 0 Error-severity warnings), and the workset/link/design-option branches of the `context/` set.
  **A half-tested fragment recorded as proven is worse than one recorded as unknown**, because the next
  session stops checking.
- 2026-08-06 — Two more filters verified, both worth the depth. `filter-by-category-and-numeric-param`
  (the "500mm duct" filter the Brain's own worked example is built on) — **all four comparison modes**
  eq/gte/lte/between, `parameterName` swapped to Width, an absent parameter (Diameter → skipped, no
  crash), a bogus name (no throw), and the mm→ft conversion (300mm = 0.984252 ft). Every expected count
  was derived from the ducts' real 300mm size *before* running, so each zero is a proven zero.
  `filter-by-multiple-categories` — both scopes, and **dedupe proven by listing one category three times
  and getting 3, not 9**, which is the only way to show a `HashSet<UniqueId>` is actually doing its job.
  Proven count now 89.
- 2026-08-06 — Four more filters verified, taking the count to **93** (73 at session start).
  `filter-by-category-name` (display-name → Id -2008000 → 3 ducts, cross-checked against a
  `BuiltInCategory` baseline that never touches the name), `filter-by-phase` (3 ducts on "New
  Construction"), `filter-by-pin-status`, `filter-by-views` (2 of 35 View-class elements — the part worth
  proving is that it **excluded all 16 view templates and the 1 schedule**).
- 2026-08-06 — **A better way to prove a two-branch filter: change the model, watch it flip, roll back.**
  `filter-by-pin-status` returned "3 unpinned" — true, but equally what a filter that ignores pinning
  entirely would return. So the test pinned one duct inside a `Transaction`, confirmed the counts moved
  to 1 pinned / 2 unpinned, then called `RollBack()`. The model is left byte-identical and the filter is
  proven to track real state rather than to be accidentally right. **Use this wherever a boolean filter's
  "off" state is the model's default** — otherwise the passing result and the broken result are the same
  number.
- 2026-08-06 — **The test model was extended rather than the untestable filters being skipped.** A Room
  (enclosed, 273 m²), a Pipe (5000 mm, 50 mm) and a named Group (`MEP_TestGroup_Terminals`, 5 members)
  were added to `Project1`, unlocking filters that had no reachable main path. `filter-by-room` and
  `filter-by-group` verified immediately after. **Fifteen filters were blocked purely by an empty model,
  not by anything wrong with them** — building the fixture is cheaper than leaving a third of the library
  permanently unprovable, and `scripts/recipes/build-test-fixtures.cs` exists for exactly this.
- 2026-08-06 — **`filter-by-room`'s result is proven by geometry, which is the standard to aim for.**
  It returned 4 of 5 air terminals; the fifth sits at y=15862 while the enclosing wall loop tops out at
  y=12862, so it is genuinely outside. A bare "4 of 5" would have been a plausible number and nothing
  more — printing each terminal's coordinates and in/out verdict is what turns it into evidence. Note the
  fragment tests with the ROOM's Z, not the element's, which is why terminals at Z=0 still resolve.
- 2026-08-06 — Placing that Room also re-confirmed the MEP level finding from earlier today: the new Pipe
  reports its level through `RBS_START_LEVEL_PARAM` (= 311), exactly as the fixed fallback chain now
  expects. Ducts and pipes behave the same way here.
- 2026-08-06 — **The test model became a workshared local file** (`Project1_ajmal.al.rvt`, 2 user
  worksets), which unlocked the last branch of `context-workset-info` and all of `filter-by-workset` —
  both now verified. `filter-by-workset` passed a semantic check, not just a count: "Shared Levels and
  Grids" returned exactly the 4 grids and 2 levels, which is precisely what that workset is for.
- 2026-08-06 — **Second API gotcha of the day, same shape as the level one: `ELEM_PARTITION_PARAM` is an
  INTEGER.** `.AsString()` returns `null`, which reads exactly like "this element has no workset" —
  a probe written earlier in this very session fell for it. Use `.AsValueString()` for the name,
  `.AsInteger()`/`WorksetId` for the Id. **And workset Id `0` is a real workset** (`Workset1`), so any
  code treating `0` as unset silently drops every element on the default workset. Full table in
  [`live-model/core.md`](live-model/core.md). Both of today's gotchas share a lesson: **when a Revit read
  returns null or zero, confirm the parameter's `StorageType` before concluding the model is empty.**
- 2026-08-06 — Test fixture extended again: a **door** (loaded `M_Door-Single-Flush_Panel_Double-Acting`
  from the metric library, hosted in wall 918932) and an **RVT link**. For the link a small purpose-made
  `MEP_TestLink.rvt` was created rather than linking any of the real project files sitting in the same
  folder — those are 200-300 MB of live work and have no business being pulled into a test fixture.
  `filter-by-host`, `filter-by-links` and the links branch of `context-linked-models` all verified.
  Proven count now **99**.
- 2026-08-06 — **`filter-by-host`'s zero is the good kind, and it is worth copying the shape.** It found
  1 hosted element on wall 918932 and **0 on a different wall in the same run**. The same code, same
  model, one wall apart — so the 0 cannot be a broken lookup. Pairing a positive and a negative case in
  one execution is stronger than any amount of reasoning about why a zero might be trustworthy.
- 2026-08-06 — **Design Options cannot be created through the Revit 2020 API.** `DesignOption` exposes
  exactly one static method, `GetActiveDesignOptionId`; there is no `CreateDesignOptionSet` and nothing
  option-related on `Autodesk.Revit.Creation.Document`. Established by **reflecting over the type and
  printing its real members** after the call failed to compile — which is the technique to reuse: it
  separates "this build lacks the method" from "I got the name wrong", definitively. Design Options
  therefore have to be made by hand in Manage → Design Options before `filter-by-design-option` and
  `action-set-design-option` can ever be proven.
- 2026-08-06 — Ajmal created the option sets by hand, and verifying `filter-by-design-option` against
  them **found a third silent-wrong-answer bug, now fixed live**. Revit names the primary option of
  *every* option set `"Option 1 (primary)"` — so a model with 3 sets has 3 identically-named options. The
  fragment's `FirstOrDefault` on name picked whichever came first and reported success. It now takes a
  `designOptionSetName` and **refuses to resolve an ambiguous name**, listing the candidate sets instead.
  Verified across 6 cases: refuses when ambiguous, resolves to the correct distinct Id when the set is
  given, still handles Main Model, and reports name-right/set-wrong as not-found.
- 2026-08-06 — **A default name that Revit assigns automatically is the most dangerous kind of lookup
  key**, because duplicates are the norm rather than the exception. `"Option 1 (primary)"` is the clear
  case, but the same reasoning applies to `"Level 1"`, `"Workset1"` and any other Revit-generated
  default: **match on Id where one is available, and when matching on a name, check how many things
  answer to it before using the first.** Three of today's four bugs were a lookup that could not fail
  loudly — it returned the wrong thing, or nothing, and said success either way.
- 2026-08-06 — **Elements cannot be moved into a Design Option through the API — all six routes are
  read-only** (`Element.DesignOption`, `DESIGN_OPTION_ID`, `DESIGN_OPTION_PARAM`, `View.DesignOption`,
  the view's "Design Option" parameter, and there is no `Document` setter). Established by reflection,
  same technique as the create-option finding. **The workaround needs exactly one UI action:** an element
  lands in whatever option is *active when it is created*, and the active option is set from Revit's
  status bar — so the user picks it there, and the bridge then creates elements straight into it. Moving
  *existing* elements stays UI-only (Manage → Design Options → Add to Set). Table in
  [`live-model/core.md`](live-model/core.md).
- 2026-08-06 — `filter-by-length` verified (all four modes against lengths printed first, plus walls, so
  three separate zeros are checkable) and `filter-by-tag-status` verified **on both branches by changing
  the model**: 3 untagged / 0 tagged → create a tag in a transaction → 2 / 1 → roll back → 3 / 0. Proven
  count **102**. This is the second filter where the "off" branch was only meaningful once the model was
  changed and put back — worth treating as the default technique rather than a special case.
- 2026-08-06 — Nine more filters verified in one pass, taking the count to **111 (42%)** and filters
  specifically to **36 of 49**: `filter-by-family`, `filter-by-types`, `filter-by-family-type`,
  `filter-by-grid`, `filter-by-view-templates`, `filter-by-current-selection`,
  `filter-by-category-and-family`, `filter-by-parameter-exists`, `filter-by-insulation-status`.
- 2026-08-06 — **The change-it-then-roll-it-back technique is now the house style, used three times
  today.** `filter-by-pin-status`, `filter-by-tag-status` and `filter-by-insulation-status` all read
  "everything is off" on a fresh model — which is *exactly* what a filter that never matches also
  reports. Each was proven by making the state true inside a `Transaction` (pin a duct / tag a duct /
  wrap 25 mm of Duct Wrap on a duct), confirming the counts flipped, then `RollBack()`. The model is left
  byte-identical every time. **Any boolean filter whose "off" state is the model's default needs this**;
  reading the default and calling it proof is not a test.
- 2026-08-06 — Two other checks worth copying. `filter-by-category-and-family` has a LINQ name path and a
  `FamilyInstanceFilter` path — **running both and getting the same 5 elements by different mechanisms**
  is stronger than either alone. And `filter-by-parameter-exists` demonstrates its three modes rather
  than describing them: `Width` → has 3 / hasvalue 3 / missing 0, while blank `Comments` → has 3 /
  hasvalue 0 / missing 3. The contrast IS the proof that `has` and `hasvalue` differ.
- 2026-08-06 — **This morning's `RBS_START_LEVEL_PARAM` fix is now proven end to end.**
  `filter-by-elements-on-level` returns 3 ducts and 1 pipe on Level 1 — **both were 0 before the fix** —
  plus 8 walls through the never-broken Wall branch, and 0 on Level 2, which is a proven zero because the
  same code gives 3 on Level 1. The bug was found, fixed in 4 files, and closed out against a real model
  in one session. Proven count **114 (43%)**, filters **39 of 49**.
- 2026-08-06 — `filter-by-linked-model-elements` verified, and the check is worth repeating whenever
  something claims to read a *different* document: the link reports **1 level while the host has 2**.
  A matching number would have proved nothing — it is the mismatch that shows it genuinely crossed into
  the linked document instead of quietly re-reading the host. Also `filter-by-size` (both the Size-text
  and numeric branches, rectangular and round).
