// ============================================================
// FRAGMENT (action) — action-create-view-template-from-view.cs
// PURPOSE: Save a fully-configured view's current settings (V/G overrides, Filters, Category overrides,
//          Phase, Detail Level, Scale, Discipline, ...) as a brand new named View Template — Revit's own
//          "Create Template from Current View". The new template is also explicitly applied back to the
//          source view here, rather than assuming that happens automatically.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on a VIEW, not model elements.
// STATUS: not yet live-verified — confirm View.CreateViewTemplate()'s exact behavior (does it already
//         apply the new template to the source view, or only create it?) against the real installed API.
//         The explicit `sourceView.ViewTemplateId = newTemplateId` line below makes the outcome correct
//         either way, so this doesn't block trusting the RESULT — just the exact mechanism.
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
            ElementId newTemplateId = sourceView.CreateViewTemplate();
            var newTemplate = Document.GetElement(newTemplateId) as View;
            if (newTemplate != null && !string.IsNullOrEmpty(newTemplateName))
            {
                newTemplate.Name = newTemplateName;
            }
            sourceView.ViewTemplateId = newTemplateId; // explicit — don't rely on undocumented auto-apply behavior
            t.Commit();
            sb.AppendLine($"Created View Template '{newTemplate?.Name ?? newTemplateName}' (Id {newTemplateId.IntegerValue}) from '{sourceView.Name}', and applied it back to that view.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create the View Template — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
