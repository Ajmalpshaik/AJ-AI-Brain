// ============================================================
// FRAGMENT (filter) — filter-by-family.cs
// PURPOSE: Every instance belonging to a specific Family name, scanned across the WHOLE model — no
//          category picked first. Use this when the family name is known but which category it lives in
//          isn't (or the same family name is being searched across more than one category). If the
//          category is already known, prefer filter-by-category-and-family.cs instead — it's narrower
//          and cheaper.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Whole-model, unbounded scan — per ../../../knowledge/live-model/core.md "watch for unbounded output",
// this walks every FamilyInstance in the document. Fine for a single family-name lookup; don't chain
// several of these in one composed script.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string familyNameContains = "VCD";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<Element> elements = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilyInstance))
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(fi => fi.Symbol?.Family?.Name?.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
    .Cast<Element>()
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s), family name contains '{familyNameContains}', across all categories.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
