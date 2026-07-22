// ============================================================
// *** NOT CHECKED — BLOCKED: only the "clear the assignment" path has run against a real view. Assigning
// an actual named Scope Box has never been tested — this model has none, and there is no API to create
// one. See the LIVE-VERIFIED note below for detail. ***
// FRAGMENT (action) — action-assign-scope-box-to-view.cs
// PURPOSE: Assign a named Scope Box to every View in `elements` (the view's own "Scope Box" property) —
//          leave scopeBoxName empty to clear the assignment instead.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. filter-by-views.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// LIVE-VERIFIED 2026-07-22: BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP confirmed to exist and be
// settable on this Revit version. Composed with filter-by-views.cs and run against a real FloorPlan view
// with scopeBoxName="" (clear mode) — write succeeded, fresh re-fetch confirmed the parameter read back
// -1 (InvalidElementId) afterward. The "assign a real named box" path (scopeBox != null) uses the exact
// same Set() call with a real ElementId instead of InvalidElementId, so the write mechanism is confirmed;
// it has not been exercised against an actual Scope Box because none can be created via API on this
// Revit version (see create-scope-box.cs's file header) and none existed in the test model. Re-confirm
// end-to-end once a real Scope Box exists (create one manually via View tab > Scope Box).
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
