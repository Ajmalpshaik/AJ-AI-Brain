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

Rewritten 2026-08-07 at the end of the big verification campaign so the next session can resume without
re-deriving anything. **223 of 268 fragments are verified against a real Revit model (83%).** The 45
below are everything left, grouped by WHAT UNBLOCKS EACH — not by folder, because the folder tells you
nothing about whether you can act. Headings are bold and items numbered on purpose: `tools/brain-status.mjs`
counts them that way.

**Needs a live bridge + the current test model — just run them:**
1. `creators/create-hvac-zone.cs` — needs Spaces; `create-space.cs` is verified, so create then add.
   CHECK FIRST: `AddSpaces` returns ONE bool for the whole SpaceSet and the code turns it into a
   per-space count without ever reading `zone.Spaces.Size` back.
2. `creators/create-room-elevations.cs` — the room exists. CHECK: it picks the Elevation ViewFamilyType by
   `FirstOrDefault` and never reports which, so two projects can silently differ.
3. `actions/sheets-views/action-add-aligned-dimensions.cs` — CHECK: Revit deletes degenerate dimensions
   during the regeneration AT commit and only warns, so re-read `Document.GetElement(dim.Id)` afterwards.
4. `actions/sheets-views/action-add-spot-elevations.cs` — CHECK: it takes the FIRST PlanarFace with a
   Reference, with no `Math.Abs(pf.FaceNormal.Z) > 0.9` test, so a vertical side face yields a plausible
   but wrong elevation.
5. `actions/sheets-views/action-manage-sheet-sets.cs` — CHECK: it mutates `Document.PrintManager` inside a
   transaction, and PrintManager state is not transactional, so it will not roll back.
6. `actions/structural-changes/action-add-remove-insulation.cs` — ducts exist; create-then-rollback.
7. `actions/structural-changes/action-place-accessory-on-run.cs` — ducts exist; create-then-rollback.
8. `examples/color-isolate-select-by-size.cs` — composed example; every part is already verified.
9. `recipes/slice-trunk-for-sizing.cs` — the 3 connected ducts are the fixture it wants.

**Needs Ajmal's explicit go-ahead — destructive, or changes settings that do not roll back:**
1. `actions/sheets-views/action-export-sheets-to-pdf.cs` — THE DANGEROUS ONE. Calls
   `SelectNewPrintDriver` and overwrites the current ViewSheetSet OUTSIDE any transaction, so it
   permanently changes the document's printer with nothing to undo. Do not run casually.
2. `actions/structural-changes/action-purge-unused.cs` — deletes unused types project-wide.
3. `examples/purge-unused-view-templates.cs` — deletes view templates.

