// ============================================================
// SCRIPT: set-reducer-offset-from-takeoff.cs
// PURPOSE: Slide every trunk REDUCER (rectangular transition) so it sits a fixed distance downstream of
//          the branch takeoff before it — Ajmal's rule, his words 2026-08-25: *"from that branch takeoff,
//          200 mm there reducer need. No need extra length."* After Revit's duct sizing the reducers land
//          wherever the size change happened to fall; this puts them where he wants them built.
// SOURCE:  ../../knowledge/live-model/hvac-duct-sizing.md § Where the reducer goes after a takeoff
// STATUS:  living document - refine in place, don't fork a v2 file.
//
// ✱✱ RUN THIS AFTER SIZING, NEVER BEFORE. It moves fittings that already exist; it does not size
//    anything and does not create or delete a single element. Sizing puts the reducers in, this tidies
//    where they sit. Measured on a 14-room floor: they landed 268 to 1796 mm from their takeoff
//    (average 587), all 45 corrected to 200 in one pass with zero open ends afterwards.
//
// ✱✱ HOW IT MOVES THEM, AND WHY THAT IS SAFE. `ElementTransformUtils.MoveElement` on the FITTING is what
//    the UI does when you drag it: Revit shortens the duct on one side and lengthens the other and keeps
//    both joined. Verified on a real move — an 826 mm upstream duct became 732 and the 1252 mm downstream
//    duct became 1347, both still reporting zero open connectors. Do NOT try to do this by rewriting the
//    ducts' LocationCurve; that is the way to break the joints.
//
// GOTCHA: THE MEASUREMENT IS TAKEOFF CENTRELINE -> REDUCER UPSTREAM FACE. Ajmal chose that datum
//         (2026-08-25) over edge-to-face and centre-to-centre because it is the one that sets out on
//         site. `targetMm` is that distance. The "upstream face" is the connector on the BIGGER side —
//         air flows big to small, so the big end is always the one facing the takeoff.
// GOTCHA: A REDUCER TOUCHING MECHANICAL EQUIPMENT IS LEFT ALONE and that guard matters. Every FCU has a
//         transition sitting on its supply connector (e.g. 850x195 -> 400x400) with nothing upstream of
//         it at all. Without the guard, the search happily matched a takeoff on a BRANCH 2225 mm away
//         and would have dragged the transition off the unit. 15 of them on that floor.
// GOTCHA: A REDUCER WITH NO TAKEOFF UPSTREAM IS LEFT ALONE — on that floor, 61 of them were the
//         200x200 -> 225x225 at each diffuser drop. They are not on a trunk and there is nothing to
//         measure from. Silence about them is correct, not a miss.
// GOTCHA: NO ElementId.IntegerValue ANYWHERE IN THIS FILE. It is gone in Revit 2027, and this very file
//         failed the 2027 compile on its first write for exactly two uses of it - a category comparison
//         and a value carried in a tuple only so it could be printed. Compare categories the way the rest
//         of the library does, `Category.Id == new ElementId(BuiltInCategory.X)`, and print an ElementId
//         directly. See knowledge/revit-version-compatibility.md.
// GOTCHA: `lateralToleranceMm` exists because a takeoff does NOT sit on the trunk centreline — its
//         insertion point is offset to the side (150-250 mm was typical). Distance is measured ALONG the
//         reducer's own axis and the sideways component is ignored; too tight a tolerance finds nothing.
// GOTCHA: it refuses a move that would leave less than `minRemainingDuctMm` of the upstream duct, and
//         REPORTS the refusal. Better a reported skip than a segment eaten on a tight layout.
// RELATED: recipes/slice-trunk-for-sizing.cs (cut the trunk at each takeoff BEFORE sizing);
//          actions/structural-changes/action-auto-size-duct.cs (the sizing itself);
//          recipes/hvac-room-supply-ducting.cs / hvac-floor-supply-ducting.cs (build the system first).
// ⚠ THE TECHNIQUE IS LIVE-PROVEN, THIS FILE AS WRITTEN IS NOT — the 45-fitting run above was made with
//   the same logic run inline on 2026-08-25, before it was saved here. Dry-run it once and read the plan.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
double targetMm = 200;              // takeoff CENTRELINE -> reducer UPSTREAM FACE. Ajmal's standard.
double searchUpstreamMm = 4000;     // how far back along the trunk to look for the takeoff
double lateralToleranceMm = 600;    // a takeoff sits offset from the trunk centreline - see the gotcha
double minRemainingDuctMm = 100;    // refuse a move that would leave less upstream duct than this
int onlyRoomId = 0;                 // 0 = whole model; else only reducers standing inside that room
bool dryRun = true;                 // true = print the plan, move nothing
// ---- END INPUTS ----

