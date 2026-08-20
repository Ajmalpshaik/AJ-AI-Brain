// ============================================================
// RECIPE — sprinkler-sidewall-layout.cs
// PURPOSE: Lay out SIDEWALL sprinkler heads along a room's walls — the corridor / small-room / no-void
//          case, where pipe cannot get above the ceiling. Spaces heads along the chosen wall against the
//          sidewall table (which is NOT the pendent table), and — the check a spacing-only tool never
//          makes — tests whether the room is even narrow enough to be reached from one side.
// STANDALONE — has its own sb and returns. Read-only; it produces positions, it does not place anything.
// SOURCE: ../../knowledge/fire-sprinkler/sprinkler-types.md    (the sidewall table and the gates on using one)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md      (the surrounding method)
//
// *** SIDEWALL IS NOT A PENDENT TURNED SIDEWAYS. *** It has its own protection-area and spacing table,
//   tighter than pendent, and it throws ONE WAY. Carrying the 15 ft standard-spray figure across is a
//   real error that produces a layout that looks generous and is not. Commonly published, and to be
//   confirmed against your edition:
//     light hazard    196 ft2 (18.2 m2) per head, 14 ft (4,267 mm) along the wall
//     ordinary hazard commonly 100 ft2 with 10 ft (3,048 mm)  [UNCONFIRMED — one source gave 130 ft2]
//   Plus the gates: light hazard needs a smooth ceiling (flat or sloped); ordinary hazard needs a smooth
//   FLAT ceiling AND a head specifically listed for ordinary-hazard use. That listing is a real gate.
//
// THE CHECK THAT MATTERS MOST AND IS ALWAYS SKIPPED: **throw across the room.** Spacing along the wall
//   says nothing about whether the far side is reached. A room wider than the head's listed throw cannot
//   be covered from one wall at any spacing — it needs heads on both walls, or a different head type.
//   Give roomThrowMm from the head's listing and this refuses to pretend otherwise.
//
// GOTCHA: this works on the room's straight boundary segments. A curved or heavily broken wall is
//         reported and skipped rather than approximated — an approximated sidewall run is worse than none.
// GOTCHA: the head projects INTO the room off the face of the wall. The positions below are offset by
//         wallOffsetMm so they sit in the room, not inside the wall; the deflector rules (102..152 mm off
//         the wall it is mounted on, 102..152 mm below the ceiling) are separate and reported, not applied.
// GOTCHA: heads on the SAME wall closer than the 6 ft minimum need a baffle. That is flagged, not solved.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. Room.GetBoundarySegments and the
//   curve maths are the same shapes already live-proven in recipes/generate-room-coverage-layout.cs
//   (2026-07-27). Run it on one corridor and check the first and last head against the wall ends by eye.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;
string hazardLabel = "";              // e.g. "Light Hazard" — printed on every line
double maxAreaPerHeadM2 = 0;          // SIDEWALL table: light 196 ft2 = 18.21 | ordinary commonly 100 ft2 = 9.29
double maxSpacingAlongWallMm = 0;     // SIDEWALL table: light 14 ft = 4267 | ordinary commonly 10 ft = 3048
double minSpacingMm = 1829;           // 6 ft — under this, heads on the same wall need a baffle
double minWallDistanceMm = 102;       // 4 in from the end wall
double roomThrowMm = 0;               // the head's LISTED throw across the room. 0 = the across-room check
                                      // is skipped and the report says the layout is unproven in that axis.
