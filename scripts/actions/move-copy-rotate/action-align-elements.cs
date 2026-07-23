// ============================================================
// FRAGMENT (action) — action-align-elements.cs
// PURPOSE: Move every element in `elements` so it matches ONE reference element's position along the
//          chosen axis/axes — Revit's own "Align" tool. E.g. alignX=true, alignY=false snaps every element
//          onto the same X coordinate as the reference, leaving each one's own Y/Z untouched.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: uses each element's own insertion point / curve midpoint / bounding-box center (same fallback
//         chain as action-find-duplicates.cs's getPoint) as "its position" — for a linear element (duct/
//         pipe/wall) this aligns the MIDPOINT, not an endpoint; for an endpoint-specific alignment use
//         action-move-elements.cs with an explicit vector instead.
// Live-verified 2026-07-22, exact match.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int referenceElementIdInt = 0; // the ONE element everything in `elements` aligns to — set explicitly
bool alignX = true, alignY = false, alignZ = false;
// ---- END INPUTS ----

Func<Element, XYZ> getPoint = e =>
{
    if (e.Location is LocationPoint lp) return lp.Point;
    if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
    try { var bb = e.get_BoundingBox(null); if (bb != null) return (bb.Min + bb.Max) * 0.5; } catch { }
    return null;
};

var reference = Document.GetElement(new ElementId(referenceElementIdInt));
XYZ refPoint = reference != null ? getPoint(reference) : null;

if (reference == null || refPoint == null)
{
    sb.AppendLine($"Reference element Id {referenceElementIdInt} not found, or its position couldn't be determined.");
}
else if (!alignX && !alignY && !alignZ)
{
    sb.AppendLine("At least one of alignX/alignY/alignZ must be true — nothing to align to.");
}
else
{
    int moved = 0, skipped = 0;

    using (var t = new Transaction(Document, "AJ Tools - Align Elements"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                if (e.Id == reference.Id) { skipped++; continue; }
                var pt = getPoint(e);
                if (pt == null) { skipped++; continue; }

                XYZ translation = new XYZ(
                    alignX ? refPoint.X - pt.X : 0,
                    alignY ? refPoint.Y - pt.Y : 0,
                    alignZ ? refPoint.Z - pt.Z : 0);

                if (translation.GetLength() < 1e-9) { skipped++; continue; }

                try
                {
                    ElementTransformUtils.MoveElement(Document, e.Id, translation);
                    moved++;
                }
                catch { skipped++; }
            }
            t.Commit();
            string axisLabel = string.Join("/", new[] { alignX ? "X" : null, alignY ? "Y" : null, alignZ ? "Z" : null }.Where(s => s != null));
            sb.AppendLine($"Aligned {moved} element(s) to '{reference.Name}' (Id {reference.Id.IntegerValue}) on axis/axes: {axisLabel}, skipped {skipped} (already aligned, or no resolvable position).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to align — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
