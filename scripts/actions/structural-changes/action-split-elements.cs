// ============================================================
// FRAGMENT (action) — action-split-elements.cs
// PURPOSE: Split each Duct or Pipe in `elements` into two elements at one point along its own length — the
//          "AutoCAD Break" operation, generalized from recipes/split-duct-near-equipment.cs's
//          equipment-referenced version. Automatically reconnects the two resulting pieces at the joint
//          afterward (MechanicalUtils/PlumbingUtils.BreakCurve does NOT do this on its own).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the freshly created far-side pieces, one per successful split,
//           for chaining into another action.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only Duct and Pipe are supported — Revit's public API only exposes BreakCurve for these two
//         (Mechanical/Plumbing Utils); Cable Tray, Conduit, and Wall have no equivalent public split call.
// GOTCHA: only a straight (Line) run can be split this way — an element on a curved/Arc LocationCurve is
//         skipped.
// NOT YET LIVE-VERIFIED — test on ONE element first before trusting it on a batch.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string positionMode = "fraction"; // "fraction" (0.0-1.0 along the run) | "distance_mm_from_start"
double positionValue = 0.5;       // fraction OR mm, per positionMode — "start" is LocationCurve endpoint 0
// ---- END INPUTS ----

var newElementIds = new List<ElementId>();
int done = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Split Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            bool isDuct = e is Autodesk.Revit.DB.Mechanical.Duct;
            bool isPipe = e is Autodesk.Revit.DB.Plumbing.Pipe;
            if (!isDuct && !isPipe) { skipped++; failures.Add($"Id {e.Id.IntegerValue}: not a Duct or Pipe"); continue; }

            var line = (e.Location as LocationCurve)?.Curve as Line;
            if (line == null) { skipped++; failures.Add($"Id {e.Id.IntegerValue}: no straight LocationCurve"); continue; }

            double lengthFt = line.Length;
            double distFt = positionMode == "distance_mm_from_start"
                ? UnitUtils.ConvertToInternalUnits(positionValue, DisplayUnitType.DUT_MILLIMETERS)
                : lengthFt * positionValue;

            if (distFt <= 0 || distFt >= lengthFt) { skipped++; failures.Add($"Id {e.Id.IntegerValue}: break point falls outside the element's own length"); continue; }

            XYZ breakPt = line.GetEndPoint(0) + line.Direction * distFt;

            try
            {
                ElementId newId = isDuct
                    ? Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, e.Id, breakPt)
                    : Autodesk.Revit.DB.Plumbing.PlumbingUtils.BreakCurve(Document, e.Id, breakPt);

                var nearFresh = Document.GetElement(e.Id) as MEPCurve;
                var newFresh = Document.GetElement(newId) as MEPCurve;
                var nearConn = nearFresh.ConnectorManager.Connectors.Cast<Connector>()
                    .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
                var newConn = newFresh.ConnectorManager.Connectors.Cast<Connector>()
                    .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(breakPt)).First();
                nearConn.ConnectTo(newConn);

                newElementIds.Add(newId);
                done++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id.IntegerValue}: {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Split {done} element(s) ({positionMode}={positionValue}), skipped {skipped}.");
        if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id.IntegerValue))}");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to split — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
