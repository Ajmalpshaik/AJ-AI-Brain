// ============================================================
// SCRIPT: slice-trunk-for-sizing.cs
// PURPOSE: Slice a main HVAC trunk duct into separate segments at each terminal-branch takeoff point,
//          offset downstream past the takeoff's own body + a clearance margin, so each resulting segment
//          can later be individually sized down after its branch removes some airflow. Skips the cut after
//          the LAST takeoff before the end cap (that segment runs through in one piece to the cap).
// SOURCE:  ../../knowledge/live-model/hvac-duct-sizing.md § Slicing a main trunk into segments for duct sizing
// STATUS:  living document - refine in place, don't fork a v2 file.
// ============================================================
// HIGH RISK - a past session's first attempt (slice at the takeoff's own center, then relocate the joint
// by editing LocationCurve.Curve) silently deleted a takeoff fitting and orphaned its whole branch, with
// the terminal's own IsConnected still reading true. This script uses the corrected technique instead:
// compute the offset break point FIRST, then BreakCurve directly at that point on the still-whole trunk -
// never slice-then-relocate. Even so: test on one room/trunk first, then run
// verify-duct-connectivity.cs (full BFS trace, not just IsConnected) before rolling out further, and check
// with the user after the first one. Live-verified 2026-07-17: 3 cuts on a 4-row/8-terminal trunk, all 8
// terminals still traced to the FCU afterward.
//
// ✓ RE-VERIFIED 2026-08-14 on a purpose-built multi-branch fixture - the fixture the 2026-07-23 note
//   below says was unavailable. A 12 m trunk in 3 pieces with 4 takeoffs (x = 2000, 5000, 7000, 10000).
//   Result: 4 takeoffs seen, grouped into 4 cut positions, last skipped, 3 cuts made at 2700/5700/7700 mm
//   (each takeoff + 700 mm offset), 0 failed, 3 union fittings all connected, trunk 3 pieces -> 6. Then a
//   full BFS trace over real connector links: 4 of 4 branches still reach the far end of the trunk.
//   THE FIXTURE WAS BUILT BY API IN A BLANK PROJECT (Duct.Create + Document.Create.NewTakeoffFitting) -
//   it never had to be found in a real model, which is what had blocked this for three weeks.
//
// BUG FOUND AND FIXED IN THAT RUN - see the onTrunkLine comment below. `Line.Distance()` on a bound
//   LocationCurve measures to the SEGMENT, so a multi-piece trunk only ever found the piece whose Id was
//   passed in. With 4 takeoffs it saw 1, skipLastTakeoff removed it, and the script printed
//   "0 cut(s) made, 0 failed" - a success message for doing nothing.
//
// CAUTION (2026-07-23, code-review only - not re-tested this pass, no matching multi-branch trunk fixture
// available): `trunkDir` is derived from `trunkPieceId`'s OWN stored point-0/point-1 order (whichever end
// Revit happens to call "0"), not from any independent FCU/cap reference. `skipLastTakeoff` removes the
// group with the LARGEST signed distance along that direction - correct ONLY if trunkDir happens to point
// FCU-side -> cap-side. If the piece you pass in was drawn in the opposite direction, "skip the last
// takeoff" would silently skip the cut nearest the FCU instead of the one nearest the cap, and cut right
// before the actual end cap instead - no exception, no warning, just the wrong segment left whole. Sibling
// script split-duct-near-equipment.cs had a related BreakCurve Id-swap bug that looked fine until checked
// geometrically (fixed 2026-07-23, see that file's header) - this script already defends against THAT
// specific gotcha correctly (re-locates each piece by geometric containment every cut, never trusts an Id
// across cuts), but the trunkDir sign issue above is a separate, still-open risk. Before trusting
// skipLastTakeoff on a new trunk: confirm which end is actually the cap (e.g. compare the reported cut
// distances against the known FCU location) rather than assuming.
// AND THE SAME SIGN RISK REACHES EVERY CUT, not just skipLastTakeoff (recorded 2026-08-25): the cut
// offset is applied on the +trunkDir side of each takeoff group unconditionally, so with trunkDir
// pointing cap->FCU every cut lands UPSTREAM of its takeoff and each segment is bounded on the wrong
// side - the later per-segment sizing then sizes each length against its neighbour's flow, silently.
// Until a direction reference input exists, the same manual check covers both: confirm the reported
// first cut sits on the CAP side of the first takeoff before letting a sizing run trust the segments.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
ElementId trunkPieceId = ElementId.InvalidElementId; // any one Duct element that is part of the trunk
double marginMm = 500;              // clearance beyond the TRUNK's half-width (read once from the piece
                                    // you name — not each takeoff's own size), per-request
