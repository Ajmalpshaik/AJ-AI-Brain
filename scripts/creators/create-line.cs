// ============================================================
// FRAGMENT (creator) — create-line.cs
// PURPOSE: Create one or more plain Model Lines or Detail Lines between mm point pairs — the standalone
//          version of the line-creation technique action-fillet-elements.cs (mode="arc") already uses
//          internally as a byproduct, for when a plain straight reference/sketch line is the actual goal.
// PRODUCES: elements (List<Element>, the newly created line(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so any action fragment can be appended after it.
// GOTCHA: mode="model" needs a horizontal (Z-normal) sketch plane through the line's own Z — fine for a
//         plan-view reference line, not meant for an arbitrary 3D line in space.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "model"; // "model" | "detail"
int viewIdInt = 0;      // mode="detail" only — 0 = Document.ActiveView
List<(double p1XMm, double p1YMm, double p1ZMm, double p2XMm, double p2YMm, double p2ZMm)> linesToCreate = new List<(double, double, double, double, double, double)>
{
    (0, 0, 0, 1000, 0, 0)
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
View view = mode == "detail" ? (viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView) : null;

if (mode == "detail" && view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Line"))
    {
        t.Start();
        try
        {
            foreach (var (p1XMm, p1YMm, p1ZMm, p2XMm, p2YMm, p2ZMm) in linesToCreate)
            {
                var p1 = new XYZ(mm(p1XMm), mm(p1YMm), mm(p1ZMm));
                var p2 = new XYZ(mm(p2XMm), mm(p2YMm), mm(p2ZMm));
                if ((p2 - p1).GetLength() < 1e-6) continue;

                var line = Line.CreateBound(p1, p2);
                CurveElement created;
                if (mode == "detail")
                {
                    created = Document.Create.NewDetailCurve(view, line);
                }
                else
                {
                    var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p1);
                    var sketchPlane = SketchPlane.Create(Document, plane);
                    created = Document.Create.NewModelCurve(line, sketchPlane);
                }
                elements.Add(created);
            }
            t.Commit();
            sb.AppendLine($"Created {elements.Count} {mode} line(s).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create line(s) — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
