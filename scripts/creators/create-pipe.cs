// ============================================================
// FRAGMENT (creator) — create-pipe.cs
// PURPOSE: Draw ONE straight pipe between two mm points — Plumbing twin of create-duct.cs.
// PRODUCES: elements (List<Element>, the single new Pipe), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: setting Diameter picks the nearest size the PipeType's routing preference/segment allows —
//         read the value back (verify-don't-trust) if the exact size matters.
// GOTCHA: a bare Pipe.Create makes an unconnected segment — fittings/system joins are recipe territory;
//         check open ends after with filter-by-connection-status.cs.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemTypeName = null;   // e.g. "Domestic Cold Water"; null = first PipingSystemType
string pipeTypeName = null;     // null = first PipeType
int levelIdInt = 0;             // reference level
double startXMm = 0, startYMm = 0, startZMm = 2700;
double endXMm = 3000, endYMm = 0, endZMm = 2700;
double diameterMm = 50;         // 0 = keep type default
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
var sysTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystemType)).Cast<Autodesk.Revit.DB.Plumbing.PipingSystemType>().ToList();
var sysType = systemTypeName == null ? sysTypes.FirstOrDefault() : sysTypes.FirstOrDefault(s => s.Name == systemTypeName);
var pipeTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeType)).Cast<Autodesk.Revit.DB.Plumbing.PipeType>().ToList();
var pipeType = pipeTypeName == null ? pipeTypes.FirstOrDefault() : pipeTypes.FirstOrDefault(p => p.Name == pipeTypeName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (sysType == null) sb.AppendLine(systemTypeName == null ? "No PipingSystemType in the project." : $"System type '{systemTypeName}' not found. Available: {string.Join(", ", sysTypes.Select(s => s.Name))}");
else if (pipeType == null) sb.AppendLine(pipeTypeName == null ? "No PipeType in the project." : $"Pipe type '{pipeTypeName}' not found. Available: {string.Join(", ", pipeTypes.Select(p => p.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Pipe"))
    {
        t.Start();
        try
        {
            var p1 = new XYZ(mm(startXMm), mm(startYMm), mm(startZMm));
            var p2 = new XYZ(mm(endXMm), mm(endYMm), mm(endZMm));
            var pipe = Autodesk.Revit.DB.Plumbing.Pipe.Create(Document, sysType.Id, pipeType.Id, level.Id, p1, p2);

            // THIS FRAGMENT IS THE PROOF CASE for why a size must be read back. Measured live
            // 2026-08-07: asking a pipe for 77mm returned Set() == TRUE and produced an 80mm pipe —
            // Revit snapped to the nearest size in the type's table and reported success. The header
            // already said "read the value back if the exact size matters"; now it does.
            // See ../../knowledge/live-model/core.md.
            string diaNote = null;
            if (diameterMm > 0)
            {
                var pD = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (pD != null && !pD.IsReadOnly)
                {
                    bool ok = pD.Set(mm(diameterMm));
                    // Regenerate BEFORE reading back — straight after Set() the parameter still reports
                    // 77mm; it only becomes 80mm at regeneration. Without this the SNAPPED note, which
                    // is the entire point of this fragment's fix, never fires.
                    Document.Regenerate();
                    double gotMm = UnitUtils.ConvertFromInternalUnits(pD.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                    if (!ok) diaNote = $"Diameter REFUSED {diameterMm}mm — still {gotMm:0.##}mm";
                    else if (Math.Abs(gotMm - diameterMm) > 0.5) diaNote = $"Diameter SNAPPED {diameterMm}mm -> {gotMm:0.##}mm (nearest size '{pipeType.Name}' allows)";
                }
                else sb.AppendLine("  Diameter parameter not settable on this pipe — type default kept.");
            }

            elements.Add(pipe);
            t.Commit();

            var dFinal = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            string actualDia = dFinal == null ? "unknown" : $"{UnitUtils.ConvertFromInternalUnits(dFinal.AsDouble(), DisplayUnitType.DUT_MILLIMETERS):0.##} mm";
            sb.AppendLine($"Created pipe (Id {pipe.Id.IntegerValue}) — type '{pipeType.Name}', system '{sysType.Name}', ACTUAL dia {actualDia}, {startXMm},{startYMm},{startZMm} -> {endXMm},{endYMm},{endZMm} mm.");
            if (diaNote != null) sb.AppendLine("  " + diaNote);
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create pipe — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
