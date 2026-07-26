// ============================================================
// FRAGMENT (creator) — create-sheet-list.cs
// PURPOSE: Create a Sheet List (drawing index) schedule — the table of every sheet in the project, which
//          goes on the cover sheet. A different API call from create-schedule.cs: a sheet list schedules
//          SHEETS, not model elements, so the normal category route can't produce one.
// PRODUCES: elements (List<Element>, the new sheet list schedule), sb
// NOT STANDALONE — see scripts/README.md for how to compose. Chain action-place-schedule-on-sheet.cs to
//          drop it on the cover, or the schedule-formatting actions to style it.
// GOTCHA: field names on a sheet list are sheet parameters ("Sheet Number", "Sheet Name", "Current
//         Revision", "Sheet Issue Date") — not model parameters. A name that isn't schedulable is
//         skipped and REPORTED, with the available names listed so the next run can be exact.
// GOTCHA: to include sheets that don't exist yet, Revit uses placeholder sheets — those are UI-created
//         and not covered here.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — created a real sheet list with both columns and the sort field,
//   then reverted it with commands/native-undo.cs. `ViewSchedule.CreateSheetList` does exist on Revit 2020.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string scheduleName = "Drawing Index";
string[] fieldNames = new[] { "Sheet Number", "Sheet Name" }; // in column order
string sortByField = "Sheet Number";  // null = no sorting
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Create Sheet List"))
{
    t.Start();
    try
    {
        var schedule = ViewSchedule.CreateSheetList(Document);
        try { schedule.Name = scheduleName; } catch { } // name collision — keeps generated name

        var definition = schedule.Definition;
        var schedulable = definition.GetSchedulableFields();

        var added = new List<string>();
        var missing = new List<string>();
        foreach (var wanted in fieldNames)
        {
            var sf = schedulable.FirstOrDefault(f => f.GetName(Document) == wanted);
            if (sf == null) { missing.Add(wanted); continue; }
            definition.AddField(sf);
            added.Add(wanted);
        }

        if (!string.IsNullOrEmpty(sortByField))
        {
            var field = definition.GetFieldOrder()
                .Select(id => definition.GetField(id))
                .FirstOrDefault(f => f.GetName() == sortByField);
            if (field != null)
                definition.AddSortGroupField(new ScheduleSortGroupField(field.FieldId, ScheduleSortOrder.Ascending));
            else
                missing.Add($"{sortByField} (as sort field)");
        }

        elements.Add(schedule);
        t.Commit();
        sb.AppendLine($"Created sheet list '{schedule.Name}' (Id {schedule.Id.IntegerValue}) with {added.Count} column(s): {string.Join(", ", added)}{(string.IsNullOrEmpty(sortByField) ? "" : $"; sorted by {sortByField}")}.");
        if (missing.Count > 0)
        {
            sb.AppendLine($"  NOT added (not schedulable on a sheet list): {string.Join(", ", missing)}");
            sb.AppendLine($"  Available field names: {string.Join(", ", schedulable.Select(f => f.GetName(Document)).OrderBy(n => n).Take(40))}");
        }
    }
    catch (Exception ex)
    {
        try { t.RollBack(); } catch { }
        sb.AppendLine($"FAILED to create sheet list — rolled back, nothing created. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
