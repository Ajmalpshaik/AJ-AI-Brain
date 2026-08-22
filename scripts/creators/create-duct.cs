// ============================================================
// FRAGMENT (creator) — create-duct.cs
// PURPOSE: Draw ONE straight duct between two mm points — the plain, general-purpose version of what the
//          HVAC recipes do inside their multi-stage builds. For full FCU-to-terminal systems use the
//          recipes; for "just draw a duct from A to B" use this.
// PRODUCES: elements (List<Element>, the single new Duct), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: size parameters only apply to the matching shape — Width/Height for rectangular duct types,
//         Diameter for round ones; the wrong pair is skipped and reported, not an error.
// GOTCHA: connecting this duct into a system (fittings, taps) is the recipes' job — a bare Duct.Create
//         makes an unconnected segment; check open ends after with filter-by-connection-status.cs.
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2
//          suggestions (the create call itself is the same one connect-equipment-to-air-terminals.cs uses live-
//          proven, but this fragment hasn't run).", which the README had already superseded. The verification was
//          recorded there and never here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemTypeName = null;   // e.g. "Supply Air"; null = first MechanicalSystemType in the project
string ductTypeName = null;     // e.g. "Default"; null = first DuctType
int levelIdInt = 0;             // reference level
double startXMm = 0, startYMm = 0, startZMm = 2700;
double endXMm = 3000, endYMm = 0, endZMm = 2700;
double widthMm = 300, heightMm = 250;  // rectangular types; ignored for round
double diameterMm = 0;                 // round types; 0 = keep type default
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
var sysTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType)).Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>().ToList();
var sysType = systemTypeName == null ? sysTypes.FirstOrDefault() : sysTypes.FirstOrDefault(s => s.Name == systemTypeName);
var ductTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType)).Cast<Autodesk.Revit.DB.Mechanical.DuctType>().ToList();
var ductType = ductTypeName == null ? ductTypes.FirstOrDefault() : ductTypes.FirstOrDefault(d => d.Name == ductTypeName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (sysType == null) sb.AppendLine(systemTypeName == null ? "No MechanicalSystemType in the project." : $"System type '{systemTypeName}' not found. Available: {string.Join(", ", sysTypes.Select(s => s.Name))}");
else if (ductType == null) sb.AppendLine(ductTypeName == null ? "No DuctType in the project." : $"Duct type '{ductTypeName}' not found. Available: {string.Join(", ", ductTypes.Select(d => d.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Duct"))
    {
        t.Start();
        try
        {
            var p1 = new XYZ(mm(startXMm), mm(startYMm), mm(startZMm));
            var p2 = new XYZ(mm(endXMm), mm(endYMm), mm(endZMm));
            var duct = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysType.Id, ductType.Id, level.Id, p1, p2);

            // SIZE IS NOT WHAT YOU ASKED FOR UNTIL YOU READ IT BACK. Two separate traps, both measured
            // live 2026-08-07 on this API:
            //   (a) Set() returns FALSE and changes nothing — e.g. Set(0) on a duct Width.
            //   (b) Set() returns TRUE and Revit SNAPS the value to the type's size table — a pipe asked
            //       for 77mm returned true and came out 80mm. A bool check alone would still have lied.
            // So: honour the bool AND compare the value actually stored. See ../../knowledge/live-model/core.md.
            var skippedSizes = new List<string>();
            var sizeNotes = new List<string>();
            Action<Parameter, string, double> applySize = (p, label, wantMm) =>
            {
                if (p == null || p.IsReadOnly) { skippedSizes.Add(label); return; }
                bool ok = p.Set(mm(wantMm));
                // Regenerate BEFORE reading back: straight after Set() the parameter still reports the
                // value you asked for, and the snap to the type's size table only happens at
                // regeneration. Without this the SNAPPED note never fires (measured live 2026-08-07).
                Document.Regenerate();
                double gotMm = p.AsDouble() * 304.8;
                if (!ok) sizeNotes.Add($"{label} REFUSED {wantMm}mm — still {gotMm:0.##}mm");
                else if (Math.Abs(gotMm - wantMm) > 0.5) sizeNotes.Add($"{label} SNAPPED {wantMm}mm -> {gotMm:0.##}mm (nearest size the type allows)");
            };

            if (widthMm > 0) applySize(duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM), "Width", widthMm);
            if (heightMm > 0) applySize(duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM), "Height", heightMm);
            if (diameterMm > 0) applySize(duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM), "Diameter", diameterMm);

            elements.Add(duct);
            t.Commit();

            // Report the size the MODEL holds, re-read after the commit — never the requested numbers.
            var wNow = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            var hNow = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
            var dNow = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            string actualSize = (dNow != null && dNow.HasValue && (wNow == null || !wNow.HasValue))
                ? $"{dNow.AsDouble() * 304.8:0.##} mm dia"
                : $"{(wNow == null ? 0 : wNow.AsDouble() * 304.8):0.##}x{(hNow == null ? 0 : hNow.AsDouble() * 304.8):0.##} mm";
            sb.AppendLine($"Created duct (Id {duct.Id}) — type '{ductType.Name}', system '{sysType.Name}', ACTUAL size {actualSize}, {startXMm},{startYMm},{startZMm} -> {endXMm},{endYMm},{endZMm} mm.");
            if (sizeNotes.Count > 0) sb.AppendLine("  " + string.Join("; ", sizeNotes));
            if (skippedSizes.Count > 0) sb.AppendLine($"  Size(s) not applicable to this duct shape, skipped: {string.Join(", ", skippedSizes)}.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create duct — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
