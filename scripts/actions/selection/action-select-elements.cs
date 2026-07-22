// ============================================================
// FRAGMENT (action) — action-select-elements.cs
// PURPOSE: Set, ADD TO, or REMOVE FROM the active Revit selection using `elements` — so after the script
//          finishes, the user sees exactly the intended set highlighted in the Revit UI. Default mode
//          REPLACES the whole selection (matches UIDocument.Selection.SetElementIds' native behavior);
//          "add"/"remove" modify whatever's already selected instead of wiping it out.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "replace"; // "replace" | "add" | "remove"
// ---- END INPUTS ----

var newIds = elements.Select(e => e.Id).ToList();

if (mode == "add")
{
    var current = new HashSet<ElementId>(UIDocument.Selection.GetElementIds());
    foreach (var id in newIds) current.Add(id);
    UIDocument.Selection.SetElementIds(current.ToList());
    sb.AppendLine($"Added {elements.Count} element(s) to the current selection (now {current.Count} total).");
}
else if (mode == "remove")
{
    var current = new HashSet<ElementId>(UIDocument.Selection.GetElementIds());
    foreach (var id in newIds) current.Remove(id);
    UIDocument.Selection.SetElementIds(current.ToList());
    sb.AppendLine($"Removed {elements.Count} element(s) from the current selection (now {current.Count} remaining).");
}
else
{
    UIDocument.Selection.SetElementIds(newIds);
    sb.AppendLine($"Selected {elements.Count} element(s) in Revit (replaced prior selection).");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
