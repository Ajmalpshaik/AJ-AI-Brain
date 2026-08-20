# AJ AI Bridge — Agent Specification

**Document type:** Operating manual / software specification, for LLM consumption.
**Audience:** An AI coding agent (e.g. Claude) operating this Brain against a live Revit session.
**Status:** Production. Some items are marked `NEEDS_REVIEW` or `UNVERIFIED` — treat those as exactly
that, not as confirmed capability. Last full staleness re-check against the routed files: 2026-07-23;
last count/encoding re-check: 2026-08-04.
**Relationship to the rest of this Brain:** This is a complete, self-contained reference — read once,
act without hopping files. It intentionally duplicates summary-level facts that also live in
`START-HERE.md` and `knowledge/*`; where this document gives a summary, the linked topic file is the
authoritative deep-dive. If the two ever disagree, the topic file wins and this document is stale —
update this file, don't trust it blindly.

**Where that duplication actually is** (enumerated 2026-08-04 from a graph of the whole Brain, which
named this file its most-connected cross-topic node — the restatements below are exactly why). These
eight rows are the ones carrying a fact whose home file is elsewhere, so they are the only places this
file can silently go stale when that home file changes:

| This file | Restates a fact owned by |
|---|---|
| §5.2 `verify-duct-connectivity` row | [`knowledge/live-model/hvac-ducts.md`](knowledge/live-model/hvac-ducts.md) — full BFS over `AllRefs`, never a single hop |
| §5.2 `slice-trunk-for-sizing` row | [`knowledge/live-model/hvac-duct-sizing.md`](knowledge/live-model/hvac-duct-sizing.md) — compute the offset break point before `BreakCurve` |
| §5.2 `connect-terminal-branch` row | [`knowledge/live-model/hvac-ducts.md`](knowledge/live-model/hvac-ducts.md) — `ConnectTo` alone inserts no fitting geometry |
| §6.4 connector placeholder size | [`knowledge/live-model/families.md`](knowledge/live-model/families.md) — a new `ConnectorElement` defaults to a generic size |
| §6.4 void-cut verification | [`knowledge/live-model/families.md`](knowledge/live-model/families.md) — unresolved, needs a human visual check |
| §9.1 category ID table | [`knowledge/live-model/core.md`](knowledge/live-model/core.md) — read-only reference, never hardcode |
| §9.2 unit conversion table | [`knowledge/live-model/core.md`](knowledge/live-model/core.md) — `DisplayUnitType` on 2020, `UnitTypeId` on 2021+ |
| §9.3 duplicate `Flow` parameter | [`knowledge/live-model/hvac-terminals.md`](knowledge/live-model/hvac-terminals.md) — use `RBS_DUCT_FLOW_PARAM` explicitly |

All eight were checked against their home file and are in sync as of 2026-08-04. Re-check them whenever
`hvac-ducts.md`, `hvac-duct-sizing.md`, `families.md`, `hvac-terminals.md` or `core.md` changes — that is a much smaller job
than a full staleness pass over this document.

---

## 1. Purpose

### 1.1 What this agent does
Connects an AI coding agent directly to a **live, open Autodesk Revit document** via MCP, so the agent
can query and modify the model in the same session — not generate code for a human to paste in later.
Every action operates on real elements in the currently open document and can be verified with a fresh
read-back in the same conversation.

### 1.2 Activation
Activate for any request about the **currently open Revit model**: counts, sizes, schedules, view
changes (isolate/hide/color), placing or creating elements, tracing real connectivity, tagging, HVAC
layout/routing, family authoring, revisions, sheets, worksharing, links, export. Do not activate for
requests about the compiled Revit add-in's own source code (a different codebase, out of scope here) or
for generic Revit questions with no live model involved.

### 1.3 Scope
- **Any Revit category** — the entire action library is category-agnostic by design (see §3, §9).
- **Any Revit version** the bridge is actually connected to — verify the open version before assuming a
  unit/type API (see §6.3); this Brain's own testing baseline is Revit 2020, but the technique generalizes.
- **182 catalogued universal actions** (`knowledge/universal-actions-reference.md`) spanning visibility,
  parameters, selection, geometry, creation, annotation, dimensions, levels/grids, views, view filters,
  sheets, schedules, revisions, phases, worksharing, links, export, model health, and project-level data.
- **Bespoke multi-stage recipes** for work that doesn't fit the generic filter+action shape (HVAC duct
  routing, MEP connectivity tracing, parametric family authoring).

### 1.4 Limitations
- **One active bridge connection at a time.** A new chat session preempts the previous one instantly;
  the old session's next call reconnects transparently but was not concurrent with the new one. True
  parallelism was never supported — Revit only runs one script at a time regardless.
- **Requires a running Revit-side listener** (a compiled add-in feature, not part of this Brain) with its
  connect toggle switched on. This Brain provides the client-side skills/scripts/relay only.
- **Reflection / assembly-loading into the add-in's own internal classes is hard-blocked** by the bridge
  itself, as a deliberate safety guard. Only plain Revit API calls work.
- **Destructive operations (Delete/Purge/file writes) are refused unless explicitly allowed** per call.
- **Only works from a session that executes directly on the machine with Revit open** — a remote/cloud
  execution sandbox cannot reach a local named pipe, this is architectural, not a config issue.
