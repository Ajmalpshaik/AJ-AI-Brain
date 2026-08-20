// ============================================================
// RECIPE — sprinkler-nfpa-grid.cs
// PURPOSE: Work out how many sprinkler heads a room needs and exactly where they go, by DERIVING the
//          grid from the code limits instead of from a chosen coverage radius. Searches nx x ny upward
//          and returns the smallest head count that satisfies every limit AT ONCE — area per head,
//          max spacing, max and min distance to the wall, min head-to-head — with every centre inside
//          the real room boundary. Prints a one-row-per-rule check table with the measured value.
// STANDALONE — has its own sb and returns; not filter+action shaped. Read-only unless drawCircles is on.
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md  (the method, step by step)
// SOURCE: ../../knowledge/nfpa13-sprinkler-spacing.md      (where the numbers come from)
// SOURCE: ../../knowledge/fire-sprinkler/nfpa-vs-en12845.md (feeding it EN 12845 numbers instead — same fragment)
//
// WHY THIS EXISTS ALONGSIDE recipes/generate-room-coverage-layout.cs:
//   That one answers "no gaps at radius r" — the smoke-detector / CCTV / WiFi question. It is the right
//   tool for those and the WRONG tool to drive a sprinkler layout from, because **NFPA has no coverage-
//   radius concept at all**. Nothing in the standard says a head covers a circle. It limits AREA PER
//   HEAD, SPACING and DISTANCE TO WALL — rectangular-grid quantities. Proven on this Brain's own model
//   2026-07-27: a 20-head layout with verified 100% coverage and zero gaps was legal for light hazard
//   (155 ft2/head) and four heads SHORT for ordinary hazard (130 ft2 cap) — identical geometry, and the
//   circles said nothing about either. This recipe never takes a radius as an input. If one is drawn it
//   is HALF THE CELL DIAGONAL, derived from the grid afterwards, purely so the drawing means something.
//
// THE FOUR THINGS IT REFUSES TO GUESS: hazard class, max area per head, max spacing, and the
//   construction type above the ceiling. None is derivable from the model and every spacing number moves
//   with them. It stops and says so rather than producing a plausible number from a remembered default.
//
// GOTCHA: **A_s = S x L, NOT room area / head count.** NFPA defines area per head from the GRID — S is
//         the spacing along the branch line (or twice the distance to the wall, whichever is greater),
//         L is the spacing between branch lines. Equal cell division makes the wall inset exactly S/2,
//         so twice it is exactly S and the two definitions agree here by construction. On an irregular
//         room or an off-centre grid they diverge and the average UNDERSTATES A_s — which passes a
//         failing layout. Both are printed and any divergence is called out.
// GOTCHA: **convert the foot value every single time.** 15 ft is 4,572 mm, NOT 4,600. A rounded metric
//         cap is 28 mm LENIENT and will pass a layout the code fails. Same for 6 ft = 1,829 and
//         7 ft 6 in = 2,286.
// GOTCHA: **the bounding box is not the room.** The grid is built on the bounding box and then every
//         centre is tested with Room.IsPointInRoom, because an L-shaped room laid out from its bbox puts
//         heads in the notch where nothing can be mounted. The recipe reports the bbox-vs-room area gap
//         and warns above 5% — on a shape that irregular the equal-division grid is the wrong instrument
//         and the room should be split into rectangles first.
// GOTCHA: **an obstructed room does not have a free grid.** Where beams set the module, one axis of the
//         spacing is decided by the bay, not by the search — set baySpacingMm/bayAxis and only the other
//         axis is searched. Run recipes/sprinkler-obstruction-survey.cs FIRST to find out whether this
//         room is obstructed at all; deciding it afterwards means re-doing the layout.
// GOTCHA: an UNPLACED room (Area 0) silently covers nothing — place it first
//         (creators/create-rooms-in-enclosed-regions.cs).
// GOTCHA: this checks GEOMETRY against limits you supply. It does not decide hazard class, density,
//         remote area or hydraulics, and it never prints the word "compliant". The AHJ (QCDD on Ajmal's
//         projects) and the project specification sit on top of NFPA and can be stricter.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session available. Every API call in it
//   is already live-proven elsewhere in this library: Room.get_BoundingBox / IsPointInRoom /
//   GetBoundarySegments and the detail-circle drawing all come from recipes/generate-room-coverage-
//   layout.cs (live 2026-07-26 and 2026-07-27). What is NEW is the search and the check arithmetic —
//   no Revit call, so it is C# logic that a first run on one real room will settle in a minute.
//   Run it on ONE room, read the check table against a hand calculation, then trust it for the rest.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;                    // the room to lay out (must be PLACED — Area > 0)

