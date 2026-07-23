// ============================================================
// FRAGMENT (creator) — create-filled-region.cs
// PURPOSE: Create a Filled Region (a filled/hatched polygon annotation) in a view from a closed loop of mm
//          points.
// PRODUCES: elements (List<Element>, the newly created FilledRegion, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so action-set-parameter-value.cs (e.g. Comments) can chain onto it.
// GOTCHA: the point list must already form a closed loop's corners IN ORDER (not closed back to the
//         first point — CurveLoop.Create closes it automatically between the last and first point).
// Live-verified 2026-07-22 — real region+boundary loop created.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0; // 0 = Document.ActiveView
string filledRegionTypeName = null; // null = use the first available Filled Region Type in the project
List<(double xMm, double yMm)> boundaryPoints = new List<(double, double)>
{
    (0, 0), (1000, 0), (1000, 1000), (0, 1000)
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else if (boundaryPoints.Count < 3)
{
    sb.AppendLine("Need at least 3 boundary points to make a closed loop.");
}
else
{
    ElementId typeId;
    if (string.IsNullOrEmpty(filledRegionTypeName))
    {
        typeId = new FilteredElementCollector(Document).OfClass(typeof(FilledRegionType)).FirstElementId();
    }
    else
    {
        typeId = new FilteredElementCollector(Document).OfClass(typeof(FilledRegionType))
            .FirstOrDefault(t => t.Name.Equals(filledRegionTypeName, StringComparison.OrdinalIgnoreCase))?.Id
            ?? ElementId.InvalidElementId;
    }

    if (typeId == null || typeId == ElementId.InvalidElementId)
    {
        sb.AppendLine("No Filled Region Type found" + (string.IsNullOrEmpty(filledRegionTypeName) ? " in the project." : $" named '{filledRegionTypeName}'."));
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Create Filled Region"))
        {
            t.Start();
            try
            {
                Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
                var pts = boundaryPoints.Select(p => new XYZ(mm(p.xMm), mm(p.yMm), 0)).ToList();
                var curves = new List<Curve>();
                for (int i = 0; i < pts.Count; i++)
                    curves.Add(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
                var loop = CurveLoop.Create(curves);

                var region = FilledRegion.Create(Document, typeId, view.Id, new List<CurveLoop> { loop });
                elements.Add(region);
                t.Commit();
                sb.AppendLine($"Created filled region Id {region.Id.IntegerValue} in view '{view.Name}'.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to create filled region — rolled back, nothing changed. Reason: {ex.Message}");
                elements = new List<Element>();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
