// ============================================================
// FRAGMENT (filter) — filter-by-system-type.cs
// PURPOSE: Every pipe/fitting/duct/duct-fitting whose MEP System TYPE (the system's classification —
//          "Supply Air", "Domestic Cold Water", or whatever short code this project renamed it to, e.g.
//          "CDP") contains a filter string. Distinct from filter-by-system-name.cs, which matches one
//          specific System instance's own name (e.g. "Supply Air 2") — several different named systems
//          can share the same Type.
//          The filter string is ALWAYS an input — check ../../../knowledge/glossary.md for the user's word
//          -> the real Revit system-type name(s) (e.g. "refrigerant" -> anything containing "DXS").
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../../../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity
// ============================================================
// RBS_PIPING_SYSTEM_TYPE_PARAM / RBS_DUCT_SYSTEM_TYPE_PARAM are ElementId-storage parameters pointing at
// the PipingSystemType/MechanicalSystemType element — AsValueString() resolves that to its display name,
// which is what actually holds the project's short code (e.g. "CDP"). Fixed from an earlier version of
// this fragment that read RBS_SYSTEM_NAME_PARAM first — that parameter exists on nearly every pipe/duct
// element, so its "fall back to Type" branch never actually ran; it was matching System NAME, not Type.
// That system-NAME behavior now lives correctly in filter-by-system-name.cs instead.
// LIVE-VERIFIED 2026-07-23 — FOUND AND FIXED A REAL BUG, discovered while testing the closely-related
// recipes/trace-mep-circuits.cs which shares this exact pattern: `FilteredElementCollector.UnionWith()`
// does NOT preserve/combine each side's own quick-filters. Calling `.WhereElementIsNotElementType()`
// BEFORE `.OfCategory(...).UnionWith(...)` on each piece (as originally written) silently loses that
// filter in the merged result — confirmed empirically: the original pattern returned 52 elements for a
// 2-category union that should have returned 4, and every extra element was a TYPE (e.g. "Radius Elbows /
// Tees"), not an instance. My FIRST verification pass of this file only exercised a single-category
// simplification and missed this entirely — a real gap in that earlier check, not just in the code.
// FIXED by moving `.WhereElementIsNotElementType()` to run ONCE, after all the UnionWith calls, on the
// combined collector — confirmed via isolated A/B/C testing that this exact reordering is what fixes it
// (applying the filter per-side-before-union: broken; once-after-union: correct). Re-verified against
// real ducts + fitting: returns exactly the 4 real instances, zero type-elements.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemTypeContains = "DXS"; // e.g. "DXS" (refrigerant), "CDP" (condensate), "WSP" (water supply)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_PipeCurves)
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeFitting))
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves))
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctFitting))
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var typeParam = e.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM) ?? e.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
        var name = typeParam?.AsValueString() ?? "";
        return name.IndexOf(systemTypeContains, StringComparison.OrdinalIgnoreCase) >= 0;
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s), System Type contains '{systemTypeContains}'.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
