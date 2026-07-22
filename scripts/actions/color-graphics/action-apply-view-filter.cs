// ============================================================
// FRAGMENT (action) — action-apply-view-filter.cs
// PURPOSE: Add an existing View Filter (by name) to a view, set its color/fill override, and set its
//          visibility — the "put it on a view and make it do something" step after
//          action-create-view-filter.cs. Safe to re-run: if the filter's already on the view, this just
//          updates its override/visibility instead of erroring.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// SOURCE: ../../../knowledge/live-model/mep-color-standard.md § Per-view filter graphic overrides need
//         BOTH projection AND cut set explicitly — Revit does not default one from the other (found live,
//         26 filters missing CutLineColor/CutForegroundPatternColor on a real floor plan view).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "MEP_System Type/CDP";
byte colorR = 255, colorG = 0, colorB = 0;
bool includeFill = true;
bool visible = true; // false = filter is applied but hidden — elements it matches are hidden in this view
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
    var filter = new FilteredElementCollector(Document).OfClass(typeof(ParameterFilterElement))
        .Cast<ParameterFilterElement>()
        .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

    if (filter == null)
    {
        sb.AppendLine($"View Filter '{filterName}' not found — create it first with action-create-view-filter.cs.");
    }
    else
    {
        var solidFillPattern = new FilteredElementCollector(Document)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

        using (var t = new Transaction(Document, "AJ Tools - Apply View Filter"))
        {
            t.Start();
            try
            {
                if (!view.IsFilterApplied(filter.Id)) view.AddFilter(filter.Id);

                var color = new Autodesk.Revit.DB.Color(colorR, colorG, colorB);
                var ogs = view.GetFilterOverrides(filter.Id);
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
                view.SetFilterOverrides(filter.Id, ogs);
                view.SetFilterVisibility(filter.Id, visible);

                t.Commit();
                sb.AppendLine($"Applied View Filter '{filterName}' to view '{view.Name}' — RGB({colorR},{colorG},{colorB}), visible: {visible}.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to apply the View Filter — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
return sb.ToString();
