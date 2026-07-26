// ============================================================
// SCRIPT: command-compact-save.cs
// PURPOSE: Save the active document with Compact = true — Revit's "Compact File" checkbox, which rewrites
//          the file without the accumulated dead space a normal incremental save leaves behind. The
//          scripted answer to "compact the model".
// GOTCHA: a compact save takes noticeably longer than a normal save on a big model — that's expected,
//         not a hang.
// GOTCHA: on a WORKSHARED local this saves the local file only — syncing to central is a different job
//         (command-sync-with-central.cs).
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog. Saving writes the file on disk;
//          get the user's go-ahead before the first run, same as any outward write.
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
