// ============================================================
// FRAGMENT (action) — action-set-schedule-appearance.cs
// PURPOSE: Set the two schedule-level appearance options this fragment has solid API confidence on —
//          "Itemize every instance" and the Grand Total row — across every ViewSchedule in `elements`. Any
//          input left null is not touched.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/by-view-and-sheet/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// HONESTY NOTE: Revit's Schedule Properties > Appearance tab has more settings than this (grid lines,
// outline style, title/header text formatting, blank row before data) — those aren't included here because
// their exact ScheduleDefinition/ViewSchedule API members aren't confidently known; guessing wrong there
// risks silently-wrong formatting rather than a loud failure. Ask for a specific one if it's needed and
// it can be researched and added properly, following this same file's naming pattern.
// CONDITIONAL FORMATTING is NOT covered by any fragment in this library yet, for the same reason — real
// API uncertainty, not an oversight. Flag if this is genuinely needed and it can be looked into properly.
// LIVE-VERIFIED 2026-07-22 — FOUND AND FIXED A REAL BUG: `ScheduleDefinition.ShowGrandTotal` is a plain
// `bool` on this Revit version (confirmed via reflection: property type is `System.Boolean`) — the
// `GrandTotal` enum (with NoGrandTotal/Totals/TotalsAndCount values) the original code tried to parse
// against doesn't exist here at all, so `Enum.TryParse<GrandTotal>` was a compile error. Fixed to a plain
// bool input. This also means the Totals-vs-TotalsAndCount distinction genuinely isn't available via this
// property on Revit 2020 — it's on/off only here. Re-verified: IsItemized and ShowGrandTotal both set and
// read back correctly.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool? isItemized = null;       // true = "Itemize every instance", false = group identical rows; null = don't change
bool? showGrandTotal = null;   // true = show Grand Total row, false = hide it; null = don't change (Revit 2020: on/off only, no Totals-vs-TotalsAndCount distinction via this API)
// ---- END INPUTS ----

int itemizedSet = 0, grandTotalSet = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Schedule Appearance"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var schedule = el as ViewSchedule;
            if (schedule == null) { skipped++; continue; }

            var def = schedule.Definition;
            if (isItemized.HasValue)
            {
                try { def.IsItemized = isItemized.Value; itemizedSet++; }
                catch (Exception ex) { failures.Add($"'{schedule.Name}' IsItemized: {ex.Message}"); }
            }
            if (showGrandTotal.HasValue)
            {
                try { def.ShowGrandTotal = showGrandTotal.Value; grandTotalSet++; }
                catch (Exception ex) { failures.Add($"'{schedule.Name}' ShowGrandTotal: {ex.Message}"); }
            }
        }
        t.Commit();
        sb.AppendLine($"IsItemized set on {itemizedSet}, Grand Total set on {grandTotalSet} schedule(s), {skipped} non-ViewSchedule element(s) skipped.");
        if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set schedule appearance — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
