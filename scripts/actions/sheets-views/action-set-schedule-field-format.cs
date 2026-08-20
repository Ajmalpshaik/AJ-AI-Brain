// ============================================================
// FRAGMENT (action) — action-set-schedule-field-format.cs
// PURPOSE: Format ONE existing column (matched by current field name) on every ViewSchedule in `elements`
//          — override heading text, hide/show it, horizontal alignment, and/or sheet column width. Any
//          input left null is not touched.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/by-view-and-sheet/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string fieldName = "Comments";        // current field/column name to format
string newHeading = null;             // null = don't change; "" = clear override, use the field's own name
bool? isHidden = null;                // null = don't change
string alignment = null;              // "Left" | "Center" | "Right"; null = don't change
double? columnWidthMm = null;         // sheet column width in mm; null = don't change
// ---- END INPUTS ----

int updated = 0, notFound = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Schedule Field Format"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var schedule = el as ViewSchedule;
            if (schedule == null) { skipped++; continue; }

            var def = schedule.Definition;
            var fieldId = def.GetFieldOrder().FirstOrDefault(id => def.GetField(id).GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (fieldId == null) { notFound++; continue; }

            var field = def.GetField(fieldId);
            try
            {
                if (newHeading != null) field.ColumnHeading = newHeading;
                if (isHidden.HasValue) field.IsHidden = isHidden.Value;
                if (!string.IsNullOrEmpty(alignment) && Enum.TryParse<ScheduleHorizontalAlignment>(alignment, true, out var ha)) field.HorizontalAlignment = ha;
                if (columnWidthMm.HasValue) field.SheetColumnWidth = columnWidthMm.Value / 304.8;
                updated++;
            }
            catch (Exception ex)
            {
                failures.Add($"'{schedule.Name}': {ex.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Formatted field '{fieldName}' on {updated} schedule(s), {notFound} didn't have that field, {skipped} non-ViewSchedule element(s) skipped.");
        if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to format schedule field — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
