// ============================================================
// SCRIPT: command-activate-view.cs
// PURPOSE: Switch Revit's active view to a given View/ViewSheet — useful before a screenshot, before a
//          view-scoped filter (filter-by-view.cs, action-tag-elements.cs) that's meant to run "in view X",
//          or simply because the user asked to be shown a different view.
// GOTCHA: some view types can't become the active view in every host state (e.g. a schedule embedded on a
//         sheet vs. its own tab) — reported as a failure, not silently ignored.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0; // set explicitly
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var view = Document.GetElement(new ElementId(viewIdInt)) as View;

if (view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else
{
    try
    {
        UIDocument.ActiveView = view;
        sb.AppendLine($"Active view set to '{view.Name}' (Id {view.Id.IntegerValue}).");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to activate view '{view.Name}'. Reason: {ex.Message}");
    }
}
return sb.ToString();
