// ============================================================
// FRAGMENT (action) — action-dimension-rooms.cs
// PURPOSE: Put real Revit dimensions on each ROOM in `elements` — its overall width and depth, measured
//          wall face to wall face, drawn just outside the room. "Dimension all the rooms", "put the
//          room sizes on the plan". Produces live Dimension elements that update with the model, not
//          text.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter fragment above — use
//          filters/by-identity/filter-by-category.cs with OST_Rooms. Anything that is not a placed Room
//          is ignored and counted.
//          FOR ONE NAMED ROOM, use filters/by-identity/filter-by-id-list.cs instead with that room's Id
//          — "dimension room 4" is a single-room job, and by-category would dimension every room in the
//          model. Verified as a composition 2026-08-23; see the second live-verification line below.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✓ LIVE-VERIFIED 2026-08-22 on `school.rvt`, Revit 2020.2.9, 3 rooms in a floor plan. 6 dimensions
//   created, 0 failed, and **every one was cross-checked against the room's own boundary extents in the
//   same run**: 14900/15900, 7732/11171, 22000/8500 mm — all six MATCH to under 1 mm. Confirmed by eye
//   on an exported PNG. Only rectangular rooms were tested; see the L-shape gotcha below.
// ✓ LIVE-VERIFIED AGAIN 2026-08-23 on `school.rvt`, Revit 2020.2.9 — this time via `run_fragment`
//   composed with `filter-by-id-list` to hit ONE room, in a 1:50 view at 500 mm offset. Room 4
//   (8800 x 8900 mm clear, 200 mm walls): 2 created, 0 failed, both MATCH. Read back independently
//   afterwards — `Dimension.References` pointed at exactly the four walls the boundary walk had named,
//   and the values agreed. That read-back is the check worth repeating: the fragment's own MATCH line
//   and an outside query are two different mechanisms, and only both agreeing proves it.
// ✓ ALL THREE `measureTo` MODES LIVE-VERIFIED 2026-08-23, Room 5, same model and view. Each number was
//   predicted from the room extent plus the walls' real thickness BEFORE it was created, and each came
//   back MATCH: inside 8800/8900 · outside 9200/9300 · centre 9000 (probed in a rolled-back
//   transaction). `lineInsideRoom` proven both ways in the same pass and confirmed by reading each
//   dimension's bounding box back against the room extent — the interior pair sits at Y 4821 / X -19981,
//   both strictly inside the room; the exterior pair at Y 3691 / X -21028, both outside it.
//
// ⚠⚠ THE TRAP: "INTERIOR" IS NOT THE FACE THAT BOUNDS THE ROOM. `HostObjectUtils.GetSideFaces` takes a
//    `ShellLayerType` of Interior or Exterior, and it is tempting to assume Interior means "the side
//    facing into the room". It does not — it means the side of the wall's own layer structure, so which
//    one bounds the room follows THE DIRECTION THE WALL WAS DRAWN. Measured on this model in one pass:
//    Rooms 1 and 2 were bounded by the **Interior** faces, Room 3 by the **Exterior** faces, same wall
//    type throughout. Hard-coding either name dimensions to the wrong side of the wall and quietly
//    reports a number 200 mm out — it will not throw. **So pick the face by DISTANCE**: project the
//    boundary segment's midpoint onto each candidate face and keep the nearest. That is what
//    `roomFacingRef` below does, and it is the only part of this fragment that must not be simplified.
//    (Same shape of trap as DatumEnds.End0 not being GetEndPoint(0) — see knowledge/live-model/datums.md.)
//
// GOTCHA: DIMENSIONS ARE MEASURED TO THE **FINISH** FACE because `SpatialElementBoundaryLocation.Finish`
//         is used for the boundary. That is the clear internal room size a client expects. Switch to
//         `Center` if you want structural centreline dimensions — but then the numbers stop matching
//         the room's Area, and someone will eventually notice.
// GOTCHA: L-SHAPED AND U-SHAPED ROOMS GET A BOUNDING-BOX ANSWER, NOT A ROOM SHAPE. This takes the
//         outermost wall on each side of the outer loop, so an L-shaped room is dimensioned as if it
//         were the rectangle around it — which is wrong, and wrong silently. **Untested on such a room**
//         (all three verification rooms were rectangles). For those, dimension each leg separately.
// GOTCHA: only the OUTER loop (`loops[0]`) is read. A room with a column or a core inside it has inner
//         loops that are ignored — correct for an overall size, wrong if you wanted the clear span past
//         the obstruction.
// GOTCHA: a boundary segment can be bounded by a ROOM SEPARATION LINE, not a wall — `seg.ElementId`
//         then resolves to something that is not a Wall, and `HostObjectUtils` cannot give it a face.
//         Those segments are skipped, so a room enclosed only by separation lines produces nothing and
//         says so, rather than throwing.
// GOTCHA: the dimension line must lie in the VIEW's plane. Its Z is taken from the view's own level
//         (`ViewPlan.GenLevel.Elevation`), not from the room, or the dimension lands off-plane and the
//         create call fails.
// GOTCHA: `offsetMm` is a per-request drafting number — ask, do not reuse. **Scale it with the view.**
//         The paper distance is what the eye judges, so the model number must follow the view's scale:
//         1000 mm reads well at 1:100 (10 mm on paper), and the SAME look at 1:50 needs 500 mm. Read
//         `view.Scale` and divide, rather than carrying one number between jobs. On a tight plan the
//         string collides with the next room and needs more.
// NOTE: `Dimension.Value` is a nullable double in FEET. Multiply by 304.8 for mm, and `?? 0` it —
//       a multi-segment dimension returns null there, which is not an error.
//
// ---- THE THREE THINGS A ROOM DIMENSION CAN MEASURE (`measureTo`) ----
// Ajmal's own words, 2026-08-23: *"see some time i need from outside sometime i need inside and also
// from the wall side also from wall mid also i need."* Three different numbers off the same room, and
// all three are measured — on `school.rvt` Room 4, clear 8800 mm between 200 mm walls:
//
//   "inside"  -> 8800  the NEAREST wall face each side. Clear internal room size; matches the room's
//                      Area, and it is what a client means by "how big is the room".
//   "outside" -> 9200  the FARTHEST wall face each side. Overall external size, wall outer face to
//                      outer face — what you check a building envelope or a plot line against.
//   "centre"  -> 9000  `new Reference(wall)` on each side. Wall centreline to centreline — the
//                      setting-out size that agrees with the grid.
//
// **`new Reference(wall)` giving the CENTRELINE is the non-obvious one, and it contradicts the obvious
// reading of knowledge/live-model/dimensioning.md's reference table.** That table says `new
// Reference(element)` is "datums only" and throws for a pipe or duct — true, but it does NOT throw for
// a wall: it resolves to the wall's location line and dimensions to it. Measured here, not assumed.
//
// ⚠ THE RISK IN "centre", AND HOW IT IS CAUGHT: `new Reference(wall)` follows the wall's LOCATION LINE,
//   which is only the geometric middle when the wall type's Location Line is set to "Wall Centerline".
//   Set it to "Finish Face: Exterior" and the "centreline" dimension quietly measures to a face instead.
//   **Every verification wall here was Location Line = Wall Centerline, so the offset case is UNTESTED.**
//   It is not silent, though: the expected value is computed independently from the room boundary plus
//   half each wall's real `Width`, so a wall with an offset location line prints `OFF BY <n> mm` rather
//   than a wrong number that looks right. If you see that, check the wall type's Location Line first.
//
// NOT BUILT — CORE FACES. Revit also dimensions to the structural CORE of a multi-layer wall, and this
// fragment does not: `HostObjectUtils.GetSideFaces` only offers the Interior/Exterior shell layers, so
// core faces need a compound-structure walk. `school.rvt` has only single-layer `Generic - 200mm` walls,
// where core and finish are the same faces, so there is nothing here to prove it against — and shipping
// an unprovable fourth mode is exactly the habit this library exists to avoid. Build it the day a
// multi-layer wall is on screen to check it with.
//
// GOTCHA: `lineInsideRoom` moves the STRING, not the measurement. The two are independent — an
//         "outside" measurement with the string drawn inside the room is legal and sometimes what a
//         crowded plan needs. Do not confuse "measure from outside" with "draw it outside".
// NOTE: the offset gap is measured from what is BEING dimensioned, not from the room boundary, so
//       "outside" mode pushes the line out by the wall thickness as well and the visible gap stays the
//       offsetMm you asked for. Without that, an outside dimension at 500 mm offset on a 200 mm wall
//       leaves only a 300 mm gap and looks wrong next to an inside one at the same setting.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = report the sizes it WOULD dimension, create nothing
double offsetMm = 1000;          // ASK. gap between what is dimensioned and the line. Scale it with the
                                 // view: 1000 at 1:100, 500 at 1:50 — same 10 mm on paper
