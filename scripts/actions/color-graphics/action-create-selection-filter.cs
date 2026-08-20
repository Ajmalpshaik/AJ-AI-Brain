// ============================================================
// FRAGMENT (action) — action-create-selection-filter.cs
// PURPOSE: Save `elements` as a named Revit SELECTION FILTER (SelectionFilterElement) — an explicit list
//          of specific elements, not a rule. Usable in the Filters tab exactly like a rule-based View
//          Filter (action-create-view-filter.cs) — pair with action-apply-view-filter.cs to color/show
//          it in a view. Use this when the elements to color don't share one clean parameter rule (e.g.
//          "these 12 items I hand-picked"); use action-create-view-filter.cs when they do (e.g.
//          "everything where System Type contains CDP").
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// STATUS: not yet live-verified.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "My Selection Filter 1";
// ---- END INPUTS ----

using (var t = new Transaction(Document, "AJ Tools - Create Selection Filter"))
{
    t.Start();
    try
    {
        var ids = elements.Select(e => e.Id).ToList();
        var existing = new FilteredElementCollector(Document).OfClass(typeof(SelectionFilterElement))
            .Cast<SelectionFilterElement>()
            .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.SetElementIds(ids);
            t.Commit();
            sb.AppendLine($"Updated existing Selection Filter '{filterName}' (Id {existing.Id}) — now {ids.Count} element(s).");
        }
        else
        {
            var sfe = SelectionFilterElement.Create(Document, filterName);
            sfe.SetElementIds(ids);
            t.Commit();
            sb.AppendLine($"Created Selection Filter '{filterName}' (Id {sfe.Id}) — {ids.Count} element(s). Not yet applied to any view — run action-apply-view-filter.cs next.");
        }
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create/update the Selection Filter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
