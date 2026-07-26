// ============================================================
// SCRIPT: command-sync-with-central.cs
// PURPOSE: Synchronize a workshared local with its central model and relinquish everything — Revit's
//          "Synchronize with Central" + "Relinquish all mine", scripted. The answer to
//          "sync and relinquish".
// GOTCHA: relinquishAll releases ALL of the user's borrowed elements and owned worksets after the sync —
//         that's the normal end-of-session behavior, but set it false if the user wants to keep checkouts.
// GOTCHA: sync can take minutes on a big central over a slow network — not a hang.
// BLOCKED (model isn't workshared) — graceful path is the only one exercisable right now; the real sync
//          path is NOT YET LIVE-VERIFIED (created 2026-07-26 from the tool-gap backlog). Sync writes the
//          central model — get the user's go-ahead before the first real run.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string syncComment = "";        // comment recorded in the central's sync history
bool relinquishAll = true;      // release all borrowed elements + owned user worksets after sync
bool saveLocalAfter = true;     // also save the local file after the sync (Revit's own default behavior)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is not workshared — nothing to synchronize. (Collaborate > Worksets to enable worksharing.)");
}
else
{
    try
    {
        var transact = new TransactWithCentralOptions();
        var sync = new SynchronizeWithCentralOptions();
        sync.Comment = syncComment;
        sync.SaveLocalAfter = saveLocalAfter;
        if (relinquishAll)
        {
            var rel = new RelinquishOptions(true); // true = relinquish everything relinquishable
            sync.SetRelinquishOptions(rel);
        }

        Document.SynchronizeWithCentral(transact, sync);
        sb.AppendLine($"Synchronized with central. Relinquish all: {relinquishAll}, local saved after: {saveLocalAfter}, comment: '{syncComment}'.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to synchronize with central. Reason: {ex.Message}");
    }
}
return sb.ToString();
