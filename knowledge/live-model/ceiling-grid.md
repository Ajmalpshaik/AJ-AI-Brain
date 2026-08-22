# Live Model — reading a ceiling's grid, and snapping things onto it

Back to [`README.md`](README.md).

The job: **put diffusers, sprinklers, light fittings or smoke detectors on the ceiling tile centres**,
not just "somewhere in the room". Different from
[`../../scripts/actions/move-copy-rotate/action-move-to-ray-hit.cs`](../../scripts/actions/move-copy-rotate/action-move-to-ray-hit.cs),
which snaps an element **up in Z** to whatever surface is above it. That one fixes the height; this one
fixes the **plan position**. A full job usually wants both — grid-snap in plan, then ray-snap in Z.

Harvested 2026-08-22 from the add-in's Ceiling Magnet tool, which has been doing this on real jobs.

## Where the tile size actually comes from — three sources, in this order

Never assume 600×600. Ask the model, and **say which of the three answered**, because the reliability
is not the same:

| # | Source | Works on | How |
|---|---|---|---|
| 1 | The ceiling's **real grid lines** | Revit **2025.3+** only | `Ceiling.GetCeilingGridLines()` returns the actual drawn curves. Exact, and it also gives you a true anchor point for free. |
| 2 | The ceiling **type's surface pattern** | every version | Walk the type's compound structure → each layer's material → `SurfaceForegroundPatternId` → the `FillPattern` → `GetFillGrids()`. |
| 3 | **600 mm fallback** | every version | Only when 1 and 2 both fail. Report it as a guess, never as a reading. |

**Source 2 is the one that matters**, because it is the only one that works on the Revit versions this
Brain mostly meets. The walk has four filters and each one rejects real ceilings:

```
CeilingType.GetCompoundStructure()   -> null on some ceiling types; skip the ceiling
  each CompoundStructureLayer
    doc.GetElement(layer.MaterialId) as Material   -> layers often have no material
      material.SurfaceForegroundPatternId          -> may be InvalidElementId
        FillPatternElement -> GetFillPattern()
          pattern.Target must be FillPatternTarget.Model   <- THE ONE THAT CATCHES PEOPLE
            GetFillGrids(), need >= 2 grids, both Offset > 0
              tileU = grids[0].Offset, tileV = grids[1].Offset, angle = grids[0].Angle
```

**`FillPatternTarget.Model` is the filter that is easy to miss.** A *drafting* pattern scales with the
view and its `Offset` is a paper distance, so taking it as a tile size gives a number that changes with
view scale. Take model patterns only. Take the **first layer that satisfies all four filters**, not the
first layer.

The `Offset` values come back in **internal units (feet)** — they are already a real model distance, so
convert with the normal mm↔feet rule in [`core.md`](core.md); don't treat them as a pattern ratio.

## Turning the angle into axes

```
axisU = (-sin(angle),  cos(angle), 0)
axisV = ( cos(angle),  sin(angle), 0)
```

For a **linked** ceiling, push both through the link's `Transform.OfVector(...)`, then flatten Z to 0
and re-normalise. Skipping the re-normalise is what makes a tilted link produce a grid that slowly
drifts off across the room.

## The anchor point — the one thing a script cannot read

Tile *size* is in the model. Tile *phase* — where the grid actually starts — is not, on any version
before 2025.3. The interactive tool solves this by **asking the user to click one grid intersection**.
A script has to either be told the anchor, or admit it is guessing.

- **2025.3+**: intersect one line from each of the two grid-line families. Free and exact.
- **Otherwise**: take the anchor as an input in mm. If nothing is supplied, fall back to a corner of the
  ceiling's own face and **say so in the report** — the tile centres will be evenly spaced and parallel
  to the real grid, but may sit half a tile out from what is drawn on screen.

Getting the phase wrong is not a visible crash. Everything lands on a neat grid that is simply the wrong
grid, and that is only caught by eye. Report the anchor source every time.

## Nearest tile CENTRE, not nearest grid line

Diffusers go in the middle of a tile, not on the tee bar. In each axis:

```
centre(v, step) = step/2 + round( (v - step/2) / step ) * step
```

Work in the grid's own frame: `rel = point − anchor`, `u = rel · axisU`, `v = rel · axisV`, snap each,
then rebuild `target = anchor + axisU·uSnap + axisV·vSnap` and **keep the element's original Z**. This
is a plan move only — height is a separate decision.

## Which elements are actually over this ceiling — use the face, not the bounding box

A bounding box lies on any ceiling that is not a plain rectangle. An L-shaped ceiling's box covers the
missing corner, so elements in the *next* room get swept up and snapped to the wrong grid.

**The exact test, and it is cheap:** get the ceiling's real solid geometry, find its **largest horizontal
planar face** (`|FaceNormal.Z|` within ~0.01 of 1.0, biggest `Area`), then for each element probe

```
face.Project( new XYZ(point.X, point.Y, face.Origin.Z) )
```

`Face.Project` returns **null when the point falls outside the trimmed face**. That is a true
contains-test against the real boundary, L-shapes and cut-outs included — not an approximation. For a
linked ceiling, push the element's point through the link transform's `Inverse` first so both sides are
in the same frame.

Keep the bounding box only as a defensive fallback for a ceiling with no usable solid geometry.

This is the same "read the geometry, don't trust the shortcut" rule as
[`mep-trace.md`](mep-trace.md) — a different shortcut, the same lesson.

## Guards worth copying

- **Skip pinned elements**, and skip anything without a `LocationPoint`. A pinned element accepts
  `MoveElement` without moving and without complaining — see
  [`geometry-and-transforms.md`](geometry-and-transforms.md).
- **Report `moved` / `already aligned` / `skipped` separately.** "Aligned" is a success, not a no-op, and
  collapsing the two hides the case where nothing was on the grid to begin with.
- **Sanity-check the tile size** before using it: reject anything under ~15 mm or over ~10 m. A pattern
  read that goes wrong usually produces an absurd number rather than a plausible one, so this catches it
  before 200 diffusers move.

## When reading real grid lines (2025.3+)

Only relevant on the newest versions, but the technique is worth keeping because it is how you turn a
pile of unordered curves into a grid:

- **Cluster into two families by direction**, normalising each direction *mod π* first (flip the sign so
  X is non-negative) so a line and its reverse land in the same family. Reject the ceiling if a **third**
  distinct direction shows up — that is not a clean orthogonal grid, so fall back rather than guess.
- **Require the two families to be roughly perpendicular** (within ~8.6° of 90°).
- **Spacing = the MEDIAN of consecutive gaps**, not the mean and not the first gap. Boundary lines get
  clipped by the ceiling edge and duplicate lines sit almost on top of each other, so de-duplicate
  positions within ~3 mm first, then take the median. Then check consistency: if fewer than ~60% of the
  gaps are within 20% of that median, the data is not trustworthy — fall back.

The general rule underneath all three: **on ambiguous data, fall back to the method you can defend, and
say which method answered.** Never quietly guess.

## The fragment

[`../../scripts/actions/move-copy-rotate/action-snap-to-ceiling-grid.cs`](../../scripts/actions/move-copy-rotate/action-snap-to-ceiling-grid.cs)
— dry-run by default, reports which of the three sources gave the tile size and where the anchor came
from.