(`commands/command-compact-save.cs` was item 3 here until 2026-08-10 — now closed as **blocked**, not
pending: saving of any kind is impossible through the bridge's transaction group. See the Log entry and
the fragment's own header.)

**Needs a file, a printer, or content only Ajmal can supply:**
1. `creators/load-family.cs` — an `.rfa` on disk. KNOWN BUG found by reading, fixable blind: its
   `if (ok && fam != null) / else if (fam == null && !ok)` has no plain `else`, so the already-loaded
   case (`ok == false`, `fam != null`) prints NOTHING — the very case its header promises to report.
2. `actions/structural-changes/action-extract-cad-curves.cs` — a CAD import.
3. `actions/structural-changes/action-copy-from-link.cs` — the RVT link exists but has no elements.
4. `actions/parameters-naming/action-import-parameters-from-csv.cs` — a CSV.
5. The six `actions/sheets-views/action-export-*.cs` — a real export folder, plus IFC/NWC exporters
   installed. ALL SIX SHARE ONE SHAPE: they announce a written file without ever checking the disk, and
   `action-export-views-to-dwg.cs` additionally discards the bool `Document.Export` returns.

**Fixture-blocked — need model content that does not exist yet:**
1. Electrical content — `filters/by-relationship/filter-by-electrical-system.cs`.
2. A nested shared family — the positive path of `filters/by-relationship/filter-by-subcomponents.cs`.
3. A Ceiling — `creators/create-ceiling.cs`, `recipes/ray-trace-to-ceiling.cs`.
4. A sleeve family — `recipes/place-sleeves-at-wall-penetrations.cs`.

**Big recipes — deliberately left to be proven the day they run on a real job:**
1. Verifying these verbatim costs far more than it returns against a test model, and they are idempotent.
   MARK THEM VERIFIED THE FIRST TIME THEY WORK ON REAL WORK — Ajmal's instruction, 2026-08-07:
   `recipes/create-mep-line-standards.cs` (385 lines, his office standard),
   `recipes/create-mep-text-standards.cs`, `recipes/tag-elements-in-active-view.cs`,
   `recipes/create-revisions-from-sheet-dates.cs`,
   `recipes/create-parametric-box-family-with-duct-connector.cs`, `recipes/build-test-fixtures.cs`.

**Standing task, not an open item:**
1. `toolserify-fragments-compile.ps1` — 24 fragments had REAL LOGIC changed on 2026-08-07 and it has
   not been re-run since, so this matters more than usual. It writes ~267 unsigned `out.dll` files into
   `%TEMP%` and Sophos flags that as `ML/PE-A`; tell IT before running, not after.

**CONFIRMED IMPOSSIBLE on Revit 2020 — closed, do not re-attempt.** These are answered, not outstanding:
Set Active Design Option (no setter exists anywhere in the assembly — re-confirmed by reflection
2026-08-07; note both of `action-set-design-option.cs`'s guards ARE verified, only the copy is blocked),
Create Phase and Rename Phase (`Document.Phases` is read-only), workset create/delete/visibility
(API is 2022+), Scope Box creation and everything depending on it (`create-scope-box`,
`action-update-scope-box`, `filter-by-scope-box`), Sync With Central (needs a real central and a second
user), and view-title extension-line length (API landed in 2022 — confirmed from Rhythm-for-Dynamo's own
source). `action-reload-links` / `action-unload-remove-links` need a link that is genuinely out of date.
See [`universal-actions-reference.md`](universal-actions-reference.md) and `live-model/core.md`.

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
- 2026-08-06 — **Filters finished: 46 of 49 proven**, overall count **121 (45%)**, up from 73 at the start
  of the day. The last seven — `filter-by-material`, `filter-by-schedules`, `filter-by-insulation-type`,
  `filter-by-region`, `filter-by-element-intersection`, `filter-by-unenclosed-spatial-elements`,
  `filter-by-assembly` — all needed the model changed and put back, which is now routine rather than
  notable.
- 2026-08-06 — **`filter-by-element-intersection` confirms its own header the hard way.** It reports 0 for
  a door against its host wall, and 0 for ducts joined at a connector — both correct, because hosting
  *cuts* an opening and connecting *abuts*; neither is volumetric overlap. Proving that required
  manufacturing a real one: a duct copied 100 mm sideways inside a transaction, each finding the other
  symmetrically, while a second copy 50 m away was correctly ignored. **A filter that returns 0 on every
  natural case in a model can only be proven by creating the unnatural one.**
- 2026-08-06 — Two API details worth keeping. `Document.Create.NewRoom(Phase)` is the unplaced-room call —
  `NewRoom(null, null)` does not compile (ambiguous between `NewRoom(Room, PlanCircuit)` and
  `NewRoom(Level, UV)`). And a freshly created `AssemblyInstance` has an **empty `Name`** until its type
  is named, with `AssemblyTypeName` throwing "No valid type for the assembly instance" — so
  `filter-by-assembly`'s name path stays unproven while its Id path is verified.
- 2026-08-06 — **Three filters end the day genuinely unprovable, and they are recorded as such rather
  than quietly skipped:** `filter-by-electrical-system` (no electrical content of any kind),
  `filter-by-subcomponents` (needs a family with nested shared components), `filter-by-scope-box` (no
  Scope Box, and no API to create one). Also `filter-by-schedules`' template/`<...>` exclusion branch,
  which never ran because the model had none of either — the fragment is marked verified, but that
  limitation is written into its row.
- 2026-08-06 — **`brain-status` was under-reporting, and the cause is worth knowing.** `statusOf()` in
  `tools/fragment-index.mjs` decides "verified" with `/verified 2026/` — a literal, case-sensitive test.
  `connect-equipment-to-air-terminals.cs` read **"✓ verified live 2026-07-26"**, and the word *live*
  between "verified" and the year meant it never matched; a genuinely proven recipe has been counted as
  unproven ever since. `ray-trace-to-ceiling.cs` said "not yet live-verified" in lowercase against a
  case-sensitive `/NOT yet live-verified/`, so it read as no-status instead of untested. Both rows
  reworded to the canonical form rather than loosening the regex — **one strict format that is easy to
  check beats a clever matcher**. When adding a row, write exactly `✓ verified YYYY-MM-DD`, with any
  qualifier *after* the date.
- 2026-08-06 — Five `commands/` verified (`clear-selection`, `regenerate`, `zoom-to-fit`, `activate-view`,
  `unhide-all-active-view`), plus `native-undo` re-verified and finally dated. **`PostCommand` is
  asynchronous** — it runs after the API context ends, so the undo cannot be checked in the same call
  that posts it. Proved by committing a throwaway duct copy, posting Undo, and confirming in a *later*
  call that the copy was gone while the room, pipe, group, door, link and 6 design options all remained.
  Count **128 (48%)**.
- 2026-08-06 — **Sixth silent bug, and the level problem has a second half nobody had noticed.**
  `action-count-by-group.cs` grouped by `"Level"` and answered `None | 3` for ducts that are provably on
  Level 1. Cause: it did `LookupParameter("Level")`, and **a Duct's parameter is called "Reference
  Level"** while **a Wall has no level-named parameter at all**. Only an air terminal has a literal
  `"Level"` — which is exactly why this looked fine whenever anyone tried it. The morning's
  `RBS_START_LEVEL_PARAM` fix solved the *Id* half of finding an element's level; this is the *name*
  half. Fixed with the same fallback chain the filters use: ducts, walls, pipes and terminals now all
  report Level 1, and Family / Family and Type / Category / Comments / Width were re-checked for
  regressions. Table in [`live-model/core.md`](live-model/core.md).
- 2026-08-06 — **A verification harness can invent a bug that is not there.** While regression-checking
  the above, a trimmed copy of `groupKey` reported "Family" as a *type* name and it briefly looked like a
  second defect. It was not — the trimmed harness had dropped the fragment's own `Family` branch.
  Re-running the complete code gave `M_Supply Diffuser: 5`, correctly. **When a harness paraphrases the
  fragment instead of pasting it, a failure is at least as likely to be in the harness — re-run the real
  thing before reporting a bug.**
- 2026-08-06 — First three `actions/reporting/` verified by **composing them after a filter**, which is
  how they are meant to be used: `action-count-and-report` (with the `preferredParamName` reorder the
  header warns about), `action-count-by-group`, `action-report-location` (all three location branches,
  and the `maxRows` truncation notice). Two actions were also chained after a single filter with no
  variable collision. Count **131 (49%)**.
- 2026-08-06 — **The Open items list had gone stale in two ways, and one of them was a counting bug.**
  Item 1 under "needs Ajmal's machine" *began with the word DONE* and said "nothing outstanding here" —
  but `brain-status` counts list entries, not their contents, so a finished job was reported as open for
  two days. Rewritten as a standing task outside the numbered list; that bucket now reads 0. **A list
  whose items are counted must not contain finished items, however clearly they are labelled.**
- 2026-08-06 — The other staleness: the fixture-blocked bucket still named worksharing, Design Options,
  links and insulation as blockers **after they had been cleared the same day**. Most were unblocked by
  *building the fixture* rather than waiting for a project model, and the rest by create-then-rollback,
  which makes a fixture unnecessary for anything the API can create. Genuinely still blocked: Ceilings,
  electrical, a CAD import, flip-capable and sleeve families, a nested shared family, a Scope Box (no
  API), and the PDF print go-ahead. **The lesson is the general one this repo keeps relearning: a
  blocker list is only useful if it is edited on the day the blocker clears**, otherwise it quietly
  argues against work that is already possible.
- 2026-08-06 — Also refreshed today: the knowledge graph (`graphify-out/`, 849 → **955 nodes**, 1,085
  edges) and its Obsidian vault (1,145 → **1,257 notes**), both of which had been stale since
  2026-08-04. Worth noting for anyone rebuilding: graphify's `--update` needs `build_merge` to fold the
  new extraction into the existing graph — building from the changed subset alone produced 307 nodes and
  its shrink-guard correctly refused to overwrite 849. **The guard caught an operator error, which is
  exactly what it is for.**
- 2026-08-07 — New knowledge file `live-model/geometry-and-transforms.md`: transforms that silently do
  NOTHING while reporting success. `MoveElement` and `RotateElement` return normally and change nothing
  on a pinned element or group member; `CopyElement` on the same elements works fine — so "it's grouped"
  is not a blanket answer. Also records why mirroring connected MEP in place re-fits instead of
  reflecting. Found while verifying `action-find-duplicates.cs`, whose zero refused to flip.
- 2026-08-07 — Verification pass, actions/reporting + qa-checks + move-copy-rotate: **3 real bugs fixed**
  — `action-move-elements.cs` and `action-rotate-elements.cs` both counted "the API didn't throw" as
  "it worked" (they reported moving/rotating 5 grouped terminals that never budged), and
  `action-report-parameters.cs` printed the same empty cell for a parameter that is blank and one that
  does not exist. All three now verify against the model itself. `action-flip-elements.cs` and
  `action-report-element-ownership.cs` came off the blocked list — the fixture now has a door and is
  workshared, so both positive paths ran for the first time.
- 2026-08-07 — Second half of the verification pass (visibility, colour-graphics, parameters-naming,
  structural-changes, prelude). Two more bug classes fixed: `SetCategoryOverrides` silently discards the
  cut line and surface fill on a non-cuttable category (Ducts/Pipes/Air Terminals), and **`Parameter.Set()`
  returns a bool that four fragments were throwing away** — Revit refuses a value by returning false, not
  by throwing, so `Set(0.0)` on a duct's Width reported success and changed nothing. `lib/prelude.cs`
  finally ran: every helper PASSed via `examples/prelude-smoke-test.cs`, after 3 days unproven.
- 2026-08-07 — `action-duplicate-view-template.cs` rewritten: it could never have worked (its own
  `CanViewBeDuplicated` guard returns false for every template, and bypassing it throws). The working
  technique — `ElementTransformUtils.CopyElements` plus a second transaction for the rename, because
  Revit auto-suffixes the copy — **was already written down in `live-model/views.md` on 2026-08-01 and I
  re-derived a worse one before finding it.** Standing lesson, and the reason `ask-brain-hybrid` exists:
  search the Brain before writing C#, including when you are only "fixing" a fragment.
- 2026-08-07 — **Adversarial code-read of the 40 still-unverified fragments** (11 parallel readers, no
  bridge access — the bridge takes one connection at a time, so all live runs stayed serial in the main
  thread). They were given the 6 bug shapes already found this session and asked to find the same shapes
  by reading. Result: 40 planned, 32 runnable against this fixture, 1 genuinely blocked, and the leads
  below. **These are code-reading suspicions, NOT confirmed bugs** — three were checked live the same day
  and one of those (viewport stacking) was real and is now fixed. Treat the rest as a work list, and
  confirm each against the model before believing it.

  - `create-duct.cs` — BUG SHAPE 2 (Set's bool ignored) — lines 55, 60, 65: 'if (pW != null && !pW.
  - `create-pipe.cs` — BUG SHAPE 2 — line 50: 'if (pD != null && !pD.
  - `create-cable-tray.cs` — BUG SHAPE 2 + BUG SHAPE 4, and this is the WORST of the four MEP creators.
  - `create-conduit.cs` — BUG SHAPE 2, in its reporting form.
  - `create-mep-system-type.cs` — BUG SHAPE 2 — line 72: 'if (pAbbr != null && !pAbbr.
  - `create-hvac-zone.cs` — BUG SHAPE 1 and BUG SHAPE 3, both real.
  - `create-room.cs` — BUG SHAPE 2 and BUG SHAPE 4 together, on one line each.
  - `create-text-note.cs` — **[checked live 2026-08-07]** 1) Z is hardcoded to model 0 — line 56, `UnitUtils.
  - `create-view.cs` — 1) Section mode never builds a Transform — lines 65-69 set only `Min`/`Max` on a fresh `BoundingBoxXYZ`, whose Transform is Identity.
  - `create-callout-view.cs` — 1) Bug shape 2, textbook.
  - `create-room-elevations.cs` — 1) The Elevation ViewFamilyType is picked by `FirstOrDefault(v => v.
  - `create-key-schedule.cs` — 1) Bug shape 1.
  - `create-revision-cloud.cs` — 1) The input contradicts this repo's own knowledge/live-model/revisions.
  - `action-place-schedule-on-sheet.cs` — **[checked live 2026-08-07]** Shape #1 (call counted as work): line 52-53 `ScheduleSheetInstance.
  - `action-place-viewport-on-sheet.cs` — **[checked live 2026-08-07]** Shape #1: line 52-53 `Viewport.
  - `action-duplicate-sheet.cs` — Shape #3 (a write that half-succeeds while the header claims it is clean): lines 51-52 create the sheet BEFORE assigning the number, and the per-sheet catch at line 85-89 does `failed++` and prints the message but never `Document.
  - `action-manage-sheet-sets.cs` — Shape #1 + #2 (report built from inputs, not a read-back): line 89-91 `target.
  - `action-remove-tags.cs` — **[checked live 2026-08-07]** Shape #4 (lite): `skipped` is incremented from two places for different reasons — line 21 (not an IndependentTag, no note recorded) and line 30 (delete threw, note recorded) — and line 35's message offers both causes for a single number.
  - `action-add-aligned-dimensions.cs` — Shape #1 (the API did not throw counted as the work happening): lines 102-104 `var dim = Document.
  - `action-add-spot-elevations.cs` — Shape #1: line 88-89 `Document.
  - `create-material.cs` — **[checked live 2026-08-07]** 1) REAL, and it is a composition bug: the catch block does NOT reset `elements`.
  - `create-grid.cs` — **[checked live 2026-08-07]** 1) HEADLINE, bug shape 1/2: line 37, `try { grid.
  - `create-levels.cs` — 1) Bug shape 1: `sb.
  - `create-wall.cs` — 1) Bug shape 1, inverted into a FALSE FAILURE: the report on line 45 runs AFTER `t.
  - `create-floor.cs` — 1) Bug shape 1, inverted into a FALSE FAILURE: the report on line 71 runs AFTER `t.
  - `create-point-based-element.cs` — 1) A DOCUMENTED CROSS-REFERENCE THAT IS FALSE.
  - `load-family.cs` — 1) HEADLINE, and it breaks the fragment's own advertised GOTCHA.
  - `action-export-ifc.cs` — 1) Bug shape #1 (partial): line 46-50 `bool ok = Document.
  - `action-export-nwc.cs` — 1) Bug shape #1, the clearest instance in this batch: lines 42-43 — `Document.
  - `action-export-parameters-to-csv.cs` — 1) Bug shape #4, verbatim: line 36 `if (p == null || !p.
  - `action-export-sheets-to-pdf.cs` — 1) Bug shape #5, the strongest finding here and NOT flagged in the header: lines 68-77 mutate PrintManager with no transaction and no restore.
  - `action-export-view-image.cs` — 1) Bug shape #1: lines 45-46 — `Document.
  - `action-export-views-to-dwg.cs` — 1) Bug shape #1, concrete and quotable — lines 53-54: `Document.
  - `action-report-view-template-status.cs` — **[checked live 2026-08-07]** 1.
  - `action-report-schedule-fields.cs` — **[checked live 2026-08-07]** 1.
  - `action-report-sheet-title-blocks.cs` — **[checked live 2026-08-07]** 1.
  - `action-duplicate-view-template.cs` — **[checked live 2026-08-07]** 1.
  - `action-duplicate-views.cs` — **[checked live 2026-08-07]** 1.
  - `action-apply-view-template.cs` — **[checked live 2026-08-07]** 1.
  - `action-remove-view-template.cs` — **[checked live 2026-08-07]** 1.
