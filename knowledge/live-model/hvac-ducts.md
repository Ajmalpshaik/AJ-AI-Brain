# Live Model — HVAC ductwork — drawing, branching, connecting

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.
> Sizing/slicing a trunk lives in [`hvac-duct-sizing.md`](hvac-duct-sizing.md); placing and orienting
> the equipment itself lives in [`hvac-equipment-placement.md`](hvac-equipment-placement.md).

> **split-review: kept whole** (reviewed 2026-09-02, at 314 lines). Past the ~300-line rule and staying
> that way. What is left here is ONE job read in order — draw the duct, branch it, connect the FCU, put
> it on a system, set the terminals — and the sections are not independent topics that happen to share a
> heading: the traps in each one bite in the others. `Duct.LevelId` going invalid after a `BreakCurve`
> is written up under *connecting to an existing open end* and is exactly what breaks a `Duct.Create`
> under *branching*; the BFS-not-first-connector rule under the same heading is what makes the
> verification in every other section trustworthy; and the last section closes the loop by proving the
> whole chain — a connector reading 800 L/s because Revit summed four 200 L/s terminals it could
> actually trace. Split those apart and each half sends the reader to the other.
>
> **The split has already been done once**, which is the strongest argument for stopping: sizing and
> equipment placement were taken out into their own files, and the two lines above route to them. What
> remains is the residue that would not divide. Per
> [`skills/brain-self-maintain/SKILL.md`](../../skills/brain-self-maintain/SKILL.md), the 300-line rule
> is *"a split candidate, not a mandate — if it's one coherent job read as a unit, splitting adds hops
> and makes things worse; say so and leave it."* This is that case, said out loud so the next status run
> stops re-raising it.

## Drawing a duct between two points, with or without connecting it
Used to draw a main duct from each room's FCU across the room (2026-07-08).

**Correction: "main duct to the farthest terminal" does NOT mean the duct literally ends at that
terminal's connector.** First attempt used the farthest supply terminal's own connector origin as the
duct's endpoint — the user rejected this (also broke down with ties: two terminals equidistant from the FCU
give an arbitrary, meaningless pick). What the user actually wants, confirmed after showing them the
numbers: the main trunk runs **straight along the room's long axis from the FCU to near the far wall**
(same wall-side as the terminal grid, using the same clearance already established for terminal wall gaps —
e.g. 750mm), staying **level at the FCU's own height** the whole way (not dropping to the terminals' lower
Z) and **fixed at the FCU's own coordinate on the short axis** (no sideways drift). Concretely: pick the
long/short axis the same way as the terminal checkerboard grid (compare room bounding-box X vs Y extent);
travel-direction sign = which way the terminals are from the FCU (`Math.Sign(farthest terminal coord - FCU
coord)` on the long axis only, just for direction, not as the endpoint itself); endpoint's long-axis
coordinate = `(wall bbox coordinate on that side) - sign * clearance`; endpoint's short-axis coordinate and
Z both stay equal to the FCU connector's own. The "farthest terminal" is only used to pick which direction
along the axis to travel, never as the actual endpoint.

