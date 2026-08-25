# Live Model — HVAC duct sizing — slicing a trunk into progressively smaller segments

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.
> Drawing and connecting the ductwork itself lives in [`hvac-ducts.md`](hvac-ducts.md).

## Slicing a main trunk into segments for duct sizing (progressively smaller after each takeoff)
Purpose: since each branch takeoff removes some of the trunk's airflow, the segment *after* a takeoff only
needs to carry what's left — so the trunk must be cut into separate `Duct` elements at each takeoff point,
each individually sizeable, rather than staying one uniform-size run (2026-07-09).

**The takeoff connector's `Origin` is the CENTER of the takeoff fitting's own body, not a zero-width
point.** Slicing exactly there cuts through the fitting's physical geometry. the user's correction: offset the
cut downstream (away from the FCU) from the takeoff's center by `(trunk width / 2) + a clearance margin`
(started at 100mm, changed to **50mm** per the user's later instruction — this margin is a per-request number,
confirm fresh each time, same convention as every other HVAC number). Also: **slice after every takeoff
except the LAST one before the end cap** — the final segment runs through the last takeoff in-line, still
one piece, all the way to the cap; don't create one more cut just for that last short stretch.

**CRITICAL BUG, confirmed by real damage: do NOT slice at the takeoff's center and then "move" the joint
afterward by editing `LocationCurve.Curve` on the two pieces.** This was the first approach tried — break
at the exact takeoff point, reconnect the joint, then stretch one piece's curve and shrink the other's to
relocate the boundary to the offset position. It silently **deleted the takeoff fitting and orphaned its
entire branch** (terminal → riser → elbow → horizontal branch, dead-ending with an open connector where the
takeoff used to be) — Revit's `IsConnected` on the terminal's own connector still showed `true` throughout,
because that only reflects the *local* terminal-to-riser link, not whether the branch actually reaches the
trunk; the break only became visible by tracing the full chain connector-by-connector until hitting an open
end. Root cause: a takeoff fitting is hosted based on being at a specific point on whichever duct element
it was created against — stretching a *different* piece's curve to cover that location doesn't transfer the
host relationship; shrinking the *original* piece away from that location leaves the takeoff's host
reference pointing at geometry that's no longer there, and Revit resolves that by dropping the fitting.

**The correct approach**: compute the desired offset break point FIRST (`takeoffCenter + (halfWidth +
margin) * downstreamDirection`), then call `MechanicalUtils.BreakCurve` **directly at that offset point**
on the still-whole (unsliced) trunk — never break at the takeoff's own center and relocate afterward. Since
the offset point is still a valid point on the original curve (as long as it's within bounds), the
takeoff — whose host reference was established back when `NewTakeoffFitting` first ran, well before any of
this slicing — ends up correctly inside whichever of the two new pieces naturally contains its true
location, with no need to move anything. Join the new joint with a real Union fitting — find each piece's
open connector nearest the break point, then `doc.Create.NewUnionFitting(c1, c2)` (NOT a bare `ConnectTo`;
see the union rule in "Why the trunk gets split" below — a fitting-less joint can be silently re-merged).

**Checkerboard layouts put two takeoffs at the exact same longitudinal position — group by position before
cutting, don't cut per-takeoff.** Live-verified (2026-07-17) on a room with 2 terminals per row (one on each
side of the trunk): both rows' takeoffs land at the identical Y (or whichever axis the trunk runs along),
tapping in from opposite lateral directions. Slicing per-takeoff would try to cut the same point twice.
Group takeoff connectors by their position along the trunk's own axis first (round to a small tolerance),
treat each distinct group as ONE cut point, and — same "skip the last one" rule as before — don't cut after
the group closest to the end cap. Re-locate the correct current trunk piece for each successive cut
geometrically (same X/Z line, break point's coordinate strictly between that piece's two endpoints) rather
than trusting a piece's element Id across cuts, since `BreakCurve` reassigns which Id keeps which segment.
Verified end-to-end after 3 cuts on a 4-row/8-terminal room: all 8 terminals still traced to the FCU via
full BFS, nothing orphaned.

