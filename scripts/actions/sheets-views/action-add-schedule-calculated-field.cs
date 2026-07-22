// ============================================================
// FRAGMENT (action) — action-add-schedule-calculated-field.cs
// PURPOSE: Add a CALCULATED VALUE field (a formula referencing other fields already on the schedule) or a
//          COMBINED PARAMETER field (several existing fields merged into one column with prefix/suffix/
//          separator per part) to every ViewSchedule in `elements`.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// CONFIDENCE NOTE, READ BEFORE USING mode="combined": mode="formula" (Definition.AddCalculatedField +
// ScheduleField.SetFormula) is a well-documented, solid Revit API call. mode="combined" is NOT —
// CombinedParameterRule's exact constructor/setter shape could not be confidently verified from memory
// this session (unlike everything else in this library, which is either solid or clearly flagged as a
// deliberate workaround). Treat mode="combined" as a best-effort starting point, not trusted code — if it
// doesn't compile as-is, check Autodesk's Revit API docs for `CombinedParameterRule` / `ScheduleField.
// SetCombinedParameters` for your installed Revit version and adjust the argument list/order below. If it
// DOES compile and run, still verify the actual column output looks right before relying on it — this is
// the single least-certain fragment in the whole library right now.
// NOT YET LIVE-VERIFIED (mode="formula" included) — test on one schedule first before trusting a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "formula";           // "formula" | "combined"
string newFieldName = "Calc Field";
string formula = "";               // mode="formula" only — e.g. "Height / 1000", referencing existing field names already on the schedule
List<(string fieldName, string prefix, string suffix, string separator)> combineFields = new List<(string, string, string, string)>
{
    // mode="combined" only — order matters; separator is inserted BEFORE this field (ignored on the first entry)
    ("Family and Type", "", "", ""),
    ("Comments", "", "", " - ")
};
// ---- END INPUTS ----

int added = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Add Schedule Calculated Field"))
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
                if (mode == "formula")
                {
                    var field = def.AddCalculatedField(newFieldName, ScheduleFieldType.Formula);
                    field.SetFormula(formula);
                    added++;
                }
                else if (mode == "combined")
                {
                    var fieldIds = new List<ScheduleFieldId>();
                    var prefixes = new List<string>();
                    var suffixes = new List<string>();
                    var separators = new List<string>();
                    foreach (var (fieldName, prefix, suffix, separator) in combineFields)
                    {
                        var fid = def.GetFieldOrder().FirstOrDefault(id => def.GetField(id).GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                        if (fid == null) continue;
                        fieldIds.Add(fid);
                        prefixes.Add(prefix ?? "");
                        suffixes.Add(suffix ?? "");
                        separators.Add(separator ?? "");
                    }

                    if (fieldIds.Count < combineFields.Count)
                    {
                        failures.Add($"'{schedule.Name}': only {fieldIds.Count}/{combineFields.Count} combineFields entries resolved to a real field — check names against action-report-schedule-fields.cs");
                    }
                    else
                    {
                        var rule = new CombinedParameterRule(fieldIds, separators, prefixes, suffixes, fieldIds.Select(f => "").ToList());
                        var field = def.AddCalculatedField(newFieldName, ScheduleFieldType.Combined);
                        field.SetCombinedParameters(rule);
                        added++;
                    }
                }
                else
                {
                    failures.Add($"'{schedule.Name}': unknown mode '{mode}'");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"'{schedule.Name}': {ex.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Added '{newFieldName}' ({mode}) to {added} schedule(s), {skipped} non-ViewSchedule element(s) skipped.");
        if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to add calculated field — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
