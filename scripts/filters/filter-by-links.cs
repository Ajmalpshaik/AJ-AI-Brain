// ============================================================
// FRAGMENT (filter) — filter-by-links.cs
// PURPOSE: Every linked model instance in the document — RVT links (RevitLinkInstance) and/or CAD links
//          (ImportInstance: DWG/DXF/etc.), optionally narrowed by a name substring. Feeds
//          actions/parameters-naming/action-set-workset.cs for "move the links onto a workset" — links are
//          ordinary elements with a Workset parameter (ELEM_PARTITION_PARAM) just like anything else, that
//          action already works on them without any change.
// PRODUCES: elements (List<Element>, each one really a RevitLinkInstance or ImportInstance — cast back
//           with `as RevitLinkInstance`/`as ImportInstance` in the action if needed), sb (StringBuilder)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "both"; // "rvt" | "cad" | "both"
string nameContains = ""; // "" = every link; else substring match on the link instance's own Name, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var found = new List<Element>();

if (mode == "rvt" || mode == "both")
{
    found.AddRange(new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Cast<Element>());
}
if (mode == "cad" || mode == "both")
{
    found.AddRange(new FilteredElementCollector(Document).OfClass(typeof(ImportInstance)).Cast<Element>());
}

List<Element> elements = string.IsNullOrEmpty(nameContains)
    ? found
    : found.Where(e => (e.Name ?? "").IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

sb.AppendLine($"Filtered {elements.Count} link instance(s), mode: {mode}" + (string.IsNullOrEmpty(nameContains) ? "" : $", name contains \"{nameContains}\"") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
