// ============================================================
// FRAGMENT (action) — action-report-room-dimensions.cs
// PURPOSE: Each room's WIDTH x LENGTH measured on the ROOM'S OWN AXES — not on project north — plus how
//          far the room is rotated, its area, and how much bigger its project-aligned bounding box is
//          than the room really is. The dimensions a layout grid, a ceiling grid or a schedule needs.
// ASSUMES: elements (List<Element>, Rooms and/or Spaces) and sb exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. READ-ONLY: no transaction, nothing created.
//
// ✱✱ A ROTATED ROOM HAS NO USEFUL BOUNDING BOX, AND THAT IS THE POINT OF THIS FRAGMENT. `get_BoundingBox`
//    is aligned to PROJECT X/Y. Turn a 8000 x 4000 room 30 degrees and its box becomes roughly
//    8900 x 7500 — half as big again, in a shape the room does not have. Anything that lays out on that
//    box puts its grid diagonal to the walls. On a site that is not square to true north — which is most
//    of them — that is every room in the model.
//    So the room's own axes are found first, and the dimensions are taken along them.
//
// ✱✱ HOW THE ROOM'S OWN AXIS IS FOUND: the LONGEST segment of its outer boundary loop. That is the
//    dominant wall, and on any room that is roughly rectangular it is the axis a person would lay out
//    along. The angle from that segment to project X is the room's rotation; the boundary points are
//    then rotated by MINUS that angle in memory and the min/max taken. **Nothing is created and nothing
//    moves** — the rotation is arithmetic on a list of points, not a transform applied to the model.
//
// ✱✱ THE OUTER LOOP IS THE COUNTER-CLOCKWISE ONE, not loop[0]. Revit returns a room's boundary as a list
//    of loops where the outer edge and the holes (a core, a column, a shaft) are mixed together, and
//    while loop[0] is usually the outer one, "usually" is not a rule. The reliable test is direction:
//    relative to the face normal the OUTER loop runs counter-clockwise and every hole runs clockwise.
//    This uses the signed area of each loop, which is the same test without needing the face.
//    (`action-create-from-room-boundaries.cs` builds from all loops and treats [0] as the outside — that
//    is correct for building a floor with holes, and this is the sharper test where it matters.)
//
// GOTCHA: WIDTH IS THE SHORTER, LENGTH THE LONGER — stated explicitly, because rotating past 45 degrees
//         swaps which project axis they land on and a report that does not fix an order silently swaps
//         its own columns halfway down the table.
// GOTCHA: an L-shaped or U-shaped room has no honest width x length. The bbox-vs-room area gap is
//         reported for every room and flagged above 5%, which is the same threshold the sprinkler grid
//         uses — on a shape that irregular, split it into rectangles rather than quoting a rectangle.
// GOTCHA: an UNPLACED room (Area 0) has no boundary. Counted separately, not treated as a failure.
// GOTCHA: this measures the room as its boundary is defined, so the boundary LOCATION matters — see
//         action-report-room-boundaries.cs, which exposes all four (Finish, Center, CoreBoundary,
//         CoreCenter). This uses Finish, the face the room actually sees, and says so.
// ✱ FOUND WHILE WRITING THIS, AND WORTH ACTING ON: `recipes/sprinkler-nfpa-grid.cs` builds its grid on
//    the project-aligned bounding box with `branchAxis` of "x" or "y". On a ROTATED rectangular room
//    that grid runs diagonal to the walls, and worse, the recipe's own bbox-vs-room-area warning fires
//    and tells you the room is too irregular for an equal-division grid — when it is a perfectly good
//    rectangle that simply is not square to project north. Run this fragment first: if the rotation is
//    not ~0 and the area gap is large, the room is rotated, not irregular.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Check one rotated room against a dimension
//   you place by hand before trusting it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxRoomsReported = 60;
double irregularWarnPercent = 5.0;   // flag when the bbox is more than this much bigger than the room
// ---- END INPUTS ----

const double MM = 304.8;
const double SQFT_PER_SQM = 10.763910416709722;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put filter-by-category.cs with OST_Rooms (or OST_MEPSpaces) above this.");
    return sb.ToString();
}

var opts = new SpatialElementBoundaryOptions
{
    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish,
    StoreFreeBoundaryFaces = true
};

int unplaced = 0, noBoundary = 0, irregular = 0, reported = 0, notSpatial = 0;
var rows = new List<string>();

