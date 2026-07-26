// ============================================================
// FRAGMENT (action) — action-export-ifc.cs
// PURPOSE: Export the model to an IFC file — File > Export > IFC, scripted. Whole model by default, or
//          scoped to one 3D view's visible elements (the standard coordination-export pattern).
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — IFC scope is the document or a view,
//          not an element set; pair with any cheap filter or compose after a context script.
// NOT STANDALONE — see scripts/README.md for how to compose. Writes a file on disk (get a go-ahead
//          first, same as the other exports).
// GOTCHA: the export call runs INSIDE a transaction on purpose — the IFC exporter may write export-setup
//         data into the document, and Revit throws "modification forbidden" without one on some setups.
//         The transaction commits only when the export succeeded.
// FLAGGED 2026-07-26 (static review): the transaction-required behavior varies by Revit build/IFC exporter
//         version — if the first run throws "Starting a transaction ... is not permitted", delete the
//         transaction wrapper; that's environment feedback, not a logic bug.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp\ifc-export";    // created if missing
string fileName = "model";                      // ".ifc" added by Revit
string ifcVersion = "IFC2x3CV2";                // "IFC2x3CV2" | "IFC4" | "IFC2x3" — coordination default is IFC2x3CV2
int scopeViewIdInt = 0;                         // 0 = whole model; else a 3D view Id — exports only what's visible in it
// ---- END INPUTS ----

try
{
    System.IO.Directory.CreateDirectory(exportFolder);

    var options = new IFCExportOptions();
    if (ifcVersion == "IFC4") options.FileVersion = IFCVersion.IFC4;
    else if (ifcVersion == "IFC2x3") options.FileVersion = IFCVersion.IFC2x3;
    else options.FileVersion = IFCVersion.IFC2x3CV2;

    if (scopeViewIdInt != 0)
    {
        var scopeView = Document.GetElement(new ElementId(scopeViewIdInt)) as View3D;
        if (scopeView == null) sb.AppendLine($"NOTE: scopeViewIdInt {scopeViewIdInt} is not a 3D view — exporting the WHOLE model instead.");
        else options.FilterViewId = scopeView.Id;
    }

    using (var t = new Transaction(Document, "AJ Tools - Export IFC"))
    {
        t.Start();
        try
        {
            bool ok = Document.Export(exportFolder, fileName, options);
            if (ok)
            {
                t.Commit();
                sb.AppendLine($"IFC export ({ifcVersion}) written: '{System.IO.Path.Combine(exportFolder, fileName)}.ifc'{(scopeViewIdInt != 0 ? $", scoped to view Id {scopeViewIdInt}" : ", whole model")}.");
            }
            else
            {
                t.RollBack();
                sb.AppendLine("IFC export returned false — no file written (check the folder is writable and the exporter add-in is healthy).");
            }
        }
        catch (Exception exInner)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED IFC export. Reason: {exInner.Message}");
        }
    }
}
catch (Exception ex)
{
    sb.AppendLine($"FAILED IFC export before it started. Reason: {ex.Message}");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
