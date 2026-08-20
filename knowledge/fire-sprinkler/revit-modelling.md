# Doing it in Revit — what to read, what to place, what lies to you

> Chunk of [`README.md`](README.md). The rules are in the other chunks; this is how they meet the model.
> General live-model technique lives in [`../live-model/README.md`](../live-model/README.md) — this file
> only covers what is specific to sprinklers.

## Where the things you need actually live

| What the rule needs | Revit category | How to get it |
|---|---|---|
| The room outline | `OST_Rooms` | `Room.GetBoundarySegments(new SpatialElementBoundaryOptions())` for the real shape, `Room.IsPointInRoom` to test a candidate head position. **`Area > 0` first** — an unplaced room encloses nothing |
| Sprinkler heads | **`OST_Sprinklers`** | its own category, not Plumbing Fixtures and not Mechanical Equipment |
| Beams, joists | `OST_StructuralFraming` | `LocationCurve` gives the real centreline; the bounding box does not |
| Columns | `OST_StructuralColumns` **and** `OST_Columns` | structural and architectural columns are two different categories and a room usually has both |
| Ceilings | `OST_Ceilings` | read only — `Ceiling.Create` has no overloads before Revit 2022 (confirmed by reflection in this Brain) |
| Slab above | `OST_Floors` | the soffit of the floor above is the deck for an exposed room |
| Wide services | `OST_DuctCurves`, `OST_FlexDuctCurves`, `OST_CableTray`, `OST_Conduit`, `OST_PipeCurves`, `OST_LightingFixtures` | width and soffit level |

Narrow any of these to one room with
[`scripts/filters/by-location/filter-by-room.cs`](../../scripts/filters/by-location/filter-by-room.cs),
which already handles the `Room.IsPointInRoom` awkwardness.

## The traps, in the order they bite

**1. The bounding box of a beam is a lie for anything not axis-aligned.** A beam at 30° has a bounding
box far wider than the beam. Use its `LocationCurve` for the plan line and the type's width for the
thickness; fall back to the bounding box only when there is no location curve, and say when you did.

**2. Rays only see what the 3D view shows.** `ReferenceIntersector` needs a real `View3D` and obeys that
view's visibility completely — a hidden category is invisible to a ray, and the probe reports "clear"
with a beam standing right there. Proven live in this Brain: the same element and code returned 0
neighbours in one 3D view and 4 in another. Every fragment here warns when the category it is looking for
is hidden in the view it picked.

**3. One ray per head misses things.** A single ray from the insertion point only sees what is directly
above the centre. For an obstruction survey, sample a small fan, not one line.

**4. The family's origin is not the deflector.** The Z you write is the family's insertion point; the
code's dimensions are to the flat plate. Measure the difference once per family and carry it as an
explicit input — never assume zero. This is the silent error that makes a fully-checked layout wrong on
site by 50 mm.

**5. `Connector.IsConnected` and element names describe intent, not physical reality.** The Brain's
standing rule, and it applies here: verify geometrically.

**6. Never `Document.Regenerate()` after `Commit()`** — illegal, and it surfaces as a hang, not an error.

## Placing a head

Two shapes, and the family decides which:

- **Unhosted / level-based**: `Document.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural)`,
  then set the height parameter. This is the reliable path and what
  [`../../scripts/creators/create-point-based-element.cs`](../../scripts/creators/create-point-based-element.cs)
  already proves live.
- **Face-based / ceiling-hosted**: needs a `Reference` to the actual face —
  `NewFamilyInstance(reference, point, referenceDirection, symbol)`. Better coordination (the head moves
  with the ceiling), but it fails outright where there is no ceiling, which is exactly the exposed-slab
  case. **Do not choose the hosted route for a car park.**

Either way: `if (!symbol.IsActive) symbol.Activate();` before the first placement, inside the
transaction. A symbol that was never activated places nothing and does not error.

Height parameters differ by family, so read rather than guess. Common ones: `Offset from Host`,
`Elevation from Level`, `Offset`. `ParamText` from
[`../../scripts/lib/prelude.cs`](../../scripts/lib/prelude.cs) keeps "blank" and "no such parameter"
visibly different, which matters here — a blank height reads as zero and puts every head on the floor.

## Reading the plane above a head

The honest way, and the one the fragments use:

1. Ray straight up from the head position, in a 3D view where the target category is visible.
2. Take the **nearest** hit, having dropped self-hits.
3. Record what was hit — Ceiling, Floor, or Structural Framing — because **that identity is the case
   decision**: a Ceiling hit means the ceiling rule, a Floor hit with no framing in between means the
   flat-soffit rule, a Structural Framing hit means the obstructed-construction rule.

Never compute it as level elevation plus a remembered void depth. The Brain has caught four separate
silent-success bugs; assumed heights are how they happen.

## The head schedule — what to write into the model

A placed head with no data is a dot. Whatever the project's parameters are called, the layout is only
usable if these ride along: type (pendent/upright/sidewall/concealed), standard vs extended coverage,
K-factor, temperature rating, response, finish, and the **hazard class and construction type the layout
was computed for**. That last pair is not a sprinkler property in any family, so it goes in a project
parameter or the view's own notes — and it is the one a reviewer asks for first.

## Which fragment to run

| Job | Fragment |
|---|---|
| What is in this room — beams, columns, ceiling, wide services, with soffit levels | [`../../scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs) |
| How many heads and where, derived from the code limits | [`../../scripts/recipes/sprinkler-nfpa-grid.cs`](../../scripts/recipes/sprinkler-nfpa-grid.cs) |
| Do these positions clear the beams and columns | [`../../scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs) |
| Move the failing ones, re-check the spacing | [`../../scripts/recipes/sprinkler-adjust-for-obstructions.cs`](../../scripts/recipes/sprinkler-adjust-for-obstructions.cs) |
| Set / verify the deflector height against what is really above | [`../../scripts/recipes/sprinkler-deflector-height.cs`](../../scripts/recipes/sprinkler-deflector-height.cs) |
| Heads on a wall instead of a ceiling | [`../../scripts/recipes/sprinkler-sidewall-layout.cs`](../../scripts/recipes/sprinkler-sidewall-layout.cs) |
| Place the real families | [`../../scripts/recipes/sprinkler-place-heads.cs`](../../scripts/recipes/sprinkler-place-heads.cs) |
| Audit heads that already exist | [`../../scripts/recipes/sprinkler-compliance-audit.cs`](../../scripts/recipes/sprinkler-compliance-audit.cs) |

**None of the eight has been run against a real model yet** (written 2026-08-20 with no Revit session
available). The Brain's own rule covers this: run one element first, check the real result, then trust it
for the batch — and say plainly that is what you are doing. Each file's STATUS block names exactly which
API calls in it are already proven elsewhere in this library and which are new.
