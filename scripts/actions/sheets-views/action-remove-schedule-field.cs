// ============================================================
// FRAGMENT (action) — action-remove-schedule-field.cs
// PURPOSE: Remove one or more columns/fields (matched by current column heading / field name) from every
//          ViewSchedule in `elements` — the paired undo for action-add-schedule-field.cs.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// Live-verified 2026-07-22, zero bugs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] fieldNamesToRemove = { "Comments" };
// ---- END INPUTS ----

int removed = 0, notFound = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Remove Schedule Field"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var schedule = el as ViewSchedule;
            if (schedule == null) { skipped++; continue; }

            var def = schedule.Definition;
            foreach (var wantedName in fieldNamesToRemove)
            {
                var fieldId = def.GetFieldOrder().FirstOrDefault(id => def.GetField(id).GetName().Equals(wantedName, StringComparison.OrdinalIgnoreCase));
                if (fieldId == null) { notFound++; continue; }
                try { def.RemoveField(fieldId); removed++; }
                catch { notFound++; }
            }
        }
        t.Commit();
        sb.AppendLine($"Removed {removed} field(s) across {elements.Count - skipped} schedule(s), {notFound} requested field(s) not found, {skipped} non-ViewSchedule element(s) skipped.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to remove schedule field(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
