// ============================================================
// SCRIPT: command-zoom-to-fit.cs
// PURPOSE: Zoom the active view's open UI window to fit its current content — useful right after an
//          isolate/section-box/crop action so the next screenshot actually shows the result instead of
//          whatever the view happened to be scrolled/zoomed to before.
// ============================================================

var sb = new System.Text.StringBuilder();
var activeView = Document.ActiveView;
var uiView = UIDocument.GetOpenUIViews().FirstOrDefault(v => v.ViewId == activeView.Id);

if (uiView == null)
{
    sb.AppendLine($"No open UI window found for active view '{activeView.Name}' — it may not be the frontmost tab.");
}
else
{
    try
    {
        uiView.ZoomToFit();
        sb.AppendLine($"Zoomed to fit in '{activeView.Name}'.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to zoom to fit. Reason: {ex.Message}");
    }
}
return sb.ToString();
