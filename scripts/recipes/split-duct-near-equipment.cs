// ============================================================
// SCRIPT: split-duct-near-equipment.cs
// PURPOSE: Split a duct at a given gap distance from an equipment connector (e.g. leave a 200mm gap right
//          at an FCU for a future flex-duct connection), and explicitly reconnect the resulting joint.
//          Simpler cousin of slice-trunk-for-sizing.cs - one fixed-offset cut from one known reference
//          connector, no grouping/clustering, no "skip the last one" logic.
// SOURCE:  ../knowledge/live-model/hvac-ducts.md § Splitting an existing duct into two segments at a given point
// STATUS:  living document - refine in place, don't fork a v2 file.
// ============================================================
// NOTE: this is NOT a standing default. ajtools-hvac-duct-routing's own convention is "no split near the
// FCU - the main duct is one single piece from the FCU connector onward" (the user removed this requirement
// once before). Only run this when the user explicitly asks for a split near a specific piece of equipment
// again - don't reintroduce it as automatic behaviour.
//
// `MechanicalUtils.BreakCurve` does NOT auto-connect the two resulting segments - always explicitly
// reconnect the joint after breaking (this script does that as its own step).
//
// LIVE-VERIFIED 2026-07-23 - FOUND AND FIXED A REAL BUG: BreakCurve reassigns which element Id keeps
// which physical segment (same gotcha slice-trunk-for-sizing.cs's header already calls out) - this script
// originally assumed `duct.Id` was always the equipment-side piece and `newDuctId` always the rest-of-run
// piece. Confirmed live: `duct.Id` came back as the FAR piece and `newDuctId` came back as the
// equipment-side stub - exactly backwards from the old report line. Fixed by determining near/far
// geometrically (distance from each piece's own endpoints to the equipment connector) instead of trusting
// which variable held which Id. Re-verified: reported Ids now match actual geometry.
//
// RETRACTED FALSE ALARM (same test session): initially also concluded BreakCurve silently drops the
// equipment-side connection itself (not just the label) - traced to a mistake in the TEST FIXTURE, not this
// script. The fixture built its test duct along an assumed axis (XYZ.BasisX) instead of the equipment
// connector's own real outward direction (`Connector.CoordinateSystem.BasisZ`), so the duct never lined up
// with the connector properly to begin with; that's what produced the unreliable connect/disconnect
// behavior, not BreakCurve. Rebuilt the fixture reading the connector's real direction (confirmed
// (0,-1,0) here, not the assumed +X) and reconnected/split it correctly: the equipment connection survived
// the split fine, both before and immediately after, on a fresh independent re-fetch. LESSON: when
// connecting TO or drawing a duct FROM an equipment connector - in a test fixture or anywhere else - always
// read that connector's own `CoordinateSystem.BasisZ` first and build/extend along it; don't assume an
// axis. No code change needed for this part - the script's existing single reconnect (the new internal
// joint only) is correct as long as the duct was properly connected to begin with, which the script assumes
// as a precondition, not something it establishes itself.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
ElementId equipmentId = ElementId.InvalidElementId; // the Mechanical Equipment (or any element) whose connector is the reference point
ElementId ductId = ElementId.InvalidElementId;      // the duct connected to that equipment, to be split
double gapMm = 200;                                  // distance from the equipment's connector to the break point, per-request
// ---- END INPUTS ----

if (equipmentId == ElementId.InvalidElementId || ductId == ElementId.InvalidElementId)
{
    return "Set equipmentId and ductId explicitly in INPUTS before running.";
}

var sb = new System.Text.StringBuilder();

var equipment = Document.GetElement(equipmentId) as FamilyInstance;
var duct = Document.GetElement(ductId) as Autodesk.Revit.DB.Mechanical.Duct;
if (equipment == null || duct == null) return "equipmentId or ductId does not point to the expected element type.";

