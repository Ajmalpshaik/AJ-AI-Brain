// ============================================================
// FRAGMENT (creator) — create-conduit.cs
// PURPOSE: Draw ONE straight conduit between two mm points — the round electrical containment twin of
//          create-cable-tray.cs.
// PRODUCES: elements (List<Element>, the single new Conduit), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: Diameter here is the conduit's nominal/outer diameter parameter — the size snaps to what the
//         type's size table allows; read back if the exact value matters.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string conduitTypeName = null;  // null = first ConduitType
int levelIdInt = 0;             // reference level
double startXMm = 0, startYMm = 0, startZMm = 2800;
double endXMm = 3000, endYMm = 0, endZMm = 2800;
double diameterMm = 25;         // 0 = keep type default
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
var condTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Electrical.ConduitType)).Cast<Autodesk.Revit.DB.Electrical.ConduitType>().ToList();
var condType = conduitTypeName == null ? condTypes.FirstOrDefault() : condTypes.FirstOrDefault(ct => ct.Name == conduitTypeName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (condType == null) sb.AppendLine(conduitTypeName == null ? "No ConduitType in the project." : $"Conduit type '{conduitTypeName}' not found. Available: {string.Join(", ", condTypes.Select(ct => ct.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Conduit"))
    {
        t.Start();
        try
        {
            var p1 = new XYZ(mm(startXMm), mm(startYMm), mm(startZMm));
            var p2 = new XYZ(mm(endXMm), mm(endYMm), mm(endZMm));
            var conduit = Autodesk.Revit.DB.Electrical.Conduit.Create(Document, condType.Id, p1, p2, level.Id);

            // The old summary printed `dia {diameterMm} mm` from the INPUT, so a refused or snapped
            // diameter was reported as though it had landed — and this file's own header already warned
            // that the size snaps to the type's size table. Read it back instead. Measured live
            // 2026-08-07: a pipe asked for 77mm returned Set() == true and came out 80mm.
            // See ../../knowledge/live-model/core.md.
            string diaNote = null;
            if (diameterMm > 0)
            {
                var pD = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                if (pD != null && !pD.IsReadOnly)
                {
                    bool ok = pD.Set(mm(diameterMm));
                    // Regenerate BEFORE reading back — straight after Set() the parameter still reports
                    // the requested value; the snap to the type's size table happens at regeneration.
                    Document.Regenerate();
                    double gotMm = UnitUtils.ConvertFromInternalUnits(pD.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                    if (!ok) diaNote = $"Diameter REFUSED {diameterMm}mm — still {gotMm:0.##}mm";
                    else if (Math.Abs(gotMm - diameterMm) > 0.5) diaNote = $"Diameter SNAPPED {diameterMm}mm -> {gotMm:0.##}mm (nearest size '{condType.Name}' allows)";
                }
                else sb.AppendLine("  Diameter parameter not settable — type default kept.");
            }

            elements.Add(conduit);
            t.Commit();

            var dFinal = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
            string actualDia = dFinal == null ? "unknown" : $"{UnitUtils.ConvertFromInternalUnits(dFinal.AsDouble(), DisplayUnitType.DUT_MILLIMETERS):0.##} mm";
            sb.AppendLine($"Created conduit (Id {conduit.Id.IntegerValue}) — type '{condType.Name}', ACTUAL dia {actualDia}, {startXMm},{startYMm},{startZMm} -> {endXMm},{endYMm},{endZMm} mm.");
            if (diaNote != null) sb.AppendLine("  " + diaNote);
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create conduit — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
