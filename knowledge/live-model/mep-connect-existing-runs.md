# Live Model — connecting two runs that already exist

Back to [`README.md`](README.md).

Different job from [`hvac-ducts.md`](hvac-ducts.md), which is about **building new** ductwork out from an
FCU to the terminals. This is the other one: **two runs are already drawn, they don't meet, close the
gap.** Offset by a bit, at an angle to each other, or dead in line with a hole between them.

Harvested 2026-08-22 from the add-in's Connect MEP Elements tool (v3), which has been through two
rewrites on real jobs. What follows is the engineering, not the code — the tool is ~2,200 lines and
porting it wholesale would be a mistake. These are the decisions worth keeping.

## The rule that shaped the whole thing: STRETCH, DON'T CREATE

The user picked two real ducts. The answer is almost always to **lengthen the things they picked**, not
to bolt a third element between them. A new piece is justified in exactly two places:

1. **The bridging run across an offset or a skew** — that piece *is* the connection; there is no pair of
   ends to stretch together.
2. **A run up to an end that physically cannot be lengthened** — flex duct, flex pipe, a piece of
   equipment, or a curved run. Those can never be trimmed longer, so there is nothing to stretch.

Anything else gets stretched. An earlier version quietly manufactured a new piece whenever stretching was
awkward, and Ajmal had it removed: the model ended up full of short segments nobody drew.

## Keep two questions apart — this is the subtle one

| Question | Name it | Answer means |
|---|---|---|
| **CAN** this end be stretched? | `canTrim` | Physical fact. False only for flex, equipment, or a curved run. |
| **MAY** it be stretched? | `mayMove` | The user's "which element is allowed to move" choice. |

They look interchangeable and are not. If an end **could** stretch but the user locked it, the right
answer is **refuse and say so** — not quietly bolt a new piece onto it. Collapsing the two into one flag
is precisely how the old behaviour crept back in. `mayMove` is always `canTrim && userAllowsIt`, never
`userAllowsIt` alone.

## Every attempt inside its own sub-transaction

The tool tries a list of bend angles, best first. Each attempt runs in its **own `SubTransaction`**:
commit on success, roll back on failure. So a failed angle leaves **untouched geometry** for the next one
to start from, rather than half-built fittings and stretched ends.

This generalises well past this tool. **Any "try several ways, keep the first that works" operation on a
live model wants a sub-transaction per attempt.** Without it, attempt 2 starts from the wreckage of
attempt 1 and the failure mode is silent — it produces a connection that looks fine and is built on
geometry the earlier attempt already moved. Related but different: [`undo.md`](undo.md) covers reversing
something the user asked to reverse; this is about not leaving debris behind in the first place.

## Angle fallback

Try the chosen angle, then a fallback list — **45°, 30°, 60°, 90°** in that order — de-duplicated so
nothing is attempted twice. Angles are capped at **5°–90°**.

Report the angle honestly, and this bit took a fix: only an **offset crank** is actually built to the
requested angle. A straight in-line bridge, a corner, and a skew all take their angle from the *shape of
the two runs*, so telling the user "built at 90° instead of your 45°" is nonsense there — it fired on
every clean in-line connection until it was made conditional.

## The crank geometry, and the sign that was wrong

Two ends facing each other but offset sideways — the ordinary crank. With `axisOffset` the distance
along the run direction and `perpendicularLength` the sideways offset:

```
requiredAxialGap = perpendicularLength / |tan(angle)|      (0 when the angle is 90°)
totalShift       = axisOffset − requiredAxialGap           ← MINUS. not plus.
```

**Adding the gap instead lands on the supplement, 180° − angle, which folds the bridge back over the run
it just left.** An earlier version computed both and picked whichever needed less travel — which silently
chose the fold-back for **every pair whose open ends had already passed each other**, i.e. the ordinary
overlapping crank, the commonest case there is. Because angles are capped at 90°, the other option is
never the right one; there is no case to choose between.

Worth stating plainly because the failure was invisible in code review and obvious on screen.

Guards around it:
- Sideways offset under ~0.1 mm → the ends are **dead in line**; no bend, just close the gap.
- Resulting bridge shorter than ~10 mm → refuse with a real message ("only X mm apart sideways — move one
  run so they are either dead in line or at least Y mm apart"), don't build a sliver.
- Minimum run length ~50 mm.

## Picking which pair of open ends to use

An element can have several open connectors. Try **every compatible pair** (matching domain), plan each,
and score:

```
score = |firstShift| + |secondShift| + distance(firstPlanPoint, secondPlanPoint)
```

Lowest wins — **the smallest total intervention**, counting both how far the ends travel and how long the
new bridging piece ends up. Not "nearest pair of connectors", which picks a pair that then needs a long
awkward crank.

## Cases that are refused rather than bodged

- **Both open ends point the same way** → a U-shaped route. Not supported; say so.
- **Ends not parallel** → only attempted if the user has explicitly allowed it, because the geometry, not
  the chosen angle, then decides the bend. Two sub-cases: if the run axes cross, both ends reach the
  crossing point and share one elbow; if they pass by each other, bridge them on their common
  perpendicular.
- **Incompatible connector domains** → skip the pair entirely rather than force it.

## Things that must be carried across, not left behind

- **Insulation and lining** — copy from the source run onto anything newly created, or the new piece is
  the one bare segment in an insulated run.
- **Size** — take it from the connector being matched, never from the duct/pipe type's default. Same trap
  as drawing a fresh duct, already recorded in [`hvac-ducts.md`](hvac-ducts.md).
- **System type and level** — resolve from the source element; a new segment with no system type does not
  join the system even when it is geometrically connected.
- **Rectangular profiles** may need rotating to match before the two ends will accept each other.

## Clash checking after the fact

The tool checks the built route against: **Walls, Floors, Ceilings, Roofs, Structural Framing, Structural
Columns, Ducts, Pipes, Cable Tray, Conduit.** It reports rather than blocks — a warning the user can
judge. That category list is a reasonable default for any "did I just run through something" check.

## What this Brain has already

The related fragments — none of which do this job, so there is no overlap to resolve:

- [`../../scripts/actions/reporting/action-report-connectors.cs`](../../scripts/actions/reporting/action-report-connectors.cs) — what connectors exist, domain, size, origin
- [`../../scripts/filters/by-relationship/filter-by-connection-status.cs`](../../scripts/filters/by-relationship/filter-by-connection-status.cs) — which elements have open ends
- [`../../scripts/actions/move-copy-rotate/action-trim-extend-elements.cs`](../../scripts/actions/move-copy-rotate/action-trim-extend-elements.cs) — trim/extend two linear elements to a corner (geometry only, no fittings)
- [`../../scripts/actions/move-copy-rotate/action-fillet-elements.cs`](../../scripts/actions/move-copy-rotate/action-fillet-elements.cs) — real elbow fitting between two MEP curves

**A fragment that does the full plan-and-build has not been written.** Doing it properly means the
sub-transaction attempt loop, the pair scoring and the crank math above, and it should be built the day a
real job needs it — not speculatively. This note is what to build it from.
