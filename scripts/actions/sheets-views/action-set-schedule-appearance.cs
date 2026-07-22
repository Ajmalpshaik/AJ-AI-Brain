// ============================================================
// FRAGMENT (action) — action-set-schedule-appearance.cs
// PURPOSE: Set the two schedule-level appearance options this fragment has solid API confidence on —
//          "Itemize every instance" and the Grand Total row — across every ViewSchedule in `elements`. Any
//          input left null is not touched.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// HONESTY NOTE: Revit's Schedule Properties > Appearance tab has more settings than this (grid lines,
// outline style, title/header text formatting, blank row before data) — those aren't included here because
// their exact ScheduleDefinition/ViewSchedule API members aren't confidently known; guessing wrong there
// risks silently-wrong formatting rather than a loud failure. Ask for a specific one if it's needed and
// it can be researched and added properly, following this same file's naming pattern.
// CONDITIONAL FORMATTING is NOT covered by any fragment in this library yet, for the same reason — real
// API uncertainty, not an oversight. Flag if this is genuinely needed and it can be looked into properly.
// NOT YET LIVE-VERIFIED — test on one schedule first before trusting it on a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool? isItemized = null;       // true = "Itemize every instance", false = group identical rows; null = don't change
string grandTotal = null;      // "NoGrandTotal" | "Totals" | "TotalsAndCount"; null = don't change
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
            if (!string.IsNullOrEmpty(grandTotal) && Enum.TryParse<GrandTotal>(grandTotal, true, out var gt))
            {
                try { def.ShowGrandTotal = gt; grandTotalSet++; }
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
