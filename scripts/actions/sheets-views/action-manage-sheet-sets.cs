// ============================================================
// FRAGMENT (action) — action-manage-sheet-sets.cs
// PURPOSE: Manage named Sheet/View Sets (`ViewSheetSet` — the saved sets in Print/Export dialogs).
//          mode="report" lists every set + its member views; mode="create" saves `elements` (sheets/views,
//          e.g. from filter-by-sheets.cs) as a new named set; mode="delete" removes a named set;
//          mode="rename" renames one. Pairs with action-export-sheets-to-pdf.cs, which prints a set.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above. Only mode="create"
//          actually consumes `elements` — report/delete/rename ignore them (pair with any cheap filter).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: everything must go through PrintManager.ViewSheetSetting — a ViewSheetSet element cannot be
//         constructed directly, renamed by Element.Name, or removed by Document.Delete.
// GOTCHA: `PrintManager.ViewSheetSetting` THROWS "This property is only available when user choose Select
//         of Print Range" unless you set `pm.PrintRange = PrintRange.Select` first. Every mode that touches
//         it does that first.
// GOTCHA: PrintManager state is NOT transactional — PrintRange stays changed even if the transaction rolls
//         back. Harmless (it is a dialog default, not model data), but do not expect a rollback to undo it.
// LIVE-VERIFIED 2026-08-14 — all four modes against sheets M-901/902/903, each verified from a separate
//         bridge call, not from this script's own report.
// CORRECTED 2026-08-14 — the previous header claimed selecting an existing saved set as CurrentViewSheetSet
//         "is awkward on 2020" and routed rename/delete through Element.Name / Document.Delete instead.
//         Measured: that is FALSE. `CurrentViewSheetSet` is a settable property (reflection: canWrite=True),
//         assigning it works, and the old route FAILED live with "You can NOT set the name here, please set
//         the name via PrintSetup::Rename method!". Both now use the proper route.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "report";        // "report" | "create" | "delete" | "rename"
string setName = "";           // create/delete/rename: the set's name
string newSetName = "";        // rename only: the new name
// ---- END INPUTS ----

var allSets = new FilteredElementCollector(Document).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>().ToList();

if (mode == "report")
{
    if (allSets.Count == 0) sb.AppendLine("No saved sheet/view sets in this document.");
    else
    {
        sb.AppendLine($"Saved sheet/view sets ({allSets.Count}):");
        foreach (var s in allSets.OrderBy(s => s.Name))
        {
            var members = s.Views.Cast<View>().ToList();
            sb.AppendLine($"  - '{s.Name}' (Id {s.Id.IntegerValue}) — {members.Count} view(s)/sheet(s): {string.Join(", ", members.Take(15).Select(v => $"'{v.Name}'"))}{(members.Count > 15 ? ", ..." : "")}");
        }
    }
}
else if (mode == "create")
{
    var views = elements.OfType<View>().ToList();
    if (views.Count == 0) sb.AppendLine("Nothing to save — `elements` holds no Views/ViewSheets.");
    else if (allSets.Any(s => s.Name == setName)) sb.AppendLine($"A set named '{setName}' already exists (Id {allSets.First(s => s.Name == setName).Id.IntegerValue}) — delete or rename it first.");
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Create Sheet Set"))
        {
            t.Start();
            try
            {
                var pm = Document.PrintManager;
                pm.PrintRange = PrintRange.Select;
                var vss = pm.ViewSheetSetting;
                var viewSet = new ViewSet();
                foreach (var v in views) viewSet.Insert(v);
                vss.CurrentViewSheetSet.Views = viewSet;
                vss.SaveAs(setName);
                t.Commit();
                sb.AppendLine($"Saved sheet set '{setName}' with {views.Count} view(s)/sheet(s).");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to create sheet set '{setName}' — rolled back. Reason: {ex.Message}");
            }
        }
    }
}
else if (mode == "delete" || mode == "rename")
{
    var target = allSets.FirstOrDefault(s => s.Name == setName);
    if (target == null) sb.AppendLine($"No saved set named '{setName}'. Available: {string.Join(", ", allSets.Select(s => $"'{s.Name}'"))}");
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - " + (mode == "delete" ? "Delete" : "Rename") + " Sheet Set"))
        {
            t.Start();
            try
            {
                // Both routes go through ViewSheetSetting — select the existing set as current, then act.
                // PrintRange.Select FIRST or `ViewSheetSetting` itself throws.
                var pm = Document.PrintManager;
                pm.PrintRange = PrintRange.Select;
                var vss = pm.ViewSheetSetting;
                vss.CurrentViewSheetSet = target;

                if (mode == "delete")
                {
                    vss.Delete();
                    t.Commit();
                    sb.AppendLine($"Deleted sheet set '{setName}'.");
                }
                else
                {
                    vss.Rename(newSetName);
                    t.Commit();
                    sb.AppendLine($"Renamed sheet set '{setName}' -> '{newSetName}'.");
                }
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to {mode} sheet set '{setName}' — rolled back. Reason: {ex.Message}");
            }
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"report\", \"create\", \"delete\", or \"rename\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
