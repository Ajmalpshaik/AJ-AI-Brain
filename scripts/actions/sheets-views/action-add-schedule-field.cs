// ============================================================
// FRAGMENT (action) — action-add-schedule-field.cs
// PURPOSE: Add one or more columns/fields to every ViewSchedule in `elements` — same
//          GetSchedulableFields()/AddField() call creators/create-schedule.cs uses, split out so it can be
//          run again later on a schedule that already exists.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: fieldNames must match GetSchedulableFields()'s real names for that schedule's category exactly
//         — these are category-specific, not free text.
// Live-verified 2026-07-22, zero bugs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] fieldNames = { "Comments" };
// ---- END INPUTS ----

int added = 0, notFound = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Add Schedule Field"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var schedule = el as ViewSchedule;
            if (schedule == null) { skipped++; continue; }

            var schedulable = schedule.Definition.GetSchedulableFields();
            foreach (var wantedName in fieldNames)
            {
                var match = schedulable.FirstOrDefault(f => f.GetName(Document).Equals(wantedName, StringComparison.OrdinalIgnoreCase));
                if (match == null) { notFound++; continue; }
                try { schedule.Definition.AddField(match); added++; }
                catch { notFound++; } // already present, or not addable to this schedule type
            }
        }
        t.Commit();
        sb.AppendLine($"Added {added} field(s) across {elements.Count - skipped} schedule(s), {notFound} requested field(s) not found/addable, {skipped} non-ViewSchedule element(s) skipped.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to add schedule field(s) — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