- **Not an engineering authority.** It can lay out geometry and check it against code limits it has been
  given, with the measured numbers shown; it does not perform hydraulic calculation, pipe sizing, pump/tank
  selection, or density/remote-area design, and it does not decide hazard class or declare a design
  compliant. The AHJ (for these projects **QCDD**) and a licensed engineer own those calls — see
  `knowledge/nfpa13-sprinkler-spacing.md` for how that boundary is stated in practice.

---

## 2. Core Principles

### 2.1 How the agent should think
1. **Verify, don't trust the API/naming at face value.** Revit's own data (`IsConnected`, element names,
   tags) describes intent, not always reality — both have been proven wrong in real sessions. When the
   obvious answer doesn't hold up, find the technique that gets the real answer (geometry, a second
   property, walking the model).
2. **Fresh reads, never recall.** The model can change between messages — by the user, by the agent's own
   prior action, or by an Undo. Re-query before acting on "known" state; read back after changing anything.
3. **Every number is a per-request input, never a default.** Clearances, flows, heights, colors, margins —
   confirm fresh and restate before calculating. Never reuse a past session's value just because it worked
   before.
4. **Plan → split → execute.** Show a short numbered plan for anything non-trivial, run one step at a
   time, check each step's real result before starting the next.
5. **Units: the user speaks mm, Revit's internal API is feet.** Convert explicitly both directions, never
   leave raw feet in a reply.

### 2.2 Safety rules
- Confirm before bulk or hard-to-reverse changes — state what will happen and how many elements, wait for
  a clear go-ahead. Small, easily-undone changes: just do them and report.
- Destructive actions (Delete) require **both** the bridge call's `allowDestructive: true` flag **and** an
  explicit prior confirmation from the user — the flag is not a substitute for asking.
- "Mistake" / "undo" / "previous" → Revit's own native Undo command via the bridge, never a hand-written
  delete/recreate script. If the user says they already undid it themselves, believe them and re-query.
- Never attempt reflection/assembly-loading to route around the bridge's internal-class block.

### 2.3 Performance rules
- Prefer the native `model_summary` tool for a plain category count or one-parameter breakdown — one
  read-only call, no separate ping needed.
- Filter narrowly (category/region/selection) rather than collecting the whole model unfiltered — an
  unbounded query on a large/complex view can produce output large enough to overflow the response.
- Cap report row counts (`maxRows`-style INPUTS) rather than dumping every matched element.
- For anything bulk or hard to reverse, run the filter fragment alone first to confirm the real count
  before appending the action — cheaper to catch a wrong filter before it acts than after.

### 2.4 Decision hierarchy (what to reach for, in order)
1. **Native fast-path tool** (`model_summary`) — plain counts/one-parameter breakdowns.
2. **Filter + action composition** (`scripts/filters/` + `scripts/actions/`) — the default for "which
   elements" + "what to do to them" requests; covers the large majority of daily work.
3. **A recipe** (`scripts/recipes/`) — genuinely bespoke, order-dependent, multi-stage builds that create
   new elements with real geometric/transactional dependencies between steps (duct routing, MEP tracing,
   family authoring). Never force this shape into filter+action.
4. **A new one-off script** — only when nothing above covers the request. Decide immediately afterward
   whether the pattern is reusable; if so, save it as a new fragment before moving on.

### 2.5 Error handling
- Every write wraps its `Transaction` in try/catch that calls `.RollBack()` and appends a clear reason to
  the output on failure — never let an exception propagate as a bare, uninformative error through the
  bridge.
- A multi-transaction sequence with real dependencies between steps (draw a duct, then cap it) runs inside
  one `TransactionGroup` — `Assimilate()` only on full success, `RollBack()` on any failure — so a
  mid-sequence error can never leave a half-built result behind.
- After any write, verify with a fresh read-back — "the API call didn't throw" is not sufficient proof of
  correctness.

### 2.6 Retry logic
- **"Revit UI was blocked by another command/tool or window"** — transient, not a real error. Retry the
  identical call; it recovers on its own. Do not change approach or report failure on the first
  occurrence.
- **Unknown API method/signature** — do not guess from a plausible-sounding name. Use the deliberate
  compile-error discovery technique (§6.5) instead of retrying blind variations.
- **A script that legitimately needs longer than ~60s** (a loop-based build) gets a soft-cancel at 60s and
  a hard backstop later — don't retry a long-running script reflexively; let it finish or explicitly
  cancel.

---

## 3. Tool Reference

Three original tools (`ping`, `run_csharp`, `model_summary`) plus **14 native action tools** (§3.4, added
2026-07-22) are actual registered MCP tools. Everything else in the 182-action library is C# composed by
the agent and executed *through* `run_csharp` — see §3.5.

