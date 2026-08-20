// ============================================================
// RECIPE — sprinkler-layout-options.cs
// PURPOSE: Give SEVERAL genuinely different compliant sprinkler layouts for one room, ranked, instead of
//          one answer. Ajmal's ask, 2026-08-20: "if I say room one, generate the layout — and if I don't
//          like, you will generate another one, another style, another ordering." This enumerates every
//          nx x ny grid that satisfies EVERY limit at once, keeps the ones that are actually distinct,
//          and prints a full check line for each so a choice can be made on numbers, not taste.
// STANDALONE — has its own sb and returns. READ-ONLY: it proposes, it never places.
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md    (the limits every option must satisfy)
// SOURCE: ../../knowledge/nfpa13-sprinkler-spacing.md        (where the numbers come from)
// SOURCE: ../../knowledge/fire-sprinkler/nfpa-vs-en12845.md  (feeding it EN 12845 numbers instead)
//
// WHY THIS IS NOT recipes/sprinkler-nfpa-grid.cs WITH A LOOP: that one answers "what is the SMALLEST
//   compliant layout", which is one question with one answer, and it stops at the first grid that passes.
//   In real work fewest-heads is often NOT the layout you issue. A 5 x 4 and a 4 x 5 can both pass with
//   the same head count and run their branch lines at ninety degrees to each other — one fights the
//   structure and the other does not, and no code check can tell them apart. Margin matters too: 129 ft2
//   against a 130 ft2 cap is legal and a reviewer will still ask about it. So this ranks and shows the
//   field, and the choice stays with a person.
//
// WHAT MAKES TWO OPTIONS "DIFFERENT" HERE — not cosmetics:
//   1. head count            — the number that costs money
//   2. branch direction      — S along X or along Y. Decides which way the pipe runs and whether it
//                              crosses the beams. Same count, completely different installation.
//   3. margin under the area cap — how much room the layout leaves against A_s. A reviewer's first question.
//   4. bay alignment         — whether the spacing lands on the structural module (set baySpacingMm).
//   Options identical on all four are collapsed; only one survives, so the list is never padded.
//
// GOTCHA: **more options is not better.** If this returns fifteen, the limits are loose and the real
//         decision is elsewhere (structure, ceiling grid, the architect's tile layout). Say that instead
//         of presenting fifteen. maxOptions caps what is printed and the total found is always reported.
// GOTCHA: **the ranking is a preference, not a code judgement.** Every option printed satisfies every
//         limit you supplied — that is the entry condition. rankBy only decides the ORDER. Nothing further
//         down the list is "less compliant", and saying so would be false.
// GOTCHA: **none of them knows about the beams yet.** This is plan geometry against the spacing limits.
//         Whichever option is chosen still has to go through recipes/sprinkler-obstruction-check.cs, and
//         it can fail there — which is a normal outcome, and a reason to have the runners-up in hand.
// GOTCHA: an equal-division grid puts the wall inset at exactly half the spacing. On a room that is not
//         close to rectangular that is the wrong instrument entirely — the bbox-vs-room warning fires and
//         the honest move is to split the room, not to pick from a list of bad options.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. It shares its geometry with
//   recipes/sprinkler-nfpa-grid.cs (Room.get_BoundingBox / IsPointInRoom / GetBoundarySegments, all live
//   in recipes/generate-room-coverage-layout.cs 2026-07-27); the enumeration and ranking are plain C#.
//   Run it on one room alongside the grid recipe and check that the grid's answer appears in this list —
//   if it does not, one of the two is wrong and that is worth knowing before either is trusted.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;

