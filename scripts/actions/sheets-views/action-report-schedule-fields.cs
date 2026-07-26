// ============================================================
// FRAGMENT (action) — action-report-schedule-fields.cs
// PURPOSE: List every field/column on each ViewSchedule in `elements`, IN ORDER — position, field name,
//          column heading (if overridden), field type (Instance/ElementType/Formula/Combined/Total/Count/
//          ...), hidden flag, alignment. Read-only. The discovery step before
//          action-remove-schedule-field.cs/action-set-schedule-field-format.cs (both need an exact
//          existing field name), and shows which columns are already Calculated/Combined fields from
//          action-add-schedule-calculated-field.cs.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/by-view-and-sheet/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

int skipped = 0;

foreach (var el in elements)
{
    var schedule = el as ViewSchedule;
    if (schedule == null) { skipped++; continue; }

    var def = schedule.Definition;
    var fieldIds = def.GetFieldOrder();
    sb.AppendLine($"Schedule '{schedule.Name}' — {fieldIds.Count} field(s), in order:");
    sb.AppendLine("  # | Name | Heading | Type | Hidden | Alignment");
    sb.AppendLine("  --- | --- | --- | --- | --- | ---");
    for (int i = 0; i < fieldIds.Count; i++)
    {
        var field = def.GetField(fieldIds[i]);
        sb.AppendLine($"  {i + 1} | {field.GetName()} | {field.ColumnHeading} | {field.FieldType} | {field.IsHidden} | {field.HorizontalAlignment}");
    }
}
if (skipped > 0) sb.AppendLine($"{skipped} non-ViewSchedule element(s) skipped.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
