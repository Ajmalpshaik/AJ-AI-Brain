// ============================================================
// FRAGMENT (creator) — create-key-schedule.cs
// PURPOSE: Create a KEY schedule for a category — the lookup table where you define a few named keys
//          (e.g. room finish sets, equipment duty presets) and then every element picks one key and
//          inherits all its values at once. Different call from create-schedule.cs, which makes a normal
//          listing schedule.
// PRODUCES: elements (List<Element>, the new key schedule), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: creating the schedule creates the KEY PARAMETER on that category too (named by keyName below).
//         Every element of the category gains it immediately — that's the point, but it IS a model-wide
//         change, so say so before running it on a live project.
// GOTCHA: this creates the empty key table and its columns. Adding the actual key ROWS (the named
//         presets) means creating elements of the key schedule's own type, which is a separate job —
//         the user usually types those few rows in Revit faster than any script could.
// GOTCHA: only fields that belong to the category can be columns; unschedulable names are skipped and
//         REPORTED with the available list, same as create-sheet-list.cs.
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string categoryName = "Rooms";      // display name of the category the keys apply to
string scheduleName = "Room Finish Keys";
string keyName = "Finish Key";      // the key parameter added to every element of the category
string[] fieldNames = new[] { "Floor Finish", "Wall Finish", "Ceiling Finish" };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Category category = null;
foreach (Category c in Document.Settings.Categories)
{
    if (c.Name == categoryName) { category = c; break; }
}

if (category == null)
{
    sb.AppendLine($"Category '{categoryName}' not found — list them with context/context-model-categories.cs.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Key Schedule"))
    {
        t.Start();
        try
        {
            var schedule = ViewSchedule.CreateKeySchedule(Document, category.Id);
            try { schedule.Name = scheduleName; } catch { } // name collision — keeps generated name

            var definition = schedule.Definition;
            if (!string.IsNullOrEmpty(keyName))
            {
                // ScheduleDefinition.SetKeyName does NOT exist on Revit 2020. The try/catch around it
                // was worthless — a missing method is a COMPILE error (CS1061), not a runtime one, so
                // this fragment never built. The 2020 route is the settable property on the schedule
                // itself. Caught by tools\verify-fragments-compile.ps1 on 2026-08-04.
                try { schedule.KeyScheduleParameterName = keyName; }
                catch (Exception exKey) { sb.AppendLine($"  Key parameter NOT renamed to '{keyName}': {exKey.Message}"); }
            }

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

            elements.Add(schedule);
            t.Commit();
            sb.AppendLine($"Created key schedule '{schedule.Name}' (Id {schedule.Id}) for {categoryName}, key parameter '{keyName}', {added.Count} column(s): {string.Join(", ", added)}.");
            sb.AppendLine($"  Every {categoryName} element now has a '{keyName}' parameter — that is a model-wide change (see header).");
            if (missing.Count > 0)
            {
                sb.AppendLine($"  NOT added (not schedulable for {categoryName}): {string.Join(", ", missing)}");
                sb.AppendLine($"  Available: {string.Join(", ", schedulable.Select(f => f.GetName(Document)).OrderBy(n => n).Take(40))}");
            }
            sb.AppendLine("  Next: type the key rows in Revit (faster than scripting them), then set each element's key via action-set-parameter-value.cs.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create key schedule — rolled back, nothing created. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
