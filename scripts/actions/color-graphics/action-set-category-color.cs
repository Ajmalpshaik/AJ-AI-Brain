// ============================================================
// FRAGMENT (action) — action-set-category-color.cs
// PURPOSE: Override an ENTIRE category's line/fill color in a view — Revit's own Visibility/Graphics >
//          Model Categories per-category override, not a per-element one. Different from
//          action-set-color-uniform.cs: that colors only the specific elements in `elements`; this
//          colors every instance of the category in the view — present now or added later — with ONE
//          category-level setting instead of per-element overrides.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — a category override has no "which elements"
//          step to compose with, so this fragment is self-contained (declares its own `sb`, ends with
//          its own `return`) rather than chained after a filter.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
byte colorR = 255, colorG = 0, colorB = 0;
bool includeFill = true;
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
    var category = Category.GetCategory(Document, targetCategory);
    if (category == null)
    {
        sb.AppendLine($"Category {targetCategory} not found in this document.");
    }
    else
    {
        var solidFillPattern = new FilteredElementCollector(Document)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

        using (var t = new Transaction(Document, "AJ Tools - Set Category Color Override"))
        {
            t.Start();
            try
            {
                var color = new Autodesk.Revit.DB.Color(colorR, colorG, colorB);
                var ogs = view.GetCategoryOverrides(category.Id);
                ogs.SetProjectionLineColor(color);
                ogs.SetCutLineColor(color);
                if (includeFill && solidFillPattern != null)
                {
                    ogs.SetSurfaceForegroundPatternColor(color);
                    ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
                    ogs.SetSurfaceForegroundPatternVisible(true);
                    ogs.SetCutForegroundPatternColor(color);
                    ogs.SetCutForegroundPatternId(solidFillPattern.Id);
                    ogs.SetCutForegroundPatternVisible(true);
                }
                view.SetCategoryOverrides(category.Id, ogs);
                t.Commit();
                sb.AppendLine($"Set category override for '{category.Name}' to RGB({colorR},{colorG},{colorB}) in view '{view.Name}' — affects every instance of this category, present or future, not just today's selection.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set category color — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
return sb.ToString();