double groupToleranceMm = 50;       // takeoffs within this distance along the trunk are one cut point
                                     // (checkerboard rooms put two takeoffs at ~the same position from
                                     // opposite sides - group them so you don't try to cut the same point twice)
bool skipLastTakeoff = true;        // per convention: never cut after the takeoff closest to the end cap
// ---- END INPUTS ----

if (trunkPieceId == ElementId.InvalidElementId)
{
    return "Set trunkPieceId explicitly in INPUTS before running - any one Duct element that's part of the trunk.";
}

var sb = new System.Text.StringBuilder();

var startPiece = Document.GetElement(trunkPieceId) as Autodesk.Revit.DB.Mechanical.Duct;
if (startPiece == null) return "trunkPieceId does not point to a Duct element.";

var startCurve = (startPiece.Location as LocationCurve)?.Curve as Line;
if (startCurve == null) return "Trunk piece does not have a straight LocationCurve.";

XYZ trunkDir = (startCurve.GetEndPoint(1) - startCurve.GetEndPoint(0)).Normalize();
XYZ refPoint = startCurve.GetEndPoint(0);
// Width, height, or DIAMETER — a round trunk has neither Width nor Height, and the old `?? 0`
// fallback silently shrank the takeoff-clearance offset to the margin alone, cutting close enough
// to delete the takeoff (the exact failure this file's HIGH RISK note describes). Found 2026-08-25;
// the 2026-08-14 fixture was rectangular, so the round path had never run. No usable size = refuse.
double widthFt = startPiece.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble()
    ?? startPiece.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.AsDouble()
    ?? startPiece.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsDouble() ?? 0;
if (widthFt <= 0) return "Could not read the trunk's Width/Height/Diameter — refusing to cut: the "
    + "takeoff-clearance offset would collapse to the margin alone and risk slicing through a takeoff.";
double halfWidthFt = widthFt / 2.0;
double marginFt = marginMm / 304.8;
double groupToleranceFt = groupToleranceMm / 304.8;
double offsetFt = halfWidthFt + marginFt;

// Find every collinear trunk piece (same INFINITE line as the starting piece) so multi-piece trunks
// are handled too.
//
// FIXED 2026-08-14 — this was measurably broken and failed SILENTLY. It used
// `startCurve.Distance(point)`, and a Revit `Line` from a LocationCurve is BOUND (IsBound=True), so
// Distance() returns the distance to that SEGMENT, not to the line it lies on. On a 3-piece 12 m trunk
// the starting piece spanned x 0-4000 and the test measured 4000 mm and 8000 mm to the other two
// pieces' far ends, excluding both. Only the piece whose Id was passed in was ever found.
//
// The damage was silent, which is what makes it worth this comment: with 4 takeoffs on the trunk it
// saw 1, grouped it into a single cut position, `skipLastTakeoff` removed that one, and the script
// reported "0 cut(s) made, 0 failed" — a clean success message for having done nothing at all.
//
// Perpendicular distance to the infinite line instead: project onto trunkDir, subtract, measure what
// is left over.
Func<XYZ, double> perpDistanceToTrunkLine = p =>
{
    XYZ v = p - refPoint;
    return (v - trunkDir * v.DotProduct(trunkDir)).GetLength();
};
Func<Autodesk.Revit.DB.Mechanical.Duct, bool> onTrunkLine = d =>
{
    var lc = (d.Location as LocationCurve)?.Curve as Line;
    if (lc == null) return false;
    return perpDistanceToTrunkLine(lc.GetEndPoint(0)) < 0.05 && perpDistanceToTrunkLine(lc.GetEndPoint(1)) < 0.05
        && (lc.Direction.IsAlmostEqualTo(trunkDir) || lc.Direction.IsAlmostEqualTo(-trunkDir));
};
Func<List<Autodesk.Revit.DB.Mechanical.Duct>> findTrunkPieces = () =>
    new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves)
        .WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Mechanical.Duct>()
        .Where(onTrunkLine).ToList();

