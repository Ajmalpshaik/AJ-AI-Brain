// ============================================================
// FRAGMENT (action) — action-set-print-settings.cs
// PURPOSE: Configure print settings — paper size (by name), orientation, zoom-to-fit — and save them as a
//          named Print Setting. Completes the print chain: sheet set (action-manage-sheet-sets.cs) +
//          print setting (this) + print run (action-export-sheets-to-pdf.cs).
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — print settings are document-level.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: available paper sizes come from the CURRENT print driver — a size that exists on the PDF
//         printer may not exist on a physical one; report-mode first lists what this driver actually has.
// FLAGGED 2026-07-26 (static review): PrintParameters on 2020 is quirky — some drivers throw on
//         PaperSize assignment, and SaveAs rejects names already in use. First failure = check those two
//         before suspecting the logic.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "report";         // "report" (list drivers' paper sizes + existing print settings) | "save"
string paperSizeName = "A1";    // save only — must match a name from report mode
string orientation = "landscape"; // "portrait" | "landscape"
bool zoomToFit = true;          // true = Fit To Page; false = 100% zoom
string saveAsName = "MEP_A1_Landscape"; // save only — name of the new Print Setting
// ---- END INPUTS ----

var pm = Document.PrintManager;

if (mode == "report")
{
    sb.AppendLine($"Current printer: '{pm.PrinterName}'");
    var sizes = new List<string>();
    foreach (PaperSize psz in pm.PaperSizes) sizes.Add(psz.Name);
    sb.AppendLine($"Paper sizes this driver offers ({sizes.Count}): {string.Join(", ", sizes)}");
    var settings = new FilteredElementCollector(Document).OfClass(typeof(PrintSetting)).ToList();
    sb.AppendLine($"Saved print settings ({settings.Count}): {string.Join(", ", settings.Select(s => $"'{s.Name}' (Id {s.Id.IntegerValue})"))}");
}
else if (mode == "save")
{
    PaperSize paper = null;
    foreach (PaperSize psz in pm.PaperSizes) { if (psz.Name == paperSizeName) { paper = psz; break; } }
    if (paper == null)
    {
        var avail = new List<string>();
        foreach (PaperSize psz in pm.PaperSizes) avail.Add(psz.Name);
        sb.AppendLine($"Paper size '{paperSizeName}' not offered by printer '{pm.PrinterName}'. Available: {string.Join(", ", avail)}");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Save Print Setting"))
        {
            t.Start();
            try
            {
                var setup = pm.PrintSetup;
                var pp = setup.CurrentPrintSetting.PrintParameters;
                pp.PaperSize = paper;
                pp.PageOrientation = orientation == "portrait" ? PageOrientationType.Portrait : PageOrientationType.Landscape;
                if (zoomToFit) pp.ZoomType = ZoomType.FitToPage;
                else { pp.ZoomType = ZoomType.Zoom; pp.Zoom = 100; }

                setup.SaveAs(saveAsName);
                t.Commit();
                sb.AppendLine($"Saved print setting '{saveAsName}' — {paperSizeName}, {orientation}, {(zoomToFit ? "fit to page" : "100%")} (printer '{pm.PrinterName}').");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to save print setting — rolled back. Reason: {ex.Message}");
            }
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"report\" or \"save\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
