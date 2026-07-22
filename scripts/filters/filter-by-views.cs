// ============================================================
// FRAGMENT (filter) — filter-by-views.cs
// PURPOSE: Every View in the model (Floor Plans, Sections, 3D, Elevations, etc. — NOT ViewSheet, use
//          filter-by-sheets.cs for those), optionally narrowed by ViewType and/or a name substring. Use
//          this to feed a set of views into action-duplicate-views.cs or
//          action-place-viewport-on-sheet.cs.
// PRODUCES: elements (List<Element>, each one really a View — cast back with `as View` in the action),
//           sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Excludes view templates (IsTemplate) and Schedule/Sheet view types by default — those aren't "a view
// you'd duplicate or place on a sheet" in the normal sense. For View Templates themselves, use
// filter-by-view-templates.cs instead.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ViewType? viewTypeFilter = null; // null = every view type; else e.g. ViewType.FloorPlan, ViewType.ThreeD, ViewType.Section
string nameContains = ""; // "" = no name filter
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(View))
    .Cast<View>()
    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.DrawingSheet)
    .AsEnumerable();

if (viewTypeFilter.HasValue)
{
    query = query.Where(v => v.ViewType == viewTypeFilter.Value);
}
if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(v => v.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(v => v.Name).Cast<Element>().ToList();
string typeLabel = viewTypeFilter.HasValue ? viewTypeFilter.Value.ToString() : "any type";
sb.AppendLine($"Filtered {elements.Count} view(s), type: {typeLabel}" + (string.IsNullOrEmpty(nameContains) ? "" : $", name contains \"{nameContains}\"") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
