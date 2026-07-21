// ============================================================
// FRAGMENT (filter) — filter-by-element-intersection.cs
// PURPOSE: Elements physically touching/clashing a specific target element — real geometric solid
//          intersection (Revit's own ElementIntersectsElementFilter), not just an overlapping bounding
//          box like filter-by-region.cs. Use for "what's clashing with this duct/wall/beam".
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// The target element itself is always excluded from the result — it always "intersects" itself.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int targetElementIdInt = 0; // the element everything else is tested against — set explicitly
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves; // category of the CANDIDATES being tested, not the target
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

var targetElement = Document.GetElement(new ElementId(targetElementIdInt));
if (targetElement == null)
{
    sb.AppendLine($"Target element Id {targetElementIdInt} not found.");
}
else
{
    var intersectFilter = new ElementIntersectsElementFilter(targetElement);
    var collector = new FilteredElementCollector(Document).WherePasses(intersectFilter).WhereElementIsNotElementType();
    if (useCategoryFilter) collector = collector.OfCategory(targetCategory);

    elements = collector.Where(e => e.Id != targetElement.Id).ToList();
    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    sb.AppendLine($"Filtered {elements.Count} element(s) physically intersecting '{targetElement.Name}' (Id {targetElement.Id.IntegerValue}), candidates: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
