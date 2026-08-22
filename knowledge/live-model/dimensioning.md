# Live Model — dimensioning: getting a Reference that actually works

Back to [`README.md`](README.md).

Dimensioning by script fails on one thing and one thing only: **a dimension needs geometry
`Reference` objects, and getting a valid one is the whole job.** `NewDimension` itself is trivial.

Harvested 2026-08-22 from the add-in's dimension services (~240 KB across five services, the largest
area in the whole add-in). Everything below is a defect that was found and fixed there on real work.

## Three ways to get a Reference, and what each one can dimension

| Route | Works for | Fails for |
|---|---|---|
| **`new Reference(element)`** | **Datums only** — grids and levels | **Throws for a pipe or duct**: *"the references are not geometric references"* |
| **`FamilyInstance.GetReferences(type)`** | Family instances **whose author marked reference planes as references** | Anything not a FamilyInstance; and MEP fittings/accessories, which expose **zero** of all four types |
| **Walking the geometry for `.Reference`** | **Everything with real geometry** — ducts, pipes, conduit, walls | Nothing much; this is the general route |

The first two are already in this Brain — [`../../scripts/creators/create-dimension.cs`](../../scripts/creators/create-dimension.cs)
uses datums, [`../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs`](../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs)
uses family references and **measured** that MEP fittings expose none. The third is what this note adds.

**`new Reference(element)` is not a safe fallback.** It is tempting because `NewDimension` accepts it
for a datum. On a pipe or duct it throws — and in a chained dimension, **that one bad reference takes
down every good reference sharing the array**. Drop the candidate and report honestly instead.

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

- [`../../scripts/creators/create-dimension.cs`](../../scripts/creators/create-dimension.cs) — across grids and levels
- [`../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs`](../../scripts/actions/sheets-views/action-add-aligned-dimensions.cs) — between family instances
- [`../../scripts/actions/sheets-views/action-dimension-mep-runs.cs`](../../scripts/actions/sheets-views/action-dimension-mep-runs.cs) — **ducts, pipes, conduit and trays via the geometry route**, which is the gap the other two could not fill
