// ============================================================
// FRAGMENT (filter) — filter-by-levels.cs
// PURPOSE: Every Level element in the model ITSELF, optionally narrowed by a name substring — NOT the
//          same job as filter-by-elements-on-level.cs, which finds elements SITTING ON a given level. Use this one to
//          feed creators/create-dimension.cs (a dimension string across named Levels) or to rename/report
//          on the Levels themselves.
// PRODUCES: elements (List<Element>, each one really a Level — cast back with `as Level` in the action),
//           sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every Level; else substring match, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(l => l.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(l => l.Elevation).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} level(s), ordered by elevation." + (string.IsNullOrEmpty(nameContains) ? "" : $" (name contains \"{nameContains}\")"));
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
