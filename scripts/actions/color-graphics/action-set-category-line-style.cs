// ============================================================
// FRAGMENT (action) — action-set-category-line-style.cs
// PURPOSE: Override line WEIGHT and/or line PATTERN for one or more ENTIRE categories in a view — the
//          category-level sibling of action-set-line-style.cs, same relationship
//          action-set-category-color.cs has to action-set-color-uniform.cs. Read-modify-write: reads
//          each category's EXISTING override first, so an existing category color isn't wiped out.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_DuctCurves };
int lineWeight = -1; // -1 = don't change; else 1-16
string linePatternName = ""; // "" = don't change; "solid" = reset to solid; else an exact Line Pattern name
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
    ElementId patternId = null;
    bool patternRequestedButMissing = false;
    if (!string.IsNullOrEmpty(linePatternName))
    {
        if (linePatternName.Equals("solid", StringComparison.OrdinalIgnoreCase))
        {
            patternId = LinePatternElement.GetSolidPatternId();
        }
        else
        {
            var patternElem = new FilteredElementCollector(Document).OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .FirstOrDefault(p => p.Name.Equals(linePatternName, StringComparison.OrdinalIgnoreCase));
            if (patternElem == null)
            {
                var available = new FilteredElementCollector(Document).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>().Select(p => p.Name);
                sb.AppendLine($"Line Pattern '{linePatternName}' not found. Available: {string.Join(", ", available)}");
                patternRequestedButMissing = true;
            }
            else
            {
                patternId = patternElem.Id;
            }
        }
    }

    if (!patternRequestedButMissing)
    {
        int okCount = 0;
        var skipped = new List<string>();

        using (var t = new Transaction(Document, "AJ Tools - Set Category Line Style"))
        {
            t.Start();
            try
            {
                foreach (var targetCategory in targetCategories.Distinct())
                {
                    var category = Category.GetCategory(Document, targetCategory);
                    if (category == null) { skipped.Add($"{targetCategory} (not found in this document)"); continue; }

                    var ogs = view.GetCategoryOverrides(category.Id);
                    if (lineWeight >= 1 && lineWeight <= 16)
                    {
                        ogs.SetProjectionLineWeight(lineWeight);
                        ogs.SetCutLineWeight(lineWeight);
                    }
                    if (patternId != null)
                    {
                        ogs.SetProjectionLinePatternId(patternId);
                        ogs.SetCutLinePatternId(patternId);
                    }
                    view.SetCategoryOverrides(category.Id, ogs);
                    okCount++;
                }
                t.Commit();
                sb.AppendLine($"Set line style on {okCount} categor(y/ies) in view '{view.Name}'" +
                    (lineWeight >= 1 && lineWeight <= 16 ? $", weight {lineWeight}" : "") +
                    (patternId != null ? $", pattern '{linePatternName}'" : "") + ".");
                if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set category line style — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
return sb.ToString();
