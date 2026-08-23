# Live Model — grids and levels: extents, bubbles, and the 2D/3D trap

Back to [`README.md`](README.md).

The jobs here: *"the grids are short in this view"*, *"someone dragged a grid end and now every view is
different"*, *"put all the level lines back"*, *"move the grid bubble to the other side"*.

Harvested 2026-08-22 from the add-in's Datums panel. Before that this Brain had **nothing** on datum
extents — `fragment-index --find datum` returned no match at all.

## Grid and Level are the same thing to the API

Both derive from **`DatumPlane`**, and every method below is on `DatumPlane`. One code path handles
both — collect `OfClass(typeof(Grid))` and `OfClass(typeof(Level))` and treat the results identically.

## The 2D/3D trap — a datum has TWO extents, and they are independent

This is the whole subject, and it is the thing that confuses people at the model.

| | What it is | Enum |
|---|---|---|
| **3D / Model extent** | ONE shared extent. Change it and **every view** that shows the datum changes. | `DatumExtentType.Model` |
| **2D / View-specific extent** | A **per-view** override. Change it and only that view changes. | `DatumExtentType.ViewSpecific` |

In Revit's UI, the little **3D / 2D toggle** at the grid end is which of the two you are dragging. Drag
with it on 2D and you have created a view-specific override that will never follow the model again —
which is exactly how a project ends up with grids that look right in one plan and stop short in the
next.

**"Reset extents to 3D" means: set both ends back to `Model`**, which discards the view-specific
override and makes the datum follow the shared extent again:

```
datum.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.Model);
datum.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.Model);
```

**Per END and per VIEW.** There is no "reset this grid everywhere" call — you loop the views you care
about, and each of the two ends is set separately. Wrap each end in its own try/catch: an end can refuse
(owned by another user, or not available in that view) and the sensible answer is to skip that end, not
to fail the whole run.

## Reading and writing the actual line

```
IList<Curve> curves = datum.GetCurvesInView(DatumExtentType.Model, view);   // null/empty is normal
datum.SetCurveInView(DatumExtentType.Model, view, newCurve);
```

**Set both ends to `Model` BEFORE writing a Model-extent curve.** If the ends are still view-specific,
the write does not land where you meant it to. Order is: set extent type, then set the curve.

**`GetCurvesInView` returning nothing is normal, not an error** — it is how you ask "does this view show
this datum as a line at all". Use exactly that as the test, rather than guessing from view type. Try
`Model` first and fall back to `ViewSpecific`; a datum that only has a 2D extent in a view returns
nothing for `Model`.

### But `SetCurveInView` may refuse outright, and the error blames your geometry (proved 2026-08-22)

On `school.rvt` in Revit 2020.2.9, **every** `SetCurveInView` call on a grid threw:

> The curve is unbound or not coincident with the original one of the datum plane. Parameter name: curve

That is not a maths problem, and the wording sends you the wrong way. Three cases were tried on the same
grid inside one transaction and **all three failed**, including the control:

| what was passed | result |
|---|---|
| `Model` extent, a longer collinear line | refused |
| `ViewSpecific` extent, the same longer line | refused |
| `Model` extent, **the grid's own `grid.Curve`, untouched** | **refused** |

The third row is the one that matters. When a datum hands back a curve it will not accept, no amount of
re-checking your own collinearity, Z values or normalisation will help — stop debugging the geometry.

**To make a datum longer, use `Maximize3DExtents()`.** It is the API behind Revit's own "maximize 3D
extents", it takes no curve, and it worked on all six grids on the first attempt. Fragment:
[`action-maximize-datum-extents.cs`](../../scripts/actions/structural-changes/action-maximize-datum-extents.cs).
The cost is that it gives no control over margin — it lands flush on the model extent.

### Never measure "the model extent" with a whole-model bounding box

The obvious way to work out how long a grid *should* be is to bound-box every element. On `school.rvt`
that returns **±30480 mm — exactly ±100 ft — on a building only 41 m across.** The cause is **`Sheets`**:
each sheet carries a nominal ±100 ft box in model space. `Cameras` also sit outside the building.

Measure only physical categories — Walls, Floors, Doors, Windows, Rooms, Ceilings, Roofs, Columns,
Structural Framing, Stairs. The same trap applies to anything else sized from "the model": scope boxes,
section extents, crop regions, a view's zoom.

## Which views can write a LEVEL extent — not plans

A level shows as a **line** only in **elevation, section and 3D** views. In a plan view a level is not a
line, so there is no extent to read or write there. Grids are the opposite way round — they show as
lines in plans as well.

Two ways to pick the views, and they are for different jobs:

- **Discover them** — loop every non-template view and keep the ones where `GetCurvesInView` returns
  something. Correct and complete, but it walks every view in the project.
- **Filter by type** — `ViewType.Elevation`, `ViewType.Section`, `ViewType.ThreeD`. Cheaper, and right
  for a bulk pass across the whole model.

