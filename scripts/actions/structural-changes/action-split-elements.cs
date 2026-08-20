// ============================================================
// FRAGMENT (action) — action-split-elements.cs
// PURPOSE: Split each Duct or Pipe in `elements` into two elements at one point along its own length — the
//          "AutoCAD Break" operation, generalized from recipes/split-duct-near-equipment.cs's
//          equipment-referenced version. Automatically reconnects the two resulting pieces at the joint
//          afterward (MechanicalUtils/PlumbingUtils.BreakCurve does NOT do this on its own).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the OTHER piece of each split, one per successful split, for
//           chaining into another action. WHICH physical side keeps the original Id is NOT guaranteed —
//           BreakCurve can hand the new Id to either side (confirmed live 2026-07-23 in
//           recipes/split-duct-near-equipment.cs). The reconnect below is side-agnostic, so the split
//           itself is safe — but if a downstream action needs a specific SIDE, re-locate each piece
//           geometrically; never assume newElementIds is the far/start side.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only Duct and Pipe are supported — Revit's public API only exposes BreakCurve for these two
//         (Mechanical/Plumbing Utils); Cable Tray, Conduit, and Wall have no equivalent public split call.
// GOTCHA: only a straight (Line) run can be split this way — an element on a curved/Arc LocationCurve is
//         skipped.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
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
            if (!isDuct && !isPipe) { skipped++; failures.Add($"Id {e.Id}: not a Duct or Pipe"); continue; }

            var line = (e.Location as LocationCurve)?.Curve as Line;
            if (line == null) { skipped++; failures.Add($"Id {e.Id}: no straight LocationCurve"); continue; }

            double lengthFt = line.Length;
            double distFt = positionMode == "distance_mm_from_start"
                ? positionValue / 304.8
                : lengthFt * positionValue;

            if (distFt <= 0 || distFt >= lengthFt) { skipped++; failures.Add($"Id {e.Id}: break point falls outside the element's own length"); continue; }

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
                // Real Union fitting, NOT a bare ConnectTo - a fitting-less direct joint between two
                // colinear same-size pieces can be silently re-merged by Revit later, losing the split
                // (live-proven on ducts 2026-07-26; same call works for pipes).
                var union = Document.Create.NewUnionFitting(nearConn, newConn);
                if (union == null) throw new InvalidOperationException("NewUnionFitting returned null - no union family available for this type.");

                newElementIds.Add(newId);
                done++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id}: {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Split {done} element(s) ({positionMode}={positionValue}), skipped {skipped}.");
        if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id))}");
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
