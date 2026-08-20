// ============================================================
// FRAGMENT (action) — action-rotate-elements.cs
// PURPOSE: Rotate every element in `elements` around a vertical axis by one angle (degrees) — e.g. spin
//          a mis-oriented piece of equipment 90 degrees, or rotate a cluster of terminals to match a new
//          room layout.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double angleDegrees = 90.0;                // positive = counterclockwise, looking down from above
double? pivotXmm = null, pivotYmm = null;  // null = rotate each element about its own location point
// ---- END INPUTS ----

double angleRadians = angleDegrees * Math.PI / 180.0;

// An orientation signature that CHANGES when a rotation really happens. RotateElement returns
// normally and rotates nothing on a pinned element or a group member — same silent no-op as
// MoveElement (proved live 2026-08-07: 5 grouped air terminals stayed at 0.00 degrees while the
// fragment reported "Rotated 5, skipped 0"). Only the model can confirm a rotation.
// See ../../../knowledge/live-model/geometry-and-transforms.md.
Func<Element, string> probeOrientation = e =>
{
    if (e.Location is LocationPoint lp) return $"R{lp.Rotation:F6}";
    if (e.Location is LocationCurve lc)
    {
        var c = lc.Curve;
        return $"P{c.GetEndPoint(0).X:F6},{c.GetEndPoint(0).Y:F6}";
    }
    try { var bb = e.get_BoundingBox(null); if (bb != null) return $"B{bb.Min.X:F6},{bb.Min.Y:F6},{bb.Max.X:F6},{bb.Max.Y:F6}"; } catch { }
    return null;
};

// 0 degrees (or a whole multiple of 360) legitimately changes nothing — don't report that as blocked.
double normalisedDeg = Math.Abs(angleDegrees % 360.0);
bool askedForNothing = normalisedDeg < 1e-6 || Math.Abs(normalisedDeg - 360.0) < 1e-6;

int rotated = 0, skipped = 0, blocked = 0, unverified = 0;
var blockedIds = new List<int>();

using (var t = new Transaction(Document, "AJ Tools - Rotate Elements"))
{
    t.Start();
    try
    {
        var before = new Dictionary<ElementId, string>();
        foreach (var e in elements)
        {
            var sig = probeOrientation(e);
            if (sig != null) before[e.Id] = sig;
        }

        var attempted = new List<Element>();
        foreach (var e in elements)
        {
            XYZ pivot;
            if (pivotXmm.HasValue && pivotYmm.HasValue)
            {
                pivot = new XYZ(
                    pivotXmm.Value / 304.8,
                    pivotYmm.Value / 304.8,
                    0);
            }
            else if (e.Location is LocationPoint lp)
            {
                pivot = lp.Point;
            }
            else if (e.Location is LocationCurve lc)
            {
                pivot = lc.Curve.Evaluate(0.5, true);
            }
            else { skipped++; continue; }

            var axis = Line.CreateBound(pivot, pivot + XYZ.BasisZ);
            try
            {
                ElementTransformUtils.RotateElement(Document, e.Id, axis, angleRadians);
                attempted.Add(e);
            }
            catch { skipped++; }
        }

        Document.Regenerate();

        foreach (var e in attempted)
        {
            string was;
            string now = probeOrientation(e);
            if (askedForNothing) { rotated++; continue; }
            if (now == null || !before.TryGetValue(e.Id, out was)) { unverified++; continue; }
            if (now == was) { blocked++; blockedIds.Add(e.Id.IntegerValue); }
            else rotated++;
        }

        t.Commit();
        sb.AppendLine($"Rotated {rotated} element(s) by {angleDegrees} degrees — verified against the model, not just 'no error'.");
        if (blocked > 0)
            sb.AppendLine($"  {blocked} DID NOT ROTATE AT ALL despite the API reporting no error — pinned, or a member of a group. Rotate the group itself, or unpin first: {string.Join(", ", blockedIds)}");
        if (skipped > 0)
            sb.AppendLine($"  {skipped} refused the rotation outright (no usable location, or the element type doesn't support it).");
        if (unverified > 0)
            sb.AppendLine($"  {unverified} could not be orientation-checked — treat as unconfirmed.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rotate — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