- 2026-08-07 — All four MEP creators fixed: an MEP size can be **accepted AND changed**. `Set()` returns
  true and Revit snaps the value to the type's size table — a pipe asked for 77 mm came out 80 mm — so
  honouring the bool is necessary but not sufficient; only a read-back tells you what the model holds,
  and it must come **after `Document.Regenerate()`** because straight after `Set()` the parameter still
  echoes the request. Filed in `live-model/core.md` beside the `Parameter.Set()` bool note.
- 2026-08-07 — Housekeeping worth knowing: `brain-status` reads a row's "NOT yet live-verified (date)"
  clause as authoritative even when a later "verified 2026-08-07" appears in the same row, so four
  fragments stayed uncounted until the stale clause was removed. **When recording a verification, delete
  the old not-verified claim rather than appending past it.**
- 2026-08-07 — `create-view.cs` section mode fixed: a `BoundingBoxXYZ` starts with an IDENTITY Transform
  and `CreateSection` reads the look direction from its `BasisZ`, so every "section" this produced looked
  straight DOWN — `ViewDirection (0,0,-1)`, a plan-shaped cut — while reporting success. **`views.md` had
  specified the required right-handed basis since before this session; the fragment just never built it.**
  Second time today the Brain already held the answer (see the `duplicate-view-template` entry). The
  standing lesson stands: search the Brain before writing or fixing C#.
