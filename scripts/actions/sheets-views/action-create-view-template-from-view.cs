// ============================================================
// FRAGMENT (action) — action-create-view-template-from-view.cs
// PURPOSE: Save a fully-configured view's current settings (V/G overrides, Filters, Category overrides,
//          Phase, Detail Level, Scale, Discipline, ...) as a brand new named View Template — Revit's own
//          "Create Template from Current View". The new template is also explicitly applied back to the
//          source view here, rather than assuming that happens automatically.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on a VIEW, not model elements.
// LIVE-VERIFIED 2026-07-22 — FOUND AND FIXED A REAL BUG: the original code read
// `ElementId newTemplateId = sourceView.CreateViewTemplate();` — this does not compile.
// `View.CreateViewTemplate()` returns a `View` directly, not an `ElementId` (confirmed via reflection:
// return type is `Autodesk.Revit.DB.View`). Fixed to use the returned View object directly instead of
// wrapping it in a nonexistent ElementId conversion. Re-verified against a real view: template created,
// named, and applied back to the source view correctly (confirmed via fresh re-fetch of ViewTemplateId).
// It does NOT auto-apply the new template to the source view on its own — the explicit
// `sourceView.ViewTemplateId = newTemplate.Id` line is required, exactly as the file already assumed.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? sourceViewIdInt = null; // null = active view — the view whose CURRENT settings become the template
string newTemplateName = "New View Template";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View sourceView = sourceViewIdInt.HasValue ? Document.GetElement(new ElementId(sourceViewIdInt.Value)) as View : Document.ActiveView;

if (sourceView == null)
{
    sb.AppendLine($"Source view (Id {sourceViewIdInt}) not found or is not a view.");
}
else if (sourceView.IsTemplate)
{
    sb.AppendLine($"'{sourceView.Name}' is already a View Template, not a real view — pick a real view to capture settings from.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create View Template From View"))
    {
        t.Start();
        try
        {
            View newTemplate = sourceView.CreateViewTemplate();
            if (newTemplate != null && !string.IsNullOrEmpty(newTemplateName))
            {
                newTemplate.Name = newTemplateName;
            }
            sourceView.ViewTemplateId = newTemplate.Id; // explicit — don't rely on undocumented auto-apply behavior
            t.Commit();
            sb.AppendLine($"Created View Template '{newTemplate?.Name ?? newTemplateName}' (Id {newTemplate.Id.IntegerValue}) from '{sourceView.Name}', and applied it back to that view.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create the View Template — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
