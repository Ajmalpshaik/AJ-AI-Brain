// ============================================================
// FRAGMENT (action) — action-set-schedule-sort-group.cs
// PURPOSE: Replace ALL sort/group fields on every ViewSchedule in `elements` with the ones given here, in
//          order — clears whatever sort/group setup already existed first, then adds the new list (same
//          "replace, not additive" reasoning as action-set-schedule-filters.cs).
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// Live-verified 2026-07-22, zero bugs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(string fieldName, bool descending, bool showHeader, bool showFooter, bool showBlankLine)> sortGroupFields = new List<(string, bool, bool, bool, bool)>
{
    ("Level", false, true, false, false)
};
// ---- END INPUTS ----

int schedulesUpdated = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Schedule Sort/Group"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var schedule = el as ViewSchedule;
            if (schedule == null) { skipped++; continue; }

            var def = schedule.Definition;
            try
            {
                for (int i = def.GetSortGroupFieldCount() - 1; i >= 0; i--) def.RemoveSortGroupField(i);

                int applied = 0;
                foreach (var (fieldName, descending, showHeader, showFooter, showBlankLine) in sortGroupFields)
                {
                    var fieldId = def.GetFieldOrder().FirstOrDefault(id => def.GetField(id).GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (fieldId == null) continue;

                    var sg = new ScheduleSortGroupField(fieldId)
                    {
                        SortOrder = descending ? ScheduleSortOrder.Descending : ScheduleSortOrder.Ascending,
                        ShowHeader = showHeader,
                        ShowFooter = showFooter,
                        ShowBlankLine = showBlankLine
                    };
                    def.AddSortGroupField(sg);
                    applied++;
                }
                schedulesUpdated++;
                if (applied < sortGroupFields.Count) failures.Add($"'{schedule.Name}': only {applied}/{sortGroupFields.Count} sort/group field(s) applied (field name not resolvable)");
            }
            catch (Exception ex)
            {
                failures.Add($"'{schedule.Name}': {ex.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Set sort/group on {schedulesUpdated} schedule(s), {skipped} non-ViewSchedule element(s) skipped.");
        if (failures.Count > 0) sb.AppendLine("Notes: " + string.Join("; ", failures));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set schedule sort/group — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
