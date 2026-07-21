// ============================================================
// FRAGMENT (filter) — filter-by-system-name.cs
// PURPOSE: Every pipe/fitting/duct/duct-fitting whose MEP System NAME (one specific System instance's
//          own name, e.g. "Supply Air 2", "DXS 1" — auto-numbered off its System Type unless renamed)
//          contains a filter string. Distinct from filter-by-system-type.cs, which matches every system
//          sharing a TYPE/classification, not just one specific run/loop.
//          The filter string is ALWAYS an input — check ../../knowledge/glossary.md for the user's word
//          -> the real Revit system name(s).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity — this is the
//         system-NAME half of what filter-by-system-type.cs used to conflate before being split in two.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemNameContains = "DXS 1"; // one specific System instance's own name — e.g. "Supply Air 2", "DXS 1"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<Element> elements = new FilteredElementCollector(Document)
    .WhereElementIsNotElementType()
    .OfCategory(BuiltInCategory.OST_PipeCurves)
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_PipeFitting))
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_DuctCurves))
    .UnionWith(new FilteredElementCollector(Document).WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_DuctFitting))
    .Where(e =>
    {
        var nameParam = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
        var name = nameParam?.AsString() ?? nameParam?.AsValueString() ?? "";
        return name.IndexOf(systemNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s), System Name contains '{systemNameContains}'.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
