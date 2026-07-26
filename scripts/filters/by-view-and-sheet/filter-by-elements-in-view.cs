// ============================================================
// FRAGMENT (filter) — filter-by-elements-in-view.cs
// PURPOSE: Every instance of a category that is actually VISIBLE in a given view (view-specific
//          graphics/filters/crop/phase all apply) — not just "exists in the model". Works against any
//          view by Id, not only the active one. For "in the active view" specifically, several other
//          filters already offer an activeViewOnly toggle; use this one when the target view isn't the
//          one currently on screen.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not open on screen
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    var collector = new FilteredElementCollector(Document, view.Id).WhereElementIsNotElementType();
    if (useCategoryFilter) collector = collector.OfCategory(targetCategory);

    elements = collector.ToList();
    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    sb.AppendLine($"Filtered {elements.Count} element(s) visible in view '{view.Name}', category: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
