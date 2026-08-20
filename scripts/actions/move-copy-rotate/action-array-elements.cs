// ============================================================
// FRAGMENT (action) — action-array-elements.cs
// PURPOSE: Create multiple evenly-spaced copies of every element in `elements` — the "AutoCAD Array"
//          operation. Two modes: "linear" (N copies at a fixed mm spacing vector, e.g. 10 diffusers
//          1000mm apart) or "radial" (N copies swept around a center point by an angle, e.g. 8 copies
//          evenly around a full circle, or a partial sweep).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — every copy created, across every source element, for
//           chaining into another action.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA (mode="radial"): axis is fixed to a vertical line through centerXMm/centerYMm (any Z) — a plan-
//         view rotation, matching action-rotate-elements.cs's own default axis.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "linear"; // "linear" | "radial"
int count = 3;           // number of COPIES to create (the originals are left in place, not counted)
double spacingXmm = 1000, spacingYmm = 0, spacingZmm = 0; // mode="linear": offset per step
double centerXMm = 0, centerYMm = 0;                       // mode="radial": center of rotation, plan
double totalSweepDegrees = 360;                            // mode="radial": divided evenly across `count` steps (360 = full circle)
// ---- END INPUTS ----

var newElementIds = new List<ElementId>();
int copied = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Array Elements"))
{
    t.Start();
    try
    {
        if (mode == "linear")
        {
            XYZ step = new XYZ(
                spacingXmm / 304.8,
                spacingYmm / 304.8,
                spacingZmm / 304.8);

            for (int i = 1; i <= count; i++)
            {
                XYZ offset = step * i;
                foreach (var e in elements)
                {
                    try
                    {
                        var ids = ElementTransformUtils.CopyElement(Document, e.Id, offset);
                        newElementIds.AddRange(ids);
                        copied += ids.Count;
                    }
                    catch { skipped++; }
                }
            }
            t.Commit();
            sb.AppendLine($"Arrayed {elements.Count} source element(s) into {count} linear step(s) ({spacingXmm}mm, {spacingYmm}mm, {spacingZmm}mm each), created {copied} new element(s), skipped {skipped}.");
        }
        else if (mode == "radial")
        {
            XYZ center = new XYZ(
                centerXMm / 304.8,
                centerYMm / 304.8,
                0);
            Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
            double stepRadians = (totalSweepDegrees / count) * (Math.PI / 180.0);
            var sourceIds = elements.Select(e => e.Id).ToList();

            for (int i = 1; i <= count; i++)
            {
                try
                {
                    var ids = ElementTransformUtils.CopyElements(Document, sourceIds, XYZ.Zero);
                    ElementTransformUtils.RotateElements(Document, ids, axis, stepRadians * i);
                    newElementIds.AddRange(ids);
                    copied += ids.Count;
                }
                catch { skipped += sourceIds.Count; }
            }
            t.Commit();
            sb.AppendLine($"Arrayed {elements.Count} source element(s) into {count} radial step(s) ({totalSweepDegrees}deg total sweep around ({centerXMm}mm, {centerYMm}mm)), created {copied} new element(s), skipped {skipped}.");
        }
        else
        {
            t.RollBack();
            sb.AppendLine($"Unknown mode '{mode}' — use \"linear\" or \"radial\". Nothing changed.");
        }
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to array — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
