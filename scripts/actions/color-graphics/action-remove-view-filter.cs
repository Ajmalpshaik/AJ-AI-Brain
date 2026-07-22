// ============================================================
// FRAGMENT (action) — action-remove-view-filter.cs
// PURPOSE: Take a View Filter OFF a view (view.RemoveFilter) — the filter definition itself still exists
//          in the document and can be re-applied to this or another view later. Set
//          deleteFilterElement = true to also permanently delete the filter definition from the whole
//          document (removes it from EVERY view that uses it, not just this one) — treat that like any
//          other delete: confirm with the user first, and it needs allowDestructive: true on the bridge
//          call too.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "MEP_System Type/CDP";
bool deleteFilterElement = false; // true = permanently delete from the whole document, not just this view — destructive, confirm first
int? targetViewIdInt = null; // null = active view
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    var filter = new FilteredElementCollector(Document).OfClass(typeof(ParameterFilterElement))
        .Cast<ParameterFilterElement>()
        .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

    if (filter == null)
    {
        sb.AppendLine($"View Filter '{filterName}' not found — nothing to remove.");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Remove View Filter"))
        {
            t.Start();
            try
            {
                if (view.IsFilterApplied(filter.Id)) view.RemoveFilter(filter.Id);

                if (deleteFilterElement)
                {
                    Document.Delete(filter.Id);
                    sb.AppendLine($"Removed View Filter '{filterName}' from view '{view.Name}' AND deleted it from the document entirely.");
                }
                else
                {
                    sb.AppendLine($"Removed View Filter '{filterName}' from view '{view.Name}' — the filter definition still exists in the document, unaffected in any other view using it.");
                }
                t.Commit();
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to remove the View Filter — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
return sb.ToString();
