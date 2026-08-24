// ============================================================
// FRAGMENT (action) — action-auto-size-duct.cs
// PURPOSE: Size the ducts in `elements` from the airflow they actually carry — velocity method: required
//          area = flow / target velocity, snapped UP to the next real manufactured size, round or
//          rectangular. Reports the resulting velocity and aspect ratio for every duct so an undersized
//          or a silly 6:1 flat duct is visible before it is written.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the ducts
//          to size, usually one system at a time via filter-by-system-type.cs.
//
// ✱✱ THE VELOCITY IS YOURS AND THERE IS NO DEFAULT WORTH TRUSTING (START-HERE.md rule 3). A main at
//    7 m/s and a branch at 4 m/s are both normal and they give completely different ducts for the same
//    flow. Size ONE system at a time with the velocity that system is designed to, rather than sweeping
//    a whole model with one figure. This fragment does not know which duct is a main.
//
// ✱✱ IT READS THE FLOW REVIT ALREADY HAS — it does not calculate load. `RBS_DUCT_FLOW_PARAM` is filled by
//    Revit from the terminals downstream once the system is connected. A duct that is NOT connected to
//    anything carries flow 0, and a flow of 0 sizes to nothing — so zero-flow ducts are reported as
//    SKIPPED (NO FLOW), never sized to the smallest size in the list. If a whole run comes back with no
//    flow, the system is not connected: that is action-check-system-connectivity.cs, not a sizing problem.
//
// ✱✱ THE UNIT ASSUMPTION IS PRINTED, SO A WRONG ONE CANNOT HIDE. Revit's internal airflow unit is cubic
//    feet per second, and the conversion below is plain arithmetic (no version-specific unit API, per
//    the library's version-proof rule). The report prints, for the first few ducts, the raw internal
//    value NEXT TO Revit's own display string — if the constant were wrong those two would disagree
//    obviously on the very first run. Check that line before trusting a single size.
//
// ✱✱ IT ALWAYS ROUNDS UP, NEVER TO NEAREST. Rounding a duct DOWN to the nearer standard size raises the
//    velocity above the design figure, which is the one direction that causes noise complaints and
//    re-work. The next size up is chosen and the actual resulting velocity is reported per duct.
//
// GOTCHA: DRY RUN BY DEFAULT. The whole sizing table prints first — read it, then set dryRun = false.
// GOTCHA: ROUND vs RECTANGULAR IS A PROPERTY OF THE DUCT TYPE, not a choice made here. Setting a
//         diameter on a rectangular type is refused by Revit. The shape is detected per duct from which
//         size parameters it actually has, and each duct is sized in its own shape; a duct whose shape
//         cannot be read is reported, not guessed at.
// GOTCHA: RESIZING A CONNECTED DUCT makes Revit re-fit its neighbours, and a fitting that cannot adapt
//         raises a warning or breaks. Warnings are non-modal here so the batch cannot stall on a dialog,
//         but re-check connectivity afterwards — the count below is what was written, not what survived.
// GOTCHA: THIS SIZES SEGMENTS, NOT A SYSTEM. It does not reduce the trunk progressively as branches take
//         air off it — each duct is sized to its own flow. For a trunk that steps down at each takeoff,
//         slice it first with recipes/slice-trunk-for-sizing.cs so each segment carries its own flow.
// RELATED: action-auto-size-pipe.cs (same method, liquid), action-report-mep-pressure-drop.cs (what the
//          sizes cost in pressure), recipes/slice-trunk-for-sizing.cs (segment a trunk first),
//          action-report-length-by-size.cs (the takeoff afterwards).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check the unit sanity line, size ONE duct,
//   compare it against a hand calculation, then do a system.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = print the sizing table only, change nothing
double targetVelocityMs = 5.0;       // design velocity for THIS system, m/s

// Rectangular ducts: hold one dimension and solve the other, so the duct still fits the void it is in.
string rectStrategy = "holdHeight";  // "holdHeight" = keep the current height, widen; "holdWidth" = keep width, deepen
double maxAspectRatio = 4.0;         // refuse a rectangular result flatter than this (w:h or h:w)

// Standard sizes, mm. Snapped UP to the first entry that is big enough.
var roundSizes = new List<double> { 100, 125, 150, 160, 200, 250, 300, 315, 350, 400, 450, 500, 560, 600, 630, 710, 800, 900, 1000, 1120, 1250 };
var rectSizes  = new List<double> { 100, 150, 200, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800, 900, 1000, 1100, 1200, 1400, 1600, 1800, 2000 };