- 2026-08-07 — Verification housekeeping worth knowing: **a rolled-back TransactionGroup does not always
  leave the element table byte-identical.** After a `ChangeTypeId` run on 8 walls that was fully rolled
  back (types and thicknesses confirmed restored), 8 new elements remained on the **"Reviewable Warnings"**
  workset — no category, no geometry, no level, invisible in every view, and not referenced by any entry
  in `Document.GetWarnings()`. Revit's own warning bookkeeping, not model content. Likewise, activating
  the default `{3D}` view in a workshared model creates a permanent per-user `{3D - username}` view plus
  ~9 dependents, and that is NOT undone by a rollback either (same class as the active-view switch noted
  in `action-section-box-and-zoom.cs`). **So judge "the model is unchanged" on category counts, parameter
  values and geometry — not on the highest ElementId, which drifts upward from bookkeeping alone.**
- 2026-08-07 — Campaign closed for this session at **223/267 verified (51% -> 84%)**, 16 real bugs found
  and fixed, all one family: a fragment reporting success for work that did not happen. Open items above
  rewritten grouped by what unblocks each, not by folder. Ajmal's instruction for the remainder: **do the
  rest during real work, and mark a fragment verified the first time it genuinely works on a live job** —
  which is why the big recipes are deliberately left rather than force-tested against a fixture.
  One tooling note worth keeping: the first rewrite of the Open items section used `###` headings and
  bullets, and `brain-status.mjs` silently reported NO open items, because it counts `**bold**` headings
  with `1.` numbered entries. Fixed by matching the format. **The status tool's silence is not the same
  as zero** — if a section suddenly reads empty, suspect the format before believing the number.
- 2026-08-07 — End-of-session sync of every derived layer, after the verification campaign. All three are
  now current and cross-checked against disk: **semantic index 309/309 content files** (verified live —
  "why did my move do nothing on grouped elements" returns the new `geometry-and-transforms.md` at #1 by
  BOTH meaning and words), **graphify graph 937 nodes / 975 edges**, health OK, no dangling or collapsed
  edges, and **Obsidian vault 1251 notes**. Zero files missing from either index.
  Two things worth keeping about the graphify `--update`:
  (1) The merge came out SMALLER than the previous graph (955 -> 937 nodes) and the #479 shrink-guard
      correctly refused to write. **Diagnose before forcing.** Per-file comparison showed it was
      re-extraction variance on 4 doc files (`brain-log` 32->29, `core` 24->20, `live-model/README` 12->4,
      `scripts/README` 39->28), offset by the genuinely new `geometry-and-transforms.md` going 0 -> 7.
      No file lost its representation, so `force=True` was the right call — but only *after* the diff
      explained the number. The guard exists to stop an UNdiagnosed shrink.
  (2) `graphify-out/` is **gitignored**. The graph and vault do NOT travel through git — they travel only
      when the FOLDER is copied. If the Brain is ever cloned rather than copied, regenerate them with
      `/graphify . --update` on the new machine.
- 2026-08-09 — Whole-Brain health check, three fixes out of it. (1) **The semantic index now covers
  `mcp-server/tools/README.md`** (310 files, 2,777 chunks) — before this, "how do I count elements
  without writing C#" could only ever land on a C# fragment, because the one doc naming the 17 native
  tools was outside the index; verified live, it now returns at #1 by BOTH meaning and words. (2) The
  "searches all N files" claim in CLAUDE.md/START-HERE.md/README.md had silently drifted 306-vs-309 —
  the doc-drift failure mode again, so it is now **check 8 in both verify-consistency checkers**: the
  claimed number is recomputed from disk on every run. (3) `fragment-compile-failures.txt` at the root
  was STALE — written 23:27 on 2026-08-04, four minutes before the commit that fixed its one failure.
  Re-ran the compile check filtered to `examples/prelude-smoke-test.cs` (with `-RevitPath` pointed at
  the real `Revit 2020` folder — auto-detect grabs `Revit Model Review 2020` first): PASS, so the file
  described a failure that no longer existed. Removed it, and the checker now deletes a stale report
  itself after any clean FULL run (a filtered run leaves it alone — it only proved a subset). Also
  re-synced every derived layer after the doc edits: graph 947 nodes / 985 edges, health OK; vault
  1,265 notes; knowledge routing audited — every knowledge file is reachable from INDEX.md or the
  live-model sub-index.
- 2026-08-10 — New standing habit, stated by the user directly: *"from now if I say something you have to
  remember okkey this is my normal work and you have to remember the words am using."* Routed per
  `brain-self-maintain` Step 1 as a habit that applies to **every** task, so it went to `START-HERE.md`
  § "This Brain improves itself" (fourth bullet) rather than into a skill, and `glossary.md`'s remit was
  widened in the same turn: it now records his working vocabulary **as he says it**, instead of only
  terms that already caused a misunderstanding. The two are the same rule seen from both ends — the habit
  says *capture the words*, the glossary is *where they go*. Also fixed the stale "Two habits" lead-in in
  START-HERE.md, which had said two while listing three.
- 2026-08-10 — "Grayout for MEP" started being taught, and its first two steps are recorded in
  `glossary.md` (marked candidate until he confirms the numbering). Step 1: all model categories ON,
  Structural Rebar + Rebar Couplers OFF. Step 2: every model category to RGB 150,150,150 on projection
  line, cut line, surface pattern and cut pattern, pattern `<Solid fill>`. Both run live on view
  "1 - Mech". **The real find is in `live-model/graphic-override-precedence.md`**: sweeping one category
  override across all 85 controllable model categories and reading every one back gave 24 fully applied
  and 61 partial, which both extends and *corrects* the 2026-08-07 note there. `IsCuttable == false` does
  predict the cut half being dropped — but nothing predicts the fill being dropped, because Railings and
  five structural-reinforcement categories are `IsCuttable == true` and still lose both fills. Also
  recorded: MEP greys as **lines only** by category override, and five categories (Rooms, Areas, Spaces,
  Raster Images, Point Clouds) take no category override at all. Worth having because it means a whole-view
  grey-out cannot be reported as "all grey with solid fill" without lying about the services.
