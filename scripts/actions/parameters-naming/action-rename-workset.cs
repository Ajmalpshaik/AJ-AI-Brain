// ============================================================
// FRAGMENT (action) — action-rename-workset.cs
// PURPOSE: Rename an existing user Workset — completes workset management alongside create-workset.cs /
//          action-set-workset.cs / context-workset-info.cs.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — worksets aren't Elements.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: workset DELETE is CONFIRMED IMPOSSIBLE on Revit 2020 — WorksetTable.DeleteWorkset only exists
//         from Revit 2022 (UI-only before that: Collaborate > Worksets > Delete). mode="delete" reports
//         this instead of throwing, same convention as create-scope-box.cs.
// BLOCKED (model isn't workshared) — graceful path only; rename path NOT YET LIVE-VERIFIED
//          (created 2026-07-26 from the round-2 suggestions).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "rename";         // "rename" | "delete" (delete = recorded as impossible on 2020)
string worksetName = "";        // existing user workset name
string newWorksetName = "";     // rename only
// ---- END INPUTS ----

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is not workshared — no worksets exist. (Collaborate > Worksets to enable.)");
}
else if (mode == "delete")
{
    sb.AppendLine("Workset DELETE is impossible via the API on Revit 2020 — WorksetTable.DeleteWorkset only exists from Revit 2022.");
    sb.AppendLine($"Ask the user to delete '{worksetName}' manually: Collaborate > Worksets > select > Delete (choosing where its elements go).");
}
else if (mode == "rename")
{
    var ws = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).FirstOrDefault(w => w.Name == worksetName);
    if (ws == null)
    {
        var avail = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).Select(w => $"'{w.Name}'").ToList();
        sb.AppendLine($"User workset '{worksetName}' not found. Available: {string.Join(", ", avail)}");
    }
    else if (string.IsNullOrWhiteSpace(newWorksetName))
    {
        sb.AppendLine("newWorksetName is empty — nothing to rename to.");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Rename Workset"))
        {
            t.Start();
            try
            {
                WorksetTable.RenameWorkset(Document, ws.Id, newWorksetName);
                t.Commit();
                sb.AppendLine($"Renamed workset '{worksetName}' -> '{newWorksetName}'.");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to rename workset — rolled back. Reason: {ex.Message}");
            }
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"rename\" or \"delete\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
