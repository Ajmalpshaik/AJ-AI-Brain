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
//
// ✱ UPGRADED 2026-08-22 (harvested from the add-in's Reset Category Graphics in View): `allCategories`
//   clears EVERY overridable category in the view in one pass. Before this you had to name each
//   BuiltInCategory by hand, which is unusable for the case that actually needs it — "I ran grayout /
//   colorize over this view and I want it back to normal", where you do not know and should not have
//   to know which of ~90 categories got touched. `recipes/mep-grayout.cs` alone writes 87.
// GOTCHA: `view.IsCategoryOverridable(categoryId)` IS THE REAL TEST for whether a category will accept
//         an override in this view — checking `CategoryType` alone is not enough, and calling
//         `SetCategoryOverrides` on one that refuses throws. Categories that refuse are skipped and
//         counted, never failed.
// GOTCHA: this clears CATEGORY overrides only. Any PER-ELEMENT override survives and still wins (rows
//         2 vs 7 of the precedence table), so a view can still look wrong after a full category reset.
//         `action-reset-graphic-overrides.cs` is the per-element counterpart — a full clean-up usually
//         needs both.
// GOTCHA: `allCategories` includes ANNOTATION categories, because a grayout-style pass sets those too.
//         Set `includeAnnotationCategories = false` to leave text, tags and dimensions alone.
// ✓ The named-category path is verified live 2026-07-22.
// ⚠ The `allCategories` path is compile-checked on 2020/2024/2027 but NOT yet live-run. It is a bulk,
//   hard-to-eyeball change — run it with `dryRun = true` first and read the count.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool allCategories = false;  // true = every overridable category in the view; false = just targetCategories below
bool includeAnnotationCategories = true;   // allCategories only: also clear text/tags/dimensions
bool dryRun = false;         // true = count what would be cleared, change nothing
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
            var blank = new OverrideGraphicSettings();
            int notOverridable = 0;

            if (allCategories)
            {
                foreach (Category category in Document.Settings.Categories)
                {
                    if (category == null || category.Id == ElementId.InvalidElementId) continue;
                    if (!includeAnnotationCategories && category.CategoryType != CategoryType.Model) continue;

                    // The real test — CategoryType alone is not enough, and a refusing category throws.
                    bool overridable = false;
                    try { overridable = view.IsCategoryOverridable(category.Id); } catch { }
                    if (!overridable) { notOverridable++; continue; }

                    if (!dryRun) view.SetCategoryOverrides(category.Id, blank);
                    okCount++;
                }
            }
            else
            {
                foreach (var targetCategory in targetCategories.Distinct())
                {
                    var category = Category.GetCategory(Document, targetCategory);
                    if (category == null)
                    {
                        skipped.Add($"{targetCategory} (not found in this document)");
                        continue;
                    }
                    if (!dryRun) view.SetCategoryOverrides(category.Id, blank);
                    okCount++;
                }
            }

            if (dryRun) t.RollBack(); else t.Commit();

            sb.AppendLine((dryRun ? "DRY RUN — nothing changed. Would reset" : "Reset") +
                          $" category override on {okCount} categor(y/ies) in view '{view.Name}'" +
                          (allCategories ? $" (every overridable category{(includeAnnotationCategories ? ", annotation included" : ", model only")})." : "."));
            if (notOverridable > 0)
                sb.AppendLine($"{notOverridable} categor(y/ies) cannot be overridden in this view and were skipped.");
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
            if (okCount > 0)
                sb.AppendLine("Note: PER-ELEMENT overrides are untouched and still win — run action-reset-graphic-overrides.cs too if the view still looks wrong.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to reset category override — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
