// ============================================================
// FRAGMENT (creator) — create-floor.cs
// PURPOSE: Create one flat Floor from a closed boundary of mm plan points on a given Level — the basic
//          slab case (no slope arrows, no openings, no shape editing).
// PRODUCES: elements (List<Element>, the single new Floor, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: Revit 2020 uses the legacy Document.Create.NewFloor(CurveArray, ...) — the newer static
//         Floor.Create(...) only exists from Revit 2022, don't "modernize" this call.
// GOTCHA: the boundary auto-closes (last point connects back to the first) and must not self-intersect;
//         Revit throws on a crossed loop and the transaction rolls back cleanly.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog; run once on a small test slab.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int levelIdInt = 0;             // required — the Level the floor sits on
string floorTypeName = null;    // exact FloorType name; null = first FloorType in the project
bool structural = false;        // true = structural floor
// closed boundary in plan, mm — pairs of {x, y}; last point auto-connects to the first
double[,] boundaryMm = new double[,] { { 0, 0 }, { 4000, 0 }, { 4000, 3000 }, { 0, 3000 } };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
if (level == null)
{
    sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
}
else if (boundaryMm.GetLength(0) < 3)
{
    sb.AppendLine($"Boundary has only {boundaryMm.GetLength(0)} point(s) — a floor needs at least 3.");
}
else
{
    FloorType floorType = null;
    var floorTypes = new FilteredElementCollector(Document).OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
    floorType = floorTypeName == null
        ? floorTypes.FirstOrDefault()
        : floorTypes.FirstOrDefault(ft => ft.Name == floorTypeName);

    if (floorType == null)
    {
        sb.AppendLine(floorTypeName == null
            ? "No FloorType exists in this project at all."
            : $"FloorType '{floorTypeName}' not found. Available: {string.Join(", ", floorTypes.Select(ft => ft.Name))}");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Create Floor"))
        {
            t.Start();
            try
            {
                int n = boundaryMm.GetLength(0);
                double z = level.Elevation;
                var curves = new CurveArray();
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    var p1 = new XYZ(mm(boundaryMm[i, 0]), mm(boundaryMm[i, 1]), z);
                    var p2 = new XYZ(mm(boundaryMm[j, 0]), mm(boundaryMm[j, 1]), z);
                    curves.Append(Line.CreateBound(p1, p2));
                }

                var floor = Document.Create.NewFloor(curves, floorType, level, structural);
                elements.Add(floor);
                t.Commit();
                sb.AppendLine($"Created floor (Id {floor.Id.IntegerValue}), type '{floorType.Name}', on level '{level.Name}', {n} boundary points, structural: {structural}.");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to create floor — rolled back, nothing changed. Reason: {ex.Message}");
                elements = new List<Element>();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