- 2026-08-10 — **"Grayout for MEP" finished being taught and became the tenth skill.** He dictated it over
  eleven turns against a live view, correcting me four times (windows must not take the wall's fill colour
  as their line; grey *everything* first including MEP, not just the background; patterns are 200 while
  lines are 150; insulation must not outdraw the duct it wraps), then said to keep it permanently —
  *"if i need to do in anothor model i will tell you only that grayout for mep so the all same work need
  todo."* Captured as [`skills/ajtools-mep-grayout/SKILL.md`](../skills/ajtools-mep-grayout/SKILL.md) plus
  [`scripts/recipes/mep-grayout.cs`](../scripts/recipes/mep-grayout.cs), built from a **read-back of the
  finished view** rather than from my notes of the conversation, so it reproduces what is actually on the
  model. The glossary entry had grown to 163 lines carrying the whole spec — now an 11-line pointer, which
  also brought `glossary.md` from 307 back to 163 and under the split rule. Two things deliberately left
  open in the skill rather than decided for him: Duct Linings (asked twice, unanswered) and whether service
  *sub-categories* should follow their parent to black — they are still background grey because the black
  pass ran after the sub-category pass, and the recipe reproduces that faithfully behind a toggle rather
  than silently "fixing" it. The consistency hook caught all eleven drift points from adding a skill and a
  recipe (README tables, three "9 skills" counts, AGENT-SPEC's 268/21, three index-count claims) — exactly
  the doc-drift failure mode this repo keeps hitting, caught mechanically this time instead of by reading.
- 2026-08-10 — **Pipe and electrical connectors proven in the Family Editor; new recipe
  `recipes/create-equipment-family-from-datasheet.cs`** (268 fragments, 21 recipes). Built the Condair
  EL 20-400V/3~ steam humidifier live from a PDF datasheet — 530×406×780 cabinet, five connectors
  (steam / supply water / drain / condensate / 400 V 3-phase), a toggleable clearance zone on its own
  subcategory, 63 parameters — resize-tested at 700×500×1000 and reset clean. Six new facts in
  `live-model/families.md` § Fourth build: `CreatePipeConnector`/`CreateElectricalConnector` work exactly
  like the duct one; pipe connectors have the **same** size-not-inherited-from-the-face bug (default 2 ft
  diameter) and the fix is to associate `CONNECTOR_DIAMETER` to an OD parameter; `NewExtrusion` takes a
  **negative** end and extrudes downward; a Length parameter accepts a **negative formula result** and can
  drive `EXTRUSION_START_PARAM`; family *type* names reject `~` (parameter names accept `/`); and
  reflection over `typeof(...).GetMethods()` is a faster, read-only replacement for the deliberate-
  compile-error signature hunt.
- 2026-08-10 — **`Document.SaveAs`/`Save` are BLOCKED through the bridge**, so
  `commands/command-compact-save.cs` is blocked rather than merely unverified. SaveAs throws *"not
  permitted when there is any open transaction phase started by API client"* while
  `Document.IsModifiable` reads **False** — the bridge holds a `TransactionGroup`, not a `Transaction`,
  so `IsModifiable` is not a valid test for it. A family build ends by handing the user the exact folder
  and filename for File → Save As.
- 2026-08-10 — `glossary.md`: loadable families in the office library use the **`TRG_`** prefix
  (`TRG_<TYPE>_<Description>_<Model>.rfa`); the standing "office prefix is `MEP_`, never TRG" rule covers
  **line styles only**. Two namespaces, not a contradiction. Standing habit: list the destination folder
  before naming a family — the house convention beats the generic ISO 19650 element name.
- 2026-08-11 — **Corrected the Condair humidifier family against the full 107-page submittal; new
  knowledge file [`reading-manufacturer-datasheets.md`](reading-manufacturer-datasheets.md).** The
  family had been built from a screenshot of one data-sheet page. The shop drawings showed four of the
  five connections were on the wrong face at invented coordinates, that there are **two** ø8 condensate
  ports (on the TOP, not the bottom), and that the unit has **two** electrical supplies (heating
  400V/3~ and a control 230V/1~ that appears only in the project schedule, never on the manufacturer
  page). Standing rule now recorded: **the data sheet gives connection SIZES, the shop drawing gives
  POSITIONS** — if you only have the data sheet, say the positions are unknown and ask. Also recorded:
  manufacturers group a range into a few housings (Condair EL: S = EL 5/8/10/15, M = EL 20/24/30/35/40/45)
  which share a cabinet AND connection positions, so the right structure is **one family per housing,
  types per capacity** — not one stretched parametric family.
- 2026-08-11 — PDF tool chain for this machine, since poppler/`pdftoppm` is absent and the Read tool
  cannot render PDFs: `pdftotext` ships inside Git for Windows (`mingw64\bin`, already on PATH in Bash),
  and `uv run --with pymupdf` renders pages/crops with nothing installed system-wide. Shop drawings are
  raster, so `page.get_drawings()` returns 0 — dimensions have to be read off a high-DPI crop, not
  measured programmatically. **`pdftotext -layout` silently shifted the left column of a two-column spec
  table up by one row** (each value belonged to the label on the NEXT line) while leaving the right
  column correct — sanity-check extracted spec tables against a rendered image before trusting them.
- 2026-08-11 — Two more Family Editor API facts in `live-model/families.md` § Fourth build: unit
  suffixes are legal inside formulas (`"Height + 60 mm"`), and `EXTRUSION_START_PARAM` and
  `EXTRUSION_END_PARAM` can both be associated to family parameters, so a stub keeps a fixed overlap
  into the body through a resize. `create-equipment-family-from-datasheet.cs` updated to match, and its
  INPUTS block now carries the real dimensioned Condair values instead of the guessed ones.
- 2026-08-11 — **Locking a manufacturer family to its product size**: `SetFormula(p, "530 mm")` on the
  driving dimensions greys them out in the UI so nobody can resize a purchased unit off-product. Verified
  it is safe to do late: a formula-locked parameter still works as a `Dimension.FamilyLabel` AND as an
  `AssociateElementParameterToFamilyParameter` target — after locking Width/Depth/Height the body stayed
  530x406x780, all nine dependent formulas resolved, all six connectors held. Recorded in
  `live-model/families.md` § Fourth build; `create-equipment-family-from-datasheet.cs` gained a
  `lockToProductSize` input. Reverse with `SetFormula(p, null)`.
- 2026-08-11 — **Family rebuilt to Ajmal's four structural corrections** (his words: *"the back and
  front you make side one side only, you did not make it equal"* / *"each extruction you can make the
  sub categroy"* / *"make all the sides referance lines ... but top is not there"* / *"make all the
  parameters ane type parameters not isntand"*). Four new API facts in `live-model/families.md`
  § Fourth build: **`FamilyManager.MakeType`/`MakeInstance` flip a parameter's scope safely** — the whole
  `ReplaceParameter` corruption trap from the third build is avoidable for a scope change, and an
  association to `IS_VISIBLE_PARAM` survived the flip; a **horizontal reference plane needs cut vector
  `XYZ.BasisY` and must be created in an ELEVATION view**, as must its `NewAlignment` and the vertical
  `Height` dimension; **aligning the top face to a "Unit Top" plane + a labelled dimension is an
  alternative to associating `EXTRUSION_END_PARAM`, never both**; and **unlock formula-locked driving
  dimensions before rebuilding geometry they constrain**. Standing preferences recorded: EQ-centre both
  plan axes by default, and give every extrusion its own subcategory.
- 2026-08-11 — `create-equipment-family-from-datasheet.cs`: `backAtOrigin` now defaults to **false**
  (EQ both axes), and the header carries a TODO for the two things the hand-rebuild added that the
  recipe still lacks — the top reference plane and per-part subcategories.
- 2026-08-11 — **Work planes: `SketchPlane.Create(doc, plane)` was producing `<not associated>` on every
  extrusion.** Ajmal spotted it in the properties palette. Root cause and six new facts now in
  `live-model/families.md` § Fourth build: the `Plane` overload never hosts — use
  `SketchPlane.Create(Document, ElementId datumId)`; it **cannot** be fixed afterwards (`Sketch.SketchPlane`
  has no setter, `SKETCH_PLANE_PARAM` is read-only) so the geometry must be rebuilt; unused SketchPlanes
  are auto-purged between `run_csharp` calls, so create them in the same transaction as their extrusion;
  plane-to-plane labelled dimensions SURVIVE deleting all geometry and silently duplicate on a rebuild;
  a horizontal reference plane's normal follows endpoint order (swap to flip); and a single full circle
  is accepted but Revit normalises it to two arcs regardless.
- 2026-08-11 — **NEGATIVE RESULT: `NewDiameterDimension` on an extruded cylinder does NOT make the
  circle parametric, and plants a modal "Constraints are not satisfied" error.** All seven calls
  succeeded and accepted a `FamilyLabel`, but changing the parameter moved the connector and left the
  geometry at its old size, then errored on regeneration. Parameters were not reporting
  (`IsReporting == false`) — the dimension binds to the derived solid face, not the sketch curve. Revit
  2020 has no API route to dimension a sketch curve post-creation (`SketchEditScope` is 2022+). All seven
  removed. For API-built round stubs: drive the CONNECTOR from the OD parameter and leave the drawn
  cylinder fixed, or add the diameter dimension by hand in the Family Editor.
- 2026-08-11 — **CORRECTION to yesterday's "saving is blocked through the bridge".** It is blocked only
  for the document open in Revit's UI. `Application.NewFamilyDocument(template)` returns a document whose
  `SaveAs` **succeeds** — so a family can now be authored end-to-end with no user interaction. Proven by
  building the Condair EL 8-400V/3~ family (housing S, 420x370x670) from an empty template to a saved
  .rfa in six scripted steps: 68 parameters, 2 types, 7 subcategories, 13 reference planes, 9 extrusions,
  6 connectors, clearance zone — 40/40 verification, 0 Revit warnings. `command-compact-save.cs`,
  `scripts/README.md` and `families.md` all corrected; the fragment stays blocked for its own use case
  (the UI document) but is no longer a dead end for the general problem.
- 2026-08-11 — Two more Family Editor facts: **`nd.ActiveView` is null on an API-created document** (no
  UI window, so the user cannot see or hand-save it — save it from script first, then they open the
  file); and **`FamilyManager.CurrentType = ft` is a document modification needing an open Transaction** —
  to read another type's values use `familyType.AsDouble(param)` directly, no switching and no
  transaction.
- 2026-08-11 — Clearance zones restructured in both humidifier families on Ajmal's instruction
  (*"for the clearance you can make the separate refarance line i think that is better"*): each clearance
  face now has its own named reference plane (Left/Right/Front/Ceiling/Floor) dimensioned **directly off
  the matching cabinet plane** with the datasheet parameter as the label, instead of centre-based
  `Width / 2 + Left Clearance` formulas. Five derived formula parameters deleted as dead weight. Checked
  against the submittal: it carries no clearance figure at all, so the five data-sheet numbers are the
  only source and left/right follows the standard "as you face the unit" convention — unconfirmed either
  way, flagged to Ajmal.
- 2026-08-11 — **New voice layer, `tools/voice/`** (Ajmal: *"i need also one jarvis or some voice mode
  also need to come that what is the ai is doing in short reply in voice"*). Speaks a short line for
  every action, in two British neural voices split by role: Ryan (Claude Code side) says the intent
  before acting, Sonia (the AJ Tools add-in, new `AiVoiceService.cs`) says the result Revit returned.
  Both write into one shared queue in `%LOCALAPPDATA%\AJTools\voice\` — outside this repo, so generated
  audio never travels with the Brain and neither side needs to know where the other is installed — and a
  single warm `drainer.py` speaks them strictly in order, which is what stops the voices overlapping.
  Falls back to the built-in Windows voice with no internet and no Python package. Four hooks added to
  `.claude/settings.json`; `voice.cmd off` silences it. Wording is regression-tested without sound by
  `node tools/voice/test-narration.mjs`.
- 2026-08-11 — **"Running script" told Ajmal nothing, so the voice now works the job out of the code.**
  He asked for a colour change and heard only *"Running script"* — because native tools carry their meaning
  in the tool name (`count_elements` → "Counting air terminals") while a `run_csharp` call carries nothing
  unless someone wrote a comment. Two fixes, deliberately both: `narrate-hook.mjs` now infers the action
  from the API calls in the script (`OverrideGraphicSettings` → "Changing colour", `Document.Delete` →
  "Deleting elements", `Duct.Create` → "Drawing duct"), ordered most-specific-first so the
  `FilteredElementCollector` that appears in almost every script cannot win and describe nothing; and
  `scripts/README.md` now requires a plain-English `//` line at the top of every `run_csharp` call, which is
  what the voice prefers when present. **The fallback names the operation, the comment names the intent —
  "Changing colour" is not "Colour the supply ducts blue", so the comment is still the job.** Also fixed:
  the old reader searched the whole script for any `//` line and would announce a note from the middle of a
  fifty-line script as if it were the purpose; it now reads only the first four lines and ignores `====`
  separators. Six new cases in `test-narration.mjs` cover the no-comment paths.
- 2026-08-11 — **The second voice was DELETED the same day it started working** (Ajmal: *"totally remove
  that female voice feature, only men voice … remove everything, even the code also related to this"*).
  The two-voice design assumed they carried different news — intent versus result — but the assistant
  already announces the job and reads the answer at the end, so the add-in's voice was a second person
  confirming something you had just been told. **A second voice earns its place only when it says
  something the first cannot.** Gone from both repos: `AiVoiceService.cs` deleted, `McpBridgeService`
  v1.10.0 no longer calls it, AJ Tools suite bumped to v1.42.0; on this side the `revit` profile, the
  dual-queue read, the add-in lock publishing and the `revitvoice` command are all removed. **One rebuild
  of the add-in is still needed before Revit actually stops speaking** — the deleted code is inside the
  currently loaded DLL. Accepted trade-off: no per-action result mid-job, only the plan and the total.
  **Two lessons, both paid for the hard way:**
  1. **A toggle was built first and it was the wrong answer.** An off-by-default switch shipped an hour
     before he asked for outright removal. *A feature nobody wants is not improved by making it
     optional* — it leaves dead code, a switch to document, and one more thing that can break. When the
     ask is "I don't want this", offer removal first.
  2. **The first mute passed its own unit test and silenced nothing.** It dropped the add-in's lines out
     of the shared queue — but the add-in only queues when it can see a live speaker and otherwise speaks
     straight through Windows, and the test never covered that fallback. Ajmal reporting "still the female
     voice is there" is what caught it. *Verify a cross-component switch against the fallback path, not
     the happy path — the fallback is where an "off" switch goes to die.*
- 2026-08-11 — **The assistant's voice was structurally impossible, and the sandbox is why.** Ajmal
  reported hearing the Revit voice but never the assistant's. Cause: the spoken-line queue lived in
  `%LOCALAPPDATA%\AJTools\voice\`, and **Claude Code writes any path outside the project folder into a
  throwaway overlay** — so every line the hooks ever queued went into a folder that does not exist.
  Proven with one probe written to both places in the same call: the `%LOCALAPPDATA%` copy was reported
  written and was absent from the real disk; the copy inside `D:\Ajmal\AJ AI Brain\` survived. The Revit
  add-in was heard because it is a real process on the real disk. **Fix:** the Brain's queue, cache and
  log moved to a gitignored `.voice-runtime/` inside the repo, and `drainer.py` now reads **both**
  queues (the add-in's stays in `%LOCALAPPDATA%`, since moving it means recompiling and closing the
  model). Order survives the split because filenames carry a millisecond timestamp. The drainer also
  publishes its lock into the add-in's folder so `AiVoiceService` stops falling back to the robotic
  Windows voice. **Verified end to end the same day: first cached MP3 ever produced on this machine
  (21,600 bytes).** Lesson: *the portability argument for putting runtime state outside the repo was
  right in principle and fatal in practice — a location a component cannot actually write to is not a
  location.*
- 2026-08-11 — **An MCP shell is NOT a safe way around the sandbox — it gave the same false filesystem
  view.** Chasing the voice bug, a Windows-MCP PowerShell reported `voice.log` absent and the audio cache
  empty, while a Python process launched from that same shell, running as the same user with the identical
  `LOCALAPPDATA`, opened that log and read twelve lines out of it. Claude Code's own `Glob` agreed with
  PowerShell and was equally wrong. Both were reading a redirected copy of
  `%LOCALAPPDATA%\AJTools\voice`. Two hours of this session went into "the drainer never runs" — a
  conclusion built entirely on a directory listing that was fiction; the drainer had been running the
  whole time. **The only readings that proved true came from the program that actually does the work.**
  So: when a filesystem answer decides what you do next, get it from inside the process that owns the
  file, or from Ajmal's own terminal — and do not treat an MCP shell as the escape hatch. The note under
  the original sandbox entry below has been corrected accordingly.
- 2026-08-11 — **A black console window appeared over Revit, and it was the voice.** `say.mjs` launched
  `python.exe` (the console interpreter) with `detached: true`; on Windows a detached child is handed its
  own console, and `windowsHide` does not reliably suppress it for a venv launcher shim, which
  re-executes the base interpreter as a second process that never saw the flag. Switched to the venv's
  `pythonw.exe`. **The comment in the file justifying python.exe was wrong** — it claimed the venv's
  pythonw "silently refuses to execute a script"; retested, it runs `-c` code, runs `drainer.py`, and
  writes files. A misdiagnosis recorded as settled fact cost the thing it was written to protect: a
  console covering the model defeats the entire purpose of a voice you use so you can watch the model.
- 2026-08-11 — **The voice had never spoken once, and the guard meant to protect it was the reason.**
  `say.mjs` read "queue deeper than 8 lines with no drainer holding the lock" as proof that starting the
  speaker was futile, and stopped trying. It is the opposite: it means the speaker is **dead**. Since the
  queue only empties when a drainer runs, and a drainer was only started when the queue was shallow, once
  it passed eight lines nothing could ever start again — a deadlock that had held for 15 hours and 139
  unspoken lines. It survived every restart (the jam is a folder on disk, not a process) and emitted no
  error anywhere, because failing to speak is indistinguishable from having nothing to say. Fixed: a deep
  queue with no lock now drops the stale backlog and starts the speaker, and a **30-second cooldown** —
  not the queue depth — is what stops an impossible spawn from retrying on every line. **Lesson worth
  more than the fix: a guard whose trigger condition can only be cleared by the thing it blocks is a
  deadlock, not a safety net.**
- 2026-08-11 — **The voice now speaks only what touches the model** (Ajmal: *"no too much reply, main
  things only like caveman"*). Counted on one real session, the old every-tool narration spoke 22 lines
  and **not one was about the model** — it read out file names, grep patterns, and at one point its own
  narration. New `speakOnly: "revit"` in `voice-config.json` draws the line at the bridge; `"all"` restores
  the old behaviour. Wording is caveman (filler stripped, capped at `maxWords`), with two deliberate
  exceptions: delete/move speak the element count when the call names exact Ids, and the closing summary
  gets 16 words because it carries the answer. **The filler-stripping went into `drainer.py`, the one
  process both voices pass through — which shortened the Revit add-in's voice with no C# change and no
  Revit restart.** Regression-tested without sound: `node tools/voice/test-narration.mjs` now checks the
  speak/stay-silent decision as well as the wording, across both stages.
- 2026-08-11 — **Claude Code's Bash and PowerShell tools are sandboxed, and this makes filesystem
  results untrustworthy.** A process they spawn writes into a throwaway overlay: Python reported a file
  written and `os.path.exists` returned True, while the same shell session's `ls` could not see it, and
  the real disk had nothing. Hours went into "debugging" a voice drainer that was working — the evidence
  was fake. **Verify anything filesystem-dependent from a real terminal**; treat a sandboxed tool's file
  listing as unproven. (**Corrected 2026-08-11:** this originally also offered "or through an MCP tool
  that runs outside the sandbox" as a safe alternative. It is not — see the entry below; an MCP shell
  was caught giving the same false view of the same folder.) Same session:
  `tools/verify-consistency.mjs` now survives a file vanishing between listing and reading, which is a
  real race for any transient file, not just the one that exposed it.
- 2026-08-11 — **Third cause of a blank parameter column, recorded in `knowledge/live-model/core.md`:**
  the native `report_parameters` tool reads the INSTANCE only, so a correctly-named parameter that lives
  on the TYPE (door/window `Width` and `Height` on standard families) prints blank. Caught live asking
  for door sizes; `action-report-parameters.cs` with `includeTypeParameters` returned them immediately.
  Logged alongside it: never read a size off the type NAME (`30" x 80"`), which is human-typed and can
  disagree with the real values.
- 2026-08-13 — **Retrieval quality is measurable again: `semantic-index/score-brain.cmd`.** The
  2026-08-06 run — 24 questions, 13 right at #1 — recorded the *score* and threw the *questions* away,
  so the most useful measurement this Brain ever made could not be repeated, and every later change to
  the embedding model, the chunking or the files would have been made blind. Four questions were
  recoverable from this log and `semantic-index/README.md` and are seeded in
  `semantic-index/test-questions.md`; the rest are Ajmal's to write, because questions written by
  whoever is tuning the search get unconsciously shaped into ones it can already answer. The seed set is
  deliberately unrepresentative — three of the four are the documented *failures* — so its score is a
  regression guard, not a quality verdict, and is not comparable to 13/24. Found while building it:
  **a new `.md` file inside `semantic-index/` was silently gitignored** (`semantic-index/*` with
  `README.md` un-ignored by name only), so the test questions would have been written, used, and then
  lost the day the Brain was copied to another machine. `.gitignore` now un-ignores `*.md` there.
- 2026-08-13 — **[`glossary.md`](glossary.md) now displaces the answering skill, exactly as
  `brain-log.md` once did — and it is not discounted.** On its very first run the score card caught
  *"how many diffusers do I need in this room"* returning `glossary.md` at #1 and
  `ajtools-hvac-terminal-layout` at #2. `semantic-index/README.md` still claims that question ranks #1,
  written when it did — documentation quietly getting ahead of reality again. The cause is structural
  and already known: a file that maps the user's words to meanings **matches more questions the bigger
  it gets**, which is precisely why `brain-log.md`'s score was discounted to 0.85 on 2026-08-06. The
  glossary has the same property and never got the same treatment. Deliberately NOT fixed in the same
  turn: the fix changes ranking, and it now has a test that will prove whether it worked. Left as the
  first target for the accuracy pass, with the README's stale claim to be corrected alongside it.
- 2026-08-13 — **The semantic index now rebuilds itself; it can no longer go stale.** It was a snapshot
  that refreshed only when someone remembered `index-brain.cmd`, so any session that forgot left every
  later session searching an older copy of the Brain — answering confidently out of text that no longer
  existed. `tools/reindex-mark.mjs` (PostToolUse) writes a zero-byte flag and returns in ~0.15 s;
  `tools/reindex-run.mjs` (Stop) does **one** rebuild per turn rather than one per edited file, then
  clears the flag. Three decisions worth keeping: **(1)** neither calls `semantic-index/index-brain.cmd`
  — that wrapper ends with `pause`, so a hook calling it would block forever waiting for a keypress, and
  the same trap is now documented at the top of `score-brain.cmd`. **(2)** The marker deliberately does
  **not** parse the hook payload to check *which* file changed: a rebuild with nothing changed re-embeds
  nothing and costs ~3.5 s, while mis-parsing an undocumented payload would silently stop marking
  altogether — the exact failure this pair removes. Cheap-and-always-right beats
  clever-and-sometimes-off. **(3)** The marker runs *before* the consistency checker in the PostToolUse
  list, because the checker exits 2 on drift and the flag must be set regardless. Measured: ~0.15 s when
  there is nothing to do, ~3.5 s for a no-change rebuild, ~12 s after editing this file — 206 of the
  index's 2,984 chunks live in `brain-log.md` alone, so the README's "2.8 s" is the small-file case.
- 2026-08-13 — **Brain search is now an MCP tool, `search_brain`, so it works where `.cmd` files do not.**
  `semantic-index\ask-brain-hybrid.cmd` is a Windows batch file: on Claude Code for web, or any
  Linux/macOS container, it does not fail — it *silently does nothing*, the same shape of failure as the
  `.ps1` hook wrapper that let a whole session run unchecked on 2026-08-04. Four decisions worth keeping.
  **(1) It lives in `mcp-server/brain-tools/`, not `mcp-server/tools/`** — `tools/brain-status.mjs:90`
  counts every `.js` in `tools/` and reports the total as native **Revit** tools, so a non-Revit tool
  there would quietly turn a true number into a false one. Verified after wiring: still reports 17.
  **(2) It returns plain text, not `asToolResult`** — the search's own layout (ranks, `found by:
  meaning #3 + words #1`, `[PROVEN]`, the STALE banner) *is* the payload, and `JSON.stringify` would
  escape every newline and make the one thing the tool exists to show unreadable. Errors still go through
  `asToolResult` so the failure shape matches every other tool. **(3) It gets its own test in
  `mcp-server/test/smoke.test.js`** rather than joining the two lists there, which assert exactly 17
  registrations and that every handler fails with a bridge error — `search_brain` does neither, and
  that 17-guard is worth keeping intact. The test accepts *either* real results *or* a clean "no venv"
  error, because `semantic-index/venv/` is gitignored and a fresh clone has none; asserting only the
  first would make `npm test` fail on every fresh checkout. **(4)** Found while finishing it: the
  `STALE INDEX` banner told you to run a rebuild "(~90 s)". That is the *full*-rebuild figure, but the
  banner fires on ordinary staleness, which needs the 2–4 s incremental — it was quoting the right
  number for the wrong situation, and now names the automatic rebuild first.
- 2026-08-13 — **Every real question now searches this Brain before the assistant answers it.**
  `tools/auto-search-hook.mjs` (UserPromptSubmit) runs the search on what Ajmal typed and puts the top
  five hits into context *before* the message is read. Retrieval had been optional: nothing forced a
  search, so whether the Brain was consulted depended on remembering to run a command, and when it was
  forgotten the answer came from general Revit knowledge instead of 269 proven fragments. Two decisions.
  **(1) Gated, not sped up.** A search costs ~3.5 s because it loads the 166 MB model — nothing on a
  real question, pure waste on "ok". Short confirmations, slash commands and anything under four words
  are skipped; a warm search service was deliberately NOT built, because gating is the cheap fix and the
  expensive one should only be built if the delay is ever actually felt. **(2) A compact block, not the
  search output.** `semantic-index/brain_context.py` prints seven lines — path, area or PROVEN status,
  and `meaning#N words#N` — because the normal output carries a long snippet per hit and would bloat
  every message in the session. The `STALE INDEX` warning is carried inline, since the point of
  injecting this is that nobody has to go looking for a warning.
- 2026-08-13 — **First agent: `brain-librarian`, and one shared rules file.** `.claude/agents/` did not
  exist before today. The Librarian files what a session learned — checks it is not already written,
  routes it per `brain-self-maintain`, records Ajmal's own words in `glossary.md`, logs it here, and
  runs the consistency checker. It is given `Read/Write/Edit/Glob/Grep/Bash` and **no Revit tools at
  all**: there is one bridge and one open session, and an agent running a script while Ajmal works in
  the same model is two transactions fighting over his work. The boundary is enforced by what it is
  given, not by asking it nicely. `.claude/agents/brain-agent-rules.md` holds the rules every future
  agent must follow and deliberately **points at** `CLAUDE.md` / `START-HERE.md` rather than copying
  them — a second copy of a rule drifts from the original, which is this repo's recurring failure. Two
  agents from the first draft were dropped for being the wrong tool: a "capture" agent (writing one line
  to a file is a single tool call) and a "health check" agent (`brain-status.mjs` already does it and
  never forgets). The rule that produced both cuts: **if the job has no judgment in it, it is a hook or
  a script, never an agent.**
