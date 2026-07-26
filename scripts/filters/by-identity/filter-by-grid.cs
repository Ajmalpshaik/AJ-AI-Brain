// ============================================================
// FRAGMENT (filter) — filter-by-grid.cs
// PURPOSE: Every Grid in the model, optionally narrowed by a name substring — feeds
//          creators/create-dimension.cs (a dimension string across named Grids) or any report/rename
//          action that needs the Grid elements themselves, not elements sitting near one.
// PRODUCES: elements (List<Element>, each one really a Grid — cast back with `as Grid` in the action),
//           sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every Grid; else substring match, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(Grid))
    .Cast<Grid>()
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(g => g.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(g => g.Name).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} grid(s)." + (string.IsNullOrEmpty(nameContains) ? "" : $" (name contains \"{nameContains}\")"));
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
