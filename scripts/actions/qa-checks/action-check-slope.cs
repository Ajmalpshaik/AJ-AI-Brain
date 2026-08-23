// ============================================================
// FRAGMENT (action) — action-check-slope.cs
// PURPOSE: Measure the real fall on every linear run in `elements` and flag anything outside the allowed
//          range — drainage that is too flat to self-clean, drainage that is too steep (the solids get
//          left behind), condensate that runs the wrong way, and duct that should be dead level and
//          isn't. Reports the fall four ways at once (1:X, %, mm per m, and total drop over the run)
//          because a drainage rule is quoted in whichever of those the specification felt like using.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — pipes,
//          ducts, conduit or cable tray. filter-by-system-type.cs narrowed to the drainage system is the
//          usual feeder; a mixed set is fine, the report groups by category.
// Read-only. The model never changes — action-set-pipe-slope.cs is the fix.
//
// ✱✱ ONE FRAGMENT FOR PIPE AND DUCT ON PURPOSE. Slope is (rise / horizontal run) whatever the service is;
//    splitting it into two files would have meant the same arithmetic twice and two places for it to be
//    wrong. What differs is the RULE, and that is an input.
//
// ✱✱ MEASURED FROM THE GEOMETRY, NOT READ FROM THE PARAMETER, and the two are cross-checked. Revit does
//    carry a slope parameter on a pipe, but it is only meaningful when Revit itself sloped the run — a
//    pipe whose ends were dragged to different heights by hand can read 0 in the parameter and have a
//    real fall on it. The geometry is the truth here; where the parameter exists and DISAGREES by more
//    than a rounding step, the row is flagged PARAM MISMATCH, because that disagreement is itself worth
//    knowing about before anyone schedules off the parameter.
//
// ✱✱ DIRECTION IS REPORTED, NOT JUST MAGNITUDE. "Falls toward the start" vs "falls toward the end" is
//    what tells you a branch runs the wrong way, and a magnitude-only check passes that happily. The
//    fall direction is given as the end that is LOWER, with its coordinates, so it can be read against
//    the drawing without opening a section.
//
// GOTCHA: A VERTICAL RUN HAS NO SLOPE — it is a stack, and dividing by a horizontal run of zero is how
//         a check like this produces nonsense. Runs whose horizontal projection is under `minRunMm` are
//         classed VERTICAL and reported separately, never as "infinitely steep" and never as a failure.
// GOTCHA: FITTINGS ARE NOT RUNS. An elbow or a tee has no LocationCurve, so it is counted as "not a
//         linear run" and skipped. A drainage line's real fall lives in its straight lengths; the
//         fittings follow them.
// GOTCHA: the rule is a RANGE, and the maximum matters. A drain at 1:10 fails a real specification just
//         as a drain at 1:200 does. `maxRatio = 0` switches the upper bound off if you genuinely only
//         care about the minimum.
// GOTCHA: 1:X notation gets LARGER as the pipe gets FLATTER. 1:100 is steeper than 1:200. The report
//         prints the percentage beside it so the direction of "worse" is never ambiguous.
// RELATED: action-set-pipe-slope.cs (fix it), action-check-drainage-connectivity via
//          action-check-plumbing-fixture-connectivity.cs (is it even joined up),
//          action-report-length-by-size.cs (how much of each size there is).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one reported fall against a spot
//   elevation in Revit before trusting a whole-building sweep.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double minRatio = 100;        // shallowest allowed, as the X in 1:X (1:100). 0 = no minimum
double maxRatio = 40;         // steepest allowed, as the X in 1:X (1:40).  0 = no maximum
double minRunMm = 300;        // horizontal run below this is treated as a vertical/stub, not sloped
double levelToleranceMm = 2;  // fall under this over the whole run counts as "level", not as a fault
bool expectLevel = false;     // true = the opposite check: flag anything that ISN'T level (duct)
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the runs you want checked).");
    return sb.ToString();
}

// ---- measure ----
var rows = new List<(Element El, double RunMm, double DropMm, double Ratio, double Percent, string LowEnd, string Verdict, bool ParamMismatch)>();
int vertical = 0, notLinear = 0;

