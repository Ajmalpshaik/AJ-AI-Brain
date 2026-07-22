// ============================================================
// FRAGMENT (action) — action-reset-category-graphics.cs
// PURPOSE: Clear a category-wide graphic override in a view for one or more categories at once — the
//          paired "undo" for action-set-category-color.cs. Different from
//          action-reset-graphic-overrides.cs, which clears PER-ELEMENT overrides on a filtered
//          `elements` set; this clears the CATEGORY-level override set via Visibility/Graphics > Model
//          Categories.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — a category override has no "which elements"
//          step to compose with, so this fragment is self-contained (declares its own `sb`, ends with
//          its own `return`) rather than chained after a filter.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_DuctCurves };
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    int okCount = 0;
    var skipped = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Reset Category Graphic Overrides"))
    {
        t.Start();
        try
        {
            foreach (var targetCategory in targetCategories.Distinct())
            {
                var category = Category.GetCategory(Document, targetCategory);
                if (category == null)
                {
                    skipped.Add($"{targetCategory} (not found in this document)");
                    continue;
                }
                view.SetCategoryOverrides(category.Id, new OverrideGraphicSettings());
                okCount++;
            }
            t.Commit();
            sb.AppendLine($"Reset category override on {okCount} categor(y/ies) in view '{view.Name}'.");
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to reset category override — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
