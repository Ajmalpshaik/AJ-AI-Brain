// ============================================================
// FRAGMENT (action) — action-move-elements.cs
// PURPOSE: Translate every element in `elements` by one offset vector (mm, X/Y/Z) — e.g. shift a room's
//          air terminals 300mm toward the wall, or nudge a mis-placed group of equipment.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double offsetXmm = 0.0;
double offsetYmm = 0.0;
double offsetZmm = 0.0;
// ---- END INPUTS ----

XYZ translation = new XYZ(
    UnitUtils.ConvertToInternalUnits(offsetXmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(offsetYmm, DisplayUnitType.DUT_MILLIMETERS),
    UnitUtils.ConvertToInternalUnits(offsetZmm, DisplayUnitType.DUT_MILLIMETERS));

// A position we can compare before and after. NOT for reporting — only for answering
// "did this element actually move?", which MoveElement itself will not tell you.
Func<Element, XYZ> probePoint = e =>
{
    if (e.Location is LocationPoint lp) return lp.Point;
    if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
    try { var bb = e.get_BoundingBox(null); if (bb != null) return (bb.Min + bb.Max) * 0.5; } catch { }
    return null;
};

double verifyTolFt = UnitUtils.ConvertToInternalUnits(1.0, DisplayUnitType.DUT_MILLIMETERS); // 1mm

int moved = 0, skipped = 0, blocked = 0, partial = 0, unverified = 0;
var blockedIds = new List<int>();
var partialIds = new List<int>();

using (var t = new Transaction(Document, "AJ Tools - Move Elements"))
{
    t.Start();
    try
    {
        var before = new Dictionary<ElementId, XYZ>();
        foreach (var e in elements)
        {
            var p = probePoint(e);
            if (p != null) before[e.Id] = p;
        }

        var attempted = new List<Element>();
        foreach (var e in elements)
        {
            try
            {
                ElementTransformUtils.MoveElement(Document, e.Id, translation);
                attempted.Add(e);
            }
            catch { skipped++; } // element type doesn't support move (e.g. some hosted/system elements)
        }

        Document.Regenerate();

        // MoveElement RETURNS NORMALLY AND MOVES NOTHING on a pinned element or a group member —
        // no exception, no return value to check. Counting "didn't throw" as "moved" reported
        // "Moved 5 element(s), skipped 0" for 5 air terminals that had not shifted by a
        // millimetre (proved live 2026-08-07). The only honest evidence is the position itself.
        // See ../../../knowledge/live-model/geometry-and-transforms.md.
        bool askedForNothing = translation.GetLength() < verifyTolFt;
        foreach (var e in attempted)
        {
            XYZ was;
            XYZ now = probePoint(e);
            if (askedForNothing) { moved++; continue; }
            if (now == null || !before.TryGetValue(e.Id, out was)) { unverified++; continue; }

            double actualShift = now.DistanceTo(was);
            if (actualShift < verifyTolFt)
            {
                blocked++;
                blockedIds.Add(e.Id.IntegerValue);
            }
            else if (now.DistanceTo(was + translation) > verifyTolFt)
            {
                partial++; // moved, but constrained — hosted/attached elements do this legitimately
                partialIds.Add(e.Id.IntegerValue);
            }
            else moved++;
        }

        t.Commit();
        sb.AppendLine($"Moved {moved} element(s) by ({offsetXmm}mm, {offsetYmm}mm, {offsetZmm}mm) — verified against the model, not just 'no error'.");
        if (partial > 0)
            sb.AppendLine($"  {partial} moved but NOT by the full offset (constrained by a host or attachment): {string.Join(", ", partialIds)}");
        if (blocked > 0)
            sb.AppendLine($"  {blocked} DID NOT MOVE AT ALL despite the API reporting no error — pinned, or a member of a group. Move the group itself, or unpin first: {string.Join(", ", blockedIds)}");
        if (skipped > 0)
            sb.AppendLine($"  {skipped} refused the move outright (element type doesn't support it).");
        if (unverified > 0)
            sb.AppendLine($"  {unverified} could not be position-checked (no readable location) — treat as unconfirmed.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to move — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
