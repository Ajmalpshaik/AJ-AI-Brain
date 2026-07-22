// ============================================================
// FRAGMENT (action) — action-set-view-workset-visibility.cs
// PURPOSE: Make one view show ONLY the named workset(s) — the "Workset 3D View" pattern (this project's
//          own AJ Tools "Workset 3D Views" convention, scripted): every OTHER user workset gets turned off
//          in that view, the named one(s) turned on. Unlike Phase (a single settable View property, see
//          action-set-view-properties.cs) or Category (action-set-category-visibility.cs), Revit has no
//          single "view scope" property for worksets — visibility is set per-workset, per-view, which is
//          exactly what this loops over.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on ONE view via View.SetWorksetVisibility,
//          not a model element set. Pair with creators/create-view.cs (mode="three_d") right after creating
//          a fresh view meant to be workset-scoped from the start.
// GOTCHA: only meaningful on a workshared (central/local) model — reports plainly and changes nothing on a
//         non-workshared model instead of throwing.
// GOTCHA: this is a per-VIEW setting, not permanent hide/delete — the same elements are still fully in the
//         model and visible in every other view; this only affects the one view targeted.
// NOT YET LIVE-VERIFIED — test on one view first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0; // 0 = Document.ActiveView
List<string> visibleWorksetNames = new List<string> { }; // these worksets stay/become visible in the view
bool hideAllOthers = true; // true = every OTHER user workset set Hidden; false = others left at UseGlobalSetting
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (!Document.IsWorkshared)
{
    sb.AppendLine("This document is not workshared — no user worksets exist, nothing to set.");
}
else
{
    var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;
    if (view == null)
    {
        sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
    }
    else
    {
        var visibleSet = new HashSet<string>(visibleWorksetNames, StringComparer.OrdinalIgnoreCase);
        var allWorksets = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).ToWorksets();
        int shown = 0, hidden = 0, unresolved = 0;
        var failures = new List<string>();

        using (var t = new Transaction(Document, "AJ Tools - Set View Workset Visibility"))
        {
            t.Start();
            try
            {
                foreach (var ws in allWorksets)
                {
                    try
                    {
                        if (visibleSet.Contains(ws.Name))
                        {
                            view.SetWorksetVisibility(ws.Id, WorksetVisibility.Visible);
                            shown++;
                        }
                        else if (hideAllOthers)
                        {
                            view.SetWorksetVisibility(ws.Id, WorksetVisibility.Hidden);
                            hidden++;
                        }
                    }
                    catch (Exception exOne)
                    {
                        failures.Add($"'{ws.Name}': {exOne.Message}");
                    }
                }
                unresolved = visibleWorksetNames.Count(n => !allWorksets.Any(ws => ws.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
                t.Commit();
                sb.AppendLine($"View '{view.Name}': {shown} workset(s) set Visible, {hidden} set Hidden" +
                    (unresolved > 0 ? $", {unresolved} name(s) in visibleWorksetNames not found." : "."));
                if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures));
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set workset visibility — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
return sb.ToString();
