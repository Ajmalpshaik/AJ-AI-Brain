// ============================================================
// FRAGMENT (action) — action-delete-revision.cs
// PURPOSE: Permanently delete one or more project Revisions by SequenceNumber — completes the revision
//          Create/Edit/Delete lifecycle. Revit itself refuses to delete a Revision that any Revision Cloud
//          in the model still references — that comes back as a skip, not a crash.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// GOTCHA: deleting a revision RENUMBERS every later one — Revision #3 out of 5 deleted means the old #4
//         and #5 become #3 and #4. Any note/reference to "Revision 4" made before this delete no longer
//         points at the same revision afterward. Re-run action-report-revisions.cs after deleting to see
//         the corrected numbering before relying on it again.
// ============================================================
// MANDATORY before running this for real: confirm the exact revisions (via action-report-revisions.cs)
// and get an explicit go-ahead from the user first — deleting a Revision is not undoable from this bridge
// beyond Revit's own native Undo. The bridge call itself also needs allowDestructive: true (it refuses
// Delete otherwise).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<int> sequenceNumbersToDelete = new List<int> { };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
int deleted = 0;
var skipped = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Delete Revision"))
{
    t.Start();
    try
    {
        foreach (var seq in sequenceNumbersToDelete)
        {
            var revision = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
                .FirstOrDefault(r => r.SequenceNumber == seq);
            if (revision == null) { skipped.Add($"#{seq} (not found)"); continue; }

            try
            {
                Document.Delete(revision.Id);
                deleted++;
            }
            catch (Exception exOne)
            {
                skipped.Add($"#{seq} ({exOne.Message})");
            }
        }
        t.Commit();
        sb.AppendLine($"Deleted {deleted} revision(s).");
        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
        if (deleted > 0) sb.AppendLine("Remaining revisions renumbered — run action-report-revisions.cs to see the new sequence.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to delete revision(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
return sb.ToString();
