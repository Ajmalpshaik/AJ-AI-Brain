// ============================================================
// FRAGMENT (filter) — filter-by-schedules.cs
// PURPOSE: Every ViewSchedule in the model, optionally narrowed by a name substring — feeds
//          actions/sheets-views/action-export-schedule-to-csv.cs or
//          actions/sheets-views/action-place-schedule-on-sheet.cs.
// PRODUCES: elements (List<Element>, each one really a ViewSchedule — cast back with `as ViewSchedule` in
//           the action), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Excludes revision/material-takeoff-only internal schedule types Revit sometimes creates for its own
// bookkeeping (IsTemplate schedules, and the internal "<Revision Schedule>"-style ones) by requiring a
// real, user-visible Name — adjust the filter below if a specific internal schedule genuinely is needed.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every schedule; else substring match, case-insensitive
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(ViewSchedule))
    .Cast<ViewSchedule>()
    .Where(s => !s.IsTemplate && !s.Name.StartsWith("<"))
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(s => s.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(s => s.Name).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} schedule(s)." + (string.IsNullOrEmpty(nameContains) ? "" : $" (name contains \"{nameContains}\")"));
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