int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
const double CUFT_PER_SEC_TO_CUM_PER_SEC = 0.0283168466;   // Revit's internal airflow unit is ft3/s
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the ducts to size).");
    return sb.ToString();
}
if (targetVelocityMs <= 0)
{
    sb.AppendLine("STOP: targetVelocityMs must be greater than 0.");
    return sb.ToString();
}

Func<List<double>, double, double> snapUp = (list, needMm) =>
{
    foreach (var s in list.OrderBy(x => x)) if (s >= needMm - 0.001) return s;
    return -1;   // bigger than anything in the list
};

sb.AppendLine($"DUCT SIZING — velocity method at {targetVelocityMs:F2} m/s, sizes rounded UP to the next standard");
sb.AppendLine();

// ---- unit sanity: print internal vs Revit's own display for the first few, so a bad constant is obvious ----
sb.AppendLine("UNIT CHECK (read this before trusting any size below) — internal value x 0.0283168466 should equal Revit's own display:");
int shown = 0;
foreach (var el in elements)
{
    var fp = el.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
    if (fp == null || !fp.HasValue) continue;
    double raw = fp.AsDouble();
    string disp = "";
    try { disp = fp.AsValueString(); } catch { }
    sb.AppendLine($"  {el.Id}: internal {raw:F6} -> {(raw * CUFT_PER_SEC_TO_CUM_PER_SEC * 1000.0):F1} L/s   |   Revit shows: {disp}");
    if (++shown >= 3) break;
}
if (shown == 0) sb.AppendLine("  (no duct in the set carries a readable Flow parameter — see the NO FLOW note below)");
sb.AppendLine();

// ---- size each duct ----
var rows = new List<(Element El, double FlowLs, string Shape, string Was, string Now, double VelMs, double Aspect, string Note, double NewA, double NewB)>();
var noFlow = new List<ElementId>();
var problems = new List<string>();

foreach (var el in elements)
{
    var fp = el.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
    if (fp == null || !fp.HasValue) { noFlow.Add(el.Id); continue; }

    double flowM3s = fp.AsDouble() * CUFT_PER_SEC_TO_CUM_PER_SEC;
    if (flowM3s <= 1e-9) { noFlow.Add(el.Id); continue; }

    double requiredAreaM2 = flowM3s / targetVelocityMs;

    var dp = el.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
    var wp = el.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
    var hp = el.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

    bool isRound = dp != null && dp.HasValue && (wp == null || !wp.HasValue);

    if (isRound)
    {
        double needMm = Math.Sqrt(4.0 * requiredAreaM2 / Math.PI) * 1000.0;
        double pick = snapUp(roundSizes, needMm);
        if (pick < 0) { problems.Add($"{el.Id}: needs {needMm:F0} mm dia — bigger than the largest size in roundSizes"); continue; }

        double actualAreaM2 = Math.PI * Math.Pow(pick / 1000.0, 2) / 4.0;
        double vel = flowM3s / actualAreaM2;
        rows.Add((el, flowM3s * 1000.0, "round", $"{ToMm(dp.AsDouble()):F0}", $"{pick:F0}", vel, 1.0, "", pick, 0));
    }
    else if (wp != null && hp != null && wp.HasValue && hp.HasValue)
    {
        double curW = ToMm(wp.AsDouble()), curH = ToMm(hp.AsDouble());
        double needAreaMm2 = requiredAreaM2 * 1e6;

        double newW = curW, newH = curH;
        if (rectStrategy.Trim().ToLower() == "holdwidth")
        {
            double needH = needAreaMm2 / Math.Max(curW, 1);
            newH = snapUp(rectSizes, needH);
            if (newH < 0) { problems.Add($"{el.Id}: needs {needH:F0} mm height at {curW:F0} wide — bigger than rectSizes allows"); continue; }
        }
        else
        {
            double needW = needAreaMm2 / Math.Max(curH, 1);
            newW = snapUp(rectSizes, needW);
            if (newW < 0) { problems.Add($"{el.Id}: needs {needW:F0} mm width at {curH:F0} high — bigger than rectSizes allows"); continue; }
        }

        double actualAreaM2 = (newW / 1000.0) * (newH / 1000.0);
        double vel = flowM3s / actualAreaM2;
        double aspect = Math.Max(newW, newH) / Math.Max(1, Math.Min(newW, newH));
        string note = aspect > maxAspectRatio ? $"ASPECT {aspect:F1}:1 — over the {maxAspectRatio:F1} limit" : "";

        rows.Add((el, flowM3s * 1000.0, "rect", $"{curW:F0}x{curH:F0}", $"{newW:F0}x{newH:F0}", vel, aspect, note, newW, newH));
    }
    else
    {
        problems.Add($"{el.Id}: shape could not be read — it has neither a usable Diameter nor a Width+Height");
    }
}

