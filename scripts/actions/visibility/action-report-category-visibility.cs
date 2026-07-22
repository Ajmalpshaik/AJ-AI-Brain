// ============================================================
// FRAGMENT (action) — action-report-category-visibility.cs
// PURPOSE: Report which categories are currently OFF (category-level, via View.GetCategoryHidden) in a
//          view — the reverse lookup for action-set-category-visibility.cs, which only ever SETS.
//          Read-only. Scoped by default to categories that have at least one element ANYWHERE in the
//          model (not just in this view) — unlike action-report-category-overrides.cs, scoping to "what's
//          visible in the view" would be self-defeating here, since a hidden category's elements don't
//          show up in a view-scoped collector at all.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] categoryScope = { }; // empty = every category with at least one element anywhere in the model
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
    List<Category> categoriesToCheck;
    if (categoryScope.Length > 0)
    {
        categoriesToCheck = categoryScope.Select(c => Category.GetCategory(Document, c)).Where(c => c != null).ToList();
    }
    else
    {
        categoriesToCheck = new FilteredElementCollector(Document)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c != null)
            .GroupBy(c => c.Id.IntegerValue)
            .Select(g => g.First())
            .OrderBy(c => c.Name)
            .ToList();
    }

    var hiddenCategories = new List<string>();
    var uncontrollable = new List<string>();

    foreach (var category in categoriesToCheck)
    {
        if (!category.get_AllowsVisibilityControl(view)) { uncontrollable.Add(category.Name); continue; }

        bool isHidden;
        try { isHidden = view.GetCategoryHidden(category.Id); } catch { continue; }

        if (isHidden) hiddenCategories.Add(category.Name);
    }

    sb.AppendLine($"Checked {categoriesToCheck.Count} categor(y/ies) in view '{view.Name}':");
    sb.AppendLine(hiddenCategories.Count > 0
        ? $"OFF: {string.Join(", ", hiddenCategories)}"
        : "OFF: none — every checked category is visible.");
    if (uncontrollable.Count > 0)
        sb.AppendLine($"Not controllable in this view (skipped): {string.Join(", ", uncontrollable)}");
}
return sb.ToString();