**`Duct.Create(...)` alone draws a plain, unconnected straight duct — it does NOT join anything by
itself.** the user explicitly asked for just the geometry first ("no need to connect, just draw straight
duct, we will connect this after") — a real, deliberate two-step workflow, not a shortcut. Don't
auto-chain the `ConnectTo` calls below unless asked; drawing the duct and connecting it are two separate
asks that can land in different turns.

**A new duct must be explicitly sized to match its source connector — `Duct.Create` does NOT inherit the
connector's size on its own.** the user undid an entire duct-drawing pass over this: the FCU's supply
connector was 1050×330mm, but the freshly created duct came out at the duct type's own default size,
producing a visible mismatch at the FCU even though geometrically the duct started at the right point.
Fix: read `connector.Width` / `connector.Height` from the source connector and set them on the new duct.
**`MEPCurve.Width`/`.Height` (the properties on `Duct`) are read-only** — setting them directly is a
compile error ("cannot be assigned to -- it is read only"). Set the actual parameters instead:
`duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(connector.Width)` and
`RBS_CURVE_HEIGHT_PARAM` for height. Do this for any duct drawn to originate at a specific piece of
equipment — always match its size to that equipment's connector, don't rely on the duct type's default.

**Clarified same session: "don't connect yet" meant the far/terminal end specifically, not the FCU end.**
Right after drawing the unconnected duct the user immediately asked "why is this not from the FCU, it needs
to connect from the FCU" — so the FCU-side connection should be made as soon as the duct is drawn (it's
the duct's actual origin, not an open question), while the far end toward the terminals stays open
deliberately, since that's the "connect each terminal to the main duct" step still to come. When in doubt
about which end of a "draw first, connect later" duct to leave open, it's the end furthest from the known
source equipment, not the equipment end itself.

**Splitting an existing duct into two segments at a given point**:
`Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(document, ductElementId, pointOnCurve)` — splits
one `Duct` into two. Compute the break point by taking the FCU-side endpoint, the unit direction toward the
far endpoint, and offsetting by the gap distance: `fcuEnd + direction.Normalize() * gapFt`. Same pattern
should work for pipes via `Autodesk.Revit.DB.Plumbing.PlumbingUtils.BreakCurve` if that's ever needed.

**Correction — `BreakCurve` does NOT auto-connect the two resulting segments.** Originally assumed it did
("no coupling fitting needed, same size/system continues through") — wrong, confirmed by directly counting
connector `IsConnected` state after a split: 0 of 28 connectors (14 ducts × 2) were connected. This is what
was actually causing the recurring "duct/pipe has been modified to be in the opposite direction, causing
the connections to be invalid" error, not the FCU-connect step itself as first suspected — connecting one
end (e.g. to the FCU) while the split joint right next to it was silently unconnected left the system in an
inconsistent state that blew up on a later regenerate. **Always explicitly connect the split joint** right
after breaking: get both segments' connectors, find the one on each segment nearest the break point
(`OrderBy(c => c.Origin.DistanceTo(breakPoint)).First()`, or nearest to the other segment's known connector
origin), and call `.ConnectTo()` between them — the same pattern as connecting to any other equipment.
Verify by counting `IsConnected` across all the room's duct connectors before assuming a multi-segment duct
run is actually a single connected system.

## Branch duct from a terminal to a main duct — vertical riser + horizontal run + real fittings
Built for connecting each room's air terminals to its main duct. The user's routing rule: go
straight up from the terminal to the main duct's height first (vertical), THEN run horizontal over to tap
into the main duct — never a single diagonal duct straight from terminal to tap point.

- **`Connector.ConnectTo()` alone does NOT insert fitting geometry — it only makes a logical connection.**
  Using `.ConnectTo()` at the vertical-to-horizontal junction left `IsConnected == true` on both sides but
  produced **no elbow fitting at all** — the user caught this immediately ("for the elbow it's not coming").
  The physical fitting has to be created explicitly with its own dedicated method: use
  `Document.Create.NewElbowFitting(connector1, connector2)` for a 90° turn between two duct connectors —
  this both creates the elbow AND makes the connection, so don't call `.ConnectTo()` as well. Same family of
  methods: `NewTeeFitting`, `NewTransitionFitting`, `NewTakeoffFitting` (used for tapping into the main
  duct — that one already produced a real tee, since it's a dedicated creation call, unlike the plain
  connect used for the elbow joint).
  **Live-verified clarification (2026-07-17): calling `.ConnectTo()` first and THEN `NewElbowFitting()` on
  the same connector pair does not error or corrupt anything** — tested directly (two test ducts meeting at
  a corner, both calls made, committed, inspected, cleaned up): `NewElbowFitting` still trims back both
  duct ends and inserts a correctly-positioned, correctly-connected elbow regardless of the prior
  `ConnectTo`. So the rule above is "don't bother calling `ConnectTo` first, it's redundant" — not "calling
  both breaks the geometry."
- **Edge case: terminal already lines up under the main duct's line (near-zero horizontal offset).**
  Creating a horizontal segment of ~0 length throws "The points of startPoint and endPoint are too close:
  for MEPCurve, the minimum length is 1/10 inch." When the projected tap point is within a few mm of the
  vertical duct's top connector, skip the horizontal segment and elbow entirely — call
  `Document.Create.NewTakeoffFitting(verticalDuctTopConnector, mainDuctSegment)` directly on the vertical
  duct's own top connector.
- **Bug: re-querying "the main duct" mid-loop by category + room-containment alone also matches previously
  created branch ducts**, once a room has more than one branch already placed — `OrderBy(...Distance...)
  .First()` can then pick a branch segment instead of the actual trunk, causing bogus near-zero-length
  duct errors for later terminals in the same room. Fix: identify true main-duct segments by a precise
  geometric signature instead of just category+room — both curve endpoints must sit on the FCU's own fixed
  perpendicular-axis coordinate AND at the main duct's Z height (small tolerance, e.g. 20mm), which no
  branch duct (vertical or the short horizontal tap run) ever satisfies on both ends simultaneously.

**Capping an open duct end**: `Autodesk.Revit.DB.Plumbing.PlumbingUtils.PlaceCapOnOpenEnds(doc, elemId,
capTypeId)` **only accepts pipe curves/fittings/accessories** — passing a `Duct` id throws "The element
elemId is neither an object of pipe curve, pipe fitting, nor pipe accessory." There is **no**
`MechanicalUtils.PlaceCapOnOpenEnds` equivalent for ducts in this Revit version. Also,
`Document.Create.NewFamilyInstance(Connector, FamilySymbol)` — a hoped-for connector-based placement
overload — doesn't exist here either ("no overload takes 2 arguments").

**Correction (2026-07-08): `IsConnected == true` after a plain place-then-`ConnectTo` does NOT mean the cap
is actually correctly sized, positioned, or facing the right way.** Original approach (place a stock-size
cap instance at the open connector's origin, call `ConnectTo`) reported success and `IsConnected == true`,
but the user caught it as visibly wrong twice — first the size didn't match the duct (see the "Duct Width"/
"Duct Height" instance-parameter fix below, which is still correct but not sufficient alone), then even
after fixing size, undid the whole batch and pointed to a working reference script to study instead.
**`ConnectTo` only makes the logical MEP-system link — it does not verify or fix the cap's actual
geometric position/orientation.** The reliable recipe (translated from that working reference):
1. **Get the cap family/type from the duct type's own Routing Preferences**, not a hardcoded family name:
   `ductType.RoutingPreferenceManager.GetRule(RoutingPreferenceRuleGroupType.Caps, i).MEPPartId` → look up
   that `ElementId` as a `FamilySymbol`. This is generally more reliable than hardcoding a family/type
   name — different projects standardize on different cap families.
2. **Duplicate a precisely-sized TYPE** rather than relying only on instance-parameter overrides — name it
   something traceable like `AJ_AUTO_CAP_{width}x{height}mm`, check for an existing type with that exact
   name first (the user's own manual work in one room had already created one; reuse it rather than
   duplicating again) and set its Width/Height parameters by searching all its `Parameters` for names like
   `"WIDTH"`, `"DUCT WIDTH"`, `"NOMINAL WIDTH"`, `"W"` (case-insensitive, skip `IsReadOnly`/non-`Double`).
3. **Place with the 3-argument `NewFamilyInstance(XYZ, FamilySymbol, StructuralType)` overload** (no
   `Level` argument) — this DOES exist and is what the working script uses; the 4-arg `Level` overload used
   earlier in this project also works, but match the point-based placement pattern that's actually verified.
4. **Set the instance's own Width/Height parameters too** (same name-search as step 2, redundant safety),
   then **directly set the cap's own connector's `Width`/`Height` (or `Radius` for round)** —
   `capConnector.Width = ductConnector.Width` — this is the step that was missing before; an instance
   parameter by itself may not actually drive the connector's real geometry for every family.
5. **Re-fetch the cap's connector after every transform** (it can become a stale reference once the
   element moves/rotates/resizes) via nearest-by-origin-distance to the target duct connector, then
   **explicitly move** the cap so the connector's `Origin` exactly coincides with the duct's open
   connector's `Origin` (`ElementTransformUtils.MoveElement` by the difference vector) — resizing can shift
   a connector's position relative to the family's insertion point, so this has to happen after sizing, not
   just once at placement.
