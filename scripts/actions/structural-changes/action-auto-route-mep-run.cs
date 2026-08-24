// ============================================================
// FRAGMENT (action) — action-auto-route-mep-run.cs
// PURPOSE: Route a duct or pipe from POINT A to POINT B as a real connected run — orthogonal legs with
//          a proper elbow fitting at every turn, sized, on a system, all in one transaction group.
//          The "just get a service from here to there" job that previously meant drawing each leg by
//          hand with create-duct.cs / create-pipe.cs and then joining nothing.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Run it on its own, not after a filter.
//
// ✱✱ REVIT HAS NO "AUTO-ROUTE" API AND THIS IS NOT PRETENDING TO BE ONE. There is no solver here that
//    avoids beams, dodges other services or picks a clever path. What it does is turn two points into an
//    ORTHOGONAL DOG-LEG (move along one axis, then the next, then the last), build a real segment per
//    leg, and put a genuine elbow between consecutive legs via Document.Create.NewElbowFitting. That is
//    the part that was slow and error-prone by hand. CHOOSING the path is still yours — set `routeOrder`.
//
// ✱✱ `routeOrder` IS THE WHOLE DESIGN DECISION, so it is an input with no safe default. "ZXY" rises to
//    the target height first, then runs X, then Y — usually right for a service leaving equipment and
//    getting up into the ceiling void. "XYZ" runs flat first and climbs last. The two produce completely
//    different routes between the same two points; there is no "correct" one to guess at.
//
// ✱✱ A ZERO-LENGTH LEG IS SKIPPED, NOT BUILT. If A and B share a Z, the Z leg is nothing — creating a
//    zero-length duct throws, and worse, a near-zero one (under `minLegMm`) creates a sliver segment that
//    Revit accepts and nobody can select. Legs shorter than `minLegMm` are dropped and REPORTED, and the
//    elbow is made between the two legs that survive.
//
// GOTCHA: DRY RUN BY DEFAULT — it prints the legs it would build, with lengths, and changes nothing.
//         Read the route, then set dryRun = false.
// GOTCHA: ELBOWS COME FROM THE ROUTING PREFERENCES OF THE TYPE, not from this fragment. If the duct/pipe
//         type has no elbow set in its Routing Preferences, NewElbowFitting fails and the legs stay as
//         disconnected segments — reported per joint, never counted as connected. Check the type first
//         with action-report-routing-preferences.cs.
// GOTCHA: SIZE IS SET AFTER CREATION, and a round size on a rectangular type (or the reverse) is refused
//         by Revit. Both are attempted and whichever the type accepts is reported; a type that takes
//         neither is a type/size mismatch, not a script fault.
// GOTCHA: this connects NOTHING at the two open ends — it routes A to B and leaves both ends free on
//         purpose, because what sits at each end (equipment, a tee off an existing main, a cap) is a
//         separate decision. Use action-connect-open-connectors.cs or action-connect-air-terminals.cs
//         after it.
// RELATED: creators/create-duct.cs and creators/create-pipe.cs (one straight segment, no fittings);
//          recipes/connect-terminal-branch.cs (the specific terminal-to-trunk case, already proven);
//          actions/reporting/action-plan-shortest-route.cs (which ORDER to connect many elements in).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Route ONE run first, look at it in a section,
//   and confirm the elbows are real fittings (select one, it should be a Duct/Pipe Fitting) before
//   routing a floor's worth.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = print the legs only, build nothing

string mepKind = "duct";             // "duct" | "pipe"

double startXmm = 0, startYmm = 0, startZmm = 3000;   // point A, project coordinates, mm
double endXmm = 6000, endYmm = 4000, endZmm = 3500;   // point B, project coordinates, mm

string routeOrder = "ZXY";           // order the legs are built in: any arrangement of X, Y, Z

string typeName = "";                // duct/pipe TYPE name; "" = the first one in the project
string systemTypeName = "";          // system type name (e.g. "Supply Air", "Domestic Cold Water"); "" = first
string levelName = "";               // level to host the run on; "" = the lowest level

bool roundSize = true;               // true = round (diameter); false = rectangular (width x height)
double diameterMm = 200;             // used when roundSize
double widthMm = 400, heightMm = 200; // used when !roundSize

