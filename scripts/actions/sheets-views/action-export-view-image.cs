// ============================================================
// FRAGMENT (action) — action-export-view-image.cs
// PURPOSE: Export each View/ViewSheet in `elements` as a PNG image — the "screenshot this view properly"
//          job, at a chosen pixel width instead of whatever the screen shows. Feed from filter-by-views.cs
//          or filter-by-sheets.cs.
// ASSUMES: elements (List<Element>, Views and/or ViewSheets) and sb (StringBuilder) exist from a filter
//          above.
// NOT STANDALONE — see scripts/README.md for how to compose. Writes files on disk (go-ahead first).
// GOTCHA: no Transaction — document I/O, same as the other exports.
// GOTCHA: Revit names the actual files itself from the prefix + view name ("<prefix> - <ViewType> -
//         <ViewName>.png") — report the folder, not exact filenames.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp\image-export";  // created if missing
string fileNamePrefix = "view";                 // Revit appends " - <ViewType> - <ViewName>" per view
int pixelWidth = 1920;                          // horizontal resolution of each image
// ---- END INPUTS ----

var views = elements.OfType<View>().Where(v => !v.IsTemplate && v.CanBePrinted).ToList();

if (views.Count == 0)
{
    sb.AppendLine("Nothing exportable — `elements` holds no printable Views/ViewSheets.");
}
else
{
    try
    {
        System.IO.Directory.CreateDirectory(exportFolder);

        var options = new ImageExportOptions
        {
            FilePath = System.IO.Path.Combine(exportFolder, fileNamePrefix),
            ExportRange = ExportRange.SetOfViews,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = pixelWidth,
            FitDirection = FitDirectionType.Horizontal
        };
        options.SetViewsAndSheets(views.Select(v => v.Id).ToList());

        Document.ExportImage(options);
        sb.AppendLine($"Image export: {views.Count} view(s)/sheet(s) written as PNG ({pixelWidth}px wide) to '{exportFolder}' with prefix '{fileNamePrefix}':");
        foreach (var v in views.Take(20))
            sb.AppendLine($"  - '{v.Name}' (Id {v.Id.IntegerValue})");
        if (views.Count > 20) sb.AppendLine($"  ... +{views.Count - 20} more");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED image export. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
