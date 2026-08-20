// ============================================================
// RECIPE — generate-room-coverage-layout.cs
// PURPOSE: Lay out the devices needed so a fixed coverage radius leaves no gap in a room, and draw the
//          circles. The sprinkler question ("how many heads at 3 m coverage, and where"), and equally the
//          smoke-detector, CCTV, WiFi or lighting version. Verifies the answer instead of asserting it.
// STANDALONE — has its own sb and returns; not filter+action shaped.
//
// TWO MODES, and this choice matters more than gridType:
//   layoutMode "inset"  — DEFAULT, and what you actually issue. Every centre INSIDE the room, off the walls.
//                         gridType "square"    : nx x ny equal cells, one device per cell centre, so the
//                                                inset is exactly half the spacing. nx = ceil(W/(r*sqrt2))
//                                                guarantees the worst point (a cell corner) is within r.
//                         gridType "hexagonal" : staggered rows. Even rows get nx devices at cell centres;
//                                                SHIFTED rows get nx+1 at the cell boundaries with the two
//                                                end ones pulled wallMinMm inside the wall. nx/ny are found
//                                                by search — the smallest device count that verifies full
//                                                coverage with nothing outside — not from a spacing formula.
//   layoutMode "cover"  — the theoretical minimum-circle covering: unconstrained lattice + greedy
//                         set-cover. Use it to answer "what is the fewest possible", NOT to issue a layout.
//
// GOTCHA: **the shifted-row construction decides whether hexagonal is worth anything at all, and the
//         obvious construction is the bad one.** Giving shifted rows nx-1 devices (the "drop one, it's
//         offset" instinct) leaves their end circles a full spacing off the side walls, so the side strips
//         must be covered by the rows above and below — which forces the rows tight together. Measured
//         2026-07-27 on Room 4: that scheme needed **32** devices against square's 20. Giving shifted rows
//         nx+1 with the ends pulled just inside the wall needed **19**. Same room, same radius, same idea
//         of "staggered" — a 40% swing purely from how the row ends are handled. Search nx/ny and verify;
//         never trust a single construction.
//
// ***THE MISTAKE THIS RECIPE ALREADY MADE ONCE (2026-07-27) — read this before using "cover":***
//   "cover" mode optimises coverage OF THE FLOOR, and a circle centred outside the wall still covers floor.
//   So it happily returns centres that are OUTSIDE THE ROOM, where no device can be mounted. Measured on
//   'Room 4' (a plain 20,700 x 13,900 rectangle, Y 3,763..17,663): the square run put 6 of 21 centres
//   outside — an entire row at Y=17,733, beyond the wall — and the hexagonal run put 8 of 20 outside.
//   Both reported "FULL COVERAGE, no gaps" and both were unbuildable. Full coverage was true and useless.
//   The fix is phasing, not more circles: the same room in "inset" mode needs 20 devices, ZERO outside,
//   spacing 4,140 x 3,475 mm, 0 gaps — FEWER devices than the 21 "cover" found. An unconstrained lattice
//   starts at bb.Min - r, so where it lands relative to the walls is an accident; inset phasing is both
//   buildable and usually cheaper. ALWAYS report the outside-centre count, never just the gap count.
//
// GOTCHA: **greedy set-cover leaves redundant picks — prune, or you over-report.** It chooses by immediate
//         gain and never re-checks, so a circle can become unnecessary once later ones land. Same 2026-07-27
//         run: the hexagonal answer of 20 contained one fully redundant circle (36,757/18,763) — removing it
//         kept 3,243/3,243 covered. The real number was 19. pruneRedundant fixes this; leave it on.
// GOTCHA: **covering is not compliance, and there are FOUR limits, not one.** A code (NFPA 13 / BS EN 12845)
//         caps max device-to-device spacing, max distance to a wall, MIN spacing between devices, AND max
//         floor area per device — all four are checked below and all four can fail independently. The area
//         limit is the one a covering algorithm never looks at, and it sets a hard minimum device count:
//         proven 2026-07-27, a 20-head zero-gap layout on Room 4 was legal for light hazard (155 ft2/head)
//         and short by 4 heads for ordinary hazard (130 ft2 cap) — while still covering every square metre.
// GOTCHA: **for FIRE SPRINKLERS do not drive this from a "coverage radius" and do not use this recipe alone.**
//         NFPA has no coverage-radius concept at all; the radius is only a drafting device here. Route via
//         `skills/ajtools-fire-sprinkler-layout/SKILL.md`, take the real limits from
//         `knowledge/nfpa13-sprinkler-spacing.md`, and convert the foot values: 15 ft is 4,572 mm, NOT 4,600
//         — a rounded metric cap is lenient and passes layouts the code fails.
// GOTCHA: **gridType is about buildability, not just count.** Hexagonal is the provably most efficient
//         plane covering (Kershner 1939, spacing r*sqrt3) but its centres stagger, and sprinkler/lighting
//         branch runs are straight, so an issued layout is SQUARE (spacing r*sqrt2). Hexagonal also has
//         WORSE spacing for code: 5,196 vs 4,243 mm at r=3000, so it fails NFPA's 15 ft (4,572 mm) cap that
//         square passes. "Fewest devices" loses on straight runs and on spacing. Ask; never assume fewest.
// GOTCHA: sampleMm drives accuracy and cost — points scale with 1/sampleMm^2. 300 mm over a 288 m2 room is
//         ~3,200 points and runs in ~100 ms; 50 mm would be ~115,000. Use ~r/10, no finer than you need.
// GOTCHA: the count grows with 1/r^2 — r=500 needed 466 circles on this room, r=3000 needed 20. A number in
//         the hundreds usually means the radius is wrong for the job; say so rather than drawing 466 circles.
// GOTCHA: an UNPLACED room (Area 0) silently covers nothing — place it first
//         (creators/create-rooms-in-enclosed-regions.cs).
//
// ✓ LIVE-VERIFIED 2026-07-26 'Room 4' (287.7 m2), "cover" mode: r=3000 -> 20 hexagonal / 21 square, 0 gaps;
//     r=500 -> 466 circles, 7,280/7,280 covered, 0 gaps (5% above the 443 area-ideal).
// ✓ LIVE-VERIFIED 2026-07-27 same room, r=3000, sample 300, drawn + grouped in '1 - Mech':
//     "cover" square    -> 21, 0 gaps, BUT 6 centres outside the room, spacing 4,243 mm.
//     "cover" hexagonal -> 20 reported / 19 real (1 redundant), 8 centres outside, spacing 5,196 mm — FAILS 4,572.
//     "inset" square    -> 20, 0 gaps, 0 outside, spacing 4,140 x 3,475 mm, wall inset 2,070/1,738 mm,
//                          worst cell-corner distance 2,703 mm vs 3,000 radius. PASSES spacing and wall caps.
//                          Drawn + grouped 'MEP_Room4_Coverage_R3000_Inset'; read back document-wide:
//                          20 members, every radius exactly 3,000 mm, 20/20 centres inside the room.
//     "inset" hexagonal -> 19, 0 gaps, 0 outside (6 cols x 3 rows, shifted rows 7). In-row 3,450 mm,
//                          rows 4,633 mm, wall inset 1,725/2,317 mm, 15.1 m2 each, overlap 1.87x.
//                          Drawn + grouped 'MEP_Room4_Coverage_R3000_Staggered', read back 19/19 inside.
//                          BUT it FAILS both NFPA caps: row spacing 4,633 (over 4,572 by 61 mm) and
//                          wall 2,317 (over 2,286 by 31 mm). The compliant staggered option is nx=5 ny=4
//                          -> 22 devices, in-row 4,140, rows 3,475, wall 2,070/1,738, both PASS.
//
// GOTCHA: **hexagonal's efficiency advantage is a PLANE result and a bounded room mostly destroys it.**
//         Kershner's r*sqrt3 covering wins on an infinite plane with no edges to waste. Inside four walls
//         the edge and corner circles are half-wasted, and constraining centres to be mountable costs more
//         than the stagger saves. Measured on Room 4 at r=3000: square inset 20 (both caps PASS) vs
//         staggered inset 19 (both caps FAIL by ~20-30 mm) vs compliant staggered 22. So hexagonal buys at
//         best one device out of twenty, and only by breaking spacing. Add straight branch runs and square
//         wins outright. Do not sell hexagonal as "30% fewer" inside a room — that number is for the plane.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;                  // the room to cover (must be PLACED — Area > 0)
double radiusMm = 0;                // device coverage radius — ASK. 0 refuses to run rather than guess.
                                    // 3000 was ONE past job's figure; a plausible number left here reads as
                                    // a standard next session. For sprinklers this is not even an NFPA
                                    // concept — derive it from the spacing rules, see the fire skill.