// --- the code limits. Take them from knowledge/nfpa13-sprinkler-spacing.md, converted from the FOOT
//     value, for the hazard class and construction type of THIS room. 0 means "not stated" and the
//     recipe refuses to run rather than assume.
string standardLabel = "";           // e.g. "NFPA 13 (2022)" or "BS EN 12845". REQUIRED: the two standards agree
                                      // on area per head and differ on almost everything around it, so a check
                                      // table that does not name its rulebook is not a check table.
                                      // See knowledge/fire-sprinkler/nfpa-vs-en12845.md.
string hazardLabel = "";              // e.g. "Ordinary Hazard Group I" — printed on every line of the report
string constructionLabel = "";        // e.g. "unobstructed, noncombustible" — the other half of the answer
double maxAreaPerHeadM2 = 0;          // NFPA: light 225 ft2 = 20.90 | ordinary 130 ft2 = 12.08 | extra >=0.25 gpm/ft2 100 ft2 = 9.29
double maxSpacingMm = 0;              // NFPA: light/ordinary 15 ft = 4572 | extra 12 ft = 3658
double minSpacingMm = 1829;           // NFPA 6 ft = 1829. Set 0 to skip the check and it will say so.
double maxWallDistanceMm = 0;         // 0 = derive as maxSpacingMm / 2 (which is what the code says) and print that it was derived
double minWallDistanceMm = 102;       // NFPA 4 in = 102
double smallRoomWallDistanceMm = 0;   // ONLY for a light-hazard, unobstructed compartment <= 800 ft2 (74.3 m2):
                                      // 9 ft = 2743. Leave 0 unless all three conditions genuinely hold.

// --- how the grid is built
string gridMode = "search";           // "search" = smallest nx x ny that passes everything (normal)
                                      // "fixed"  = use fixedNx x fixedNy and just check it
int fixedNx = 0, fixedNy = 0;
int maxGridSteps = 24;                // upper bound on nx and ny in the search
string branchAxis = "x";              // which way the branch lines run: "x" or "y". Decides which spacing
                                      // is reported as S (along the branch) and which as L (between branches).
double baySpacingMm = 0;              // >0: one axis is FIXED by the structural bay, not searched
string bayAxis = "y";                 // which axis the bay module runs along: "x" or "y"

// --- verification and drawing
double sampleMm = 300;                // floor test spacing for the worst-point check; ~spacing/12 is plenty
bool drawCircles = false;             // draw r = half the cell diagonal — a DRAFTING device, not an NFPA quantity
int drawInViewIdInt = 0;              // the PLAN view to draw into; 0 = don't draw
int[] circleColorRgb = new[] { 255, 0, 0 };
bool groupDrawn = true;
string drawnGroupName = "";           // e.g. "FF_Room4_Sprinkler_Grid"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;
Func<double, double> toM2 = v => v / 10.763910416709722;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

// ---- refuse rather than guess -------------------------------------------------------------------
var missing = new List<string>();
if (maxAreaPerHeadM2 <= 0) missing.Add("maxAreaPerHeadM2");
if (maxSpacingMm <= 0) missing.Add("maxSpacingMm");
if (string.IsNullOrWhiteSpace(hazardLabel)) missing.Add("hazardLabel");
if (string.IsNullOrWhiteSpace(constructionLabel)) missing.Add("constructionLabel");
if (string.IsNullOrWhiteSpace(standardLabel)) missing.Add("standardLabel");