double wallOffsetMm = 150;            // how far into the room the head body sits off the wall face
int[] wallSegmentIndexes = new int[] { };   // which boundary segments to put heads on. Empty = list them and stop.
int maxListed = 100;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;
Func<double, double> toM2 = v => v / 10.763910416709722;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0) { sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to lay out."); }
else if (maxSpacingAlongWallMm <= 0 || maxAreaPerHeadM2 <= 0 || string.IsNullOrWhiteSpace(hazardLabel))
{
    sb.AppendLine("NOT RUN — maxSpacingAlongWallMm, maxAreaPerHeadM2 and hazardLabel are all required.");
    sb.AppendLine("  Take them from the SIDEWALL table, not the pendent one — knowledge/fire-sprinkler/sprinkler-types.md.");
    sb.AppendLine("  Light hazard: 196 ft2 = 18.21 m2, 14 ft = 4267 mm. Ordinary is tighter AND needs a listed head.");
}
else
{
    double zProbe = room.Level.Elevation + mm(1000);
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    var segs = new List<Curve>();
    try
    {
        var opt = new Autodesk.Revit.DB.SpatialElementBoundaryOptions();
        foreach (var loop in room.GetBoundarySegments(opt)) foreach (var s in loop) segs.Add(s.GetCurve());
    }
    catch (Exception ex) { sb.AppendLine($"Could not read the room boundary: {ex.Message}"); }

    sb.AppendLine($"SIDEWALL LAYOUT — '{room.Name}' (Id {roomIdInt}), {toM2(room.Area):F1} m2");
    sb.AppendLine($"HAZARD: {hazardLabel} | max {maxAreaPerHeadM2:F2} m2 per head | max {maxSpacingAlongWallMm:N0} mm along the wall");
    sb.AppendLine();

    if (segs.Count == 0) { sb.AppendLine("No boundary segments — check the room is really bounded."); }
    else if (wallSegmentIndexes.Length == 0)
    {
        sb.AppendLine("WHICH WALL? Pick from these boundary segments and put their indexes in wallSegmentIndexes:");
        for (int i = 0; i < segs.Count; i++)
        {
            var c = segs[i];
            bool straight = c is Line;
            var a = c.GetEndPoint(0); var b = c.GetEndPoint(1);
            sb.AppendLine($"  [{i}] {(straight ? "straight" : "CURVED — cannot be used")} {toMm(c.Length):N0} mm"
                + $"  from {toMm(a.X):N0},{toMm(a.Y):N0}  to {toMm(b.X):N0},{toMm(b.Y):N0} mm");
        }
        sb.AppendLine();
        sb.AppendLine("  Sidewall heads usually go on ONE long wall of a corridor, or on both where the room is too");
        sb.AppendLine("  wide for one throw. Nothing has been laid out — pick the segments and run it again.");
    }
    else
    {
        int totalHeads = 0, baffleFlags = 0;
        var allPoints = new List<XYZ>();

        foreach (var idx in wallSegmentIndexes)
        {
            if (idx < 0 || idx >= segs.Count) { sb.AppendLine($"  segment [{idx}] does not exist — skipped."); continue; }
            var c = segs[idx];
            var line = c as Line;
            if (line == null)
            {
                sb.AppendLine($"  segment [{idx}] is CURVED — skipped. A sidewall run along a curve needs to be set out by hand;");
                sb.AppendLine("     an approximated one is worse than none.");
                continue;
            }

            double lenMm = toMm(line.Length);
            var dir = line.Direction;
            // inward normal: rotate the direction 90 degrees and pick the side that lands inside the room
            var nA = new XYZ(-dir.Y, dir.X, 0).Normalize();
            var mid = line.Evaluate(0.5, true);
            var testA = new XYZ(mid.X + nA.X * mm(300), mid.Y + nA.Y * mm(300), 0);
            var inward = insideRoom(testA) ? nA : new XYZ(dir.Y, -dir.X, 0).Normalize();

            // how many heads fit under the spacing cap, with half-spacing off each end
            int n = (int)Math.Ceiling(lenMm / maxSpacingAlongWallMm);
            if (n < 1) n = 1;
            double spacing = lenMm / n;
            double endInset = spacing / 2.0;

            sb.AppendLine($"  WALL [{idx}] {lenMm:N0} mm -> {n} head(s), spacing {spacing:N0} mm, {endInset:N0} mm off each end");
            if (endInset < minWallDistanceMm)
                sb.AppendLine($"     *** the end heads sit {endInset:N0} mm off the end wall, under the {minWallDistanceMm:N0} mm minimum.");
            if (n > 1 && spacing < minSpacingMm)
            {
                baffleFlags++;
                sb.AppendLine($"     *** {spacing:N0} mm apart is under the {minSpacingMm:N0} mm minimum — heads on the SAME wall this");
                sb.AppendLine("         close need a BAFFLE between them (solid, about 200 mm long x 150 mm high, top 50-75 mm above");
                sb.AppendLine("         the deflectors). Flagged, not solved — decide it deliberately.");
            }

            for (int k = 0; k < n; k++)
            {
                double t = (endInset + k * spacing) / lenMm;
                var onWall = line.Evaluate(Math.Min(Math.Max(t, 0), 1), true);
                var pt = new XYZ(onWall.X + inward.X * mm(wallOffsetMm), onWall.Y + inward.Y * mm(wallOffsetMm), 0);
                allPoints.Add(pt);
                totalHeads++;
                if (totalHeads <= maxListed)
                    sb.AppendLine($"     head {totalHeads,3}: {toMm(pt.X):N0}, {toMm(pt.Y):N0} mm"
                        + (insideRoom(pt) ? "" : "   *** OUTSIDE THE ROOM — check wallOffsetMm and the inward direction"));
            }
        }

        sb.AppendLine();
        if (totalHeads == 0) sb.AppendLine("  No heads laid out.");
        else
        {
            double areaEach = toM2(room.Area) / totalHeads;
            int minByArea = (int)Math.Ceiling(toM2(room.Area) / maxAreaPerHeadM2);
            sb.AppendLine("CHECK TABLE — limit | measured | verdict");
            sb.AppendLine($"  max area per head  {maxAreaPerHeadM2,8:F2} m2 | {areaEach,8:F2} m2 | {(areaEach <= maxAreaPerHeadM2 ? "PASS" : "FAIL")}");
            sb.AppendLine($"  head count floor   {minByArea,8:N0}    | {totalHeads,8:N0}    | {(totalHeads >= minByArea ? "PASS" : "FAIL")}");
            sb.AppendLine("    (area per head here is room area / head count — with sidewall heads on one wall of an");
            sb.AppendLine("     irregular room that average can flatter the layout. Check the served strip by hand.)");

            // --- the across-room check: can the far side even be reached ---
            if (roomThrowMm <= 0)
            {
                sb.AppendLine();
                sb.AppendLine("  *** ACROSS-ROOM THROW NOT CHECKED — roomThrowMm is 0.");
                sb.AppendLine("      Spacing along the wall says NOTHING about whether the far side of the room is reached.");
                sb.AppendLine("      Get the listed throw off the head's data sheet and re-run, or this layout is unproven");
                sb.AppendLine("      in the axis that matters most.");
            }
            else
            {
                double worst = 0; XYZ worstPt = null;
                var rbb = room.get_BoundingBox(null);
                for (double x = rbb.Min.X; x <= rbb.Max.X; x += mm(300))
                    for (double y = rbb.Min.Y; y <= rbb.Max.Y; y += mm(300))
                    {
                        var p = new XYZ(x, y, 0);
                        if (!insideRoom(p)) continue;
                        double best = double.MaxValue;
                        foreach (var h in allPoints)
                        { double dx = p.X - h.X, dy = p.Y - h.Y; double d = Math.Sqrt(dx * dx + dy * dy); if (d < best) best = d; }
                        if (best > worst) { worst = best; worstPt = p; }
                    }
                double worstMm = toMm(worst);
                sb.AppendLine($"  across-room throw  {roomThrowMm,8:N0} mm | {worstMm,8:N0} mm | {(worstMm <= roomThrowMm ? "PASS" : "FAIL")}");
                if (worstPt != null && worstMm > roomThrowMm)
                {
                    sb.AppendLine($"    the worst point is at {toMm(worstPt.X):N0}, {toMm(worstPt.Y):N0} mm — {worstMm - roomThrowMm:N0} mm beyond the listed throw.");
                    sb.AppendLine("    This room CANNOT be covered from these walls at any spacing. Put heads on the opposite wall");
                    sb.AppendLine("    too, use a longer-throw listed head, or go back to pendent. Do not thin the spacing to hide it.");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"  {totalHeads} head position(s) produced" + (baffleFlags > 0 ? $", {baffleFlags} wall(s) flagged for baffles." : "."));
            sb.AppendLine("  Deflector rules for these, applied separately: 102..152 mm below the ceiling, and no more than");
            sb.AppendLine("  152 mm out from the wall face they are mounted on. See knowledge/fire-sprinkler/deflector-and-ceiling-height.md.");
            sb.AppendLine("  Nothing here is a compliance statement — the AHJ (QCDD) and the project spec sit on top of NFPA.");
        }
    }
}

return sb.ToString();
