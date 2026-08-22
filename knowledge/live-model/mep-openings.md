# Live Model — cutting openings where MEP crosses a wall, floor or beam

Back to [`README.md`](README.md).

The job: *"put the sleeves in"*, *"cut the openings for the ducts"*, *"the pipes go through this wall,
make the holes"*. Harvested 2026-08-22 from the add-in's MEP Openings tool — at ~136 KB it is the
largest single service in that add-in, and the Brain had **nothing** on openings before this.

This note is the method. The fragment is
[`../../scripts/recipes/create-mep-openings.cs`](../../scripts/recipes/create-mep-openings.cs), which
does the core job; the add-in's version additionally handles merging, reruns and family-based sleeves,
and those parts are described here so they can be built the day a job needs them.

## Three host types, three completely different API calls

This is the thing to get right first. `Document.Create.NewOpening` has **three overloads** and picking
the wrong one throws or silently makes the wrong shape:

| Host | Call | Notes |
|---|---|---|
| **Wall** | `doc.Create.NewOpening(wall, p1, p2)` | Two **opposite corner points**. Rectangular, in the wall's own plane: width along the wall direction, height in Z. No profile. |
| **Floor / roof / ceiling** | `doc.Create.NewOpening(host, profile, true)` | A `CurveArray` **profile**. The `true` means perpendicular to the face. |
| **Beam / structural framing** | `doc.Create.NewOpening(host, profile, eRefFace)` | A profile **plus a reference face** — `eRefFace.CenterX` / `CenterY` / `CenterZ` (`Autodesk.Revit.Creation.eRefFace`). |

**For a beam, which reference face works is not predictable — try them in order.** The add-in builds a
profile for `CenterY` first, then `CenterZ`, then `CenterX`, taking the first that does not throw. Do
not try to reason out which one is right from the beam's orientation; catch and fall through.

## Finding the crossing — real solid intersection, not bounding boxes

Two bounding boxes overlap constantly without the elements touching — a duct running *past* a wall in
the next room shares a box corner with it. The honest test is a boolean:

```
Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
    mepSolid, hostSolid, BooleanOperationsType.Intersect);
if (intersection == null || intersection.Volume <= 1e-7) -> no real crossing
```

Then take `intersection.GetBoundingBox()` as the opening extent.

Three practical points that are not obvious:

- **An element can have several solids** (a duct with insulation, a wall with layers). Loop every
  pairing and **union** the intersection boxes you get.
- **Some Revit solids refuse boolean operations and throw.** Catch per *pair* and carry on — another
  pair of solids on the same two elements will usually still work. One throw must not abandon the
  element.
- Use `Options { DetailLevel = Fine, IncludeNonVisibleObjects = false, ComputeReferences = false }` to
  get the geometry.

## Linked MEP into a host model

The common real case: services are in a linked model, the wall is in yours.

- Get the linked element's solids, then bring each one into host coordinates with
  **`SolidUtils.CreateTransformed(solid, linkInstance.GetTotalTransform())`**. Only then intersect.
- **The link is read-only.** You cut the opening in *your* host element. A linked host cannot be
  modified at all — that case needs a face-based opening *family* placed in your model instead, which
  is a different technique.

## The extent, and why a raw intersection box is not enough

- **Minimum profile size.** A grazing crossing gives a sliver box, and a sliver opening is useless and
  sometimes invalid. Clamp width and height to a minimum, and take that minimum as a per-request input,
  not a constant.
- **Clearance.** A real sleeve is bigger than the pipe. The intersection box is the pipe's own
  footprint, so add the clearance the project asks for — again a per-request number, never a default.
- **Wall openings work along the wall direction.** Take the range of the crossing box projected onto
  the wall's own direction, not its world X or Y — a wall at 30 degrees would otherwise get an opening
  far too wide.

## Merging, and re-running without making a mess

Both matter on a real job and are worth building when the job needs them:

- **Merge nearby crossings on the same host into one opening.** Four pipes through one wall should be
  one builder's opening, not four. Group the crossings by host, then union boxes that are within the
  merge distance of each other.
- **A rerun must not duplicate.** Before creating, check whether an existing `Opening` on that host
  already covers the crossing box. Replace an existing opening only when the merged extent genuinely
  needs to be bigger. Without this, running the tool twice doubles every opening.

## What this is NOT

Not the same as the sprinkler/coverage geometry work, and not
[`../../scripts/actions/qa-checks/action-report-clashes.cs`](../../scripts/actions/qa-checks/action-report-clashes.cs)-style
clash *reporting* — this **cuts real holes in real elements** and is destructive. Treat it under rule 5
of [`../../START-HERE.md`](../../START-HERE.md): say how many openings in how many hosts, and get a
go-ahead. The fragment is dry-run by default for that reason.

Deleting an `Opening` afterwards is a normal element delete, so a bad run is recoverable — but only if
you know which ids were created, so the fragment reports them.
