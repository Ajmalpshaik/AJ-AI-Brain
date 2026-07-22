// ============================================================
// FRAGMENT (action) — action-export-sheets-to-pdf.cs
// PURPOSE: Batch-export every ViewSheet in `elements` to PDF via Document.Export — combined into one PDF
//          (default) or one PDF per sheet.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above (e.g.
//          filter-by-sheets.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: exportFolder must already exist on disk — this does NOT create it.
// GOTCHA: PDFExportOptions' exact property names have drifted slightly across Revit versions (this was
//         written against the "Combine" bool + "FileName" pattern). If this throws a compile/member error,
//         check PDFExportOptions in this Revit version's API reference and adjust those two property
//         names — the Document.Export(...) call shape itself is stable across versions.
// NOT YET LIVE-VERIFIED — confirm this Revit version's PDFExportOptions members before trusting it, and
// test on ONE sheet first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp"; // must already exist
string combinedFileName = "AJ Tools Export"; // used as the file name when combineIntoOneFile = true
bool combineIntoOneFile = true;
// ---- END INPUTS ----

var sheetIds = elements.OfType<ViewSheet>().Select(v => v.Id).ToList();

if (sheetIds.Count == 0)
{
    sb.AppendLine("No ViewSheet elements found in `elements` — nothing to export.");
}
else if (!System.IO.Directory.Exists(exportFolder))
{
    sb.AppendLine($"Export folder does not exist: {exportFolder} — create it first, this fragment won't.");
}
else
{
    var options = new PDFExportOptions
    {
        Combine = combineIntoOneFile,
        FileName = combinedFileName
    };

    try
    {
        Document.Export(exportFolder, combinedFileName, sheetIds, options);
        sb.AppendLine($"Exported {sheetIds.Count} sheet(s) to PDF in '{exportFolder}' ({(combineIntoOneFile ? $"combined into '{combinedFileName}.pdf'" : "one file per sheet")}).");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to export PDF. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
