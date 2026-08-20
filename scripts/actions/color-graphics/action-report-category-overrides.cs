// ============================================================
// FRAGMENT (action) — action-report-category-overrides.cs
// PURPOSE: Read back which categories currently have a CATEGORY-LEVEL graphic override set in a view —
//          the reverse lookup for action-set-category-color.cs/action-set-category-halftone.cs/
//          action-set-category-line-style.cs, which only ever SET. Read-only. Scoped to categories that
//          actually have elements present in the view (not every category in the document) unless
//          categoryScope is set explicitly.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// SOURCE: override-detection technique (IsSurfaceForegroundPatternVisible defaults to TRUE even with
//         nothing overridden, so the real signal is a valid color/real pattern Id, not the visibility
//         flag) reused from action-report-graphic-overrides.cs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] categoryScope = { }; // empty = every category with at least one element present in the view
int? targetViewIdInt = null; // null = active view
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

Func<Autodesk.Revit.DB.Color, string> colorText = c => (c != null && c.IsValid) ? $"RGB({c.Red},{c.Green},{c.Blue})" : "(none)";

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
        categoriesToCheck = new FilteredElementCollector(Document, view.Id)
            .WhereElementIsNotElementType()
            .Select(e => e.Category)
            .Where(c => c != null)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.Name)
            .ToList();
    }

    int withOverride = 0;
    foreach (var category in categoriesToCheck)
    {
        OverrideGraphicSettings ogs;
        try { ogs = view.GetCategoryOverrides(category.Id); } catch { continue; }

        bool hasAnyOverride = ogs.ProjectionLineColor.IsValid || ogs.CutLineColor.IsValid
            || ogs.SurfaceForegroundPatternColor.IsValid || ogs.CutForegroundPatternColor.IsValid
            || ogs.SurfaceForegroundPatternId != ElementId.InvalidElementId
            || ogs.CutForegroundPatternId != ElementId.InvalidElementId
            || ogs.Transparency != 0 || ogs.Halftone
            || ogs.ProjectionLineWeight != -1 || ogs.CutLineWeight != -1
            || ogs.ProjectionLinePatternId != ElementId.InvalidElementId || ogs.CutLinePatternId != ElementId.InvalidElementId;

        if (!hasAnyOverride) continue;

        sb.AppendLine($"'{category.Name}': projection line {colorText(ogs.ProjectionLineColor)}, cut line {colorText(ogs.CutLineColor)}, " +
            $"surface pattern color {colorText(ogs.SurfaceForegroundPatternColor)}, transparency {ogs.Transparency}%, halftone {ogs.Halftone}, " +
            $"projection weight {(ogs.ProjectionLineWeight == -1 ? "(default)" : ogs.ProjectionLineWeight.ToString())}.");
        withOverride++;
    }

    if (withOverride == 0) sb.AppendLine($"No category-level overrides found in view '{view.Name}' among {categoriesToCheck.Count} categor(y/ies) checked.");
    else sb.AppendLine($"{withOverride} of {categoriesToCheck.Count} categor(y/ies) checked have a category-level override in view '{view.Name}'.");
}
return sb.ToString();
