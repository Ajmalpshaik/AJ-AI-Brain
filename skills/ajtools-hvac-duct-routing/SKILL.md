---
name: ajtools-hvac-duct-routing
description: Place FCU (Fan Coil Unit) Mechanical Equipment in a room, draw its main supply duct, and connect each air terminal to that main duct with a branch (riser + elbow + takeoff tee). Use whenever the user says things like "place the FCU", "draw the main duct", "connect the air terminal to the duct", "draw branch duct", "connect this duct to airterminal", or similar broken-English/dictated phrasings ("drw main duct", "conect duct to airterminal") for any part of this FCU-to-terminal ductwork chain — even if they only ask for one piece of it (e.g. just "place the FCU" today, "now draw the main duct" days later). This skill assumes each room's air terminals are already placed with correct Flow values (that's ajtools-hvac-terminal-layout's job) and each Space already has correct Supply/Return Airflow (ajtools-hvac-space-airflow's job) — don't recalculate either here, just read them. Do NOT use this for terminal count/placement itself (no FCU or duct involved) — that's ajtools-hvac-terminal-layout. Do NOT use this for Space airflow calculation — that's ajtools-hvac-space-airflow. Do NOT use this for tracing existing/unknown MEP connectivity — that's ajtools-mep-trace, which is for figuring out what's already connected, not building new connections. Do NOT use this to just VERIFY whether ductwork already built is still fully connected end-to-end (no new placing/drawing/connecting) — that's ajtools-mep-connectivity-verify; hand off to it after finishing a build to double check, or when the user asks "is everything connected" without asking for new work.
---

# AJ Tools — HVAC Duct Routing (FCU → Main Duct → Branch to Terminals)

This is the skill for the physical ductwork chain that comes *after* terminals and FCUs already have their
airflow numbers right: placing the FCU itself, drawing its main supply duct, and connecting each terminal
to that main duct with its own branch. Real use has included larger rooms needing more than one FCU/zone —
the pattern below handles that too, not just the single-FCU-per-room case.

