# Live Model Notes — AJ AI Bridge scripting

> Entry point of the live-model knowledge set. Index: [`README.md`](README.md) — route from there to the topic you need.

Technical notes specific to writing C# snippets run via the `mcp__aj-tools-aj-ai__run_csharp` /
`ping` MCP tools against the user's live, open Revit document. This is about the ad-hoc bridge scripts
only — a separate concern from the Revit add-in's own compiled source code, which this Brain doesn't
cover (a gotcha in one doesn't necessarily apply to the other).

**This file explains the recipes; [`scripts/`](../../scripts/README.md) holds the actual working
code.** Check the scripts folder before writing new C#, and when a recipe here changes, update its
script too so the two never drift apart. Two shapes live there: `filters/` + `actions/` — small,
element-type-agnostic fragments composed per request (e.g. "which elements" + "what to do to them", see
the scripts README for the user's own worked example) — and `recipes/`, for the genuinely bespoke,
order-dependent, multi-stage builds below (HVAC placement/routing, MEP trace) that create new elements
rather than just act on existing ones and don't fit the filter+action shape.

**Contents** (this file is long — jump to the section you need, don't re-derive what's already here):
- Bridge basics — ping first, report version+model, script globals, what's blocked
- Revit version + unit conversion — 2020 `DisplayUnitType`, mm↔feet, fully-qualified types
- View visibility patterns — isolate/hide/reset, verify view state fresh each turn
- Tracing real MEP connectivity — bulk clustering, geometric trace, color-coding
- Undoing a mistake — native Revit Undo via PostCommand, never a delete script
- HVAC air terminal layout — Space airflow params, matched counts, checkerboard `(row+col)%2`,
  near-square row formula, grid orientation, Flow-parameter gotcha, multi-FCU zoning, `IsPointInRoom` Z
- Rotating equipment to face a target — connector identification (Fresh Air decoy), rotation math
- Drawing a duct between two points — sizing to the source connector, BreakCurve + explicit reconnect
- Branch duct from terminal to main duct — riser + real elbow + takeoff, cap-end recipe (7 steps)
- Slicing a main trunk for duct sizing — HIGH RISK, offset-cut recipe, orphaned-branch recovery
- Posting AJ Tools' own ribbon commands — doesn't work, don't re-attempt

## Bridge basics
- **"Don't go to Revit" is an absolute stop — obey it without asking why** (the user's rule, stated
  2026-08-14: *"if i say do not go to revit becose another session is running"*). Make **no** bridge call
  at all — not a ping, not a read-only count — until he says it is free again. The reason is the
  documented one-connection-at-a-time limit (`AGENT-SPEC.md` §1.4): a call from here does not queue
  behind his other session, it **preempts** it, so a "harmless" read can break work he is in the middle
  of. Keep working on everything that does not need the model — the Brain's own files, reports built
  from numbers already read this session, dashboards, planning — and say plainly which figures are from
  an earlier read rather than fresh. Related: the same limit is what makes parallel bridge calls
  unreliable — six at once on 2026-08-14 produced `the AJ AI bridge closed the pipe connection` on one
  of them; go sequential.
- For a common category count with one optional parameter breakdown, prefer the native
  `model_summary` MCP tool when it is exposed. It performs one read-only bridge call and returns the
  Revit version and model title, so a separate ping is unnecessary. Keep `run_csharp` for complex,
  multi-parameter, geometry, and model-changing work.
- Always `mcp__aj-tools-aj-ai__ping` first if it's been a while — if it fails, Revit is closed or
  the AJ AI pane's Connect AJ AI Bridge toggle is off. Ask the user to reconnect rather than guessing.
- **Follow the first successful ping of a session with `scripts/context/context-session-start.cs`** —
  one call, and it answers everything a session would otherwise assume (Ajmal's rule, 2026-08-20:
  *"everytime while pinging or connection to revit check the all things like what is the version of
  revit what is the model"*). It reports the Revit version **and which API generation is actually live**
  (64-bit ElementId? ForgeTypeId units? split Dimension classes?), the document and its path, whether it
  is workshared and where the central sits, project name/number/client, **what unit the project really
  displays**, model size, **links that are NOT loaded**, **worksets that are CLOSED**, design options,
  phases, warning count, the active view and the current selection.
  Four of those lines exist to catch a *silently wrong answer* rather than an error:
  an unloaded link, a closed workset and an unexamined design option each make a query quietly return
  LESS than the truth, and a project displaying metres rather than millimetres makes every figure wrong
  by a thousand. None of them throws; all of them are invisible unless you look at the start.
  `context-active-view.cs` stays as the lighter view-only re-check for mid-session use.
- **Whenever reporting a successful ping, always also report the session snapshot** — the user wants this
  every time, not just on request (rule extended 2026-07-16: active view added to the original
  version+model rule). Get it in one follow-up `run_csharp` call by running
  [`scripts/context/context-active-view.cs`](../../scripts/context/context-active-view.cs), which returns
  everything the report needs: Revit version, model title (+ family vs project, worksharing), active view
  name/type, open views, and current selection count. Report compactly, e.g. "Connected — Revit 2020,
  model: MODEL PROJECT, active view: {3D} (3D), nothing selected." A bare "pong" with no snapshot is an
  incomplete ping report.
- Globals available directly in scripts: `Document`, `UIDocument`, `Application`, `UIApplication`. No
  `using AJTools...` — the script isn't compiled with a reference to AJTools.dll.
- Destructive ops (Delete/Purge/file writes) are refused unless `allowDestructive: true` is explicitly
  passed. This is deliberate — don't route around it.
- **The destructive-op guard reads the whole script as TEXT, including plain output strings, and it is
  CUMULATIVE** (found live 2026-07-26 while verifying `recipes/model-health-audit.cs`). That read-only
  audit — no `Document.Delete` anywhere, only counts — was refused because two of its output lines together
  said "Purgeable (dry-run)" and "delete via ... action-purge-unused.cs". Each line alone passed; together
  they crossed the threshold. **The fix is to soften the OUTPUT WORDING of genuinely read-only scripts
  ("Unused, removable later", "see X") — never to pass `allowDestructive: true` just to get a read past
  the guard.** Doing that would train away the one protection that catches a real mistake.
- **`ReferenceIntersector` (ray-casting) ONLY FINDS WHAT ITS 3D VIEW SHOWS — a silent, dangerous
  failure mode** (found live 2026-07-26). It runs inside a `View3D` and obeys that view's hidden
  categories, section box, view filters and closed worksets. A hidden category is invisible to a ray, so a
  probe reports "nothing there" with a wall standing right in front of it — no error, no warning, just a
  confident wrong answer. Proven with identical code on the same element: view `{3D}` with Walls hidden
  returned **0** hits; view `3D Plumbing` with Walls visible returned **4**. Always check
  `view.GetCategoryHidden(catId)` before trusting an empty ray result, prefer a full-visibility
  coordination view, and never let a ray-driven MOVE run against a partially hidden model — it will snap
  elements onto whatever happens to be visible behind the real surface. The ray fragments now warn (and
  `action-move-to-ray-hit.cs` refuses) when the target category is hidden.
- **Reflection / assembly-loading is hard-blocked** ("Loads assemblies or uses reflection to bypass normal
  API usage") — cannot reach into the add-in's own internal (non-public) classes this way. Only plain
  Revit API calls work. If a task seems to need this, do it with plain Revit API calls instead, or ask the
  user to run the real tool themselves.
- Multi-statement scripts need an explicit `return` — a trailing expression-without-semicolon (Roslyn
  scripting convention) does not reliably produce output here; the last line should be `return sb.ToString();`
  not just `sb.ToString();`.
- **A bridge call can transiently fail with "Revit UI was blocked by another command/tool or window"**
  even with no user action in between — this is Revit being momentarily busy, not a real error. Simply
  retry the same call; it recovers on its own. Don't treat one blocked response as a reason to change
  approach or report a failure.
- **Discover a category's real parameter names/IDs before bulk reading or writing on it, don't guess from
  a plausible name.** Run [`action-report-parameters.cs`](../../scripts/actions/reporting/action-report-parameters.cs)
  (or a one-off parameter dump) against one representative element of a category the first time it comes
  up in a session — parameter names vary by family/template, and a guessed name that happens to work on
  one project can silently miss or fail on another.
- **Watch for unbounded output on a large/complex query** — collecting or reporting every element in a
  big 3D view, or a whole-model dump with no category/region filter, can produce a very large response.
  Prefer a targeted filter (category, region, selection) over a blanket collector, and cap row counts on
  report actions (`maxRows` INPUTS already do this on the `report-*` fragments) rather than dumping
  everything.
- **Re-check the model/document identity if a session runs long.** The user can close, switch, or open a
  different Revit document without saying so. If a later call's `context-active-view.cs` snapshot shows a
  different model title than earlier in the same conversation, treat every earlier element ID / view ID /
  family name from before the switch as invalid — re-orient before continuing, don't assume continuity.
- **An empty result (zero elements, `[]`) is a valid answer, not an error.** If a correctly-scoped
  filter returns nothing, report the honest zero — don't assume the script failed, silently loosen the
  filter, or retry until something appears. Only re-check the scope if the user's wording suggests the
  filter itself was wrong.
- **Never invent or guess an ElementId.** Every id a script acts on must come from a query in this same
  session, recent enough to still trust (fresh-reads rule) — ids remembered from an earlier conversation
  or "probably still the same" are how a script silently acts on the wrong element.
- **Spatial words ("move it left / up / north") are view-relative — resolve the real direction before
  acting.** Left/right/up/down depend on the active view's orientation (and north can mean true vs
  project north); never guess a sign or an axis. Read the active view's orientation and a real reference
  (grids, levels, a named target element) first, restate the resolved direction plainly ("left in this
  view = −X, toward Grid A"), then move.
- **One composed script beats many bridge calls.** When many elements need the same change, run one
  filter+action script in one transaction — not a per-element loop of separate `run_csharp` calls.
  Fewer round-trips, a single undo step, one thing to verify.
- **Verify small after changing.** Read back fresh (never skip that), but scope the read-back to what
  changed — the count, or the changed elements' new values — not a whole-category re-dump (pairs with
  the unbounded-output rule above).
- **A same-script inline verification is not enough proof — re-check with a SEPARATE later call.** Found
  live 2026-08-01 (project 4355, filter fill-color cleanup across 6 view templates): a script cleared
  `TRG_Accessories_Duct`'s fill override, reported "removed" for all 6, and even an immediate same-script
  read-back confirmed unset. A later script (after an unrelated restore pass touching 5 sibling filters,
  which by inspection never referenced this filter's name) found all 6 back to their pre-clear fill color.
  Root cause not confirmed — the restore script's logic looked correct on inspection, so this may be a
  bridge/transaction-commit timing quirk rather than a code bug. Re-clearing and verifying in a THIRD,
  independent call held. **Practical rule: after any filter/graphic-override mutation across multiple
  elements, don't trust a same-call verification alone — issue one more read-only call afterward,
  separately, before reporting success**, especially when other mutating calls run in between.
- **On a workshared model, the bridge can't sync or relinquish.** After bulk changes (the context
  snapshot reports worksharing), remind the user to Synchronize with Central themselves — edited
  elements stay borrowed by them until they do.
- **A view-scoped `FilteredElementCollector(Document, viewId)` UNDER-REPORTS right after a create+group
  transaction — it can miss elements that are genuinely there and fully visible.** Measured 2026-07-27:
  immediately after drawing + grouping 20 detail circles in `1 - Mech`, the query
  `FilteredElementCollector(Document, view.Id).OfClass(typeof(CurveElement))` returned **20** curve
  elements and **1** group; the byte-identical query re-run moments later returned **74** and **3**. Nothing
  had been created, deleted or hidden in between — checked: `IsHidden(view)` false for every member, Lines
  category not hidden, no crop box, no temporary hide/isolate. The first read simply didn't see the
  pre-existing elements. This is dangerous precisely because the *wrong* answer looks like a clean fact and
  invites the conclusion "the user's earlier work was deleted." **Never conclude something is gone from a
  view-scoped read alone.** Confirm existence document-wide first (`Document.GetElement(id)`, or an
  unscoped collector grouped by `OwnerViewId`) — those were correct and complete on the first try — and
  only then, if you truly need visibility, re-run the view-scoped query.
- **An element hosted on a linked model's face (not this document's own levels) reports `LevelId ==
  InvalidElementId` — this is expected, not a bug.** Grouping such elements by level via the normal
  `LevelId`/level-parameter lookup silently fails for them. If level-grouping matters for an element like
  this, read its real Z coordinate (`get_Location`-style bounding-box or `LocationPoint.Point.Z`) and
  compare against known level elevations instead.
- **A parameter report by display name gives a BLANK column for a parameter that doesn't exist — it never
  says "no such parameter".** Verified live 2026-08-04: `report_parameters` asked for `Level` on Ducts
  returned an empty cell on every row, which reads exactly like "the parameter is there but unset." The real
  name on a duct is **`Reference Level`** (`BuiltInParameter.RBS_START_LEVEL_PARAM`); with that name every
  row filled in immediately. So a blank column is ambiguous between "empty value" and "wrong parameter
  name", and on a takeoff or a schedule that ambiguity is a silent wrong answer, not a visible error.
  **Before reporting a blank column as missing data, confirm the name exists on one sample element** — loop
  `element.Parameters` and print the definition names — rather than concluding the value is unset. Applies
  to any name-based parameter read, the native tool and hand-written script alike.
  - **A blank column has a THIRD cause: the name is right, but the parameter lives on the TYPE and the
    native `report_parameters` tool reads the INSTANCE only.** Verified live 2026-08-11 on `Doors` in an
    imperial model: the native tool asked for `Width` and `Height` returned an empty cell on all 4 rows,
    while `Door-Single-Panel : 30" x 80"` plainly had both. The same display names resolved immediately via
    `Document.GetElement(e.GetTypeId()).LookupParameter("Width")` → 762 mm / 2032 mm. So the name was never
    wrong and the value was never unset — the native tool simply doesn't fall back to the type.
    [`action-report-parameters.cs`](../../scripts/actions/reporting/action-report-parameters.cs) **does**
    fall back (its `includeTypeParameters` input), so when the native tool gives you a blank size column,
    re-run through that fragment before believing the model is missing data. Door/window sizes are type
    parameters on most standard families, so this is the common case, not an edge case.
  - **Never read a size off the TYPE NAME** — `30" x 80"` is a label a human typed and can disagree with the
    real parameter values. Read the parameters and let the name agree with them, not the reverse.
- **`Parameter.Set()` RETURNS A BOOL, and Revit uses it to refuse a value — it does not throw.** Verified
  live 2026-08-07: `duct.LookupParameter("Width").Set(0.0)` returned **false** and left the width at
  300 mm; the same parameter set to 450 mm returned **true** and took. The parameter was neither
  read-only nor missing, so every "can I write this?" check passed — the value itself was simply not
  acceptable. **A script that ignores the return value reports a write that never happened**, which is
  how `action-remove-parameter-value.cs` came to announce "zeroed on 3" for three ducts still 300 mm
  wide. Always write `bool ok = p.Set(...)` and count only on `ok`.
  Where this bites in practice: any numeric parameter with a valid range (0 is usually outside it), and
  **anything that must be unique** — Room Number and Sheet Number refuse a value another element already
  holds, one element at a time, so a renumber can silently leave gaps while claiming a clean sequence.
- **An MEP size can be accepted AND changed: `Set()` returns true and Revit SNAPS the value to the
  type's size table.** Verified live 2026-08-07: a pipe asked for **77 mm** returned `true` and came out
  **80 mm**; asked for 50 mm it came out 50 mm. So honouring the bool is necessary but NOT sufficient —
  for a size, only reading the value back tells you what the model holds. Ducts and cable trays accepted
  arbitrary sizes (337 mm went in unchanged) in the same test, so this is per-type behaviour driven by
  the type's size table, not a fixed rule you can predict.
  **Read it back after `Document.Regenerate()`, not straight after `Set()`** — immediately after the set
  the parameter still reports the value you asked for, and the snap only lands at regeneration. That one
  detail is the difference between a size report that is true and one that merely echoes the request.
  `create-duct.cs`, `create-pipe.cs`, `create-cable-tray.cs` and `create-conduit.cs` all now report
  ACTUAL size and flag a REFUSED or SNAPPED value explicitly.
- **A script that throws AFTER its transaction committed still rolls back COMPLETELY — the bridge wraps
  the whole script, so a late exception undoes even committed work.** Verified live 2026-08-09: a wall
  trim committed its Transaction, then a stray `Document.Regenerate()` AFTER the commit threw
  ("Modification of the document is forbidden" — Regenerate needs an open transaction), and the read-back
  showed the wall untouched. Two rules fall out: (1) never call `Regenerate()` outside a transaction —
  `Commit()` already regenerates, so a post-commit Regenerate is both illegal and pointless; (2) this
  rollback is protective, not a bug — a bridge script either fully lands or fully doesn't, so after ANY
  script error re-read the model instead of assuming the pre-error lines stuck.
- **A script that throws AFTER its transaction committed still rolls back COMPLETELY — the bridge wraps
  the whole script, so a late exception undoes even committed work.** Verified live 2026-08-09: a wall
  trim committed its Transaction, then a stray `Document.Regenerate()` AFTER the commit threw
  ("Modification of the document is forbidden" — Regenerate needs an open transaction), and the read-back
  showed the wall untouched. Two rules fall out: (1) never call `Regenerate()` outside a transaction —
  `Commit()` already regenerates, so a post-commit Regenerate is both illegal and pointless; (2) this
  rollback is protective, not a bug — a bridge script either fully lands or fully doesn't, so after ANY
  script error re-read the model instead of assuming the pre-error lines stuck.

- **When two projects are open, `Document` is whatever is in FRONT — and it can change between two of
  your own tool calls.** Proven the hard way 2026-08-19: with `4355-BHVD-3D-60P00-BL006A` and
  `4355-BHVD-3D-60A10-BL002A` both open, a schedule was created into BL002A while the session believed it
  was working in BL006A. Nothing errored. The bridge reported success, a real Id, and a plausible row
  count — the giveaway was only that the count (8 air terminals) disagreed with a number read minutes
  earlier from the same "model" (45). **A wrong-document write is silent; only a number that contradicts
  an earlier reading exposes it.**
  - **For anything that WRITES, pin the document by title instead of using `Document`:**
    `Document doc = null; foreach (Document d in Application.Documents) if (!d.IsLinked && d.Title == "<title>") doc = d;`
    then build the `Transaction`, the `FilteredElementCollector` and every `GetCategory`/`GetElement` call
    against `doc`. Transactions on a non-active open document work fine — this costs nothing and removes
    the whole failure mode.
  - **State the document title in the result line of any script that changes something**, so the wrong
    target is visible in the output rather than inferred three calls later.
  - Two open projects from the same template will BOTH contain the same view and schedule names, so
    "I found MM_V03, therefore I'm in the right model" is not evidence of anything.

- **`list_revit_instances` reports a STALE window title — do not use it to identify the open model.**
  Proven live 2026-08-20: Ajmal closed BL006A and opened `4355-BHVD-3D-60P00-BL003A`, and the tool still
  reported `windowTitle: "... 4355-BHVD-3D-60P00-BL006A.rvt ..."`. Acting on that would have written
  colour overrides into the wrong project, and — exactly as the entry above warns — nothing would have
  errored. The title is captured per Revit *process*, not refreshed per document swap, so it names
  whatever was in front when the instance was first seen.
  - **Ask the document itself**, in one cheap call, before any read that matters and every write:
    `Document.Title` plus a walk of `Application.Documents`. One line of C#, and it is the only
    authoritative answer.
  - The same call exposes **linked models**, which matter for a different reason: `IsLinked == true`
    documents are not yours to edit, and view graphic overrides never reach them. On BL003A six links
    were open alongside the host — so "everything else went grey except these" was only ever true of
    host elements, and saying so is part of an honest result line.

## Writing bridge C# — API surface traps that cost a round trip

Each of these is a real compile error hit while writing live bridge scripts. They fail at compile, not
at runtime, so they cost a full round trip through Revit for nothing.

- **`IsDeterminedByFormula` does not exist on `Parameter`** — it is a member of `FamilyParameter`, which
  only exists inside a family document. Checking "is this value locked by a formula?" from the project
  therefore cannot be done on the instance; open the family (`Document.EditFamily`) and read
  `FamilyManager.Parameters` instead. Hit on Revit 2020, 2026-08-20.
- **`MechanicalSystem` needs its full namespace** — `Autodesk.Revit.DB.Mechanical.MechanicalSystem`. The
  bridge wrapper imports `Autodesk.Revit.DB` but not the `Mechanical` / `Plumbing` / `Electrical`
  sub-namespaces, so the bare name is `CS0246: type or namespace not found`. Same applies to
  `Autodesk.Revit.DB.Mechanical.Space` and `Duct`. `MEPSystem` itself is in the base namespace and needs
  no prefix. Hit on Revit 2020, 2026-08-20.

## Revit version + unit conversion
- **Check which Revit version is actually open before assuming a unit API** — `UnitTypeId.Millimeters`
  only exists from 2021 onward; on 2020 or earlier use
  `UnitUtils.ConvertToInternalUnits(mm, DisplayUnitType.DUT_MILLIMETERS)` instead. Don't assume the
  version from a past session; a different project, or a future session on the same project, may be
  running a different Revit year.
- The user always speaks in **mm**, Revit's internal API is always **feet** — convert both ways explicitly,
  don't leave raw feet in a reply.
- `Autodesk.Revit.DB.Structure.StructuralType` must be **fully qualified** when calling
  `Document.Create.NewFamilyInstance(...)` — a bare `StructuralType` fails to compile in this script
  context ("inaccessible due to its protection level").
- Same fully-qualify rule hits MEP types in this script context: `Autodesk.Revit.DB.Mechanical.Duct` and
  `Autodesk.Revit.DB.Mechanical.MechanicalSystemType` — a bare `Duct`/`MechanicalSystemType` fails with
  "type or namespace not found". `Connector.DuctSystemType`'s enum type (`Autodesk.Revit.DB.Mechanical.
  DuctSystemType`) goes further — even fully qualified it's "inaccessible due to its protection level" (the
  enum itself isn't public in this script context, only the property that returns it is) — compare via
  `connector.DuctSystemType.ToString() == "SupplyAir"` instead of referencing the enum type directly.
- `new ElementId(someLong)` fails to compile with a confusing error — "cannot convert from 'long' to
  'Autodesk.Revit.DB.BuiltInParameter'" — because this Revit version's `ElementId` only has an `(int)` and
  a legacy `(BuiltInParameter)` constructor, no `(long)` overload; `long` doesn't implicitly narrow to
  `int` so the compiler falls through to the wrong overload. Cast explicitly: `new ElementId((int)someLong)`.
### Viewports and view titles on a sheet — see views.md

Those Revit 2020 viewport limits were split out on 2026-08-13, the second time this file passed the
~300-line rule: [`views.md`](views.md) — why view-title length and position cannot be set at all before
Revit 2022, how to CALCULATE the centering offset so the manual drag is a known number, the
mutate-then-`RollBack()` trick for measuring a hypothetical state without touching the model (and the
stale-read caveat that makes it lie), and the type-level blast-radius check before editing any Viewport
Type — one type was shared by 77 viewports across a whole document.

### Reading an element's level, workset or design option — see element-identity.md

Those four traps were split out on 2026-08-06 when this file passed the ~300-line rule:
[`element-identity.md`](element-identity.md) — why a Duct's level is not where you expect, why
`ELEM_PARTITION_PARAM` reads as null, and the two things Design Options will not let the API do.

### Category ID quick reference (for reading raw output only — never hardcode these in scripts)
Verified live (2026-07-14) against the real installed RevitAPI.dll — all 27 matched exactly, none wrong:

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

**Why this is a reference, not something scripts should use directly**: every fragment in `scripts/`
writes the symbolic name (`BuiltInCategory.OST_Walls`), never the raw negative number — a typo in the enum
name is a compile error, a typo in a raw int (e.g. transposing `-2001320` and `-2001360`) would silently
point at the wrong category with no warning. This table is only useful for recognizing a bare category Id
when it shows up in raw output (a warning, an export, a debug dump) — converting int→enum in a script is
always a live one-line cast (`(BuiltInCategory)someInt`) or `Category.GetCategory(doc, id).Name`, which is
authoritative for every category, not just these 27 common ones.
