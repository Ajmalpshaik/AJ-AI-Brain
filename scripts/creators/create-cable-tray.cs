// ============================================================
// FRAGMENT (creator) — create-cable-tray.cs
// PURPOSE: Draw ONE straight cable tray between two mm points — electrical containment twin of
//          create-duct.cs.
// PRODUCES: elements (List<Element>, the single new CableTray), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: a bare CableTray.Create makes an unconnected segment — check open ends after with
//         filter-by-connection-status.cs; fittings between segments are not created here.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string trayTypeName = null;     // null = first CableTrayType (ladder/channel etc. are types)
int levelIdInt = 0;             // reference level
double startXMm = 0, startYMm = 0, startZMm = 2800;
double endXMm = 3000, endYMm = 0, endZMm = 2800;
double widthMm = 300, heightMm = 100;  // 0 = keep type default
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
var trayTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Electrical.CableTrayType)).Cast<Autodesk.Revit.DB.Electrical.CableTrayType>().ToList();
var trayType = trayTypeName == null ? trayTypes.FirstOrDefault() : trayTypes.FirstOrDefault(tt => tt.Name == trayTypeName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (trayType == null) sb.AppendLine(trayTypeName == null ? "No CableTrayType in the project." : $"Tray type '{trayTypeName}' not found. Available: {string.Join(", ", trayTypes.Select(tt => tt.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Cable Tray"))
    {
        t.Start();
        try
        {
            var p1 = new XYZ(mm(startXMm), mm(startYMm), mm(startZMm));
            var p2 = new XYZ(mm(endXMm), mm(endYMm), mm(endZMm));
            var tray = Autodesk.Revit.DB.Electrical.CableTray.Create(Document, trayType.Id, p1, p2, level.Id);

            if (widthMm > 0)
            {
                var pW = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
                if (pW != null && !pW.IsReadOnly) pW.Set(mm(widthMm));
            }
            if (heightMm > 0)
            {
                var pH = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
                if (pH != null && !pH.IsReadOnly) pH.Set(mm(heightMm));
            }

            elements.Add(tray);
            t.Commit();
            sb.AppendLine($"Created cable tray (Id {tray.Id.IntegerValue}) — type '{trayType.Name}', {widthMm}x{heightMm} mm, {startXMm},{startYMm},{startZMm} -> {endXMm},{endYMm},{endZMm} mm.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create cable tray — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
