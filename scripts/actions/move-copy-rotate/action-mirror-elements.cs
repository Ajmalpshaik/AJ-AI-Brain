// ============================================================
// FRAGMENT (action) — action-mirror-elements.cs
// PURPOSE: Mirror every element in `elements` across a vertical plane defined by two plan points (mm) —
//          Revit's "Mirror - Draw Axis". Set mirrorCopy = true to keep the originals and create mirrored
//          copies (produces newElementIds for chaining), or false to mirror the originals in place.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — only populated when mirrorCopy = true.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double axisP1XMm = 0, axisP1YMm = 0;
double axisP2XMm = 1000, axisP2YMm = 0; // the mirror axis runs through these two plan points, any Z
bool mirrorCopy = true; // true = keep originals, create mirrored copies; false = mirror in place
// ---- END INPUTS ----

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
XYZ p1 = new XYZ(mm(axisP1XMm), mm(axisP1YMm), 0);
XYZ p2 = new XYZ(mm(axisP2XMm), mm(axisP2YMm), 0);

var newElementIds = new List<ElementId>();

if ((p2 - p1).GetLength() < 1e-6)
{
    sb.AppendLine("Mirror axis points are the same point — set two distinct points to define the axis.");
}
else
{
    XYZ dir = (p2 - p1).Normalize();
    XYZ normal = new XYZ(-dir.Y, dir.X, 0); // perpendicular in plan — the mirror plane is vertical through the axis line
    Plane mirrorPlane = Plane.CreateByNormalAndOrigin(normal, p1);
    var ids = elements.Select(e => e.Id).ToList();

    using (var t = new Transaction(Document, "AJ Tools - Mirror Elements"))
    {
        t.Start();
        try
        {
            if (mirrorCopy)
            {
                var result = ElementTransformUtils.MirrorElements(Document, ids, mirrorPlane, true);
                newElementIds.AddRange(result);
                t.Commit();
                sb.AppendLine($"Mirrored {ids.Count} element(s) across the given axis, created {newElementIds.Count} new element(s).");
            }
            else
            {
                ElementTransformUtils.MirrorElements(Document, ids, mirrorPlane, false);
                t.Commit();
                sb.AppendLine($"Mirrored {ids.Count} element(s) across the given axis, in place.");
            }
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to mirror — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
