# Live Model — dimensioning: getting a Reference that actually works

Back to [`README.md`](README.md).

Dimensioning by script fails on one thing and one thing only: **a dimension needs geometry
`Reference` objects, and getting a valid one is the whole job.** `NewDimension` itself is trivial.

Harvested 2026-08-22 from the add-in's dimension services (~240 KB across five services, the largest
area in the whole add-in). Everything below is a defect that was found and fixed there on real work.

## Three ways to get a Reference, and what each one can dimension

| Route | Works for | Fails for |
|---|---|---|
| **`new Reference(element)`** | **Datums — and WALLS**. Grids and levels; on a wall it resolves to the **location line**, which is how you get a CENTRELINE dimension (measured 2026-08-23, see below) | **Throws for a pipe or duct**: *"the references are not geometric references"* |
| **`FamilyInstance.GetReferences(type)`** | Family instances **whose author marked reference planes as references** | Anything not a FamilyInstance; and MEP fittings/accessories, which expose **zero** of all four types |
| **`HostObjectUtils.GetSideFaces(host, shell)`** | **Walls, floors, roofs, ceilings** — one call, no geometry walk | Anything that is not a `HostObject`. And see the shell-layer trap below — it does not mean what its name suggests |
| **Walking the geometry for `.Reference`** | **Everything with real geometry** — ducts, pipes, conduit, walls | Nothing much; this is the general route |

The first two are already in this Brain — [`../../scripts/creators/create-dimension.cs`](../../scripts/creators/create-dimension.cs)
uses datums, [`../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs`](../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs)
uses family references and **measured** that MEP fittings expose none. The third is what this note adds.

**`new Reference(element)` is not a safe fallback.** It is tempting because `NewDimension` accepts it
for a datum. On a pipe or duct it throws — and in a chained dimension, **that one bad reference takes
down every good reference sharing the array**. Drop the candidate and report honestly instead.

## Dimensioning to a WALL: `Interior` is not the side facing the room (measured 2026-08-22)

`HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior | .Exterior)` is the short way to a wall
face `Reference` — no geometry walk, one call. The trap is the enum. **`Interior` and `Exterior` name
sides of the wall's own layer structure, not "inside the room" and "outside the building"**, so which
one bounds a given room follows **the direction that wall was drawn**.

Measured on `school.rvt` in a single pass, every wall the same `Generic - 200mm` type:

| Room | Face 0 mm from the room boundary | Face 200 mm away |
|---|---|---|
| Room 1 | `Interior` | `Exterior` |
| Room 2 | `Interior` | `Exterior` |
| **Room 3** | **`Exterior`** | `Interior` |

**This failure does not throw.** Hard-code either name and two rooms dimension correctly while the third
silently reports a number one wall-thickness out — which on a 200 mm wall is the kind of error that
reaches a drawing.

**Pick the face by distance instead.** Project the room boundary segment's midpoint onto each candidate
face and keep the nearest:

```
foreach (var st in new[]{ ShellLayerType.Exterior, ShellLayerType.Interior })
    foreach (var r in HostObjectUtils.GetSideFaces(wall, st))
    {
        var f = wall.GetGeometryObjectFromReference(r) as Face;
        var pr = f?.Project(segmentMidpoint);
        if (pr != null && pr.Distance < bestD) { bestD = pr.Distance; best = r; }
    }
```

Then **cross-check the created dimension against geometry you computed independently** — compare
`Dimension.Value` to the room's own boundary extents and print MATCH or OFF BY. That check is what makes
this silent failure loud. It is built into
[`../../scripts/actions/sheets-views/action-dimension-rooms.cs`](../../scripts/actions/sheets-views/action-dimension-rooms.cs).

This is the same *shape* of trap as `DatumEnds.End0` not being `Curve.GetEndPoint(0)`
([`datums.md`](datums.md)): **a Revit enum whose name implies a physical side, whose actual meaning
follows how the element was drawn.** When you meet one, resolve it by measuring, never by reading the
name — and prove it with a picture or an independent number.

