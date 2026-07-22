// ============================================================
// FRAGMENT (action) — action-set-category-halftone.cs
// PURPOSE: Turn halftone ON or OFF for one or more ENTIRE categories in a view — the category-level
//          sibling of action-set-halftone.cs, same relationship action-set-category-color.cs has to
//          action-set-color-uniform.cs. Read-modify-write: reads the category's EXISTING override first
//          so an existing category color isn't wiped out.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_DuctCurves };
bool halftone = true;
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

    using (var t = new Transaction(Document, "AJ Tools - Set Category Halftone"))
    {
        t.Start();
        try
        {
            foreach (var targetCategory in targetCategories.Distinct())
            {
                var category = Category.GetCategory(Document, targetCategory);
                if (category == null) { skipped.Add($"{targetCategory} (not found in this document)"); continue; }

                var ogs = view.GetCategoryOverrides(category.Id);
                ogs.SetHalftone(halftone);
                view.SetCategoryOverrides(category.Id, ogs);
                okCount++;
            }
            t.Commit();
            sb.AppendLine($"Set halftone = {halftone} on {okCount} categor(y/ies) in view '{view.Name}' (existing category color preserved).");
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set category halftone — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
