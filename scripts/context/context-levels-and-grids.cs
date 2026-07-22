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
    double elevMm = UnitUtils.ConvertFromInternalUnits(l.Elevation, DisplayUnitType.DUT_MILLIMETERS);
    sb.AppendLine($"  - {l.Name} (Id {l.Id.IntegerValue}, elevation {elevMm:F0}mm)");
}

var grids = new FilteredElementCollector(Document).OfClass(typeof(Grid)).Cast<Grid>().OrderBy(g => g.Name).ToList();
sb.AppendLine($"Grids ({grids.Count}):");
foreach (var g in grids)
    sb.AppendLine($"  - {g.Name} (Id {g.Id.IntegerValue})");

return sb.ToString();
