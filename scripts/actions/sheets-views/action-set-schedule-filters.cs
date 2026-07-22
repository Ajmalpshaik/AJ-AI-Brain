// ============================================================
// FRAGMENT (action) — action-set-schedule-filters.cs
// PURPOSE: Replace ALL filter rules on every ViewSchedule in `elements` with the ones given here — clears
//          whatever filters already existed first, then adds the new list. Not additive by design (avoids
//          silently stacking filters run after run); re-run with the SAME list plus one more entry if the
//          intent is genuinely additive.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: value is always given as a string here and converted — numeric filter types
//         (GreaterThan/LessThan/etc.) parse it as a double, text filter types (Contains/BeginsWith/Equal/
//         etc.) use it as-is; HasValue/HasNoValue ignore value entirely.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(string fieldName, string filterType, string value)> filtersToSet = new List<(string, string, string)>
{
    ("Comments", "Contains", "REVIEW")
    // filterType: Equal | NotEqual | GreaterThan | GreaterThanOrEqual | LessThan | LessThanOrEqual |
    //             Contains | NotContains | BeginsWith | NotBeginsWith | EndsWith | NotEndsWith |
    //             HasValue | HasNoValue
};
// ---- END INPUTS ----

int schedulesUpdated = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Schedule Filters"))
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
                for (int i = def.GetFilterCount() - 1; i >= 0; i--) def.RemoveFilter(i);

                int applied = 0;
                foreach (var (fieldName, filterTypeName, value) in filtersToSet)
                {
                    var fieldId = def.GetFieldOrder().FirstOrDefault(id => def.GetField(id).GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (fieldId == null) continue;
                    if (!Enum.TryParse<ScheduleFilterType>(filterTypeName, true, out var filterType)) continue;

                    ScheduleFilter filter;
                    if (filterType == ScheduleFilterType.HasValue || filterType == ScheduleFilterType.HasNoValue)
                        filter = new ScheduleFilter(fieldId, filterType);
                    else if (double.TryParse(value, out var numeric))
                        filter = new ScheduleFilter(fieldId, filterType, numeric);
                    else
                        filter = new ScheduleFilter(fieldId, filterType, value);

                    def.AddFilter(filter);
                    applied++;
                }
                schedulesUpdated++;
                if (applied < filtersToSet.Count) failures.Add($"'{schedule.Name}': only {applied}/{filtersToSet.Count} filter(s) applied (field/type not resolvable)");
            }
            catch (Exception ex)
            {
                failures.Add($"'{schedule.Name}': {ex.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Set filters on {schedulesUpdated} schedule(s), {skipped} non-ViewSchedule element(s) skipped.");
        if (failures.Count > 0) sb.AppendLine("Notes: " + string.Join("; ", failures));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set schedule filters — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
