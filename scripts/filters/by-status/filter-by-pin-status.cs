// ============================================================
// FRAGMENT (filter) — filter-by-pin-status.cs
// PURPOSE: Category elements that ARE or ARE NOT pinned — QA sweep ("show me everything pinned").
//          Reverse direction of action-set-pin-state.cs, which CHANGES pin state rather than finding it.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
bool wantPinned = true; // true = only pinned elements; false = only unpinned elements
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e => e.Pinned == wantPinned)
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}, {(wantPinned ? "pinned" : "not pinned")}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