double minLegMm = 50;                // legs shorter than this are dropped rather than built
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

bool isDuct = mepKind.Trim().ToLower() == "duct";

var pA = new XYZ(ToFeet(startXmm), ToFeet(startYmm), ToFeet(startZmm));
var pB = new XYZ(ToFeet(endXmm), ToFeet(endYmm), ToFeet(endZmm));

sb.AppendLine($"ROUTE {mepKind.ToUpper()}  A ({startXmm:F0}, {startYmm:F0}, {startZmm:F0}) -> B ({endXmm:F0}, {endYmm:F0}, {endZmm:F0}) mm");
sb.AppendLine($"Leg order: {routeOrder.ToUpper()}");

// ---- resolve the type, the system type and the level ----
ElementId runTypeId = ElementId.InvalidElementId;
ElementId systemTypeId = ElementId.InvalidElementId;
ElementId levelId = ElementId.InvalidElementId;
string runTypeLabel = "", systemLabel = "", levelLabel = "";

if (isDuct)
{
    var types = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))
        .Cast<Autodesk.Revit.DB.Mechanical.DuctType>().ToList();
    var picked = string.IsNullOrWhiteSpace(typeName)
        ? types.FirstOrDefault()
        : types.FirstOrDefault(t => t.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0);
    if (picked != null) { runTypeId = picked.Id; runTypeLabel = picked.Name; }

    var systems = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
        .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>().ToList();
    var sysPicked = string.IsNullOrWhiteSpace(systemTypeName)
        ? systems.FirstOrDefault()
        : systems.FirstOrDefault(t => t.Name.IndexOf(systemTypeName, StringComparison.OrdinalIgnoreCase) >= 0);
    if (sysPicked != null) { systemTypeId = sysPicked.Id; systemLabel = sysPicked.Name; }
}
else
{
    var types = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeType))
        .Cast<Autodesk.Revit.DB.Plumbing.PipeType>().ToList();
    var picked = string.IsNullOrWhiteSpace(typeName)
        ? types.FirstOrDefault()
        : types.FirstOrDefault(t => t.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0);
    if (picked != null) { runTypeId = picked.Id; runTypeLabel = picked.Name; }

    var systems = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystemType))
        .Cast<Autodesk.Revit.DB.Plumbing.PipingSystemType>().ToList();
    var sysPicked = string.IsNullOrWhiteSpace(systemTypeName)
        ? systems.FirstOrDefault()
        : systems.FirstOrDefault(t => t.Name.IndexOf(systemTypeName, StringComparison.OrdinalIgnoreCase) >= 0);
    if (sysPicked != null) { systemTypeId = sysPicked.Id; systemLabel = sysPicked.Name; }
}

var levels = new FilteredElementCollector(Document)
    // `Elevation` here is DELIBERATE and must not be "fixed" to ProjectElevation. This only SORTS the
    // levels to pick one; the two bases differ by a constant offset, so the resulting ORDER is identical
    // either way. Nothing in this fragment subtracts a level height from a world coordinate — the route
    // points come straight from the mm inputs — so the defect that affects other fragments cannot arise
    // here. Recorded because a sweep for `.Elevation` will find this line and it looks like the others.
    .OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
var lvl = string.IsNullOrWhiteSpace(levelName)
    ? levels.FirstOrDefault()
    : levels.FirstOrDefault(l => l.Name.IndexOf(levelName, StringComparison.OrdinalIgnoreCase) >= 0);
if (lvl != null) { levelId = lvl.Id; levelLabel = lvl.Name; }

if (runTypeId == ElementId.InvalidElementId)
    sb.AppendLine($"STOP: no {mepKind} TYPE found" + (string.IsNullOrWhiteSpace(typeName) ? " in this project at all." : $" matching '{typeName}'."));
if (systemTypeId == ElementId.InvalidElementId)
    sb.AppendLine($"STOP: no {mepKind} SYSTEM TYPE found" + (string.IsNullOrWhiteSpace(systemTypeName) ? " in this project at all." : $" matching '{systemTypeName}'."));
