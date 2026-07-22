// ============================================================
// FRAGMENT (action) — action-export-schedule-to-csv.cs
// PURPOSE: Export every ViewSchedule in `elements` to a CSV file via Revit's own native
//          ViewSchedule.Export/ScheduleExportOptions — the same mechanism as File > Export > Reports >
//          Schedule, scripted. One CSV file per schedule (Revit's own export API doesn't combine multiple
//          schedules into one file).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: exportFolder must already exist on disk — this does NOT create it.
// NOT YET LIVE-VERIFIED — test on one schedule first before trusting it on a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string exportFolder = @"C:\Temp"; // must already exist
string fieldDelimiter = ",";
bool includeTitle = false;
bool includeColumnHeaders = true;
// ---- END INPUTS ----

var schedules = elements.OfType<ViewSchedule>().ToList();

if (schedules.Count == 0)
{
    sb.AppendLine("No ViewSchedule elements found in `elements` — nothing to export.");
}
else if (!System.IO.Directory.Exists(exportFolder))
{
    sb.AppendLine($"Export folder does not exist: {exportFolder} — create it first, this fragment won't.");
}
else
{
    var options = new ViewScheduleExportOptions
    {
        FieldDelimiter = fieldDelimiter,
        Title = includeTitle,
        ColumnHeaders = includeColumnHeaders ? ExportColumnHeaders.OneRow : ExportColumnHeaders.None
    };

    int exported = 0, skipped = 0;
    var failures = new List<string>();

    foreach (var sch in schedules)
    {
        string safeName = string.Join("_", sch.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
        try
        {
            sch.Export(exportFolder, safeName + ".csv", options);
            exported++;
        }
        catch (Exception ex)
        {
            skipped++;
            failures.Add($"'{sch.Name}': {ex.Message}");
        }
    }

    sb.AppendLine($"Exported {exported} schedule(s) to '{exportFolder}', skipped {skipped}.");
    if (failures.Count > 0)
        sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) + (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
