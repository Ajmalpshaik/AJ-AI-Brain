# Brain — Change Log

Dated, short entries for changes to the Brain itself — a new skill created, a knowledge file split, a
script fragment added or retired, a technique that finally got solved. This is a record of the Brain's
own growth, separate from `live-model/log.md` (which is a record of what was done *to a Revit model*).

Add a line here whenever [`skills/brain-self-maintain/SKILL.md`](../skills/brain-self-maintain/SKILL.md)
creates, splits, or retires something, or whenever any other skill's "after finishing" step says to log
here.

**How long an entry should be — restated 2026-08-22, because the old rule was fiction.** It said *"keep
entries to 1–3 lines"*. Measured across all 288 entries: **median 8 lines, 87 of them over 10.** A rule
that 90% of a file breaks is not a standard, it is drift with a sentence on top. Ajmal's instruction the
same day: *"need full details keep."*

So the real rule: **write the length the finding deserves.** One line for a mechanical change (a fragment
renamed, a file split). Several paragraphs when the entry carries a *measurement, a reversal, or a trap* —
those are the entries a future session actually needs, and compressing them throws away the evidence that
makes them worth having. What does not belong here is anything already written elsewhere: the code is in
the fragment, the reasoning is in the knowledge note, the diff is in git. **Link to those, don't restate
them.**