6. **Explicitly rotate the cap to face the correct mating direction** — a cap's connector must point
   *opposite* to the duct's open connector direction, not just be logically linked to it. Compute
   `angle = capConnector.CoordinateSystem.BasisZ.AngleTo(-ductConnector.CoordinateSystem.BasisZ)`; if
   `angle > ~0.0001` rad, rotate about the cross-product axis (`capDir.CrossProduct(targetDir)`, falling
   back to crossing with `XYZ.BasisX` then `XYZ.BasisY` if that cross product is ~zero) using
   `ElementTransformUtils.RotateElement(doc, capId, Line.CreateBound(ductConn.Origin, ductConn.Origin +
   axis), angle)`. Confirmed necessary in practice — rolling this out across 6 open ends in one project,
   3 needed a real rotation (90°, 90°, 180°), only 2 needed none.
7. **Move again after rotating** (rotation can shift the connector's position slightly), THEN finally call
   `capConnector.ConnectTo(ductConnector)`.

This same manual place-then-fix-then-connect pattern is the general fallback whenever a dedicated
`Place...` utility doesn't cover the MEP domain/category you're working with — but don't stop at "it
connected without erroring"; verify size, position, and facing direction explicitly, since none of those
are validated by a bare `ConnectTo` call.
- **`Duct.Create` may only have the XYZ-point overload on your Revit version** —
  `Duct.Create(doc, systemTypeId, ductTypeId, levelId, startPoint, endPoint)`. A connector-based overload
  (passing two `Connector` objects directly) does not exist on every version and can fail to compile
  ("cannot convert from Connector to XYZ") — check your version; if only the point overload exists, create
  with `connector.Origin` for both points instead.
- **Getting the system type / duct type element IDs**: `MechanicalSystemType.SystemClassification` is an
  `MEPSystemClassification` enum (not `DuctSystemType` — those are different enums, mixing them up is a
  compile error), e.g. filter `FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))` by
  `SystemClassification == MEPSystemClassification.SupplyAir`. Duct types: filter
  `OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))` by `FamilyName`/`Name` (e.g. `"Rectangular Duct"`
  / `"Radius Elbows / Taps"`).
- **Actually joining the new duct to existing equipment/terminal connectors**: after `Duct.Create`, the new
  duct has its own two end connectors via `duct.ConnectorManager.Connectors` — match each to the nearer
  target connector by `Origin.DistanceTo(...)`, then call `targetConnector.ConnectTo(ductEndConnector)` on
  **both** ends. This is what actually creates the physical join (and lets Revit insert a transition
  fitting automatically if the two connector sizes/shapes differ) — just creating the duct at the right
  points without this `ConnectTo` step leaves it geometrically coincident but not connected.
- Picking "the farthest terminal" (or any distance-based target) is a plain LINQ
  `OrderByDescending(fi => locA.DistanceTo(locB)).First()` — no special API needed.

## Connecting a new FCU to an already-existing open main-duct end (not drawing main duct fresh)
Different from the normal flow above: the main duct + all branches already existed (built by a past
session), only the FCU was outstanding. Placed the FCU first (the user, manually, in Revit), then connected
its supply connector straight into the pre-existing open trunk end with a plain `ConnectTo` (no new duct
segment needed since the FCU was positioned right at the open connector already) — same as connecting to
any other open connector, just confirm sizes match first.

**`Duct.LevelId` can silently go invalid (`-1`) on a trunk piece after it's been through a `BreakCurve` or
`NewTakeoffFitting` split, even though the element itself is still perfectly valid and physically
connected.** Don't feed a duct's own `LevelId` into a subsequent `Duct.Create` call for a new branch off
of it without checking — use the *terminal's* `LevelId` instead (confirmed reliable), or check
`!= ElementId.InvalidElementId` first and fall back.

**A naive single-hop connector trace (`AllRefs.First()`) gives a false "broken" result at a tee/takeoff
junction** — a trunk duct with a takeoff has 3 relevant connectors (upstream, downstream, branch), and
blindly taking the first one found in `AllRefs` can walk toward a dead end (e.g. the end cap) instead of
toward the FCU, reporting "broken" on a branch that's actually fully connected. Always use a proper BFS
that enqueues *every* connector in `AllRefs` (not just the first) when verifying a branch reaches its
FCU/equipment — this is what `verify-duct-connectivity.cs` already does; don't write a shortcut linear
walk for a one-off check, it will lie at any junction.

## Putting elements on a duct SYSTEM logically (no ductwork drawn) — and the `MEPSystem.Add` trap

"Connect these to a duct system" can mean the logical MEP system (the `System Name` / `System Type`
parameters and the System Browser entry), not physical ductwork — ask which, they're different jobs.

**`MEPSystem.Add(ConnectorSet)` does NOT add your element to that system when the system already holds a
physically-connected network. It re-homes the network into a BRAND-NEW system and leaves the original
system object holding only the element you just added.** Verified live 2026-08-05 (Revit 2020, Project1):
system `Mechanical Supply Air 1` (Id 918928) had 7 members (4 ducts + 3 elbows); calling `.Add()` with one
unconnected air terminal's connector left 918928 with **1** member (the terminal), and silently created
`Mechanical Supply Air 2` holding the original 7. **The added element reads back the expected system name,
so a read-back on that element alone looks like clean success** — the damage is on the elements you never
queried: the real trunk got moved onto a differently-named system, which quietly breaks any schedule,
filter or takeoff keyed on the system name. Always read back the *pre-existing* system's member count and
the other elements' `System Name` too, not just the element you touched. Fully reversible with native
Undo ([`undo.md`](undo.md)) — confirmed restoring all 7 members and deleting the phantom system.

**Root cause / the rule that follows**: a Revit MEP system is derived from physical connectivity, so an
unconnected element cannot join an existing physical network's system. To give unconnected elements a
system, create their own with **one atomic call** —
`Document.Create.NewMechanicalSystem(baseEquipmentConnector, connectorSet, DuctSystemType.SupplyAir)`,
which is exactly what Revit's own "Duct System" ribbon button does. Notes:
- `baseEquipmentConnector` accepts **`null`** when there's no FCU/AHU — verified, systems without base
  equipment are legal (the existing trunk system had `BaseEquipment == none` too).
- The enum here is `Autodesk.Revit.DB.Mechanical.DuctSystemType` (`SupplyAir`/`ReturnAir`/`ExhaustAir`/…),
  **not** the `MEPSystemClassification` used for `MechanicalSystemType.SystemClassification` — see the
  mixing-them-up compile-error note earlier in this file.
- Pass **all** the elements' connectors in that single `NewMechanicalSystem` call. Don't create with one
  and then `.Add()` the rest — that walks straight back into the trap above.
- Filter connectors by `c.Domain == Domain.DomainHvac` when gathering them.
- Verify with `system.GetFlow()` (convert from internal units): it should equal the sum of the members'
  Flow values — 9 terminals × 235 L/s read back as 2115.0 L/s, which is the cheap proof the right elements
  landed on the system.

## The user's connection method — drawing duct/pipe FROM any connector-bearing element (taught 2026-07-26)
The user's standing rule for connecting anything to equipment/terminals, live-proven twice on 2026-07-26
(40 stubs off 8 rotated/mirrored FCUs, then a full 6-terminal branched system). One-click version:
[`../../scripts/recipes/connect-equipment-to-air-terminals.cs`](../../scripts/recipes/connect-equipment-to-air-terminals.cs).
**Never draw blind — element → connectors → domain/size → direction → then draw:**
Steps 1-3 are packaged as one reusable read:
[`../../scripts/actions/reporting/action-report-connectors.cs`](../../scripts/actions/reporting/action-report-connectors.cs)
(2026-07-26) — run it on the element(s) first, then draw from what it reports.
1. **Check connectors exist** — `MEPModel.ConnectorManager` (null-check both). No connectors, no drawing.
2. **Check what kind** — `Connector.Domain` (`DomainHvac`=duct, `DomainPiping`=pipe, `DomainElectrical`=
   nothing drawable), plus `DuctSystemType`/`PipeSystemType`, `Shape`, size, `IsConnected`.
3. **Check the real direction** — `Connector.CoordinateSystem.BasisZ` + `Origin`. Never assume an axis:
   in the 8-FCU exercise the 4 mirrored units faced ±X while the others faced ±Y — an assumed axis would
   have misdrawn 20 of 40 curves.
4. **Then draw** from `Origin` along `BasisZ` at the connector's size.
5. **Main duct with branches: extend past the last branch, then cap.** Never end a main exactly at the
   last tap centerline — the tap has no duct body to seat on (the user caught this live with a
   screenshot). Extend ~500mm past the last branch, then close the end with a cap (below).

**The CONNECTOR overload of `Duct.Create`/`Pipe.Create` inherits size AND system type and auto-connects**
— `Duct.Create(doc, ductTypeId, levelId, connector, endXYZ)` / same shape for `Pipe.Create`. There is NO
`systemTypeId` argument in these overloads (passing one is a compile error on Revit 2020); system, size
and connection all come from the connector. This is the exception to the earlier note that `Duct.Create`
doesn't inherit size — that warning applies to the XYZ+XYZ overload only, which still needs explicit
`RBS_CURVE_WIDTH_PARAM`/`RBS_CURVE_HEIGHT_PARAM` sizing.

**Placing an end cap on an open duct end by script** (no direct cap API exists): find a duct-fitting
`FamilySymbol` whose Family `FAMILY_CONTENT_PART_TYPE` is `PartType.Cap` (e.g. `M_Rectangular Endcap`),
`Activate()` it, place with `NewFamilyInstance(openConn.Origin, sym, StructuralType.NonStructural)`,
rotate so the cap's connector `BasisZ` faces OPPOSITE the duct connector's (`AngleTo` + cross-product
axis; fall back to any perpendicular axis when antiparallel), set its `Duct Width`/`Duct Height` params
to the duct size, `MoveElement` so the connector origins coincide, then `openEnd.ConnectTo(capConn)`.
Clears the "open connector" warning on the duct. Live-proven 2026-07-26 (id 921372 in the session model).

## Terminals directly under the trunk — and the fragment that moves them instead

A corridor puts the diffusers on the trunk's own centreline: trunk overhead, necks directly below, no
sideways offset for a branch to use. That is a THIRD geometry, and it needs its own answer:

| Where the terminal sits | What to use |
|---|---|
| Offset to the SIDE of the trunk | `recipes/hvac-room-supply-ducting.cs` (branch + elbow + drop) |
| Directly UNDER the trunk | `recipes/connect-terminals-under-trunk.cs` (vertical drop + takeoff) |
| Already TOUCHING the duct | `actions/structural-changes/action-connect-air-terminals.cs` |

The room recipe **refuses** the middle case rather than fudging it — *"sits on the trunk centreline -
needs an inline tee, skipped"* — and that refusal is the signal to switch tools.

**`action-connect-air-terminals.cs` will move your diffuser.** Measured 2026-08-25: it reported
*"Connected 1 terminal(s). 0 refused"*, Revit returned true, and what it had actually done was **lift the
diffuser 625 mm** out of the ceiling (Z 2100) into the void (Z 2725) to meet the duct. No drop duct, no
tap fitting. When there is a vertical gap and nothing else to move, Revit satisfies the connection by
relocating the terminal. **So always re-read the terminal's Z after connecting it.** The air path can be
perfect while the diffuser is in the wrong place, and every text-level check passes.

The build that works, and never moves the terminal:

```csharp
var drop = Duct.Create(doc, ductTypeId, levelId, terminalConnector, pointAtTrunkZ);  // connector overload
Document.Create.NewTakeoffFitting(freeTopConnectorOfDrop, trunk);
```

**The drop comes back SHORTER than the gap and that is correct** — the takeoff shortens it to fit its own
body. 422 mm came back from a 621 mm gap. Never verify a drop by comparing its length to the gap asked
for; that check reports a false failure.

## Setting airflow on terminals — the unit trap

`Flow` on an air terminal is a writable instance parameter (`BuiltInParameter.RBS_DUCT_FLOW_PARAM`), but
it is **not a length**, so the library's usual bulk setter is wrong for it.
`actions/parameters-naming/action-set-parameter-value.cs` divides every number by 304.8 because it assumes
millimetres: asked for 200 L/s it would set **18 L/s**, wrong by 10.8x, and report success. Convert
properly instead:

```csharp
double v = UnitUtils.ConvertToInternalUnits(200.0, DisplayUnitType.DUT_LITERS_PER_SECOND);  // 7.06293 ft3/s
```

(On Revit 2021+ the same call takes `UnitTypeId.LitersPerSecond`.)

**Setting the terminals proves the system is really joined.** After 200 L/s went onto 122 diffusers, each
room FCU's supply connector read exactly **800 L/s** — 4 x 200 — computed by Revit from the network. A
connector that reports the sum of its terminals is stronger evidence of a correct system than any
geometry check, because Revit only sums what it can actually trace.