foreach (var el in elements)
{
    if (reported >= maxRoomsReported) break;
    var spatial = el as SpatialElement;
    if (spatial == null) { notSpatial++; continue; }

    double area = 0;
    try { area = spatial.Area; } catch { }
    if (area <= 0) { unplaced++; continue; }

    IList<IList<BoundarySegment>> loops = null;
    try { loops = spatial.GetBoundarySegments(opts); } catch { }
    if (loops == null || loops.Count == 0) { noBoundary++; continue; }

    // ---- pick the OUTER loop by signed area, not by index ----
    IList<BoundarySegment> outer = null;
    double bestSigned = 0;
    foreach (var loop in loops)
    {
        // Shoelace on the segment end points. The outer loop has the largest |signed area|, and its sign
        // tells you the direction — holes run the other way.
        double signed = 0;
        var pts = new List<XYZ>();
        foreach (var seg in loop)
        {
            Curve c = null;
            try { c = seg.GetCurve(); } catch { }
            if (c != null) pts.Add(c.GetEndPoint(0));
        }
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            signed += (p.X * q.Y) - (q.X * p.Y);
        }
        signed /= 2.0;
        if (Math.Abs(signed) > Math.Abs(bestSigned)) { bestSigned = signed; outer = loop; }
    }
    if (outer == null) { noBoundary++; continue; }

    // ---- the room's own axis: the longest boundary segment ----
    var outerPts = new List<XYZ>();
    Curve longest = null; double longestLen = 0;
    foreach (var seg in outer)
    {
        Curve c = null;
        try { c = seg.GetCurve(); } catch { }
        if (c == null) continue;
        outerPts.Add(c.GetEndPoint(0));
        double len = 0;
        try { len = c.ApproximateLength; } catch { try { len = c.Length; } catch { } }
        if (len > longestLen) { longestLen = len; longest = c; }
    }
    if (longest == null || outerPts.Count < 3) { noBoundary++; continue; }

    double angle = 0;
    try
    {
        var a = longest.GetEndPoint(0); var b = longest.GetEndPoint(1);
        if (a.X > b.X) { var t = a; a = b; b = t; }      // consistent direction, so the angle is stable
        var v = b.Subtract(a);
        angle = Math.Atan2(v.Y, v.X);                     // radians, from project X
    }
    catch { }

    // ---- rotate the boundary points by -angle IN MEMORY and take the extents ----
    double cos = Math.Cos(-angle), sin = Math.Sin(-angle);
    double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
    foreach (var p in outerPts)
    {
        double rx = p.X * cos - p.Y * sin;
        double ry = p.X * sin + p.Y * cos;
        if (rx < minX) minX = rx; if (rx > maxX) maxX = rx;
        if (ry < minY) minY = ry; if (ry > maxY) maxY = ry;
    }
    double d1 = maxX - minX, d2 = maxY - minY;
    double lengthFt = Math.Max(d1, d2), widthFt = Math.Min(d1, d2);   // fixed order: length >= width

    // ---- how much bigger is the PROJECT-aligned box than the room? ----
    double projMinX = outerPts.Min(p => p.X), projMaxX = outerPts.Max(p => p.X);
    double projMinY = outerPts.Min(p => p.Y), projMaxY = outerPts.Max(p => p.Y);
    double projBoxArea = (projMaxX - projMinX) * (projMaxY - projMinY);
    double ownBoxArea = d1 * d2;
    double roomAreaFt = area;
    double gapOwn = ownBoxArea > 0 ? (ownBoxArea - roomAreaFt) / ownBoxArea * 100.0 : 0;
    double gapProj = projBoxArea > 0 ? (projBoxArea - roomAreaFt) / projBoxArea * 100.0 : 0;

    double angleDeg = angle * 180.0 / Math.PI;
    while (angleDeg < 0) angleDeg += 180.0;      // a room rotated 190 degrees is rotated 10
    while (angleDeg >= 180.0) angleDeg -= 180.0;
    if (angleDeg > 90.0) angleDeg -= 180.0;

    bool isIrregular = gapOwn > irregularWarnPercent;
    if (isIrregular) irregular++;

    string number = "";
    try { var p = spatial.get_Parameter(BuiltInParameter.ROOM_NUMBER); number = p == null ? "" : (p.AsString() ?? ""); } catch { }

    rows.Add($"{number} | {spatial.Name} | {Math.Round(lengthFt * MM)} | {Math.Round(widthFt * MM)} | {angleDeg:F1}° | {roomAreaFt / SQFT_PER_SQM:F2} | {gapOwn:F1}% | {gapProj:F1}%"
        + (isIrregular ? " | ⚠ not a rectangle — split it before laying out a grid" : (Math.Abs(angleDeg) > 1.0 ? " | rotated, but a clean rectangle" : "")));
    reported++;
}

sb.AppendLine($"Room dimensions on each room's OWN axes (Finish boundary), {reported} room(s) reported:");
if (unplaced > 0 || noBoundary > 0 || notSpatial > 0)
    sb.AppendLine($"Skipped: {unplaced} unplaced (Area 0), {noBoundary} with no readable boundary, {notSpatial} not a Room/Space.");
sb.AppendLine();
sb.AppendLine("Room | Name | Length (mm) | Width (mm) | Rotation | Area (m2) | Own-box gap | Project-box gap | Note");
sb.AppendLine("--- | --- | --- | --- | --- | --- | --- | --- | ---");
foreach (var r in rows) sb.AppendLine(r);
sb.AppendLine();
sb.AppendLine("Length is always the longer side. Rotation is from project X; 0° means square to project north.");
sb.AppendLine("Own-box gap is how much bigger the room's OWN rectangle is than the room — that is the real irregularity.");
sb.AppendLine("Project-box gap is how much bigger the PROJECT-ALIGNED box is — a large gap here with a small gap on the left means the room is simply ROTATED, not irregular.");
if (irregular > 0)
    sb.AppendLine($"⚠ {irregular} room(s) are genuinely not rectangular (own-box gap over {irregularWarnPercent}%). A single width x length does not describe those.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
