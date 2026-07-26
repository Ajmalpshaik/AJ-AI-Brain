// ============================================================
// FRAGMENT (creator) — create-mep-system-type.cs
// PURPOSE: Create a new MEP SYSTEM TYPE (duct or pipe) by duplicating an existing one, then set its name,
//          abbreviation and graphic colour — how a project gets "Supply Air - Zone 1", "CHWS", "CHWR" as
//          distinct, separately-filterable systems instead of everything on one default type.
// PRODUCES: elements (List<Element>, the new system type), sb
// NOT STANDALONE — see scripts/README.md for how to compose. Feeds filter-by-system-type.cs and every
//          drawing fragment that takes a systemTypeName.
// GOTCHA: Revit has no "create system type from nothing" API — every one is a Duplicate of an existing
//         type, which is why sourceTypeName is required. Duplicating carries over the source's settings
//         (calculation, fluid, material) — check them if the source wasn't already close to what's wanted.
// GOTCHA: the ABBREVIATION is what appears in system names and most schedules, so it matters more than
//         the type name in daily use — set it deliberately, don't leave the source's.
// GOTCHA: this creates the system TYPE (the classification). The system INSTANCES ("DXS 1") are created
//         by Revit automatically when elements are connected — see live-model/mep-trace.md.
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string domain = "duct";             // "duct" | "pipe"
string sourceTypeName = "";         // existing type to duplicate (required — see GOTCHA)
string newTypeName = "";            // name for the new system type
string abbreviation = null;         // null = keep the source's; set it deliberately (see GOTCHA)
int[] colorRgb = null;              // e.g. new[]{0,120,200}; null = keep the source's colour
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var systemTypes = domain == "pipe"
    ? new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystemType)).Cast<MEPSystemType>().ToList()
    : new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType)).Cast<MEPSystemType>().ToList();

var source = systemTypes.FirstOrDefault(s => s.Name == sourceTypeName);

if (domain != "duct" && domain != "pipe")
{
    sb.AppendLine($"Unknown domain '{domain}' — use \"duct\" or \"pipe\".");
}
else if (source == null)
{
    sb.AppendLine($"Source system type '{sourceTypeName}' not found among {domain} system types. Available: {string.Join(", ", systemTypes.Select(s => $"'{s.Name}'"))}");
}
else if (string.IsNullOrWhiteSpace(newTypeName))
{
    sb.AppendLine("newTypeName is empty — nothing to create.");
}
else if (systemTypes.Any(s => s.Name == newTypeName))
{
    sb.AppendLine($"A {domain} system type named '{newTypeName}' already exists (Id {systemTypes.First(s => s.Name == newTypeName).Id.IntegerValue}) — pick another name.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create MEP System Type"))
    {
        t.Start();
        try
        {
            var newType = source.Duplicate(newTypeName) as MEPSystemType;
            if (newType == null)
            {
                t.RollBack();
                sb.AppendLine($"Duplicate of '{sourceTypeName}' did not return a system type — nothing created.");
            }
            else
            {
                var applied = new List<string>();

                if (!string.IsNullOrEmpty(abbreviation))
                {
                    var pAbbr = newType.get_Parameter(BuiltInParameter.RBS_SYSTEM_ABBREVIATION_PARAM);
                    if (pAbbr != null && !pAbbr.IsReadOnly) { pAbbr.Set(abbreviation); applied.Add($"abbreviation '{abbreviation}'"); }
                    else applied.Add("abbreviation NOT settable on this type");
                }

                if (colorRgb != null && colorRgb.Length == 3)
                {
                    try
                    {
                        newType.LineColor = new Color((byte)colorRgb[0], (byte)colorRgb[1], (byte)colorRgb[2]);
                        applied.Add($"colour RGB({colorRgb[0]},{colorRgb[1]},{colorRgb[2]})");
                    }
                    catch { applied.Add("colour NOT settable on this type"); }
                }

                elements.Add(newType);
                t.Commit();
                sb.AppendLine($"Created {domain} system type '{newType.Name}' (Id {newType.Id.IntegerValue}) from '{sourceTypeName}'{(applied.Count > 0 ? " — " + string.Join(", ", applied) : "")}.");
                sb.AppendLine("  Settings inherited from the source (calculation, fluid, material) — check them if the source wasn't close.");
            }
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create system type — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