### 3.1 `ping`
| | |
|---|---|
| **Purpose** | Confirm Revit is open and the bridge is connected before doing anything else. |
| **Inputs** | None. |
| **Outputs** | Success/failure. On failure: an explicit message that the bridge isn't connected. |
| **Constraints** | Single connection only — see §1.4. |
| **Best practice** | Call first if it's been a while since the last confirmed call in this session. Follow every successful ping with the session-snapshot pattern (§3.5, `context-session-start.cs`) — a bare "pong" with no snapshot is an incomplete report to the user. |
| **Common mistakes** | Treating a failed ping as "Revit crashed" — it usually just means the toggle is off or Revit is closed. Ask the user to reconnect rather than escalating. |
| **Performance** | Cheap — safe to call defensively. |

### 3.2 `run_csharp`
| | |
|---|---|
| **Purpose** | Execute a C# snippet against the live, open `Document`/`UIDocument`/`Application`/`UIApplication`. This is the actual execution mechanism behind every action in the library. |
| **Inputs** | `code` (string, required) — the composed C# script text. `allowDestructive` (bool, default false) — must be `true` for Delete/Purge/file-write operations. |
| **Outputs** | Whatever the script's final `return` produces (typically a `StringBuilder`'s `.ToString()`), or a compile/runtime error message. |
| **Constraints** | Reflection/assembly-loading into the add-in's own non-public classes is refused. Destructive ops without `allowDestructive: true` are refused. Multi-statement scripts need an explicit trailing `return` — a bare trailing expression does not reliably produce output. |
| **Best practices** | Compose from existing `filters/` + `actions/`/`creators/` fragments rather than writing fresh (§4). Fill every `INPUTS` block with today's real values — nothing pre-filled in a fragment is a default. |
| **Common mistakes** | Calling `PostableCommand.Undo` (or any single-post command) twice in one call — Revit refuses a second post in the same call; issue each as its own separate `run_csharp` call. Forgetting the fully-qualified-type requirement for some Revit-namespace types in this scripting context (§6.3). |
| **Performance** | One Roslyn compile + execute per call — batch logically-related work into one composed script rather than many round trips, but don't chain unrelated concerns into one script just to save a call. |

### 3.3 `model_summary`
| | |
|---|---|
| **Purpose** | Fast-path read-only count, with an optional single-parameter breakdown, for a category. |
| **Inputs** | Category, optional parameter name. |
| **Outputs** | Count (and breakdown if requested) **plus** Revit version and model title in the same response — no separate ping needed first. |
| **Constraints** | Read-only; single category/parameter only — for multi-parameter, geometry, or model-changing work, use `run_csharp` instead. |
| **Best practice** | Prefer this over composing a filter+`action-count-and-report.cs` script for the simple case — fewer round trips, same result. |
| **Common mistakes** | Reaching for a composed script for a plain count when this tool already covers it. |
| **Performance** | Fastest read path available — always prefer it when the request fits. |

### 3.4 Native tools (14, added 2026-07-22) — the common daily set
These ARE real, individually registered, schema-validated MCP tools — not composed code. Each generates
the same proven C# pattern as the matching `scripts/` fragment internally, and sends it through the exact
same `callBridge()` pipe mechanism `run_csharp` uses. `McpBridgeService.cs` (the Revit-side listener)
needed **no changes** — it already accepts any C# generically; the whole upgrade lives on the Node side.
As of the same day, `mcp-server/` is split one-file-per-tool (mirrors the `scripts/` fragment pattern) —
`mcp-server/index.js` is now just the entry point; see [`mcp-server/tools/README.md`](mcp-server/tools/README.md)
for the routing index into all 17 tool files.

| Tool | Covers |
|---|---|
| `list_elements` | Real items (Id + Category + Family/Type) for a category/filter or explicit Ids |
| `count_elements` | Bare count, any category (not limited to `model_summary`'s fixed list) |
| `hide_elements` / `unhide_elements` | Temp or permanent hide, reverse a permanent hide |
| `isolate_elements` / `reset_isolation` | Temporary isolate, clear it |
| `set_color` | RGB line + solid fill override |
| `reset_graphic_overrides` | Clear overrides |
| `set_transparency` | 0–100% surface transparency |
| `select_elements` | Set the active Revit selection |
| `set_parameter_value` | Bulk-set one parameter (string or numeric mm) |
| `report_parameters` | Parameter table, Element ID included |
| `move_elements` | Translate by an mm offset vector |
| `delete_elements` | Permanent delete — schema requires `confirm: true` literally, refuses the call otherwise |

All 13 element-targeting tools share one input shape: `elementIds` (exact Ids, takes priority) OR
`category` + optional `familyName`/`parameterName`/`comparison`/`valueMm` (mirrors
`filter-by-category-and-numeric-param.cs`). Six of those (the view-scoped graphics/visibility ones —
hide/unhide/isolate/set_color/reset_graphic_overrides/set_transparency) also take `targetViewId`
(optional, defaults to active view — same view-targeting fix as the 11 graphics fragments in §3.5), and
`reset_isolation` takes only `targetViewId` (it has no element filter). A structural regression test
(`npm test` from `mcp-server/`, added 2026-07-23) proves all 17 tools register with correct
names/schemas and that every handler's C#-generation runs to completion and fails gracefully with no
bridge connected. **Still not live-verified against a running Revit** — the test can't reach a real
document; verify each tool on one element before trusting it for a batch.

### 3.5 The rest of the action library — composed code, not separate tools
The remaining actions catalogued in `knowledge/universal-actions-reference.md` (182 total, 14 of which
now also have a native tool above), and the 283 real C# fragments in `scripts/` (49 filters, 145
actions, 33 creators, 8 commands, 33 recipes, 3 examples, 11 read-only `context/` fragments, 1 shared
`lib/` prelude — count re-verified 2026-08-20, and now enforced by `tools/verify-consistency.*` check 5 so it cannot drift
silently again), are **not** individually registered MCP tools. Each is a code template with an `INPUTS` block; the agent picks the
matching fragment(s), fills in real values, pastes them together, and sends the composed text through
`run_csharp`. See `scripts/README.md` for the authoritative fragment index and composition rules.
`NEEDS_REVIEW` entries in the action reference have no fragment yet — do not claim they're built. Prefer
the native tool (§3.4) over composing the matching fragment when one exists — same result, no
code-generation step.

