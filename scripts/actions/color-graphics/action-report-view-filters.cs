// ============================================================
// FRAGMENT (action) — action-report-view-filters.cs
// PURPOSE: List every filter (View Filter AND Selection Filter — the shared FilterElement base) in the
//          whole document, and which real views currently have each one applied. Read-only. Answers "what
//          filters already exist" before creating a near-duplicate, and "where is this filter used"
//          before editing or deleting one.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// LIVE-VERIFIED 2026-07-22 — FOUND AND FIXED A REAL BUG: `allRealViews` excluded IsTemplate/Schedule/
// DrawingSheet but not the internal pseudo-view-types Revit's own FilteredElementCollector still returns
// alongside real views — specifically ViewType.ProjectBrowser (confirmed live: View.IsFilterApplied throws
// "The view type does not support Visibility/Graphics Overriddes" on it; ViewType.SystemBrowser happened
// to not throw in this test but isn't a real graphics-capable view either and shouldn't be trusted to
// stay that way). Fixed by wrapping the IsFilterApplied probe per-view in a try/catch so one
// non-graphics-capable pseudo-view can't crash the whole report.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every filter; else substring match on the filter's Name
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var filters = new FilteredElementCollector(Document).OfClass(typeof(FilterElement))
    .Cast<FilterElement>()
    .Where(f => string.IsNullOrEmpty(nameContains) || f.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
    .OrderBy(f => f.Name)
    .ToList();

var allRealViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.DrawingSheet)
    .ToList();

sb.AppendLine($"{filters.Count} filter(s) in the document" + (string.IsNullOrEmpty(nameContains) ? "" : $" (name contains \"{nameContains}\")") + ":");

foreach (var f in filters)
{
    string kind = f is ParameterFilterElement ? "View Filter (rule-based)" : f is SelectionFilterElement ? "Selection Filter (explicit list)" : f.GetType().Name;
    var usedInViews = allRealViews.Where(v => { try { return v.IsFilterApplied(f.Id); } catch { return false; } }).Select(v => v.Name).ToList();
    string usageLabel = usedInViews.Count > 0 ? string.Join(", ", usedInViews) : "(not applied to any view)";
    sb.AppendLine($"  '{f.Name}' (Id {f.Id}) — {kind} — used in: {usageLabel}");
}
return sb.ToString();
