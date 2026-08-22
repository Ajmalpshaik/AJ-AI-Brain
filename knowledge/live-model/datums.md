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
this datum as a line at all". The add-in uses exactly that as the test, rather than guessing from view
type. Try `Model` first and fall back to `ViewSpecific`; a datum that only has a 2D extent in a view
returns nothing for `Model`.

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

## Guards that belong on any datum operation

- **Not in the Family Editor** — `doc.IsFamilyDocument` — there are no project datums there.
- **Not on a view template** — `view.IsTemplate`.
- **Per-end try/catch**, as above. Never let one refusing end abort a batch.
- **A datum changed in one view can move it in every view.** Setting a *Model* extent is a
  project-wide change dressed up as a per-view one. Say how many views will be written before doing it,
  and treat it as a bulk change under rule 5 of [`../../START-HERE.md`](../../START-HERE.md).

## The fragments

- [`../../scripts/actions/structural-changes/action-reset-datum-extents.cs`](../../scripts/actions/structural-changes/action-reset-datum-extents.cs)
  — put grids/levels back on their shared 3D extent
- [`../../scripts/actions/structural-changes/action-set-datum-bubbles.cs`](../../scripts/actions/structural-changes/action-set-datum-bubbles.cs)
  — show, hide or flip the bubble at each end
- [`../../scripts/recipes/maximize-level-extents.cs`](../../scripts/recipes/maximize-level-extents.cs)
  — stretch every level to the active 3D view's section box, across all its views
