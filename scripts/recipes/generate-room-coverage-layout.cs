// ============================================================
// RECIPE — generate-room-coverage-layout.cs
// PURPOSE: Work out the FEWEST equal circles of a given radius that completely cover a room's floor, and
//          draw them. The sprinkler-layout question ("how many heads at 3 m coverage, and where"), and
//          equally the smoke-detector, CCTV, WiFi or lighting version — anything where a fixed-radius
//          device must leave no gap. Also verifies the answer instead of asserting it.
// STANDALONE — has its own sb and returns; not filter+action shaped (sample -> lattice -> reduce -> verify).
//
// THE ALGORITHM, and why each step is there:
//   1. SAMPLE the room floor on a grid (sampleMm) using Room.IsPointInRoom — this is the ground truth of
//      "what must be covered", and it handles L-shapes and any irregular plan for free. A bounding box
//      would lie; the room's real shape is what matters.
//   2. LATTICE — lay out candidate centres on a HEXAGONAL (triangular) grid with spacing r*sqrt(3) and
//      rows 1.5r apart. That is the provably most efficient covering of a plane by equal circles
//      (Kershner 1939). A square grid needs spacing r*sqrt(2), which is ~30% MORE circles for the same
//      job — the fragment reports both so the trade-off is visible.
//   3. REDUCE — greedy set-cover: repeatedly take the candidate that newly covers the most uncovered
//      sample points, stop when everything is covered. This trims candidates that only cover floor
//      already served, and candidates that fall outside the room entirely.
//   4. VERIFY — count uncovered sample points and REPORT the number. Never claim full coverage without it.
//
// GOTCHA: **gridType matters for buildability, not just count.** Hexagonal gives the fewest devices, but
//         sprinkler and lighting branch pipes/conduits run in straight lines, so a real issued layout is
//         usually SQUARE even though it needs more heads. Ask which the user wants; do not assume fewest.
// GOTCHA: **covering is not compliance.** Full geometric coverage says nothing about the MAXIMUM SPACING
//         codes also impose (NFPA 13 / BS EN 12845 cap head spacing as well as area per head). A layout
//         can cover every square metre and still fail. Measured live 2026-07-26: r=3000 gave 20 heads with
//         zero gaps but 5,196 mm spacing, over the common 4,600 mm cap. Report spacing alongside coverage
//         and let the user check it against their hazard class.
// GOTCHA: sampleMm drives both accuracy and cost — points scale with 1/sampleMm^2. 200 mm over a 288 m2
//         room is ~7,300 points and runs in a few seconds; 50 mm would be ~115,000 and is not worth it.
//         Use ~r/10, never finer than needed to trust the gap count.
// GOTCHA: the circle count grows with 1/r^2. r=500 needed 466 circles on this room; r=3000 needed 20.
//         If a number comes back in the hundreds, the radius is probably wrong for the job — say so.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 'Room 4' (287.7 m2), both scales:
//     r=500 mm  -> 466 circles, 7,280/7,280 sample points covered, 0 gaps (5% above the 443 area-ideal)
//     r=3000 mm -> 20 circles,  3,243/3,243 covered, 0 gaps, 14.4 m2 per head, 105 ms
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;                  // the room to cover (must be PLACED — Area > 0)
double radiusMm = 3000;             // device coverage radius
string gridType = "hexagonal";      // "hexagonal" (fewest devices) | "square" (straight pipe runs)
double sampleMm = 300;              // floor test spacing; ~r/10 is plenty
bool drawCircles = true;            // draw the result
int drawInViewIdInt = 0;            // the PLAN view to draw into (detail circles); 0 = don't draw
int[] circleColorRgb = new[]{255,0,0};
bool groupDrawn = true;
string drawnGroupName = "";         // e.g. "MEP_Room4_Sprinkler_R3000"
double maxAllowedSpacingMm = 0;     // optional code check, e.g. 4600 — 0 = don't check
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double,double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double,double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double,double> toM2 = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_SQUARE_METERS);

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0)
{
    sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — it encloses nothing, so there is nothing to cover.");
    sb.AppendLine("  Place it first: creators/create-rooms-in-enclosed-regions.cs.");
}
else
{
    double r = mm(radiusMm);
    var bb = room.get_BoundingBox(null);
    double zProbe = room.Level.Elevation + mm(1000);   // probe at room height, not device height

    // --- 1. sample the real room shape ---
    var samples = new List<XYZ>();
    for (double x = bb.Min.X; x <= bb.Max.X; x += mm(sampleMm))
        for (double y = bb.Min.Y; y <= bb.Max.Y; y += mm(sampleMm))
        {
            var p = new XYZ(x, y, zProbe);
            bool inside = false; try { inside = room.IsPointInRoom(p); } catch { }
            if (inside) samples.Add(new XYZ(x, y, 0));
        }
    sb.AppendLine($"'{room.Name}' {toM2(room.Area):F1} m2 — {samples.Count:N0} floor test points at {sampleMm} mm.");
    if (samples.Count == 0) { sb.AppendLine("  No point tested inside the room — check the room is really bounded."); }
    else
    {
        // --- 2. candidate lattice ---
        double dx, dy;
        if (gridType == "square") { dx = r * Math.Sqrt(2.0); dy = dx; }   // square covering
        else { dx = r * Math.Sqrt(3.0); dy = 1.5 * r; }                    // hexagonal covering
        var lattice = new List<XYZ>();
        int row = 0;
        for (double y = bb.Min.Y - r; y <= bb.Max.Y + r; y += dy, row++)
        {
            double xOff = (gridType == "square" || row % 2 == 0) ? 0 : dx / 2.0;
            for (double x = bb.Min.X - r + xOff; x <= bb.Max.X + r; x += dx)
                lattice.Add(new XYZ(x, y, 0));
        }

        // --- 3. greedy reduce to the smallest covering subset ---
        double r2 = r * r;
        var covers = new List<List<int>>();
        foreach (var c in lattice)
        {
            var lst = new List<int>();
            for (int s = 0; s < samples.Count; s++)
            {
                double ax = samples[s].X - c.X, ay = samples[s].Y - c.Y;
                if (ax * ax + ay * ay <= r2) lst.Add(s);
            }
            covers.Add(lst);
        }
        var pool = Enumerable.Range(0, lattice.Count).Where(i => covers[i].Count > 0).ToList();
        var covered = new bool[samples.Count];
        var chosen = new List<int>();
        while (true)
        {
            int best = -1, bestGain = 0;
            foreach (var i in pool)
            {
                int gain = 0;
                foreach (var s in covers[i]) if (!covered[s]) gain++;
                if (gain > bestGain) { bestGain = gain; best = i; }
            }
            if (best < 0 || bestGain == 0) break;
            chosen.Add(best);
            foreach (var s in covers[best]) covered[s] = true;
            pool.Remove(best);
        }

        // --- 4. verify, never assert ---
        int gaps = covered.Count(c => !c);
        double perDevice = toM2(room.Area) / Math.Max(1, chosen.Count);
        sb.AppendLine($"{gridType.ToUpper()} covering at {radiusMm:F0} mm radius:");
        sb.AppendLine($"  DEVICES NEEDED: {chosen.Count:N0}");
        sb.AppendLine($"  VERIFIED: {samples.Count - gaps:N0}/{samples.Count:N0} points covered"
            + (gaps == 0 ? " — FULL COVERAGE, no gaps." : $" — {gaps:N0} UNCOVERED, layout incomplete."));
        sb.AppendLine($"  spacing {toMm(dx):N0} mm between devices, rows {toMm(dy):N0} mm apart");
        sb.AppendLine($"  floor area per device: {perDevice:F1} m2");
        double totalCircle = chosen.Count * Math.PI * Math.Pow(radiusMm / 1000.0, 2);
        sb.AppendLine($"  overlap ratio {totalCircle / toM2(room.Area):F2}x (1.21x is the theoretical best for circle covering)");
        // the other grid, for comparison — buildability vs count
        double otherDx = gridType == "square" ? r * Math.Sqrt(3.0) : r * Math.Sqrt(2.0);
        double otherCell = gridType == "square" ? (Math.Sqrt(3)/2.0)*otherDx*otherDx/1.0 : otherDx*otherDx;
        sb.AppendLine($"  (the {(gridType == "square" ? "hexagonal" : "square")} grid would need roughly {Math.Ceiling(room.Area/mm(1)/mm(1)/ (otherCell/mm(1)/mm(1))):N0} — fewer devices but pipe runs are not straight)");

        if (maxAllowedSpacingMm > 0)
            sb.AppendLine(toMm(dx) <= maxAllowedSpacingMm
                ? $"  SPACING CHECK: {toMm(dx):N0} mm is within your {maxAllowedSpacingMm:N0} mm limit — PASS."
                : $"  SPACING CHECK: {toMm(dx):N0} mm EXCEEDS your {maxAllowedSpacingMm:N0} mm limit — FAIL. Full coverage is not the same as compliance.");

        // --- 5. draw ---
        if (drawCircles && drawInViewIdInt != 0)
        {
            var dv = Document.GetElement(new ElementId(drawInViewIdInt)) as View;
            if (dv == null || dv.IsTemplate) sb.AppendLine($"  drawInViewIdInt {drawInViewIdInt} is not a usable view — nothing drawn.");
            else
            {
                double flatZ = (dv is ViewPlan && (dv as ViewPlan).GenLevel != null)
                    ? (dv as ViewPlan).GenLevel.Elevation : dv.Origin.Z;
                var made = new List<ElementId>();
                using (var t = new Transaction(Document, "AJ Tools - Coverage Layout"))
                {
                    t.Start();
                    try
                    {
                        foreach (var i in chosen)
                        {
                            var ctr = new XYZ(lattice[i].X, lattice[i].Y, flatZ);
                            // ONE closed circle = ONE element (verified 2026-07-26 — do not split into arcs)
                            var dc = Document.Create.NewDetailCurve(dv, Arc.Create(ctr, r, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
                            var ogs = new OverrideGraphicSettings();
                            ogs.SetProjectionLineColor(new Color((byte)circleColorRgb[0], (byte)circleColorRgb[1], (byte)circleColorRgb[2]));
                            ogs.SetProjectionLineWeight(4);
                            dv.SetElementOverrides(dc.Id, ogs);
                            made.Add(dc.Id);
                        }
                        if (groupDrawn && made.Count > 1)
                        {
                            try {
                                var g = Document.Create.NewGroup(made);
                                if (!string.IsNullOrEmpty(drawnGroupName)) { try { g.GroupType.Name = drawnGroupName; } catch { } }
                                sb.AppendLine($"  Grouped as '{g.GroupType.Name}' (Id {g.Id.IntegerValue}).");
                            } catch (Exception exG) { sb.AppendLine($"  Grouping failed ({exG.Message}) — circles still drawn."); }
                        }
                        t.Commit();
                        sb.AppendLine($"  Drew {made.Count:N0} circle(s) in '{dv.Name}' — one element each.");
                    }
                    catch (Exception ex) { try { t.RollBack(); } catch { } sb.AppendLine($"  FAILED to draw — rolled back. {ex.Message}"); }
                }
            }
        }
        sb.AppendLine("  Centres are where the devices go — place them with creators/create-point-based-element.cs.");
    }
}
return sb.ToString();