**View targeting is a variable, never hardcoded, on every graphics/visibility action.** All 11
graphics/visibility fragments (`action-hide-elements.cs`, `action-isolate-elements.cs`,
`action-set-color-uniform.cs`, `action-color-by-group.cs`, `action-highlight-vs-rest.cs`,
`action-reset-graphic-overrides.cs`, `action-report-graphic-overrides.cs`, `action-set-transparency.cs`,
`action-section-box-and-zoom.cs`, `action-set-view-crop.cs`, `action-unhide-elements.cs`) take an
optional `targetViewIdInt` INPUTS value — defaults to the active view, but can target any view directly
by Id, including one not currently on screen. Same "never hardcode" principle as element/value (§2.1) —
view was the one exception until this was fixed; don't reintroduce a hardcoded `Document.ActiveView` in
a new graphics fragment.

---

## 4. Recommended Workflows

### 4.1 Step-by-step execution flow (the default shape)
```
1. Ping (if needed) → confirm bridge connected, report session snapshot
2. Identify the request shape:
   a. "Which elements" → pick a filters/ (or creators/ if they don't exist yet) fragment
   b. "What to do to them" → pick one or more actions/ fragments
3. Read each chosen fragment's INPUTS block
4. Fill every INPUTS value from the actual current request — never reuse a prior session's value
5. Paste filter body, then each action body in order, into one script
   (all fragments share the variable names `elements` and `sb` — no glue code needed)
   5a. Optionally paste `scripts/lib/prelude.cs` FIRST, ahead of the filter, for the shared helpers
       (InTransaction, ToFeet/ToMm, ResolveView, ParamText, LevelIdOf, CollectOf, SizeSortKey).
       Optional today — no shipped fragment requires it, and it declares no name a fragment already
       declares. Prefer it in NEW fragments: it is the single place DisplayUnitType, transaction
       rollback and the missing-vs-blank parameter rule live, rather than 80/150/38 separate copies.
6. Add exactly one `return sb.ToString();` as the final line
7. For bulk/hard-to-reverse work: run the filter alone first, confirm the count, THEN append the action(s)
8. Run via run_csharp
9. Verify the real result with a fresh read-back
10. Report per the Response Standards (§10)
```

### 4.2 Tool chaining
Filters and creators both produce `elements`; actions consume `elements`. Any filter/creator chains into
any action with zero glue code — this is the entire point of the split. Multiple actions can chain in
sequence (e.g. `filter-by-category-and-numeric-param.cs` → `action-set-color-uniform.cs` →
`action-isolate-elements.cs` → `action-select-elements.cs`).

### 4.3 Decision tree
```
Is this a plain count / one-parameter breakdown?
  YES → model_summary
  NO ↓
Does the request name one specific value ("the 300x300 VCDs"), not a full breakdown?
  YES → filter + action-report-parameters.cs (lists items WITH Element ID)
  NO ↓
Is this "which elements" + "what to do to them"?
  YES → filter/creator + action (§4.1)
  NO ↓
Is this a bespoke, multi-stage, order-dependent build (drawing/connecting/tracing/authoring)?
  YES → check scripts/recipes/ for an existing one, or design a new TransactionGroup-wrapped sequence (§5)
  NO → smallest correct one-off script; decide immediately after whether to save it as a new fragment
```

### 4.4 Worked example
Request: *"Change the color of the 500mm-height ducts, then isolate and select them."*
```
filters/by-property/filter-by-category-and-numeric-param.cs                 (Ducts, Height, = 500mm)  → produces `elements`
    + actions/color-graphics/action-set-color-uniform.cs                                   → colors `elements`
    + actions/visibility/action-isolate-elements.cs                                         → isolates `elements`
    + actions/selection/action-select-elements.cs                                           → selects `elements`
```
Fully assembled reference: `scripts/examples/color-isolate-select-by-size.cs`.

---

## 5. Advanced Workflows

### 5.1 Bulk operations
- **Explorer first, invoker second.** Run the filter alone, confirm the count matches expectation, only
  then append the action. Applies to anything large in scope or not cheaply undone.
- **Batch logically, not by convenience.** Don't split one coherent bulk operation across many small
  calls just to "be safe" — one well-confirmed composed script is both faster and easier to verify than
  many fragments of the same operation.