## The three numbers a room has, and how to get each (measured 2026-08-23)

Ajmal's own words: *"see some time i need from outside sometime i need inside and also from the wall
side also from wall mid also i need."* A room does not have one size — it has three, and a drawing may
need any of them. All three measured on `school.rvt` Room 4, 8800 mm clear between 200 mm walls:

| He calls it | Measures | How | Result |
|---|---|---|---|
| **inside** / interior dimension | clear internal room | the **NEAREST** wall face to the room boundary | **8800** |
| **outside** / exterior dimension | overall external | the **FARTHEST** wall face | **9200** |
| **wall mid** | wall centreline to centreline | **`new Reference(wall)`** | **9000** |

Two things make this work, and neither is obvious:

**Nearest vs farthest is the whole switch between inside and outside.** Both faces come from the same
`HostObjectUtils.GetSideFaces` call — the room-side face projects to 0 mm from the boundary segment
midpoint, the outer face to the wall's thickness. So "measure from outside" is one comparison operator
away from "measure from inside", and neither needs a named `ShellLayerType` (which would be wrong
anyway — see the section above).

**`new Reference(wall)` does NOT throw for a wall**, even though the table at the top of this note is
easy to read as saying it would. It throws for a pipe or duct; on a wall it resolves to the wall's
**location line** and `NewDimension` accepts it, giving the centreline dimension. There is no other
route to one: a wall's geometry contains **no centreline curve at all** — measured on this model,
`get_Geometry` with `ComputeReferences` *and* `IncludeNonVisibleObjects` returned **0 curves** and
`LocationCurve.Curve.Reference` was **null**. The duct-centreline technique below simply does not
transfer to walls.