if (missing.Count > 0)
{
    sb.AppendLine("NOT RUN — these are per-request inputs and there is no safe default for any of them:");
    foreach (var m in missing) sb.AppendLine("  - " + m);
    sb.AppendLine("Every spacing number moves with the hazard class AND the construction type above the ceiling.");
    sb.AppendLine("Take them from knowledge/nfpa13-sprinkler-spacing.md, converted from the FOOT value:");
    sb.AppendLine("  light 225 ft2 / 15 ft  ->  20.90 m2 / 4572 mm");
    sb.AppendLine("  ordinary 130 ft2 / 15 ft  ->  12.08 m2 / 4572 mm");
    sb.AppendLine("  light, combustible obstructed, members < 3 ft apart  ->  130 ft2 = 12.08 m2");
    sb.AppendLine("If the hazard class is genuinely unknown, run this three times (light / ordinary / extra)");
    sb.AppendLine("  and show the three head counts side by side — that is useful; guessing one is not.");
}
else if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0)
{
    sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — it encloses nothing, so there is nothing to lay out.");
    sb.AppendLine("  Place it first: creators/create-rooms-in-enclosed-regions.cs.");
}
else
{
    // the wall maximum IS half the allowable spacing — deriving it is applying the rule, not guessing
    bool wallDerived = maxWallDistanceMm <= 0;
    if (wallDerived) maxWallDistanceMm = maxSpacingMm / 2.0;
    double wallLimitMm = smallRoomWallDistanceMm > 0 ? smallRoomWallDistanceMm : maxWallDistanceMm;

    var bb = room.get_BoundingBox(null);
    double W = bb.Max.X - bb.Min.X, H = bb.Max.Y - bb.Min.Y;
    double zProbe = room.Level.Elevation + mm(1000);      // probe at room height, not head height
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    double roomM2 = toM2(room.Area);
    double bboxM2 = toM2(W * H);

    sb.AppendLine($"ROOM '{room.Name}' (Id {roomIdInt}) — {roomM2:F1} m2, bounding box {toMm(W):N0} x {toMm(H):N0} mm");
    sb.AppendLine($"STANDARD: {standardLabel}  |  HAZARD: {hazardLabel}  |  CONSTRUCTION: {constructionLabel}");
    sb.AppendLine($"LIMITS USED — area/head {maxAreaPerHeadM2:F2} m2 | max spacing {maxSpacingMm:N0} mm | "
        + $"min spacing {(minSpacingMm > 0 ? minSpacingMm.ToString("N0") + " mm" : "NOT CHECKED")} | "
        + $"wall {minWallDistanceMm:N0}..{wallLimitMm:N0} mm{(wallDerived ? " (max derived as half the spacing)" : "")}"
        + (smallRoomWallDistanceMm > 0 ? " [SMALL ROOM RULE APPLIED]" : ""));
    if (smallRoomWallDistanceMm > 0 && roomM2 > 74.3)
        sb.AppendLine($"  *** SMALL ROOM RULE DOES NOT APPLY: this room is {roomM2:F1} m2, over the 800 ft2 (74.3 m2) threshold. Remove smallRoomWallDistanceMm.");
    if (bboxM2 > 0 && (bboxM2 - roomM2) / bboxM2 > 0.05)
        sb.AppendLine($"  *** SHAPE WARNING: the bounding box is {bboxM2:F1} m2 against a room of {roomM2:F1} m2 "
            + $"({(bboxM2 - roomM2) / bboxM2 * 100:F0}% larger). This room is not rectangular enough for one equal-division grid — "
            + "split it into rectangles and lay out each, or read every 'centres inside' number below very carefully.");

    // --- the head-count FLOOR from the area rule alone. No layout may go under it. ---
    int minHeadsByArea = (int)Math.Ceiling(roomM2 / maxAreaPerHeadM2);
    sb.AppendLine($"AREA RULE FLOOR: {roomM2:F1} m2 / {maxAreaPerHeadM2:F2} m2 = at least {minHeadsByArea:N0} head(s), "
        + "whatever the geometry looks like.");

    // --- floor sample points: the ground truth of what has to be reached ---
    var samples = new List<XYZ>();
    for (double x = bb.Min.X; x <= bb.Max.X; x += mm(sampleMm))
        for (double y = bb.Min.Y; y <= bb.Max.Y; y += mm(sampleMm))
        {
            var p = new XYZ(x, y, 0);
            if (insideRoom(p)) samples.Add(p);
        }
    sb.AppendLine($"  {samples.Count:N0} floor test point(s) at {sampleMm:N0} mm.");

    // --- boundary segments once: every wall check measures against the REAL boundary ---
    var boundary = new List<Curve>();
    try
    {
        var opt = new Autodesk.Revit.DB.SpatialElementBoundaryOptions();
        foreach (var loop in room.GetBoundarySegments(opt))
            foreach (var seg in loop) boundary.Add(seg.GetCurve());
    }
    catch (Exception exB) { sb.AppendLine($"  (boundary segments unavailable: {exB.Message} — wall checks will be skipped)"); }

    // build the centres for a given nx x ny, equal cell division => wall inset is exactly half the spacing
    Func<int, int, List<XYZ>> build = (nx, ny) =>
    {
        double sx = W / nx, sy = H / ny;
        var pts = new List<XYZ>();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                pts.Add(new XYZ(bb.Min.X + sx * (i + 0.5), bb.Min.Y + sy * (j + 0.5), 0));
        return pts;
    };

    // measure one candidate grid against every limit. Returns null if it passes, else the first reason.
    Func<int, int, List<XYZ>, string> firstFailure = (nx, ny, pts) =>
    {
        double sx = toMm(W / nx), sy = toMm(H / ny);
        double asGridM2 = toM2((W / nx) * (H / ny));
        if (asGridM2 > maxAreaPerHeadM2 + 1e-9) return $"A_s {asGridM2:F2} m2 > {maxAreaPerHeadM2:F2}";
        if (Math.Max(sx, sy) > maxSpacingMm + 1e-6) return $"spacing {Math.Max(sx, sy):N0} mm > {maxSpacingMm:N0}";
        if (minSpacingMm > 0 && pts.Count > 1)
        {
            double closest = double.MaxValue;
            if (nx > 1) closest = Math.Min(closest, sx);
            if (ny > 1) closest = Math.Min(closest, sy);
            if (closest < minSpacingMm - 1e-6) return $"closest pair {closest:N0} mm < {minSpacingMm:N0}";
        }
        double halfX = sx / 2.0, halfY = sy / 2.0;
        if (Math.Max(halfX, halfY) > wallLimitMm + 1e-6) return $"wall inset {Math.Max(halfX, halfY):N0} mm > {wallLimitMm:N0}";
        if (Math.Min(halfX, halfY) < minWallDistanceMm - 1e-6) return $"wall inset {Math.Min(halfX, halfY):N0} mm < {minWallDistanceMm:N0}";
        if (pts.Count < minHeadsByArea) return $"{pts.Count} head(s) under the area-rule floor of {minHeadsByArea}";
        int outside = pts.Count(p => !insideRoom(p));
        if (outside > 0) return $"{outside} centre(s) outside the room";
        return null;
    };

    int bestNx = 0, bestNy = 0;
    var chosen = new List<XYZ>();
    string failReason = null;

    if (gridMode == "fixed")
    {
        if (fixedNx <= 0 || fixedNy <= 0) sb.AppendLine("gridMode is \"fixed\" but fixedNx/fixedNy are not set — nothing to check.");
        else { bestNx = fixedNx; bestNy = fixedNy; chosen = build(bestNx, bestNy); failReason = firstFailure(bestNx, bestNy, chosen); }
    }
    else
    {
        // the bay module, where there is one, FIXES an axis instead of searching it
        int bayN = 0;
        if (baySpacingMm > 0)
        {
            double dim = toMm(bayAxis == "x" ? W : H);
            bayN = Math.Max(1, (int)Math.Round(dim / baySpacingMm));
            sb.AppendLine($"BAY MODULE: {bayAxis.ToUpper()} axis fixed at {baySpacingMm:N0} mm -> {bayN} row(s)/column(s) "
                + $"({dim:N0} mm / {baySpacingMm:N0} mm). Only the other axis is searched.");
        }

        int bestCount = int.MaxValue;
        for (int nx = 1; nx <= maxGridSteps; nx++)
            for (int ny = 1; ny <= maxGridSteps; ny++)
            {
                if (bayN > 0 && bayAxis == "x" && nx != bayN) continue;
                if (bayN > 0 && bayAxis == "y" && ny != bayN) continue;
                if (nx * ny >= bestCount) continue;              // already have something smaller
                var pts = build(nx, ny);
                if (firstFailure(nx, ny, pts) != null) continue;
                bestCount = nx * ny; bestNx = nx; bestNy = ny; chosen = pts;
            }

        if (bestCount == int.MaxValue)
        {
            sb.AppendLine($"NO GRID between 1x1 and {maxGridSteps}x{maxGridSteps} satisfies every limit at once.");
            sb.AppendLine("  The nearest misses, so you can see WHICH limit is binding:");
            var tried = new List<string>();
            for (int nx = 1; nx <= Math.Min(8, maxGridSteps); nx++)
                for (int ny = 1; ny <= Math.Min(8, maxGridSteps); ny++)
                {
                    var why = firstFailure(nx, ny, build(nx, ny));
                    if (why != null) tried.Add($"    {nx} x {ny} ({nx * ny} heads): {why}");
                }
            foreach (var line in tried.Take(12)) sb.AppendLine(line);
            sb.AppendLine("  Usual causes: a room too irregular for one grid (split it), a bay module that fights the");
            sb.AppendLine("  spacing cap, or a wall limit that cannot be met at any equal division — in which case the");
            sb.AppendLine("  grid has to be off-centre and this recipe is the wrong instrument. Say so; do not fudge it.");
        }
    }

    if (chosen.Count > 0)
    {
        double sx = toMm(W / bestNx), sy = toMm(H / bestNy);
        double S = branchAxis == "x" ? sx : sy;               // along the branch line
        double L = branchAxis == "x" ? sy : sx;               // between branch lines
        double asGridM2 = toM2((W / bestNx) * (H / bestNy));
        double avgM2 = roomM2 / chosen.Count;

        sb.AppendLine();
        sb.AppendLine($"LAYOUT: {bestNx} x {bestNy} = {chosen.Count:N0} head(s)"
            + (gridMode == "fixed" ? " (fixed, as given)" : " — the smallest count that satisfies every limit")
            + (failReason != null ? $"  *** THIS GRID FAILS: {failReason}" : ""));
        sb.AppendLine($"  S (along branch, {branchAxis.ToUpper()}) {S:N0} mm  |  L (between branches) {L:N0} mm  |  A_s = S x L = {asGridM2:F2} m2");

        // --- the check table: one row per rule, limit vs measured, PASS/FAIL ---
        double halfX = sx / 2.0, halfY = sy / 2.0;
        double worstInset = Math.Max(halfX, halfY), bestInset = Math.Min(halfX, halfY);
        double closestPair = double.MaxValue;
        if (bestNx > 1) closestPair = Math.Min(closestPair, sx);
        if (bestNy > 1) closestPair = Math.Min(closestPair, sy);
        int outsideCount = chosen.Count(p => !insideRoom(p));

        Func<bool, string> verdict = ok => ok ? "PASS" : "FAIL";
        sb.AppendLine();
        sb.AppendLine("CHECK TABLE — limit | measured | verdict");
        sb.AppendLine($"  max area per head   {maxAreaPerHeadM2,8:F2} m2 | {asGridM2,8:F2} m2 | {verdict(asGridM2 <= maxAreaPerHeadM2 + 1e-9)}");
        sb.AppendLine($"  head count floor    {minHeadsByArea,8:N0}    | {chosen.Count,8:N0}    | {verdict(chosen.Count >= minHeadsByArea)}");
        sb.AppendLine($"  max head-to-head    {maxSpacingMm,8:N0} mm | {Math.Max(sx, sy),8:N0} mm | {verdict(Math.Max(sx, sy) <= maxSpacingMm + 1e-6)}");
        if (minSpacingMm > 0 && closestPair < double.MaxValue)
            sb.AppendLine($"  min head-to-head    {minSpacingMm,8:N0} mm | {closestPair,8:N0} mm | {verdict(closestPair >= minSpacingMm - 1e-6)}");
        else
            sb.AppendLine($"  min head-to-head         NOT CHECKED — minSpacingMm is 0, or there is only one head");
        sb.AppendLine($"  max to wall         {wallLimitMm,8:N0} mm | {worstInset,8:N0} mm | {verdict(worstInset <= wallLimitMm + 1e-6)}");
        sb.AppendLine($"  min to wall         {minWallDistanceMm,8:N0} mm | {bestInset,8:N0} mm | {verdict(bestInset >= minWallDistanceMm - 1e-6)}");
        sb.AppendLine($"  centres in the room {chosen.Count,8:N0}    | {chosen.Count - outsideCount,8:N0}    | {verdict(outsideCount == 0)}");

        if (Math.Abs(asGridM2 - avgM2) > 0.05)
            sb.AppendLine($"  NOTE: room area / head count is {avgM2:F2} m2, {Math.Abs(asGridM2 - avgM2):F2} m2 away from A_s. "
                + "The grid does not tile this room exactly. A_s is the one the code means — trust it, not the average.");

        // --- wall distances measured against the REAL boundary, not the bounding box ---
        if (boundary.Count > 0)
        {
            double worstWall = 0; double nearestWall = double.MaxValue;
            foreach (var crv in boundary)
            {
                double zc = crv.GetEndPoint(0).Z;
                double nearest = double.MaxValue;
                foreach (var c in chosen)
                { double d = crv.Distance(new XYZ(c.X, c.Y, zc)); if (d < nearest) nearest = d; }
                if (nearest > worstWall) worstWall = nearest;
                if (nearest < nearestWall) nearestWall = nearest;
            }
            sb.AppendLine($"  measured on the real boundary ({boundary.Count} segment(s)): worst wall-to-nearest-head "
                + $"{toMm(worstWall):N0} mm | {verdict(toMm(worstWall) <= wallLimitMm + 1e-6)}"
                + $"   closest head-to-wall {toMm(nearestWall):N0} mm | {verdict(toMm(nearestWall) >= minWallDistanceMm - 1e-6)}");
            sb.AppendLine("    (this is the number that counts on an irregular room — the half-spacing figure above assumes a rectangle)");
        }

        // --- the drafting radius, derived, and what it actually reaches ---
        double rDraw = Math.Sqrt(sx * sx + sy * sy) / 2.0;    // half the cell diagonal, in mm
        int unreached = 0; double worstPoint = 0;
        double rFeet = mm(rDraw);
        foreach (var s in samples)
        {
            double best = double.MaxValue;
            foreach (var c in chosen)
            { double ax = s.X - c.X, ay = s.Y - c.Y; double d = Math.Sqrt(ax * ax + ay * ay); if (d < best) best = d; }
            if (best > worstPoint) worstPoint = best;
            if (best > rFeet) unreached++;
        }
        sb.AppendLine();
        sb.AppendLine($"DRAFTING RADIUS (derived, NOT an NFPA quantity): {rDraw:N0} mm = half the cell diagonal — "
            + "the farthest any floor point sits from its head.");
        sb.AppendLine($"  worst floor point {toMm(worstPoint):N0} mm from the nearest head; {samples.Count - unreached:N0}/{samples.Count:N0} within the drawn radius.");

        // --- the centres. THIS is the deliverable. ---
        sb.AppendLine();
        sb.AppendLine($"HEAD CENTRES (mm, project coordinates) — {chosen.Count:N0}:");
        int n = 0;
        foreach (var c in chosen.OrderBy(p => p.Y).ThenBy(p => p.X))
            sb.AppendLine($"  {++n,3}. {toMm(c.X):N0}, {toMm(c.Y):N0}");

        sb.AppendLine();
        sb.AppendLine("NEXT: this grid knows the room outline and NOTHING about what hangs inside it.");
        sb.AppendLine("  Run recipes/sprinkler-obstruction-check.cs on these centres before placing anything —");
        sb.AppendLine("  beams and columns move heads, and a moved head has to be re-checked against this same table.");
        sb.AppendLine("  Nothing here is a compliance statement: the AHJ (QCDD) and the project spec sit on top of NFPA.");

        // --- optional drawing ---
        if (drawCircles && drawInViewIdInt != 0)
        {
            var dv = Document.GetElement(new ElementId(drawInViewIdInt)) as View;
            if (dv == null || dv.IsTemplate) sb.AppendLine($"  drawInViewIdInt {drawInViewIdInt} is not a usable view — nothing drawn.");
            else
            {
                double flatZ = (dv is ViewPlan && (dv as ViewPlan).GenLevel != null)
                    ? (dv as ViewPlan).GenLevel.Elevation : dv.Origin.Z;
                var made = new List<ElementId>();
                using (var t = new Transaction(Document, "AJ Tools - Sprinkler Grid"))
                {
                    t.Start();
                    try
                    {
                        foreach (var c in chosen)
                        {
                            var ctr = new XYZ(c.X, c.Y, flatZ);
                            // ONE closed circle = ONE element (verified 2026-07-26 — do not split into arcs)
                            var dc = Document.Create.NewDetailCurve(dv, Arc.Create(ctr, rFeet, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
                            var ogs = new OverrideGraphicSettings();
                            ogs.SetProjectionLineColor(new Color((byte)circleColorRgb[0], (byte)circleColorRgb[1], (byte)circleColorRgb[2]));
                            ogs.SetProjectionLineWeight(4);
                            dv.SetElementOverrides(dc.Id, ogs);
                            made.Add(dc.Id);
                        }
                        if (groupDrawn && made.Count > 1)
                        {
                            var g = Document.Create.NewGroup(made);
                            if (!string.IsNullOrWhiteSpace(drawnGroupName))
                            { try { g.GroupType.Name = drawnGroupName; } catch (Exception exN) { sb.AppendLine($"  (group name not applied: {exN.Message})"); } }
                        }
                        t.Commit();
                        sb.AppendLine($"  DREW {made.Count:N0} circle(s) at r = {rDraw:N0} mm in '{dv.Name}'.");
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        sb.AppendLine($"FAILED (draw) — rolled back, nothing changed. Reason: {ex.Message}");
                    }
                }
            }
        }
    }
}

return sb.ToString();