## Stretching levels to a 3D view's section box

The "make all my level lines reach across the building" job. Two pieces of real technique:

**The section box is in its own transform.** `view3D.GetSectionBox()` returns a `BoundingBoxXYZ` whose
Min/Max are in the box's own frame — push all eight corners through `box.Transform` to get world
coordinates before taking min/max X and Y. Same trap as the view crop box, in
[`../../scripts/actions/visibility/action-set-view-crop.cs`](../../scripts/actions/visibility/action-set-view-crop.cs).
Check `view3D.IsSectionBoxActive` first; a box that is off still returns geometry.

**Project the box corners onto the datum's own line — do not build an axis-aligned line.** A level line
can sit at any angle, so:

1. Take the level's existing curve as a `Line`, and make an **unbound** line from its start and
   direction.
2. `unbound.Project(corner)` each of the section box's four plan corners (at the level's own Z) and keep
   the **min and max parameter**.
3. `unbound.Evaluate(param, false)` both, and `Line.CreateBound` the result.

That gives the shortest line along the datum's own direction that still spans the box, at any angle. An
axis-aligned box would be wrong the moment the grid is rotated.

## Bubbles

Per end, per view, and there are only three calls:

```
datum.IsBubbleVisibleInView(DatumEnds.End0, view)
datum.ShowBubbleInView(DatumEnds.End1, view)
datum.HideBubbleInView(DatumEnds.End0, view)
```

**"Flip the bubble" is not one call** — it is read both ends, then hide one and show the other. The two
awkward cases are worth handling explicitly rather than letting them no-op:

- **neither end visible** → show End0 (something has to appear, or the "flip" looks broken)
- **both ends visible** → hide End1 (leaving one, which is what "flipped" should mean)

Bubbles make sense in **plan, ceiling plan, engineering plan, area plan, section and elevation** views.

### `DatumEnds.End0` is NOT `Curve.GetEndPoint(0)` — measured 2026-08-22

This is the trap that turns "put the bubbles on the left" into a coin flip. `DatumEnds.End0` and
`End1` are labels for the datum's own two ends, and on `school.rvt` they came out **reversed** against
the curve's endpoint order. Proved by moving bubbles and exporting the plan as a PNG twice:

| | `Curve.GetEndPoint(0)` | `Curve.GetEndPoint(1)` |
|---|---|---|
| grid 5, along X | X = −23034 (drawn first) | X = +24246 |
| bubble on `End0` showed at… | | **X = +24246** ✔ picture |
| bubble on `End1` showed at… | **X = −23034** ✔ picture | |

So `End0 ↔ GetEndPoint(1)` and `End1 ↔ GetEndPoint(0)` there. **Do not port that mapping as a constant** —
it follows the direction each datum was drawn in, so a model with grids drawn both ways will have both
mappings at once.

**The safe pattern is to never name an end directly.** Ask which end *displays* at the coordinate you
want, then act on that one:

```
Func<Grid, DatumEnds, XYZ> shownAt =
    (g, e) => e == DatumEnds.End0 ? g.Curve.GetEndPoint(1) : g.Curve.GetEndPoint(0);
// then: for "left", keep whichever end's shownAt(...).X is smaller
```

That is what "all the X grids on the left, all the Y grids above" needs, and it survives grids drawn in
opposite directions — which is exactly the case `action-set-datum-bubbles.cs` warns flips half a batch
the wrong way when you use its `flip` mode on a mixed set. **Verify the first run with a picture**, not
by reading `IsBubbleVisibleInView` back: reading it back only confirms which *label* is on, which is the
very thing that misleads.

## Guards that belong on any datum operation

- **Not in the Family Editor** — `doc.IsFamilyDocument` — there are no project datums there.
- **Not on a view template** — `view.IsTemplate`.
- **Per-end try/catch**, as above. Never let one refusing end abort a batch.
- **A datum changed in one view can move it in every view.** Setting a *Model* extent is a
  project-wide change dressed up as a per-view one. Say how many views will be written before doing it,
  and treat it as a bulk change under rule 5 of [`../../START-HERE.md`](../../START-HERE.md).

## The fragments

- [`../../scripts/actions/structural-changes/action-maximize-datum-extents.cs`](../../scripts/actions/structural-changes/action-maximize-datum-extents.cs)
  — make grids/levels span the whole model ("the grids only go half way"). ✓ verified 2026-08-22
- [`../../scripts/actions/structural-changes/action-reset-datum-extents.cs`](../../scripts/actions/structural-changes/action-reset-datum-extents.cs)
  — put grids/levels back on their shared 3D extent
- [`../../scripts/actions/structural-changes/action-set-datum-bubbles.cs`](../../scripts/actions/structural-changes/action-set-datum-bubbles.cs)
  — show, hide or flip the bubble at each end
- [`../../scripts/recipes/maximize-level-extents.cs`](../../scripts/recipes/maximize-level-extents.cs)
  — stretch every level to the active 3D view's section box, across all its views
