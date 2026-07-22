// ============================================================
// FRAGMENT (action) — action-delete-phase.cs
// PURPOSE: Permanently delete one or more project Phases by name — completes the phase
//          Create/Rename/Delete lifecycle. Revit itself refuses to delete a phase that elements are still
//          assigned to, or the project's only phase — those come back as a skip, not a crash.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================
// MANDATORY before running this for real: confirm the phase names and get an explicit go-ahead from the
// user first — deleting a Phase is not undoable from this bridge beyond Revit's own native Undo. The
// bridge call itself also needs allowDestructive: true (it refuses Delete otherwise).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] phaseNamesToDelete = { };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
int deleted = 0;
var skipped = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Delete Phase"))
{
    t.Start();
    try
    {
        foreach (var name in phaseNamesToDelete)
        {
            var phase = Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (phase == null) { skipped.Add($"{name} (not found)"); continue; }

            try
            {
                Document.Delete(phase.Id);
                deleted++;
            }
            catch (Exception exOne)
            {
                skipped.Add($"{name} ({exOne.Message})");
            }
        }
        t.Commit();
        sb.AppendLine($"Deleted {deleted} phase(s).");
        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to delete phase(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
return sb.ToString();