**Recovering an orphaned branch if this has already happened**: trace the chain from the terminal
connector-by-connector (riser → elbow → horizontal segment) until hitting an open connector — don't trust
`IsConnected` on the terminal alone. Find the current trunk piece whose curve range geometrically contains
that open connector's location (`Curve.Distance(openConn.Origin)`, pick the nearest/smallest), and call
`NewTakeoffFitting(openConn, thatPiece)` again to re-tap it in.

## Why the trunk gets split: the user's sizing rule (taught by worked example 2026-07-26)
The slicing section above covers HOW to cut safely; this is WHY and what size each piece gets. The user
demonstrated by manually splitting/sizing the 6-terminal FCU system, then had the pattern verified
(6/6 terminals still BFS-traced to the FCU afterward — his splits sat ~400-600mm downstream of each tap,
clear of the fitting body, matching the slicing section's offset rule; no split after the last tap group).
- **Why:** airflow accumulates. The trunk at the equipment carries the SUM of all downstream terminal
  flows and drops after every tap (his example: 6×235 = 1410 L/s at the FCU → 940 → 470 after each
  column). One unsplit duct holds ONE size, so split at every tap group, then size per segment.
- **Sizing rule (verified exact on all his sizes):** square duct, max velocity 5 m/s, side rounded UP to
  the next 25mm — `side_mm = ceil(sqrt((Q_m3s) / 5.0) * 1000 / 25) * 25`. His picks: 1410 L/s → 550×550,
  940 → 450×450, 470 → 325×325, branch 235 → 225×225 (velocities 4.45-4.66 m/s).
- **Read flow, don't compute it:** each segment's `RBS_DUCT_FLOW_PARAM` already holds the accumulated
  flow (Revit sums connected terminals) — size from that, and join different-size segments with
  transitions. Branches follow the same rule per terminal flow (transition at the terminal connector if
  its connection size differs).
- **The preparation stage comes first (his second demo, same day):** split BEFORE sizing. Right after
  splitting, all pieces still hold the original size and are joined by **Union fittings** (what the UI
  Split tool leaves), but each piece already reports its OWN accumulated flow — that per-segment flow
  readout is the entire purpose of splitting first. When the sizes are then applied, Revit swaps those
  unions into transitions automatically. So the check for "is the model ready for sizing": trunk split
  at every tap group (none after the last), unions at the joints, all terminals still tracing to the
  equipment, distinct flow per segment.
- **SIZING IS THE USER'S OWN STEP — stop after split+verify and hand the model over** (his decision,
  2026-07-26). Don't script it, and don't drive the UI dialog for it either unless he explicitly asks.
  Deliver the model ready-for-sizing (built, split at every tap group with unions in place, every
  terminal tracing to its equipment, 0 open ends) and say it's ready; he runs Duct/Pipe Sizing himself.
  The rest of this bullet is background for the case where he does ask for it.
  **It must go through the UI dialog — it cannot be scripted** (tested both paths live 2026-07-26). There is no API and **no postable command ID**:
  the journal records the ribbon button as `Jrn.PushButton "ToolBar , ... Dialog_BuildingSystems_
  RbsDuctSizingBar" , "Sizing..., Control_BuildingSystems_RbsBtnSizing"` — a toolbar push-button, not a
  `Jrn.Command`, so `LookupCommandId`/`PostCommand` have nothing to post (Autodesk's own forum answer says
  the same: no API for it). Scripted sizing (setting `RBS_CURVE_WIDTH_PARAM`/`RBS_CURVE_HEIGHT_PARAM` per
  duct from `RBS_DUCT_FLOW_PARAM`) computes the RIGHT numbers — identical to the dialog's — but wrecks the
  fittings: Revit re-fittings on regenerate, swapping each trunk Union into two back-to-back Transitions
  with their facing connectors left OPEN, silently fragmenting the system (tested twice: BFS fell to 2/6,
  then a second attempt broke a system down to 20 of 37 elements even with an automatic joint-repair
  sweep; both were reverted with native Undo). The dialog does the same job with zero warnings and zero
  open ends.
  **The fast workflow (proven, and it is fast — the slow part was doing it per system):** script does
  everything heavy — build, split, verify — then select EVERY system's elements at once with
  `UIDocument.Selection.SetElementIds` and run the dialog ONCE for the whole model: Duct/Pipe Sizing →
  Velocity → value → OK. One click sized 111 elements across 3 systems in ~5s, all identical to the
  first system the dialog had sized alone (550/450/325 trunk, 225 branches, 0 warnings, 0 open ends,
  6/6 terminals each). The dialog also remembers the last velocity, so repeat runs are just OK.
