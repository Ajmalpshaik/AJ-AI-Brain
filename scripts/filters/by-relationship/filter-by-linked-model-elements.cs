// ============================================================
// FRAGMENT (filter) — filter-by-linked-model-elements.cs
// PURPOSE: Elements of one category INSIDE a specific linked RVT model (not the link instance itself —
//          use filter-by-links.cs for that) — "how many beams are in the structural link", "what ducts
//          exist in the linked MEP model". Coordinates are read in the LINK's own coordinate system;
//          transformToHostCoords converts each returned element's reported values via the link's placement
//          transform if geometry math is needed downstream.
// PRODUCES: elements (List<Element>) — these belong to the LINKED document, NOT `Document` (the host)
//          — see the GOTCHA below before composing with an action.
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// GOTCHA, READ BEFORE CHAINING AN ACTION: these Element objects live in the LINKED document, not
// `Document`. Every action fragment in this library that calls `Document.GetElement(e.Id)` or opens a
// Transaction on `Document` expecting to edit them will NOT work — the Id only resolves inside the link's
// own document, and nothing here can write into a link from the host session anyway (open/edit the linked
// file directly for that). SAFE to compose with read-only reporting actions that use the element object
// directly without re-resolving it (action-report-parameters.cs, action-report-parameter-inventory.cs,
// action-count-and-report.cs, action-count-by-group.cs) — those just call methods on `e` itself. NOT safe
// with anything that moves/sets/deletes.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string linkNameContains = ""; // "" = the first loaded link found; else substring match on the link instance's Name
BuiltInCategory targetCategory = BuiltInCategory.OST_StructuralFraming;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

var linkInstance = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
    .FirstOrDefault(li => string.IsNullOrEmpty(linkNameContains) || (li.Name ?? "").IndexOf(linkNameContains, StringComparison.OrdinalIgnoreCase) >= 0);

if (linkInstance == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
    sb.AppendLine($"No loaded link instance found" + (string.IsNullOrEmpty(linkNameContains) ? "." : $" matching '{linkNameContains}'.") +
        $" Available: {string.Join(", ", available.Select(li => li.Name))}");
}
else
{
    var linkedDoc = linkInstance.GetLinkDocument();
    if (linkedDoc == null)
    {
        sb.AppendLine($"Link '{linkInstance.Name}' is not currently loaded (unloaded or not found) — can't read its elements.");
    }
    else
    {
        elements = new FilteredElementCollector(linkedDoc)
            .OfCategory(targetCategory)
            .WhereElementIsNotElementType()
            .ToList();
        sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory} inside linked model '{linkInstance.Name}'.");
    }
}
// ---- continue with a READ-ONLY action fragment below (see GOTCHA above), or add return sb.ToString(); to stop here ----
