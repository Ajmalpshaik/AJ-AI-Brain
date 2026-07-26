// ============================================================
// FRAGMENT (action) — action-export-nwc.cs
// PURPOSE: Export the model to a Navisworks NWC file — the coordination handoff format. Whole model or
//          scoped to one 3D view (the usual "NWC export view" pattern).
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — same document/view scope reasoning as
//          action-export-ifc.cs.
// NOT STANDALONE — see scripts/README.md for how to compose. Writes a file on disk (go-ahead first).
// GOTCHA: NWC export only exists when the Navisworks exporter add-in is installed in this Revit — checked
//         first via OptionalFunctionalityUtils and reported gracefully, not thrown.
// GOTCHA: no Transaction — plain document I/O (unlike IFC, the NWC exporter doesn't write into the doc).
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp\nwc-export";    // created if missing
string fileName = "model";                      // ".nwc" added by Revit
int scopeViewIdInt = 0;                         // 0 = whole model; else a 3D view Id — exports only what's visible in it
// ---- END INPUTS ----

if (!OptionalFunctionalityUtils.IsNavisworksExporterAvailable())
{
    sb.AppendLine("The Navisworks exporter add-in is NOT installed in this Revit — NWC export unavailable. (Install 'Navisworks NWC Export Utility' from Autodesk, then rerun.)");
}
else
{
    try
    {
        System.IO.Directory.CreateDirectory(exportFolder);

        var options = new NavisworksExportOptions();
        if (scopeViewIdInt != 0)
        {
            var scopeView = Document.GetElement(new ElementId(scopeViewIdInt)) as View3D;
            if (scopeView == null) sb.AppendLine($"NOTE: scopeViewIdInt {scopeViewIdInt} is not a 3D view — exporting the WHOLE model instead.");
            else
            {
                options.ExportScope = NavisworksExportScope.View;
                options.ViewId = scopeView.Id;
            }
        }

        Document.Export(exportFolder, fileName, options);
        sb.AppendLine($"NWC export written: '{System.IO.Path.Combine(exportFolder, fileName)}.nwc'{(scopeViewIdInt != 0 ? $", scoped to view Id {scopeViewIdInt}" : ", whole model")}.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED NWC export. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