### 5.2 Complex, multi-stage automation (recipes)
Real recipes in this library and the pattern they demonstrate:
| Recipe | Pattern demonstrated |
|---|---|
| `draw-main-duct-with-cap.cs` | `TransactionGroup` wrapping draw + cap so a partial failure can't leave an uncapped duct |
| `connect-terminal-branch.cs` | Riser + real elbow fitting + takeoff tee — `NewElbowFitting`/`NewTakeoffFitting` create the physical fitting AND the connection; plain `ConnectTo` alone does not insert fitting geometry |
| `trace-mep-circuits.cs` | Bulk clustering over one-path-at-a-time — process the whole filtered set and let circuits fall out of the geometry, rather than walking one named unit outward |
| `slice-trunk-for-sizing.cs` | HIGH RISK pattern — compute the offset break point BEFORE calling `BreakCurve`, never break at a feature's center and relocate the joint afterward (this specific mistake silently deletes a takeoff fitting and orphans its branch) |
| `verify-duct-connectivity.cs` | Full BFS across every connector in `AllRefs`, not a single-hop check — a naive linear walk gives false "broken" results at any tee/takeoff junction |
| `create-parametric-box-family-with-duct-connector.cs` | Family Editor authoring — reference-plane alignment, EQ-dimension symmetric resize, `AssociateElementParameterToFamilyParameter`, all inside one `run_csharp` call where an exception anywhere later rolls back everything earlier, even committed transactions |
| `generate-room-coverage-layout.cs` | Sample → lay out → verify, where the verification must cover BUILDABILITY as well as the obvious metric — an optimiser told only "cover the floor" returns device positions outside the walls and still reports zero gaps (§6.7) |

### 5.3 Multi-step reasoning
For a request that splits into a genuine sequence with real dependencies, state the plan before executing
(§2.1.4), and treat each stage's real result — not the absence of an exception — as the gate for starting
the next stage.

### 5.4 Recovery strategies
- **Orphaned branch after a bad trunk slice**: trace the chain from the terminal connector-by-connector
  until hitting an open connector (don't trust `IsConnected` on the terminal alone), find the current
  trunk piece whose curve geometrically contains that open point, `NewTakeoffFitting` it back in.
- **A revision that vanished**: an unattached Revision can be auto-purged the next time any
  sheet-revision association is touched, project-wide, not just the sheet being edited. Match revisions
  by their own set data (description/date/issued-to), never by `SequenceNumber`, which renumbers when
  orphans are swept.
- **A preempted bridge session**: reconnects transparently on its own next call — no manual recovery
  needed, just re-ping if unsure.

---

## 6. Lessons Learned

Curated highlights — each topic file has the full write-up; this is the "don't rediscover this the hard
way" summary.

### 6.1 Connectivity & tracing
- `Connector.IsConnected` can read `false` on a genuinely physically-connected run — a connector-graph
  walk alone can miss real connections. The reliable method is geometric: match connector positions within
  a small tolerance. Full detail: `knowledge/live-model/mep-trace.md`.
- Naming/tag conventions can be actively misleading about real physical wiring, not just incomplete —
  verify by tracing, never assume a pairing from matching codes/names.
- `MechanicalUtils.BreakCurve` can reassign which ElementId keeps which physical segment after a split —
  never trust the original Id to be the near/equipment-side piece; re-locate each piece geometrically
  after every cut (confirmed live 2026-07-23; bit `split-duct-near-equipment.cs` for real).
- After any `BreakCurve` split, join the pieces with a real Union fitting (`doc.Create.NewUnionFitting`),
  never a bare `ConnectTo` — a fitting-less direct joint between colinear same-size pieces can be
  silently re-merged by Revit, losing the split entirely (confirmed live 2026-07-26 during a 4-trunk
  sizing-prep build; the union is what physically preserves the split until sizing).
- Always read a connector's own real outward direction (`Connector.CoordinateSystem.BasisZ`) before
  drawing toward or from it — never assume an axis (`XYZ.BasisX` etc.), in a test fixture or anywhere
  else; an assumed axis produced a hard-to-diagnose false alarm during live verification.
- `FilteredElementCollector.UnionWith()` does NOT preserve either side's own quick-filters — a
  `.WhereElementIsNotElementType()` applied per-piece before the union is silently lost in the merged
  result (TYPE elements leak in). Apply such filters ONCE, after all `UnionWith` calls, on the combined
  collector (confirmed empirically 2026-07-23; bit 3 fragments).

### 6.2 View & graphics
- View state (isolation, color overrides) can be cleared between messages by the user, by Undo, or by
  direct Visibility/Graphics edits — always re-check before assuming a prior turn's state still holds.
- The pattern-visibility getters (`IsSurfaceForegroundPatternVisible` etc.) return `true` even on a
  completely untouched element — they mean "would show if set," not "has an override." The real signal is
  color/pattern *validity*, not the visibility getters.
- Current selection can include elements outside the active view's own collector (e.g. tags belonging to
  a different open view) — a mismatch between selected-count and in-view-count is not automatically a bug.
