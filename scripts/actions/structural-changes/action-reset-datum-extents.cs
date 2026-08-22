// ============================================================
// FRAGMENT (action) — action-reset-datum-extents.cs
// PURPOSE: Put the GRIDS and LEVELS in `elements` back on their shared 3D (Model) extent, discarding
//          the per-view 2D override — the fix for "someone dragged a grid end and now it's short in
//          this view but not that one". Optionally across MANY views in one pass, not just one.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above. Anything in
//          `elements` that is not a Grid or a Level is ignored and counted.
//          To get every datum in a view: `filters/by-view-and-sheet/filter-by-active-view.cs` then
//          this; or collect Grids/Levels with a category filter.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/datums.md — read the 2D/3D section before running this.
//
// GOTCHA: THIS IS NOT A PER-VIEW CHANGE, WHATEVER IT LOOKS LIKE. Switching an end back to `Model`
//         discards that view's own override and hands the datum back to the SHARED extent — so the
//         line can move in the view you are looking at AND stop being independent everywhere else.
//         That is the point of the tool, but it means it is a bulk change: say how many datums and how
//         many views before running it for real.
// GOTCHA: DRY RUN BY DEFAULT for that reason.
// GOTCHA: extent type is set PER END and PER VIEW — there is no "reset this grid everywhere" call. Each
//         end is wrapped in its own try/catch because an end can refuse (owned by another user, or not
//         available in that view) and the right answer is to skip that end, not fail the batch.
// GOTCHA: a LEVEL is only a line in elevation/section/3D views — in a plan there is no extent to reset.
//         Grids are lines in plans too. With `allViews = true` this only counts a view where the datum
//         actually has a curve, so plan views simply do not appear in the level counts.
// NOTE: `Grid` and `Level` both derive from `DatumPlane`, so one loop handles both.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Reset Grid/Level Extents
//   to 3D, which does exactly this on real jobs. Run it on ONE grid in ONE view first and look at it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = report what would be reset; false = actually reset
bool allViews = false;           // false = the target view only; true = EVERY non-template view showing the datum
int? targetViewIdInt = null;     // null = active view (ignored when allViews = true)
// ---- END INPUTS ----

View singleView = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (!allViews && singleView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (Document.IsFamilyDocument)
{
    sb.AppendLine("This is the Family Editor — there are no project grids or levels here.");
}
else
{
    // Which datums did we actually get?
    var datums = new List<DatumPlane>();
    int notADatum = 0;
    foreach (var e in elements)
    {
        var d = e as DatumPlane;
        if (d != null && d.IsValidObject) datums.Add(d); else notADatum++;
    }

    if (datums.Count == 0)
    {
        sb.AppendLine($"No Grids or Levels in `elements` ({notADatum} other element(s) ignored) — nothing to do.");
    }
    else
    {
        // Which views to write.
        List<View> views;
        if (allViews)
        {
            views = new FilteredElementCollector(Document)
                .OfClass(typeof(View)).Cast<View>()
                .Where(v => v != null && !v.IsTemplate)
                .ToList();
        }
        else
        {
            views = new List<View> { singleView };
        }

        int endsReset = 0, datumsTouched = 0, endsRefused = 0;
        var viewsTouched = new HashSet<string>();
        var rows = new List<string>();

        using (var t = new Transaction(Document, "AJ Tools - Reset Datum Extents to 3D"))
        {
            t.Start();
            try
            {
                foreach (var datum in datums)
                {
                    bool thisDatumTouched = false;

                    foreach (var view in views)
                    {
                        // Does this view show this datum as a line at all? Empty is NORMAL, not an error —
                        // it is how you ask the question. A level in a plan answers "no".
                        bool hasCurveHere = false;
                        try
                        {
                            var mc = datum.GetCurvesInView(DatumExtentType.Model, view);
                            var vc = datum.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                            hasCurveHere = (mc != null && mc.Count > 0) || (vc != null && vc.Count > 0);
                        }
                        catch { }
                        if (!hasCurveHere) continue;

                        foreach (var end in new[] { DatumEnds.End0, DatumEnds.End1 })
                        {
                            try
                            {
                                if (!dryRun) datum.SetDatumExtentType(end, view, DatumExtentType.Model);
                                endsReset++;
                                thisDatumTouched = true;
                                viewsTouched.Add(view.Name);
                            }
                            catch
                            {
                                endsRefused++;   // this end will not move in this view — skip it, don't fail
                            }
                        }
                    }

                    if (thisDatumTouched)
                    {
                        datumsTouched++;
                        if (rows.Count < 25)
                            rows.Add(datum.Id + " | " + (datum is Grid ? "Grid" : "Level") + " | " + datum.Name);
                    }
                }

                if (dryRun) t.RollBack(); else t.Commit();
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
                return sb.ToString();
            }
        }

        sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
                      $"Reset to 3D (Model) extents: {datumsTouched} datum(s), {endsReset} end(s), " +
                      $"across {viewsTouched.Count} view(s)" +
                      (allViews ? " (every view that shows them)." : $" ('{singleView.Name}')."));
        if (endsRefused > 0) sb.AppendLine($"{endsRefused} datum end(s) refused and were skipped.");
        if (notADatum > 0) sb.AppendLine($"{notADatum} element(s) in the set were not Grids or Levels and were ignored.");

        if (rows.Count > 0)
        {
            sb.AppendLine("");
            sb.AppendLine("Id | Type | Name");
            sb.AppendLine("--- | --- | ---");
            foreach (var r in rows) sb.AppendLine(r);
            if (datumsTouched > rows.Count) sb.AppendLine("... and " + (datumsTouched - rows.Count) + " more");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
