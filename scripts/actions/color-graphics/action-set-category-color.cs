// ============================================================
// FRAGMENT (action) — action-set-category-color.cs
// PURPOSE: Override one or more ENTIRE categories' line/fill color in a view — Revit's own Visibility/
//          Graphics > Model Categories per-category override, not a per-element one. Different from
//          action-set-color-uniform.cs: that colors only the specific elements in `elements`; this
//          colors every instance of each target category in the view — present now or added later —
//          with ONE category-level setting instead of per-element overrides. Accepts several categories
//          at once (e.g. the whole "Duct System" group: Ducts + Fittings + Accessories + Flex Ducts +
//          Insulation + Lining) so a system-wide category color doesn't need one run per category.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — a category override has no "which elements"
//          step to compose with, so this fragment is self-contained (declares its own `sb`, ends with
//          its own `return`) rather than chained after a filter.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_DuctCurves };
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
    var solidFillPattern = new FilteredElementCollector(Document)
        .OfClass(typeof(FillPatternElement))
        .Cast<FillPatternElement>()
        .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

    var color = new Autodesk.Revit.DB.Color(colorR, colorG, colorB);
    int okCount = 0;
    var skipped = new List<string>();
    var partial = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Set Category Color Override"))
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
                okCount++;

                // REVIT SILENTLY DROPS PART OF A CATEGORY OVERRIDE, depending on the category.
                // Measured 2026-08-07 on this Revit version: with Category.IsCuttable == false
                // (Ducts, Pipes, Air Terminals, Mechanical Equipment) the CUT line colour never
                // sticks, and on Ducts/Pipes/Air Terminals the surface fill is dropped too — only
                // the projection line colour survives. Walls and Doors (IsCuttable == true) keep
                // all of it. The setters do not complain and the in-memory object holds every
                // value; only a read-back shows the difference. Reporting "coloured, fill included"
                // when the fill was discarded is the silent wrong answer this library exists to
                // avoid, so say what actually stuck.
                // See ../../../knowledge/live-model/graphic-override-precedence.md.
                var applied = view.GetCategoryOverrides(category.Id);
                var dropped = new List<string>();
                if (!applied.ProjectionLineColor.IsValid) dropped.Add("projection line colour");
                if (!applied.CutLineColor.IsValid) dropped.Add("cut line colour");
                if (includeFill && solidFillPattern != null)
                {
                    if (!applied.SurfaceForegroundPatternColor.IsValid) dropped.Add("surface fill");
                    if (!applied.CutForegroundPatternColor.IsValid) dropped.Add("cut fill");
                }
                if (dropped.Count > 0)
                    partial.Add($"{category.Name} (IsCuttable={category.IsCuttable}) — Revit kept the rest but DISCARDED: {string.Join(", ", dropped)}");
            }
            t.Commit();
            sb.AppendLine($"Set category override on {okCount} categor(y/ies) to RGB({colorR},{colorG},{colorB}) in view '{view.Name}' — affects every instance of each category, present or future, not just today's selection.");
            if (partial.Count > 0)
            {
                sb.AppendLine("PARTIALLY APPLIED — read back from the view, not assumed:");
                foreach (var p in partial) sb.AppendLine("  - " + p);
                sb.AppendLine("  A non-cuttable category cannot take a cut override at all; for a solid-looking result on those, override the ELEMENTS (action-set-color-uniform.cs) instead of the category.");
            }
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set category color — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