// ---- report ----
sb.AppendLine($"SIZED: {rows.Count}   no flow (skipped): {noFlow.Count}   problems: {problems.Count}");
sb.AppendLine();

if (rows.Count > 0)
{
    sb.AppendLine("| Element | Flow L/s | Shape | Was mm | Becomes mm | Velocity m/s | Note |");
    sb.AppendLine("|---|---|---|---|---|---|---|");
    foreach (var r in rows.OrderBy(r => r.NewA).ThenBy(r => r.NewB).Take(maxReportedRows))
        sb.AppendLine($"| {r.El.Id} | {r.FlowLs:F0} | {r.Shape} | {r.Was} | {r.Now} | {r.VelMs:F2} | {(r.Was == r.Now ? "unchanged" : "")}{r.Note} |");
    if (rows.Count > maxReportedRows) sb.AppendLine($"\n... and {rows.Count - maxReportedRows} more");

    int overAspect = rows.Count(r => r.Aspect > maxAspectRatio);
    if (overAspect > 0) sb.AppendLine($"\nWARNING: {overAspect} duct(s) end up flatter than {maxAspectRatio:F1}:1 — hold the other dimension instead, or accept it deliberately.");
    sb.AppendLine($"Resulting velocity range: {rows.Min(r => r.VelMs):F2} to {rows.Max(r => r.VelMs):F2} m/s (target {targetVelocityMs:F2}).");
}

if (noFlow.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NO FLOW ({noFlow.Count}) — NOT sized, and not a pass. A duct reads zero flow when nothing downstream is connected to it:");
    sb.AppendLine("  " + string.Join(", ", noFlow.Take(25).Select(i => i.ToString())) + (noFlow.Count > 25 ? $" ... and {noFlow.Count - 25} more" : ""));
    sb.AppendLine("  If this is most of the set, run action-check-system-connectivity.cs — the system is not joined up.");
}

if (problems.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"PROBLEMS ({problems.Count}):");
    foreach (var p in problems.Take(25)) sb.AppendLine($"  {p}");
    if (problems.Count > 25) sb.AppendLine($"  ... and {problems.Count - 25} more");
}

if (rows.Count == 0) return sb.ToString();

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — no duct was resized. Check the unit line and the table, then set dryRun = false.");
    return sb.ToString();
}

// ---- write ----
int done = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - size ducts"))
{
    tx.Start();
    var opts = tx.GetFailureHandlingOptions();
    opts.SetForcedModalHandling(false);
    tx.SetFailureHandlingOptions(opts);
    try
    {
        foreach (var r in rows)
        {
            try
            {
                if (r.Shape == "round")
                {
                    var dp = r.El.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    if (dp == null || dp.IsReadOnly) { failures.Add($"{r.El.Id}: diameter is read-only"); continue; }
                    dp.Set(ToFeet(r.NewA));
                }
                else
                {
                    var wp = r.El.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    var hp = r.El.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    if (wp == null || hp == null || wp.IsReadOnly || hp.IsReadOnly) { failures.Add($"{r.El.Id}: width/height is read-only"); continue; }
                    wp.Set(ToFeet(r.NewA));
                    hp.Set(ToFeet(r.NewB));
                }
                done++;
            }
            catch (Exception ex) { failures.Add($"{r.El.Id}: {ex.Message}"); }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (size ducts) — rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"RESIZED: {done} of {rows.Count} duct(s).");
if (failures.Count > 0)
{
    sb.AppendLine("REFUSED — unchanged in the model:");
    foreach (var f in failures.Take(25)) sb.AppendLine($"  {f}");
}
sb.AppendLine("Resizing re-fits neighbouring fittings — re-run action-check-system-connectivity.cs before treating this as finished.");

return sb.ToString();
