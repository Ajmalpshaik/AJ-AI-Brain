// ============================================================
// FRAGMENT (creator) — create-schedule.cs
// PURPOSE: Create one new schedule (ViewSchedule) for a category, with a chosen set of fields/columns.
//
// ***A NEW SCHEDULE IS NOT FINISHED WHEN ITS COLUMNS ARE RIGHT (Ajmal, 2026-08-19).*** A project that
// organises its Project Browser by custom view parameters will drop a freshly created schedule into the
// wrong group — or no group — because those parameters start EMPTY. On the Tecnimont MM_ projects the two
// that matter are **`View Owner`** and **`View Use Group`** (values there: `Tecnimont` and `03 - MileMate!`).
// His words: *"there is parameter for this schedule, view owner and view use group, that we need to change
// as per the MM_V03, same like that we need for new creating schedules also."*
// **Copy them off an EXISTING schedule at run time, never hardcode them** — they differ per project, the
// same way CWA does:
//     var src = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
//                   .FirstOrDefault(s => s.Name == "MM_V03");          // the project's reference schedule
//     foreach (var bp in new[] { "View Owner", "View Use Group" }) {
//         var sp = src.LookupParameter(bp); var dp = schedule.LookupParameter(bp);
//         if (sp != null && dp != null && !dp.IsReadOnly) dp.Set(sp.AsString() ?? "");
//     }
// Do this inside the same transaction that creates the schedule. Verified live 2026-08-19 on
// 4355-BHVD-3D-60P00-BL003A.
//          Bare schedule only — no sorting/grouping/filtering/formatting configured; that still has to be
//          set up in Revit's Schedule Properties dialog afterward, or added later as its own script if
//          this becomes a repeated need.
// PRODUCES: elements (List<Element>) — the newly created ViewSchedule, sb (StringBuilder)
// NOT STANDALONE — see scripts/README.md for how to compose (chain into
//          actions/sheets-views/action-place-schedule-on-sheet.cs to put it on a sheet).
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;
string scheduleName = "New Schedule";
string[] fieldNames = { "Family and Type", "Count" }; // must match GetSchedulableFields' real names for this category
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var categoryElement = Autodesk.Revit.DB.Category.GetCategory(Document, targetCategory);
if (categoryElement == null)
{
    sb.AppendLine($"Category {targetCategory} not found in this document.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Schedule"))
    {
        t.Start();
        try
        {
            // ASK FIRST. Not every category can carry a schedule, and CreateSchedule throws a bare
            // ArgumentException on the ones that cannot - which reaches the user as a stack trace
            // instead of "that category cannot be scheduled". Revit will answer directly.
            // Added 2026-08-24.
            bool okForSchedule = true;
            try { okForSchedule = ViewSchedule.IsValidCategoryForSchedule(categoryElement.Id); } catch { }
            if (!okForSchedule)
            {
                t.RollBack();
                sb.AppendLine($"'{categoryElement.Name}' cannot be scheduled - Revit does not allow a schedule on that category.");
                // Revit will also hand over the WHOLE valid list, so the refusal can name the alternatives
                // instead of leaving the user to guess. GetValidCategoriesForSchedule is a static.
                try
                {
                    var valid = ViewSchedule.GetValidCategoriesForSchedule();
                    if (valid != null && valid.Count > 0)
                    {
                        var names = valid.Select(id => Category.GetCategory(Document, id))
                                         .Where(c => c != null).Select(c => c.Name)
                                         .OrderBy(x => x).Take(40).ToList();
                        sb.AppendLine($"Revit accepts {valid.Count} categories here. The first {names.Count} alphabetically: {string.Join(", ", names)}");
                    }
                }
                catch { }
                sb.AppendLine("Annotation, view and datum categories are the usual refusals. Or use create-key-schedule.cs for a key schedule.");
                return sb.ToString();
            }

            var schedule = ViewSchedule.CreateSchedule(Document, categoryElement.Id);
            schedule.Name = scheduleName;

            var schedulable = schedule.Definition.GetSchedulableFields();
            int added = 0, notFound = 0;
            foreach (var wantedName in fieldNames)
            {
                var match = schedulable.FirstOrDefault(f =>
                    f.GetName(Document).Equals(wantedName, StringComparison.OrdinalIgnoreCase));
                if (match == null) { notFound++; continue; }
                schedule.Definition.AddField(match);
                added++;
            }

            elements.Add(schedule);
            t.Commit();
            sb.AppendLine($"Created schedule '{schedule.Name}' for category {targetCategory} with {added} field(s)" +
                (notFound > 0 ? $", {notFound} requested field name(s) not found on this category (check exact naming via GetSchedulableFields)." : "."));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create schedule — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with an action fragment below (e.g. action-place-schedule-on-sheet.cs), or add return sb.ToString(); to finish ----