- A **view-scoped** `FilteredElementCollector(Document, viewId)` can UNDER-REPORT immediately after a
  create+group transaction — measured 20 curve elements / 1 group where the byte-identical query moments
  later returned 74 / 3, with nothing created, deleted or hidden in between. Never conclude that earlier
  work was deleted from a view-scoped read; confirm document-wide (`Document.GetElement`, or an unscoped
  collector grouped by `OwnerViewId`) first — those were correct on the first try. Detail:
  `knowledge/live-model/core.md`.

### 6.3 Version & type gotchas
- Check which Revit version is actually open before assuming a unit API — `UnitTypeId` is 2021+ only; use
  `DisplayUnitType` on 2020 and earlier.
- Several Revit-namespace types need to be fully qualified in this bridge's scripting context
  (`Autodesk.Revit.DB.Structure.StructuralType`, `Autodesk.Revit.DB.Mechanical.Duct`) — a bare name fails
  to compile even though it would resolve in a normal add-in project.
- `new ElementId(someLong)` fails to compile in this API surface (no `(long)` constructor overload) — cast
  explicitly to `(int)`.
- **Revit 2020 hard API gaps, confirmed live via exhaustive reflection (2026-07-23)** — these are
  UI-only, not scriptable at all on this version; the matching fragments now report the limitation
  instead of attempting it: **no Scope Box creation** (`create-scope-box.cs`), **no Phase creation**
  (`Document.Phases` is read-only — `action-create-phase.cs`), **no Design Option activation** (only a
  read-only getter exists — `action-set-design-option.cs` requires manual activation first).
- More 2020 version traps confirmed live: `PDFExportOptions` doesn't exist (PDF goes through
  `Document.PrintManager` + a virtual printer driver — `action-export-sheets-to-pdf.cs`);
  `SpatialElement.Volume` doesn't exist as a property (read
  `get_Parameter(BuiltInParameter.ROOM_VOLUME)` instead); schedule Combined Parameter fields use
  `TableCellCombinedParameterData` (a `CombinedParameterRule` class never existed in any version).
- Room/Space `Element.Name` auto-combines to "{name} {number}" the moment any Number exists (which is
  always — one is auto-assigned at creation). Read `BuiltInParameter.ROOM_NAME` for the plain name;
  matching on `Element.Name` silently misses (bit `filter-by-space.cs` for real).
- Reassigning `LocationCurve.Curve` in place to trim/extend a line is silently a no-op (no exception,
  clean commit, geometry unchanged) when the two curves share a coincident endpoint — the normal case
  when filleting an existing corner. Delete+recreate instead (bit `action-fillet-elements.cs`).

### 6.4 Family & geometry
- A newly created `ConnectorElement`'s Width/Height default to a generic placeholder, not the hosting
  face's real size — set the actual parameters explicitly.
- Extrusion face `.Reference` is only reliably populated on a horizontal (Z-normal) sketch plane — a
  vertical sketch plane silently loses references on most faces.
- Void-form cuts (`NewExtrusion(isSolid: false, ...)`) have an open, unresolved verification problem in
  this Brain's own testing — don't assume a volume/geometry query proves a void cut worked; get a human
  visual check until this is resolved.

### 6.5 Discovering an unknown API signature
Deliberately trigger a compile error and read it, rather than guessing from memory or a plausible name:
(1) zero-arg call confirms existence vs. typo, (2) a bogus named-arg call surfaces Roslyn's best-overload
guess, (3) a plausible-typed-args-with-null call compiles (confirming the shape) and throws a runtime
exception naming the real unclear parameter. Three steps, no memorized guessing.

### 6.6 Performance bottlenecks observed
- Whole-model, unfiltered collectors on a large document are the main source of oversized output — always
  filter by category/region/selection first.
- A registry/scoring-based placement algorithm (evaluating candidates against everything already placed)
  scales far better than place-then-resolve-overlaps-after for large batches — the resolver pass on a
  large batch can end up moving roughly half the elements to converge, silently destroying an earlier
  correctness property (e.g. flow-direction-correct siding) that a smarter first-pass placement wouldn't
  have broken in the first place.

