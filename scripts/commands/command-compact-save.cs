// ============================================================
// SCRIPT: command-compact-save.cs
// PURPOSE: Save the active document with Compact = true — Revit's "Compact File" checkbox, which rewrites
//          the file without the accumulated dead space a normal incremental save leaves behind. The
//          scripted answer to "compact the model".
// GOTCHA: a compact save takes noticeably longer than a normal save on a big model — that's expected,
//         not a hang.
// GOTCHA: on a WORKSHARED local this saves the local file only — syncing to central is a different job
//         (command-sync-with-central.cs).
// BLOCKED for the document open in Revit's UI — which is what this fragment targets. NOT blocked in
//          general: corrected 2026-08-11, `SaveAs`/`Save` DO work on a document the API created itself
//          via `Application.NewFamilyDocument(...)`, because that one is not the UI document. See
//          ../../knowledge/live-model/families.md § Fourth build for the full author-and-save pattern.
//          For the active document, everything below still holds. Proven 2026-08-10:
//          `Document.SaveAs(...)` throws "Operation is not permitted when there is any open transaction
//          phase started by API client", and `Document.Save(...)` hits the same wall on a document that
//          already has a path. The bridge holds a TransactionGroup around every `run_csharp` call —
//          note that `Document.IsModifiable` reads **False** at the time, so it is NOT a valid way to
//          test for this. There is no in-script workaround; committing the bridge's own group would
//          break the bridge. Ask the user to do File -> Save (or Save As) in Revit instead, and hand
//          them the exact folder and filename. Keep this fragment as the documented dead end so a
//          future session doesn't re-derive it. Created 2026-07-26 from the tool-gap backlog.
// ============================================================

var sb = new System.Text.StringBuilder();

if (string.IsNullOrEmpty(Document.PathName))
{
    sb.AppendLine("This document has never been saved (no file path) — do a normal Save As in Revit first.");
}
else if (Document.IsReadOnly)
{
    sb.AppendLine("Document is read-only — cannot save.");
}
else
{
    try
    {
        long beforeBytes = 0;
        try { beforeBytes = new System.IO.FileInfo(Document.PathName).Length; } catch { }

        Document.Save(new SaveOptions { Compact = true });

        long afterBytes = 0;
        try { afterBytes = new System.IO.FileInfo(Document.PathName).Length; } catch { }

        sb.AppendLine($"Compact save done: '{Document.PathName}'.");
        if (beforeBytes > 0 && afterBytes > 0)
            sb.AppendLine($"File size: {beforeBytes / 1048576.0:F1} MB -> {afterBytes / 1048576.0:F1} MB.");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"FAILED to compact-save. Reason: {ex.Message}");
    }
}
return sb.ToString();
