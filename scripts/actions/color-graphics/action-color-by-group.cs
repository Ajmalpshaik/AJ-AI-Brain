// ============================================================
// FRAGMENT (action) — action-color-by-group.cs
// PURPOSE: Apply a DISTINCT color per sub-group within `elements`, grouped by the actual value of any
//          named parameter (e.g. "color by System Type", "color by Level", "color by Comments") — not
//          just a hardcoded grouping rule. Five color-assignment modes: a fixed palette (default), a
//          gradient across ordered groups, or three hue-stepped modes (random/pastel/neon — see below).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: ../../../knowledge/live-model/mep-trace.md § Color-coding a traced run; parameter-value stringification and the
//         palette/gradient modes adapted from an external repository's
//         ColorSplashEventHandler (a different architecture — fixed MCP tools, not raw scripting — so
//         only the technique was taken, not any code, and re-verified against this project's own
//         version-compat rules, e.g. RevitCompat's ForgeTypeId boundary already documented separately).
// GOTCHA fixed 2026-07-22: "random" used to pick each group's R/G/B independently — two groups can land
//         on near-identical colors purely by chance (the user: "its not be identical visually also i
//         need different colors and randomize"). Fixed by stepping HUE evenly around the color wheel
//         (360/groupCount degrees apart) instead — GUARANTEES every group is visually distinct regardless
//         of group count, while a random starting hue each run still gives real variety between runs.
//         "pastel" and "neon" reuse the same guaranteed-distinct hue-stepping, just at a different
//         saturation/brightness band — see ../../../knowledge/live-model/color-vocabulary.md for the
//         general technique and for picking ONE named color (e.g. for action-set-color-uniform.cs).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string groupByParameterName = "RBS_SYSTEM_NAME_PARAM"; // any parameter display name the user names, e.g. "Level", "Comments", "System Type" — or a BuiltInParameter name as shown here for MEP system name
string colorMode = "palette"; // "palette" | "gradient" | "random" | "pastel" | "neon"
var palette = new List<(byte r, byte g, byte b)>
{
    (230, 25, 75), (60, 180, 75), (0, 130, 200), (245, 130, 48), (145, 30, 180), (0, 128, 128)
};
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View colorView = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

// Group by the parameter's actual value, storage-type-aware — handles Double (with units via
// AsValueString), ElementId (resolved to the referenced element's name), Integer (detects Yes/No
// boolean parameters so they don't show as "1"/"0"), and String, falling back to "ungrouped".
Func<Element, string> groupKey = e =>
{
    Parameter p = null;
    if (groupByParameterName == "RBS_SYSTEM_NAME_PARAM")
        p = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM) ?? e.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
    else
        p = e.LookupParameter(groupByParameterName);

    if (p == null || !p.HasValue) return "None";

    switch (p.StorageType)
    {
        case StorageType.Double:
            return p.AsValueString() ?? p.AsDouble().ToString();
        case StorageType.ElementId:
            var id = p.AsElementId();
            if (id == ElementId.InvalidElementId) return "None";
            var refElem = Document.GetElement(id);
            return refElem?.Name ?? id.ToString();
        case StorageType.Integer:
            var valueString = p.AsValueString();
            if (!string.IsNullOrEmpty(valueString) && (valueString == "Yes" || valueString == "No"))
                return valueString;
            return p.AsValueString() ?? p.AsInteger().ToString();
        case StorageType.String:
            return p.AsString() ?? "None";
        default:
            return "None";
    }
};

var solidFillPattern = new FilteredElementCollector(Document)
    .OfClass(typeof(FillPatternElement))
    .Cast<FillPatternElement>()
    .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

if (colorView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
var groups = elements.GroupBy(groupKey).ToList();
var rnd = new Random();

// Plain HSV -> RGB conversion (no System.Drawing dependency — not guaranteed available in this script
// context). h is degrees, 0 to 360; s and v are each 0 to 1.
Func<double, double, double, (byte r, byte g, byte b)> hsvToRgb = (h, s, v) =>
{
    h = h % 360; if (h < 0) h += 360;
    double c = v * s;
    double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
    double m = v - c;
    double r1, g1, b1;
    if (h < 60) { r1 = c; g1 = x; b1 = 0; }
    else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
    else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
    else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
    else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
    else { r1 = c; g1 = 0; b1 = x; }
    return ((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
};

// Evenly-spaced hues guarantee every group is visually distinct from every other, no matter how many
// groups there are — independent-random RGB can't guarantee that (two random colors can land close
// together by chance). A random starting hue, re-rolled each run, still gives real run-to-run variety.
double hueOffset = rnd.NextDouble() * 360.0;
double hueStep = groups.Count > 0 ? 360.0 / groups.Count : 0;

Func<int, (byte r, byte g, byte b)> pickColor = gi =>
{
    if (colorMode == "gradient" && groups.Count > 1)
    {
        double ratio = (double)gi / (groups.Count - 1);
        return ((byte)(0 + (180 - 0) * ratio), (byte)0, (byte)(180 + (0 - 180) * ratio)); // blue -> red
    }
    if (colorMode == "random") return hsvToRgb(hueOffset + hueStep * gi, 0.65, 0.85); // vivid, guaranteed-separated hues
    if (colorMode == "pastel") return hsvToRgb(hueOffset + hueStep * gi, 0.35, 0.95); // soft, light — low saturation, high brightness
    if (colorMode == "neon") return hsvToRgb(hueOffset + hueStep * gi, 1.0, 1.0); // maximum saturation + brightness
    return palette[gi % palette.Count]; // "palette" default
};

using (var t = new Transaction(Document, "AJ Tools - Color By Group"))
{
    t.Start();
    try
    {
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var (r, g, b) = pickColor(gi);
            var color = new Autodesk.Revit.DB.Color(r, g, b);
            foreach (var e in groups[gi])
            {
                var ogs = colorView.GetElementOverrides(e.Id);
                ogs.SetProjectionLineColor(color);
                ogs.SetCutLineColor(color);
                if (solidFillPattern != null)
                {
                    ogs.SetSurfaceForegroundPatternColor(color);
                    ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
                    ogs.SetSurfaceForegroundPatternVisible(true);
                    ogs.SetCutForegroundPatternColor(color);
                    ogs.SetCutForegroundPatternId(solidFillPattern.Id);
                    ogs.SetCutForegroundPatternVisible(true);
                }
                colorView.SetElementOverrides(e.Id, ogs);
            }
        }
        t.Commit();
        sb.AppendLine($"Colored {groups.Count} group(s) by '{groupByParameterName}' ({colorMode} mode, {elements.Count} element(s) total).");
        foreach (var grp in groups) sb.AppendLine($"  {grp.Key}: {grp.Count()} element(s).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to color by group — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
