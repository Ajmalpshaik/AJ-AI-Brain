// ============================================================
// FRAGMENT (action) — action-find-duplicate-values.cs
// PURPOSE: Flag elements that share the SAME value in a named parameter — e.g. two doors both marked
//          "D-101", two pieces of equipment with the same Equipment Tag. Different QA check from
//          action-find-duplicates.cs, which flags duplicate LOCATIONS (stacked elements), not duplicate
//          DATA. Read-only, never deletes or moves anything.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Mark";
bool ignoreBlank = true; // true = don't flag elements that all share an EMPTY value as "duplicates"
bool selectFlagged = true;
// ---- END INPUTS ----

Func<Element, string> valueOf = e =>
{
    var p = e.LookupParameter(parameterName);
    if (p == null || !p.HasValue) return null;
    return p.StorageType == StorageType.String ? p.AsString() : (p.AsValueString() ?? p.AsInteger().ToString());
};

var groups = elements
    .Select(e => (el: e, val: valueOf(e)))
    .Where(x => !ignoreBlank || !string.IsNullOrEmpty(x.val))
    .GroupBy(x => x.val)
    .Where(g => g.Key != null && g.Count() > 1)
    .ToList();

var flagged = groups.SelectMany(g => g.Select(x => x.el)).ToList();

sb.AppendLine($"Checked {elements.Count} element(s) on '{parameterName}': {groups.Count} duplicate value(s), {flagged.Count} element(s) flagged.");
foreach (var g in groups)
    sb.AppendLine($"  '{g.Key}': " + string.Join(", ", g.Select(x => x.el.Id.IntegerValue)));

if (selectFlagged && flagged.Count > 0)
{
    UIDocument.Selection.SetElementIds(flagged.Select(e => e.Id).ToList());
    sb.AppendLine($"Selected {flagged.Count} flagged element(s) in Revit for review.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
