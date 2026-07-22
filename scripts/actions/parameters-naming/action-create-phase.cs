// ============================================================
// FRAGMENT (action) — action-create-phase.cs
// PURPOSE: Create one or more new project Phases, each appended in order after whatever the current
//          last Phase is. Skips any name that already exists rather than creating a duplicate.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — a Phase isn't picked by a filter, it's a
//          whole-document naming operation, self-contained (declares its own `sb`, ends with its own
//          `return`).
// STATUS: not yet live-verified.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] newPhaseNames = { "Phase 1" };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var created = new List<string>();
var skipped = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Create Phase"))
{
    t.Start();
    try
    {
        foreach (var name in newPhaseNames)
        {
            var existing = Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) { skipped.Add($"{name} (already exists)"); continue; }

            Phase lastPhase = Document.Phases.get_Item(Document.Phases.Size - 1);
            Phase newPhase = Document.Phases.Insert(lastPhase);
            newPhase.Name = name;
            created.Add(name);
        }
        t.Commit();
        sb.AppendLine($"Created {created.Count} phase(s): {string.Join(", ", created)}.");
        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create phase(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
return sb.ToString();
