// ============================================================
// *** NOT CHECKED — the real SubmitPrint() call has never actually fired. Code compiles and the API calls
// are reflection-confirmed to exist, but no PDF has actually been produced by this fragment yet. Held back
// deliberately — this system's printer list includes a real physical office copier, see the SAFETY GUARD
// note below. Needs an explicit go-ahead before the first real run. ***
// FRAGMENT (action) — action-export-sheets-to-pdf.cs
// PURPOSE: Batch-export every ViewSheet in `elements` to PDF — combined into one PDF (default) or one PDF
//          per sheet.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above (e.g.
//          filter-by-sheets.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// REWRITTEN 2026-07-22 — the original technique (`PDFExportOptions` + `Document.Export(folder, name,
// sheetIds, options)`) does not exist on this Revit version at all: confirmed by reflection that
// `PDFExportOptions` is not a type anywhere in the RevitAPI assembly, and none of `Document`'s `Export`
// overloads take a bare `ICollection<ElementId>` + options for PDF (only CAD/BIM formats — DWG/DXF/DGN/
// SAT/IFC/gbXML/Navisworks/FBX/DWF/DWFX — have an Export overload; there is no PDF one). That API was
// added in a later Revit release. On Revit 2020, the only path to a PDF is via `Document.PrintManager`
// routed through an installed PDF-capable virtual printer driver (this is Autodesk's own documented
// workaround for this Revit era) — confirmed by reflection: PrintManager.SelectNewPrintDriver(string),
// .PrintRange, .ViewSheetSetting (an IViewSheetSet, only accessible once PrintRange=Select — throws
// otherwise), IViewSheetSet.Views (a settable ViewSet you Insert() sheets into), .CombinedFile,
// .PrintToFile, .PrintToFileName, .Apply(), .SubmitPrint() (returns bool) all exist and were confirmed via
// reflection this session.
// SAFETY GUARD (read before running "for real"): this system's installed printers include a REAL PHYSICAL
// OFFICE COPIER ("6008ci" / "KONICA MINOLTA bizhub C658") alongside PDF-capable virtual drivers
// ("Microsoft Print to PDF", "PDF24"). Submitting a print job to the wrong device wastes physical paper on
// a shared device — hard to reverse, visible to others. This fragment calls SelectNewPrintDriver(the
// configured pdfPrinterDriverName) and then RE-READS PrinterName to confirm it actually took effect before
// calling SubmitPrint() — if it doesn't match, it aborts with no print submitted. Do not remove that guard.
// NOT LIVE-EXECUTED THIS SESSION (deliberately): the API surface above is fully reflection-verified and
// compiles, but actually calling SubmitPrint() was held back rather than run blind — a stuck/hung print
// dialog from an unfamiliar driver, or any gap in the guard's coverage, is a real-world side effect this
// session chose not to risk without your explicit go-ahead (same caution as the Tier 2 destructive
// fragments, even though this one isn't in that group). Confirm you want a real test print-to-PDF fired
// (target: C:\Temp, driver: "Microsoft Print to PDF") before running this for real.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp"; // must already exist
string combinedFileName = "AJ Tools Export"; // used as the file name when combineIntoOneFile = true
bool combineIntoOneFile = true;
string pdfPrinterDriverName = "Microsoft Print to PDF"; // must exactly match an entry in System.Drawing.Printing.PrinterSettings.InstalledPrinters — NEVER point this at a physical device
// ---- END INPUTS ----

var sheets = elements.OfType<ViewSheet>().ToList();

if (sheets.Count == 0)
{
    sb.AppendLine("No ViewSheet elements found in `elements` — nothing to export.");
}
else if (!System.IO.Directory.Exists(exportFolder))
{
    sb.AppendLine($"Export folder does not exist: {exportFolder} — create it first, this fragment won't.");
}
else
{
    try
    {
        var pm = Document.PrintManager;
        pm.SelectNewPrintDriver(pdfPrinterDriverName);

        if (!pm.PrinterName.Equals(pdfPrinterDriverName, StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"SAFETY ABORT: requested printer driver '{pdfPrinterDriverName}' but PrintManager.PrinterName reads back '{pm.PrinterName}' after SelectNewPrintDriver — refusing to submit a print job to an unexpected device (this system has a real physical copier installed too). Nothing was printed. Check the driver name against System.Drawing.Printing.PrinterSettings.InstalledPrinters.");
        }
        else
        {
            pm.PrintRange = PrintRange.Select;
            var vss = pm.ViewSheetSetting;
            var viewSet = new ViewSet();
            foreach (var s in sheets) viewSet.Insert(s);
            vss.CurrentViewSheetSet.Views = viewSet;

            pm.CombinedFile = combineIntoOneFile;
            pm.PrintToFile = true;
            pm.PrintToFileName = System.IO.Path.Combine(exportFolder, combinedFileName + ".pdf");
            pm.Apply();
            bool ok = pm.SubmitPrint();
            sb.AppendLine($"SubmitPrint() returned {ok}. Requested export of {sheets.Count} sheet(s) via '{pdfPrinterDriverName}' to '{pm.PrintToFileName}'" +
                (combineIntoOneFile ? " (combined into one file)." : " (Revit names one file per sheet when CombinedFile=false — PrintToFileName becomes a base name)."));
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to export PDF. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
