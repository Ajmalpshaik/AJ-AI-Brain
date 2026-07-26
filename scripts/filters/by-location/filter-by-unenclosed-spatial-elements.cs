// ============================================================
// FRAGMENT (filter) — filter-by-unenclosed-spatial-elements.cs
// PURPOSE: QA sweep — every Room and/or Space in the model that came out unbounded (zero Area, "Not
//          Enclosed") — the systematic version of the single-creation-time warning creators/create-room.cs
//          and creators/create-space.cs already print. Use this to find every problem room/space already
//          sitting in the model, not just ones just created this session.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string scope = "both"; // "rooms" | "spaces" | "both"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var candidates = new List<SpatialElement>();

if (scope == "rooms" || scope == "both")
{
    candidates.AddRange(new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_Rooms)
        .WhereElementIsNotElementType()
        .Cast<SpatialElement>());
}
if (scope == "spaces" || scope == "both")
{
    candidates.AddRange(new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_MEPSpaces)
        .WhereElementIsNotElementType()
        .Cast<SpatialElement>());
}

List<Element> elements = candidates.Where(sp => sp.Area <= 0).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} unenclosed (zero-area) Room/Space element(s) out of {candidates.Count} checked, scope: {scope}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
