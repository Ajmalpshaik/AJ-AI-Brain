// ============================================================
// FRAGMENT (filter) — filter-by-element-intersection.cs
// PURPOSE: Elements physically touching/clashing a specific target element — real geometric solid
//          intersection (Revit's own ElementIntersectsElementFilter), not just an overlapping bounding
//          box like filter-by-region.cs. Use for "what's clashing with this duct/wall/beam".
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// LIVE-VERIFIED 2026-07-22: mechanism confirmed correct both ways — tested against a duct deliberately
// overlapped with a copy of itself (found it, as expected), AND against two ducts joined by a real elbow
// fitting at a shared connector (found NOTHING, including across every category, not just the one tested).
// CLARIFICATION, not a bug: MEP elements joined at a connector are TOUCHING/abutting, not VOLUMETRICALLY
// OVERLAPPING — ElementIntersectsElementFilter tests real solid overlap, and two connected pipes/ducts/
// fittings meeting cleanly at a shared face generally do NOT overlap in that sense. This filter is for
// genuine physical clashes (two things occupying the same space) — for "what's connected to this MEP
// element", use filter-by-connection-status.cs or recipes/trace-mep-circuits.cs instead, not this fragment.
// The target element itself is always excluded from the result — it always "intersects" itself.
//
// ✱✱ NOT EVERY ELEMENT CAN BE THE TARGET, and the old version found out by throwing. Revit answers the
//    question directly, before you build the filter:
//        ElementIntersectsFilter.IsCategorySupported(element)  — is this CATEGORY testable at all
//        ElementIntersectsFilter.IsElementSupported(element)   — is THIS element testable
//    Added 2026-08-23. It matters more than a nicer error message: the same blind spot in
//    action-report-clashes.cs was silently dropping elements from a coordination report, so the pair of
//    fixes went in together. Typically refused: annotation and view-specific elements, anything with no
//    solid geometry, and elements living in a linked document (a link needs its own transform).
//    NOTE the candidates are not pre-checked — a candidate that cannot be tested simply never passes the
//    filter, which is Revit's own behaviour and cannot be distinguished from "does not clash" here. If
//    you need that distinction, use action-report-clashes.cs, which reports both sets.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int targetElementIdInt = 0; // the element everything else is tested against — set explicitly
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves; // category of the CANDIDATES being tested, not the target
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

// Ask Revit whether the target can be geometrically tested at all, instead of letting the filter throw.
Func<Element, string> whyUntestable = el =>
{
    bool catOk = true, elOk = true;
    try { catOk = ElementIntersectsFilter.IsCategorySupported(el); } catch { }
    try { elOk = ElementIntersectsFilter.IsElementSupported(el); } catch { }
    if (!catOk) return $"category '{el.Category?.Name ?? "(none)"}' is not supported by geometric intersection";
    if (!elOk) return "element is not supported (usually it has no solid geometry)";
    return null;
};

var targetElement = Document.GetElement(new ElementId(targetElementIdInt));
string untestableReason = targetElement == null ? null : whyUntestable(targetElement);

if (targetElement == null)
{
    sb.AppendLine($"Target element Id {targetElementIdInt} not found.");
}
else if (untestableReason != null)
{
    sb.AppendLine($"Target '{targetElement.Name}' (Id {targetElement.Id}) CANNOT be clash-tested: {untestableReason}.");
    sb.AppendLine("This is not 'nothing clashes with it' — nothing was tested. Pick a target with real solid geometry, or use filter-by-region.cs for a bounding-box test.");
}
else
{
    var intersectFilter = new ElementIntersectsElementFilter(targetElement);
    var collector = new FilteredElementCollector(Document).WherePasses(intersectFilter).WhereElementIsNotElementType();
    if (useCategoryFilter) collector = collector.OfCategory(targetCategory);

    elements = collector.Where(e => e.Id != targetElement.Id).ToList();
    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    sb.AppendLine($"Filtered {elements.Count} element(s) physically intersecting '{targetElement.Name}' (Id {targetElement.Id}), candidates: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
