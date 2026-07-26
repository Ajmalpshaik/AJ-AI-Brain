// ============================================================
// FRAGMENT (filter) — filter-by-scope-box.cs
// PURPOSE: Every Scope Box in the model, optionally narrowed by a name substring — feeds
//          action-assign-scope-box-to-view.cs, action-rename-element.cs (rename it), or
//          action-delete-elements.cs (deleting a scope box is just deleting an ordinary element, no
//          dedicated delete fragment needed for that alone).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every Scope Box; else substring match, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
    .WhereElementIsNotElementType()
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(e => (e.Name ?? "").IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(e => e.Name).ToList();
sb.AppendLine($"Filtered {elements.Count} scope box(es)." + (string.IsNullOrEmpty(nameContains) ? "" : $" (name contains \"{nameContains}\")"));
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