const double MM = 304.8;
var sb = new System.Text.StringBuilder();

Func<FamilyInstance, XYZ> centre = fi => {
    double x = 0, y = 0, z = 0; int n = 0;
    foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors) { x += c.Origin.X; y += c.Origin.Y; z += c.Origin.Z; n++; }
    return n == 0 ? null : new XYZ(x / n, y / n, z / n);
};

BoundingBoxXYZ scope = null;
if (onlyRoomId != 0) {
    var rm = Document.GetElement(new ElementId(onlyRoomId));
    if (rm == null) return $"onlyRoomId {onlyRoomId} is not an element in this model.";
    scope = rm.get_BoundingBox(null);
    if (scope == null) return $"onlyRoomId {onlyRoomId} has no bounding box - is it a placed Room?";
}

var fittings = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctFitting)
    .WhereElementIsNotElementType().Cast<FamilyInstance>()
    .Where(f => f.MEPModel != null && f.MEPModel.ConnectorManager != null).ToList();
var takeoffs = fittings.Where(f => f.Symbol.FamilyName.IndexOf("Takeoff", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
var reducers = fittings.Where(f => f.Symbol.FamilyName.IndexOf("Transition", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

if (reducers.Count == 0) return "No rectangular transitions (reducers) in this model - has the ductwork been sized yet?";
if (takeoffs.Count == 0) return "No takeoffs in this model - nothing to measure the reducers from.";

sb.AppendLine($"{reducers.Count} reducer(s), {takeoffs.Count} takeoff(s). Target: {targetMm:F0} mm from takeoff centreline to reducer face.");
sb.AppendLine();

int atEquip = 0, already = 0, noTakeoff = 0, outOfScope = 0;
var plan = new List<Tuple<ElementId, XYZ, double, ElementId, double>>();   // reducer, delta, gapNow, upstreamDuct, upstreamLen
var refused = new List<string>();

foreach (var r in reducers)
{
    var cons = r.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
    if (cons.Count != 2) continue;

    if (scope != null) {
        var c0 = centre(r);
        if (c0 == null || c0.X <= scope.Min.X || c0.X >= scope.Max.X
                       || c0.Y <= scope.Min.Y || c0.Y >= scope.Max.Y) { outOfScope++; continue; }
    }

    // leave the equipment transition alone - it has nothing upstream (see the gotcha)
    bool touchesEquipment = false;
    foreach (Connector c in cons)
        foreach (Connector o in c.AllRefs)
            if (o.Owner != null && o.Owner.Category != null
                && o.Owner.Category.Id == new ElementId(BuiltInCategory.OST_MechanicalEquipment))
                touchesEquipment = true;
    if (touchesEquipment) { atEquip++; continue; }

    var big = cons.OrderByDescending(c => c.Width * c.Height).First();
    var small = cons.OrderBy(c => c.Width * c.Height).First();
    var axis = big.Origin - small.Origin;
    if (axis.GetLength() < 1e-9) continue;
    var upstream = axis.Normalize();          // big end faces the takeoff

    FamilyInstance nearest = null; double nearestAlong = double.MaxValue;
    foreach (var t in takeoffs)
    {
        var tc = centre(t); if (tc == null) continue;
        var v = tc - big.Origin;
        double along = v.DotProduct(upstream);                 // >0 means upstream of the face
        if (along <= 1e-6 || along * MM > searchUpstreamMm) continue;
        if ((v - upstream * along).GetLength() * MM > lateralToleranceMm) continue;   // ignore the sideways offset
        if (along < nearestAlong) { nearestAlong = along; nearest = t; }
    }
    if (nearest == null) { noTakeoff++; continue; }

    double gapNow = nearestAlong * MM;
    if (Math.Abs(gapNow - targetMm) < 1.0) { already++; continue; }
    double moveMm = gapNow - targetMm;                          // >0 = pull it upstream

    MEPCurve upDuct = null;
    foreach (Connector o in big.AllRefs) { var d = o.Owner as MEPCurve; if (d != null && o.Owner.Id != r.Id) upDuct = d; }
    if (upDuct == null) { noTakeoff++; continue; }
    double upLen = ((upDuct.Location as LocationCurve).Curve).Length * MM;
    if (upLen - moveMm < minRemainingDuctMm) {
        refused.Add($"reducer {r.Id}: moving {moveMm:F0} mm would leave {upLen - moveMm:F0} mm of duct {upDuct.Id} - refused");
        continue;
    }
    plan.Add(Tuple.Create(r.Id, upstream * (moveMm / MM), gapNow, upDuct.Id, upLen));
}

sb.AppendLine($"to move {plan.Count}   already at target {already}   on equipment (left alone) {atEquip}   "
            + $"no takeoff upstream (left alone) {noTakeoff}"
            + (outOfScope > 0 ? $"   outside the room {outOfScope}" : ""));
if (plan.Count > 0) {
    var gaps = plan.Select(p => p.Item3).ToList();
    sb.AppendLine($"current offsets {gaps.Min():F0} .. {gaps.Max():F0} mm (average {gaps.Average():F0})");
}
foreach (var x in refused) sb.AppendLine("  " + x);

if (dryRun) {
    sb.AppendLine();
    foreach (var p in plan.Take(25))
        sb.AppendLine($"  reducer {p.Item1} at {p.Item3:F0} mm -> move {p.Item3 - targetMm:F0} mm (upstream duct {p.Item4} is {p.Item5:F0} mm)");
    if (plan.Count > 25) sb.AppendLine($"  ... and {plan.Count - 25} more");
    sb.AppendLine();
    sb.AppendLine("DRY RUN - nothing moved. Set dryRun = false to apply.");
    return sb.ToString();
}

// ---- move, each in its own transaction so one refusal costs only itself ----
int moved = 0;
var failures = new List<string>();
foreach (var p in plan)
{
    using (var t = new Transaction(Document, "AJ Tools - reducer offset from takeoff")) {
        t.Start();
        try { ElementTransformUtils.MoveElement(Document, p.Item1, p.Item2); Document.Regenerate(); t.Commit(); moved++; }
        catch (Exception ex) { if (t.HasStarted()) t.RollBack(); failures.Add($"{p.Item1}: {ex.Message}"); }
    }
}

// ---- verify: re-measure every reducer this run was responsible for ----
int verified = 0; var wrong = new List<string>();
foreach (var p in plan)
{
    var r = Document.GetElement(p.Item1) as FamilyInstance;
    if (r == null) { wrong.Add($"{p.Item1} vanished"); continue; }
    var cons = r.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
    var big = cons.OrderByDescending(c => c.Width * c.Height).First();
    var small = cons.OrderBy(c => c.Width * c.Height).First();
    var up = (big.Origin - small.Origin).Normalize();
    double bestA = double.MaxValue;
    foreach (var t in takeoffs) {
        var tc = centre(t); if (tc == null) continue;
        var v = tc - big.Origin;
        double a = v.DotProduct(up);
        if (a <= 1e-6 || a * MM > searchUpstreamMm) continue;
        if ((v - up * a).GetLength() * MM > lateralToleranceMm) continue;
        if (a < bestA) bestA = a;
    }
    if (bestA != double.MaxValue && Math.Abs(bestA * MM - targetMm) < 1.5) verified++;
    else wrong.Add($"{p.Item1} reads {(bestA == double.MaxValue ? -1 : bestA * MM):F0} mm");
}

int openEnds = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType().Cast<MEPCurve>()
    .Sum(d => d.ConnectorManager.Connectors.Cast<Connector>().Count(c => !c.IsConnected));

sb.AppendLine();
sb.AppendLine($"MOVED {moved} of {plan.Count}. Verified at {targetMm:F0} mm: {verified}. Open duct ends model-wide: {openEnds}.");
foreach (var f in failures.Take(8)) sb.AppendLine("  FAILED " + f);
foreach (var w in wrong.Take(8)) sb.AppendLine("  NOT AT TARGET " + w);
if (openEnds > 0) sb.AppendLine("  ⚠ open ends appeared - a move pulled something apart, check before going further.");
return sb.ToString();