if (levelId == ElementId.InvalidElementId)
    sb.AppendLine("STOP: no Level found" + (string.IsNullOrWhiteSpace(levelName) ? "." : $" matching '{levelName}'."));

if (runTypeId == ElementId.InvalidElementId || systemTypeId == ElementId.InvalidElementId || levelId == ElementId.InvalidElementId)
    return sb.ToString();

sb.AppendLine($"Type: {runTypeLabel}   System: {systemLabel}   Level: {levelLabel}");
sb.AppendLine();

// ---- build the leg points from the route order ----
// Each letter moves the running point along ONE axis until it matches B on that axis. Any letter left
// out of routeOrder is simply never moved, which would leave the run short of B — that is reported.
var order = routeOrder.ToUpper().Where(c => c == 'X' || c == 'Y' || c == 'Z').Distinct().ToList();
if (order.Count == 0)
{
    sb.AppendLine("STOP: routeOrder contains none of X, Y, Z — nothing to build.");
    return sb.ToString();
}
if (order.Count < 3)
    sb.AppendLine($"NOTE: routeOrder '{routeOrder}' only moves {order.Count} of the 3 axes — the run will stop short of B on the axis/axes left out. Deliberate? If not, use a 3-letter order.");

var pts = new List<XYZ> { pA };
var running = pA;
foreach (var axis in order)
{
    XYZ next;
    if (axis == 'X') next = new XYZ(pB.X, running.Y, running.Z);
    else if (axis == 'Y') next = new XYZ(running.X, pB.Y, running.Z);
    else next = new XYZ(running.X, running.Y, pB.Z);
    pts.Add(next);
    running = next;
}

// ---- drop the legs that are too short to be real ----
var legs = new List<(XYZ From, XYZ To, double LenMm, char Axis)>();
var dropped = new List<string>();
for (int i = 0; i < pts.Count - 1; i++)
{
    double lenMm = ToMm(pts[i].DistanceTo(pts[i + 1]));
    char axis = order[i];
    if (lenMm < minLegMm)
    {
        if (lenMm > 0.0001) dropped.Add($"{axis} leg {lenMm:F1} mm (under minLegMm {minLegMm:F0})");
        continue;
    }
    legs.Add((pts[i], pts[i + 1], lenMm, axis));
}

// The legs must chain end-to-end after the drops, or an elbow would be asked to join two points that
// are not the same point. Dropping a middle leg makes exactly that hole, so rebuild the chain from the
// surviving legs rather than assuming the original list is still continuous.
for (int i = 0; i < legs.Count - 1; i++)
{
    if (legs[i].To.DistanceTo(legs[i + 1].From) > 0.0001)
    {
        var fixedLeg = legs[i + 1];
        legs[i + 1] = (legs[i].To, fixedLeg.To, ToMm(legs[i].To.DistanceTo(fixedLeg.To)), fixedLeg.Axis);
    }
}

sb.AppendLine($"LEGS ({legs.Count})");
foreach (var lg in legs)
    sb.AppendLine($"  {lg.Axis}: ({ToMm(lg.From.X):F0}, {ToMm(lg.From.Y):F0}, {ToMm(lg.From.Z):F0}) -> ({ToMm(lg.To.X):F0}, {ToMm(lg.To.Y):F0}, {ToMm(lg.To.Z):F0})   {lg.LenMm:F0} mm");
foreach (var d in dropped) sb.AppendLine($"  dropped: {d}");
sb.AppendLine($"Elbows to make: {Math.Max(0, legs.Count - 1)}");
sb.AppendLine();