(Entries before 2026-07-23 were compressed from long-form with the user's OK — git history has the original.)

**When this file gets too big — asked 2026-08-22, and the answer is not "shorten it".** Ajmal asked
whether the detail was worth keeping. It is: this file being detailed is what let the same session find a
Revit-2024 regression, by reading what a migration two days earlier had actually done. A 1–3 line log
could not have. But detail is not free — this file sits inside `knowledge/`, so **every line of it is
indexed and competes with the notes that answer real questions.** Measured that day: **10% of the whole
searchable corpus**, against the 20% that got 604 chunks of external standards reverted in an hour on
2026-08-13.

So the rule is **move, never shorten**: when `tools/brain-status.mjs` reports this file past **20% of the
corpus**, cut entries older than ~60 days into `docs/brain-log-archive.md` — `docs/` is outside
`INDEX_TARGETS`, so every word survives, git still has it, and it stops crowding the search. The share is
printed by `brain-status.mjs --full` every session, so nobody has to argue about it from memory again.

## Open items — the single current list (supersedes any "Next" list in older entries)

Rewritten 2026-08-07 at the end of the big verification campaign so the next session can resume without
re-deriving anything. Everything left is below, grouped by WHAT UNBLOCKS EACH — not by folder, because the
folder tells you nothing about whether you can act.

> **Never quote a verification count from this file.** It read *"237 of 280 (85%)"* until 2026-08-22, when
> the live figure was **241 of 290 (83%)** — stale in both halves, and stale in the flattering direction.
> `tools/brain-status.mjs` computes it from disk at every session start; that is the only number to repeat.
> What is durable below is *what is blocked and why*. The arithmetic is not, so it is no longer written here. Headings are bold and items numbered on purpose: `tools/brain-status.mjs`
counts them that way.

**Needs a live bridge + the current test model — just run them:**
1. `actions/structural-changes/action-place-accessory-on-run.cs` — ducts exist; create-then-rollback.
   Its METHOD is proven but the file as one uninterrupted run threw an unisolated null reference; read its
   STATUS block. Suspect: an element handle reused across a transaction boundary after `BreakCurve`.

2. `recipes/sprinkler-sidewall-layout.cs` — a placed Room is all it needs, and four exist. It is the last
   of the three that were listed here on 2026-08-20 as provable on the model as it stands:
   **`sprinkler-nfpa-grid.cs` and `sprinkler-compliance-audit.cs` were both closed the same day** — run
   live on all four rooms, hand-checked, 0 failures (see [2026-08-20](#2026-08-20) in the Log).
   `sprinkler-obstruction-survey.cs` and `sprinkler-place-heads.cs` went with them. Sidewall needs a
   corridor-shaped room to be a fair test; Room 4 (27,900 x 4,900 mm) is the obvious candidate.

3. `recipes/sprinkler-layout-options.cs` and `recipes/sprinkler-floor-scope.cs` (2026-08-20) — a placed
   Room and a Level are all they need, both of which exist. Run the options one on Room 4 alongside
   `sprinkler-nfpa-grid.cs` and check that the grid's single answer APPEARS in the options list: if it
   does not, one of the two is wrong, and finding that out costs one run.
4. `recipes/sprinkler-pipe-schedule-size.cs` (2026-08-20) — needs modelled sprinkler PIPE connected to
   heads, which this model does not have yet. The walk and the lookup can be exercised the moment any
   connected pipe run exists; a duct run would even prove the clustering half.

(**Everything else in this group is closed.** 2026-08-14: `action-add-aligned-dimensions.cs`,
`action-add-spot-elevations.cs`, `action-manage-sheet-sets.cs` and `action-add-remove-insulation.cs`
verified live — several of their predicted CHECKs turned out wrong, see the Log entry for that date.
`create-hvac-zone.cs` and `create-room-elevations.cs` were verified earlier the same day;
`slice-trunk-for-sizing.cs` and `color-isolate-select-by-size.cs` a few days before that.)

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
1. `actions/structural-changes/action-extract-cad-curves.cs` — a CAD import.
2. `actions/structural-changes/action-copy-from-link.cs` — the RVT link exists but has no elements.
3. `actions/parameters-naming/action-import-parameters-from-csv.cs` — a CSV.
4. The six `actions/sheets-views/action-export-*.cs` — a real export folder, plus IFC/NWC exporters
   installed. ALL SIX SHARE ONE SHAPE: they announce a written file without ever checking the disk, and
   `action-export-views-to-dwg.cs` additionally discards the bool `Document.Export` returns.

(`creators/load-family.cs` was item 1 here until 2026-08-14 — **closed**. It never needed Ajmal to supply
an `.rfa`: the stock library at `C:\ProgramData\Autodesk\RVT 2020\Libraries\US Metric\` ships thousands,
including 166 electrical. Its "KNOWN BUG" also turned out not to be real — see the Log entry.)

**Fixture-blocked — need model content that does not exist yet:**
1. A nested shared family — the positive path of `filters/by-relationship/filter-by-subcomponents.cs`.
2. A sleeve family — `recipes/place-sleeves-at-wall-penetrations.cs`.
3. `recipes/ray-trace-to-ceiling.cs` — **not really fixture-blocked: it needs Ajmal to draw ONE ceiling by
   hand**, ten seconds of work. `Ceiling.Create` does not exist before Revit 2022 (re-confirmed by
   reflection 2026-08-14: 0 overloads, and no ceiling method on `Document.Create`), so the API cannot
   build this one fixture. That makes it an ASK, not a wait. `creators/create-ceiling.cs` is not an open
   item at all — it is already written up as CONFIRMED IMPOSSIBLE and does the right thing.

4. **Structural framing and columns in the test model** — the positive path of
   `recipes/sprinkler-obstruction-survey.cs`, `recipes/sprinkler-obstruction-check.cs` and
   `recipes/sprinkler-adjust-for-obstructions.cs` (2026-08-20). All three will RUN today and report
   "nothing found", which proves only the empty branch. Beams and columns are buildable by API
   (`NewFamilyInstance` with a structural type), so this is a build-the-fixture job, not a wait — the same
   move that closed the electrical and insulation items. Add one beam and one column to Room 4 and all
   three get a real test, including the bay-module detection.
5. `recipes/sprinkler-deflector-height.cs` — needs **a ceiling**, exactly like `ray-trace-to-ceiling.cs`
   above, and blocked by the same Revit 2020 `Ceiling.Create` gap. Same ASK: one ceiling drawn by hand
   unblocks both. Its no-ceiling cases (2a exposed slab, 2b under a beam) can be proven without one.
6. `recipes/sprinkler-place-heads.cs` — needs **a sprinkler family loaded**. Not yet checked whether the
   model has one; the stock library ships them, so this is likely a `creators/load-family.cs` call rather
   than a real block. Check before assuming — "fixture-blocked" has been wrong four times in this Brain.

(Electrical content was item 1 here until 2026-08-14 — **closed by building the fixture**: two stock MEP
families loaded, a real panelled PowerCircuit created, `filter-by-electrical-system.cs` verified.)

**Big recipes — deliberately left to be proven the day they run on a real job:**
1. Verifying these verbatim costs far more than it returns against a test model, and they are idempotent.
   MARK THEM VERIFIED THE FIRST TIME THEY WORK ON REAL WORK — Ajmal's instruction, 2026-08-07:
   `recipes/create-mep-line-standards.cs` (385 lines, his office standard),
   `recipes/create-mep-text-standards.cs`, `recipes/tag-elements-in-active-view.cs`,
   `recipes/create-revisions-from-sheet-dates.cs`,
   `recipes/create-parametric-box-family-with-duct-connector.cs`, `recipes/build-test-fixtures.cs`.

**Standing task, not an open item:**
1. `tools\verify-fragments-compile.ps1` — 24 fragments had REAL LOGIC changed on 2026-08-07 and it has
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

**Jump to a date** — 290 entries across 20 working days, oldest first, newest at the bottom.

| Date | Entries | | Date | Entries |
|---|---|---|---|---|
| [2026-07-21](#2026-07-21) | 2 | | [2026-08-10](#2026-08-10) | 6 |
| [2026-07-22](#2026-07-22) | 18 | | [2026-08-11](#2026-08-11) | 21 |
| [2026-07-23](#2026-07-23) | 9 | | [2026-08-13](#2026-08-13) | 23 |
| [2026-07-26](#2026-07-26) | 26 | | [2026-08-14](#2026-08-14) | 14 |
| [2026-07-27](#2026-07-27) | 11 | | [2026-08-15](#2026-08-15) | 1 |
| [2026-08-01](#2026-08-01) | 8 | | [2026-08-17](#2026-08-17) | 1 |
| [2026-08-04](#2026-08-04) | 24 | | [2026-08-19](#2026-08-19) | 4 |
| [2026-08-06](#2026-08-06) | 53 | | [2026-08-20](#2026-08-20) | 45 |
| [2026-08-07](#2026-08-07) | 11 | | [2026-08-21](#2026-08-21) | 10 |
| [2026-08-09](#2026-08-09) | 1 | | [2026-08-22](#2026-08-22) | 2 |

### 2026-07-21

- 2026-07-21 — Added 13 `filters/` fragments after de-duplicating a proposed list; declined 2 as
  redundant/wrong-contract.

- 2026-07-21 — Found `filter-by-system-type.cs` silently matching system NAME, not TYPE (a `??` fallback
  that never ran). Fixed in place; split the old behavior into `filter-by-system-name.cs`.

### 2026-07-22

- 2026-07-22 — Element ID glossary entry + standing rule: element reports always include Element ID.

- 2026-07-22 — Reply-style rule: a narrowed request ("the 300x300 VCDs") gets an item list with IDs, not
  a bare count. Updated `reply-style.md`, live-model skill, `scripts/architecture.md`.

- 2026-07-22 — Universal-actions audit: built `action-delete-elements.cs`, `action-rename-element.cs`,
  `create-schedule.cs`; wrote `knowledge/universal-actions-reference.md`.

- 2026-07-22 — Expanded the reference to 175 actions (v2), then 182 (v3, full Revisions lifecycle).

- 2026-07-22 — Folded four factual Revit lessons into `core.md`/glossary (UniqueId stability,
  discover-params-first, overflow caution, linked-element LevelId). Same pass: all 11
  graphics/visibility fragments got optional `targetViewIdInt`.

- 2026-07-22 — Created `AGENT-SPEC.md` (11-section operating manual); follow-up caught 2 staleness gaps
  in it same-day. Lesson: a consolidated spec needs a deliberate re-check after changes, not just a link
  check.

- 2026-07-22 — Built 14 native schema-validated MCP tools (Node-side only — the Revit listener needed
  zero changes); split `mcp-server/` one-file-per-tool. Caught a NUL-byte corruption `node --check`
  missed — lesson: `node --check` alone is not sufficient proof of a refactor.

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

### 2026-07-23

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

### 2026-07-26

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

- 2026-07-26 — Added 6 bridge principles to `live-model/core.md` Bridge basics:
  empty-result-is-valid, never-invent-ElementIds, resolve view-relative direction words before
  moving, one-composed-script-over-many-calls, verify-small, workshared-sync reminder. A
  "reuse cached state, don't re-query" rule was considered and rejected — it conflicts with our proven
  fresh-reads rule.

- 2026-07-26 — Tool-gap backlog build: 9 gaps found became 14 new fragments — room elevations, floors, sheet sets, compare-elements,
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

### 2026-07-27

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

### 2026-08-01

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

### 2026-08-04

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

### 2026-08-06

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

### 2026-08-07

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

### 2026-08-09

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

### 2026-08-10

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

### 2026-08-11

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

### 2026-08-13

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

- 2026-08-13 — **`glossary.md` discounted to 0.93 — and 0.85 was measurably too harsh.** It had taken #1
  from `ajtools-hvac-terminal-layout` on the diffuser question, the same displacement `brain-log.md` was
  discounted for; `semantic-index/README.md` still claimed the skill ranked first, and had been wrong for
  about a week. Applying the log's 0.85 fixed that and **broke a different question**: "what does duck
  mean" then returned `nfpa13-sprinkler-spacing.md` at #1, so the glossary could no longer answer the one
  kind of question it exists for. Swept both cases: they both hold from **0.90 to 0.96**, so 0.93 (the
  centre) was chosen rather than an edge value — these RRF scores differ by thousandths, so an edge is one
  new file away from flipping. Both are now guard rows in `test-questions.md`. **The transferable rule: a
  discount aimed at a file's accidental matches must be checked against the file's own real job, or the
  fix quietly removes a capability.**

- 2026-08-13 — **REFUTED: big files do NOT win by having more chunks.** Worth recording precisely because
  the evidence looked strong and the fix was about to be built. Both remaining test failures had a wrong
  winner 4–5× the size of the right answer (`universal-actions-reference.md` 32 chunks vs
  `create-levels.cs` 6; `action-plan-shortest-route.cs` 34 vs `action-count-by-group.cs` 8), which fitted
  a tidy theory: `_best_per_file` takes each file's best chunk, so more chunks = more draws, and neither
  BM25's length normalisation (which is per *chunk*, and chunks are all ~900 chars) nor RRF corrects for
  it. That theory also seemed to explain why `brain-log.md` and `glossary.md` had each needed a manual
  discount. **Tested it before building it: damping the suspected winner to 0.90/0.80/0.70 never surfaced
  the correct file — a third file simply took #1 instead** (`create-view.cs`, then
  `generate-room-coverage-layout.cs`). So the small correct files are not being *beaten* by big ones; they
  are not scoring on their own merits at all, which points at the embedding model rather than at ranking.
  **Two consequences.** (1) No chunk-count normalisation was built. (2) The claim in
  `docs/superpowers/specs/2026-08-13-brain-rag-and-agents-design.md` §7.4 that splitting the two oversized
  knowledge files would improve *retrieval* has lost its evidence — the ~300-line rule stands on
  readability, not accuracy, until something measures otherwise. `scripts/README.md` is the largest matcher
  in the index at **251 chunks, 8.4% of all 2,989**, and is deliberately left undiscounted on the same
  reasoning: nothing has shown it is actually displacing better answers.

- 2026-08-13 — **`job-log/` records what this Brain is actually used for.** Nothing did before, so three
  questions were unanswerable: which of the 269 fragments do the work, which have never run, and which
  fail against a real model — the last being the most valuable signal the system produces and the one
  that used to vanish when a session ended. `questions.jsonl` (written by `brain_context.py --log` from
  the auto-search hook) records each question and what came back; `revit-runs.jsonl` (written by
  `tools/job-log-revit.mjs`) records each call that reached Revit. **Fragments are identified from the
  `// FRAGMENT (kind) — name.cs` headers inside the composed C# actually sent**, so the record stays
  correct through renames and needs no separate registry — but it also means a malformed header makes
  real usage invisible. Read it with `node tools/job-report.mjs [--unused]`. Two deliberate choices:
  it lives **outside every indexed folder**, because a steadily growing file inside `knowledge/` would
  become another large matcher — the exact fault `brain-log.md` and `glossary.md` were each discounted
  for; and `--unused` says on every run that it means *no evidence yet*, not *dead*, because the log
  started today and a fragment used heavily last week still shows as never-used. Its second purpose is
  slower: every line is a *question → file* pair, which is the shape of data a fine-tune needs, and
  fine-tuning is the only route left at the site-vocabulary failures that re-ranking was proven unable
  to fix earlier the same day.

- 2026-08-13 — **Two more agents: `brain-script-writer` and `brain-investigator`.** The Script Writer's
  whole reason to exist is the one instruction most likely to be skipped — *search all 269 fragments
  first* — and it is told that reporting "this already exists" is a complete, successful outcome, better
  than a new file, because every fragment added competes in every future search. It marks what it writes
  **untested**: compiling is not proof, and a corrupted NUL byte once passed a syntax check clean
  (2026-07-22). The Investigator gets five read-only bridge tools and **no `run_csharp`**. That choice
  has a real cost, recorded honestly in its own definition: without `run_csharp` it cannot trace MEP
  connectivity, read geometry, or follow a system — those jobs stay in the main conversation with Ajmal.
  The alternative was to give it `run_csharp` and *instruct* it not to write, which is not a boundary at
  all. **The principle, now applied three times: a limit is what an agent was given, never what it was
  told.**

- 2026-08-13 — **Indexing external standards PDFs was built, then reverted the same hour on Ajmal's
  call — scope, not capability.** His words: *"we are making a Revit AJ AI RAG, it's not connected with
  Ashghal standards or something like that — for that we have another skill, or we will create one."*
  Recorded in `START-HERE.md` under what this Brain deliberately does not cover, so it is not rebuilt.
  What the aborted attempt established, and is worth keeping: the documents live in
  `D:\Ajmal\BIM Resources` (PWA/Ashghal CAD Standards Manuals — Buildings v3.0 2015 and v4.0 2023, Roads
  and Drainage v6.0 2022 — plus the D0601 Modelling and Data Management Guide and a Quick Reference).
  **Five of the six extract clean text with `pypdf`: ~561 pages, about 604 chunks** — which is 0.2× the
  existing index, *not* the 7× that had been assumed, so scale was never the objection. The sixth
  (`1761109911158.pdf`, 54 pages, ~79 chars/page, its one readable line reading `ESFR SPRINKLERS`) is
  scanned images and would need OCR. Every page carries a running header giving title, version and page:
  both the citation source and a contamination risk, since left in it would put the document's own title
  into every chunk — the same everything-matches fault as `brain-log.md` and `glossary.md`, but baked
  into the content rather than fixable by a weight. **The reasons to stop were not technical:** these are
  CAD *drafting* standards (layers, title blocks, drawing numbers), not Revit modelling; 604 chunks is a
  20% index increase with no way to measure the damage while the test set holds 5 questions; and
  `job-log/` shows nothing has ever asked for them. **The better pattern, if a rule ever matters: write
  it as a knowledge note in his own words, having read it** — higher signal than 600 unchecked chunks,
  and it cannot quietly quote a superseded version, which was a live risk here since two editions of the
  Buildings manual sit side by side. `pypdf` was uninstalled so the venv matches `requirements.txt`.

- 2026-08-13 — **Retrieval now re-scores itself whenever something that can change ranking changes.**
  `tools/score-check.mjs` (Stop hook) fingerprints five files — `brain_search_hybrid.py`,
  `brain_common.py`, `brain_index.py`, `test-questions.md`, `site-vocabulary.md` — and re-runs
  `score_brain.py` when any of them differs, comparing against the previous history entry and shouting
  if fewer questions now return the right answer first. It **reports, never blocks**: sometimes a drop is
  the accepted cost of a fix, and that is Ajmal's call, not a hook's. It also refuses to compare across
  different question counts, so adding a question is not mistaken for a regression. **Why it was needed:
  the score card existed all day and nothing ever ran it.** It caught three real problems on 2026-08-13,
  and every one was caught only because a person happened to type the command. Proven by deliberately
  crushing the glossary weight to 0.10 and watching it report `3/5 -> 2/5`, then reverting.

- 2026-08-13 — **Tuned path weights drift as content changes — the cliff moves.** Found while testing the
  above. This morning a sweep put the safe window for `glossary.md` at 0.90–0.96, with 0.85 measurably
  breaking "what does duck mean" (`nfpa13-sprinkler-spacing.md` took #1). By the same evening, 0.85 no
  longer broke it — the index had changed underneath, through this log growing and a full rebuild, and
  the competing file no longer outranked the glossary. **Nothing was wrong with the earlier measurement;
  it simply expired.** So a hand-tuned weight is a measurement of one moment, not a constant, and the
  only defence is re-measuring automatically — which is exactly what the hook above now does. 0.93 was
  kept: it sat in the middle of the window then and remains safe now, which is the whole argument for
  choosing a window's centre over its edge.

- 2026-08-13 — **The injected context block now carries an explicit "say so" guardrail.** Retrieval is
  right at #1 on 3 of 5 questions, so roughly two in five put a wrong file at the top — and the silent
  failure mode is the dangerous one: an answer from general Revit knowledge, delivered with the
  confidence of one drawn from Ajmal's own proven files. `brain_context.py` now ends every block with an
  instruction to say plainly when none of the hits actually answer the question. **"The Brain does not
  cover this" is a correct answer; quietly inventing one is not.**

- 2026-08-13 — **A cross-encoder re-ranker is built, works, and ships OFF — because it measured neutral.**
  `semantic-index/rerank.py` runs `ms-marco-MiniLM-L-6-v2` as ONNX through onnxruntime and tokenizers,
  both of which arrive with chromadb, so it adds **no pip dependency**; `sentence-transformers` was
  rejected for dragging in PyTorch (~2 GB) on a deliberately minimal offline machine. Enable per search
  with `--rerank`, or `use_rerank=True`. Checked *before* building, which set an honest ceiling: of five
  test questions three already returned the right file at #1, one had its answer at **#7** (what
  re-ranking is for), and one was **absent from the whole candidate pool** — no re-ranker can promote
  what the first stage never retrieved. Ceiling 3/5 → 4/5. Result: **2/5 fed the raw question, 3/5 fed
  the expanded one, i.e. neutral, at ~1.5 s per query.** The reason is the keeper, and it is not a bug in
  either component: *"take my door schedule out to excel"* only works **because** `site-vocabulary.md`
  expands it — the word "excel" is in no file — so the raw question makes the cross-encoder correctly
  find nothing; but *"add 4 more floor levels"* expands to `level elevation storey`, and **"elevation" is
  two different things in Revit** (a level's height, and an elevation view), so the cross-encoder, which
  genuinely reads, is led to `create-room-elevations.cs` (−2.35) over `create-levels.cs` (−7.08) — and it
  was scoring the correct PURPOSE card, not a code chunk. **One layer needs the expansion, the other is
  misled by it, in opposite directions, on the same five questions.** Untunable at this sample size, so
  shipping it on would have been exactly the blind change the score card exists to prevent. The 89 MB
  model is git-ignored, so `rerank.py --download` exists rather than living in someone's memory.

- 2026-08-13 — **Housekeeping pass: the ~300-line warning is clear for the first time since it appeared.**
  `core.md` went 328 → **262** lines by moving the Revit 2020 viewport and view-title limits into
  [`live-model/views.md`](live-model/views.md), where they belong on *subject* and not merely on size —
  none of that material was about units or bridge basics, and `views.md` was only 96 lines. A pointer
  section stays behind, matching the `element-identity.md` split of 2026-08-06. **`families.md` (456
  lines) was reviewed and deliberately KEPT WHOLE**, marked with `split-review: kept whole` so
  `brain-status.mjs` stops flagging it: it is not four topics but one method plus a chronological record of
  four builds, each written as what the previous one got wrong — split by build and the corrections lose
  what they correct; split method from builds and the method loses its evidence. Re-open only if a fifth
  build adds a section that stands alone. Also in the same pass: the knowledge graph was rebuilt
  (`graphify update .` — AST only, no LLM and no subagents: **1,214 nodes, 1,296 edges, 344 communities**)
  and the Obsidian vault regenerated to **1,558 notes with 308 stale ones pruned**. Both had last run
  2026-08-09 and were four days behind. **Still outstanding there:** graphify's *semantic* pass over the
  markdown needs either a `GEMINI_API_KEY` or subagents, so doc-level entities are as of 2026-08-09 while
  code-level ones are current — a real, and currently invisible, split-brain in that graph.

- 2026-08-13 — **The graphify split-brain is closed: the markdown side was extracted with subagents, on
  Ajmal's explicit go-ahead.** 58 documents detected, 28 already cached, **30 needing extraction** — exactly
  the files touched since 2026-08-09. Final graph: **1,184 nodes, 1,356 edges, 336 communities**, from 689
  AST nodes plus 496 semantic (197 cached + **300 newly extracted**). Four things worth keeping, three of
  them problems. **(1) One subagent died mid-run to an API connection error and wrote nothing.** The other
  two survived. Retried it as *two* smaller chunks rather than one — the failed chunk had all the big root
  docs (`AGENT-SPEC.md`, `README.md`, `START-HERE.md`, `CLAUDE.md`, `SETUP.md`) in a single batch — and both
  halves then completed. **Chunk by size, not just by count.** **(2) The shrink guard fired and had to be
  overridden:** 1,184 new against 1,214 existing. Verified legitimate before forcing — the old graph held
  semantic nodes from the *2026-08-09* versions of 30 changed files, including the viewport text that has
  since moved from `core.md` to `views.md`, and this extraction was deliberately instructed to be more
  conservative (no node per fragment row, no node per log entry). Fewer, better nodes is a *smaller* graph,
  which is exactly the shape the guard is built to stop — so a guard like that needs a human reason, not a
  flag. **(3) UNRESOLVED — the health check reports 203 dangling-endpoint edges (~15% of 1,356)**, plus 16
  collapsed same-endpoint edges. That is the AST-vs-LLM node-ID mismatch the graphify spec warns about:
  subagent edges pointing at IDs the AST names differently. The graph is usable and this is recorded rather
  than hidden, but it is a real integrity gap and nobody has chased it. **(4) Communities are unlabelled** —
  `graphify label` found no LLM backend, so all 336 keep `Community N` placeholders. Fixable any time with
  `GOOGLE_API_KEY` set and a re-run. Obsidian vault regenerated to 1,503 notes, 325 stale pruned.

- 2026-08-13 — **The 203 dangling edges are fixed: 203 → 0, and 162 edges recovered.**
  [`tools/graphify-repair.py`](../tools/graphify-repair.py) runs on the extraction *before* the graph is
  built. **The loss was invisible by design** — the build never warns, it just drops them, so 1,549
  extraction edges quietly became 1,356 and only `diagnose_extraction` ever knew. Measured, they were not
  one problem but four: **(A) 111 endpoints were external modules** — `ref_node_fs`, `ref_zod`, `sys`,
  `pathlib` — where the AST emits an import edge for every dependency but only makes nodes for files
  *inside* the corpus, so "brain_common.py imports sys" was a true edge pointing at a node nobody created;
  **(B) 9 were whole-file references** whose ID was exactly a real file stem; **(C) 82 were concepts in a
  file the writing subagent did not have in its chunk**, so it invented a plausible ID for something the
  chunk that *did* have the file named differently — remapped to that file's own node, which is weaker
  than the intended edge but true; **(D) 0 were unresolvable.** **A one-off JSON patch was rejected: every
  one of these is regenerated by the next extraction, so hand-fixing would look fixed and silently rot.**
  Two things worth keeping. **The pass order is load-bearing** — the first version classified all four in
  one loop and dropped ~20 `knowledge_brain_log_*` concepts, because C looked for a `knowledge_brain_log`
  node before B had created it; splitting into create-then-match took D from 29 ids to 0. **And the honest
  cost:** collapsing concept references onto file nodes raised same-endpoint collapsed edges from 16 to 47.
  That is real information merging, traded knowingly for 162 recovered edges — and it is why the script
  prints what it did rather than running silently. Idempotent: a second run reports nothing to repair.

- 2026-08-13 — **All 328 graph communities are named; zero `Community N` placeholders left.** `graphify
  label` needs an LLM backend and found none — **but the host agent IS the LLM**, which is exactly what the
  skill's Step 5 says: read the analysis, look at each community's node labels, write a 2–5 word name. No
  API key was ever required for this; the CLI path simply assumes an external model. The distribution made
  it tractable: **265 of 328 communities are a single node**, and only 60 hold 3+, but those 60 cover
  **921 of 1,192 nodes (77%)** — so the whole job was reading 60 clusters, not 328. Those were named by
  hand (`MCP Server & Native Tools`, `Live Model Scripting Gotchas`, `MEP Connection Method & Tracing`,
  `Parametric Family Authoring`, `Voice Speaker Process`, …); the remaining 268 are auto-named after their
  own hub node, which is what graphify's fallback does and is more honest than inventing a theme for one
  node. **Where they are stored is worth writing down**: not `graph.community_labels` and not
  `node.community_label`, but **`community_name` on each node** — two wrong guesses were made before
  checking, and a "0 labels stored" reading nearly got reported as a failure when the labels were fine.

- 2026-08-13 — **NEAR MISS: `npm test` in `mcp-server/` will DELETE MODEL CONTENT if the bridge is
  connected. Now gated.** The suite invokes every handler with `SAMPLE_ARGS`, and those include
  **`delete_elements: { category: "Ducts", confirm: true }`** plus `move_elements`,
  `set_parameter_value` and the hide/colour tools. Its own header says "no live Revit/bridge connection
  needed" — **and nothing ever checked that.** The premise silently stops being true the moment Revit is
  open with the bridge on, which is precisely when someone runs the tests. It was run during a live
  session today and survived **only by luck**: the active document happened to be an unrelated blank
  `Project1` with zero ducts, so every destructive call matched nothing. Had the real model been active
  it would have deleted **354 ducts, with `confirm: true` already supplied**, and the first sign would
  have been a test *passing*. **Fixed by enforcing the premise instead of assuming it:** a read-only
  `ping` now decides, and the handler-invocation test SKIPS with a loud reason when the bridge is live.
  **The transferable lesson: a test whose safety depends on an environment assumption must CHECK that
  assumption, not state it in a comment.** Two lines of prose at the top of the file were doing the work
  of a guard for months. Also worth noting how it surfaced — the failure looked like an ordinary broken
  assertion (`expected isError`), and the real meaning was the opposite of what it appeared to say.

- 2026-08-13 — **`search_graph`: the knowledge graph is finally reachable from retrieval.** The Brain had
  a 1,192-node graph with 328 named communities and **nothing could query it** — every search went through
  the vector index only, so a fully-built asset sat unused. Studying the public RAG-technique catalogues
  Ajmal collected (NirDiamant/RAG_Techniques, athina-ai/rag-cookbooks) confirmed most of their list is
  already here or does not apply at 3,000 chunks — hybrid dense+BM25+RRF, query transformation, explainable
  retrieval, pre-filtering, reranking, evaluation — but **GraphRAG was the one real, already-paid-for gap.**
  It is not a second opinion; it answers a different SHAPE of question. Measured on "how do I stop ducts
  overlapping the ceiling": the vector search returned `filter-by-element-intersection.cs` / `tagging.md` /
  `brain-log.md`, while the graph returned `create-ceiling.cs` and **`ray-trace-to-ceiling.cs`** — a file
  the vector index never surfaced at any depth, found by matching the question's ENTITIES and walking to
  their neighbours. Modes: `query`, `explain`, `path`. **Honest limitation recorded in the tool itself:
  the graph does not rebuild itself**, so `search_graph` can be older than `search_brain` in a way that is
  invisible from its output.

- 2026-08-13 — **REVERTED: contextual chunk headers made retrieval WORSE here — 7/14 → 5/14 at top-5.**
  This is **Anthropic's own published Contextual Retrieval**, which reports a 35% drop in retrieval
  failures, and reading our own chunker showed the gap was real: `.cs` **code** chunks were emitted as raw
  body text with *no filename and no PURPOSE*, and `.md` section labels were folded in **before** splitting,
  so any section long enough to split kept its heading on piece one and lost it on every piece after. Both
  look like free wins. Both scored worse, and the code prefix alone was enough to cause it — measured
  separately. Restored the baseline and re-scored to prove the revert was complete: **4/14 #1 and 7/14
  top-5 came back exactly.** Best reading of why it does not transfer: **the context was already indexed,
  in the chunk built for it.** Every fragment has a `card` chunk carrying filename + PURPOSE, and
  `KIND_WEIGHT` scores code at **0.35** *precisely because* variable names are incidental. Injecting
  PURPOSE into every code piece hands those chunks the same high-value words the card carries, so a
  fragment's code competes with its own card — on text the ranking deliberately weighted down. For
  markdown, repeating `filename § heading` across every piece also inflates those tokens' document
  frequency, so BM25's rarity weighting counts them for less. **The transferable lesson: a technique with
  a published effect size was measured against someone else's corpus and someone else's chunker. This one
  is already structure-aware and kind-weighted, and the same idea double-counts instead of adding.** Fifth
  time today that measuring beat reasoning — and the first where the reasoning was a published paper's.

- 2026-08-13 — **The knowledge graph now rebuilds itself too, and says when its markdown half is stale.**
  `tools/graph-rebuild.mjs` (Stop hook) + `tools/graph-rebuild.py`. This closes the last silent-staleness
  gap: the vector index has self-rebuilt since Phase 1, the graph never did, so `search_graph` could answer
  confidently from a graph built days earlier with nothing anywhere saying so. Four decisions.
  **(1) It does NOT call `graphify update .`.** That command runs extract → build → write in one step with
  nowhere to insert `graphify-repair.py`, so it would silently undo the fix that took 203 dangling edges to
  0 — a self-maintaining pipeline that quietly reverts its own repair is worse than no automation.
  `graph-rebuild.py` runs the same pipeline with the repair in the middle. **(2) Gated on CODE files only.**
  A rebuild is ~13 s against ~0.3 s for the index; only a code change can move the AST half, so editing a
  knowledge note no longer pays to re-parse 328 source files. No-op cost is 0.68 s. **(3) The markdown half
  cannot be automatic** — it needs subagents or a Gemini key — so instead of pretending, it REPORTS: "N of
  58 documents changed since their cached extraction". Naming the gap is the honest half of not being able
  to close it. **(4) `score-history.md` is excluded from that count**, because it is machine-written and
  would report stale forever; a warning that is always on is one nobody reads, the same reasoning that made
  the STALE INDEX banner compare content and not dates. Found while building it: the hook first reached for
  `semantic-index/venv`, which does **not** contain graphify — it lives in whichever interpreter
  `graphify` was installed into, recorded by graphify itself in `graphify-out/.graphify_python`.

### 2026-08-14

- 2026-08-14 — **Fixtures can be BUILT, so "fixture-blocked" was never the blocker it looked like.**
  `action-add-remove-insulation.cs` had sat unproven since 2026-07-26 marked *"BLOCKED — no insulation
  fixture in this model"*. It took one `Duct.Create` call to make five ducts in a blank scratch project
  and the fragment then verified **all three branches**: add put 25 mm on 5 of 5 (read back as five real
  insulation elements, not trusted from the report), re-add correctly added 0 and skipped 5, remove
  deleted all 5 and left the ducts intact. **The lesson generalises to the whole "fixture-blocked"
  category: the question is not "does this model contain X" but "can X be created by API".**

- 2026-08-14 — **`action-place-accessory-on-run.cs` DISPROVED live, then rewritten. Three separate causes,
  each of which alone looks like the whole answer.** The original called
  `NewFamilyInstance(point, symbol, run, StructuralType)` and assumed Revit breaks the run automatically
  the way the UI command does. **It does not, and it does not error** — duct count unchanged, `fi.Host`
  null, accessory sitting loose with 0 of 2 connectors joined, and the fragment reported success. Its own
  2026-07-26 static review had flagged exactly this and named the two suspects; the live run settled it as
  the overload, not the family. **(1)** `BreakCurve` must be called explicitly — and it confirmed two
  existing notes: the ORIGINAL id keeps the SECOND half, and the halves are not auto-connected. **(2) A
  newly placed accessory takes its FAMILY DEFAULT size, not the run's.** On a 300x300 duct that default
  was 300x300 and looked like Revit had matched it — a coincidence that hid the bug until a 200x200 duct
  produced the same 300x300 accessory. Only `Duct Width`/`Duct Height` are writable; every other size
  parameter is read-only and derived. **(3) THE ONE THAT COSTS AN HOUR: joining both halves inside a
  single transaction makes Revit raise, at COMMIT, a modal "Error — cannot be ignored: The family is
  connected in a network and can no longer keep the connectivity."** A bridge script cannot answer a modal
  dialog, so the call does not fail — **it HANGS**, and Stop must be pressed in the AJ AI pane. Ajmal
  screenshotted the dialog, which is what identified it; from the script side it was indistinguishable
  from a frozen Revit. Committing each join separately gives 2/2 silently. **Also re-learned the hard way:
  `Document.Regenerate()` after `Commit()` throws "Modification of the document is forbidden" — already
  documented in `live-model/core.md` and still walked into.** Status recorded honestly: the METHOD is
  proven (2/2 joined, both halves 1/1), the rewritten file as one uninterrupted run is NOT — a re-test
  threw a null reference and rolled back cleanly, and the null was not isolated before the session ended.

- 2026-08-14 — **`slice-trunk-for-sizing.cs` re-verified, and a SILENT bug found and fixed:
  `Line.Distance()` measures to the SEGMENT, not the line.** `onTrunkLine` tested collinearity with
  `startCurve.Distance(endpoint) < 0.05`, but a `Line` taken from a `LocationCurve` is **bound**
  (`IsBound = True`), so on a 3-piece 12 m trunk it measured 4000 mm and 8000 mm to the other two pieces
  and excluded both. **A function whose comment says "so multi-piece trunks are handled too" only ever
  found the piece whose Id was passed in.** The failure was invisible: with 4 takeoffs on the trunk it saw
  1, grouped it into one cut position, `skipLastTakeoff` removed that one, and the script printed
  **"0 cut(s) made, 0 failed"** — a clean success message for having done nothing. Fixed by measuring
  perpendicular distance to the INFINITE line (project onto `trunkDir`, subtract, take what is left).
  After the fix: 3 pieces found, 4 takeoffs, 3 cuts at 2700/5700/7700 mm (each takeoff + the 700 mm
  offset), 0 failed, 3 union fittings all connected, trunk 3 pieces → 6 — then a full BFS walk over real
  connector links confirmed **4 of 4 branches still reach the far trunk end**, which is the check the
  recipe's own header demands instead of trusting `IsConnected`.
  **The bigger point, and the second time in two days: the fixture was BUILT, not found.** The 2026-07-23
  note on this file says plainly "no matching multi-branch trunk fixture available", and it had blocked
  re-testing for three weeks. It took `Duct.Create` plus `Document.Create.NewTakeoffFitting` in a blank
  project to make one — a 12 m trunk in 3 pieces with 4 real takeoffs, each producing the
  `ConnectorType.Curve` connector the recipe counts. **"Fixture-blocked" should always be challenged with
  "can this be created by API?" before it is accepted.**

- 2026-08-14 — **`examples/color-isolate-select-by-size.cs` verified live — all three chained actions.**
  Run verbatim on its own default input (Height eq 500 mm) against a deliberately adversarial fixture: 17
  ducts, only 2 at height 500, and 15 negatives at 400/300/250/200 with **different widths on the two
  targets**, so passing required filtering on Height alone rather than on size generally. Every result was
  confirmed from a SEPARATE bridge call rather than from the script's own report: 2/2 coloured red with
  **0 wrongly coloured**, exactly those two visible under temporary isolate, exactly those two selected.
  **One anomaly, written down precisely because it was not explained:** on the first run the selection read
  back as **0** in the following call while the script had printed "Selected 2". It never reproduced.
  `UIDocument.Selection.SetElementIds` was then shown to work, to survive across bridge calls, and to
  survive an isolate-then-select in the same script — the exact order the example uses. So no bug is
  claimed. **The practical rule: if a selection looks empty after this example, re-read it before
  concluding the script failed** — three silent-success bugs in two days makes an unexplained zero worth
  a second look rather than a fourth bug report.

- 2026-08-14 — **The whole non-MEP fixture set was built by API in a blank project, and two more fragments
  cleared with it.** Four room-bounding walls (a closed 8×6 m rectangle), a **Room** and an **MEP Space**
  both computing to 43.2 m², a **Floor** (Generic 300 mm), and **three Sheets** on the A1 metric title
  block — `Wall.Create` + `NewRoom` + `NewSpace` + `NewFloor` + `ViewSheet.Create`, none of it needing a
  real project. Cleared with it: **`create-hvac-zone.cs`** (zone on Level 1 under phase "New
  Construction" — which is what `phaseName = null` resolves to, the document's LAST phase, not its first;
  1 space added, 0 rejected, and the zone read back as genuinely holding that Space) and
  **`create-room-elevations.cs`** (marker on the room's true centre, all 4 slots producing views at 1:50,
  ViewSection count 16 → 20 confirmed independently). **Noted, because it will look like a bug the next
  time somebody sees it: a freshly created Zone reports `Area = 0 m²` while the Space inside it reports
  43.2 — the same delayed computation a new Space itself shows, which is why every check here reads state
  back in a later call instead of trusting the value available at creation time.**

- 2026-08-14 — **The last three annotation/sheet-set fragments verified live — and two of their three
  predicted CHECKs were wrong, one dangerously so.** `action-add-aligned-dimensions.cs`: 3 diffusers at
  2500 mm centres produced a 2-segment dimension reading 2500.0 mm each, confirmed from a separate call.
  Measured while doing it — **stock duct fittings and duct accessories expose NO family references at all**
  (CenterLeftRight/CenterFrontBack/Strong/Weak all 0), so this fragment can never dimension them; air
  terminals expose CLR+CFB and work. Also: reading a dimension back, `Dimension.Curve` throws "The input
  curve is not bound" — verify via `Segments`/`References`, never `.Curve`.
  **`action-add-spot-elevations.cs` — a fourth silent-success bug, and the predicted fix would not have
  caught it.** It annotated the SOFFIT at -300.0 mm on a 300 mm floor while reporting "1 placed, 0
  skipped": Revit hands back the bottom face (normalZ -1.00) BEFORE the top, and the fragment took the
  first planar face with a Reference. The open-items CHECK proposed guarding with
  `Math.Abs(pf.FaceNormal.Z) > 0.9` — **that passes -1.00 and fixes nothing.** The guard has to be
  POSITIVE: `FaceNormal.Z > 0.9`. Fixed by collecting every candidate face and choosing deliberately via a
  new `facePreference` input ("top"/"bottom"/"any"), and the annotated level is now printed per element so
  a wrong face cannot hide inside a success message again. Re-run reads 0.0 mm.
  **`action-manage-sheet-sets.cs` — all four modes verified, and a three-week-old header assumption
  disproved.** The header claimed selecting an existing saved set as `CurrentViewSheetSet` "is awkward on
  2020", so rename/delete used `Element.Name`/`Document.Delete` instead. Rename FAILED live — Revit itself
  names the fix: "please set the name via PrintSetup::Rename method". Reflection then showed
  `CurrentViewSheetSet` is plainly settable (canWrite=True). Both now assign it and call
  `ViewSheetSetting.Rename()`/`.Delete()`; rename kept the same element id and members, delete removed the
  set and left all 3 sheets standing. Two further gotchas recorded in the header: `ViewSheetSetting` throws
  unless `PrintRange = Select` is set first, and that PrintRange change is genuinely non-transactional.
  **The pattern across all three: a CHECK written by reading the code is a hypothesis, not a finding.**
  One was right, one was wrong-and-harmless, one was wrong in a way that would have shipped the bug.

- 2026-08-14 — **"Fixture-blocked" was wrong a third time, and the fix took one call.**
  `action-add-remove-insulation.cs` had been marked BLOCKED since July for "no insulation fixture in this
  model". But that fragment **creates** insulation — it never needed a fixture, only an insulation TYPE,
  and the stock template ships six (2 DuctInsulationType, 2 PipeInsulationType, 2 DuctLiningType). It was
  runnable the whole time. Verified live: add (3 ducts -> 3 DuctInsulation, "Rigid Fiber Board", 25.0 mm),
  the already-insulated skip ("added 1, skipped 1" over one insulated + one bare duct), and remove (2 of 4
  gone, the other 2 untouched, all 17 ducts still standing). Its output then cleared
  `filter-by-insulation-status.cs` (4 insulated / 13 bare out of 17, exact ids) and
  `filter-by-insulation-type.cs` (20–30 mm band -> all 4, resolveToHost -> the 4 Ducts, **>=50 mm -> 0 as a
  deliberate negative control** — a filter that only ever returns "all" looks correct until the day it
  matters). **Two ducts are left permanently insulated in the test model** so these three stay re-runnable.
  That is now THREE false "fixture-blocked" items in three days. The question to ask first is not "does the
  fixture exist" but "can this fragment build its own fixture, or can the API build one".

- 2026-08-14 — **`tools/brain-status.mjs` was undercounting verified fragments — the drift-detector had
  drifted.** It reads `scripts/README.md` row markers with `/verified 2026/`, but nine rows say
  "verified **live** 2026-08-14", so nine genuinely-verified fragments were being reported as "no status
  either way" — the precise failure this tool exists to catch, inside the tool. Widened to
  `/verified(?:\s+\w+)? 2026-\d\d-\d\d/`, which also picks up "re-verified". Real figure moved 223 -> 232
  (83% -> 86%) with no fragment changing state. **Lesson: a checker that reads prose markers needs to
  tolerate the ways a human actually writes them, or it quietly reports the library as worse than it is —
  and nobody doubts a number that errs pessimistically.**

- 2026-08-14 — Fixed drift found the same day: `create-hvac-zone.cs` and `create-room-elevations.cs` had
  been verified live and logged, but both fragment headers still read "NOT YET LIVE-VERIFIED", and the two
  insulation filters' headers still read "not yet live-verified" while their README rows recorded a
  2026-08-06 verification. **The README and the headers are two records of the same fact and they drifted
  apart in both directions.** Headers now carry the evidence, including the two gotchas worth keeping: a
  fresh Zone reports `Area = 0 m²` until recomputed, and `phaseName = null` resolves to the document's LAST
  phase, not its first.

- 2026-08-14 — **Electrical was never "fixture-blocked" either — the fixture was built in four calls, and
  `load-family.cs` fell out of the same discovery.** On 2026-08-07 `filter-by-electrical-system.cs` was
  re-checked and called "genuinely blocked, needs electrical content". The unasked question was where
  content comes from: `C:\ProgramData\Autodesk\RVT 2020\Libraries\US Metric\` ships **166 electrical
  .rfa files**. Loading two of them cleared `load-family.cs` (item 1 of "needs a file only Ajmal can
  supply" — it never needed one) and then produced the circuit that cleared the filter: a real panelled
  PowerCircuit via `ElectricalSystem.Create` + `SelectPanel`. Verified with a negative control ("Data" -> 0).
  **Three gotchas worth more than the verification itself:** (1) only the **MEP** library families carry
  electrical connectors — the Architectural `M_Electrical Panel.rfa` / `M_Outlet-Duplex.rfa` place happily
  but have a NULL `MEPModel.ConnectorManager`, and `ElectricalSystem.Create` then fails with the unhelpful
  "There should be at least one component that can create the specified circuit type"; (2)
  `ElectricalSystem.Elements` returns the LOADS on a circuit, **not** the panel feeding it, so a 2-equipment
  circuit reports 1 element and that is correct; (3) `Connector.IsConnected` **throws** on a panelboard —
  its 6 connectors include CableTrayConduit MasterSurface ones, and connection status only exists for
  PhysicalConn. That last one sharpens the standing "never trust `IsConnected`" rule in `mep-trace.md`:
  on some equipment it does not merely mislead, it will not even answer.
  Also re-confirmed by reflection, so it is not re-litigated: **`Ceiling.Create` has 0 overloads on 2020**
  and `Document.Create` has no ceiling method. `create-ceiling.cs` is correctly written up as impossible;
  `ray-trace-to-ceiling.cs` is an ASK ("Ajmal, draw one ceiling"), not a fixture wait.

- 2026-08-14 — **New skill: [`ajtools-visual-report`](../skills/ajtools-visual-report/SKILL.md), and
  visualization became a standing reply rule rather than a request.** He said *"i need always need
  visualization... if vishalization needdd it need to come"* after showing two Revit dashboards he wanted
  matched, so the rule went into `START-HERE.md` (rule 8) and `reply-style.md`, not only the skill: two or
  more comparable numbers now get a chart or a published dashboard unasked, while a bare count stays a
  bare number. Ships with `dashboard-template.html` so the look is not re-derived per job. Proven once
  end-to-end (duct takeoff off the live model); the other six shapes have proven *fragments* but no
  dashboard built yet. Also logged: **his "visualization" means charts of the model's NUMBERS, never a 3D
  render** — recorded in `glossary.md` because the Revit-normal meaning is the opposite job.

- 2026-08-14 — **"Don't go to Revit" is now an absolute stop** (`live-model/core.md`, Bridge basics).
  When he says another session is running, make zero bridge calls — not even a ping — because the
  one-connection-at-a-time limit means a call preempts his other session rather than queueing. Same limit
  killed one of six parallel calls that day: go sequential.

- 2026-08-14 — **New: [`mcp-ui-surface.md`](mcp-ui-surface.md)** — corrects a claim made in session that
  day: *"no widget, no artifact, no web page can ever reach the bridge."* True of a chat widget or a
  published page; **false for a UI served by our own MCP server**, which is what MCP Apps
  (`io.modelcontextprotocol/ui`) is for. The Revit half already exists — `select_elements`,
  `set_parameter_value`, `report_parameters` since 2026-07-22 — so what is missing is the UI layer, not
  the plumbing. Host support is the real unknown: build a small proof before promising it.

- 2026-08-14 — **Corrected `ajtools-visual-report` within the hour: the chat is the default, a page is
  on request.** The first two reports were published as artifacts he never asked for; he came back with
  *"normaly i need to come in the chat... if i ask the artifects its need to come like this you make html
  file"*. Skill, `reply-style.md` and `START-HERE.md` rule 8 all flipped, and **"artifact" is now recorded
  in `glossary.md` as a request word, not a default output**. Also added to the skill: a size/width axis
  sorts smallest→largest in BOTH table and chart, and a grouping that hides another (width hiding height)
  must say so.

### 2026-08-15

- 2026-08-15 — **`recipes/mep-grayout.cs` proven end-to-end on a real model** — run on
  `PLAN AT EL. +100.950_HVAC Ground Floor Layout Copy 2` (1:50, HLR, no view template): 87 categories +
  589 sub-categories written, rebar pair off, and a read-back confirmed every value in the skill's table.
  The 63 categories that lost a slot are exactly the predicted non-cuttable ones. Gap found on this
  project: `MEP_Hidden_Short_Dash` is not loaded, so insulation came out grey but **solid** —
  `recipes/create-mep-line-standards.cs` installs it, and the grayout must be re-run afterwards.

### 2026-08-17

- 2026-08-17 — **The daily "check AJ Tool / vector index / Graphify / Obsidian" routine cannot see three
  of its four targets, and would have passed silently forever.** It runs in a Claude Code cloud container
  against a fresh `git clone`, and the vector index (`semantic-index/chroma-db`, `venv`, `model-cache`),
  the graph (`graphify-out/`) and the Obsidian vault generated into it are all **gitignored on purpose** —
  the same "a stale index in this repo is worse than no index" rule recorded 2026-08-07. So the clone has
  the *code* for all three and the *state* of none, and `.mcp.json` points at `D:\Ajmal\...`, so there is
  no bridge in that session either. What a cloud run genuinely proves is the source side only: all 8
  consistency checks, 11 skills · 270 fragments · 17 tools, 317 indexable files matching the coverage
  claims, and every `.py`/`.mjs` tool parsing. **Freshness of the index, graph and vault is only
  answerable on the Windows machine** — and note `brain-status.mjs` does not check any of the three, so
  nothing on either machine reports their age today. That gap is the thing to close, not the routine.

### 2026-08-19

- 2026-08-19 — **a schedule's *recipe* is now readable, not just its columns.**
  `action-report-schedule-fields.cs` lists the columns of a schedule but nothing could read back the
  rules around them, so "study how this schedule is built" meant hand-written C# every time. New
  `actions/sheets-views/action-report-schedule-definition.cs` reports the category (including the `-1`
  `<Multi-Category>` case), the filter rules with category/element IDs resolved to real names, the
  sort/group levels in order, the itemised/headers/grand-total/links switches, and per column whether
  its value comes from a built-in parameter, a SHARED parameter (with GUID), a project parameter, or a
  schedule formula. Proven on the real multi-category `MM_V03` in 4355-BHVD-3D-60P00-BL006A. **The
  formula TEXT of a calculated field cannot be read on Revit 2020** — `ScheduleField` has no formula
  member at all, so the fragment detects it by reflection rather than calling `GetFormula()`, which is a
  *compile* error there and therefore unreachable by try/catch.

- 2026-08-19 — **the two-open-projects trap, caught live.** With BL006A and BL002A both open, a new
  schedule was created into the WRONG document because `Document` follows whatever Revit has in front,
  and it changed between two tool calls in the same session. It failed silently — success message, real
  Id, plausible row count. Written up in `knowledge/live-model/core.md`: pin the document by title for
  any WRITE, and echo the document title in the result line. The only thing that exposed it was a count
  that contradicted an earlier reading of the "same" model.

- 2026-08-19 — **the MM_ document register became a recipe, not a memory.** Filling the handover register
  (CWA, MM_NP System Type, MM_Discipline Code, MM_Main Document Definition/Statement/Revision,
  Sub-Discipline, MM_Main Drawing Number) was done by hand across four categories on
  `4355-BHVD-3D-60P00-BL006A` — 160 elements — then folded into
  `scripts/recipes/fill-mm-document-register.cs`. Two findings worth more than the script: the drawing
  number must come from **what a sheet actually shows** (`FilteredElementCollector(doc, viewId)`), never
  from the element's level; and **the candidate sheets differ per category** — ducting items belong to the
  duct-layout sheets, equipment to the equipment-layout sheet. Getting that second one wrong is silent:
  every element still receives a plausible sheet number. Ajmal caught it, not the script. Exceptions exist
  inside a category too — exhaust louvres are Mechanical Equipment but drawn on the duct sheets.

- 2026-08-19 — **a family formula makes a parameter read-only, and "it's a type parameter" is not the
  reason.** Four air-terminal grille types refused `Description.Set()` even inside a transaction, while the
  round diffusers accepted it — same category, same built-in parameter, both type-level. The difference was
  a material `if()` formula on Description inside the grille families. `Document.EditFamily` +
  `FamilyManager.get_Parameter(...).IsDeterminedByFormula` is how to prove it rather than guess. 74 more
  locked types sit unplaced across 22 other TCM families in the same project.

### 2026-08-20

- 2026-08-20 — **the 2026-08-17 gap is closed: `brain-status.mjs` now reports the three derived layers.**
  It looks for `semantic-index/chroma-db`, `graphify-out/graph.json`, and the biggest `.md` folder inside
  `graphify-out/` (the Obsidian vault has moved between graphify versions, so it is discovered not
  hard-coded), prints their build date and how many source files are newer than each. When none are
  present — every cloud/container session — it says so in those words instead of inventing a staleness
  number, because a fresh checkout stamps every file with checkout time and would read as "everything is
  newer than the build". Judge is mtime, a hint not a verdict; the authoritative checks are still the
  content-comparing STALE INDEX banner from `ask-brain-hybrid` and `python tools/graph-rebuild.py --check`
  — both named in the output so nobody stops there. `brain-log.md` and `score-history.md` are excluded
  from the "newer than build" count for the same reason `graph-rebuild.py` excludes score-history: they
  are machine-written every session and a warning that is always on gets ignored. Fixture-tested on both
  branches (absent and present).

- 2026-08-20 — **fire sprinklers went from one knowledge note to a subject.** Ajmal asked for the whole
  thing studied properly — spacing, room coverage, "with celling how without celling how", pendent /
  upright / wall, "how mcuh from the wall", "if upraght howmcuh from the slab", and what a beam or a column
  does — then turned into tools. Added `knowledge/fire-sprinkler/` (6 chunks: types, deflector/ceiling
  height, obstructions, the method, the Revit side, and a folder README), and eight fragments
  `scripts/recipes/sprinkler-*.cs` that run as a chain: survey the room → derive the grid from the code
  limits → check every head against the beams and columns → move what fails → set the height by reading
  what is really above → place → audit. `nfpa13-sprinkler-spacing.md` kept its spacing rules and worked
  Room 4 examples and handed its three thin bullets (deflector, obstructions, sidewall) to the new chunks.
  Three things are worth more than the files:
  **(1)** the old Rule 6 bullet here quoted *"2.5 in at 1 ft, 5.5 in at 3 ft, 22 in at 10 ft"* as the beam
  table — those numbers are the **obstruction-against-a-wall** table, which climbs far more slowly. Two
  different tables, conflated for weeks, lenient one way and strict the other.
  **(2)** NFPA 13's own beam table could not be retrieved in that session — the environment blocked every
  page fetch and only search snippets came back. Rather than hardcode remembered numbers, the table is an
  **editable input** on `sprinkler-obstruction-check.cs` seeded `[UNCONFIRMED]`, and every run prints that
  warning until someone types their adopted edition's values in. Same treatment for the other values that
  only one source corroborated. An unchecked number cannot quietly become a compliance claim.
  **(3)** the column exception: the three-times rule is capped at 24 in for most isolated obstructions but
  **not for a vertical one**, so a 600 mm column needs 1,800 mm clear, not 610. That is the car-park trap
  and it is now enforced in code rather than remembered.
  All eight fragments are written but **not live-verified** — no Revit in that session. Every Revit call in
  them is proven elsewhere in this library and each header names which, so the honest route is one element
  first, check the real result, then the batch.

- 2026-08-20 — **the ceiling void, and a number remembered right with the wrong source.** Ajmal asked
  whether NFPA requires sprinklers in the ceiling void once it is "more than eight hundred or something",
  and flagged himself that he was not sure. Checked: the **800 mm is real but it is BS 5306-2 / BS EN
  12845, not NFPA**. NFPA 13 has no depth trigger for a ceiling void — it tests whether the concealed
  space is of combustible construction, and permits omission in a noncombustible/limited-combustible space
  with minimal combustible loading. The two disagree **both ways**: a 900 mm noncombustible void may need
  nothing under NFPA and heads under BS/EN; a 600 mm combustible void is the reverse. New chunk
  `knowledge/fire-sprinkler/concealed-spaces.md` carries the two tests side by side, the NFPA omission
  list, and the two-layer consequence (upright in the void, pendent below — and the void is usually
  OBSTRUCTED construction, so its grid is not the ceiling grid copied upward, which is the standard
  mistake). `sprinkler-obstruction-survey.cs` now measures the void (ceiling top to slab soffit) and flags
  it against a settable threshold, while saying in the same breath that a depth flag cannot answer an NFPA
  question. The pattern worth keeping is bigger than the fact: **he remembered the number correctly and
  the standard incorrectly**, which is the normal shape of site knowledge — so "which standard is this
  project on" now belongs in the opening questions of a sprinkler job, not in an assumption.

- 2026-08-20 — **"is there anything like that?" — yes, and the reason it catches people is the agreement,
  not the disagreement.** Following the ceiling-void finding, Ajmal asked whether there were more rules of
  the same shape. Searched, and the answer has a pattern worth more than the list: **NFPA 13 and BS EN
  12845 agree almost exactly on the headline number and diverge on everything around it.** Max area per
  head, light hazard: NFPA 225 ft² = 20.9 m², EN 21 m². Ordinary: 12.1 m² against 12 m². That near-identity
  is *why* people treat the two as interchangeable — the first number they check does match. Then:
  deflector below a smooth ceiling is **25–305 mm (NFPA) against 75–150 mm (EN)**, so a habit of "250 below"
  is legal under one and not the other and nobody re-checks a mounting height that has worked for years;
  minimum spacing is 1,829 mm against **2,000 mm**, so a layout at 1.9 m passes NFPA and fails EN; and the
  hazard classes **do not map** — EN splits Ordinary into OH1–OH4 and adds HHP/HHS, so reading "OH3" and
  laying out to NFPA "Ordinary Hazard" is a guess, not a translation. New chunk
  `knowledge/fire-sprinkler/nfpa-vs-en12845.md`. `sprinkler-nfpa-grid.cs` and
  `sprinkler-compliance-audit.cs` now take a **required `standardLabel`** and print it on every report —
  the grid refuses to run without it, the audit prints NOT STATED in the clear. A head count with no
  standard named is the same failure as one with no hazard class named, and this session produced the
  evidence for that in one question.

- 2026-08-20 — **"can I design sprinklers from zero to finish?" — not yet, and now the gaps are a list
  rather than a feeling.** Ajmal asked whether a plain architectural plan could be taken all the way, at
  any scope ("the whole plan", "room one", "something specific"), and separately whether the Brain could
  offer **several layout options** so he can reject one and get another. The second answer was a flat no:
  8 of 8 sprinkler fragments took a single room Id, and nothing generated alternatives — the grid recipe
  stops at the first passing grid by design, because it answers "what is the smallest compliant layout",
  which is one question with one answer. Both gaps closed this session:
  `recipes/sprinkler-layout-options.cs` enumerates every compliant nx × ny, collapses the ones that are
  the same *decision*, and ranks them; and `recipes/sprinkler-floor-scope.cs` sweeps a whole level.
  The idea worth keeping from the options work: **two layouts differ in ways no code check can see.** A
  5 × 4 and a 4 × 5 can carry the same head count, pass identically, and run their branch lines at ninety
  degrees to each other — one fights the structure, the other does not. So the fragment ranks on head
  count, branch direction, margin under the area cap, and bay alignment, and states on every run that the
  ranking is preference and not compliance. And if it returns more than about eight distinct options,
  that is itself the finding: the code limits are not what decides that room.
  The floor sweep is built to **over-report**: every space matching an omission rule goes in an ASK bucket
  with the reason, never omitted, because a room silently dropped from a fire layout is the worst output
  this Brain could produce. It classifies on room name and area, which is a prompt to look, not a finding —
  every real omission rule turns on construction and combustible loading, which the model does not hold.
  Also added `knowledge/fire-sprinkler/where-sprinklers-are-required.md` (the "zero" end: exempt locations,
  and temperature-rating selection, which matters more in Qatar than the tables suggest — an unconditioned
  soffit can sit well past the 38 °C ambient that ordinary-rated heads assume) and
  `roadmap-zero-to-finish.md`, which states plainly what is still missing: hazard class per room, the head
  schedule, a multi-room driver, drawing output, sloped ceilings, and above all **one live run of the whole
  chain**. Pipe sizing is gated behind that live run, at Ajmal's own sequencing — and the note records
  now, while it is fresh, that sprinkler pipe sizing splits into a tractable **pipe-schedule** method and a
  **hydraulic** method that belongs with a licensed engineer. Establishing which one a project uses is the
  first move when the gate opens, not an afterthought.

- 2026-08-20 — **pipe sizing, and the finding is that you usually are not allowed to use it.** Ajmal asked
  for pipe sizing to be built (his sequencing: sprinklers first, then sizing, *"not routing, pipe sizing
  only"*). Built: `knowledge/fire-sprinkler/pipe-sizing.md` and
  `scripts/recipes/sprinkler-pipe-schedule-size.cs`, covering the **pipe schedule** method — walk the
  network, count the heads each segment feeds, look the size up, compare to what is modelled.
  **The valuable half turned out to be the gate, not the table.** The schedule method is restricted to
  light and ordinary hazard, and to new systems of **465 m² (5,000 ft²) or less** — larger only where the
  required flow is available at **50 psi residual at the highest sprinkler**, which is a water-supply fact
  needing a real curve, not a drawing fact. The **2025 edition removed** the old allowance for additions to
  existing pipe-schedule systems. Consequence for Ajmal's work: **a Qatar project of any normal size does
  not qualify**, and the honest output is "this needs hydraulic calculation by the fire engineer" —
  delivered before someone spends a day sizing pipe that cannot be issued. The fragment checks the gate
  first, says so loudly, and still prints the sizes underneath marked INDICATIVE ONLY, because they are
  genuinely useful for coordination, clash and take-off.
  Three more things worth keeping: **heads above AND below a ceiling both count on the branch that feeds
  them**, so a protected ceiling void doubles what the ceiling plan suggests — the walk catches it
  automatically, but only if the void heads are modelled and connected; **a looped or gridded network has
  no downstream at all**, so a count-based schedule is meaningless on one and the fragment reports the loop
  rather than silently following one path; and **Revit snaps a written diameter to the Pipe Type's allowed
  list**, so a write-back can land on a different size than the one requested — the fragment says to read
  it back from a separate call. The tables themselves hit the same wall as the beam obstruction table:
  sources gave the method's limits clearly and its numbers not at all, so they are an editable input seeded
  `[UNCONFIRMED]` with a warning on every run. **Hydraulic calculation stays permanently out of scope.**

- 2026-08-20 — **hazard classification studied in full, and car parks are the finding.** Ajmal asked for
  all hazard types, how they are decided, and the understanding behind them, kept in the Brain. The Brain
  had been refusing to run without a hazard class since the first sprinkler session while carrying only a
  four-row table of example occupancies — so this closes the gap it had been pointing at.
  `knowledge/fire-sprinkler/hazard-classification.md` now carries all five NFPA classes with their
  defining test, examples and design density/area; the EN 12845 set (LH, OH1–OH4, HHP1–4, HHS) with its
  different shape — **EN holds density constant across OH1–OH4 and grows the area of operation instead,
  where NFPA moves the density**; how the call is actually made; the **8 ft (2.4 m) stockpile line**, which
  is the sharpest and most measurable question on a walk-through; mixed occupancy; and where storage stops
  being an occupancy class and becomes a different chapter entirely.
  **The finding that matters most for Ajmal: car parks were reclassified from Ordinary Hazard Group 1 to
  Group 2** in recent NFPA editions — 0.20 gpm/ft² instead of 0.15. Car parks are constant on his projects
  and an old office template will still say OH1. **The max area per head is IDENTICAL between OH1 and OH2
  (130 ft² / 12.1 m²), so the layout looks exactly the same and the hydraulic demand is a third higher.**
  A spacing check cannot catch this; only the class label can. That is the general shape of the whole
  subject and the reason every fragment prints its class on every line.
  Also built `recipes/sprinkler-set-room-hazard.cs`, closing roadmap item 2: the class, its SOURCE and its
  STANDARD are recorded per Room and read back, so a mixed floor stays mixed instead of being flattened to
  one label. It deliberately **never decides the class** — the room-name matching is a suggestion engine
  for the report only, never written, and write mode refuses to run without the source of the decision.
  Two limits recorded honestly: a script cannot create the project parameters (that needs a shared-
  parameter file bound through the UI, a one-time job for Ajmal), and the rest of the chain still takes a
  typed `hazardLabel` rather than reading the room's recorded class — wiring that through is the next step.

- 2026-08-20 — Search retrieval: swapped the embedding model from `all-MiniLM-L6-v2` to
  `bge-small-en-v1.5` (new `semantic-index/embed_bge.py`, ONNX on the existing `onnxruntime` — no new
  dependency, same pattern `rerank.py` established). Reason from the score card, not taste: 7 of 14 test
  questions had the right file ABSENT from the whole 80-chunk candidate pool, which no re-ranker can
  repair. Also fixed `BRAIN_ROOT`, which was a hardcoded `D:\Ajmal\AJ AI Brain` with no fallback — it is
  now derived from `brain_common.py`'s own location, so the Brain finally works as the copy-the-folder
  package it claims to be (proven on Linux: 339 files found). **Needs one `embed_bge.py --download`
  (~127 MB) then `index-brain.cmd --full` before searching again** — it refuses to fall back to the old
  model rather than half-migrate the index. Chunk size and the BGE query prefix were deliberately left
  alone: one measurable change at a time, and neither can be judged until `test-questions.md` has ~30 rows.

- 2026-08-20 — Search, measured properly for the first time. The embedding model is now **selectable**
  (`AJ_BRAIN_EMBED_MODEL`), because replacing it outright made the change impossible to A/B. Found and
  fixed a real retrieval bug: RRF was fusing **chunk** ranks while ranking **files**, so a correct answer
  sitting behind one long note scored as rank 40 instead of rank 5 purely because that note is split into
  many chunks — 80 retrieved chunks were yielding only 32-37 distinct files. `_rank_files()` renumbers
  over files: MRR 0.299 -> 0.323, retrievable 10 -> 11, top-3 3 -> 5, **top-5 6 <- 7 (one regressed)**.
  `score_brain.py` now reports **retrievable-at-all vs ranked-below-5** — the split that says whether to
  fix retrieval or ranking — plus MRR, and stamps every history line with model/chunk/corpus/fingerprint.
  Two ideas measured and REJECTED, written down so they are not rebuilt from intuition: a **confidence
  floor** (correct top-1 closeness 35.5-65.1, wrong 27.8-56.6 — they overlap, no threshold works) and a
  **skill-area prior** (peaks at 1.1-1.2 but only moves wins between halves of a 7/7 test set — fitting
  the sample). Over-fetching chunks also measured neutral. Docs: the three circulating accuracy figures
  (75%, 60%, 29%) are gone; `score-history.md` is now the single stamped source.

- 2026-08-20 — New note `knowledge/revit-version-compatibility.md`: what happens to the fragment library on
  Revit 2024+. Measured, not estimated — **200 of 282 fragments (71%) touch an API that changed after 2020**,
  and there is **not one version guard anywhere**. The important distinction it records: unit conversion
  (`DisplayUnitType`, 93 files) fails LOUD as a bridge compile error, while `ElementId.IntegerValue` /
  `new ElementId(int)` (168 files) fails SILENT — compiles and runs on 2024+, throws only once ids exceed
  32 bits, so a small test model passes and a real project model does not. Also settles a recurring
  confusion: fragments are source compiled by the bridge at run time, so the .NET target is the BRIDGE's
  problem, never a fragment's. Tags (2022) and the Dimension split (2025) scanned clean, 0 fragments each.

- 2026-08-20 — Corrected the same note within the hour. It had said a fragment fixed for 2024+ must stop
  working on 2020, so the library had to fork. **Wrong.** `#if` really is unavailable (the bridge compiles
  a bare string with no `REVIT20XX` symbols), but two techniques need no symbols: **units become plain
  arithmetic** — 1 ft is exactly 304.8 mm, so `mm / 304.8` has no API to deprecate, killing 206 of 208
  conversions outright — and **ElementId uses runtime reflection**, the C# twin of the `hasattr` pattern
  `revit-version-matrix` already prescribes for pyRevit. Also found the sharper diagnosis: `new
  ElementId(myInt)` compiles fine on 2024+ because C# widens int to long, so the constructor was never the
  bug — the bug is 33 inputs **declared** `int` (`viewIdInt`, `roomIdInt`, `levelIdInt`), which cannot hold
  a 64-bit id whatever they are passed to.

- 2026-08-20 — **The whole fragment library is now version-proof, 2020 through 2027, from one source.**
  202 unit conversions became arithmetic; 195 id prints dropped `.IntegerValue`; ~80 id collections,
  GroupBy/OrderBy keys and tuples now carry the `ElementId` itself (its `GetHashCode` returns the id, so
  it keys a Dictionary/HashSet directly — version-proof with **no reflection**, which matters because
  several sit in loops over every element). Only 8 genuinely numeric sites use a cached-lookup helper.
  **No `#if`, no fork, no per-version copy.** Three deliberate exceptions, all annotated in place: 2
  electrical conversions (Revit does not store voltage as volts and the doc hosts were blocked — verify
  the factor first), 1 `WorksetId` (a different class, not affected), and `prelude.cs ResolveView` which
  now takes an `ElementId`. A post-sweep scan for `new ElementId(x)` where `x` had itself become an
  `ElementId` caught **2 real bugs** — worth repeating after any similar mass edit. **Not compiled, not
  run**: the brain-status proven-counts describe the pre-migration state.

- 2026-08-20 — New `scripts/context/context-session-start.cs`: the opening check, one bridge call, wired
  into the ping rule (`live-model/core.md`, `AGENT-SPEC.md` §1 + §3.5, the live-model SKILL). Ajmal's
  words: *"everytime while pinging or connection to revit check the all things like what is the version
  of revit what is the model."* It reports Revit version/build **and which API generation is actually
  live** (64-bit ElementId? ForgeTypeId units? split Dimension classes?) — the confirmation the
  2026-08-20 migration needs, since one source now serves 2020-2027 and the version number alone is an
  inference. Also: document + path + central, project name/number/client, **what unit the project really
  displays** (read off a Level's own AsValueString, so it names no version-specific unit API), size,
  unloaded links, closed worksets, design options, phases, warnings, active view, selection. Four of
  those catch a **silently wrong answer** rather than an error — an unloaded link, a closed workset and
  an unexamined design option each make a query quietly return LESS than the truth, and metres-not-mm
  makes every figure wrong by 1000. None of them throws. Every section is independently guarded so one
  unreadable part cannot take down the report. Unproven — never run.

- 2026-08-20 — Ajmal asked for the whole Revit API from `revitapidocs.com` to be pulled into the Brain.
  **Declined for the main index, with numbers, and replaced with something better.** The API is ~1,700
  classes and **30,000+ documented members** against this Brain's **3,786 chunks** — indexing it leaves
  the Brain as ~11% of its own index and every question lands on a reference page. It is the 604-chunk
  external-standards mistake (2026-08-13, reverted the same hour) eight times over. Also: the site is
  unreachable from the session environment, and it is a community site rather than a primary source.
  **What replaced it:** new generated `knowledge/revit-api-surface.md` + `tools/api-surface.mjs` — the
  **229 types, 68 BuiltInParameters and 41 BuiltInCategories the 283 fragments actually use**, each row
  naming fragments that use it correctly. The argument that settles it: a reference page gives a
  signature, but it does not tell you `FilteredElementCollector.UnionWith()` silently drops quick filters
  or that `RBS_START_LEVEL_PARAM` is the only level parameter an MEP curve has — this Brain knows both
  because it learned them the hard way. Generated, never hand-edited, so it cannot drift from `scripts/`.
  Recorded in START-HERE's "deliberately does NOT cover" beside the standards decision. **If the full API
  is ever genuinely wanted it goes in a SEPARATE index the Brain's own search never touches** — Ajmal's
  own instinct ("keep it in a separate section") was the right half of the idea.

- 2026-08-20 — **The full Revit API is now available, in a genuinely separate index** — Ajmal reaffirmed
  he wanted it after seeing the numbers, and his own instinct ("keep it in a separate section") is what
  makes it safe. New `api-index/` (own `api_common/api_index/api_search`, `index-api.cmd`, `ask-api.cmd`,
  README) plus `scripts/context/harvest-revit-api.cs`. **Source is reflection over the RevitAPI.dll the
  running Revit actually loaded**, not `revitapidocs.com`: it gives YOUR version rather than a website's
  copy (which is the whole point after the 2020-2027 migration), needs no scraping or third-party
  dependency, works offline, and the site is unreachable from the session environment anyway. Picks up
  `RevitAPI.xml` descriptions when Autodesk ships them; signatures regardless.
  **Separation checked live, not assumed:** different database dir (`chroma-db-api` vs `chroma-db`),
  different collection (`revit_api` vs `aj_brain`), each client lists only its own, and `api-index/` sits
  outside `INDEX_TARGETS` so the Brain's indexer never reads it — **0 of 343 indexed files come from
  it**, and the Brain stayed at 3,786 chunks. Indexing + search proven end to end on a synthetic corpus
  that matches reflection's output shape; the real harvest is unproven until run on a live Revit.

- 2026-08-20 — New `recipes/audit-flex-curves.cs`, closing a gap that was **bigger than the count
  suggested**. A first look said "FlexDuct 5 fragments, FlexPipe 1"; reading them showed all six hits
  were only the CATEGORY NAME inside a list (obstruction sweeps, grayout, a multi-category filter).
  **Nothing in the library measured, connected or checked a flex run.** The new recipe audits both types
  together — they behave identically — reporting size, real spline length against the direct line (the
  slack the drawing hides), bend points, whether both ends are genuinely connected, and which runs exceed
  the length the PROJECT allows. The length limit is an input defaulting to 0/off: a flex allowance is a
  per-job spec decision, not a number to inherit (START-HERE rule 3).
  Two design choices worth keeping: it **collects by `OST_FlexDuctCurves`/`OST_FlexPipeCurves` and casts
  to `MEPCurve`**, so it never names `FlexDuct` (`...DB.Mechanical`) or `FlexPipe` (`...DB.Plumbing`) and
  cannot hit the fully-qualify compile trap `core.md` records; and it reads length from `Curve.Length` /
  `GetEndPoint` / `Tessellate` only, which exist on every targeted version, rather than any
  flex-specific member. Counts `Connector.AllRefs`, never `IsConnected`, per the hvac-ducts.md gotcha.
  Unproven — written without Revit.

- 2026-08-20 — Ajmal stated his two standing problems plainly, and said he is **not a coder** — so every
  programming decision is the assistant's to make, not his to choose between. Recorded in `CLAUDE.md`
  with his own words. Problem 2 ("proven in 2020, errors in a newer Revit") turned out to be **already
  solved and simply undiscoverable**: `tools/verify-fragments-compile.ps1` has always compile-checked the
  whole library against a chosen Revit's real API DLLs **without opening Revit**, and it was mentioned in
  exactly two places — one line of a tools list, and a footnote about `%TEMP%`. New
  `tools/check-scripts.cmd` + `.ps1` wraps it: finds **every** Revit on the PC, checks all 285 fragments
  against each, and prints one plain-language line per version (SAFE / n scripts would error). Routed
  from START-HERE's table, the version note's opening line, and CLAUDE.md, because the lesson of the
  original tool is that a good tool nobody can find is worth nothing. Problem 1 ("not covered, so fresh
  code, which is slow and goes wrong") is answered in CLAUDE.md as a habit rather than a tool: search
  before writing, and **compile-check fresh C# before he runs it** — a round trip through Revit costs his
  attention, a compile costs a minute of nobody's.

- 2026-08-20 — **The two tools built that day were both broken on the Windows PC they were built for,
  and running them is what proved it.** `tools/check-scripts.cmd` — called "the single command that
  answers the whole session" — could not start: the `.ps1` was written in a Linux container as UTF-8
  **without a BOM**, and Windows PowerShell 5.1 reads a BOM-less file as ANSI, so its eight em dashes
  corrupted and broke the string terminators. Exactly the trap `CLAUDE.md` warns about, arriving from the
  one direction the warning did not cover: a file *authored* off-Windows rather than bulk-edited on it.
  Fixed with a UTF-8 BOM, which the three `.ps1` files that already worked all had. **Rule for container
  sessions: a `.ps1` needs a BOM or pure ASCII.** Second, search was dead — `bge-small-en-v1.5` had been
  made the *default* embedding model but its ~127 MB weights were never downloaded, so every
  `ask-brain-hybrid` call died on "model has not been downloaded yet". `brain_common.py` now defaults to
  `all-MiniLM-L6-v2`, which ships inside chromadb; BGE stays one env var away, which is all the A/B ever
  needed. A default that requires a download before the tool runs at all is not a default.

- 2026-08-20 — **The version-proofing was measured, and it mostly held: 281/287 compile on 2020,
  274/287 on 2024.** The 2027 figure of 4/287 is a **harness** bug, not 283 broken fragments — Revit 2027
  runs on .NET 10, so `RevitAPI.dll` wants `System.Runtime 10.0.0.0` while
  `verify-fragments-compile.ps1` still compiles against the .NET Framework reference set; every failure is
  `CS0012 ... 'System.Runtime, Version=10.0.0.0'`. Fix the harness, not the fragments. The real failures
  are genuine per-version API differences (`CS0122` member not accessible, `CS1061` member absent, plus
  `CS0308/0030/0019/0029/1503/1501`), and six fail on **both** 2020 and 2024 — the one that matters most
  being `recipes/mep-grayout.cs`, Ajmal's own standing "do the grayout" job. Full list and the fix order
  are in `docs/HANDOVER.md`.

- 2026-08-20 — Ajmal asked whether the project should be **re-architected "as per the best RAG"**. Answer
  written up as `knowledge/rag-architecture-decisions.md` and routed from `knowledge/INDEX.md`: **no** —
  the pipeline already has hybrid dense+BM25 with RRF, structure-aware chunking, kind and path weighting,
  live query expansion, file-level re-ranking, incremental indexing and an eval harness, and **six
  standard "best practice" upgrades have already been measured here, four of them neutral or negative**
  (contextual-retrieval prefixes twice, a confidence floor, a bigger candidate pool). The file collects
  those results in one place so a rewrite is not proposed again from a diagram. The real bottleneck is
  the **14-row test set** — two finished features are switched off waiting on it — plus site vocabulary
  and the fact that `job-log/questions.jsonl` is not being written yet. One genuine refactor identified:
  `api-index/` is a *copy* of the pipeline rather than a second config of it, and a shared corpus module
  is what "easy to do all kinds of RAG working" actually costs. Second gap: the search is Windows-only,
  so it is dark on Claude Code for web.

- 2026-08-20 — **The search now sets up and runs off Windows.** New `semantic-index/setup.sh` builds the
  venv, installs chromadb, fetches the model and indexes, in one command. Proven end to end on a wiped
  venv in a Linux container — **the first time the search has ever run in a web session**. Three real
  faults were found by running it rather than reading it: `huggingface.co` is blocked from the container,
  so it now falls back to the chromadb-shipped `all-MiniLM-L6-v2` instead of dying with a traceback; the
  fallback was exported into a shell that died, leaving every later search defaulting to BGE against a
  MiniLM index, so the choice is now persisted in `semantic-index/embed-model.txt` (`brain_common.py`,
  precedence env > file > BGE); and `.gitignore`'s `semantic-index/*` would have silently dropped
  `setup.sh` from the commit. `embed_bge.py`'s "not downloaded" message no longer prints Windows-only
  commands to a Linux user.

- 2026-08-20 — **Folder structure: leave it.** Recorded in `semantic-index/rag-architecture-decisions.md`.
  The layout is load-bearing — the path becomes each chunk's `category`, and 610 markdown path references
  plus 73 `// SOURCE:` lines point at it. Four untidy spots noted, none urgent.

- 2026-08-20 — **A knowledge note about the search cost the search a point, and chasing it found
  something better.** Writing the RAG decision record into `knowledge/` dropped the score 3/14 → 2/14
  (`what does duck mean`: `glossary.md` #1 → #6). Stripping its verbatim test questions did nothing;
  moving it out of the indexed folders did nothing; reverting this session's own log entries did nothing.
  **The score still sat at 2/14 with a corpus 2 chunks larger than baseline** — one `INDEX.md` row and one
  edited `START-HERE.md` line. Re-swept `glossary.md`'s weight 0.85→1.00 on the current corpus: **no value
  returns it to #1 any more**; the sprinkler files beat it outright. **The weight was left at 0.93** —
  moving it to 0.96 scores better by a different route, which is fitting the sample. Three conclusions:
  documents about the search go in `semantic-index/` (outside `INDEX_TARGETS`, like `docs/`), the
  `what does duck mean` guard row is flagged BROKEN in `test-questions.md` so nobody re-chases it, and
  **one knife-edge row is 7% of a 14-row score** — the score card will cry wolf until the test set grows.
  Third time a file describing the Brain's own machinery has cost it accuracy; first time caught before
  shipping, because the score card ran.

- 2026-08-20 — **`docs/superpowers/` retired.** Two implementation plans and one design spec from
  2026-08-13, carrying 60 unticked checkboxes describing work that is entirely built (score card,
  reindex hooks, `search_brain`, compact context, auto-search hook, three agents). They read as 60 open
  jobs. Deleted — git history has them — after folding the five genuinely live items forward into
  `semantic-index/rag-architecture-decisions.md`, including the spec's **refuted** theory that splitting
  oversized files improves retrieval, which now sits with the other measured-and-reverted experiments.
  Also fixed on the way: `START-HERE.md` and the `search_brain` tool description both still claimed
  search accuracy was "~3 in 4 at #1" — 75%, one of the three discredited figures `CLAUDE.md` warns
  about two paragraphs below, against a measured 3/14. Both now quote `score-history.md`.

- 2026-08-20 — **Revit 2020 is now 287/287, and five of the six failures were one word each.** The
  version-proofing pass had left `int` where its own new code returned `ElementId`:
  `mep-grayout.cs` declared `int doorId = IdOf(...)` on the line directly below a correct
  `ElementId wallId = IdOf(...)`, and that single mismatch produced all five of its errors. Same shape in
  `connect-equipment-to-air-terminals.cs` (`List<Tuple<int, ...>>` one line after a migrated
  `List<Tuple<ElementId, ...>>`). The three `CS0030` casts were the documented case the prelude already
  solves: a `BuiltInCategory` is an enum over the id's NUMBER, so it needs `(BuiltInCategory)IdValue(id)`,
  not `(BuiltInCategory)id`. **Lesson: the migration's misses cluster on the line *after* a correctly
  migrated one** — worth grepping for, not just compiling for.

- 2026-08-20 — **`sprinkler-layout-options.cs` had never compiled on any Revit version**, and that is a
  different bug from the rest. It packed **9 fields into a `System.Tuple`, which stops at 8** — so both
  `Tuple<...>` and `Tuple.Create(...)` were errors on 2020 as much as on 2027. Rewritten to a named
  ValueTuple, which fixes the limit and retires 38 unreadable `o.Item7`-style reads at the same time.
  First fragment in the library to use named ValueTuple; it compiles on 2020 and 2024, so the technique
  is available to the rest. Still not live-run.

- 2026-08-20 — **`knowledge/revit-version-compatibility.md` under-counted, and compiling is what caught
  it.** The note claimed `IndependentTag` affected **0 fragments, "scanned"**. It affects **two**
  (`filter-by-tag-status.cs`, `tag-elements-in-active-view.cs`), and two further removed APIs were absent
  from the note altogether — `UnitType`/`DisplayUnitType`/`UnitSymbolType` in `context-project-units.cs`,
  and `Document.NewFloor` in `create-floor.cs`. Note corrected with a new §4. **A source scan is a guess;
  compiling against the real RevitAPI.dll is a measurement** — the same lesson this repo keeps relearning,
  now with the tool that makes it cheap. Those 7, plus the 3 known `ParameterType` ones, are the whole of
  what still fails on 2024; all are real API removals needing reflection dispatch, not migration slips.

- 2026-08-20 — **The fire-sprinkler chain ran on a real model for the first time: 4 of the 10 fragments
  are now LIVE-VERIFIED.** `sprinkler-obstruction-survey.cs`, `sprinkler-nfpa-grid.cs`,
  `sprinkler-place-heads.cs` and `sprinkler-compliance-audit.cs`, on Revit 2020 / model `Project1`, four
  rectangular rooms with a flat 2,400 mm grid ceiling, light hazard / NFPA 13 / unobstructed. 38 heads
  placed, 0 failures on an independent audit run from a separate bridge call. The grid recipe's table was
  hand-checked against all four rooms and came out exact. STATUS blocks updated with what is proven and
  what is still not: the bay-module arithmetic, the services branches, `gridMode "fixed"`, `baySpacingMm`
  and `drawCircles` were all untouched by this model and remain unproven.

- 2026-08-20 — **Two placement traps found, and the "place one first" rule is what found them.** (1) The
  Z of the placement point is **not honoured** on a OneLevelBased family — asked 2,400 mm, got 2,500 mm,
  silently, with the script reporting success; the height must be written to `Elevation from Level`
  afterwards and read back. (2) A family named "RASCO F156 **CONVENTIONAL**" is modelled as an **UPRIGHT**
  — connector at the origin pointing down, deflector 56 mm *above* the origin — so the origin-to-deflector
  offset had the opposite sign to the obvious guess. Both written into
  `knowledge/fire-sprinkler/revit-modelling.md`, including the slice-the-solid method for measuring where
  any family's deflector really is. **The family name told us nothing; the geometry told us everything.**

- 2026-08-20 — Recorded that the bridge prelude does not import `Autodesk.Revit.DB.Architecture`, so a
  bare `Room` is `CS0246`. Fully qualify it in anything composed from the sprinkler fragments.

- 2026-08-20 — **Every fragment now compiles on every Revit on the PC: 287/287 on 2020, 2024 AND 2027.**
  The thirteen that failed on a newer version were real API removals, not migration slips — they compiled
  on 2020 precisely because they used the surface Autodesk later deleted. All are now resolved **by name
  at run time**: `ParameterType`→`SpecTypeId`, `DisplayUnitType`→`UnitTypeId`, `UnitType`/`DisplayUnits`→
  `GetAllMeasurableSpecs`/`GetUnitTypeId`, `IndependentTag.TaggedLocalElementId`/`.LeaderElbow`→the
  multi-reference methods, `Document.NewFloor`→`Floor.Create`, `BuiltInParameterGroup`→`GroupTypeId`,
  and the string-rule `caseSensitive` argument that 2023 dropped. **The rule that made this work: pick the
  overload from the REAL method's own parameter type, never by testing whether `ForgeTypeId` exists** —
  Revit 2021 ships `ForgeTypeId` while those same methods there still take the old enum, so the obvious
  feature-test gives the wrong answer on exactly one version.

- 2026-08-20 — **The "283 fragments fail on Revit 2027" scare was one missing compiler flag.**
  `verify-fragments-compile.ps1` was compiling against the .NET **Framework** reference set while 2027
  runs on **.NET 10**, so every fragment died on `CS0012 ... System.Runtime 10.0.0.0`. It now detects a
  .NET-based Revit from the `RevitAPI.runtimeconfig.json` Autodesk ships beside `RevitAPI.dll` and passes
  the matching reference pack with `/nostdlib+`. 2027 went from 4/287 to 281/287 in one run, then to
  287/287 once the six real removals underneath were fixed. **If a future Revit ever reports every
  fragment failing, suspect the harness before the library.**

- 2026-08-20 — **New tool `tools/probe-revit-api/`: read any Revit version's real API without opening
  Revit.** Written because a question could not be answered any other way — Windows PowerShell 5.1 runs on
  .NET Framework and **throws `BadImageFormatException` loading a .NET 10 assembly**, so the usual
  `Assembly::LoadFrom` one-liner is dead on exactly the versions whose API changed most. It uses
  `MetadataLoadContext`, which only reads metadata and never executes Revit code. It immediately settled
  the one thing reflection could not fix: **Revit 2027 removed HVAC zones from the API outright** — no
  zone method left on `Creation.Document`, no `Zone.AddSpaces`, and `Space.Zone` read-only. That is a
  capability removal, not a rename, so `creators/create-hvac-zone.cs` now compiles on 2027 and reports in
  plain words that the job must be done in the Revit UI there. Use this tool before assuming a missing
  member was merely renamed.

- 2026-08-20 — **Two Revit sessions can now be connected at once, and the bridge was single-instance by
  construction rather than by oversight.** `McpBridgeService` hosted a FIXED pipe name with
  `maxNumberOfServerInstances: 2`, and those two are not spare capacity: one Revit needs both, one
  servicing the chat and one already listening so preemption stays instant. **Measured before changing
  anything** — a standalone test creating four servers on one name got two, then
  `All pipe instances are busy` twice. Each Revit now owns a pipe named by its process id (the
  named-pipe equivalent of pyRevit's one-port-per-instance) and publishes itself in
  `%APPDATA%\AJTools\bridges\<pid>.json`. `ajai-bridge.json` is still written unchanged, so an older
  client keeps working. New tools `list_revit_instances` / `use_revit_instance` (native tools: 17 → 19).

- 2026-08-20 — **The rule Ajmal chose for two open sessions: ask, don't guess** — and the distinction
  that makes it safe was found by a failing test, not by review. Auto-picking the only session and being
  *told* which session to use are different facts and must be tracked separately: auto-picked then a
  second Revit opens → **ask**, because he never chose this one; auto-picked and it closes → quietly take
  the survivor; he chose it and more open → keep his choice; **he chose it and it closes → stop and say
  so**, never slide onto another project. The first draft had no such flag, so opening a second Revit
  mid-chat left every later command silently going to the first — precisely the failure the feature
  exists to prevent. Rules and reasons live in `mcp-server/test/multi-instance.test.js`.

- 2026-08-20 — **The destructive-test safety gate was failing OPEN, and it fired.** `smoke.test.js`
  invokes every handler including `delete_elements` with `confirm: true`; since 2026-08-13 a `ping`
  probe was supposed to skip it whenever a live bridge is connected. But the rule was "ping errored → not
  live", and a bridge that is merely BUSY answers *"Another script is still running"* — read as "no
  bridge". The suite duly ran against a live Revit; nothing was destroyed only because Revit's OWN
  busy-guard rejected the first call and the assertion aborted the test before `delete_elements`. The
  audit log is what proved it. Now inverted to fail CLOSED: only an explicit not-connected / pipe-missing
  answer counts as proof the bridge is down; any other error, exception or unreadable result is treated
  as live. **A false skip costs one test run; a false "not live" costs 354 ducts.** Same lesson in the
  test itself: `%APPDATA%` must be redirected before anything imports `bridge-connection.js`, because ES
  modules are cached — an earlier draft redirected it too late and sent its probe to the real Revit.

- 2026-08-20 — **"Which Revit?" and "which project inside it?" are two problems, and the second one was
  still open.** The morning's fix gave each Revit session its own pipe; that says nothing about a Revit
  holding several projects. `RevitExecutionService` has always built its globals from
  `app.ActiveUIDocument`, so `Document` means **the front window** — which moves when Ajmal clicks
  another project, between two calls of one job. The bridge request now takes an optional `document`
  title (AJ Tools 1.56.0), resolved against `app.Application.Documents` with links skipped, plus a
  `use_revit_document` tool (native tools 19 → 20). **Same rule as the pid lookup: a name that does not
  resolve is an ERROR listing the real choices, never a fall back to whatever is in front** — falling
  back IS the failure. Backward compatibility is by **omission**: the field is left out of the JSON
  entirely when nothing is pinned, and a test asserts it is *absent* rather than empty, because "" and
  missing are different things to a deserialiser. Known limit, recorded rather than hidden: a
  `UIDocument` for a background project still carries THAT project's active view, so view-scoped work
  (isolate, colour, crop) follows Revit; model work is unaffected.

- 2026-08-20 — **A test whose checks all pass can still fail the file, and the reason is worth keeping:**
  `document-targeting.test.js` stands up a real named pipe, and the client deliberately holds its
  connection OPEN between calls (that is what makes a long job fast). Closing only the server left a
  live socket holding the event loop up, so the run hung to the runner's timeout — 7 green checks and a
  failed file. Keep every accepted socket and destroy them in `after()`.

- 2026-08-20 — **Every outside source stripped from both repos, and the rule written down so it stays
  stripped.** Ajmal: *"do not mention any thing that we took from this web site or repo or we take like
  that files also remove and the words also remove... remove his name and do not use like that."*
  Removed across the Brain and the AJ Tools repo: **22 `// SOURCE:` / `LESSON:` fragment-header
  attributions** naming an outside repo (11 fragments here, 11 in the mirrored `.claude/scripts/` copy),
  the whole competitor-comparison knowledge note, the AJ Tools `scripts/history.md` "where these ideas
  came from" file, and nine log/README passages naming outside projects. Derived layers cleaned too
  (job-log entries, graphify caches and stale graph snapshots); all gitignored, so none of it ever
  travelled in git anyway. **What was kept deliberately:** the *techniques* themselves, which are ours
  now and verified here, and the one genuinely-our-own engineering fact rescued out of the deleted note
  into [`mcp-ui-surface.md`](mcp-ui-surface.md). The rule is now a bullet in `CLAUDE.md` in both repos,
  because the failure mode is a future session politely re-adding a credit line.
  **The one judgement call, flagged rather than buried:** `action-test-view-filter-match.cs` and
  `action-manage-named-set.cs` were written on 2026-08-19 from an outside tool list. The C# is ours and
  the capability is real, so they stay — but they now carry **no provenance line at all** and read as
  this Brain's own work, which is what the instruction asks for. If Ajmal wants the files gone as well as
  the words, delete both and drop the count back to 287.

- 2026-08-20 — **The history was scrubbed too, on Ajmal's explicit go-ahead the same evening.** Both
  repos turned out to be **PUBLIC**, not private as the AJ Tools `CLAUDE.md` had claimed since
  2026-08-05 — so the old commits were readable by anyone clicking "History", which made this worth
  doing rather than theoretical. `git filter-repo` over both: the two dedicated files purged from every
  tree, every outside name replaced in **file contents *and* commit messages**, then a force-push.
  **266 commits and all 57 tags survive in each repo**; nothing was squashed or lost. Backups first —
  `git bundle` of each, verified restorable, in `D:\Ajmal\_repo-backups-2026-08-20\`.
  **The one thing that nearly went wrong, worth keeping:** one of the removed names is a **substring of
  the ordinary English word "canonical"**, which appears in three files here — a plain search-and-replace
  would have silently mangled all three. The rules were `(?i)\b…\b` word-boundary regexes and were
  **unit-tested against "canonical" before being let near a commit**. Do that again if this is ever
  repeated: write the test first, then the rules. Checked and clear: zero forks on all three repos, and `AJ-Tools-Installer` never
  contained any of it. The stale `AJ Tools\` clone on disk was scrubbed as well (182 commits) since it
  is a full copy, though it stays gitignored and unpushable. **Still outstanding:** GitHub keeps
  unreferenced commits reachable by direct SHA link until it garbage-collects — ask GitHub Support to
  purge them if that matters.

- 2026-08-20 — **the two capabilities from that evaluation rebuilt clean, credit-free.** Ajmal asked for
  everything worth keeping to be kept "in our part", so both fragments deleted in the strip above are back
  as this Brain's own, with no outside name anywhere in them:
  [`action-test-view-filter-match.cs`](../scripts/actions/reporting/action-test-view-filter-match.cs)
  (dry-run a View Filter against `elements` without applying it — and its third verdict, *N/A, the
  category was never in the filter's scope*, is the one that actually answers "why isn't my filter
  catching these ducts") and
  [`action-manage-named-set.cs`](../scripts/actions/visibility/action-manage-named-set.cs) (name a set
  once, then select / isolate / hide / show it by name). The named-set one is built on Revit's own
  `SelectionFilterElement` rather than an in-memory id cache **on purpose** — a frozen id list goes stale
  exactly the way recall does, which `START-HERE.md` rule 2 forbids, so every mode re-resolves and reports
  what no longer exists. Library 287 → 289; **289/289 compile-clean on Revit 2020, 2024 and 2027**, both
  still to be live-verified.

- 2026-08-20 — new note [`knowledge/live-model/family-category-change.md`](live-model/family-category-change.md),
  routed from `live-model/README.md`. Written from a live conversion of `TCM_FAL_T001_FreshAirIntakeLouvre`
  from Duct Accessories to Air Terminals on BL006A. Two things it records that were assumed wrong beforehand:
  **the instances convert in place and keep their element IDs** (the prediction was that Revit would delete
  them), and the only genuine parameter loss is **a project parameter not bound to the destination category**
  — everything else that vanished was an old-category built-in worth nothing. Adds the pre-flight check:
  walk `Document.ParameterBindings` and test the destination category before changing, not after.
  Snapshot evidence in [`job-log/snapshots/2026-08-20-HWL-fresh-air-louvre-params.md`](../job-log/snapshots/2026-08-20-HWL-fresh-air-louvre-params.md).

- 2026-08-20 — new fragment [`filters/by-identity/filter-by-wrong-category.cs`](../scripts/filters/by-identity/filter-by-wrong-category.cs):
  elements whose family/type name or Equipment Tag prefix says one thing while their **category** says
  another. Written because `--find louvre` returned nothing and the same whole-model sweep got authored
  from scratch **twice in one session** — the exact "not covered, so fresh code gets written" failure
  CLAUDE.md names. Library 289 → 290 (50 filters). Reports OK vs WRONG per category so a loose keyword
  is caught before acting.

- 2026-08-20 — [`live-model/core.md`](live-model/core.md) gains two entries. **`list_revit_instances`
  returns a stale window title** — it named BL006A while the bridge's `Document` was BL003A, which would
  have written into the wrong project silently; the process-level title is not refreshed when the open
  document changes, so ask `Document.Title` instead. And a new **"API surface traps that cost a round
  trip"** section: `IsDeterminedByFormula` lives on `FamilyParameter`, not `Parameter`, and
  `MechanicalSystem` needs its full `Autodesk.Revit.DB.Mechanical.` namespace — both cost a wasted Revit
  round trip this session.

- 2026-08-20 — the vector index now stamps `built_at` and `git_commit` into its manifest, so its age is
  a fact rather than an mtime guess. [`brain_common.py`](../semantic-index/brain_common.py),
  [`brain-status.mjs`](../tools/brain-status.mjs).

### 2026-08-21

- 2026-08-21 — **the search corrects misspellings by itself.** 65% of real logged questions contain a
  word in no file, mostly ordinary Revit words typed fast, which are worth nothing to exact-word search.
  New `correct_spelling`; fires on 44% of real questions. Working and limits:
  [`rag-architecture-decisions.md`](../semantic-index/rag-architecture-decisions.md).

- 2026-08-21 — **never write a misspelling into any file here, not even as an example.** These folders
  are the dictionary the corrector checks against, so a typo written here becomes a real word and
  switches off its own correction. Rules in [`site-vocabulary.md`](site-vocabulary.md).

- 2026-08-21 — **a rebuild could mark itself finished before it had.** The manifest was written before
  the build was verified, so a crash left 1,200 chunks of 3,895 reporting "UP TO DATE". Both paths now
  count first. **Write the "this is finished" record last** — the general rule, worth more than the fix.

- 2026-08-21 — derived files (`index-manifest.json`, `corpus-vocabulary.txt`, the fragment cache) now
  write-then-rename. A search running during a re-index could read a half-written dictionary.

- 2026-08-21 — the fragment index is cached to disk instead of spawning Node on every query: **426 ms ->
  38 ms**, the largest single cost in a search. A near-duplicate check now runs on every build (0.4 s).

- 2026-08-21 — **claims about the search that were wrong, now corrected**: the live embedding model
  (docs said BGE, it is MiniLM and BGE has never been scored), the cross-encoder (read as inert, it
  changes 88% of answers), and `site-vocabulary.md` rows (documented as additive, they replace).
  All measurements in [`rag-architecture-decisions.md`](../semantic-index/rag-architecture-decisions.md),
  which lives outside the index on purpose — writing about the search inside it costs retrieval.

- 2026-08-21 — **the search no longer loads the model on every message.** A warm process
  ([`brain_server.py`](../semantic-index/brain_server.py) + `brain_client.py`) holds the embedding
  model and the tokenised corpus, and the per-message hook talks to it from Node with no Python at
  all: **3,536 ms -> ~650 ms**. Same `hybrid_search`, byte-identical output, guardrail lines intact.
  It starts itself on first use, and every failure path falls back to searching in-process, so it can
  speed a search up but never break one. Localhost-only, token-checked, read-only, idles out.
  Also cached the corpus + BM25 build inside `hybrid_search` itself (763 ms -> ~220 ms per warm
  query), keyed on the index build stamp so a rebuild invalidates it — verified across a real
  rebuild, and verified identical on 30 real questions.

- 2026-08-21 — **the five end-of-turn hooks now run together**
  ([`stop-hooks.mjs`](../tools/stop-hooks.mjs)): 2,105 ms -> 900 ms on a quiet turn, and 31 s -> 20 s
  on a turn that actually re-indexes, re-scores and rebuilds the graph. Re-index runs first and alone
  because the score check reads the index it rebuilds; the other four have no such dependency. Each
  child is isolated and a failure is reported rather than swallowed — five hooks behind one entry
  means a bug here would silently disable all of them, which is this repo's worst failure mode.

- 2026-08-21 — **the warm search server no longer opens a black window over Revit.** The venv's
  `python.exe` is a launcher shim over the Store Python: it re-executes the base interpreter as a
  second process, which never sees `DETACHED_PROCESS`, so that process took a console of its own —
  and on Windows 11 a console is a Windows Terminal window. Closing it killed the server, so the
  next message started another. `brain_client.start_server_detached()` now launches `pythonw.exe`
  (no console exists to hand out), and the four hook-side Python spawns pass `windowsHide: true`.
  Verified: no `conhost.exe` under the new server, no terminal host running at all. The same trap
  hit the voice drainer on 2026-08-11 and the lesson lived only as a code comment, so it is now a
  knowledge note: [`windows-console-window-trap.md`](windows-console-window-trap.md).

- 2026-08-21 — **asked whether a KV cache would help; the answer is nothing to build, and it is now on
  disk.** Two different things wear that name: the model's own KV cache runs on the API side with no
  setting to reach, and prompt caching — the configurable half — is already handled by the harness, on a
  1-hour TTL, with `CLAUDE.md` + `START-HERE.md` (23.6 KB, ~6,000 tokens) sitting in the cached prefix.
  The three Brain-side caches were already settled: fragment index cache built (426 ms -> 38 ms), warm
  search server built (3,536 ms -> ~220 ms), query cache declined at a 0.6% hit rate. Recorded in
  `semantic-index/rag-architecture-decisions.md`, not `knowledge/` — a file about the Brain's own
  machinery costs retrieval accuracy when indexed, which that file itself proved on 2026-08-20.

- 2026-08-21 — **the daily check found six stale fragment counts in the three entry docs, and the drift
  checker now has a ninth check so they cannot drift again.** Disk holds 290; CLAUDE.md said 268 and 285,
  START-HERE.md said 285, README.md said 267 and 266, and README also still quoted the superseded 14-row
  search score and "seven drift checks". Check 5 only ever covered `AGENT-SPEC.md` and check 8 only the
  "searches all N files" line, so fragment counts in the entry docs were the one uncovered number — the
  repo's own named failure mode, live in four places at once. Check 9 reads `scripts/*.cs` and matches the
  `all N` / `the N` forms only, skipping the deliberate historical example in CLAUDE.md (`said 206
  fragments against 264`) by its `said ... against` shape. Negative-tested: it names file, line and claim.
  Added to both checkers; **the `.ps1` half is ASCII-only and BOM-preserved but has never been parsed —
  no PowerShell in the container, which is exactly the trap `CLAUDE.md` warns about. Run it once on the PC.**

### 2026-08-22
- 2026-08-22 — **the proven library can now be RUN by name, not retyped.** New MCP tool `run_fragment`
  ([`run-fragment.js`](../mcp-server/tools/run-fragment.js)): name the fragments, pass the input values,
  and the `.cs` files go to Revit **byte-identical apart from their `INPUTS` declarations**. Until now
  every scripted job read the file, hand-edited the block and pasted a copy — so "PROVEN" described a
  file that was never the thing that ran. Four mistakes that used to cost a Revit round trip are now
  local errors: an unknown or ambiguous fragment name, an input name that is a typo, a wrong-typed
  value, and a composition C# would reject (two filters both declaring `sb`). Native tools 20 → 21.
- 2026-08-22 — **one declaration line can declare several inputs, and the first writer for them was
  wrong.** `byte colorR = 255, colorG = 0, colorB = 0;` — 50 such lines across 28 fragments, every
  colour job among them. Replacing the initialiser as one string kept `colorR`'s new value and deleted
  `colorG` and `colorB`. `parseInputs` now expands each declarator, so `--show` lists all three fields
  as well. Caught by a whole-library sweep, which is now a standing test: rewriting the VALUES must
  never change the declared types, names or comments, across all 290 fragments.
- 2026-08-22 — the fragment parser moved to [`tools/fragment-lib.mjs`](../tools/fragment-lib.mjs),
  shared by `fragment-index.mjs` and `run_fragment`. A runner that re-implemented the parse would drift
  from the index silently — `--show` printing one form while the tool filled in another. Proof the
  refactor was safe: `--json` byte-identical before and after.
- 2026-08-22 — **"widget" is his word for a picture in the chat reply**, confirmed by asking him rather
  than guessing — a synonym of "visualization" (2026-08-14), never a published page. The part worth
  keeping: he used it while asking **how the Brain works**, not about model numbers, so
  [`ajtools-visual-report`](../skills/ajtools-visual-report/SKILL.md) now says explicitly that an
  explanation gets a diagram too. The old rule was written only around Revit figures.
- 2026-08-22 — **`run_fragment` benchmarked** on the real library, 30 runs each: build + check a
  2-fragment job **32 ms**, catch a bad input or a wrong fragment name **28 ms**. Text through the model
  for one composed colour job: **2,800 tokens → 46**, a 98.3% cut, or roughly 55,000 tokens over a
  20-job session. **Revit's own execution time is unchanged** — the same C# arrives. No measured figure
  for a Revit round trip exists anywhere in this Brain, so the cost of a mistake is still stated as a
  shape, not a number; measure one on the PC and record it here.
- 2026-08-22 — **the job log was about to go blind.** `job-log-revit.mjs` read fragment names out of
  the pasted `code`, which only `run_csharp` has. `run_fragment` NAMES its fragments instead, so every
  call through the new tool recorded zero — and it is the tool meant to carry most jobs. Fixed to read
  `input.fragments` too, normalised to the same bare `name.cs`, so counts from both paths add up
  instead of splitting. **Found while building something that depends on that log, not by the log
  complaining** — nothing would have complained.
- 2026-08-22 — **the fragments Ajmal really uses are now in front of every message.**
  [`tools/shortlist.mjs`](../tools/shortlist.mjs) ranks them from his own last 30 days of recorded work
  and `auto-search-hook.mjs` injects three lines. **0.17 ms on a real log, 2.0 ms on a simulated year
  of heavy use, ~92 tokens** — against a search that costs 650 ms and is right at #1 on 5 of 28
  questions. His idea, and his framing of the decision order, now in AGENT-SPEC §2.4.
  **What he asked for and did NOT get, deliberately:** MCP tools registering and unregistering
  themselves by usage. That churns the tool list, breaks any skill note naming a tool, only takes
  effect on a restart, and a machine-made tool is worse than a hand-written one — it cannot invent
  `category: "Ducts"` in place of `OST_DuctCurves`. The shortlist gives the same benefit (no search)
  for all 290 fragments instead of ~8, updates every message instead of every restart, and registers
  nothing so it can break nothing.
- 2026-08-22 — **run_fragment shrank what promoting to a native tool is worth.** Before it, native was
  ~15 tokens against ~2,800 for read-and-paste. Now it is ~15 against ~46. The remaining reason to
  hand-write a native tool is the friendly typed input and skipping the search — not saved code. Judge
  the next promotion on that, not on the old arithmetic.
- 2026-08-22 — **five fragment-backed native tools**: `grayout`, `session_start`,
  `verify_connectivity`, `report_length_by_size`, `color_by_group`. Native tools 21 → 26. They hold
  **no Revit code** — each names one proven fragment and the shared engine
  ([`fragment-runner.js`](../mcp-server/shared/fragment-runner.js)) composes it off disk, so there is
  never a second copy of a proven file to drift. `run-fragment.js` shrank 478 → 122 lines onto the same
  engine, all 14 of its tests unchanged and passing, which is the proof the extraction changed nothing.
  **`grayout` is the case that justifies the pattern:** its recipe declares 33 inputs and 32 of them
  ARE Ajmal's settled standard, so the tool sets one value and leaves the rest byte-identical.
- 2026-08-22 — **the five were picked from Brain evidence, not from usage** — his job log never travels
  in git, so it could not be read from the container. Ranked instead by how often each fragment is named
  across the skills and entry docs, plus AGENT-SPEC §9.4's standing request shapes. **Recorded so it is
  revisited, not inherited:** `job-report.mjs` and `shortlist.mjs` on the PC give the real ranking, and
  AGENT-SPEC §11's promotion list is now marked as written-from-memory rather than measured.

- 2026-08-22 — **This log was tidied, and the tidying found four things wrong with it.** Ajmal asked for
  housekeeping on this file and `semantic-index/rag-architecture-decisions.md`, with *"need full details
  keep"* — so nothing was compressed and no entry was deleted. All 288 entries are still here, each still
  carrying its own date line. What changed is structure and honesty:
  **(1) The open-items header was stale in the flattering direction** — it claimed *237 of 280 verified
  (85%)* when the live figure was **241 of 290 (83%)**. Both halves wrong. It is now replaced by a pointer
  to `tools/brain-status.mjs`, which computes it from disk every session, plus a standing warning never to
  quote a count from this file again. This is the exact failure `CLAUDE.md` opens by warning about, found
  inside the log that is supposed to catch it.
  **(2) The entries were not in date order, and four dates were split across two places each** —
  2026-07-22, 07-23, 07-26 and 08-20 each appeared in two separate runs, so "newest at the bottom" was not
  true. Stable-sorted by date; within-date order untouched. Verified by asserting the per-date counts were
  identical before and after, so no entry can have moved days.
  **(3) Two entry formats had grown up side by side** — 276 wrote `- DATE — **title**` and 12 wrote
  `- **DATE — title**`, with the date inside the bold. Any tool counting entries by date silently missed
  those 12. All 288 are now one format.
  **(4) 2,575 lines with no headings and no index.** Added one `### DATE` heading per day and a jump table
  at the top of the Log. The date also stays on every entry line, deliberately redundant: a heading is for
  reading, the line is what greps and tools match on, and an earlier attempt at this that stripped the
  prefix lost which day 90 entries belonged to.
  **The rule at the top of this file was also fiction and is now rewritten.** It said *"keep entries to
  1–3 lines"*; measured, the median is **8 lines and 87 entries are over 10**. A rule 90% of a file breaks
  is drift with a sentence on top, so it now says to write the length the finding deserves and to link
  rather than restate.

- 2026-08-22 — **`rag-architecture-decisions.md` was arguing from a number that had doubled underneath
  it.** Same housekeeping pass. The file's headline conclusion is *"do not rewrite the RAG — the test set
  is what limits it"*, and limiter 1 read **"the test set is 14 questions… 3/14 at #1, MRR 0.321."** Ajmal
  added 14 rows from his own question log on **2026-08-21** and that section was never updated. Live:
  **28 rows, 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267.**
  **The consequence is not cosmetic.** This file sets its own gate — *"revisit a rewrite when
  `test-questions.md` holds 30+ rows"* — and at 28 the Brain is **two rows from its own trigger**, with two
  finished features (the cross-encoder and the skill weighting) switched off only because the set was too
  small to judge them. Written as 14, that reads as far off; written as 28, "write two more test questions"
  becomes the highest-value job in the whole file. The stale number was hiding a decision that is nearly
  due. The declined **adversarial eval rows** verdict carried the same 30-row trigger and is now flagged
  *"two rows from expiring — re-read it, do not re-apply it."*
  Also corrected: the corpus line **"352 files, 3,892 chunks"** is now stamped as a 2026-08-20 snapshot and
  marked **unverifiable from a clone** — `index-manifest.json` is gitignored, so a cloud session cannot
  check the file count at all; live elsewhere is 353 indexable files and 3,890 chunks.
  **Nothing measured was deleted.** Every historical `x/14` score stays exactly as recorded — they are the
  evidence for the decisions written beside them — and a banner at the top now says any `x/14` is the
  pre-2026-08-21 era and that `score-history.md` wins whenever the two disagree. Added a 14-section table
  of contents; all anchors checked. The rule this pass keeps proving: **a generated file beats a written
  one, so a written file should point at the generator instead of copying its number.**

- 2026-08-22 — **The two files still flagged as over the split rule were reviewed, and one of them was
  wrong about the thing it exists to be right about.** `tools/brain-status.mjs` had been listing
  `knowledge/live-model/core.md` and `knowledge/revit-version-compatibility.md` as *past the ~300-line
  rule, not yet reviewed* — the last two without a `split-review` marker. Both are now reviewed and
  **kept whole**, with the reason recorded in each file. That list is now empty.
  **`core.md` — its contents list had been lying for weeks.** It advertised eleven topics; nine of them
  were split out on 2026-08-06 and 2026-08-13 and only their contents-line stayed behind, so a reader
  scrolled for duct routing, view visibility or undo in a file none of them had been in since. Replaced
  with what the file really holds, plus a block naming where each of the nine actually went. Also removed
  an **exact byte-identical duplicate bullet** (the post-commit roll-back lesson, present twice, 8 lines).
  Kept whole because this is the third time it has passed 300 and grown back: what returns is the residue
  that cannot be routed away, and `README.md` says in one line that *"core.md is the only file worth
  reading alongside another"* — splitting the always-read file means every session opens two.
  **`revit-version-compatibility.md` — four claims had gone stale, all in the same direction.** It led
  with *"200 of the 282 fragments (71%) touch a changed API"*, which was the **pre-migration** scan; the
  migration was applied that same day, so a fresh scan of all 290 finds one unmigrated call site, not two
  hundred. It listed **three deliberate exceptions** when one had already been solved — the electrical
  unit conversion, fixed by asking the API which unit type its own method takes rather than guessing a
  factor, which is the general move worth remembering. And it ended *"none of this has been compiled or
  run"* when `tools\check-scripts.cmd` had taken it to **287/287 on Revit 2020, 2024 and 2027** hours
  later. Every pre-migration count is kept as the record, under a banner saying a fresh
  `check-scripts.cmd` run beats any number written here.
  **The finding worth the most: a one-pass migration does not defend itself.**
  `filters/by-identity/filter-by-wrong-category.cs` was written on **2026-08-21, the day after** the
  library was moved off `ElementId.IntegerValue`, and used that exact removed API to compare categories —
  inside a `.Where()` running once per FamilyInstance in the model. Rewritten to compare `ElementId` to
  `ElementId` (`new ElementId(expectedCategory)` with the `==` / `!=` operators): correct on 2020 through
  2027, and no reflection in a per-element loop. Not compile-checked — this session has no Revit — so run
  `tools\check-scripts.cmd` before trusting it. A **"Keeping it fixed"** section now carries the rule
  (never take a number out of an `ElementId`) and a two-line grep that re-runs the scan from anywhere,
  including a container with no Revit on it. The pattern behind all three files this week is one thing:
  **a sweep fixes the library as it stands and does nothing about the next file somebody writes.**

- 2026-08-22 — **Ajmal asked whether other files had stale numbers like the two just fixed. They did —
  seventeen of them, across fourteen files, and the audit is now a permanent check.** Every numeric claim
  in every markdown file was scanned against disk. What was wrong, all of it in the flattering or
  alarming direction, never the harmless one:
  **Fragment counts frozen at four different values** — `README.md` said *267* and *266*, both agent
  definitions (`brain-investigator`, `brain-script-writer`) said *269*, `knowledge/INDEX.md` said *282*,
  `CLAUDE.md` and `START-HERE.md` said *285*, `job-log/README.md` said *269*. Disk holds **290**.
  **Native tools** read *17* in `semantic-index/README.md` and *17* / *19* in two places in `AGENT-SPEC.md`
  against a real **20**.
  **The checker was wrong about itself, twice.** `CLAUDE.md` promised the PowerShell copy ran *"the same
  eight checks"*; `brain-self-maintain/SKILL.md` said *"same three checks"* and told you to prefer the
  PowerShell one — which the hook has not run since 2026-08-04. `README.md` called it *"seven drift
  checks"* while listing eight.
  **`CLAUDE.md` carried the failure it opens by warning about**, on line 79, two paragraphs below the
  line naming *"README said 8 skills against 9"* as the repo's recurring disease.
  **A generator was stamping a date it did not run on.** `tools/api-surface.mjs` had `2026-08-20` as a
  string literal in its own header template, so every regeneration re-dated the file to a day it was not
  produced — the exact lie the generator exists to prevent. Now takes the date from the clock. Rerunning
  it also moved `revit-api-surface.md` from *285 fragments / 230 types* to the true **290 / 245**, which
  made `START-HERE.md`'s *"229 types"* wrong by a different route.
  **The fix that matters is check 9, not the seventeen edits.** `verify-consistency.mjs` had eight checks
  and only one of them looked at a count — a single hardcoded regex against a single sentence in
  `AGENT-SPEC.md`, which is why everything above survived. Check 9 scans **every** markdown file for
  whole-library totals (fragments, skills, native tools) and compares each to disk. It was tested by
  deliberately breaking `CLAUDE.md` and confirming it fails with the file and line. Today it checks 13
  live claims and finds nothing.
  **Its escape hatch is the lesson, not a loophole:** a number is a live claim *unless the line itself
  carries a date or a `<!-- count-history -->` marker.* A date on the enclosing heading does not count,
  because nothing downstream can see the heading — which is precisely how *"one pass over all 282
  fragments"* sat under a dated heading in `revit-version-compatibility.md` and still read as current.
  **Not ported to PowerShell.** Writing a `.ps1` from a Linux container is the documented encoding trap
  that has already broken two scripts here, so `verify-consistency.ps1` trails at eight checks and the
  docs now say so instead of claiming parity.

- 2026-08-22 — **"Is the log too detailed?" — measured instead of argued, and the answer is keep the
  detail but cap the cost.** Ajmal asked whether this file should be cut back to the main points. Three
  numbers decided it. **(1) The detail earns its keep:** this same session found a Revit-2024 regression
  in `filter-by-wrong-category.cs` only by reading what the 2026-08-20 migration had actually done — which
  exceptions it left, that it hit 287/287 on three Revit versions, when it ran. A 1–3 line log would have
  recorded "migration applied" and nothing findable. **(2) The detail is not free:** this file sits in
  `knowledge/`, so every line is indexed and competes with the notes that answer real questions. Measured
  today at **272 KB — 36% of all knowledge/, and 10% of the entire searchable corpus.** For scale, 604
  chunks of external standards were indexed on 2026-08-13 and reverted the same hour for being a 20%
  increase. **(3) Nothing here can prove archiving would help**, because the test set is 28 rows — the
  same reason the RAG doc declines every other retrieval change.
  So: **move, never shorten.** Past **20% of the corpus**, entries older than ~60 days go to
  `docs/brain-log-archive.md`, which is outside `INDEX_TARGETS` — every word survives, git still has it,
  and it stops crowding the search. At 10% today, nothing to do. `tools/brain-status.mjs` now computes and
  prints that share every session (loudly past 20%, quietly under `--full` below it), so the trigger fires
  on a number rather than on somebody's impression of how long the file looks.

- 2026-08-22 — **Ajmal asked for the same stale-number audit over the `.cs` fragments. Two real classes of
  drift, both the same shape: a sweep over the documentation that never reached the code.**
  **(1) Nineteen fragment headers said the fragment had never run — for fragments proven three weeks
  earlier.** `create-wall.cs`, `create-duct.cs`, `create-pipe.cs`, `create-floor.cs` and twelve more still
  opened with *"NOT YET LIVE-VERIFIED — created 2026-07-26"* while their `scripts/README.md` rows carry
  hard evidence of a live run: *"length 4000 mm, height 3000 mm, `LevelId` 311"*, *"read back `RevisionId`
  49030"*, *"12.00 m2"*. The 2026-08-06/07 verification campaign recorded every result in the README and
  never went back to the files. **This exact bug is already in this log**, on 2026-08-07, from the other
  side: a stale "NOT yet live-verified" clause left inside a README row hid four fragments from the count.
  It was fixed in the README and the other half was missed. Sixteen headers now carry the verified date
  and a pointer to the README row, each quoting its own old wording verbatim so nothing is lost. Three
  were **not** flatly wrong and were rewritten as `PARTLY VERIFIED` instead, naming which path is proven
  and which is not — `action-create-view-filter.cs` (contains-rule only), `filter-by-assembly.cs` (by-Id,
  not by-name), `filter-by-host.cs` (hosted families, not insulation). **The dangerous direction is clean:**
  no fragment claims to be proven when the README says otherwise. The one hit was a false positive — a
  GOTCHA saying a *technique* was "proven live in this Brain", which is true.
  **(2) The 2026-08-20 outside-source strip never touched `scripts/`.** Seven fragment headers still
  carried a `(Dynamo-package equivalent: X's Y nodes.)` line in the PURPOSE block a modeller reads first —
  pure attribution, no technical content — plus three more in `scripts/README.md`. All removed, per
  Ajmal's own words that day: *"do not mention any thing that we took from this web site or repo... the
  words also remove."* One source citation in `knowledge/live-model/views.md` went too; the fact it
  supported (the view-title API landed in Revit 2022) stands on its own and is recorded in
  `revit-version-compatibility.md` anyway.
  **What was checked and found clean:** every relative path referenced from a fragment (109 of them, 0
  broken), library counts stated inside `.cs` comments (none), and headers overclaiming their own status.
  **Two checks added, both tested by deliberately breaking a file first** — the first attempt at check 10
  had a skip rule broad enough to swallow the very line it was meant to catch, and passed its own test.
  Check 10 compares each fragment's header status to its README row (241 verified fragments). Check 11
  scans `scripts/` and `skills/` for outside-source names, and **found three more on its first run**, in
  `scripts/README.md`, which the `.cs`-only grep had never looked at. That is the argument for a check
  over a one-off sweep, in one line.
  **Two things deliberately left for Ajmal**, because both are his call and not a checker's:
  `knowledge/dynamo-vocabulary-map.md` (a whole file whose second table maps community package names to
  fragments — it is also the only thing that routes "the Rhythm one" to the right fragment), and the eight
  external URLs under "Sources consulted" in `knowledge/nfpa13-sprinkler-spacing.md` (fire-code values
  whose own heading warns they are secondary summaries — removing the sources would leave unverified
  numbers looking authoritative). Neither is scanned by check 11; the reason is written into the check.

- 2026-08-22 — **Ajmal settled both open outside-source questions, and they went opposite ways.** His
  words: *"keep the nfpa links and THE DYNAMO FILES NO NEED BECOSE WE DONT HAVE ANYTING RELATED TO
  DYNAMO."* Both decisions are now recorded where they will be found, not just done.
  **`knowledge/dynamo-vocabulary-map.md` deleted** (55 lines, his explicit OK). His reason is the right
  one and it beats the argument for keeping it: the file existed to translate Dynamo node names into
  fragments, and this setup has never used Dynamo — its own first paragraph said so. The routing it
  provided was for a vocabulary that is not in play. Removed with it: the `knowledge/INDEX.md` row that
  pointed at it, and the last stray mention in `live-model/graphic-override-precedence.md` (an API-gap
  note that read fine without it). Git has the file if it is ever wanted back.
  **The NFPA links stay, and now say why.** A blockquote above the "Sources consulted" heading in
  `nfpa13-sprinkler-spacing.md` records that this is a deliberate exception on safety grounds — fire-code
  values, a heading that already warns they are secondary summaries rather than the standard itself, and
  stripping the sources would leave unverified life-safety numbers looking authoritative. A future session
  tidying references will see the decision instead of re-making it.
  **Check 11 widened from `scripts/` + `skills/` to include `knowledge/`** — 344 files — now that the one
  file blocking it is gone. Two exemptions remain, each with its reason written into the check itself:
  `brain-log.md` (dated history — the July package-comparison entries record something that really
  happened, and rewriting them would make this log lie about the Brain's own past) and the NFPA sources
  section. Tested by planting a name in `knowledge/glossary.md`; it fired with file and line.
  **Check 8 earned its keep in the same minute.** Deleting one knowledge file moved the indexable corpus
  from 353 to 352, and the checker immediately flagged all three "searches all 353 files" claims in
  `CLAUDE.md`, `START-HERE.md` and `README.md`. Before check 8 existed that would have drifted silently —
  it is on record as having done exactly that for two days. One delete, three docs corrected, no thought
  required.

- 2026-08-22 — **Final sweep of the markdown, and the one thing it found was the failure `CLAUDE.md` names
  by name.** `README.md` was quoting the search accuracy as **"3/14 at #1, 6/14 in top 5, 11/14 retrievable
  at all"**. Two things wrong with that, and the second is worse than the first: the test set doubled to 28
  rows on 2026-08-21, *and* that exact combination **matches no line in `score-history.md` that ever
  happened** — it was a remembered composite, which is precisely what `CLAUDE.md` warns about when it says
  *"three different numbers (75%, 60%, 29%) were once in circulation here."* Corrected to the real last
  run, **5/28 at #1, MRR 0.267**, with the note that it is not comparable to any `x/14` above it.
  **Check 12 added** so a score claim is compared to the last run in `score-history.md` automatically —
  `score-history.md`, `brain-log.md`, `rag-architecture-decisions.md` and `HANDOVER.md` are exempt because
  all four quote every era on purpose and the last three already carry a banner saying so. Tested by
  putting the old number back; it fired with file and line.
  **Everything else in the markdown came back clean, and the negative results are worth recording** so the
  next session does not repeat the search: **35 in-file `#anchor` links, all resolving** (check 2 strips
  the `#`, so these had never been validated — including the jump table added to this log this morning);
  **every `tools/*.mjs|ps1|py|cmd` named in prose exists on disk**; **0 orphan knowledge files** of 43,
  so everything is reachable from an index; **no TODO/FIXME/placeholder leftovers** (11 keyword hits, all
  legitimate — "need todo" is Ajmal's own quoted words, `(XXX)` is a naming regex, "connector placeholder
  size" is a real term); **no sentence over 150 characters appears in two files**, so the "never duplicate
  a fact across two files" rule is actually holding; and **no duplicate headings** inside any file.
  **One measurement error caught in my own audit, worth more than the clean results.** A percentage check
  flagged two figures in this log as wrong arithmetic. Both were my regex, not the log: one spanned two
  unrelated numbers, and the other read `1,192` as `1` because of the comma. **Neither was reported as a
  finding.** The pattern this session keeps proving in both directions: a scan is evidence only after you
  read what it actually matched.
  **`docs/HANDOVER.md` rewritten at the top** for the close-out — the whole 2026-08-22 session, the four
  new checks, the four decisions Ajmal took, and the three things only he can do. Its own stale numbers
  (270 / 283 / 287 fragments, `3/14`) were stamped or corrected; the 2026-08-20 Windows bridge work below
  is untouched because it is still unfinished.

- 2026-08-22 — **A conflicted `CLAUDE.md` passed all twelve consistency checks, so there is now a
  thirteenth.** Found while merging the daily-check branch into main: `CLAUDE.md` still carried
  `<<<<<<<` / `=======` / `>>>>>>>` from an unresolved merge, and `verify-consistency.mjs` reported
  **"All checks passed - no drift found."** Every check up to that point reads content looking for a
  specific claim — a count, a link, a status, a score — and **not one of them asks whether the file is
  coherent at all.** A conflict marker shipped into `CLAUDE.md` would corrupt the instructions every
  session loads first, and nothing would have said a word. Check 13 scans all 449 tracked text files for
  the markers; tested by planting one. It goes last because it guards everything above it.
  **How the marker survived:** the conflict was in a file I had not listed in my own `grep -c` after
  resolving — I checked the three files git named in the conflict output and moved on, and README.md and
  CLAUDE.md had conflicted too. That is the same error shape as the truncated `head -25` earlier today
  and the phantom log entry this morning: **a scan is evidence only for what it actually covered.** The
  fix each time is the same, and it is now a check rather than a resolution to be careful.

- 2026-08-22 — **Both open PRs merged and the repo tidied; one housekeeping job cannot be done from a
  cloud session and is recorded rather than left silent.** PR #30 (the number audit, 49 files) and PR #31
  (the daily check's PowerShell port) are both in `main`, which now passes all 13 checks. Merging #30
  first meant #31 arrived mostly superseded — its six count fixes were a subset of #30's seventeen, and
  its single new check a subset of checks 9–12 — so those conflicts resolved to `main` and only the
  `.ps1` port was taken, after verifying the branch's own *"it is ASCII-only"* claim was **false** (two em
  dashes, both pre-existing, and the BOM is intact, so it is safe for a different reason than the one
  given).
  **`main` had moved underneath this work**: PR #32 merged `run_fragment` and five fragment-backed native
  tools, taking `mcp-server/tools/` from 20 `.js` files to **26** — falsifying every "20 native tools"
  claim the audit had just finished correcting, one merge later. Check 9 caught all of them. That is the
  clearest argument yet for the checks over the fixes: the fixes were obsolete within a day; the check
  was not.
  **What could not be done:** deleting the three merged `claude/*` branches. `git push --delete` returns
  **HTTP 403**, and the GitHub tools available here have `create_branch` and no delete counterpart.
  Nothing depends on them and `main` contains every commit, so this is cosmetic — recorded in
  `docs/HANDOVER.md` rather than left to be rediscovered.

- 2026-08-22 — **The branch-delete 403 was blamed on the wrong thing, and the correction is the useful
  part.** Earlier today this log and `docs/HANDOVER.md` both recorded that deleting the merged `claude/*`
  branches fails because *"the container's git proxy returns 403"*. Asked to try again, the proxy's own
  diagnostics settled it: `curl $HTTPS_PROXY/__agentproxy/status` reports **`recentRelayFailures: []`**
  immediately after a failed delete, so the proxy never saw a failure — **the request reached GitHub and
  GitHub refused it.** The session's git credentials can push refs but not delete them. That is a
  deliberate guardrail, not a misconfiguration, and `/root/.ccr/README.md` is explicit that a 403 is to be
  reported rather than routed around. Both files now say so.
  **Two accurate lessons instead of one wrong one.** First, a plausible cause is not a cause: "it is
  behind a proxy, the proxy returned 403" was coherent and false, and one status endpoint disproved it in
  a second. Second, the fix is not a workaround but a repo setting — **Settings → General →
  "Automatically delete head branches"** makes every future merged branch clean itself up, which removes
  the whole class of job rather than this instance of it.
  Safety was confirmed before any of this: all three branches are **0 commits ahead of `main`**. Two of
  them show files differing from `main`, which is only them being *behind* it — `rev-list --count
  main..branch` is the test that answers "is anything lost", and `diff --stat` is not.

- 2026-08-22 — **Asked whether the housekeeping was actually done, the honest answer was no — check 9 had
  only ever looked at markdown, and there were six more stale counts sitting in code.** Every one frozen
  in the mid-260s against 290 on disk: `tools/fragment-nudge.mjs` (twice), `tools/auto-search-hook.mjs`,
  `tools/fragment-index.mjs`, `tools/job-log-revit.mjs`, `semantic-index/brain_context.py` and
  `tools/verify-fragments-compile.ps1`. Two of those matter more than the rest — **`auto-search-hook.mjs`
  injects its text into every message**, and `fragment-nudge.mjs` is the Stop hook whose whole job is
  telling you to *"search the N first"*, so both were quoting a number to the model on every turn.
  **Check 9 now scans `.mjs`, `.js`, `.py`, `.ps1`, `.cmd` and `.json` alongside `.md`** — 13 claims
  became 22. It immediately caught a seventh: the comment inside the check describing the fix, which
  quoted the old figures; that comment now describes them instead of repeating them, the same resolution
  check 12 forced in `CLAUDE.md`. Negative-tested by putting a stale count back into `job-log-revit.mjs`.
  **The repo hygiene sweep found the rest clean** and the negatives are worth recording so nobody repeats
  it: no empty tracked files, no byte-identical duplicates, nothing committed that should not be (no
  `node_modules`, databases or binaries), `.gitignore` genuinely covers all three derived layers, and the
  two empty directories are `.voice-runtime/` runtime dirs that are both ignored and untrackable anyway.
  **One live tool was documented nowhere.** `fragment-nudge.mjs` is wired into `stop-hooks.mjs` and runs
  every turn, but appeared in no markdown file — so it was invisible to anyone reading the Brain, which is
  a particular irony for the hook that exists to make invisible things visible. `README.md` now describes
  all four end-of-turn hooks together, with the 2026-08-13 session that caused it: thirteen `run_csharp`
  calls, zero saved fragments used, nothing saved back. Every tool in `tools/` is now referenced from at
  least one document.
  **The pattern, stated once more because it keeps being the same one:** a sweep is evidence only for what
  it covered. Markdown was swept and declared done while code sat untouched — exactly as the 2026-08-20
  outside-source strip covered docs and missed `scripts/`, and as the verification campaign updated
  `scripts/README.md` and missed nineteen fragment headers.
- 2026-08-22 — **started harvesting the AJ Tools add-in into the Brain**, one tool at a time, on
  Ajmal's instruction: read each tool's real source, then give it one of four verdicts — build it,
  upgrade ours, keep ours and say why, or skip. The ledger is [`docs/addin-harvest.md`](../docs/addin-harvest.md)
  (in `docs/`, outside the search index, same as the handover) so a fresh session never re-reads a
  tool already judged. **First result closes a whole branch:** all **77** C# fragments in the
  add-in's own `.claude/scripts` are already here among the 290 — the two that looked missing
  (`action-length-by-size`, `action-material-takeoff`) were renamed into `actions/reporting/`, not
  absent. Checked by basename with the `action-`/`filter-` prefixes stripped, because the Brain
  reorganised that library into sub-folders. The untouched material is the compiled add-in itself:
  ~62 ribbon tools over 34 service groups, plus 27 helper/compat classes.
- 2026-08-22 — **harvested the AJ Tools MEP panel** (first round of the add-in harvest). Three of four
  tools were real gaps, one was skipped on Ajmal's call. New: [`plumbing-pipe-sizing.md`](plumbing-pipe-sizing.md)
  — a domain this Brain had nothing on (water supply fixture units -> Hunter's curve -> velocity sizing
  -> Hazen-Williams, with all four lookup tables) and its fragment
  [`recipes/size-domestic-water-pipe.cs`](../scripts/recipes/size-domestic-water-pipe.cs);
  [`live-model/ceiling-grid.md`](live-model/ceiling-grid.md) — reading a ceiling's real tile size off the
  type's **model** surface pattern (a drafting pattern's Offset is a paper distance and changes with view
  scale), the exact over-this-ceiling test via `Face.Project` rather than a bounding box, and the
  tile-CENTRE snap math, with its fragment
  [`actions/move-copy-rotate/action-snap-to-ceiling-grid.cs`](../scripts/actions/move-copy-rotate/action-snap-to-ceiling-grid.cs);
  and [`live-model/mep-connect-existing-runs.md`](live-model/mep-connect-existing-runs.md) — closing the
  gap between two runs that already exist (stretch-don't-create, `canTrim` vs `mayMove`, one
  sub-transaction per attempt, and the crank sign trap where adding the axial gap instead of subtracting
  it folds the bridge back over the run it just left). **No fragment for that last one on purpose** —
  the add-in's builder is ~2,200 lines; the note is what to build from the day a job needs it. HVAC
  Schematic skipped: unfinished in the add-in. Ledger: [`../docs/addin-harvest.md`](../docs/addin-harvest.md).
- 2026-08-22 — **`filter-by-wrong-category.cs` could not compile on Revit 2027** and nothing had noticed.
  It compared categories through `ElementId.IntegerValue`, which is **gone** on 2027 (it survived the
  2024 64-bit widening, so the 2020 and 2024 passes were both clean and hid it). Now compares `ElementId`
  to `ElementId`; re-verified passing on 2020 and 2027. Found only because the two new fragments above
  were compile-checked against **every** installed Revit rather than the oldest — which is now the
  standing rule for anything harvested: **check the newest, not just the oldest.**
- 2026-08-22 — **harvested the AJ Tools View panel** (6 tools): 3 KEEP OURS, 3 UPGRADE, 0 build — a
  well-covered area where half of it genuinely had nothing to teach us. **`action-set-view-crop.cs`
  had a real, silent defect and it is now fixed**: it merged world-aligned bounding boxes and assigned
  a Transform-less `BoundingBoxXYZ`, but `CropBox` Min/Max are read in the box's OWN transform — so on
  any rotated plan, section or elevation the crop landed elsewhere while the fragment reported success.
  It only looked right on a plain north-up plan, which is what its 2026-07-22 live test used, so the
  test passed and the defect survived; its header had even recorded the gap as a known simplification.
  Now projects all eight corners through `CropBox.Transform.Inverse` and writes back keeping the view's
  own transform and Z. It also gained the four refusal checks — **three of which do not throw**: no
  crop box, a **scope box** owning the crop, a **view template** controlling it.
  **`action-highlight-vs-rest.cs`** now pulls insulation and lining along with their host in both
  directions (`HostElementId` one way, `InsulationLiningBase.GetInsulationIds`/`GetLiningIds` the
  other) — without it an insulated duct highlights half-grey. Those getters throw for an id that cannot
  host a wrap, so the per-element catch IS the category filter.
  **[`graphic-override-precedence.md`](live-model/graphic-override-precedence.md)** gained two sections
  the Brain had **no mention of anywhere**: filter-versus-filter order inside one view (there is no
  reorder API — capture overrides and visibility, remove all, re-add, restore, and skipping the capture
  silently resets every filter), `View.GetFilters()` returning **new `ElementId` wrappers that are not
  reference-equal**, and a view template blocking every filter change without throwing.
  Kept unchanged, with the reason recorded: Unhide All, Toggle Links, Colorize.
  Ledger: [`../docs/addin-harvest.md`](../docs/addin-harvest.md).
- 2026-08-22 — **harvested the AJ Tools Graphics panel** (3 tools): one BUILD, one UPGRADE, one KEEP.
  New [`action-match-graphics.cs`](../scripts/actions/color-graphics/action-match-graphics.cs) — copy
  the graphic overrides off one source element onto a filtered set, per-element or category-to-category.
  The Brain could set a colour you name and clear a colour, but had no way to **copy the look off
  something already right**. `action-reset-category-graphics.cs` gained **`allCategories`**: it could
  only clear categories named by hand, which cannot serve the request it exists for — "I ran grayout
  over this view, put it back", where `recipes/mep-grayout.cs` alone has written 87 categories and 589
  sub-categories. Four API facts went into
  [`graphic-override-precedence.md`](live-model/graphic-override-precedence.md), none of which appeared
  anywhere in this Brain: **"no override" is a sentinel** (`Color.InvalidColorValue`,
  `OverrideGraphicSettings.InvalidPenNumber`, `ElementId.InvalidElementId`) and writing `0` as a line
  weight clears nothing (valid weights are 1-16); **a pattern id and its visible flag are two separate
  writes**, so setting the id alone leaves the pattern present but undrawn; **`new
  OverrideGraphicSettings(existing)` is a copy constructor**, and re-setting properties one at a time
  turns every property you forget into "no override"; and **`view.IsCategoryOverridable(id)`** is the
  real test, not `CategoryType`. Apply Graphics kept as-is — our small composable fragments beat one
  dialog. `GraphicsOverrideMemoryService` skipped: it remembers last-used dialog values, the opposite of
  this Brain's every-number-is-a-per-request-input rule.
- 2026-08-22 — **harvested the AJ Tools Datums panel** (3 tools, all BUILD — the emptiest area yet).
  `fragment-index --find datum` answered "Nothing matched", and `DatumExtentType`, `SetCurvesInView`
  and `DatumPlane` appeared nowhere in the Brain: grids and levels could be listed and created, but
  their extents could not be touched. New [`live-model/datums.md`](live-model/datums.md) plus three
  fragments — `action-reset-datum-extents.cs`, `action-set-datum-bubbles.cs` and
  `recipes/maximize-level-extents.cs`. The subject is the **2D/3D trap**: a datum has one shared
  **Model** extent and a **per-view 2D override**, the toggle at the grid end picks which you drag, and
  dragging on 2D makes an override that never follows the model again. Reset = both ends back to
  `DatumExtentType.Model`, **per end and per view**. Also recorded: a Level is a line only in
  elevation/section/3D (`GetCurvesInView` returning nothing is the normal answer, not an error); both
  ends must be set to `Model` **before** writing a Model curve; and a bubble "flip" is not one call —
  Revit has only Is/Show/Hide per end, so the neither-visible and both-visible cases must be decided
  explicitly. The section-box recipe carries the same own-transform trap as the view crop box, and
  builds its new line **along the datum's own direction** by projecting the box corners onto its
  unbound line — an axis-aligned line is wrong on any rotated building.
- 2026-08-22 — **check 9 was reporting drift inside `graphify-out/` and asking for generated files to
  be hand-edited.** Adding three fragments produced 19 such lines, several from a stale 2026-08-13
  snapshot still quoting 269 fragments and 9 skills. That folder is derived, gitignored and rebuilt
  wholesale from the sources, so it duplicates every library total quoted in Brain prose and any edit
  to it is overwritten on the next rebuild. `graphify-out` is now in the checker's `skipDir`; check 9
  still covers 22 live claims in real source files. Whether the derived layers have gone stale is a
  genuine question answered by the STALE INDEX banner and `python tools/graph-rebuild.py --check` —
  not by the consistency checker.
- 2026-08-22 — **harvested the Modify, Opening and Coordination panels** (9 tools: 5 BUILD, 1 UPGRADE,
  2 KEEP, 1 SKIP). New [`live-model/mep-openings.md`](live-model/mep-openings.md) and
  [`recipes/create-mep-openings.cs`](../scripts/recipes/create-mep-openings.cs) — the add-in's largest
  service (~136 KB) and a subject this Brain had nothing on. Two facts carry the round.
  **`Document.Create.NewOpening` has three completely different overloads**: a wall takes two opposite
  corner POINTS, a floor takes a CurveArray profile plus `true`, and a beam takes a profile plus an
  `eRefFace` **whose correct value is not predictable** — CenterY, CenterZ, CenterX are tried in turn.
  A crossing is found by a real `BooleanOperationsType.Intersect` with non-zero volume, never by
  overlapping bounding boxes, and some Revit solids throw on a boolean so each solid PAIR is caught
  individually. **Re-pointing an element's level MOVES IT unless the offset is compensated** —
  `newOffset = oldOffset + oldLevel.Elevation - newLevel.Elevation` — and nothing throws, so 400 ducts
  jump a storey invisibly in plan; there is no single level parameter either (`RBS_START_LEVEL_PARAM`
  for MEP curves, `FAMILY_LEVEL_PARAM` / `INSTANCE_REFERENCE_LEVEL_PARAM` for family instances). Also
  new: `action-align-mep-elevation.cs`, which aligns by TOP/BOTTOM/CENTRE using each element's real
  size — `action-align-elements.cs` aligns insertion points, i.e. centrelines, so it leaves a 600 mm
  duct and a 100 mm pipe with soffits 250 mm apart; and `create-workset-3d-views.cs`.
- 2026-08-22 — **a duplicate fragment was written and deleted the same turn, and the cause is worth
  keeping.** `action-set-pin-state.cs` already existed at `actions/visibility/` and was PROVEN;
  `fragment-index --find pin` **did report it**, but the output was piped through `head -12` and the
  negative conclusion drawn from the truncated list. The duplicate was removed and the existing proven
  fragment upgraded instead — it gained a dry run and a **read-back**, because `Element.Pinned` does not
  throw when the state will not stick, so the old version counted a failed write as `updated`. Rule:
  **never draw a "we don't have this" conclusion from a truncated search — grep the full list for the
  name.**
- 2026-08-22 — **harvested the Data and Manage panels** (14 buttons, five real jobs: 4 BUILD, 1 UPGRADE,
  2 KEEP, 1 deliberately DEFERRED). New [`duct-sheet-metal-takeoff.md`](duct-sheet-metal-takeoff.md) and
  `action-report-duct-weight.cs` — the Duct Standard tool turns out to be a **sheet-metal weight
  takeoff**, not duct sizing, and the Brain had nothing on it. **Fabrication allowances add +24% over
  bare sheet (+29% reinforced)**, so quoting bare weight understates a job by a quarter; an oval duct
  carries width, height AND diameter, so shape must be tested oval-first or every oval is called round
  and gets the wrong perimeter (Ramanujan's approximation, not `pi x (w+h)/2`). Also new:
  `action-transfer-views-between-documents.cs`, `action-purge-unplaced-views.cs`, and
  `action-assign-location-data.cs`; `action-purge-unused.cs` gained a **groups** mode (a `GroupType`
  with an empty `Groups` set has no placed instances). Two more silent-wrong-answer traps recorded:
  **a schedule is NOT placed via a Viewport** — sheets hold schedules as `ScheduleSheetInstance`, so a
  Viewport-only scan reports every schedule in the project as unplaced; and the document-to-document
  `CopyElements` copies the view **SHELL ONLY**, so a legend or drafting view arrives empty and its
  contents need a second copy. **Purge unused family parameters was deliberately NOT built** — it needs
  opening and editing every family document in turn, which is its own job.
- 2026-08-22 — **first recorded job the fragment harness structurally cannot do.** Resolving a duplicate
  TYPE name during a cross-document copy needs an `IDuplicateTypeNamesHandler` passed on
  `CopyPasteOptions`, and that handler **must be a class** — but the bridge wraps every fragment inside a
  single method body, so `class ... : IDuplicateTypeNamesHandler` will not compile. Proved on the
  compile checker. `action-transfer-views-between-documents.cs` therefore copies each view in its own
  try/catch and names any that fail, pointing at Revit's own Transfer Project Standards for those. Worth
  knowing before someone spends an hour trying to make a handler work in a fragment.
- 2026-08-22 — **harvested the Family and Dimensions panels** (7 tools: 1 BUILD, 2 KEEP, 3 SKIP, plus a
  correction to our own work). New [`live-model/dimensioning.md`](live-model/dimensioning.md) and
  `action-dimension-mep-runs.cs`. **The gap it closes had been written off by this Brain.**
  `action-add-aligned-dimensions.cs` measured on 2026-08-14 that MEP fittings expose zero of all four
  `FamilyInstanceReferenceType` values and concluded "this fragment can never dimension them" — the
  measurement is right, the conclusion was too broad. Ducts, pipes, conduit and trays dimension fine by
  **walking the geometry** for a `.Reference`, provided **`Options.IncludeNonVisibleObjects = true`**,
  because a run's CENTRELINE is a non-visible geometry object. That header is corrected. Also recorded:
  `new Reference(element)` works for a datum but THROWS for a pipe, and in a chained dimension one bad
  reference takes down every good one sharing the array; only `Linear`/`LinearFixed` dimension types are
  legal; a Coarse view returns non-null geometry containing only lines, so a null-check never triggers a
  model-geometry fallback; `PlanarFace.Origin` can sit OUTSIDE its face; `view.CropBox` is stale when the
  crop is off. **Shared to Family: KEEP OURS** — the add-in solves the `ReplaceParameter` name clash with
  a temp-name detour, but this Brain has a measured corruption incident for that family of sequence, so
  `families.md` now records both orderings and why the script-side caution stays stricter.
- 2026-08-22 — **`fragment-index.mjs` was under-reporting proven fragments by ELEVEN, and the fix had
  already been made once in the wrong place.** Its status test was the literal `verified 2026`, so a
  README row reading "verified **live** 2026-08-14" failed to match and the fragment reported as
  UNPROVEN. `brain-status.mjs` found this same bug on 2026-08-14 and fixed it **in itself** with a looser
  regex — but `tools/fragment-lib.mjs`, which `fragment-index.mjs` uses, kept the strict one. So the two
  tools disagreed about the same fact, and the one CLAUDE.md tells every session to search with was the
  wrong one. Measured 231 -> 242 proven. Fixed in `fragment-lib.mjs`; the `NOT yet live-verified` test
  still runs first, so explicit not-yet rows are unaffected. **Lesson: when a bug is found in a shared
  fact, fix the shared library, not the caller you happened to be standing in.**
- 2026-08-22 — **`ElementId.IntegerValue` was written three times in one day, by the session that wrote
  the warning about it that morning.** It compiles on 2020 and 2024 and fails only on 2027, so nothing
  catches it until the newest-Revit pass. Root cause: the rule lived only in
  [`revit-version-compatibility.md`](revit-version-compatibility.md), which is not the file a fragment
  author opens. It is now its own section in [`../scripts/README.md`](../scripts/README.md) with a table
  of what to write instead — print `elem.Id`, compare `ElementId` to `ElementId`, and use the
  `lib/prelude.cs` reflection helper only when the number itself is genuinely needed.
- 2026-08-22 — **harvested AJ Annotation's Family and Annotation panels** (4 tools: 3 BUILD, 1 SKIP).
  **Ajmal caught a miss**: there are TWO Family panels — AJ Tools -> Family (Shared to Family, done
  earlier) and AJ Annotation -> Family (Center Annotation), which had not been touched. The ledger row
  was honest about it; the summary was not. New fragments: `action-center-room-tags.cs`,
  `action-revision-cloud-around-elements.cs`, `action-place-flow-arrows.cs`, plus two sections in
  [`live-model/tagging.md`](live-model/tagging.md). **The best fact of the round is how to find "the
  centre of a room"** — four methods in order: the true area-weighted BOUNDARY CENTROID summed across
  every loop (so a room with a hole works), the bounding-box centre, then **an interior grid point**,
  because on an L-shaped or U-shaped room **the true centroid falls OUTSIDE the room** and a script
  without that step silently tags the corridor; then the Location point. Check every candidate with
  `IsPointInRoom`. A tag on a LINKED room needs `GetTotalTransform()` or it lands at the wrong end of
  the site, and `TagHeadPosition` must be read back because on a pinned tag the set is a silent no-op.
  **Flow arrows: the direction is on the CONNECTORS, never the location curve** — a duct's curve runs
  whichever way it was drawn, so using it points half the drawing backwards with nothing to warn you;
  arrow runs `In` -> `Out`. Also: activate the FamilySymbol before first placement, and rotate about the
  VIEW's normal rather than world Z or sections come out wrong.
- 2026-08-22 — **a limitation was stated instead of faked.** The add-in's Revision Clouds by Elements
  rasterises element footprints into a grid, extracts connected components, traces each boundary loop
  and simplifies it, so its cloud follows the real STEPPED shape of what changed. Half-implementing that
  would produce plausible-looking wrong outlines, so `action-revision-cloud-around-elements.cs` does the
  honest simpler thing — cluster by proximity, one rectangle per cluster, which gets the main benefit of
  several clouds following the groups — and its header says plainly what it does not do. The full
  algorithm is described there for the day it is genuinely wanted.
- 2026-08-22 — **harvested the Tags panel** (8 tools: 3 BUILD, 3 KEEP OURS, 1 already done, 1 SKIP) —
  **and the prediction was wrong.** Every earlier round called Tags "the biggest uncovered area"; it was
  the most-covered one. The reason is on the record: on **2026-07-14 Ajmal pointed this Brain straight
  at the add-in's own `SmartTagPlacementEngine`** and it was read in full and adapted then, so the crown
  jewel was harvested six weeks before this harvest began — and `tagging.md` documents it with better
  measured outcomes than the add-in carries (1092/1092 tags, 546/546 flow-direction match, 3.3%
  fallback, 0 own-leader clashes). New: `action-force-tag-leader-lshape.cs`, `action-stack-tags.cs`,
  `action-set-section-mark-visibility.cs`.
- 2026-08-22 — **TAG CLASSES SHARE NO BASE exposing `LeaderElbow`, `LeaderEnd`, `TagHeadPosition` or
  `LeaderEndCondition`.** `IndependentTag`, `RoomTag`, `SpaceTag` and `AreaTag` each declare their own,
  with no common interface. A fragment that casts to `IndependentTag` **silently skips every room and
  space tag** in the selection — no error, just a smaller number than expected. Both new tag fragments
  read and write these **by reflection, by name**. Second fact from the same tool: setting `LeaderElbow`
  often fails until `LeaderEndCondition` is set to **`Free`**, and the ORIGINAL condition must then be
  restored or every tag ends up detached from its element. Third: a tag stack must be ordered
  **nearest-element-first**, or the leaders cross and it looks like the tool is broken. Section marks:
  what you hide is the section's **`Viewer`** element (`OST_Viewers`), never the view, "is it on a
  sheet" comes from the section view's own `VIEWER_SHEET_NUMBER` parameter, and the pass must **unhide
  everything first** or a mark hidden on an earlier run never returns once its section IS placed.
- 2026-08-22 — **`knowledge/live-model/tagging.md` is now 379 lines across 14 sections and is a genuine
  split candidate**, raised rather than actioned. It has grown past one job — it now covers placement
  scoring, clash resolution, leader logic, room-tag centring and flow arrows. It was deliberately NOT
  split at the end of a long session: the rule requires cutting mechanically at the section seams and
  proving the result lossless against a backup, and a careless split loses content. Recommended as the
  next maintenance action, with `families.md` (466 lines) the only larger file.
- 2026-08-22 — **Game Mode was the surprise of the harvest: its collision service found a real defect in
  five of this Brain's ray-casting fragments.** It raycasts against the model and its own notes say
  plainly *"architecture usually lives in a linked model"*, so it sets
  `ReferenceIntersector.FindReferencesInRevitLinks = true`. **None of our five ray fragments did.** On a
  normal project the ceilings and slabs ARE in a linked architectural model, so "snap the terminals up
  to the ceiling" would find nothing and report "no hit" — a failure that reads as a broken tool when
  the model is merely arranged the usual way. Fixed in `action-report-ray-hits.cs` and
  `action-move-to-ray-hit.cs`, including the part that is easy to get wrong: **a linked hit's
  `Reference.ElementId` is the `RevitLinkInstance`, not what you hit** — the real element is
  `Reference.LinkedElementId`, fetched from the link's own document via `GetLinkDocument()`. Resolve it
  lazily and the report names the RVT file instead of the ceiling. **Three fragments still owe the same
  fix** (`action-check-surface-fit.cs`, `ray-trace-to-ceiling.cs`, `sprinkler-deflector-height.cs`) —
  left deliberately, because the linked path is compile-checked and not yet live-proven and deserves one
  real test against a model with links first; tracked in a table in
  [`live-model/core.md`](live-model/core.md). Two more ray facts came with it, both live-verified in the
  add-in on Revit 2020: `ReferenceIntersector` **works on a PERSPECTIVE `View3D`**, same results as
  orthographic at ~0.1 ms per hitting ray, so there is no need to hunt for an orthographic view; and it
  only reports what is VISIBLE in that view. **Quick Menu: SKIP** — ribbon UI, and the Brain has no
  ribbon. **The lesson: the two panels that looked like they had nothing produced one of the session's
  most material findings. Reading them cost ten minutes.**
- 2026-08-22 — **harvested the settled values out of the add-in's `*Settings` classes** into
  [`ajtools-settled-values.md`](ajtools-settled-values.md) — Ajmal's own numbers, decided once on real
  work and shipped as his tools' defaults. Framed deliberately as **material to ASK with, never to apply
  silently**: rule 3 of START-HERE still holds that every number is a per-request input, but knowing his
  number turns "what clearance do you want?" into "your openings tool uses a 25 mm duct buffer and
  merges within 100 mm — same here?", which he can answer in one word. Covers tagging (300 mm offset,
  1000 mm minimum run, 12 mm stack spacing, 5 fix passes, **50 mm maximum drift** — a tag that cannot be
  fixed within 50 mm is reported rather than dragged across the drawing), openings (pipe 20 mm circle,
  duct and tray 25 mm rectangle, merge at 100 mm, **insulation included**), connecting (90 deg, fallback
  45/30/60/90, copy insulation and workset), dimensioning (8 mm first row, 6 mm spacing, 150 mm search
  band, all PAPER mm), view crop (300 mm margin, **datums OFF**), and the duct allowances.
- 2026-08-22 — **two places where his tools and this Brain's fragments disagree, now visible instead of
  surprising.** His MEP Openings tool **includes insulation** in the opening size;
  `recipes/create-mep-openings.cs` measures the bare service. His View Crop **excludes datums**;
  `actions/visibility/action-set-view-crop.cs` crops to whatever `elements` it is handed, so grids and
  levels would give a crop the size of the site. Neither is a bug in either place — they are different
  defaults for the same job. Both are flagged in the values note rather than silently reconciled,
  because which one is right is Ajmal's call, not ours.
- 2026-08-22 — **harvested the four version-compat shims, and `TagCompat` caught a defect in fragments
  written the same day.** **Revit 2023 REMOVED `IndependentTag.LeaderElbow`, `.LeaderEnd`,
  `.GetTaggedReference()` and `.TaggedLocalElementId`** in favour of a per-reference API
  (`SetLeaderElbow(Reference, XYZ)`, `GetLeaderEnd(Reference)`, `GetTaggedReferences()`). **Proved, not
  assumed** — a probe fragment naming all three compiled PASS on 2020 and FAIL on both 2024 and 2027.
  **The dangerous part is that reflection hides it completely**: `action-force-tag-leader-lshape.cs` and
  `action-stack-tags.cs`, both written earlier today, read `GetProperty("LeaderElbow")` /
  `GetProperty("LeaderEnd")` by name — which compiles perfectly on every version and simply finds
  nothing on 2023+, so every duct tag would report "no writable LeaderElbow" and the run would do
  nothing while looking like it worked. `RoomTag` and `SpaceTag` were NOT changed and still carry the
  plain properties, so both routes are needed and chosen at runtime. Both fragments now bridge; all six
  compile checks pass on 2020/2024/2027. `FilterRuleCompat` (the `caseSensitive` argument, deprecated
  2023 and removed 2026) was **already handled** by `action-create-view-filter.cs`, which reflects over
  the factory and picks whichever overload the running Revit offers. `RevitCompat` and
  `CeilingGridApiCompat` owed nothing new.
- 2026-08-22 — **general lesson: a compile check cannot see a break that is reached by reflection.**
  Where a fragment reflects on a member NAME to stay version-agnostic, it has bought compilation safety
  at the price of silent failure — so that member's own version history has to be checked by hand, or a
  probe compiled against each installed Revit, as was done here. Recorded at the end of
  [`revit-version-compatibility.md`](revit-version-compatibility.md).
- 2026-08-22 — **transaction discipline: KEEP OURS, and this one was measured rather than assumed.** The
  add-in's `TransactionHelper` guards its rollback with `if (t.HasStarted() && !t.HasEnded())`, which
  looked at first like a robustness gap in this Brain's 195 unguarded `t.RollBack()` calls. It is not.
  The guard exists because the add-in starts the transaction **inside** the try, so a failing `Start()`
  reaches its own catch and `RollBack()` would throw a second exception that masks the real message.
  **This Brain starts the transaction OUTSIDE the try** — measured that day: **184 fragments outside, 0
  inside** — so a failing `Start()` propagates straight out with its real reason and the catch never
  runs. Safe by construction rather than by a check that has to be remembered. Written into the
  transaction section of [`../scripts/README.md`](../scripts/README.md) so the convention is defended
  rather than accidental.
- 2026-08-22 — **selection filters: SKIP as code, BUILD as knowledge.** They are `ISelectionFilter`
  classes for mouse picking and this Brain has no pick — but `SmartConnectSelectionFilter` writes down
  the whole answer to *"what counts as a connectable MEP element"*, now in
  [`live-model/mep-connect-existing-runs.md`](live-model/mep-connect-existing-runs.md): pipe/duct/tray
  curves always, then conduit, flex duct and flex pipe **separately** (split in v3 because a flex run
  can never be trimmed longer), air terminals, fittings, accessories — and **Equipment as a CATCH-ALL**
  for any other connector-bearing family instance. The catch-all is the lesson: naming only the four
  explicit categories *"would have silently dropped everything else, which used to be pickable"* — a
  real regression that happened there. **Prefer "connector-bearing family instance" as the test and use
  the category list only to classify, never to gate**, or sprinklers and plumbing fixtures vanish with
  no message.
