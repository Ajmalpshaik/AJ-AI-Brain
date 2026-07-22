// ============================================================
// FRAGMENT (action) — action-assign-scope-box-to-view.cs
// PURPOSE: Assign a named Scope Box to every View in `elements` (the view's own "Scope Box" property) —
//          leave scopeBoxName empty to clear the assignment instead.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. filter-by-views.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// NOT YET LIVE-VERIFIED — confirm BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP is the right parameter
// in this Revit version before trusting it on a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string scopeBoxName = "Scope Box 1"; // "" = clear the Scope Box assignment instead
// ---- END INPUTS ----

Element scopeBox = null;
bool resolved = string.IsNullOrEmpty(scopeBoxName);

if (!resolved)
{
    scopeBox = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_VolumeOfInterest).WhereElementIsNotElementType()
        .FirstOrDefault(e => e.Name.Equals(scopeBoxName, StringComparison.OrdinalIgnoreCase));
    if (scopeBox == null)
    {
        var available = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_VolumeOfInterest).WhereElementIsNotElementType();
        sb.AppendLine($"Scope Box '{scopeBoxName}' not found. Available: {string.Join(", ", available.Select(e => e.Name))}");
    }
    else
    {
        resolved = true;
    }
}

if (resolved)
{
    ElementId targetId = scopeBox?.Id ?? ElementId.InvalidElementId;
    int updated = 0, skipped = 0;
    var failures = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Assign Scope Box"))
    {
        t.Start();
        try
        {
            foreach (var el in elements)
            {
                var view = el as View;
                if (view == null) { skipped++; continue; }
                try
                {
                    view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.Set(targetId);
                    updated++;
                }
                catch (Exception ex) { skipped++; failures.Add($"'{view.Name}': {ex.Message}"); }
            }
            t.Commit();
            sb.AppendLine($"{(scopeBox == null ? "Cleared Scope Box on" : $"Assigned '{scopeBoxName}' to")} {updated} view(s), skipped {skipped}.");
            if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to assign scope box — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
