// ============================================================
// FRAGMENT (action) — action-apply-view-template.cs
// PURPOSE: Apply an existing View Template (by name) to one or more views — bundles ALL of a view's
//          graphic/visibility settings (V/G overrides, View Filters, Category overrides, Phase, Detail
//          Level, Scale, Discipline) into one named preset instead of setting each view up by hand.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on VIEWS, not model elements. Pair
//          with filters/by-view-and-sheet/filter-by-views.cs if you want to pick which views to apply this to by
//          type/name rather than listing Element Ids by hand (cast the resulting `elements` back to
//          `View` and pass their Ids in here).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string templateName = "Mechanical - Coordination";
int[] targetViewIdInts = { }; // empty = active view only; else every listed view Id gets the template
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var template = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .FirstOrDefault(v => v.IsTemplate && v.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

if (template == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).Select(v => v.Name);
    sb.AppendLine($"View Template '{templateName}' not found. Available: {string.Join(", ", available)}");
}
else
{
    var targetViews = targetViewIdInts.Length > 0
        ? targetViewIdInts.Select(id => Document.GetElement(new ElementId(id)) as View).Where(v => v != null).ToList()
        : new List<View> { Document.ActiveView };

    int okCount = 0;
    var skipped = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Apply View Template"))
    {
        t.Start();
        try
        {
            foreach (var v in targetViews)
            {
                if (v.IsTemplate) { skipped.Add($"{v.Name} (is itself a template)"); continue; }
                try
                {
                    v.ViewTemplateId = template.Id;
                    okCount++;
                }
                catch (Exception exView)
                {
                    skipped.Add($"{v.Name} ({exView.Message})");
                }
            }
            t.Commit();
            sb.AppendLine($"Applied View Template '{template.Name}' to {okCount} view(s).");
            if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to apply the View Template — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
