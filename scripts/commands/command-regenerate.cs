// ============================================================
// SCRIPT: command-regenerate.cs
// PURPOSE: Force Document.Regenerate() — useful after a composed script chains several actions where a
//          later one depends on geometry/parameters the earlier one just changed (new elements' real
//          bounding boxes, a just-duplicated type's properties, etc.) not yet reflected mid-transaction.
// LIVE-VERIFIED 2026-07-22 — FOUND AND FIXED A REAL BUG: the original fragment called
// `Document.Regenerate()` with no transaction of its own. Tested standalone (exactly how every other
// `commands/` fragment in this folder is meant to run, and exactly how this one would actually get used
// in practice — every `actions/` fragment in this library opens and commits its OWN `Transaction` inside
// itself, so by the time you'd insert this between two composed actions, the prior one's transaction is
// already closed and the next one's hasn't started) — it threw "Modification of the document is
// forbidden. Typically, this is because there is no open transaction." Regenerate() counts as a document
// modification to Revit and needs an open transaction just like any other write. Fixed by wrapping it in
// its own Transaction; re-verified working after the fix.
// ============================================================

var sb = new System.Text.StringBuilder();
using (var t = new Transaction(Document, "AJ Tools - Regenerate"))
{
    t.Start();
    try
    {
        Document.Regenerate();
        t.Commit();
        sb.AppendLine("Document regenerated.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to regenerate. Reason: {ex.Message}");
    }
}
return sb.ToString();