⚠ **The catch, and it is untested:** the location line is only the geometric middle when the wall's
**Location Line** parameter is "Wall Centerline". Set it to a finish or core face and the "centreline"
dimension quietly measures to that instead. Every wall verified here was Wall Centerline. The defence is
the same one this whole note argues for — **compute the expected number independently** (room boundary
plus half each wall's real `Width`) and print MATCH or OFF BY, so an offset location line shows up as a
number rather than as nothing.

**Where the string sits is a separate question from what it measures.** An exterior dimension drawn
inside the room is legal and sometimes what a crowded plan needs. Keep the two as independent inputs.

Both are in [`../../scripts/actions/sheets-views/action-dimension-rooms.cs`](../../scripts/actions/sheets-views/action-dimension-rooms.cs)
as `measureTo` and `lineInsideRoom`. **Core faces are deliberately absent**: `GetSideFaces` only offers
the Interior/Exterior shell layers, so a multi-layer wall's core needs a compound-structure walk, and
this model has only single-layer walls to prove it against.

## The geometry route — and the option everyone misses

```
element.get_Geometry(new Options {
    ComputeReferences      = true,   // without this every .Reference is null
    IncludeNonVisibleObjects = true, // THE CENTRELINE IS A NON-VISIBLE OBJECT
    DetailLevel            = ViewDetailLevel.Fine
})
```

**`IncludeNonVisibleObjects = true` is what makes a duct's centreline reachable at all.** Leave it out
and the centreline simply is not in the geometry, so a round pipe yields nothing and it looks like the
API cannot do it.

Then walk the `GeometryElement`, **recursing into every `GeometryInstance`** via `GetInstanceGeometry()`,
and take `curve.Reference` (or `face.Reference`) where it is non-null.

`LocationCurve.Curve.Reference` is worth trying first but **is usually null on MEP curves** — the
geometry pass is what actually finds one.

## Six defects worth not repeating

Each of these produced a wrong answer rather than an error:

1. **A Coarse view returns geometry with no solids.** Revit draws MEP as single lines in Coarse, so
   asking the *view* for geometry gives a valid, non-null `GeometryElement` containing only lines. Code
   that falls back to model geometry only when the result is **null** never triggers. Gate the fallback
   on *"no usable solid"*, not on null.
2. **`PlanarFace.Origin` is the plane's parametric origin and can sit OUTSIDE the face.** Using it as a
   position mis-places the reference on any face even slightly off-parallel. Take a **tessellated edge
   point** nearest the measuring line instead.
3. **Round pipes, conduit and round ducts have no planar side faces at all.** A face-only search finds
   nothing on them. Fall back to the **centreline** reference.
4. **One failed edge must not discard the whole face.** Catching per-face collapsed a face to a single
   point and wrongly rejected long walls. Catch **per edge**.
5. **`view.CropBox` returns a stale extent when the crop is switched OFF.** Using it as a search
   distance culls genuine references. Gate on `view.CropBoxActive` first — the same class of trap as the
   scope box in [`../../scripts/actions/visibility/action-set-view-crop.cs`](../../scripts/actions/visibility/action-set-view-crop.cs).
6. **A de-duplication key that falls back to the element id makes every face on one element identical**,
   so all but one are silently discarded and the run then fails with a misleading message. A key that
   cannot be built should be **empty and the candidate dropped honestly**.

## The dimension TYPE

Only **`DimensionStyleType.Linear`** and **`LinearFixed`** are legal for a linear dimension. Handing
`NewDimension` an angular or radial type is a guaranteed failure. Collect `DimensionType` and filter on
`StyleType`; if that yields nothing useful, fall back to all of them and let Revit reject an unusable
one rather than presenting an empty list that looks like a broken tool.

## Reading a dimension back

**`Dimension.Curve` throws *"The input curve is not bound"***. To verify what you made, use
`NumberOfSegments`, `Segments` and `References` — never `.Curve`. (Measured in this Brain 2026-08-14.)

## Batching

Create the whole batch inside **one transaction** so it is a single undo step, and have the creation
helper return `false` with a reason instead of throwing — one bad reference must not abort the run.

## The fragments

**These five WRITE Dimension elements into a view. Pick by what is being dimensioned:**

- [`../../scripts/creators/create-dimension.cs`](../../scripts/creators/create-dimension.cs) — across grids and levels
- [`../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs`](../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs) — between family instances
- [`../../scripts/actions/sheets-views/action-dimension-mep-runs.cs`](../../scripts/actions/sheets-views/action-dimension-mep-runs.cs) — **ducts, pipes, conduit and trays via the geometry route**, which is the gap the other two could not fill. This is the one for *"dimension between the ducts"*, *"put a dimension between the services"*
- [`../../scripts/actions/sheets-views/action-dimension-rooms.cs`](../../scripts/actions/sheets-views/action-dimension-rooms.cs) — **each room's width and depth wall-face to wall-face**, via `HostObjectUtils` with the nearest-face rule above. ✓ verified 2026-08-22. **Project X/Y only — see the rotation gotcha in its header before using it on a site that is not square to north**
- [`../../scripts/actions/sheets-views/action-dimension-wall-openings.cs`](../../scripts/actions/sheets-views/action-dimension-wall-openings.cs) — **along a wall, picking up every door and window opening in it** — a running string plus an overall

**Measuring is not dimensioning, and the same sentence asks for both.** A question wants a number and
must change nothing; an instruction to annotate writes elements into the view. These do not draw:

- [`../../scripts/actions/reporting/action-report-room-dimensions.cs`](../../scripts/actions/reporting/action-report-room-dimensions.cs) — read-only room width × length **on the room's own axes**, so it is the one that is right on a rotated room. *"how big is room 4"*, *"what are the room sizes"*
- [`../../scripts/actions/qa-checks/action-report-mep-clearance.cs`](../../scripts/actions/qa-checks/action-report-mep-clearance.cs) — the exact gap in mm between MEP runs. *"how much clearance is between those two ducts"*. Its own header carries the five-way clearance routing table

Measured 2026-08-24 against this Brain's own search: *"dimension between the ducts"* returned two
clearance reports at the top and `action-dimension-mep-runs.cs` nowhere at all, and *"what are the room
sizes"* returned the fragment that **draws** at #1 with the read-only one absent from the top three.
Both were vocabulary gaps, not missing fragments — the spoken phrasings are now written into the
headers themselves, which is where the search actually reads them.
