// ============================================================
// RECIPE — sprinkler-compliance-audit.cs
// PURPOSE: Audit the sprinkler heads that are ALREADY in a room — whoever placed them, however they are
//          arranged — against every spacing limit at once, and print one row per rule with the measured
//          value. Ajmal's "check my sprinkler spacing" in one call. READ-ONLY.
// STANDALONE — has its own sb and returns.
// SOURCE: ../../knowledge/nfpa13-sprinkler-spacing.md          (the limits)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md      (step 8, and the reply format)
//
// WHY THIS EXISTS SEPARATELY FROM recipes/sprinkler-nfpa-grid.cs: that one MAKES a regular grid and can
//   check it cheaply, because it knows nx, ny and the spacing by construction. A real layout is not a
//   regular grid — heads have been nudged round beams, added at bulkheads, inherited from a previous
//   revision. So this measures everything from the actual positions:
//     - area per head from a real VORONOI-style nearest-head partition, not from a grid formula
//     - spacing from each head's real nearest neighbours
//     - wall distance from the room's real boundary segments
//   That difference matters: on an irregular layout the grid formula does not apply and room-area-divided-
//   by-head-count flatters it. This reports the worst case, which is what a reviewer looks for.
//
// GOTCHA: **it audits SPACING, not the whole design.** Obstructions are a separate pass
//         (recipes/sprinkler-obstruction-check.cs) and heights are a third
//         (recipes/sprinkler-deflector-height.cs). A green table here with a beam through the middle of
//         the room is not a result. All three, or none.
// GOTCHA: **area per head from the partition is a measurement, not the code's A_s.** NFPA defines A_s
//         from the grid dimensions S x L. Where a layout IS a regular grid, both are reported and they
//         agree; where it is not, the partition figure is the honest one and is labelled as such. Never
//         quietly present one as the other.
// GOTCHA: heads are found by category Sprinklers inside the room. A head modelled as a Plumbing Fixture,
//         or living in a linked model, is invisible here — and "0 found" then reads as an empty room
//         rather than a wrong filter. The count is always stated so that can be spotted.
// GOTCHA: this never prints the word "compliant". It prints what was checked, the measured value and the
//         limit it was checked against. QCDD and the project specification sit on top of NFPA.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. Every Revit call is proven
//   elsewhere (collector by category, Room.IsPointInRoom, GetBoundarySegments, Curve.Distance — all live
//   in recipes/generate-room-coverage-layout.cs, 2026-07-27). The partition arithmetic is plain C#.
//   Run it on a room with a known-good layout first; if the numbers do not match a hand check, the
//   sampling resolution (sampleMm) is the first thing to look at.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;
string hazardLabel = "";              // printed on the report — an audit without it means nothing
string constructionLabel = "";
double maxAreaPerHeadM2 = 0;          // 0 = don't check (and the report says the check was skipped)
double maxSpacingMm = 0;
double minSpacingMm = 1829;
double maxWallDistanceMm = 0;         // 0 = derive as maxSpacingMm / 2, and say it was derived
double minWallDistanceMm = 102;
double sampleMm = 250;                // partition resolution; finer = slower, ~spacing/15 is plenty
int maxListed = 60;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> toM2 = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_SQUARE_METERS);

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0) { sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to audit."); }
else
{
    bool wallDerived = maxWallDistanceMm <= 0 && maxSpacingMm > 0;
    if (wallDerived) maxWallDistanceMm = maxSpacingMm / 2.0;

    var rbb = room.get_BoundingBox(null);
    double zProbe = room.Level.Elevation + mm(1000);
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    double roomM2 = toM2(room.Area);

    // ---- the heads that are really there ----
    var heads = new List<Tuple<int, XYZ, string>>();
    foreach (var h in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Sprinklers)
                 .WhereElementIsNotElementType().ToList())
    {
        var lp = h.Location as LocationPoint;
        if (lp == null || !insideRoom(lp.Point)) continue;
        string tn = null;
        try { var ts = Document.GetElement(h.GetTypeId()); tn = ts == null ? null : ts.Name; } catch { }
        heads.Add(Tuple.Create(h.Id.IntegerValue, lp.Point, tn ?? "(type unknown)"));
    }

    sb.AppendLine($"SPRINKLER AUDIT — '{room.Name}' (Id {roomIdInt}), {roomM2:F1} m2");
    sb.AppendLine($"HAZARD: {(string.IsNullOrWhiteSpace(hazardLabel) ? "*** NOT STATED — this audit is unanchored without it" : hazardLabel)}"
        + $"  |  CONSTRUCTION: {(string.IsNullOrWhiteSpace(constructionLabel) ? "*** NOT STATED" : constructionLabel)}");
    sb.AppendLine($"HEADS FOUND: {heads.Count:N0} (category Sprinklers, host document, inside this room)");
    if (heads.Count == 0)
    {
        sb.AppendLine("  None. Before reading that as an empty room: are they modelled as Plumbing Fixtures, or in a LINK?");
    }
    else
    {
        var byType = heads.GroupBy(h => h.Item3).OrderBy(g => g.Key);
        foreach (var g in byType) sb.AppendLine($"    {g.Count(),4:N0} x {g.Key}");
        if (byType.Count() > 1)
            sb.AppendLine("    *** more than one type in one room — different types can have different spacing tables."
                + " Audit them separately if they do.");
        sb.AppendLine();

        // ---- 1. spacing between heads, measured ----
        double closest = double.MaxValue, worstNearest = 0;
        string closestPair = "", worstIsolated = "";
        for (int a = 0; a < heads.Count; a++)
        {
            double nearest = double.MaxValue;
            for (int b = 0; b < heads.Count; b++)
            {
                if (a == b) continue;
                double dx = toMm(heads[a].Item2.X - heads[b].Item2.X), dy = toMm(heads[a].Item2.Y - heads[b].Item2.Y);
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < nearest) nearest = d;
                if (a < b && d < closest)
                { closest = d; closestPair = $"Id {heads[a].Item1} and Id {heads[b].Item1}"; }
            }
            if (heads.Count > 1 && nearest > worstNearest)
            { worstNearest = nearest; worstIsolated = $"Id {heads[a].Item1}"; }
        }

        // ---- 2. wall distances, from the real boundary ----
        var boundary = new List<Curve>();
        try
        {
            var opt = new Autodesk.Revit.DB.SpatialElementBoundaryOptions();
            foreach (var loop in room.GetBoundarySegments(opt)) foreach (var s in loop) boundary.Add(s.GetCurve());
        }
        catch (Exception ex) { sb.AppendLine($"  (boundary unavailable: {ex.Message} — wall checks skipped)"); }

        double worstWallGap = 0, closestToWall = double.MaxValue;
        string worstWallDesc = "";
        foreach (var crv in boundary)
        {
            double zc = crv.GetEndPoint(0).Z;
            double nearest = double.MaxValue;
            foreach (var h in heads)
            { double d = toMm(crv.Distance(new XYZ(h.Item2.X, h.Item2.Y, zc))); if (d < nearest) nearest = d; }
            if (nearest > worstWallGap) { worstWallGap = nearest; worstWallDesc = $"a {toMm(crv.Length):N0} mm wall"; }
            if (nearest < closestToWall) closestToWall = nearest;
        }

        // ---- 3. area per head, by nearest-head partition of the real room shape ----
        var counts = new int[heads.Count];
        int samples = 0;
        double cell = mm(sampleMm);
        double cellAreaM2 = (sampleMm / 1000.0) * (sampleMm / 1000.0);
        double worstReach = 0;
        for (double x = rbb.Min.X; x <= rbb.Max.X; x += cell)
            for (double y = rbb.Min.Y; y <= rbb.Max.Y; y += cell)
            {
                var p = new XYZ(x, y, 0);
                if (!insideRoom(p)) continue;
                samples++;
                int best = -1; double bestD = double.MaxValue;
                for (int i = 0; i < heads.Count; i++)
                {
                    double dx = p.X - heads[i].Item2.X, dy = p.Y - heads[i].Item2.Y;
                    double d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) counts[best]++;
                double reach = toMm(Math.Sqrt(bestD));
                if (reach > worstReach) worstReach = reach;
            }

        double sampledM2 = samples * cellAreaM2;
        double scale = sampledM2 > 0 ? roomM2 / sampledM2 : 1.0;      // correct the sampling bias to the real area
        double biggestShareM2 = 0; int biggestIdx = -1;
        for (int i = 0; i < heads.Count; i++)
        {
            double share = counts[i] * cellAreaM2 * scale;
            if (share > biggestShareM2) { biggestShareM2 = share; biggestIdx = i; }
        }
        double avgM2 = roomM2 / heads.Count;

        // ---- the check table ----
        Func<bool, string> verdict = ok => ok ? "PASS" : "FAIL";
        sb.AppendLine("CHECK TABLE — limit | measured | verdict");
        if (maxAreaPerHeadM2 > 0)
        {
            int minByArea = (int)Math.Ceiling(roomM2 / maxAreaPerHeadM2);
            sb.AppendLine($"  max area per head   {maxAreaPerHeadM2,8:F2} m2 | {biggestShareM2,8:F2} m2 | {verdict(biggestShareM2 <= maxAreaPerHeadM2)}"
                + $"   (worst head: Id {(biggestIdx >= 0 ? heads[biggestIdx].Item1.ToString() : "?")}, by nearest-head partition)");
            sb.AppendLine($"  head count floor    {minByArea,8:N0}    | {heads.Count,8:N0}    | {verdict(heads.Count >= minByArea)}");
            sb.AppendLine($"    room area / head count is {avgM2:F2} m2 — the AVERAGE. The partition figure above is the worst"
                + " single head, and it is the one a reviewer measures.");
        }
        else sb.AppendLine("  max area per head        NOT CHECKED — maxAreaPerHeadM2 is 0");

        if (maxSpacingMm > 0)
            sb.AppendLine($"  max head-to-head    {maxSpacingMm,8:N0} mm | {worstNearest,8:N0} mm | {verdict(worstNearest <= maxSpacingMm)}"
                + $"   (most isolated: {worstIsolated})");
        else sb.AppendLine("  max head-to-head         NOT CHECKED — maxSpacingMm is 0");

        if (minSpacingMm > 0 && heads.Count > 1)
            sb.AppendLine($"  min head-to-head    {minSpacingMm,8:N0} mm | {closest,8:N0} mm | {verdict(closest >= minSpacingMm)}"
                + $"   ({closestPair})");
        else sb.AppendLine("  min head-to-head         NOT CHECKED");

        if (boundary.Count > 0)
        {
            sb.AppendLine($"  max to wall         {maxWallDistanceMm,8:N0} mm | {worstWallGap,8:N0} mm | "
                + (maxWallDistanceMm > 0 ? verdict(worstWallGap <= maxWallDistanceMm) : "not checked")
                + $"   (worst: {worstWallDesc}){(wallDerived ? " [limit derived as half the spacing]" : "")}");
            sb.AppendLine($"  min to wall         {minWallDistanceMm,8:N0} mm | {closestToWall,8:N0} mm | {verdict(closestToWall >= minWallDistanceMm)}");
        }

        sb.AppendLine($"  worst floor point            - | {worstReach,8:N0} mm | from its nearest head — a geometry measure, not an NFPA limit");
        sb.AppendLine();

        // ---- the outliers, which is what actually gets fixed ----
        var far = new List<string>();
        for (int i = 0; i < heads.Count; i++)
        {
            double share = counts[i] * cellAreaM2 * scale;
            if (maxAreaPerHeadM2 > 0 && share > maxAreaPerHeadM2)
                far.Add($"  Id {heads[i].Item1} at {toMm(heads[i].Item2.X):N0},{toMm(heads[i].Item2.Y):N0} mm covers {share:F2} m2"
                    + $" — over the {maxAreaPerHeadM2:F2} m2 limit");
        }
        if (far.Count > 0)
        {
            sb.AppendLine($"HEADS OVER THE AREA LIMIT — {far.Count:N0}:");
            foreach (var f in far.Take(maxListed)) sb.AppendLine(f);
            if (far.Count > maxListed) sb.AppendLine($"  ... and {far.Count - maxListed:N0} more.");
            sb.AppendLine("  Adding heads in those areas is the fix; moving the existing ones usually just moves the problem.");
            sb.AppendLine();
        }

        sb.AppendLine("WHAT THIS DID NOT CHECK");
        sb.AppendLine("  - obstructions: beams, columns, wide ducts   -> recipes/sprinkler-obstruction-check.cs");
        sb.AppendLine("  - deflector heights, with or without ceiling -> recipes/sprinkler-deflector-height.cs");
        sb.AppendLine("  - hazard class, density, remote area, hydraulics — a licensed fire engineer's calls, not this Brain's.");
        sb.AppendLine("  This is a measurement report against the limits you supplied. It is not a compliance statement:");
        sb.AppendLine("  QCDD and the project specification sit on top of NFPA and can be stricter.");
    }
}

return sb.ToString();
