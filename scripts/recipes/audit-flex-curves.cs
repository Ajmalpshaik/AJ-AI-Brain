// ============================================================
// FRAGMENT (recipe) — audit-flex-curves.cs
// PURPOSE: Audit every FLEX DUCT and FLEX PIPE in the model (or one view): size, how long each run
//          really is, how much slack it is carrying, whether both ends are actually connected, and
//          which runs exceed the length you allow. Standalone — no filter needed. Read-only.
//
// WHY THIS EXISTS: the library had NOTHING that treats a flex curve as a flex curve (checked
//          2026-08-20). `FlexDuct`/`FlexPipe` appeared in exactly five fragments and in all five it was
//          only the CATEGORY NAME inside a list — obstruction sweeps, grayout, a multi-category filter.
//          Nothing measured one, connected one, or checked one.
//
// THE TWO THINGS THIS CATCHES, both of which are silent in Revit:
//   1. SLACK. A flex run's Location is a spline, so its real length is longer than the straight line
//      between its ends. Revit shows the real length and says nothing about the difference. A run
//      sagging 40% over the direct distance is wasted static pressure on site and nobody sees it in plan.
//   2. AN OPEN END. A flex duct drawn to a diffuser but not connected looks correct in every view. This
//      reports the connector truth, not the drawing.
//
// GOTCHA — WHY IT COLLECTS BY CATEGORY, NOT BY TYPE: `FlexDuct` lives in
//          `Autodesk.Revit.DB.Mechanical` and `FlexPipe` in `Autodesk.Revit.DB.Plumbing`, and
//          ../../knowledge/live-model/core.md records that MEP sub-namespace types must be FULLY
//          QUALIFIED in this bridge's script context or they fail to compile. Collecting by
//          `OST_FlexDuctCurves` / `OST_FlexPipeCurves` and casting to `MEPCurve` (which is plain
//          `Autodesk.Revit.DB`) avoids naming either type and works on every Revit version.
//
// GOTCHA — LENGTH IS READ FROM THE CURVE, not from a `.Points` property. Curve.Length,
//          Curve.GetEndPoint and Curve.Tessellate exist on every version this library targets;
//          flex-specific members do not need to be named at all, so nothing here can fail to compile on
//          an older or newer Revit.
//
// GOTCHA — `Connector.IsConnected` can be true with no physical partner (proven wrong in real sessions,
//          see hvac-ducts.md). This counts REFERENCES, not the flag.
//
// NOT YET LIVE-VERIFIED — written 2026-08-20 without Revit. Run it on a model that has flex before
//          trusting the numbers; the reads it uses are the same ones action-report-connectors.cs uses.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double maxFlexLengthMm = 0;        // 0 = do not flag on length. Set the limit your PROJECT allows —
                                   // this is a spec/code decision per job, not a number to inherit.
int activeViewOnly = 0;            // 1 = only what is visible in the active view, 0 = whole model
double slackWarnPercent = 25;      // flag a run whose real length exceeds the straight line by this much
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;

var flex = new List<Element>();
foreach (var bic in new[] { BuiltInCategory.OST_FlexDuctCurves, BuiltInCategory.OST_FlexPipeCurves })
{
    var col = activeViewOnly == 1
        ? new FilteredElementCollector(Document, Document.ActiveView.Id)
        : new FilteredElementCollector(Document);
    flex.AddRange(col.OfCategory(bic).WhereElementIsNotElementType().ToList());
}

if (flex.Count == 0)
{
    return activeViewOnly == 1
        ? "No flex duct or flex pipe visible in the active view."
        : "No flex duct or flex pipe in this model.";
}

Func<Element, string> sizeOf = e =>
{
    var d = e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
    if (d != null && d.HasValue) return $"{toMm(d.AsDouble()):F0}ø";
    var w = e.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
    var h = e.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
    if (w != null && h != null && w.HasValue && h.HasValue)
        return $"{toMm(w.AsDouble()):F0}x{toMm(h.AsDouble()):F0}";
    return "unknown";
};

int overLength = 0, slack = 0, openEnds = 0, unreadable = 0;
double totalMm = 0;
var rows = new List<string>();

foreach (var e in flex.OrderBy(x => x.Category?.Name).ThenBy(x => x.Id.ToString()))
{
    try
    {
        var lc = e.Location as LocationCurve;
        if (lc == null || lc.Curve == null) { unreadable++; continue; }

        double realMm = toMm(lc.Curve.Length);
        var a = lc.Curve.GetEndPoint(0);
        var b = lc.Curve.GetEndPoint(1);
        double directMm = toMm(a.DistanceTo(b));
        totalMm += realMm;

        // How much longer the routed run is than the direct line — the slack the drawing hides.
        double extraPct = directMm > 1 ? (realMm - directMm) / directMm * 100.0 : 0;
        int bends = 0;
        try { bends = Math.Max(0, lc.Curve.Tessellate().Count - 2); } catch { }

        // Connector truth: count real references, never trust IsConnected on its own.
        int ends = 0, joined = 0;
        var cm = (e as MEPCurve)?.ConnectorManager;
        if (cm != null)
            foreach (Connector c in cm.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End) continue;
                ends++;
                try { if (c.AllRefs != null && c.AllRefs.Size > 0) joined++; } catch { }
            }
        bool open = ends > 0 && joined < ends;

        var flags = new List<string>();
        if (maxFlexLengthMm > 0 && realMm > maxFlexLengthMm) { flags.Add($"OVER LIMIT by {realMm - maxFlexLengthMm:F0}mm"); overLength++; }
        if (extraPct > slackWarnPercent) { flags.Add($"SLACK {extraPct:F0}%"); slack++; }
        if (open) { flags.Add($"OPEN END ({joined}/{ends} joined)"); openEnds++; }

        string kind = (e.Category?.Name ?? "flex").Replace(" Curves", "");
        rows.Add($"  {kind,-12} Id {e.Id,-10} {sizeOf(e),-12} "
               + $"length {realMm:F0}mm (direct {directMm:F0}mm, {bends} bend pts)"
               + (flags.Count > 0 ? "   <- " + string.Join(", ", flags) : ""));
    }
    catch (Exception ex) { unreadable++; rows.Add($"  Id {e.Id}: could not read — {ex.Message}"); }
}

sb.AppendLine($"Flex duct / flex pipe audit — {flex.Count} run(s)"
            + (activeViewOnly == 1 ? $" visible in '{Document.ActiveView.Name}'" : " in the whole model"));
sb.AppendLine($"  total flex length : {totalMm:F0}mm ({totalMm / 1000.0:F1}m)");
sb.AppendLine($"  average per run   : {totalMm / Math.Max(flex.Count - unreadable, 1):F0}mm");
sb.AppendLine(maxFlexLengthMm > 0
    ? $"  over {maxFlexLengthMm:F0}mm limit : {overLength}"
    : "  length limit      : not checked (maxFlexLengthMm = 0 — set your project's allowance)");
sb.AppendLine($"  carrying slack    : {slack}  (real length more than {slackWarnPercent:F0}% over the direct line)");
sb.AppendLine($"  with an open end  : {openEnds}  <- these look connected in every view");
if (unreadable > 0) sb.AppendLine($"  unreadable        : {unreadable}");
sb.AppendLine();
foreach (var r in rows) sb.AppendLine(r);

return sb.ToString();