// Find the equipment connector that is actually connected to this duct (walk AllRefs, don't assume which one).
Connector equipConn = null;
foreach (Connector c in equipment.MEPModel.ConnectorManager.Connectors)
{
    if (c.Domain != Domain.DomainHvac) continue;
    foreach (Connector r in c.AllRefs)
    {
        if (r.Owner != null && r.Owner.Id == duct.Id) { equipConn = c; break; }
    }
    if (equipConn != null) break;
}
if (equipConn == null) return "Could not find a connector on the equipment that links to the given duct - check ductId is really connected to equipmentId (directly, or via a fitting in between).";

var ductCurve = (duct.Location as LocationCurve)?.Curve as Line;
if (ductCurve == null) return "Duct does not have a straight LocationCurve.";

// Reference point = the duct's own end nearest the equipment connector (may not be equipConn.Origin itself
// if a transition/elbow sits in between).
var ductEndConns = duct.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End).ToList();
var nearEquipEnd = ductEndConns.OrderBy(c => c.Origin.DistanceTo(equipConn.Origin)).First();
var farEnd = ductEndConns.OrderBy(c => c.Origin.DistanceTo(equipConn.Origin)).Last();

XYZ dir = (farEnd.Origin - nearEquipEnd.Origin).Normalize();
double gapFt = UnitUtils.ConvertToInternalUnits(gapMm, DisplayUnitType.DUT_MILLIMETERS);
XYZ breakPt = nearEquipEnd.Origin + dir * gapFt;
sb.AppendLine($"Reference (near-equipment) duct end: ({nearEquipEnd.Origin.X:F3},{nearEquipEnd.Origin.Y:F3},{nearEquipEnd.Origin.Z:F3})");
sb.AppendLine($"Break point: {gapMm}mm downstream -> ({breakPt.X:F3},{breakPt.Y:F3},{breakPt.Z:F3})");

if (ductCurve.Distance(breakPt) > 0.01)
    return "Computed break point does not sit on the duct's own curve - check gapMm isn't larger than the duct's own length before the next fitting.";

ElementId newDuctId;
using (var t = new Transaction(Document, "AJ Tools - Split Duct Near Equipment"))
{
    t.Start();
    try
    {
        newDuctId = Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, duct.Id, breakPt);
        var pieceA = Document.GetElement(duct.Id) as Autodesk.Revit.DB.Mechanical.Duct;
        var pieceB = Document.GetElement(newDuctId) as Autodesk.Revit.DB.Mechanical.Duct;

        // BreakCurve reassigns which element Id keeps which physical segment - do NOT assume duct.Id
        // is the equipment-side piece (confirmed live: it can come back as the far/rest-of-run piece
        // instead). Work out near/far geometrically, same defense slice-trunk-for-sizing.cs already uses.
        Func<Autodesk.Revit.DB.Mechanical.Duct, double> distToEquip = d =>
        {
            var lc = (d.Location as LocationCurve).Curve as Line;
            return Math.Min(lc.GetEndPoint(0).DistanceTo(equipConn.Origin), lc.GetEndPoint(1).DistanceTo(equipConn.Origin));
        };
        var nearFresh = distToEquip(pieceA) <= distToEquip(pieceB) ? pieceA : pieceB;
        var newFresh = nearFresh.Id == pieceA.Id ? pieceB : pieceA;

        var nearConn = nearFresh.ConnectorManager.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
        var newConn = newFresh.ConnectorManager.Connectors.Cast<Connector>()
            .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
        nearConn.ConnectTo(newConn);
        t.Commit();
        sb.AppendLine($"Split: equipment-side piece Id={nearFresh.Id} ({gapMm}mm long) / rest-of-run piece Id={newFresh.Id}.");
        sb.AppendLine($"Joint reconnected - IsConnected: near-side={nearConn.IsConnected}, new-side={newConn.IsConnected}.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        return $"FAILED - rolled back, nothing changed. Reason: {ex.Message}";
    }
}

sb.AppendLine("Verify with verify-duct-connectivity.cs afterward - don't trust IsConnected alone.");
return sb.ToString();
