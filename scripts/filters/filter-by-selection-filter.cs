// ============================================================
// FRAGMENT (filter) — filter-by-selection-filter.cs
// PURPOSE: Retrieve the actual elements referenced by an existing named Selection Filter (or View Filter's
//          rule-matched result in the given view) — the read-back counterpart to
//          actions/color-graphics/action-create-selection-filter.cs, which only saves a set, and
//          actions/color-graphics/action-create-view-filter.cs, which only defines a rule. Use this when
//          the request is "what's in that filter I saved earlier", or to reuse a saved set as the starting
//          `elements` for a completely different action than the one it was originally saved for.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// GOTCHA: a SelectionFilterElement stores an explicit Id list directly (SetElementIds/GetElementIds) — a
//         true saved selection. A ParameterFilterElement (View Filter) stores a RULE, not a set of Ids —
//         reading "its elements" means re-running that rule against one specific view's own collector, so
//         viewIdInt is REQUIRED for that case and ignored for a true Selection Filter.
// Live-verified 2026-07-22, both branches (named Selection Filter and View Filter re-evaluation).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "";
int viewIdInt = 0; // required only if filterName resolves to a View Filter (ParameterFilterElement); 0 = Document.ActiveView
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

var filterElement = new FilteredElementCollector(Document)
    .OfClass(typeof(FilterElement))
    .Cast<FilterElement>()
    .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

if (filterElement == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(FilterElement)).Cast<FilterElement>();
    sb.AppendLine($"Filter '{filterName}' not found. Available: {string.Join(", ", available.Select(f => f.Name))}");
}
else if (filterElement is SelectionFilterElement selFilter)
{
    var ids = selFilter.GetElementIds();
    elements = ids.Select(id => Document.GetElement(id)).Where(e => e != null).ToList();
    sb.AppendLine($"Filtered {elements.Count} element(s) from Selection Filter '{filterElement.Name}'.");
}
else if (filterElement is ParameterFilterElement paramFilter)
{
    var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;
    if (view == null)
    {
        sb.AppendLine($"'{filterElement.Name}' is a View Filter (rule-based) — needs a real view to re-evaluate against. View not found for viewIdInt {viewIdInt}.");
    }
    else
    {
        var categoryIds = paramFilter.GetCategories();
        var elementFilter = paramFilter.GetElementFilter();
        var collector = new FilteredElementCollector(Document, view.Id)
            .WherePasses(new ElementMulticategoryFilter(categoryIds))
            .WhereElementIsNotElementType();
        if (elementFilter != null) collector = collector.WherePasses(elementFilter);

        elements = collector.ToList();
        sb.AppendLine($"Filtered {elements.Count} element(s) matching View Filter '{filterElement.Name}''s rule, re-evaluated in view '{view.Name}'.");
    }
}
else
{
    sb.AppendLine($"'{filterElement.Name}' is a FilterElement of an unrecognized subtype ({filterElement.GetType().Name}) — neither Selection nor Parameter filter.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
