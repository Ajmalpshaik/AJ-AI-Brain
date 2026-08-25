// ============================================================
// *** NOT CHECKED — this script has NOT been run against the live model. Only a targeted reflection check
// confirmed the less-common API members it calls actually exist on this Revit version (Connector.AllRefs
// walk, Document.Create.NewTakeoffFitting, MEPSystemClassification, etc.) — that catches "this method
// doesn't exist" bugs, NOT wrong logic, wrong math, or wrong parameters. Needs a real terminal + main duct
// layout to actually execute and verify. ***
// SCRIPT: connect-terminal-branch.cs
// PURPOSE: Connect one air terminal to the main duct — vertical riser up to the main duct's height,
//          a real elbow fitting at the turn, then a horizontal run tapped into the main duct via a
//          takeoff tee. Skips the horizontal segment entirely if the terminal already lines up under
//          the main duct's line (near-zero offset would throw a minimum-length error).
// SOURCE:  ../../knowledge/live-model/hvac-ducts.md § Branch duct from a terminal to a main duct
// STATUS:  living document — refine in place, don't fork a v2 file.
// GOTCHA (flagged 2026-07-23 static review): the riser is drawn VERTICALLY by design — correct for a
//         ceiling diffuser whose duct connector points up, but a side-inlet terminal (connector pointing
//         horizontally) would get a riser misaligned with its connector. Before running on an unfamiliar
//         terminal family, check termConn.CoordinateSystem.BasisZ is roughly vertical (same
//         read-the-real-direction lesson as AGENT-SPEC.md §6.1).
//
// ✱✱ FOUR FRAGMENTS JOIN MEP TOGETHER AND THEY BUILD DIFFERENT AMOUNTS OF DUCTWORK. Pick by how
//    much already exists, because the wrong one either builds a system you did not ask for or fails
//    for want of one:
//      actions/structural-changes/action-connect-air-terminals.cs   The duct ALREADY RUNS PAST the
//                                                    terminals. Revit cuts the tap itself. Nothing new
//                                                    is drawn. "connect the terminals to the duct",
//                                                    "tap these diffusers into that main".
//      recipes/connect-terminal-branch.cs            ONE terminal, and the branch to the main DOES NOT
//                                                    EXIST yet. Draws the vertical riser, a real elbow
//                                                    at the turn, then the horizontal into a takeoff
//                                                    tee. Use when the tap alone will not reach.
//      recipes/connect-equipment-to-air-terminals.cs THE WHOLE ROOM AT ONCE, from equipment. Builds the
//                                                    main trunk out of the FCU, a tap per terminal, the
//                                                    branches, the drops, and caps the trunk past the
//                                                    last branch. Ajmal own connection method. This one
//                                                    CREATES A SYSTEM - do not reach for it when a duct
//                                                    is already there and only the taps are missing.
//      actions/structural-changes/action-connect-open-connectors.cs  CLEANUP, builds nothing. Joins
//                                                    pairs that already touch but that Revit does not
//                                                    think are connected - after a copy/paste, after a
//                                                    link is bound, after a run drawn leg by leg.
//    To CHECK rather than change, the connectivity fragments are separate again: verify-duct-connectivity.cs
//    (terminal to FCU chain), action-check-system-connectivity.cs (how many pieces is this really in),
//    action-check-open-pipe-ends.cs and action-find-dead-end-system.cs. Measured 2026-08-24: the plain
//    phrase "connect the air terminals to the duct" returned the WHOLE-SYSTEM recipe at #1 and the
//    tap-into-existing-duct fragment at #3, which is why this table is in all four files.
// ============================================================
// Because supply/return terminals are checkerboard-alternated, "nearest terminal" is frequently the
// WRONG system type — this script is meant to be called once per terminal already filtered by system.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId terminalId = ElementId.InvalidElementId;
ElementId mainDuctId = ElementId.InvalidElementId; // the true trunk segment, not a previously-placed branch
// ---- END INPUTS ----

if (terminalId == ElementId.InvalidElementId || mainDuctId == ElementId.InvalidElementId)
{
    return "Set terminalId and mainDuctId explicitly in INPUTS before running.";
}

var terminal = Document.GetElement(terminalId) as FamilyInstance;
var mainDuct = Document.GetElement(mainDuctId) as Duct;
if (terminal == null || mainDuct == null)
{
    return "terminalId or mainDuctId does not point to the expected element type.";
}

var terminalConnectorSet = terminal.MEPModel?.ConnectorManager?.Connectors;
var termConn = terminalConnectorSet == null
    ? null
    : terminalConnectorSet.Cast<Connector>().FirstOrDefault(c => c.Domain == Domain.DomainHvac);
if (termConn == null) return "No HVAC connector found on the terminal.";

var mainCurve = (mainDuct.Location as LocationCurve)?.Curve;
if (mainCurve == null) return "Main duct does not have a valid location curve.";

double mainZ = mainCurve.GetEndPoint(0).Z;

var sb = new System.Text.StringBuilder();

ElementId ductTypeId = mainDuct.GetTypeId();
ElementId branchLevelId = mainDuct.LevelId != ElementId.InvalidElementId ? mainDuct.LevelId : terminal.LevelId;
if (branchLevelId == ElementId.InvalidElementId) return "Could not resolve a level for the branch duct.";

