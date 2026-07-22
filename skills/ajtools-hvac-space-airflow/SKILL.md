---
name: ajtools-hvac-space-airflow
description: Create/find the MEP Space for each room and (re)calculate its Specified Supply/Return Airflow parameters from a thumb-rule the user gives per request (e.g. "14 sqm = 1 ton = 400 cfm", "return 10% lower") — AND, if air terminals already exist in that room, refresh each one's own Flow parameter to match the new total. Use whenever the user says things like "update the space for the HVAC", "update the space as per the HVAC and air terminal", "update the space airflow", "set the supply/return airflow", "calculate CFM/L/s per room", or gives/changes an AC sizing rule and wants it written onto the Spaces (and any already-placed terminals) themselves — not just reported in chat. Always re-applies to every Space in scope, existing or newly created — never skip an existing Space assuming its old numbers still hold, and never leave an existing terminal's Flow stale after its Space total changed. Do NOT use this to place brand-new terminals or change terminal count/position — that's ajtools-hvac-terminal-layout. Do NOT use for a plain room/area query with no airflow component — that's ajtools-live-model. Do NOT use this for placing the FCU or drawing/connecting any ductwork — that's ajtools-hvac-duct-routing.
---

# AJ Tools — HVAC Space Airflow

This is the skill for the request "update the space[s] for the HVAC" — or the user's fuller phrasing, "update
the space as per the HVAC and air terminal" — meaning: recalculate the Space's Supply/Return Airflow from
the current room area and rule, **and** if that room already has air terminals placed, push the new numbers
down to them too so nothing is left stale. It's deliberately kept **separate** from
`ajtools-hvac-terminal-layout` (a distinct request shape) — sometimes the user only wants
the Space (and any existing terminals) refreshed, with no new terminal placement or re-layout involved.

**Every numeric rule is an input the user gives per request — never assume last session's numbers still
apply.** CFM-per-ton, m²-per-ton, the return-airflow fraction (e.g. 10% lower than supply) — all
per-request. Restate what you're using before calculating.

## How to work: plan, split, then execute

1. **Room areas** — collect `OST_Rooms` on the level in question, read `Area` in m².
2. **Matching Space per room** — if an MEP Space doesn't already exist at a room's location, create one
   (`Document.Create.NewSpace`), carrying over Name/Number so it stays identifiable. If one already exists
   there, use it as-is — don't create a duplicate. The standalone
   [`creators/create-space.cs`](../../scripts/creators/create-space.cs) fragment wraps this same call
   (with the same unbounded/zero-area check) for a plain "add a Space here" request that isn't part of this
   airflow-cascade flow.
3. **Airflow calc onto the Space's real parameters — always, existing or newly created, no exceptions.**
   Turn the user's current thumb-rule into supply and return `Specified Supply/Return Airflow` values on
   **every** Space in scope. Never skip an existing Space assuming its airflow is already correct — the
   room may have been resized since, or the user may be giving a different rate this time (confirmed necessary
   in practice: room sizes changed mid-session once already, and every existing Space's airflow had to be
   recalculated from the new areas). Exact BuiltInParameter names (`ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM` /
   `ROOM_DESIGN_RETURN_AIRFLOW_PARAM`), the `ReturnAirflowType.Specified` mode-switch requirement (return
   airflow is silently ignored without it), and the unit-conversion pattern (`DisplayUnitType` — the exact
   API differs by Revit version, check yours) are all in
   [`live-model/hvac-terminals.md`](../../knowledge/live-model/hvac-terminals.md) — read it, don't re-derive it.
4. **Cascade to any air terminals already in that room — do this every time, not only when asked twice.**
   After a Space's Supply/Return Airflow changes, check whether `OST_DuctTerminal` instances already exist
   in that room (`Autodesk.Revit.DB.Architecture.Room.IsPointInRoom`, fully-qualified — see
   `live-model/hvac-terminals.md`). If any do, recompute each one's share as `newRoomTotal / existingTerminalCount`
   (existing count, unchanged) and write it to that instance's own Flow parameter
   (`BuiltInParameter.RBS_DUCT_FLOW_PARAM` — note there are two same-named "Flow" parameters on these
   families, see `live-model/hvac-terminals.md` for which one actually matters). **Don't change the terminal count
   or move them** — that's a re-layout, and re-layout is `ajtools-hvac-terminal-layout`'s job, only if the user
   separately asks for it. If a room has no terminals yet, there's nothing to cascade — just leave it at
   the Space update.
5. **Report back per room** — area, supply value, return value, and (if applicable) how many existing
   terminals' Flow got refreshed — so the user can sanity-check the rule was applied the way they meant.

## Before running anything

1. **Ping first**: `mcp__aj-tools-aj-ai__ping`. If Revit isn't connected, say so plainly.
2. **Check [`glossary.md`](../../knowledge/glossary.md)** for any ambiguous term in the request.
3. **Check [`live-model/hvac-terminals.md`](../../knowledge/live-model/hvac-terminals.md)** for the exact Space-airflow API
   pattern before writing new C#.
4. **Start from [`scripts/recipes/set-space-airflow.cs`](../../scripts/recipes/set-space-airflow.cs)** rather than
   writing this fresh — update its INPUTS block (level, CFM/ton, sqm/ton, return fraction) with today's
   actual rule before running.

## While running

- If the user says the last update was a **mistake**, or to **undo** it, or refers to the **previous** one:
  use Revit's own native Undo via the bridge, not a hand-written fix script. The exact call is in
  `live-model/hvac-terminals.md`.
- If the user says they **already undid it themselves**, treat that as ground truth about current model state —
  re-query fresh rather than trusting an earlier tool-call result from this same conversation.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md). A compact per-room table (area, supply, return)
is usually the right format here.

## After finishing

If a new technical gotcha comes up, add it to `live-model/hvac-terminals.md`. If a new ambiguous term comes up, add
it to `glossary.md`. If this run improved or corrected the recipe (a bug, a better formula), update
`set-space-airflow.cs` in place — don't fork a v2 file. If `ajtools-hvac-terminal-layout` is run right after this in the same conversation
(e.g. the user wants a room's terminal count/layout changed too, not just refreshed), it can reuse the Space
values just written rather than recalculating them from scratch — but always re-read them fresh from the
model rather than trusting the numbers reported in chat a few turns back.
