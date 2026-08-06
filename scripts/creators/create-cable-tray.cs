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

            // The old version had NO else branch at all, so a missing parameter, a read-only one and a
            // refused value were all identical silence — and the summary then printed the REQUESTED
            // widthMm x heightMm as though they had landed. Sizes must be read back: Set() can return
            // false and change nothing, or return true and snap to the type's size table (measured live
            // 2026-08-07 — a pipe asked for 77mm returned true and came out 80mm).
            // See ../../knowledge/live-model/core.md.
            var skippedSizes = new List<string>();
            var sizeNotes = new List<string>();
            Action<Parameter, string, double> applySize = (p, label, wantMm) =>
            {
                if (p == null || p.IsReadOnly) { skippedSizes.Add(label); return; }
                bool ok = p.Set(mm(wantMm));
                // Regenerate BEFORE reading back — straight after Set() the parameter still reports the
                // requested value; the snap to the type's size table happens at regeneration.
                Document.Regenerate();
                double gotMm = UnitUtils.ConvertFromInternalUnits(p.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                if (!ok) sizeNotes.Add($"{label} REFUSED {wantMm}mm — still {gotMm:0.##}mm");
                else if (Math.Abs(gotMm - wantMm) > 0.5) sizeNotes.Add($"{label} SNAPPED {wantMm}mm -> {gotMm:0.##}mm (nearest size the type allows)");
            };

            if (widthMm > 0) applySize(tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM), "Width", widthMm);
            if (heightMm > 0) applySize(tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM), "Height", heightMm);

            elements.Add(tray);
            t.Commit();

            var wNow = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM);
            var hNow = tray.get_Parameter(BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM);
            string actualSize = $"{(wNow == null ? 0 : UnitUtils.ConvertFromInternalUnits(wNow.AsDouble(), DisplayUnitType.DUT_MILLIMETERS)):0.##}x{(hNow == null ? 0 : UnitUtils.ConvertFromInternalUnits(hNow.AsDouble(), DisplayUnitType.DUT_MILLIMETERS)):0.##} mm";
            sb.AppendLine($"Created cable tray (Id {tray.Id.IntegerValue}) — type '{trayType.Name}', ACTUAL size {actualSize}, {startXMm},{startYMm},{startZMm} -> {endXMm},{endYMm},{endZMm} mm.");
            if (sizeNotes.Count > 0) sb.AppendLine("  " + string.Join("; ", sizeNotes));
            if (skippedSizes.Count > 0) sb.AppendLine($"  Size(s) not settable on this tray type, skipped: {string.Join(", ", skippedSizes)}.");
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
