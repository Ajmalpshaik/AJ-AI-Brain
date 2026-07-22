// ============================================================
// FRAGMENT (action) — action-rename-phase.cs
// PURPOSE: Rename one or more existing project Phases, old name -> new name.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// STATUS: not yet live-verified.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
(string oldName, string newName)[] renames = { ("Existing", "Phase 0 - Existing") };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
int renamed = 0;
var skipped = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Rename Phase"))
{
    t.Start();
    try
    {
        foreach (var (oldName, newName) in renames)
        {
            var phase = Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            if (phase == null) { skipped.Add($"{oldName} (not found)"); continue; }

            try
            {
                phase.Name = newName;
                renamed++;
            }
            catch (Exception exOne)
            {
                skipped.Add($"{oldName} -> {newName} ({exOne.Message})");
            }
        }
        t.Commit();
        sb.AppendLine($"Renamed {renamed} phase(s).");
        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rename phase(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
return sb.ToString();