It's a companion to, but separate from, [`ajtools-hvac-terminal-layout`](../ajtools-hvac-terminal-layout/SKILL.md)
(terminal count + placement) and [`ajtools-hvac-space-airflow`](../ajtools-hvac-space-airflow/SKILL.md)
(Space airflow numbers) — this skill assumes both of those are already correct and just reads them. The
user often asks for one piece of this chain at a time across different turns ("place the FCU" today, "now
draw the main duct" later, "now connect the branch ducts" after that) — treat each as its own step, not a
signal to do the whole chain at once unless they say so.

**Every numeric rule is a per-request input — never assume last session's numbers still apply.** FCU height
above the ceiling, the door-side inset distance, wall clearance for the main duct, max flow per terminal —
all of these are per-request, same convention as the other HVAC skills. Restate what you're using before
calculating.

**No split near the FCU — the main duct is one single piece from the FCU connector onward.** A past request
asked for a 200mm split there (for a future flex-duct connection) but later removed that requirement ("no
need to slice that 200mm from the FCU") once the takeoff-based slicing work below turned out unreliable —
don't reintroduce the FCU-side split unless the user asks for it again specifically. When they do, start
from [`recipes/split-duct-near-equipment.cs`](../../scripts/recipes/split-duct-near-equipment.cs)
(live-verified 2026-07-17) rather than writing it fresh — it's a fixed-offset cut from one equipment
connector, same `BreakCurve` + explicit-reconnect pattern as the trunk-slicing recipe below, just simpler
(no grouping, one cut).

## How to work: plan, split, then execute

Don't jump from "do the ductwork" to one opaque script. This is a genuine multi-stage chain — confirm
you're on the right stage before running anything, since the user may only be asking for one piece:

1. **FCU placement** — one Mechanical Equipment instance per room (family confirmed live, e.g.
   `STI_ME_FCU_Fan Coil Unit`), at the height the user specifies above the ceiling (read the real `Ceiling`
   element, don't hardcode), then repositioned toward the room's door if asked — shift **only** along the
   wall-perpendicular axis by the given inset, keep the along-wall coordinate wherever it already was
   (room center, typically) — don't snap to the door's exact tangential position. Rotate the FCU's real
   Supply Air duct connector (filter out any same-system-type decoy connector like "Fresh Air" by checking
   `Description`) to face the centroid of that room's terminals, if/when asked to.
2. **Main duct** — sized to the FCU's supply connector (never the duct type's default size). **Check
   whether the FCU sits at one end of the terminal grid or in the middle of it (terminals on both sides
   along the long axis) before picking an approach** — these need different shapes:
   - *FCU at one end*: a single straight duct from the FCU's connector running along the room's long axis
     toward the far wall works directly as both trunk and FCU connection.
   - *FCU in the middle (terminals on both sides)*: draw a **continuous trunk spanning the terminal grid's
     full extent** (near one wall to near the other, independent of the FCU's own position), and connect
     the FCU to it as its own branch — a stub from the FCU's connector in its own facing direction over to
     the trunk's line, then a takeoff tee. Don't make the FCU the trunk's endpoint in this case: a single
     one-directional duct out of the FCU leaves every terminal on the other side unreachable, and is also
     physically wrong if the connector's facing direction doesn't match the duct's travel direction (no
     elbow inserted, so `ConnectTo` "succeeds" on nonsense geometry).
   
   Either way: draw it as **one single duct piece**, with the FCU-end connected explicitly — no split near
   the FCU (see the note above). **If the room has more than one FCU**, split its terminals into zones by
   nearest-FCU first, and bound each FCU's own main duct/trunk to just past its own zone's farthest terminal
   — never to the room's actual far wall, which would cross into the other FCU's zone.

   **Cap every open trunk end as part of this same step — NOT a deferred, ask-first extra.** the user made
   this standing: whenever a main duct/trunk is drawn, its open end(s) get capped immediately, same turn,
   without needing to be asked separately every time. Capping is NOT just place-a-cap-and-`ConnectTo` —
   `IsConnected == true` does not mean the cap is actually the right size, in the right position, or facing
   the right way; verify all three. Get the cap family/type from the duct type's own Routing Preferences
   (Caps group), duplicate/reuse a precisely-sized type named for its dimensions, set the cap's own
   connector Width/Height directly (not just an instance parameter), then explicitly move the cap so its
   connector coincides with the duct's open connector and rotate it so its connector faces the *opposite*
   direction — re-fetching the connector reference after every move/rotate/resize since it goes stale. Full
   recipe (adapted from the user's own working pyRevit tool) is in `live-model/hvac-ducts.md` — read it, don't
   re-derive a simpler version that skips the rotation/position steps.
3. **Branch ducts** — for each terminal (filter explicitly by system type, e.g. family name contains
   "Supply" — checkerboard-laid-out terminals mean "nearest terminal" is NOT reliably the same system type):
   a vertical riser from the terminal's connector up to the main duct's height, sized to the terminal's
   connector; then a horizontal run over to the nearest point on the main duct (skip this segment entirely,
   tapping the riser straight into the main duct, if the terminal already lines up almost exactly under the
   main duct's line); a real elbow fitting at the vertical-to-horizontal turn; and a takeoff tee connecting
   into the main duct. (This taps into the trunk's interior via a takeoff, so it doesn't disturb the caps
   already placed on the trunk's end connectors in step 2.)
4. **Report back per room/FCU** — what got placed/drawn/connected, and a plain count so the user can sanity
   check (e.g. "7 FCUs placed", "8 main ducts (one room split into 2 zones), all ends capped",
   "35/35 terminals connected").

**Slicing the trunk into progressively smaller segments for duct sizing (after each takeoff, since flow
decreases past every branch) is NOT part of the standard flow above — treat it as a separate, higher-risk
request, only when the user explicitly asks for it.** This was attempted 2026-07-09 and repeatedly caused real
damage (a takeoff fitting silently deleted, its whole branch orphaned) before a working technique was found
— see `live-model/hvac-ducts.md` for the corrected recipe (offset the cut past the takeoff's own body, slice
directly at that offset point, never slice-then-relocate) — but the user ultimately found the results still
weren't coming out right and asked to hold off. Live-verified working end-to-end 2026-07-17 (3 cuts, 8/8
terminals still traced to the FCU afterward) — start from
[`recipes/slice-trunk-for-sizing.cs`](../../scripts/recipes/slice-trunk-for-sizing.cs) rather than writing
fresh; it also handles the checkerboard case (two takeoffs at ~the same position from opposite sides need
grouping into one cut, not two). Still high risk: read the knowledge section fully, test on one room, verify
by tracing each terminal's full connector chain with
[`recipes/verify-duct-connectivity.cs`](../../scripts/recipes/verify-duct-connectivity.cs) (not just
`IsConnected` on the terminal itself, which only checks the local link) before rolling out further, and
check with the user after the first room.

## Before running anything

1. **Ping first**: `mcp__aj-tools-aj-ai__ping`. If Revit isn't connected, say so plainly.
2. **Check [`glossary.md`](../../knowledge/glossary.md)** for any ambiguous term in the request.
3. **Check [`live-model/hvac-ducts.md`](../../knowledge/live-model/hvac-ducts.md)** for the exact API patterns before
   writing new C# — connector identification (the Fresh Air decoy connector, `DuctSystemType` vs
   `MEPSystemClassification`), `Duct.Create`'s XYZ-only overload in this Revit version, `BreakCurve` NOT
   auto-connecting the split (has to be done explicitly), the elbow/takeoff bug (`NewElbowFitting`/
   `NewTakeoffFitting` are dedicated calls — a bare `ConnectTo` makes a logical join but inserts no physical
   fitting), the multi-FCU zone-splitting rule, and the `IsPointInRoom`-fails-on-Z-mismatch gotcha (matching
   an FCU/duct sitting above a room's normal height range back to its room needs a test point at the room's
   own Z, not the element's real Z).
4. **Start from the matching saved script rather than writing fresh** — one per stage of this chain:
   [`place-fcu.cs`](../../scripts/recipes/place-fcu.cs),
   [`draw-main-duct-with-cap.cs`](../../scripts/recipes/draw-main-duct-with-cap.cs),
   [`connect-terminal-branch.cs`](../../scripts/recipes/connect-terminal-branch.cs). Update each one's INPUTS
   block (room/FCU ids, clearances, heights) before running — none of the pre-filled values are defaults.

## While running

- Show the plan (counts, which stage, per-room breakdown) and wait for a clear go-ahead before creating or
  deleting elements at any real scale — this ductwork is exactly the kind of work where a wrong batch is
  expensive to redo. A vague "let me check" is not sufficient; get an explicit yes.
- **When something looks wrong after a batch (e.g. "the elbow's not coming"), verify the model's actual
  current state before touching anything else** — the user frequently undoes work himself in Revit between
  turns, live, without a new chat message. A prior turn's "success" report can be stale; re-query
  `IsConnected` counts, element counts, etc. before assuming what you built is still there.
- If the user says the last batch was a **mistake**, or to **undo** it, or refers to the **previous** one: use
  Revit's own native Undo via the bridge, not a hand-written delete script.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md). A compact per-room table (FCU, terminals served,
duct type) is usually the right format for the main-duct/branch stages; a plain count line is enough for a
single FCU-placement confirmation.

## After finishing

If a new technical gotcha comes up, add it to `live-model/hvac-ducts.md`. If a new ambiguous term comes up, add
it to `glossary.md`. If this run improved one of the three scripts above (a bug, a better recipe), update
that script in place — this is the highest-risk chain in the whole toolkit, so a fix here is worth
capturing carefully, not just noted in chat. Same rule as every other AJ Tools skill: one fact, one file,
no duplication.
