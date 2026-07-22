// ============================================================
// FRAGMENT (action) — action-duplicate-view-template.cs
// PURPOSE: Duplicate an existing View Template (by name) into a new, separately-named template with the
//          same settings — a starting point for a variant without hand-rebuilding it. Different from
//          action-duplicate-views.cs: that one consumes `elements` from filter-by-views.cs, which
//          deliberately EXCLUDES templates (IsTemplate elements aren't "a view" in that filter's sense),
//          so templates need their own by-name lookup instead of a filter.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`), same as the other View Template fragments.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceTemplateName = "Mechanical - Coordination";
string newTemplateName = "Mechanical - Coordination (Copy)";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var source = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .FirstOrDefault(v => v.IsTemplate && v.Name.Equals(sourceTemplateName, StringComparison.OrdinalIgnoreCase));

if (source == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).Select(v => v.Name);
    sb.AppendLine($"View Template '{sourceTemplateName}' not found. Available: {string.Join(", ", available)}");
}
else if (!source.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
{
    sb.AppendLine($"View Template '{sourceTemplateName}' cannot be duplicated (CanViewBeDuplicated returned false).");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Duplicate View Template"))
    {
        t.Start();
        try
        {
            var newId = source.Duplicate(ViewDuplicateOption.Duplicate);
            var newTemplate = Document.GetElement(newId) as View;
            if (newTemplate != null) newTemplate.Name = newTemplateName;
            t.Commit();
            sb.AppendLine($"Duplicated View Template '{sourceTemplateName}' as '{newTemplateName}' (Id {newId.IntegerValue}).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to duplicate the View Template — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
