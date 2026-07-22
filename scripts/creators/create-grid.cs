// ============================================================
// FRAGMENT (creator) — create-grid.cs
// PURPOSE: Create one or more straight Grid lines from mm endpoint pairs.
// PRODUCES: elements (List<Element>, the newly created Grid(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so any action fragment can be appended after it (e.g.
//          action-rename-element.cs to set each grid's final name after creation).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(double p1XMm, double p1YMm, double p2XMm, double p2YMm, string name)> gridsToCreate = new List<(double, double, double, double, string)>
{
    (0, 0, 0, 10000, "A")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

using (var t = new Transaction(Document, "AJ Tools - Create Grids"))
{
    t.Start();
    try
    {
        foreach (var (p1XMm, p1YMm, p2XMm, p2YMm, name) in gridsToCreate)
        {
            var p1 = new XYZ(mm(p1XMm), mm(p1YMm), 0);
            var p2 = new XYZ(mm(p2XMm), mm(p2YMm), 0);
            if ((p2 - p1).GetLength() < 1e-6) continue; // same point, no valid line

            var line = Line.CreateBound(p1, p2);
            var grid = Grid.Create(Document, line);
            if (!string.IsNullOrEmpty(name))
            {
                try { grid.Name = name; } catch { } // name collision — grid still created, just keeps its default name
            }
            elements.Add(grid);
        }
        t.Commit();
        sb.AppendLine($"Created {elements.Count} grid(s).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create grids — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
