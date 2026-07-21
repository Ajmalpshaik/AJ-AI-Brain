---
name: ajtools-hvac-terminal-layout
description: Place brand-new supply/return air terminals in each room on the live Revit model (count + physical layout), and write each new terminal's own Flow parameter to match its share. Use whenever the user asks things like "how many air terminals do I need", "place the supply/return diffusers", or describes a NEW terminal count-and-placement task in their own broken English (e.g. "200 l/s max per terminal", "500 gap from wall", "return also same count as supply"). This skill assumes each room's MEP Space already has correct Supply/Return Airflow numbers on it. Do NOT use this if terminals already exist and the user just wants their Flow refreshed after a Space/area change with no new placement or recount — that's ajtools-hvac-space-airflow (it cascades to existing terminals on its own). Do NOT use this for a plain room/area query with no terminal-placement angle — that's ajtools-live-model. Do NOT use this for tracing existing pipe/duct connectivity between equipment — that's ajtools-mep-trace. Do NOT use this for placing the FCU or drawing/connecting any ductwork (main duct, branch ducts) — that's ajtools-hvac-duct-routing.
---

# AJ Tools — HVAC Air Terminal Layout

This is the skill for turning a room's already-set Space airflow into real physical air terminals on the
live model: terminal count → placement → each terminal's own Flow parameter. It's a companion to, but
deliberately **separate from**, [`ajtools-hvac-space-airflow`](../ajtools-hvac-space-airflow/SKILL.md) —
that split exists because sometimes the user wants only the Space numbers updated with no terminal
placement at all, and sometimes (this skill) they want terminals placed against numbers already on the
Space.

**Every numeric rule is an input the user gives per request — never assume last session's numbers still
apply.** The max L/s per terminal, the minimum terminal count per room, the wall clearance — these are all
real examples of values that have shown up in past requests (e.g. 200 L/s, a minimum of 2, a 500mm
clearance), and **none of them are defaults to fall back on**. The wall clearance in particular varies
request to request (could be 1000mm, could be something else) — always ask for or confirm it fresh, the
same as every other number in this list. Restate what you're using before calculating, so a stale
assumption doesn't silently carry over from a different job.

## How to work: plan, split, then execute

Don't jump straight from "place the terminals" to one opaque script. Split it like this, confirming each
stage's numbers with the user before moving to placement (the numbers are cheap to get wrong and expensive to
re-place):

1. **Confirm the Space airflow is current** — read each room's Space `Specified Supply/Return Airflow`
   fresh from the model (don't trust a value reported in chat earlier in the conversation). If it looks
   stale, missing, or the user mentions a rule change, run [`ajtools-hvac-space-airflow`](../ajtools-hvac-space-airflow/SKILL.md)
   first rather than guessing or recalculating it inline here — that's its job, not this skill's.
2. **Terminal count per room** — from the user's max-L/s-per-terminal and minimum-count rules. **Important
   correction confirmed in practice**: supply and return terminal counts must be equal per room, even though
   return airflow is lower — don't let a naive `ceil(airflow/maxPerTerminal)` give supply and return
   different counts. Use supply's count for both; each terminal then simply carries `roomTotal / count`,
   which is why supply and return terminals end up at different individual L/s despite an equal count.
3. **Placement** — grid across each room, wall clearance shrinking the usable rectangle (**ask what the
   clearance is for this request — don't default to 500mm just because that's what a past job used**),
   ceiling height read from the real `Ceiling` element (not hardcoded), supply/return spatially
   **alternated as a TRUE checkerboard** (a zoned layout was already rejected once; a "same-row-only"
   alternation was also caught as wrong and fixed — every terminal's neighbor, in every direction, must be
   the other system type). **Assign type with `(row + col) % 2`, not a continuous running index across
   rows** — the continuous-index approach looked fine (correct 50/50 split) but silently produced *identical*
   rows whenever the per-row count was even, so a supply terminal ended up with another supply directly
   across from it in the next row. `(row + col) % 2` doesn't have that flaw. **Default row count to the
   near-square formula, not a flat 2** — pick the row count that makes the grid closest to square for that
   room's proportions (fewer big gaps on the short axis than a fixed 2-row grid gives), rather than always
   defaulting to 2 rows. The row-count formula is in `live-model/hvac-terminals.md` — read it, don't
   re-derive it. The user can still ask for a specific row count for a specific room if they want to
   override the formula for that room only.
   **Verify the actual pattern after placing** — query each placed terminal's position and type and check
   adjacency directly (group by row/column position), don't rely on a "nearest terminal by raw distance"
   check: a same-row neighbor can be geometrically farther away than a wrong-type neighbor in an adjacent
   row, so distance-based checks can miss a real violation entirely (this is exactly how the bug above went
   unnoticed at first).
4. **Flow parameter per terminal** — after placing, write each instance's own Flow so it reflects its
   individual share, not the room total. Which of the two same-named "Flow" parameters Revit exposes on
   these families is the one that actually matters — documented in `live-model/hvac-terminals.md`, don't guess.

## Before running anything

1. **Ping first**: `mcp__aj-tools-aj-ai__ping`. If Revit isn't connected, say so plainly — don't
   guess at numbers with no model to check them against.
2. **Check [`glossary.md`](../../knowledge/glossary.md)** for any ambiguous term in the request.
3. **Check [`live-model/hvac-terminals.md`](../../knowledge/live-model/hvac-terminals.md)** for the exact API patterns before
   writing new C# — unit conversions, the Space airflow parameters, the checkerboard grid trick, the
   duplicate-"Flow"-parameter gotcha, and the fully-qualified `StructuralType` requirement.
4. **Start from [`scripts/recipes/place-terminals-checkerboard.cs`](../../scripts/recipes/place-terminals-checkerboard.cs)**
   rather than writing this fresh — update its INPUTS block (room, family symbols, max L/s, min count,
   wall clearance) with today's actual numbers before running.

## While running

- If the room/space set is large or the placement is hard to undo cheaply, show the plan (counts per room,
  the rule being applied) and wait for a clear go-ahead before creating elements. A vague "let me check" is
  not sufficient confirmation to place things in the user's live model; get an explicit yes.
- If the user says the last placement was a **mistake**, or to **undo** it, or refers to the **previous** one:
  use Revit's own native Undo via the bridge, not a hand-written delete script. The exact call is in
  `live-model/hvac-terminals.md`.
- If the user says they **already undid it themselves**, treat that as ground truth about current model state —
  re-query fresh (an element count is cheap) rather than trusting an earlier tool-call result from this same
  conversation.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md). For this kind of work a compact per-room table
(count, L/s each) is usually the right format — it makes a supply/return count mismatch easy to spot at a
glance.

## After finishing

If a new technical gotcha comes up (a different family's parameter naming, a different placement rule), add
it to `live-model/hvac-terminals.md`. If a new ambiguous term comes up, add it to `glossary.md`. If this run
improved the recipe, update `place-terminals-checkerboard.cs` in place. Same rule as every
other AJ Tools skill: one fact, one file, no duplication.