foreach (var el in elements)
{
    var lc = el.Location as LocationCurve;
    if (lc == null || lc.Curve == null) { notLinear++; continue; }

    var p0 = lc.Curve.GetEndPoint(0);
    var p1 = lc.Curve.GetEndPoint(1);

    double runMm = ToMm(Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2)));
    double dropMm = ToMm(Math.Abs(p1.Z - p0.Z));

    if (runMm < minRunMm) { vertical++; continue; }

    double ratio = dropMm > 0.0001 ? runMm / dropMm : double.PositiveInfinity;   // the X in 1:X
    double percent = runMm > 0 ? (dropMm / runMm) * 100.0 : 0;

    string lowEnd;
    if (Math.Abs(p1.Z - p0.Z) <= (levelToleranceMm / MM_PER_FOOT)) lowEnd = "level";
    else if (p0.Z < p1.Z) lowEnd = $"falls to START ({ToMm(p0.X):F0}, {ToMm(p0.Y):F0}, {ToMm(p0.Z):F0})";
    else lowEnd = $"falls to END ({ToMm(p1.X):F0}, {ToMm(p1.Y):F0}, {ToMm(p1.Z):F0})";

    // Verdict
    string verdict;
    if (expectLevel)
    {
        verdict = dropMm <= levelToleranceMm ? "OK" : "NOT LEVEL";
    }
    else if (dropMm <= levelToleranceMm)
    {
        verdict = minRatio > 0 ? "FLAT — no fall at all" : "OK";
    }
    else
    {
        verdict = "OK";
        if (minRatio > 0 && ratio > minRatio) verdict = $"TOO FLAT (1:{ratio:F0}, needs 1:{minRatio:F0} or steeper)";
        else if (maxRatio > 0 && ratio < maxRatio) verdict = $"TOO STEEP (1:{ratio:F0}, limit 1:{maxRatio:F0})";
    }

    // Cross-check Revit's own slope parameter where it exists.
    bool mismatch = false;
    var sp = el.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE);
    if (sp != null && sp.HasValue)
    {
        try
        {
            // The parameter is a rise/run ratio in internal units — dimensionless, so it compares
            // directly against the measured drop/run.
            double paramSlope = sp.AsDouble();
            double measuredSlope = runMm > 0 ? dropMm / runMm : 0;
            if (Math.Abs(paramSlope - measuredSlope) > 0.002) mismatch = true;
        }
        catch { }
    }

    rows.Add((el, runMm, dropMm, ratio, percent, lowEnd, verdict, mismatch));
}

// ---- report ----
sb.AppendLine(expectLevel
    ? $"LEVEL CHECK — anything with more than {levelToleranceMm:F0} mm of fall is flagged"
    : $"SLOPE CHECK — allowed range " + (minRatio > 0 ? $"1:{minRatio:F0} (flattest)" : "no minimum") + " to " + (maxRatio > 0 ? $"1:{maxRatio:F0} (steepest)" : "no maximum"));
sb.AppendLine($"Linear runs measured: {rows.Count}" +
              (vertical > 0 ? $"   vertical/stub (under {minRunMm:F0} mm horizontal, not sloped): {vertical}" : "") +
              (notLinear > 0 ? $"   fittings/non-linear skipped: {notLinear}" : ""));

var bad = rows.Where(r => r.Verdict != "OK").ToList();
var mismatches = rows.Where(r => r.ParamMismatch).ToList();

sb.AppendLine($"OUTSIDE THE RULE: {bad.Count}   of {rows.Count}");
sb.AppendLine();

if (rows.Count == 0)
{
    sb.AppendLine("Nothing measurable in the set — every element was a fitting, or shorter than minRunMm.");
    return sb.ToString();
}

if (bad.Count == 0)
{
    sb.AppendLine("CLEAR — every run is inside the allowed range.");
}
else
{
    sb.AppendLine("| Element | Category | Run mm | Drop mm | Slope | % | Direction | Verdict |");
    sb.AppendLine("|---|---|---|---|---|---|---|---|");
    foreach (var r in bad.OrderByDescending(r => r.Ratio).Take(maxReportedRows))
    {
        string slopeTxt = double.IsInfinity(r.Ratio) ? "level" : $"1:{r.Ratio:F0}";
        sb.AppendLine($"| {r.El.Id} | {r.El.Category?.Name ?? "-"} | {r.RunMm:F0} | {r.DropMm:F1} | {slopeTxt} | {r.Percent:F2} | {r.LowEnd} | {r.Verdict} |");
    }
    if (bad.Count > maxReportedRows)
        sb.AppendLine($"\n... and {bad.Count - maxReportedRows} more (raise maxReportedRows to see them).");
}

// Totals worth having whether or not anything failed.
var sloped = rows.Where(r => !double.IsInfinity(r.Ratio)).ToList();
if (sloped.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"Range across the set: flattest 1:{sloped.Max(r => r.Ratio):F0}, steepest 1:{sloped.Min(r => r.Ratio):F0}, total fall {sloped.Sum(r => r.DropMm):F0} mm over {sloped.Sum(r => r.RunMm) / 1000.0:F1} m of run.");
}
int levelCount = rows.Count(r => r.DropMm <= levelToleranceMm);
if (levelCount > 0) sb.AppendLine($"Dead level (within {levelToleranceMm:F0} mm): {levelCount} run(s).");

if (mismatches.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"PARAM MISMATCH ({mismatches.Count}) — the Slope parameter disagrees with the measured geometry. Schedules read the parameter; site builds the geometry:");
    foreach (var m in mismatches.Take(20))
        sb.AppendLine($"  {m.El.Id}: measured 1:{m.Ratio:F0} ({m.Percent:F2}%)");
    if (mismatches.Count > 20) sb.AppendLine($"  ... and {mismatches.Count - 20} more");
}

return sb.ToString();