// --- the limits every option must satisfy. Same numbers you give the grid recipe.
string standardLabel = "";            // "NFPA 13 (2022)" / "BS EN 12845" — REQUIRED, printed on every option
string hazardLabel = "";              // REQUIRED
string constructionLabel = "";        // REQUIRED
double maxAreaPerHeadM2 = 0;          // NFPA: light 20.90 | ordinary 12.08 | extra >=0.25 gpm/ft2 9.29
double maxSpacingMm = 0;              // NFPA: light/ordinary 4572 | extra 3658
double minSpacingMm = 1829;           // NFPA 6 ft. EN 12845 is 2000 — see nfpa-vs-en12845.md
double maxWallDistanceMm = 0;         // 0 = derive as half the spacing, and say so
double minWallDistanceMm = 102;       // NFPA 4 in
double smallRoomWallDistanceMm = 0;   // light hazard, unobstructed, <= 74.3 m2 only: 2743

// --- what counts as a good option
string rankBy = "fewest";             // "fewest"  = lowest head count first (cost)
                                      // "margin"  = most headroom under the area cap first (review comfort)
                                      // "square"  = cells closest to square first (even spray pattern)
                                      // "bay"     = options aligned to baySpacingMm first (buildability)
double baySpacingMm = 0;              // the structural module, from recipes/sprinkler-obstruction-survey.cs
string bayAxis = "y";                 // which axis that module runs along
double bayToleranceMm = 150;          // how close a spacing must be to the module to count as aligned
int maxGridSteps = 24;
int maxOptions = 6;                   // how many to print
bool showCentresFor = true;           // print the head centres for the top-ranked option
int optionToDetail = 1;               // which option to print centres for (1 = top of the list)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;
Func<double, double> toM2 = v => v / 10.763910416709722;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

var missing = new List<string>();
if (maxAreaPerHeadM2 <= 0) missing.Add("maxAreaPerHeadM2");
if (maxSpacingMm <= 0) missing.Add("maxSpacingMm");
if (string.IsNullOrWhiteSpace(standardLabel)) missing.Add("standardLabel");
if (string.IsNullOrWhiteSpace(hazardLabel)) missing.Add("hazardLabel");
if (string.IsNullOrWhiteSpace(constructionLabel)) missing.Add("constructionLabel");

