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
