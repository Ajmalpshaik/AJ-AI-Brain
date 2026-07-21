// ============================================================
// FRAGMENT (filter) — filter-by-system-type.cs
// PURPOSE: Every pipe/fitting/duct/duct-fitting whose MEP System TYPE (the system's classification —
//          "Supply Air", "Domestic Cold Water", or whatever short code this project renamed it to, e.g.
//          "CDP") contains a filter string. Distinct from filter-by-system-name.cs, which matches one
//          specific System instance's own name (e.g. "Supply Air 2") — several different named systems
//          can share the same Type.
//          The filter string is ALWAYS an input — check ../../knowledge/glossary.md for the user's word
//          -> the real Revit system-type name(s) (e.g. "refrigerant" -> anything containing "DXS").
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity
// ============================================================
// RBS_PIPING_SYSTEM_TYPE_PARAM / RBS_DUCT_SYSTEM_TYPE_PARAM are ElementId-storage parameters pointing at
// the PipingSystemType/MechanicalSystemType element — AsValueString() resolves that to its display name,
// which is what actually holds the project's short code (e.g. "CDP"). Fixed from an earlier version of
// this fragment that read RBS_SYSTEM_NAME_PARAM first — that parameter exists on nearly every pipe/duct
// element, so its "fall back to Type" branch never actually ran; it was matching System NAME, not Type.
// That system-NAME behavior now lives correctly in filter-by-system-name.cs instead.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemTypeContains = "DXS"; // e.g. "DXS" (refrigerant), "CDP" (condensate), "WSP" (water supply)
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
        var typeParam = e.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM) ?? e.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
        var name = typeParam?.AsValueString() ?? "";
        return name.IndexOf(systemTypeContains, StringComparison.OrdinalIgnoreCase) >= 0;
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s), System Type contains '{systemTypeContains}'.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