if (missing.Count > 0)
{
    sb.AppendLine("NOT RUN — these are per-request inputs with no safe default:");
    foreach (var m in missing) sb.AppendLine("  - " + m);
    sb.AppendLine("Options are only meaningful against a stated standard, hazard class and construction type —");
    sb.AppendLine("  every one of them moves the limits, and an unlabelled option list is a list of guesses.");
}
else if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0) { sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to lay out."); }
else
{
    bool wallDerived = maxWallDistanceMm <= 0;
    if (wallDerived) maxWallDistanceMm = maxSpacingMm / 2.0;
    double wallLimitMm = smallRoomWallDistanceMm > 0 ? smallRoomWallDistanceMm : maxWallDistanceMm;

    var bb = room.get_BoundingBox(null);
    double W = bb.Max.X - bb.Min.X, H = bb.Max.Y - bb.Min.Y;
    double zProbe = room.Level.Elevation + mm(1000);
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    double roomM2 = toM2(room.Area), bboxM2 = toM2(W * H);
    int minHeadsByArea = (int)Math.Ceiling(roomM2 / maxAreaPerHeadM2);

    sb.AppendLine($"LAYOUT OPTIONS — '{room.Name}' (Id {roomIdInt}), {roomM2:F1} m2, bbox {toMm(W):N0} x {toMm(H):N0} mm");
    sb.AppendLine($"STANDARD: {standardLabel}  |  HAZARD: {hazardLabel}  |  CONSTRUCTION: {constructionLabel}");
    sb.AppendLine($"LIMITS — area/head {maxAreaPerHeadM2:F2} m2 | spacing {minSpacingMm:N0}..{maxSpacingMm:N0} mm | "
        + $"wall {minWallDistanceMm:N0}..{wallLimitMm:N0} mm{(wallDerived ? " (max derived)" : "")}"
        + (smallRoomWallDistanceMm > 0 ? " [SMALL ROOM RULE]" : ""));
    sb.AppendLine($"AREA RULE FLOOR: at least {minHeadsByArea:N0} head(s), whatever the geometry looks like.");
    if (bboxM2 > 0 && (bboxM2 - roomM2) / bboxM2 > 0.05)
        sb.AppendLine($"  *** SHAPE WARNING: bbox {bboxM2:F1} m2 vs room {roomM2:F1} m2 "
            + $"({(bboxM2 - roomM2) / bboxM2 * 100:F0}% larger). An equal-division grid is the wrong instrument here —"
            + " split the room into rectangles rather than choosing from a list of poor options.");
    sb.AppendLine();

    Func<int, int, List<XYZ>> build = (nx, ny) =>
    {
        double sx = W / nx, sy = H / ny;
        var pts = new List<XYZ>();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                pts.Add(new XYZ(bb.Min.X + sx * (i + 0.5), bb.Min.Y + sy * (j + 0.5), 0));
        return pts;
    };

    // --- enumerate everything that passes EVERY limit ---
    // fields: nx, ny, count, sxMm, syMm, asM2, marginPct, squareness, bayAligned
    var passing = new List<Tuple<int, int, int, double, double, double, double, double, bool>>();
    for (int nx = 1; nx <= maxGridSteps; nx++)
        for (int ny = 1; ny <= maxGridSteps; ny++)
        {
            double sx = toMm(W / nx), sy = toMm(H / ny);
            double asM2 = toM2((W / nx) * (H / ny));
            if (asM2 > maxAreaPerHeadM2 + 1e-9) continue;
            if (Math.Max(sx, sy) > maxSpacingMm + 1e-6) continue;
            double closest = double.MaxValue;
            if (nx > 1) closest = Math.Min(closest, sx);
            if (ny > 1) closest = Math.Min(closest, sy);
            if (minSpacingMm > 0 && closest < double.MaxValue && closest < minSpacingMm - 1e-6) continue;
            double halfX = sx / 2.0, halfY = sy / 2.0;
            if (Math.Max(halfX, halfY) > wallLimitMm + 1e-6) continue;
            if (Math.Min(halfX, halfY) < minWallDistanceMm - 1e-6) continue;
            int count = nx * ny;
            if (count < minHeadsByArea) continue;
            var pts = build(nx, ny);
            if (pts.Any(p => !insideRoom(p))) continue;          // unmountable centres

            double marginPct = (maxAreaPerHeadM2 - asM2) / maxAreaPerHeadM2 * 100.0;
            double squareness = Math.Min(sx, sy) / Math.Max(sx, sy);   // 1.0 = perfectly square cells
            bool bayOk = false;
            if (baySpacingMm > 0)
            {
                double along = bayAxis == "x" ? sx : sy;
                bayOk = Math.Abs(along - baySpacingMm) <= bayToleranceMm;
            }
            passing.Add(Tuple.Create(nx, ny, count, sx, sy, asM2, marginPct, squareness, bayOk));
        }

    if (passing.Count == 0)
    {
        sb.AppendLine("NO grid between 1x1 and " + maxGridSteps + "x" + maxGridSteps + " satisfies every limit at once.");
        sb.AppendLine("  That is a real answer, not a tool failure. Usual causes: a room too irregular for one grid,");
        sb.AppendLine("  a wall limit unreachable at any equal division, or a spacing cap fighting the area cap.");
        sb.AppendLine("  Run recipes/sprinkler-nfpa-grid.cs — it prints WHICH limit is binding for the nearest misses.");
    }
    else
    {
        // --- collapse options that are the same decision ---
        // same count AND same branch direction AND same margin band AND same bay alignment = one option
        Func<Tuple<int, int, int, double, double, double, double, double, bool>, string> key = o =>
        {
            string dir = Math.Abs(o.Item4 - o.Item5) < 1 ? "sq" : (o.Item4 > o.Item5 ? "X" : "Y");
            return $"{o.Item3}|{dir}|{Math.Round(o.Item7 / 5.0)}|{o.Item9}";
        };
        var distinct = passing.GroupBy(key).Select(g => g.OrderByDescending(o => o.Item8).First()).ToList();

        IEnumerable<Tuple<int, int, int, double, double, double, double, double, bool>> ordered;
        if (rankBy == "margin") ordered = distinct.OrderByDescending(o => o.Item7).ThenBy(o => o.Item3);
        else if (rankBy == "square") ordered = distinct.OrderByDescending(o => o.Item8).ThenBy(o => o.Item3);
        else if (rankBy == "bay") ordered = distinct.OrderByDescending(o => o.Item9).ThenBy(o => o.Item3);
        else ordered = distinct.OrderBy(o => o.Item3).ThenByDescending(o => o.Item7);
        var list = ordered.ToList();

        sb.AppendLine($"{passing.Count:N0} grid(s) satisfy every limit; {list.Count:N0} are genuinely different decisions.");
        sb.AppendLine($"Ranked by \"{rankBy}\". EVERY option below passes EVERY limit — the ranking is preference, not compliance.");
        if (baySpacingMm > 0)
            sb.AppendLine($"Bay module {baySpacingMm:N0} mm on {bayAxis.ToUpper()}, tolerance {bayToleranceMm:N0} mm.");
        if (list.Count > 8)
            sb.AppendLine("*** That is a lot of valid options, which means the code limits are NOT what decides this room."
                + " The real decision is the structure, the ceiling grid or the architect's tile layout. Say so.");
        sb.AppendLine();

        int shown = 0;
        foreach (var o in list)
        {
            if (shown >= maxOptions) break;
            shown++;
            string dir = Math.Abs(o.Item4 - o.Item5) < 1 ? "square cells"
                : (o.Item4 > o.Item5 ? "branches along Y, wider spacing on X" : "branches along X, wider spacing on Y");
            sb.AppendLine($"  OPTION {shown}: {o.Item1} x {o.Item2} = {o.Item3:N0} heads   [{dir}]"
                + (o.Item9 ? "  [BAY ALIGNED]" : ""));
            sb.AppendLine($"     spacing {o.Item4:N0} x {o.Item5:N0} mm | A_s {o.Item6:F2} m2 of {maxAreaPerHeadM2:F2} "
                + $"({o.Item7:F0}% margin) | wall inset {o.Item4 / 2:N0} / {o.Item5 / 2:N0} mm | cells {o.Item8:F2} square");
            if (o.Item7 < 5)
            {
                sb.AppendLine($"     NOTE: only {o.Item7:F0}% under the area cap. Legal, and fine to issue — just be ready");
                sb.AppendLine("     to show the number, because it is the first thing a reviewer measures.");
            }
        }
        if (list.Count > shown)
            sb.AppendLine($"  ... and {list.Count - shown:N0} more distinct option(s). Raise maxOptions, or change rankBy to see a different face of the field.");

        // --- centres for the one being taken forward ---
        if (showCentresFor && optionToDetail >= 1 && optionToDetail <= list.Count)
        {
            var pick = list[optionToDetail - 1];
            var pts = build(pick.Item1, pick.Item2);
            sb.AppendLine();
            sb.AppendLine($"HEAD CENTRES for OPTION {optionToDetail} ({pick.Item1} x {pick.Item2} = {pick.Item3:N0} heads), mm project coordinates:");
            int n = 0;
            foreach (var c in pts.OrderBy(p => p.Y).ThenBy(p => p.X))
                sb.AppendLine($"  {++n,3}. {toMm(c.X):N0}, {toMm(c.Y):N0}");
        }

        sb.AppendLine();
        sb.AppendLine("NEXT: none of these knows about the beams and columns yet.");
        sb.AppendLine("  Take the chosen option's centres through recipes/sprinkler-obstruction-check.cs before placing.");
        sb.AppendLine("  If it fails there, come back to this list — that is exactly what the runners-up are for.");
        sb.AppendLine("  Nothing here is a compliance statement: QCDD and the project spec sit on top of the standard.");
    }
}

return sb.ToString();