if (legs.Count == 0)
{
    sb.AppendLine("Nothing to build — A and B are the same point, or every leg is under minLegMm.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine("DRY RUN — nothing was created. Set dryRun = false to build this route.");
    return sb.ToString();
}

// ---- build ----
// One TransactionGroup so a failure halfway cannot leave half a route behind (README "Transaction
// safety"): the segments and their elbows are one thing or they are nothing.
var madeIds = new List<ElementId>();
int elbowsMade = 0;
var elbowFailures = new List<string>();

using (var tg = new TransactionGroup(Document, "AJ Tools - route MEP run"))
{
    tg.Start();
    try
    {
        using (var tx = new Transaction(Document, "AJ Tools - route legs"))
        {
            tx.Start();
            try
            {
                var made = new List<MEPCurve>();
                foreach (var lg in legs)
                {
                    MEPCurve created = isDuct
                        ? (MEPCurve)Autodesk.Revit.DB.Mechanical.Duct.Create(Document, systemTypeId, runTypeId, levelId, lg.From, lg.To)
                        : (MEPCurve)Autodesk.Revit.DB.Plumbing.Pipe.Create(Document, systemTypeId, runTypeId, levelId, lg.From, lg.To);

                    // Size it. Round and rectangular are mutually exclusive per type — try what was asked
                    // for, and report rather than throw if the type refuses it.
                    try
                    {
                        if (roundSize)
                        {
                            var dp = created.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)
                                  ?? created.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                            if (dp != null && !dp.IsReadOnly) dp.Set(ToFeet(diameterMm));
                        }
                        else
                        {
                            var wp = created.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                            var hp = created.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                            if (wp != null && !wp.IsReadOnly) wp.Set(ToFeet(widthMm));
                            if (hp != null && !hp.IsReadOnly) hp.Set(ToFeet(heightMm));
                        }
                    }
                    catch (Exception sizeEx)
                    {
                        sb.AppendLine($"  NOTE: size not applied to the {lg.Axis} leg — {sizeEx.Message}");
                    }

                    made.Add(created);
                    madeIds.Add(created.Id);
                }

                Document.Regenerate();

                // ---- elbow at each joint ----
                // The two connectors to join are the ones sitting ON the shared point. Matching by
                // position is what makes this reliable: connector ORDER inside a ConnectorManager is not
                // guaranteed to follow the curve's direction, so "take connector 0 of one and 1 of the
                // other" quietly produces an elbow at the wrong end on some segments.
                Func<MEPCurve, XYZ, Connector> connectorAt = (curve, pt) =>
                {
                    Connector best = null;
                    double bestD = double.MaxValue;
                    foreach (Connector c in curve.ConnectorManager.Connectors)
                    {
                        if (c.ConnectorType != ConnectorType.End) continue;
                        double d = c.Origin.DistanceTo(pt);
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    return bestD < ToFeet(1.0) ? best : null;
                };

                for (int i = 0; i < made.Count - 1; i++)
                {
                    var joint = legs[i].To;
                    var c1 = connectorAt(made[i], joint);
                    var c2 = connectorAt(made[i + 1], joint);
                    if (c1 == null || c2 == null)
                    {
                        elbowFailures.Add($"joint {i + 1}: could not find an end connector on the shared point");
                        continue;
                    }
                    try
                    {
                        var fitting = Document.Create.NewElbowFitting(c1, c2);
                        if (fitting != null) { elbowsMade++; madeIds.Add(fitting.Id); }
                        else elbowFailures.Add($"joint {i + 1}: Revit returned no fitting");
                    }
                    catch (Exception elbowEx)
                    {
                        elbowFailures.Add($"joint {i + 1}: {elbowEx.Message}");
                    }
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                sb.AppendLine($"FAILED (route legs) — rolled back, nothing changed. Reason: {ex.Message}");
                throw;
            }
        }
        tg.Assimilate();
    }
    catch (Exception ex)
    {
        tg.RollBack();
        sb.AppendLine($"FAILED (route MEP run) — whole route rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine($"BUILT: {legs.Count} segment(s), {elbowsMade} of {Math.Max(0, legs.Count - 1)} elbow(s).");
if (elbowFailures.Count > 0)
{
    sb.AppendLine("ELBOWS NOT MADE — these joints are two segments sitting end to end, NOT connected:");
    foreach (var f in elbowFailures) sb.AppendLine($"  {f}");
    sb.AppendLine("  Usual cause: the type's Routing Preferences carry no elbow. Check with action-report-routing-preferences.cs.");
}
sb.AppendLine("Element Ids: " + string.Join(", ", madeIds.Select(i => i.ToString())));
sb.AppendLine("Both ends are left OPEN on purpose — connect them with action-connect-open-connectors.cs.");

return sb.ToString();