### 6.7 Layout & optimisation (device coverage, spacing, anything solved by search)
- **A verified metric can still be the WRONG metric.** A room-coverage run reported "3,243/3,243 points
  covered, zero gaps" — measured, true, and useless: 6 of 21 device positions were outside the room, past
  the wall, because the optimiser was told to cover floor and a circle centred beyond the wall still covers
  floor. Every optimisation needs its physical constraint stated as an explicit, reported check ("how many
  of these can actually be installed?"), not left implied by the objective.
- **Where a lattice is PHASED matters as much as its spacing.** An unconstrained lattice starting at
  `bb.Min - r` lands relative to the walls by accident; the same room phased inset from the walls needed
  FEWER devices (20 vs 21) with none outside. Cheaper and buildable at once — the constraint was not a cost.
- **Greedy set-cover leaves redundant picks — always prune and re-verify.** It selects by immediate gain and
  never re-checks, so a pick can become unnecessary once later ones land: a "20 devices" answer contained a
  circle whose removal changed nothing. Over-reporting by one is a real quantity error on a real BOQ.
- **A textbook optimum for the infinite plane does not survive four walls.** Hexagonal covering (r·√3) beats
  square (r·√2) on an unbounded plane, but inside a room the edge circles are half-wasted: measured 20
  square (both code caps passing) vs 19 staggered (both failing marginally) vs 22 compliant staggered. Never
  quote the plane figure as a room saving.
- **Try more than one construction of "the same" idea before reporting a number.** Two equally reasonable
  ways to build staggered rows — shifted rows with one device fewer vs one more, ends pulled inside the wall
  — gave 32 vs 19 devices on the identical room and radius. Search the arrangement and verify each candidate;
  a single plausible construction is not an answer.
- **Ask which RULE SET governs before optimising anything, not after.** Three rounds of "verified, zero
  gaps" on a room layout said nothing about the code that governed it. Once NFPA 13 was actually read, two
  things changed: the metric cap in use (15 ft is 4,572 mm, not the 4,600 that had been assumed — a rounded
  conversion is LENIENT and passes layouts the code fails), and the number of limits (four, not two — the
  missing one being max floor area per device, which no covering algorithm looks at and which sets a hard
  MINIMUM device count). The same 20-device zero-gap layout was legal for light hazard and 4 devices short
  for ordinary hazard. **Derive the grid FROM the limits; do not pick a radius and check afterwards.**
- Full detail and the live-verified figures: `scripts/recipes/generate-room-coverage-layout.cs` header.
  Fire sprinklers are their own job with their own rules: `skills/ajtools-fire-sprinkler-layout/SKILL.md`
  and `knowledge/nfpa13-sprinkler-spacing.md`.

---

## 7. Best Practices

### 7.1 Fastest methods
- `model_summary` over a composed script for plain counts/single-parameter breakdowns.
- Session snapshot (`context-session-start.cs`) once per ping, not re-derived per request. It is the
  fuller opening check: Revit version AND which API generation is live, document, units, size, unloaded
  links, closed worksets, warnings, active view. `context-active-view.cs` remains the lighter
  view-only snapshot for re-checking the view mid-session.
- Compose from existing fragments — writing fresh C# every time is strictly slower and more error-prone
  than filling an already-correct template's `INPUTS`.

### 7.2 Lowest token usage
- Read the relevant index, open exactly one topic file — never read a whole knowledge folder to find one
  fact.
- Cap report row counts; don't request/return more detail than the question needs (bare count vs. table
  vs. full item list — see §10).
- Don't re-derive a technique that's already documented (e.g. the MEP bulk-clustering trace method) —
  read it, don't reason it out from scratch each time.

### 7.3 Most reliable workflow
Explorer-first for anything bulk (§5.1) + verify-after for anything written (§2.5) + fresh reads instead
of trusting a prior turn's result (§2.1.2) is the combination that has actually prevented real incidents
in this Brain's history, not a theoretical best practice.

### 7.4 Production recommendations
- Never ship a "count" or "verified" claim that wasn't actually re-queried this turn.
- Treat every `NEEDS_REVIEW` item in the action reference as unbuilt until it has a real fragment and a
  live test — don't imply capability that doesn't exist yet.
- When a script is genuinely new, run it once, verify, THEN decide whether to save it as a fragment — 
  don't save unverified code as if it were proven.

---

## 8. Anti-Patterns

**Never do these:**
- Guess a Revit API method/parameter name from what sounds plausible — verify (§6.5) or check the
  knowledge base first.
- Trust `Connector.IsConnected`, element names, or tags as proof of real connectivity — verify
  geometrically.
- Reuse a numeric value (clearance, flow, height, color) from a past session as a default — every number
  is a per-request input.
- Search the scripts folder by today's element noun (`*duct*`, `*pipe*`) to decide whether a reusable
  fragment exists — route by request *shape* instead; generic fragments are not named after categories.
- Call a single-post command (e.g. `PostableCommand.Undo`) twice in one `run_csharp` call.
- Break a curve at a feature's exact center and relocate the joint afterward when a dependent fitting
  (e.g. a takeoff) is hosted there — compute the true offset point first and break there directly.
- Run tag-vs-tag and tag-vs-duct overlap resolution as two sequential passes — resolving one can
  reintroduce the other; combine into one loop.
- Skip the explorer-first count-check before a bulk or destructive action because "it's probably fine."
- Report a geometric or optimiser result without checking it is physically installable — "zero gaps" said
  nothing about 6 of 21 devices sitting outside the room (§6.7).
- Trust a greedy optimiser's output as the minimum without a prune-and-re-verify pass.
- Conclude that elements are missing or were deleted based on a view-scoped collector read (§6.2).
- Reflect into the add-in's own internal (non-public) classes to bypass a normal-API limitation — this is
  a deliberate, permanent block, not a bug to route around.
- Claim a `NEEDS_REVIEW` action is production-ready.

---

## 9. Quick Reference Tables

### 9.1 Category IDs (reference only — never hardcode in a script; use `BuiltInCategory.OST_*`)
| Category | Id | Category | Id |
|---|---|---|---|
| Walls | -2000011 | Sheets | -2003100 |
| Doors | -2000023 | Schedules | -2000573 |
| Windows | -2000014 | Levels | -2000240 |
| Floors | -2000032 | Grids | -2000220 |
| Roofs | -2000035 | Views | -2000279 |
| Ceilings | -2000038 | Viewports | -2000510 |
| Rooms | -2000160 | MEP Spaces | -2003600 |
| Stairs | -2000120 | Plumbing Fixtures | -2001160 |
| Columns | -2000100 | Lighting Fixtures | -2001120 |
| Structural Framing | -2001320 | Mechanical Equipment | -2001140 |
| Curtain Wall Panels | -2000170 | Electrical Equipment | -2001040 |
| Curtain Wall Mullions | -2000171 | Generic Model | -2000151 |
| Furniture | -2000080 | Casework | -2001000 |
| Planting | -2001360 | | |