string layoutMode = "inset";        // "inset" = buildable, centres inside the room (ISSUE THIS)
                                    // "cover" = theoretical minimum, may put centres OUTSIDE the room
string gridType = "square";         // "square" (straight runs, better spacing) | "hexagonal" (staggered rows)
double sampleMm = 300;              // floor test spacing; ~r/10 is plenty
double wallMinMm = 300;             // inset hexagonal: how far inside the wall a clamped row-end device sits
bool requireCentresInsideRoom = true;  // cover mode: throw away candidates centred outside the room
bool pruneRedundant = true;         // cover mode: drop picks that became unnecessary — leave ON
double maxAllowedSpacingMm = 0;     // code MAX between devices — NFPA 15 ft = 4572 (NOT 4600); 0 = don't check
double maxWallDistanceMm = 0;       // code MAX device-to-wall — half the spacing, 15 ft/2 = 2286; 0 = don't check
double minSpacingMm = 0;            // code MIN between devices — NFPA 6 ft = 1829; 0 = don't check
double maxAreaPerDeviceM2 = 0;      // code MAX floor area per device — NFPA 130 ft2 = 12.1; 0 = don't check
bool drawCircles = true;            // draw the result
int drawInViewIdInt = 0;            // the PLAN view to draw into (detail circles); 0 = don't draw
int[] circleColorRgb = new[]{255,0,0};
bool groupDrawn = true;
string drawnGroupName = "";         // e.g. "MEP_Room4_Sprinkler_R3000"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double,double> toMm = v => v * 304.8;
Func<double,double> mm = v => v / 304.8;
Func<double,double> toM2 = v => v / 10.763910416709722;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (radiusMm <= 0)
{
    sb.AppendLine("radiusMm is not set. State the coverage radius for THIS job — it is a per-request input,");
    sb.AppendLine("  never a carried-over default. For fire sprinklers do not pick one at all: derive the grid");
    sb.AppendLine("  from the NFPA spacing limits first (skills/ajtools-fire-sprinkler-layout/SKILL.md), then");
    sb.AppendLine("  the drawn radius falls out of it as half the cell diagonal.");
}
else if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0)
{
    sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — it encloses nothing, so there is nothing to cover.");
    sb.AppendLine("  Place it first: creators/create-rooms-in-enclosed-regions.cs.");
}
else
{
    double r = mm(radiusMm), r2 = r * r;
    var bb = room.get_BoundingBox(null);
    double zProbe = room.Level.Elevation + mm(1000);   // probe at room height, not device height
    Func<XYZ,bool> insideRoom = p => { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    // --- 1. sample the real room shape (ground truth of what must be covered; handles L-shapes) ---
    var samples = new List<XYZ>();
    for (double x = bb.Min.X; x <= bb.Max.X; x += mm(sampleMm))
        for (double y = bb.Min.Y; y <= bb.Max.Y; y += mm(sampleMm))
        {
            var p = new XYZ(x, y, 0);
            if (insideRoom(p)) samples.Add(p);
        }
    sb.AppendLine($"'{room.Name}' {toM2(room.Area):F1} m2, bbox {toMm(bb.Max.X-bb.Min.X):N0} x {toMm(bb.Max.Y-bb.Min.Y):N0} mm"
        + $" — {samples.Count:N0} floor test points at {sampleMm} mm.");
    if (samples.Count == 0) { sb.AppendLine("  No point tested inside the room — check the room is really bounded."); }
    else
    {
        var chosen = new List<XYZ>();
        double spX, spY;

        if (layoutMode == "inset")
        {
            double W = bb.Max.X - bb.Min.X, H = bb.Max.Y - bb.Min.Y;
            double wallMin = mm(wallMinMm);
            if (gridType == "hexagonal")
            {
                // --- staggered rows. nx/ny come from a SEARCH that verifies, not from a spacing formula:
                //     the shifted-row construction changes the answer 40% (see the header gotcha). ---
                Func<int,int,List<XYZ>> build = (nx0, ny0) => {
                    double sx = W/nx0, sy = H/ny0;
                    var pts = new List<XYZ>();
                    for (int j = 0; j < ny0; j++)
                    {
                        double y = bb.Min.Y + sy*(j+0.5);
                        if (j % 2 == 0) for (int i=0;i<nx0;i++) pts.Add(new XYZ(bb.Min.X + sx*(i+0.5), y, 0));
                        else for (int i=0;i<=nx0;i++)        // shifted row gets nx+1, ends pulled inside
                        {
                            double x = bb.Min.X + sx*i;
                            if (i == 0)   x = bb.Min.X + wallMin;
                            if (i == nx0) x = bb.Max.X - wallMin;
                            pts.Add(new XYZ(x, y, 0));
                        }
                    }
                    return pts;
                };
                int bx = 0, by = 0, bestCount = int.MaxValue;
                for (int nx0 = 2; nx0 <= 8; nx0++) for (int ny0 = 2; ny0 <= 8; ny0++)
                {
                    var pts = build(nx0, ny0);
                    var cv = new bool[samples.Count];
                    foreach (var c in pts) for (int s=0;s<samples.Count;s++)
                    { double ax=samples[s].X-c.X, ay=samples[s].Y-c.Y; if (ax*ax+ay*ay<=r2) cv[s]=true; }
                    if (cv.Any(x=>!x)) continue;                       // gaps
                    if (pts.Any(c=>!insideRoom(c))) continue;          // unmountable
                    if (pts.Count < bestCount) { bestCount = pts.Count; bx = nx0; by = ny0; }
                }
                if (bestCount == int.MaxValue)
                { sb.AppendLine("  No staggered inset arrangement (2..8 x 2..8) covers this room at this radius."); spX = 0; spY = 0; }
                else
                {
                    spX = W/bx; spY = H/by;
                    chosen.AddRange(build(bx, by));
                    sb.AppendLine($"INSET STAGGERED (hexagonal) at {radiusMm:F0} mm radius: {bx} cols x {by} rows, shifted rows get {bx+1}");
                }
            }
            else
            {
                // --- equal cell division: every centre inside, half-spacing off the walls ---
                double sMax = r * Math.Sqrt(2.0);              // largest square spacing that still covers
                int nx = (int)Math.Ceiling(W / sMax), ny = (int)Math.Ceiling(H / sMax);
                spX = W / nx; spY = H / ny;                    // equal division => inset is exactly half spacing
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                        chosen.Add(new XYZ(bb.Min.X + spX * (i + 0.5), bb.Min.Y + spY * (j + 0.5), 0));
                sb.AppendLine($"INSET SQUARE GRID at {radiusMm:F0} mm radius: {nx} x {ny}");
                sb.AppendLine($"  worst cell-corner distance {toMm(Math.Sqrt(spX*spX + spY*spY)/2.0):N0} mm vs {radiusMm:F0} mm radius"
                    + (Math.Sqrt(spX*spX + spY*spY)/2.0 <= r ? " — within reach." : " — TOO FAR, will gap."));
            }
        }
        else
        {
            // --- unconstrained lattice + greedy set-cover: the theoretical minimum ---
            double dx, dy;
            if (gridType == "square") { dx = r * Math.Sqrt(2.0); dy = dx; }
            else { dx = r * Math.Sqrt(3.0); dy = 1.5 * r; }
            spX = dx; spY = dy;
            var lattice = new List<XYZ>();
            int row = 0;
            for (double y = bb.Min.Y - r; y <= bb.Max.Y + r; y += dy, row++)
            {
                double xOff = (gridType == "square" || row % 2 == 0) ? 0 : dx / 2.0;
                for (double x = bb.Min.X - r + xOff; x <= bb.Max.X + r; x += dx)
                {
                    var c = new XYZ(x, y, 0);
                    // the 2026-07-27 fix: without this, chosen centres land outside the wall
                    if (requireCentresInsideRoom && !insideRoom(c)) continue;
                    lattice.Add(c);
                }
            }
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
            var cov = new bool[samples.Count];
            var picks = new List<int>();
            while (true)
            {
                int best = -1, bestGain = 0;
                foreach (var i in pool)
                {
                    int gain = 0;
                    foreach (var s in covers[i]) if (!cov[s]) gain++;
                    if (gain > bestGain) { bestGain = gain; best = i; }
                }
                if (best < 0 || bestGain == 0) break;
                picks.Add(best);
                foreach (var s in covers[best]) cov[s] = true;
                pool.Remove(best);
            }
            // PRUNE — greedy leaves picks that later picks made unnecessary (proven: 20 reported, 19 real)
            if (pruneRedundant)
            {
                var hits = new int[samples.Count];
                foreach (var i in picks) foreach (var s in covers[i]) hits[s]++;
                var keep = new List<int>();
                int dropped = 0;
                foreach (var i in picks)
                {
                    bool needed = false;
                    foreach (var s in covers[i]) if (hits[s] == 1) { needed = true; break; }
                    if (needed) keep.Add(i);
                    else { foreach (var s in covers[i]) hits[s]--; dropped++; }
                }
                if (dropped > 0) sb.AppendLine($"  PRUNED {dropped} redundant circle(s) greedy had picked.");
                picks = keep;
            }
            foreach (var i in picks) chosen.Add(lattice[i]);
            sb.AppendLine($"COVER {gridType.ToUpper()} lattice at {radiusMm:F0} mm radius"
                + (requireCentresInsideRoom ? " (centres constrained inside the room):" : " (UNCONSTRAINED — centres may be outside):"));
        }

        // --- 2. VERIFY: coverage, and the buildability check that was once missing ---
        var covered2 = new bool[samples.Count];
        foreach (var c in chosen)
            for (int s = 0; s < samples.Count; s++)
            { double ax = samples[s].X - c.X, ay = samples[s].Y - c.Y; if (ax * ax + ay * ay <= r2) covered2[s] = true; }
        int gaps = covered2.Count(c => !c);
        int outside = chosen.Count(c => !insideRoom(c));

        sb.AppendLine($"  DEVICES NEEDED: {chosen.Count:N0}");
        sb.AppendLine($"  COVERAGE: {samples.Count - gaps:N0}/{samples.Count:N0} points"
            + (gaps == 0 ? " — FULL COVERAGE, no gaps." : $" — {gaps:N0} UNCOVERED, layout incomplete."));
        sb.AppendLine($"  BUILDABLE: {chosen.Count - outside:N0}/{chosen.Count:N0} centres inside the room"
            + (outside == 0 ? " — all mountable." : $" — {outside:N0} OUTSIDE THE ROOM, cannot be mounted. Use layoutMode=\"inset\"."));
        sb.AppendLine($"  spacing {toMm(spX):N0} x {toMm(spY):N0} mm | floor area per device {toM2(room.Area)/Math.Max(1,chosen.Count):F1} m2");
        double totalCircle = chosen.Count * Math.PI * Math.Pow(radiusMm / 1000.0, 2);
        sb.AppendLine($"  overlap ratio {totalCircle / toM2(room.Area):F2}x (1.21x is the theoretical best for circle covering)");

        // distance from each WALL to its nearest device — the half of the code check that was missed
        double worstWall = 0; string worstWallDesc = "";
        try {
            var opt = new Autodesk.Revit.DB.SpatialElementBoundaryOptions();
            foreach (var loop in room.GetBoundarySegments(opt))
                foreach (var seg in loop)
                {
                    var crv = seg.GetCurve();
                    double zc = crv.GetEndPoint(0).Z;
                    double nearest = double.MaxValue;
                    foreach (var c in chosen)
                    { double d = crv.Distance(new XYZ(c.X, c.Y, zc)); if (d < nearest) nearest = d; }
                    if (nearest > worstWall) { worstWall = nearest; worstWallDesc = $"{toMm(crv.Length):N0} mm long wall"; }
                }
            sb.AppendLine($"  worst wall-to-nearest-device: {toMm(worstWall):N0} mm ({worstWallDesc})");
            if (maxWallDistanceMm > 0)
                sb.AppendLine(toMm(worstWall) <= maxWallDistanceMm
                    ? $"  WALL CHECK: {toMm(worstWall):N0} mm within your {maxWallDistanceMm:N0} mm limit — PASS."
                    : $"  WALL CHECK: {toMm(worstWall):N0} mm EXCEEDS your {maxWallDistanceMm:N0} mm limit — FAIL.");
        } catch (Exception exW) { sb.AppendLine($"  (wall-distance check skipped: {exW.Message})"); }

        if (maxAllowedSpacingMm > 0)
            sb.AppendLine(Math.Max(toMm(spX), toMm(spY)) <= maxAllowedSpacingMm
                ? $"  SPACING CHECK: {Math.Max(toMm(spX), toMm(spY)):N0} mm is within your {maxAllowedSpacingMm:N0} mm limit — PASS."
                : $"  SPACING CHECK: {Math.Max(toMm(spX), toMm(spY)):N0} mm EXCEEDS your {maxAllowedSpacingMm:N0} mm limit — FAIL. Full coverage is not the same as compliance.");

        // MINIMUM spacing — a code limit in the other direction. A dense layout breaks this while
        // covering perfectly, so the max-spacing check alone does not catch it (NFPA 6 ft = 1,829 mm).
        if (minSpacingMm > 0 && chosen.Count > 1)
        {
            double closest = double.MaxValue; string pair = "";
            for (int a = 0; a < chosen.Count; a++)
                for (int b = a + 1; b < chosen.Count; b++)
                {
                    double dxx = chosen[a].X - chosen[b].X, dyy = chosen[a].Y - chosen[b].Y;
                    double d = Math.Sqrt(dxx*dxx + dyy*dyy);
                    if (d < closest) { closest = d; pair = $"{toMm(chosen[a].X):N0},{toMm(chosen[a].Y):N0} and {toMm(chosen[b].X):N0},{toMm(chosen[b].Y):N0}"; }
                }
            sb.AppendLine(toMm(closest) >= minSpacingMm
                ? $"  MIN SPACING CHECK: closest pair {toMm(closest):N0} mm >= your {minSpacingMm:N0} mm minimum — PASS."
                : $"  MIN SPACING CHECK: closest pair {toMm(closest):N0} mm is UNDER your {minSpacingMm:N0} mm minimum — FAIL ({pair}).");
        }

        // MAX AREA PER DEVICE — the limit a covering algorithm has no reason to look at, and the one that
        // sets a hard MINIMUM device count. Proven 2026-07-27: a 20-head layout with zero gaps was legal on
        // light hazard (155 ft2/head) and short by 4 heads on ordinary hazard (130 ft2 limit).
        //
        // TWO methods, and NFPA means the FIRST one:
        //   A_s = S x L  — the grid dimensions (spacing along the branch x spacing between branches).
        //                  This is what NFPA 13 defines, and it is reported as the governing value.
        //   area / count — a convenience average. Agrees with A_s ONLY when the grid tiles the room exactly
        //                  with half-spacing insets. On an L-shape, an irregular room, or a grid whose cells
        //                  hang outside the boundary, it UNDERSTATES A_s and can pass a failing layout.
        // Both are printed, and a divergence is called out rather than hidden behind one number.
        if (maxAreaPerDeviceM2 > 0)
        {
            double asGrid = toM2(spX * spY);                                  // NFPA method
            double avg = toM2(room.Area) / Math.Max(1, chosen.Count);          // convenience average
            int minNeeded = (int)Math.Ceiling(toM2(room.Area) / maxAreaPerDeviceM2);
            sb.AppendLine(asGrid <= maxAreaPerDeviceM2
                ? $"  AREA/DEVICE CHECK (A_s = S x L = {asGrid:F2} m2): within your {maxAreaPerDeviceM2:F2} m2 limit — PASS."
                : $"  AREA/DEVICE CHECK (A_s = S x L = {asGrid:F2} m2): EXCEEDS your {maxAreaPerDeviceM2:F2} m2 limit — FAIL.");
            sb.AppendLine($"    area/count average {avg:F2} m2 | area rule needs at least {minNeeded:N0} devices, layout has {chosen.Count:N0}"
                + (chosen.Count >= minNeeded ? " — OK." : " — SHORT."));
            if (Math.Abs(asGrid - avg) > 0.05)
                sb.AppendLine($"    NOTE: the two differ by {Math.Abs(asGrid-avg):F2} m2 — the grid does not tile this room exactly"
                    + " (irregular shape or cells overhanging the boundary). A_s is the one the code means; trust it, not the average.");
        }

        // --- 3. draw ---
        if (drawCircles && drawInViewIdInt != 0)
        {
            var dv = Document.GetElement(new ElementId(drawInViewIdInt)) as View;
            if (dv == null || dv.IsTemplate) sb.AppendLine($"  drawInViewIdInt {drawInViewIdInt} is not a usable view — nothing drawn.");
            else
            {
                double flatZ = (dv is ViewPlan && (dv as ViewPlan).GenLevel != null)
                    ? (dv as ViewPlan).GenLevel.Elevation : dv.Origin.Z;
                var made = new List<ElementId>();
                var centres = new List<string>();   // report the actual centres — they ARE the device positions
                using (var t = new Transaction(Document, "AJ Tools - Coverage Layout"))
                {
                    t.Start();
                    try
                    {
                        foreach (var c in chosen)
                        {
                            var ctr = new XYZ(c.X, c.Y, flatZ);
                            // ONE closed circle = ONE element (verified 2026-07-26 — do not split into arcs)
                            var dc = Document.Create.NewDetailCurve(dv, Arc.Create(ctr, r, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
                            var ogs = new OverrideGraphicSettings();
                            ogs.SetProjectionLineColor(new Color((byte)circleColorRgb[0], (byte)circleColorRgb[1], (byte)circleColorRgb[2]));
                            ogs.SetProjectionLineWeight(4);
                            dv.SetElementOverrides(dc.Id, ogs);
                            made.Add(dc.Id);
                            centres.Add($"{toMm(ctr.X):N0},{toMm(ctr.Y):N0}");
                        }
                        if (groupDrawn && made.Count > 1)
                        {
                            try {
                                var g = Document.Create.NewGroup(made);
                                if (!string.IsNullOrEmpty(drawnGroupName)) { try { g.GroupType.Name = drawnGroupName; } catch { } }
                                sb.AppendLine($"  Grouped as '{g.GroupType.Name}' (Id {g.Id}).");
                            } catch (Exception exG) { sb.AppendLine($"  Grouping failed ({exG.Message}) — circles still drawn."); }
                        }
                        t.Commit();
                        sb.AppendLine($"  Drew {made.Count:N0} circle(s) in '{dv.Name}' — one element each.");
                        sb.AppendLine("  CENTRES (mm, project X,Y): " + string.Join(" | ", centres));
                    }
                    catch (Exception ex) { try { t.RollBack(); } catch { } sb.AppendLine($"  FAILED to draw — rolled back. {ex.Message}"); }
                }
                // A view-scoped read right after this transaction UNDER-REPORTS — see live-model/core.md.
                // Never conclude earlier work was deleted from it; confirm document-wide first.
            }
        }
        sb.AppendLine("  Centres are where the devices go — place them with creators/create-point-based-element.cs.");
    }
}
return sb.ToString();
