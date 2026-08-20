// ============================================================
// FRAGMENT (action) — action-offset-elements.cs
// PURPOSE: Offset each linear element in `elements` sideways by a perpendicular distance — the "AutoCAD
//          Offset" operation. Works on anything with a Line/Arc LocationCurve (duct, pipe, cable tray,
//          conduit, wall, model/detail line, beam) via Curve.CreateOffset, so an already-curved run (e.g.
//          the arc from action-fillet-elements.cs mode="arc") offsets correctly too, not just straight runs.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — only populated when copy = true.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: normal is fixed to the world Z-axis, i.e. this offsets WITHIN A HORIZONTAL PLANE (sideways in
//         plan) — the normal case (a parallel duct/pipe/wall run in plan). A vertical riser or sloped run
//         won't offset sideways the way you'd expect; not meant for that case.
// GOTCHA: the sign of offsetMm picks the side (Curve.CreateOffset's right-hand rule around the Z-axis,
//         not a "left"/"right" label) — if the result lands on the wrong side, flip the sign.
// GOTCHA: for MEP curves (duct/pipe/cable tray/conduit) that were connected to fittings, offsetting breaks
//         the original connections — reconnect manually afterward, same caveat as action-move-elements.cs.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double offsetMm = 500; // signed — flip the sign to flip the side, see GOTCHA above
bool copy = true;      // true = new offset copy alongside the original; false = move the original itself
// ---- END INPUTS ----

double offsetFt = offsetMm / 304.8;
var newElementIds = new List<ElementId>();
int done = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Offset Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var lc = e.Location as LocationCurve;
            if (lc == null) { skipped++; failures.Add($"Id {e.Id}: no LocationCurve"); continue; }

            try
            {
                var offsetCurve = lc.Curve.CreateOffset(offsetFt, XYZ.BasisZ);
                if (copy)
                {
                    var newId = ElementTransformUtils.CopyElement(Document, e.Id, XYZ.Zero).First();
                    var newLc = Document.GetElement(newId).Location as LocationCurve;
                    newLc.Curve = offsetCurve;
                    newElementIds.Add(newId);
                }
                else
                {
                    lc.Curve = offsetCurve;
                }
                done++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id}: {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Offset {done} element(s) by {offsetMm}mm ({(copy ? "copy" : "moved in place")}), skipped {skipped}.");
        if (copy && newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id.IntegerValue))}");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to offset — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
