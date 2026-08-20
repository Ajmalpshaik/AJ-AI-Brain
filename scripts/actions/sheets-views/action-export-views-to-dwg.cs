// ============================================================
// FRAGMENT (action) — action-export-views-to-dwg.cs
// PURPOSE: Export each View/ViewSheet in `elements` to its own DWG file — Revit's File > Export > CAD
//          Formats > DWG, scripted. Feed from filter-by-views.cs or filter-by-sheets.cs.
// ASSUMES: elements (List<Element>, Views and/or ViewSheets) and sb (StringBuilder) exist from a filter
//          above.
// NOT STANDALONE — see scripts/README.md for how to compose. Writes files on disk (tell the user the
//          folder — an outward effect, get a go-ahead first, same as PDF export).
// GOTCHA: no Transaction — export is document I/O, not a model edit (same reasoning as
//         action-export-schedule-to-csv.cs).
// GOTCHA: exports one view per call in a loop (clean per-view filenames) rather than one call with all
//         Ids — Revit's multi-view call generates its own combined names you can't fully control.
// GOTCHA: filenames strip characters Windows forbids; a sheet exports as "<number> - <name>.dwg".
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp\dwg-export";    // created if missing
string fileNamePrefix = "";                     // optional prefix on every file, e.g. "PRJ01_"
// DWG flavor: default export options; set a named saved setup instead via setupName if the office has one
string setupName = null;                        // null = Revit's in-session default DWG settings
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

        DWGExportOptions options = null;
        if (!string.IsNullOrEmpty(setupName))
        {
            options = DWGExportOptions.GetPredefinedOptions(Document, setupName);
            if (options == null) sb.AppendLine($"NOTE: saved DWG setup '{setupName}' not found — using default options instead.");
        }
        if (options == null) options = new DWGExportOptions();

        Func<string, string> safe = s => string.Join("_", s.Split(System.IO.Path.GetInvalidFileNameChars()));

        int done = 0, failed = 0;
        foreach (var v in views)
        {
            var sheet = v as ViewSheet;
            string name = fileNamePrefix + (sheet != null ? $"{safe(sheet.SheetNumber)} - {safe(sheet.Name)}" : safe(v.Name));
            try
            {
                Document.Export(exportFolder, name, new List<ElementId> { v.Id }, options);
                done++;
            }
            catch (Exception exOne)
            {
                failed++;
                sb.AppendLine($"  FAILED '{v.Name}' (Id {v.Id}): {exOne.Message}");
            }
        }
        sb.AppendLine($"DWG export: {done} of {views.Count} view(s)/sheet(s) written to '{exportFolder}', {failed} failed.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED DWG export before any file was written. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
