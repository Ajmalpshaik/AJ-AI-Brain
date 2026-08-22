// ============================================================
// FRAGMENT (action) — action-set-datum-bubbles.cs
// PURPOSE: Show, hide or FLIP the bubble (the head with the grid/level name in it) on the GRIDS and
//          LEVELS in `elements`, in one view — "put the grid bubbles on the other side", "turn the
//          bubbles on at the left end", "the level heads are on the wrong side of the section".
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above. Anything that is
//          not a Grid or a Level is ignored and counted.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/datums.md
//
// GOTCHA: bubbles are PER END and PER VIEW. This changes one view only — that is correct behaviour for
//         bubbles (unlike extents, where `Model` type is shared: see action-reset-datum-extents.cs).
// GOTCHA: "FLIP" IS NOT ONE CALL. Revit has only Is/Show/Hide per end, so a flip is read-both-ends then
//         hide one and show the other. Two cases have no obvious flip and are handled explicitly rather
//         than silently doing nothing:
//           - NEITHER end visible -> show End0 (something has to appear, or "flip" looks broken)
//           - BOTH ends visible   -> hide End1 (leaving one, which is what "flipped" should mean)
// GOTCHA: End0 and End1 are the datum's OWN two ends, not "left" and "right". Which one looks left
//         depends on the direction the grid was drawn in and on the view. If a batch flips half of them
//         the way you wanted and half the other way, the datums were drawn in opposite directions —
//         that is the model, not this fragment. Use mode "end0"/"end1" to force a consistent side.
// GOTCHA: bubbles only mean anything in plan, ceiling plan, engineering plan, area plan, section and
//         elevation views. In a 3D view the calls are skipped and counted.
// NOTE: dry-run by default, because a bulk bubble change is hard to eyeball afterwards.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Flip Grid/Level Bubbles.
//   Run it on ONE grid first and look at which end moved.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "flip";            // "flip" | "end0" | "end1" | "both" | "none"
bool dryRun = true;              // true = report only; false = actually change
int? targetViewIdInt = null;     // null = active view
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (view.IsTemplate)
{
    sb.AppendLine($"'{view.Name}' is a view template — bubbles are set on real views.");
}
else
{
    bool viewTypeOk =
        view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan ||
        view.ViewType == ViewType.EngineeringPlan || view.ViewType == ViewType.AreaPlan ||
        view.ViewType == ViewType.Section || view.ViewType == ViewType.Elevation;

    if (!viewTypeOk)
    {
        sb.AppendLine($"'{view.Name}' is a {view.ViewType} view — datum bubbles only apply in plan, " +
                      "ceiling plan, engineering plan, area plan, section or elevation views. Nothing changed.");
    }
    else
    {
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
            int changed = 0, refused = 0, noCurveHere = 0;
            var rows = new List<string>();

            using (var t = new Transaction(Document, "AJ Tools - Set Datum Bubbles"))
            {
                t.Start();
                try
                {
                    foreach (var datum in datums)
                    {
                        // Is this datum a line in this view at all? Empty is normal (a level in a plan).
                        bool hasCurveHere = false;
                        try
                        {
                            var mc = datum.GetCurvesInView(DatumExtentType.Model, view);
                            var vc = datum.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                            hasCurveHere = (mc != null && mc.Count > 0) || (vc != null && vc.Count > 0);
                        }
                        catch { }
                        if (!hasCurveHere) { noCurveHere++; continue; }

                        bool e0 = false, e1 = false;
                        try
                        {
                            e0 = datum.IsBubbleVisibleInView(DatumEnds.End0, view);
                            e1 = datum.IsBubbleVisibleInView(DatumEnds.End1, view);
                        }
                        catch { refused++; continue; }

                        bool wantE0, wantE1;
                        switch (mode.ToLowerInvariant())
                        {
                            case "end0": wantE0 = true;  wantE1 = false; break;
                            case "end1": wantE0 = false; wantE1 = true;  break;
                            case "both": wantE0 = true;  wantE1 = true;  break;
                            case "none": wantE0 = false; wantE1 = false; break;
                            default:     // "flip"
                                if (e0 && !e1)      { wantE0 = false; wantE1 = true;  }
                                else if (e1 && !e0) { wantE0 = true;  wantE1 = false; }
                                else if (!e0 && !e1){ wantE0 = true;  wantE1 = false; }  // neither -> show one
                                else                { wantE0 = true;  wantE1 = false; }  // both -> leave one
                                break;
                        }

                        if (wantE0 == e0 && wantE1 == e1) continue;   // already how it was asked for

                        try
                        {
                            if (!dryRun)
                            {
                                if (wantE0) datum.ShowBubbleInView(DatumEnds.End0, view);
                                else        datum.HideBubbleInView(DatumEnds.End0, view);
                                if (wantE1) datum.ShowBubbleInView(DatumEnds.End1, view);
                                else        datum.HideBubbleInView(DatumEnds.End1, view);
                            }
                            changed++;
                            if (rows.Count < 25)
                                rows.Add(datum.Id + " | " + (datum is Grid ? "Grid" : "Level") + " | " + datum.Name +
                                         " | " + (e0 ? "End0" : "") + (e1 ? " End1" : "") + " -> " +
                                         (wantE0 ? "End0" : "") + (wantE1 ? " End1" : "") +
                                         ((!wantE0 && !wantE1) ? "(none)" : ""));
                        }
                        catch { refused++; }
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
                          $"Bubble mode '{mode}' in view '{view.Name}': {changed} datum(s) changed of {datums.Count}.");
            if (noCurveHere > 0) sb.AppendLine($"{noCurveHere} datum(s) are not drawn in this view (a Level has no line in a plan) — skipped.");
            if (refused > 0) sb.AppendLine($"{refused} datum(s) refused the change and were skipped.");
            if (notADatum > 0) sb.AppendLine($"{notADatum} element(s) in the set were not Grids or Levels and were ignored.");

            if (rows.Count > 0)
            {
                sb.AppendLine("");
                sb.AppendLine("Id | Type | Name | Was -> Now");
                sb.AppendLine("--- | --- | --- | ---");
                foreach (var r in rows) sb.AppendLine(r);
                if (changed > rows.Count) sb.AppendLine("... and " + (changed - rows.Count) + " more");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
