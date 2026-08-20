// ============================================================
// FRAGMENT (creator) — create-floor.cs
// PURPOSE: Create one flat Floor from a closed boundary of mm plan points on a given Level — the basic
//          slab case (no slope arrows, no openings, no shape editing).
// PRODUCES: elements (List<Element>, the single new Floor, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// VERSION: both floor APIs are reached BY NAME at run time, so one source covers 2020 through 2027.
//         Revit 2022 removed Document.Create.NewFloor(CurveArray, FloorType, Level, bool) and replaced
//         it with the static Floor.Create(Document, IList<CurveLoop>, ElementId, ElementId). Neither
//         name can appear in the source or it stops compiling on the other version. Floor.Create takes
//         no `structural` flag - it is set afterwards via FLOOR_PARAM_IS_STRUCTURAL.
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

Func<double, double> mm = v => v / 304.8;

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
                var lines = new List<Curve>();
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    var p1 = new XYZ(mm(boundaryMm[i, 0]), mm(boundaryMm[i, 1]), z);
                    var p2 = new XYZ(mm(boundaryMm[j, 0]), mm(boundaryMm[j, 1]), z);
                    lines.Add(Line.CreateBound(p1, p2));
                }

                // Modern API preferred where it exists; both looked up by name (see VERSION note above).
                Floor floor;
                var _floorCreateM = typeof(Floor).GetMethod("Create",
                    new[] { typeof(Document), typeof(IList<CurveLoop>), typeof(ElementId), typeof(ElementId) });
                if (_floorCreateM != null)
                {
                    var loop = CurveLoop.Create(lines);
                    floor = (Floor)_floorCreateM.Invoke(null,
                        new object[] { Document, new List<CurveLoop> { loop }, floorType.Id, level.Id });
                    if (structural)
                    {
                        var sp = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                        if (sp != null && !sp.IsReadOnly) sp.Set(1);
                    }
                }
                else
                {
                    var _creation = Document.Create;
                    var _newFloorM = _creation.GetType().GetMethod("NewFloor",
                        new[] { typeof(CurveArray), typeof(FloorType), typeof(Level), typeof(bool) });
                    if (_newFloorM == null)
                        throw new InvalidOperationException("This Revit has neither Floor.Create nor Document.Create.NewFloor.");
                    var curves = new CurveArray();
                    foreach (var c in lines) curves.Append(c);
                    floor = (Floor)_newFloorM.Invoke(_creation, new object[] { curves, floorType, level, structural });
                }
                elements.Add(floor);
                t.Commit();
                sb.AppendLine($"Created floor (Id {floor.Id}), type '{floorType.Name}', on level '{level.Name}', {n} boundary points, structural: {structural}.");
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
