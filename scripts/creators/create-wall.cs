// ============================================================
// FRAGMENT (creator) — create-wall.cs
// PURPOSE: Create one straight Wall between two mm plan points on a Level with a given height — the basic
//          line-based case (no profiles, no arcs, no openings).
// PRODUCES: elements (List<Element>, the single new Wall), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: height is unconnected (fixed mm) — attaching top to another Level is a parameter edit
//         (WALL_HEIGHT_TYPE) not done here.
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2
//          suggestions.", which the README had already superseded. The verification was recorded there and never
//          here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string wallTypeName = null;     // exact WallType name; null = first basic WallType
int levelIdInt = 0;             // base level
double startXMm = 0, startYMm = 0;
double endXMm = 4000, endYMm = 0;
double heightMm = 3000;         // unconnected height
bool structural = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
var wallTypes = new FilteredElementCollector(Document).OfClass(typeof(WallType)).Cast<WallType>()
    .Where(wt => wt.Kind == WallKind.Basic).ToList();
var wallType = wallTypeName == null ? wallTypes.FirstOrDefault() : wallTypes.FirstOrDefault(wt => wt.Name == wallTypeName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (wallType == null) sb.AppendLine(wallTypeName == null ? "No basic WallType in the project." : $"Wall type '{wallTypeName}' not found. Available: {string.Join(", ", wallTypes.Select(wt => wt.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Wall"))
    {
        t.Start();
        try
        {
            double z = level.Elevation;
            var line = Line.CreateBound(new XYZ(mm(startXMm), mm(startYMm), z), new XYZ(mm(endXMm), mm(endYMm), z));
            var wall = Wall.Create(Document, line, wallType.Id, level.Id, mm(heightMm), 0, false, structural);
            elements.Add(wall);
            t.Commit();
            sb.AppendLine($"Created wall (Id {wall.Id}) — type '{wallType.Name}', level '{level.Name}', {startXMm},{startYMm} -> {endXMm},{endYMm} mm, height {heightMm} mm, structural: {structural}.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create wall — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
