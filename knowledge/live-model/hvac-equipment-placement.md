# Live Model — Placing and orienting MEP equipment (FCU and similar)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.
> Drawing the ductwork that connects it lives in [`hvac-ducts.md`](hvac-ducts.md).

## Rotating equipment to face a target direction (e.g. "FCU duct connector toward the terminals")
Used to rotate a placed FCU so its supply-air duct connector faces the centroid of that room's air
terminals (2026-07-08).
- **Identifying the right connector on Mechanical Equipment**: `FamilyInstance.MEPModel.ConnectorManager
  .Connectors` gives every connector (piping, electrical, HVAC all mixed together) — filter to
  `Domain == Domain.DomainHvac`. This FCU family exposed **two** HVAC duct connectors, both
  `Connector.DuctSystemType == DuctSystemType.SupplyAir`: one labeled `Description == "Fresh Air"` (outside
  air intake) and one with an **empty** `Description` (the real supply-air-out connector that needs to face
  the terminals). Don't assume there's only one HVAC connector on an equipment family — check
  `Description` (and `DuctSystemType`) to pick the right one.
- **Reading a connector's current facing direction**: `connector.CoordinateSystem.BasisZ` — already in world
  coordinates (accounts for the instance's current placement/rotation), no extra transform needed.
- **Computing and applying the rotation**: project both the connector's current direction and the target
  direction onto the XY plane (zero out Z), get each angle via `Math.Atan2(dir.Y, dir.X)`, and rotate by
  `targetAngle - currentAngle` (normalize into `(-π, π]` by adding/subtracting `2π`). Apply with
  `ElementTransformUtils.RotateElement(doc, elementId, Line.CreateBound(pt, pt + XYZ.BasisZ), rotation)`
  using a vertical axis through the element's own insertion point — rotates the whole instance in place.

## Placing equipment relative to a door (e.g. "FCU near the door side")
Used to move a room-center-placed FCU to sit near its door instead (2026-07-08).
- **Which door belongs to which room**: doors don't have a direct "room" property — use the **phase-based**
  `FamilyInstance.get_ToRoom(phase)` / `get_FromRoom(phase)` (get a `Phase` via
  `Document.Phases.get_Item(Document.Phases.Size - 1)` for the current/last phase), and match either side
  against the room's `Id`. A room can have more than one door — pick the one relevant to the request if so.
- **Getting the wall's in-plan direction and an inward-pointing normal**: `door.Host as Wall`, then
  `(wall.Location as LocationCurve).Curve.GetEndPoint(0/1)` to get the wall direction vector, and
  `new XYZ(-direction.Y, direction.X, 0)` for a perpendicular normal — but this doesn't tell you which of
  the two perpendicular directions points *into* the room vs. into the neighboring space. **Test it**:
  offset a small distance (e.g. 200mm) from the door's location point along the candidate normal, then
  check `room.IsPointInRoom(testPoint)` — if false, flip the normal. Don't assume a fixed sign convention,
  it depends on which way the wall's location curve happens to run.
- Final placement = door's `LocationPoint` + the confirmed inward normal × the desired inset distance,
  at whatever Z height the equipment needs (independent of this XY calculation).
- **Correction (2026-07-08): "move toward the door" means shift in ONE axis only (perpendicular to the
  door's wall), not snap to the door's exact position on both axes.** the user explicitly rejected using the
  door's full location (which also pulls the along-wall/tangential coordinate to match the door) — the
  along-wall coordinate should stay wherever it already was (e.g. the room's center), only the
  perpendicular-to-wall coordinate should move toward the wall. Decompose using the wall's tangent vector
  `t` (its direction vector) and the inward normal `n`: keep the original point's component along `t`
  unchanged, replace only the component along `n`. Formula used: `finalXY = doorPt + inward*insetFt +
  t * Dot(originalPoint - doorPt, t)` — i.e. take the perpendicular offset from the door/wall, but the
  tangential (along-wall) position from wherever the equipment already was, not from the door.

## `Default Elevation` is ignored by `NewFamilyInstance` (2026-08-25)

A Mechanical Equipment family's type parameter **`Default Elevation`** only feeds Revit's **UI placement
tool**. Placing the same family through the API puts it at the given point and **nothing lifts it** —
18 FCUs with `Default Elevation = 2400` all landed at Z 0, on the floor, with no error.

Set the height explicitly after placing:

```csharp
var p = inst.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);        // "Elevation from Level"
// fallback: BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM                 // "Offset from Host"
p.Set(2400.0 / 304.8);
```

Both parameters exist on a level-based equipment family and both are writable; `INSTANCE_ELEVATION_PARAM`
is the one that reads "Elevation from Level".

**Verify against the geometry, not the parameter.** Read `LocationPoint.Z` or the bounding box — a
parameter can read 2400 while the instance has not moved.

Expect the bounding box to extend **below** the unit when the family carries a clearance zone: an FCU at
2400 with 450 mm bottom clearance reports a box starting at 1950.

## Replicating a placement the user has already made by hand

He places one, then asks for the rest — *"I PLACED SME LIKE THAT ADD ALL THE ROOM DOOR"*. **Measure his
one before copying it.** A door that looked centred was actually **200.0 mm clear of the adjacent wall
face** — a round number, so deliberate. Copying "centre of the wall" would have been wrong in all 17.

Derive the rule as an offset from a *room edge*, not an absolute coordinate, so it survives rooms of
different sizes. And match the swing: read `FacingOrientation` on his instance and `flipFacing()` the new
ones so each opens the same way relative to its own room — rooms on the opposite side of a corridor need
the opposite facing to achieve the same result.

`FamilyInstance.HostId` does not exist in Revit 2020 — use **`inst.Host.Id`**, guarding for a null Host.

## Matching an element to its room: use containment, never one coordinate

A verification that found each FCU by **X alone** reported all 18 correct when 9 of them were the wrong
unit. In a corridor layout the rooms on either side share the same X centres, so `Math.Abs(lp.X - cx) <
tol` always returned the first match — the north row — and it was compared against the south corridor.
It still printed "yes" for every row.

**Match on the full bounding box:**

```csharp
var f = list.FirstOrDefault(x => { var lp = x.Location as LocationPoint;
    return lp != null && lp.Point.X > bb.Min.X && lp.Point.X < bb.Max.X
                      && lp.Point.Y > bb.Min.Y && lp.Point.Y < bb.Max.Y; });
```

or `Room.IsPointInRoom`. **A symmetric layout makes a single-axis match silently wrong**, and the failure
looks like a pass.

## "Move it to the door side, return facing the door"

His standing arrangement for a room off a corridor: FCU pulled toward the corridor wall with the
**return** connector facing it — return air is drawn back through the door/corridor side, supply blows
into the room.

- **Corridor side = where that room's door is**, which is the room's low-Y edge for rooms north of a
  corridor and the high-Y edge for rooms south of it.
- Position by an offset from the **room boundary** — 1000 mm to the FCU centre worked here — not by an
  absolute coordinate.
- The family's return sits on its **+Y** face, so rooms whose corridor is to the **south need a 180°
  rotation**; rooms whose corridor is north need none.

Rotate about a vertical axis through the instance's own location point:

```csharp
var p = (f.Location as LocationPoint).Point;
ElementTransformUtils.RotateElement(doc, f.Id,
    Line.CreateBound(p, new XYZ(p.X, p.Y, p.Z + 10)), Math.PI);
```

**Verify by reading the real connectors** — `f.MEPModel.ConnectorManager.Connectors`, checking
`c.DuctSystemType` and comparing each origin's distance to the corridor wall. Trusting the rotation angle
alone proves nothing.

**Say the side-effect out loud:** rotating 180 degrees also swaps which side the pipe stubs and control
box face, so the two rows end up mirrored.