- **Script-side splits MUST use `NewUnionFitting`, NOT plain `ConnectTo` — a direct duct-to-duct joint
  does not reliably survive.** Live-proven the hard way (2026-07-26, 4-FCU build): after
  `MechanicalUtils.BreakCurve`, rejoining the two open end connectors with `ConnectTo` creates a
  fitting-less direct joint, and Revit **silently re-merged one such pair back into a single duct**
  during later edits (the split vanished; the merged-away piece's Id returned null). The union fitting
  is what physically preserves the split. Correct sequence per cut: `BreakCurve` → find both open end
  connectors at the cut point → `c1.DisconnectFrom(c2)` if touching → `doc.Create.NewUnionFitting(c1,
  c2)`. (The older ConnectTo advice in the splitting sections above predates this discovery — it held in
  the one-off split case, but for sizing-prep splits that must persist, always place the union.)

## Where the reducer goes after a takeoff — Ajmal's rule, 2026-08-25

Sizing decides the reducer's SIZE. It does not decide where the reducer SITS, and Revit drops it wherever
the size change happened to fall. Measured on a 14-room floor straight after sizing: the reducers sat
**268 to 1796 mm** downstream of their takeoff, averaging 587. No two rooms matched.

His rule, in his own words: *"if you take one reducer from the FCU, if you come you find the reducer,
before the reducer there is chance we have one duct connecting branch takeoff. So from that branch
takeoff, 200 mm there reducer need. No need extra length."*

So: **200 mm of straight duct after the branch takeoff, then the reducer.** The datum is **takeoff
CENTRELINE to the reducer's UPSTREAM FACE**, which he chose over edge-to-face and centre-to-centre
because it is the one a fitter sets out from. `recipes/set-reducer-offset-from-takeoff.cs` does it.

**Move the FITTING, never the ducts.** `ElementTransformUtils.MoveElement` on the transition is what
dragging does in the UI: Revit shortens the duct on one side and lengthens the other and keeps both
joined. Proven on a real move — an 826 mm upstream duct became 732 and the 1252 mm downstream duct became
1347, both still at zero open connectors. Rewriting the ducts' `LocationCurve` to achieve the same thing
is how the joints get broken.

**Two reducers must be left alone, and both look like candidates.**
- **The one on the equipment.** Every FCU carries a transition on its supply connector (850x195 -> 400x400
  here) with no duct and no takeoff upstream of it at all. Skip anything whose connector joins straight to
  Mechanical Equipment. Without that guard the nearest-takeoff search matched one on a *branch* 2225 mm
  away and would have dragged the transition off the unit — 15 of them on that floor.
- **The one at the diffuser neck.** 200x200 -> 225x225 at each drop, 61 of them. Not on a trunk, nothing
  to measure from. Silence about these is the correct outcome, not a miss.

**A takeoff does not sit on the trunk centreline.** Its insertion point is offset to the side — 150 to 250
mm was typical here. Measure the distance ALONG the reducer's own axis (dot product) and ignore the
sideways component, or a centreline-tight filter finds no takeoffs at all and reports "nothing to do".

