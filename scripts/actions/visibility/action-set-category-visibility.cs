// ============================================================
// FRAGMENT (action) — action-set-category-visibility.cs
// PURPOSE: Turn one or more ENTIRE categories on/off in a view — the Visibility checkbox column in
//          Visibility/Graphics > Model Categories, NOT a per-element hide. Different from
//          action-hide-elements.cs: that one hides only the specific elements in `elements`, temporary
//          or permanent; this one hides the CATEGORY ITSELF, persistently, affecting every instance —
//          present now or added later — with no "temporary" concept (same persistence class as
//          action-set-category-color.cs). Checks Category.get_AllowsVisibilityControl first — some
//          categories can't be toggled in some view types (e.g. certain view-specific/annotation
//          categories), and calling SetCategoryHidden on one of those throws.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_Furniture };
bool hidden = true; // true = turn the category OFF; false = turn it back ON
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

    using (var t = new Transaction(Document, "AJ Tools - Set Category Visibility"))
    {
        t.Start();
        try
        {
            foreach (var targetCategory in targetCategories.Distinct())
            {
                var category = Category.GetCategory(Document, targetCategory);
                if (category == null) { skipped.Add($"{targetCategory} (not found in this document)"); continue; }
                if (!category.get_AllowsVisibilityControl(view)) { skipped.Add($"{category.Name} (visibility not controllable in this view)"); continue; }

                view.SetCategoryHidden(category.Id, hidden);
                okCount++;
            }
            t.Commit();
            sb.AppendLine($"Set {(hidden ? "OFF" : "ON")} for {okCount} categor(y/ies) in view '{view.Name}'.");
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set category visibility — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