string measureTo = "inside";     // ASK. "inside" = clear room, nearest wall faces
                                 //      "outside" = overall external, farthest wall faces
                                 //      "centre" = wall centreline to centreline (setting-out size)
bool lineInsideRoom = false;     // false = string drawn OUTSIDE the room (normal)
                                 // true  = string drawn INSIDE the room. Independent of measureTo
bool doWidth = true;             // the X dimension, drawn below the room
bool doDepth = true;             // the Y dimension, drawn to the left of the room
int? targetViewIdInt = null;     // null = active view. Must be a plan view
// ---- END INPUTS ----

const double MM_PER_FT = 304.8;
double dimOffset = offsetMm / MM_PER_FT;

string modeKey = (measureTo ?? "inside").Trim().ToLowerInvariant();
if (modeKey == "center") modeKey = "centre";              // spell it either way
if (modeKey == "face" || modeKey == "clear") modeKey = "inside";
if (modeKey == "external" || modeKey == "overall") modeKey = "outside";
bool wantCentre  = modeKey == "centre";
bool wantOutside = modeKey == "outside";
bool modeKnown   = wantCentre || wantOutside || modeKey == "inside";

View dimView = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (dimView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view. Nothing done.");
}
else if (!modeKnown)
{
    sb.AppendLine($"measureTo = '{measureTo}' is not one of: inside / outside / centre. Nothing done — " +
                  "guessing which face was meant is the exact mistake this fragment exists to prevent.");
}
else
{
    var planView = dimView as ViewPlan;
    double planZ = (planView != null && planView.GenLevel != null) ? planView.GenLevel.Elevation : 0;
    if (planView == null)
        sb.AppendLine($"WARNING — '{dimView.Name}' is a {dimView.ViewType}, not a plan. Dimensions may refuse to place.");

    var rooms = elements.OfType<Autodesk.Revit.DB.Architecture.Room>()
        .Where(r => { try { return r.Area > 1e-6; } catch { return false; } }).ToList();
    int notARoom = elements.Count - elements.OfType<Autodesk.Revit.DB.Architecture.Room>().Count();
    int unplaced = elements.OfType<Autodesk.Revit.DB.Architecture.Room>().Count() - rooms.Count;

    // THE face-picking rule this fragment exists for — pick by DISTANCE, never by a named ShellLayerType.
    // NEAREST face = the one bounding the room ("inside"); FARTHEST = the outer face ("outside").
    Func<Wall, Curve, Reference> roomFacingRef = (wall, seg) =>
    {
        if (wantCentre)                      // the wall's own location line, not a face at all
        { try { return new Reference(wall); } catch { return null; } }

        XYZ mid; try { mid = seg.Evaluate(0.5, true); } catch { return null; }
        Reference best = null; double bestD = wantOutside ? -1.0 : double.MaxValue;
        foreach (var st in new[] { ShellLayerType.Exterior, ShellLayerType.Interior })
        {
            IList<Reference> refs = null;
            try { refs = HostObjectUtils.GetSideFaces(wall, st); } catch { continue; }
            if (refs == null) continue;
            foreach (var r in refs)
            {
                Face f = null;
                try { f = wall.GetGeometryObjectFromReference(r) as Face; } catch { }
                if (f == null) continue;
                try
                {
                    var pr = f.Project(mid); if (pr == null) continue;
                    if (wantOutside ? pr.Distance > bestD : pr.Distance < bestD) { bestD = pr.Distance; best = r; }
                }
                catch { }
            }
        }
        return best;
    };

    // How far PAST the room boundary the measured line sits, on one side, in FEET. Reads each wall's own
    // real thickness, so opposite walls of different types still produce an honest expected value — and
    // it is what makes the MATCH / OFF BY check meaningful in outside and centre mode.
    Func<Wall, double> beyond = w =>
    {
        if (w == null || !(wantCentre || wantOutside)) return 0;
        try { return wantCentre ? w.Width / 2.0 : w.Width; } catch { return 0; }
    };

    var report = new List<string>();
    int made = 0, failed = 0, noRef = 0;

    Action buildAll = () =>
    {
        foreach (var room in rooms)
        {
            var bopts = new SpatialElementBoundaryOptions
            { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
            IList<IList<BoundarySegment>> loops = null;
            try { loops = room.GetBoundarySegments(bopts); } catch { }
            if (loops == null || loops.Count == 0)
            { report.Add($"{room.Name} | - | not enclosed — skipped"); failed++; continue; }

            var outer = loops[0];
            var pts = outer.SelectMany(s => new[] { s.GetCurve().GetEndPoint(0), s.GetCurve().GetEndPoint(1) }).ToList();
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);

            var segs = outer.Select(s =>
            {
                var c = s.GetCurve(); var a = c.GetEndPoint(0); var b = c.GetEndPoint(1);
                return new
                {
                    C = c,
                    W = Document.GetElement(s.ElementId) as Wall,   // null for a room separation line
                    AlongX = Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y),
                    MidX = (a.X + b.X) / 2,
                    MidY = (a.Y + b.Y) / 2
                };
            }).Where(s => s.W != null).ToList();

            // Both lists are needed by BOTH blocks: the width dimension is drawn past the bottom wall,
            // so it needs `horiz` to know how thick that wall is, and the depth dimension needs `vert`.
            var vert  = segs.Where(s => !s.AlongX).OrderBy(s => s.MidX).ToList();   // bound the room in X
            var horiz = segs.Where(s =>  s.AlongX).OrderBy(s => s.MidY).ToList();   // bound the room in Y

            // WIDTH: across the two walls that run along Y (they are what bound the room in X)
            if (doWidth)
            {
                double expect = ((maxX - minX)
                    + (vert.Count >= 2 ? beyond(vert.First().W) + beyond(vert.Last().W) : 0)) * MM_PER_FT;
                if (vert.Count < 2) { report.Add($"{room.Name} | width | fewer than 2 wall-bounded sides — skipped"); noRef++; }
                else
                {
                    var rL = roomFacingRef(vert.First().W, vert.First().C);
                    var rR = roomFacingRef(vert.Last().W, vert.Last().C);
                    if (rL == null || rR == null) { report.Add($"{room.Name} | width | no usable wall face reference"); noRef++; }
                    else if (dryRun) report.Add($"{room.Name} | width | would dimension {expect:F0} mm");
                    else
                    {
                        var ra = new ReferenceArray(); ra.Append(rL); ra.Append(rR);
                        // gap is measured from what is BEING dimensioned, so an outside dimension clears
                        // the wall it just measured to instead of eating the offset
                        double yLine = lineInsideRoom
                            ? minY + dimOffset
                            : minY - (horiz.Count > 0 ? beyond(horiz.First().W) : 0) - dimOffset;
                        var line = Line.CreateBound(new XYZ(minX, yLine, planZ), new XYZ(maxX, yLine, planZ));
                        try
                        {
                            var d = Document.Create.NewDimension(dimView, line, ra);
                            double got = (d.Value ?? 0) * MM_PER_FT;
                            report.Add($"{room.Name} | width | {got:F0} mm | expected {expect:F0} | " +
                                       (Math.Abs(got - expect) < 1 ? "MATCH" : $"OFF BY {got - expect:F0} mm — check the face side"));
                            made++;
                        }
                        catch (Exception ex) { report.Add($"{room.Name} | width | FAILED — {ex.Message.Split('\n')[0]}"); failed++; }
                    }
                }
            }

            // DEPTH: across the two walls that run along X
            if (doDepth)
            {
                double expect = ((maxY - minY)
                    + (horiz.Count >= 2 ? beyond(horiz.First().W) + beyond(horiz.Last().W) : 0)) * MM_PER_FT;
                if (horiz.Count < 2) { report.Add($"{room.Name} | depth | fewer than 2 wall-bounded sides — skipped"); noRef++; }
                else
                {
                    var rB = roomFacingRef(horiz.First().W, horiz.First().C);
                    var rT = roomFacingRef(horiz.Last().W, horiz.Last().C);
                    if (rB == null || rT == null) { report.Add($"{room.Name} | depth | no usable wall face reference"); noRef++; }
                    else if (dryRun) report.Add($"{room.Name} | depth | would dimension {expect:F0} mm");
                    else
                    {
                        var ra = new ReferenceArray(); ra.Append(rB); ra.Append(rT);
                        double xLine = lineInsideRoom
                            ? minX + dimOffset
                            : minX - (vert.Count > 0 ? beyond(vert.First().W) : 0) - dimOffset;
                        var line = Line.CreateBound(new XYZ(xLine, minY, planZ), new XYZ(xLine, maxY, planZ));
                        try
                        {
                            var d = Document.Create.NewDimension(dimView, line, ra);
                            double got = (d.Value ?? 0) * MM_PER_FT;
                            report.Add($"{room.Name} | depth | {got:F0} mm | expected {expect:F0} | " +
                                       (Math.Abs(got - expect) < 1 ? "MATCH" : $"OFF BY {got - expect:F0} mm — check the face side"));
                            made++;
                        }
                        catch (Exception ex) { report.Add($"{room.Name} | depth | FAILED — {ex.Message.Split('\n')[0]}"); failed++; }
                    }
                }
            }
        }
    };

    if (dryRun) buildAll();
    else
    {
        using (var tDim = new Transaction(Document, "AJ Tools - Dimension rooms"))
        {
            tDim.Start();
            try { buildAll(); tDim.Commit(); }
            catch (Exception ex) { tDim.RollBack(); sb.AppendLine("FAILED — rolled back, nothing created. " + ex.Message); }
        }
    }

    string modeWords = wantCentre ? "CENTRE — wall centreline to centreline"
                     : wantOutside ? "OUTSIDE — overall external, outer wall face to outer wall face"
                                   : "INSIDE — clear room, wall face to wall face";
    sb.AppendLine($"Measuring: {modeWords}. String drawn {(lineInsideRoom ? "INSIDE" : "outside")} the room.");
    sb.AppendLine(dryRun
        ? $"DRY RUN — nothing created. {rooms.Count} room(s) would be dimensioned."
        : $"Room dimensions: created {made}, failed {failed}" + (noRef > 0 ? $", no reference {noRef}" : "") +
          $", in view '{dimView.Name}' at {offsetMm:F0} mm offset.");
    if (notARoom > 0) sb.AppendLine($"  ({notARoom} element(s) were not Rooms — ignored.)");
    if (unplaced > 0) sb.AppendLine($"  ({unplaced} unplaced room(s) with no area — ignored.)");
    if (report.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("Room | What | Result");
        sb.AppendLine("--- | --- | ---");
        foreach (var r in report) sb.AppendLine(r);
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