// Collect every takeoff (Curve-type) connector across all current trunk pieces, projected to a distance
// along trunkDir from refPoint.
var takeoffDistances = new List<double>();
foreach (var piece in findTrunkPieces())
{
    foreach (Connector c in piece.ConnectorManager.Connectors)
    {
        if (c.ConnectorType != ConnectorType.Curve) continue;
        double dist = (c.Origin - refPoint).DotProduct(trunkDir);
        takeoffDistances.Add(dist);
    }
}
if (takeoffDistances.Count == 0) return "No takeoff (Curve-type) connectors found on the trunk - nothing to slice at.";

// Group nearby takeoffs (checkerboard: two terminals per row tap at ~the same position from opposite sides).
takeoffDistances.Sort();
var groups = new List<double>(); // one representative distance per group
double groupStart = takeoffDistances[0];
double groupSum = takeoffDistances[0];
int groupCount = 1;
for (int i = 1; i < takeoffDistances.Count; i++)
{
    if (takeoffDistances[i] - takeoffDistances[i - 1] <= groupToleranceFt)
    {
        groupSum += takeoffDistances[i];
        groupCount++;
    }
    else
    {
        groups.Add(groupSum / groupCount);
        groupStart = takeoffDistances[i];
        groupSum = takeoffDistances[i];
        groupCount = 1;
    }
}
groups.Add(groupSum / groupCount);
sb.AppendLine($"Found {takeoffDistances.Count} takeoff connector(s) grouped into {groups.Count} cut position(s) (tolerance {groupToleranceMm}mm).");

if (skipLastTakeoff && groups.Count > 0)
{
    sb.AppendLine($"Skipping the cut after the last group (closest to the end cap), per convention.");
    groups.RemoveAt(groups.Count - 1);
}

// Re-locate the correct current trunk piece for each cut geometrically (BreakCurve reassigns which
// element Id keeps which segment, so don't trust an Id across cuts).
Func<double, Autodesk.Revit.DB.Mechanical.Duct> findPieceContainingDistance = (d) =>
{
    foreach (var piece in findTrunkPieces())
    {
        var lc = (piece.Location as LocationCurve).Curve as Line;
        double d0 = (lc.GetEndPoint(0) - refPoint).DotProduct(trunkDir);
        double d1 = (lc.GetEndPoint(1) - refPoint).DotProduct(trunkDir);
        double dMin = Math.Min(d0, d1), dMax = Math.Max(d0, d1);
        if (d > dMin + 0.05 && d < dMax - 0.05) return piece;
    }
    return null;
};

int cutCount = 0, failCount = 0;
foreach (var groupDist in groups)
{
    double cutDist = groupDist + offsetFt;
    var piece = findPieceContainingDistance(cutDist);
    if (piece == null)
    {
        sb.AppendLine($"Cut at distance {cutDist*304.8:F0}mm: FAILED - no current trunk piece contains this point (offset may run past the next group or off the trunk's end).");
        failCount++;
        continue;
    }

    XYZ breakPt = refPoint + trunkDir * cutDist;
    using (var t = new Transaction(Document, "AJ Tools - Slice Trunk for Sizing"))
    {
        t.Start();
        try
        {
            var newId = Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, piece.Id, breakPt);
            var origFresh = Document.GetElement(piece.Id) as Autodesk.Revit.DB.Mechanical.Duct;
            var newFresh = Document.GetElement(newId) as Autodesk.Revit.DB.Mechanical.Duct;
            var origConn = origFresh.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
            var newConn = newFresh.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
            // Real Union fitting, NOT a bare ConnectTo - a fitting-less direct duct-duct joint can be
            // silently re-merged by Revit later, losing the split entirely (live-proven 2026-07-26).
            var union = Document.Create.NewUnionFitting(origConn, newConn);
            if (union == null) throw new InvalidOperationException("NewUnionFitting returned null - no union family available for this duct type.");
            t.Commit();
            sb.AppendLine($"Cut at {cutDist*304.8:F0}mm along trunk: split piece {piece.Id} -> new piece {newId}, union {union.Id} joins them (IsConnected={origConn.IsConnected && newConn.IsConnected}).");
            cutCount++;
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"Cut at {cutDist*304.8:F0}mm: FAILED - rolled back, nothing changed. Reason: {ex.Message}");
            failCount++;
        }
    }
}

sb.AppendLine($"\n{cutCount} cut(s) made, {failCount} failed.");
sb.AppendLine("IMPORTANT: now run verify-duct-connectivity.cs (full BFS trace per terminal) before trusting this - " +
    "never rely on IsConnected alone after a trunk slice.");
return sb.ToString();
