// ============================================================
// SCRIPT (context) — context-levels-and-grids.cs
// PURPOSE: Quick list of every Level (name + elevation, ordered bottom-up) and every Grid (name) in the
//          document. Read-only, zero input, safe to call anytime — the "what's the project's own
//          vertical/horizontal reference framework" orientation step, same role context-project-units.cs
//          plays for units. Feeds create-dimension.cs, filter-by-grid.cs, filter-by-levels.cs.
// ============================================================

var sb = new System.Text.StringBuilder();

var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
sb.AppendLine($"Levels ({levels.Count}):");
foreach (var l in levels)
{
    double elevMm = l.Elevation * 304.8;   // feet -> mm. 1 ft is exactly 304.8 mm, so no units API is needed
    sb.AppendLine($"  - {l.Name} (Id {l.Id}, elevation {elevMm:F0}mm)");
}

var grids = new FilteredElementCollector(Document).OfClass(typeof(Grid)).Cast<Grid>().OrderBy(g => g.Name).ToList();
sb.AppendLine($"Grids ({grids.Count}):");
foreach (var g in grids)
    sb.AppendLine($"  - {g.Name} (Id {g.Id})");

return sb.ToString();