ElementId systemTypeId = ElementId.InvalidElementId;
var systemTypeParam = mainDuct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
if (systemTypeParam != null) systemTypeId = systemTypeParam.AsElementId();
if (systemTypeId == ElementId.InvalidElementId && mainDuct.MEPSystem != null)
{
    systemTypeId = mainDuct.MEPSystem.GetTypeId();
}
if (systemTypeId == ElementId.InvalidElementId)
{
    var fallbackSystemType = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
        .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>()
        .FirstOrDefault(st => st.SystemClassification == MEPSystemClassification.SupplyAir);
    if (fallbackSystemType == null) return "Could not resolve a duct system type for the branch.";
    systemTypeId = fallbackSystemType.Id;
    sb.AppendLine("WARNING: main duct system type was not readable - used first Supply Air system type as fallback.");
}

XYZ riserTop = new XYZ(termConn.Origin.X, termConn.Origin.Y, mainZ);
var projectedToMain = mainCurve.Project(riserTop);
if (projectedToMain == null) return "Terminal does not project onto the main duct curve.";

var tapPointOnMain = projectedToMain.XYZPoint;
double distToTermXY = new XYZ(termConn.Origin.X, termConn.Origin.Y, 0)
    .DistanceTo(new XYZ(tapPointOnMain.X, tapPointOnMain.Y, 0));

// distToTermXY measures to the main's CENTRELINE, so a horizontal leg shorter than the main's own
// half-width lies entirely INSIDE the main's body — the takeoff then fails and rolls everything back
// (found 2026-08-25; the old guard was 3 mm, two orders of magnitude under a real duct). Refuse
// up front with the number, instead of failing mid-transaction.
double mainHalfWidthFt = (mainDuct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.AsDouble()
    ?? mainDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsDouble() ?? 0) / 2.0;
double nearGuardFt = 3 / 304.8; // ~zero: terminal directly under the main — the direct-tap path below
if (distToTermXY >= nearGuardFt && distToTermXY < mainHalfWidthFt + 50 / 304.8)
    return $"Terminal sits {distToTermXY * 304.8:F0}mm off the main's centreline, inside the main's own"
        + $" half-width ({mainHalfWidthFt * 304.8:F0}mm) — a horizontal branch cannot fit. Either it is close"
        + " enough to tap straight down (move it onto the centreline) or the main needs rerouting.";

// A ROUND terminal connector has no Width/Height — reading them throws. Read the size by shape once,
// and set the matching parameter on each created duct (2026-08-25; the sibling recipe already did this).
bool termRound = termConn.Shape == ConnectorProfileType.Round;
double termW = termRound ? 0 : termConn.Width;
double termH = termRound ? 0 : termConn.Height;
double termDia = termRound ? termConn.Radius * 2 : 0;

using (var t = new Transaction(Document, "AJ Tools - Connect Terminal Branch"))
{
    t.Start();
    try
    {

    // Vertical riser.
    var riser = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, systemTypeId, ductTypeId, branchLevelId, termConn.Origin, riserTop);
    if (termRound) riser.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(termDia);
    else
    {
        riser.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(termW);
        riser.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(termH);
    }
    Document.Regenerate();

    var riserConns = riser.ConnectorManager.Connectors.Cast<Connector>().ToList();
    var riserBottom = riserConns.OrderBy(c => c.Origin.DistanceTo(termConn.Origin)).First();
    var riserTopConn = riserConns.OrderBy(c => c.Origin.DistanceTo(riserTop)).First();
    termConn.ConnectTo(riserBottom);

    double minLengthFt = 3 / 304.8; // ~1/10 inch guard

    if (distToTermXY < minLengthFt)
    {
        // Near-zero offset — tap the riser's own top connector straight into the main duct, no elbow.
        Document.Create.NewTakeoffFitting(riserTopConn, mainDuct);
        sb.AppendLine("Terminal lines up under the main duct — riser tapped directly, no elbow needed.");
    }
    else
    {
        var tapPoint = tapPointOnMain;
        var horiz = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, systemTypeId, ductTypeId, branchLevelId, riserTop, tapPoint);
        if (termRound) horiz.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(termDia);
        else
        {
            horiz.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(termW);
            horiz.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(termH);
        }
        Document.Regenerate();

        var horizConns = horiz.ConnectorManager.Connectors.Cast<Connector>().ToList();
        var horizNearRiser = horizConns.OrderBy(c => c.Origin.DistanceTo(riserTop)).First();
        var horizFar = horizConns.OrderBy(c => c.Origin.DistanceTo(tapPoint)).First();

        // Real elbow fitting at the turn — NOT a bare ConnectTo (that makes a logical link, no geometry).
        Document.Create.NewElbowFitting(riserTopConn, horizNearRiser);
        Document.Create.NewTakeoffFitting(horizFar, mainDuct);
        sb.AppendLine("Riser + elbow + horizontal run, tapped into main duct via takeoff.");
    }

    t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        return $"FAILED - rolled back branch connection, nothing changed. Reason: {ex.Message}";
    }
}

return sb.ToString();
