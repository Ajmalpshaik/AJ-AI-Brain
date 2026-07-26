// ============================================================
// FRAGMENT (action) — action-add-schedule-calculated-field.cs
// PURPOSE: Add a COMBINED PARAMETER field (several existing fields merged into one column with
//          prefix/suffix/separator per part) to every ViewSchedule in `elements`. Also supports
//          mode="formula" IF the installed Revit version's API supports it (see CONFIDENCE NOTE).
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/by-view-and-sheet/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// LIVE-VERIFIED 2026-07-22 on Revit 2020, mode="combined" only — see notes below.
// mode="formula": Calculated Value FORMULA fields have NO public API in Revit 2020 — confirmed by
// reflection: ScheduleDefinition has no AddCalculatedField method, and ScheduleField has no SetFormula/
// Formula member at all anywhere in its public surface. Formula calculated fields are UI-only on this
// Revit version (Insert > Calculated Value > Formula). That capability was added to the public API in a
// later Revit release (2022+) as ScheduleField.SetFormula(string)/GetFormula() — if running on Revit
// 2022+, that's the shape to try (roughly: `var f = def.AddField(ScheduleFieldType.Formula); f.
// SetFormula(formula);`) and re-verify against that version; it is NOT wired up here because it cannot
// compile against the Revit 2020 API this library currently runs against. On Revit 2020, mode="formula"
// reports the limitation into `sb` and does nothing, rather than throwing a compile error that would
// also break mode="combined" in the same file.
// mode="combined" was originally written against an imagined `CombinedParameterRule` class that does not
// exist anywhere in the Revit API. The real class is `TableCellCombinedParameterData` — a static
// `Create()` factory, with settable `ParamId` (ElementId, from SchedulableField.ParameterId — NOT a
// ScheduleFieldId), `CategoryId`, `Prefix`, `Suffix`, `Separator` properties — added to the schedule via
// `ScheduleDefinition.InsertCombinedParameterField(IList<TableCellCombinedParameterData>, fieldName,
// index)`, which creates AND configures the field in one call (no separate AddCalculatedField step).
// LIVE-VERIFIED: field created correctly (IsCombinedParameterField=true confirmed on a fresh re-fetch of
// the schedule after the transaction committed). Cell-VALUE rendering (the actual "prefix + value +
// separator + value + suffix" text) was confirmed structurally (correct column, correct name) but not
// against a populated data row — the test model had 0 instances of the schedule's category at test time.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "combined";          // "combined" (live-verified) | "formula" (Revit 2022+ API only, see note above — no-op on 2020)
string newFieldName = "Calc Field";
string formula = "";               // mode="formula" only, Revit 2022+ — e.g. "Height / 1000", referencing existing field names already on the schedule
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
                    failures.Add($"'{schedule.Name}': mode=\"formula\" has no public API on this Revit version (2020) — Calculated Value formula fields are UI-only here. Use mode=\"combined\", or add the field manually via Insert > Calculated Value.");
                }
                else if (mode == "combined")
                {
                    var schedulable = def.GetSchedulableFields();
                    var dataList = new List<TableCellCombinedParameterData>();
                    int resolvedCount = 0;
                    foreach (var (fieldName, prefix, suffix, separator) in combineFields)
                    {
                        var sf = schedulable.FirstOrDefault(x => x.GetName(Document).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                        if (sf == null) continue;
                        var data = TableCellCombinedParameterData.Create();
                        data.ParamId = sf.ParameterId;
                        data.CategoryId = def.CategoryId;
                        data.Prefix = prefix ?? "";
                        data.Suffix = suffix ?? "";
                        data.Separator = separator ?? "";
                        dataList.Add(data);
                        resolvedCount++;
                    }

                    if (resolvedCount < combineFields.Count)
                    {
                        failures.Add($"'{schedule.Name}': only {resolvedCount}/{combineFields.Count} combineFields entries resolved to a real schedulable field — check names against action-report-schedule-fields.cs");
                    }
                    else
                    {
                        def.InsertCombinedParameterField(dataList, newFieldName, def.GetFieldCount());
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
