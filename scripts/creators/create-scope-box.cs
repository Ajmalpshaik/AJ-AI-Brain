// ============================================================
// FRAGMENT (creator) — create-scope-box.cs
// PURPOSE: Create one or more Scope Boxes from mm box corners.
// PRODUCES: elements (List<Element>, the newly created Scope Box(es)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so action-assign-scope-box-to-view.cs can chain onto it directly.
// NOT YET LIVE-VERIFIED — Document.Create.NewScopeBox has not been run against a real model this session.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(double minXMm, double minYMm, double minZMm, double maxXMm, double maxYMm, double maxZMm, string name)> boxesToCreate = new List<(double, double, double, double, double, double, string)>
{
    (0, 0, 0, 10000, 10000, 3000, "Scope Box 1")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

using (var t = new Transaction(Document, "AJ Tools - Create Scope Boxes"))
{
    t.Start();
    try
    {
        foreach (var (minXMm, minYMm, minZMm, maxXMm, maxYMm, maxZMm, name) in boxesToCreate)
        {
            var p1 = new XYZ(mm(minXMm), mm(minYMm), mm(minZMm));
            var p2 = new XYZ(mm(maxXMm), mm(maxYMm), mm(maxZMm));
            var scopeBox = Document.Create.NewScopeBox(p1, p2);
            if (!string.IsNullOrEmpty(name))
            {
                try { scopeBox.Name = name; } catch { } // name collision — still created, keeps default name
            }
            elements.Add(scopeBox);
        }
        t.Commit();
        sb.AppendLine($"Created {elements.Count} scope box(es).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create scope boxes — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below (e.g. action-assign-scope-box-to-view.cs), or add
//      return sb.ToString(); to stop here ----