Full detail and provenance: `knowledge/live-model/core.md`.

### 9.2 Unit conversion
| From | To | Call |
|---|---|---|
| mm (user-facing) | internal feet | `UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS)` (2020) or `UnitTypeId.Millimeters` (2021+) |
| internal feet | mm (reply) | `UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS)` |
| L/s, CFM | internal | `DUT_LITERS_PER_SECOND`, `DUT_CUBIC_FEET_PER_MINUTE` |
| m² | internal | `DUT_SQUARE_METERS` |

### 9.3 Known duplicate/ambiguous parameter names
| Symptom | Resolution |
|---|---|
| Air terminal exposes two params both named "Flow" | Use `BuiltInParameter.RBS_DUCT_FLOW_PARAM` explicitly — the other resolves to `INVALID` |
| `DuctSystemType` vs `MechanicalSystemType` | Different enum types with similarly-worded names — verify via reflection on `PropertyType.FullName`, don't guess |

### 9.4 Frequently used fragment combinations
| Request shape | Compose |
|---|---|
| "How many X?" | `filter-by-category.cs` + `action-count-and-report.cs` |
| "How many X, what size?" | same + `wantBreakdownTable = true` |
| "How many X, size + total length?" | `filter-by-category.cs` + `action-report-length-by-size.cs` |
| "Show me/list the [specific value] X" | matching filter + `action-report-parameters.cs` |
| "Color/isolate/hide/select X" | matching filter + the matching action |
| Full fragment index | `scripts/README.md` |

---

## 10. Response Standards

### 10.1 Output format
- **Quantity/count question** → bare number, one line. No table, no prose, unless sizes/breakdown asked.
- **Size/breakdown question** → schedule-style markdown table (`Size (mm) | Qty`), sorted by size
  ascending, never by quantity.
- **Specific/narrowed value** ("the 300x300 VCDs") → the actual item list with **Element ID** for each —
  not a count, not an aggregate table. The ID is what makes the next request actionable without
  re-filtering.
- **Substantive work** (build/fix/check/live-model change) → close with the 7-point Final Report (§10.3).

### 10.2 Error format
State plainly what failed and why (the caught exception's message), confirm nothing was changed (rollback
succeeded), and what the user's options are — never a bare stack trace with no interpretation.

### 10.3 Success format — the 7-point Final Report (substantive work only)
```
1. What I understood the request to be
2. What already existed that got reused
3. Split / update / create, and why
4. What was live-tested, and the real result
5. What still needs the user's decision
6. What got saved/documented so next time is faster
7. Any good next step, flagged without being asked
```
Keep it plain-language and only as long as the work warrants — don't pad a small fix out to hit all 7
points at length.

### 10.4 Logging format
When something new is saved (a fragment, a knowledge fact, a skill), log one dated line in
`knowledge/brain-log.md`: what changed and why — same standard as every other change in this Brain.

---

## 11. Future Extensions

**Reserved for planned work — not yet built. Do not imply these exist.**

- ~~Native tool registration for the top common actions~~ — **DONE, 2026-07-22.** See §3.4. Turned out to
  require zero add-in changes — `McpBridgeService.cs` already accepts any C# generically, so the whole
  thing was a Node-side addition, now split one-file-per-tool under `mcp-server/tools/`. 14 tools built;
  the remaining ~5-10 candidates from the original "top 15-20" estimate can follow the same pattern
  (copy an existing `tools/*.js` file's shape) on request.
- **Combined Model Health Report** — one action aggregating warnings + unused elements + family bloat +
  worksharing status, beyond what any single existing action currently reports alone.
- **Lean/decision-tree variant of this spec** for smaller or local models, if this Brain is ever handed to
  a user running a weaker model than this document assumes.
- **Audit Model, Set Shared Coordinates** — real Revit capabilities, deliberately left
  `NEEDS_REVIEW` (see `universal-actions-reference.md`) pending a safer, more explicit design. (Purge
  Unused left this list 2026-07-22: `action-purge-unused.cs` now covers the provably-correct subset —
  unused View Templates/Filters/Materials, dry-run by default.)
- **Native tools for the remaining common actions not yet ported**: creation tools (`create_room`,
  `create_levels`, `create_schedule`), `color_by_group`, `report_location`/`report_bounding_box`,
  `copy_elements`, `rotate_elements`, `rename_element`. Same pattern as §3.4, straightforward to add.

### Plugin architecture note
This Brain's own extension mechanism is `skills/brain-self-maintain/SKILL.md` — new
skills/knowledge/fragments are added there, following the routing and size rules in that file, not by
growing this document indefinitely. This document should be refreshed to reflect major additions, not
used as the place new detail is written first.
