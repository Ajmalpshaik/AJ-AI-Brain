// ============================================================
// FRAGMENT (action) — action-auto-size-pipe.cs
// PURPOSE: Size the pipes in `elements` from the flow they actually carry — velocity method: required
//          bore = the diameter that gives flow / target velocity, snapped UP to the next real pipe size.
//          Reports the resulting velocity for every pipe, so a pipe left running at 4 m/s (noise, erosion)
//          is visible before it is written.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the pipes
//          to size, one system at a time via filter-by-system-type.cs.
//
// ✱✱ THIS IS THE GENERAL FLOW-AND-VELOCITY SIZER, NOT THE DOMESTIC WATER ONE. If the job is a domestic
//    cold or hot water run sized from fixture units, recipes/size-domestic-water-pipe.cs is the right
//    tool and does the whole WSFU -> probable demand -> size chain properly. Use THIS one when a real
//    flow already exists in the model or is known: chilled water, condenser water, heating, condensate,
//    a pumped main. Two fragments because they take completely different inputs, not because the maths
//    differs.
//
// ✱✱ THE VELOCITY IS YOURS AND IT IS SERVICE-SPECIFIC (START-HERE.md rule 3). Chilled water mains and
//    small branches are not designed to the same figure, and copper, steel and plastic have different
//    erosion limits. Size ONE system at a time with that system's figure. This fragment does not know
//    what service it is looking at.
//
// ✱✱ IT READS THE FLOW REVIT ALREADY HAS — `RBS_PIPE_FLOW_PARAM`, filled from the connected fixtures or
//    equipment. A pipe connected to nothing reads zero, and zero flow sizes to nothing: those are
//    reported as SKIPPED (NO FLOW) rather than sized to the smallest pipe in the list. A whole run with
//    no flow means the system is not connected — action-check-system-connectivity.cs, not a sizing job.
//
// ✱✱ THE UNIT ASSUMPTION IS PRINTED SO A WRONG ONE CANNOT HIDE. Revit's internal flow unit is cubic feet
//    per second and the conversion here is plain arithmetic (no version-specific unit API, per the
//    library's version-proof rule). The report prints the raw internal value beside Revit's own display
//    string for the first few pipes — if the constant were wrong they would disagree obviously on the
//    first run. Read that line before trusting a size.
//
// ✱✱ NOMINAL SIZE IS NOT BORE, AND THE DIFFERENCE MATTERS AT SMALL SIZES. The velocity is calculated on
//    the size actually set. `sizesAreBore` says whether the numbers in `pipeSizes` are true internal
//    diameters (set it true and the velocity figures are right) or nominal labels (leave it false and
//    the reported velocity is approximate, which the report says on every row). Neither is guessed at.
//
// GOTCHA: DRY RUN BY DEFAULT — the table prints first. Read it, then set dryRun = false.
// GOTCHA: SIZE IS OFTEN DRIVEN BY THE PIPE TYPE'S SEGMENT TABLE. A type whose segment offers only certain
//         sizes refuses anything else, and that refusal is reported per pipe rather than silently
//         swallowed. Check the type first with action-report-routing-preferences.cs.
// GOTCHA: RESIZING A CONNECTED PIPE re-fits its fittings and can break a joint that cannot adapt.
//         Warnings are non-modal so the batch cannot stall; re-check connectivity afterwards.
// GOTCHA: THIS SIZES SEGMENTS, NOT A SYSTEM. It does not step a main down as branches come off it — each
//         pipe is sized to its own flow figure.
// GOTCHA: DRAINAGE IS NOT SIZED THIS WAY. A gravity drain is sized on fixture units and fall, not on a
//         velocity in a full bore — use the drainage rules, and action-check-slope.cs for the fall.
// RELATED: recipes/size-domestic-water-pipe.cs (fixture-unit method), action-auto-size-duct.cs (same
//          method, air), action-report-mep-pressure-drop.cs (what the sizes cost),
//          action-check-slope.cs (gravity systems).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check the unit sanity line, size ONE pipe,
//   compare against a hand calculation, then do a system.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = print the sizing table only, change nothing
double targetVelocityMs = 1.5;       // design velocity for THIS service, m/s

// Standard sizes, mm. Snapped UP to the first entry big enough.
var pipeSizes = new List<double> { 15, 20, 25, 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500 };
bool sizesAreBore = false;           // true = the numbers above are true internal bore; false = nominal labels

int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
const double CUFT_PER_SEC_TO_CUM_PER_SEC = 0.0283168466;   // Revit's internal flow unit is ft3/s
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the pipes to size).");
    return sb.ToString();
}
if (targetVelocityMs <= 0)
{
    sb.AppendLine("STOP: targetVelocityMs must be greater than 0.");
    return sb.ToString();
}

Func<double, double> snapUp = needMm =>
{
    foreach (var s in pipeSizes.OrderBy(x => x)) if (s >= needMm - 0.001) return s;
    return -1;
};

sb.AppendLine($"PIPE SIZING — velocity method at {targetVelocityMs:F2} m/s, sizes rounded UP to the next standard");
if (!sizesAreBore)
    sb.AppendLine("NOTE: pipeSizes are treated as NOMINAL labels, so the velocity figures below are approximate. Set sizesAreBore = true and list true bores for exact velocities.");
sb.AppendLine();

// ---- unit sanity ----
sb.AppendLine("UNIT CHECK (read this before trusting any size below) — internal value x 0.0283168466 should equal Revit's own display:");
int shown = 0;
foreach (var el in elements)
{
    var fp = el.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM);
    if (fp == null || !fp.HasValue) continue;
    double raw = fp.AsDouble();
    string disp = "";
    try { disp = fp.AsValueString(); } catch { }
    sb.AppendLine($"  {el.Id}: internal {raw:F6} -> {(raw * CUFT_PER_SEC_TO_CUM_PER_SEC * 1000.0):F2} L/s   |   Revit shows: {disp}");
    if (++shown >= 3) break;
}
if (shown == 0) sb.AppendLine("  (no pipe in the set carries a readable Flow parameter — see the NO FLOW note below)");
sb.AppendLine();

// ---- size each pipe ----
var rows = new List<(Element El, double FlowLs, double WasMm, double NowMm, double VelMs, double NeedMm)>();
var noFlow = new List<ElementId>();
var problems = new List<string>();

foreach (var el in elements)
{
    var fp = el.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM);
    if (fp == null || !fp.HasValue) { noFlow.Add(el.Id); continue; }

    double flowM3s = fp.AsDouble() * CUFT_PER_SEC_TO_CUM_PER_SEC;
    if (flowM3s <= 1e-9) { noFlow.Add(el.Id); continue; }

    var dp = el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
          ?? el.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
    if (dp == null || !dp.HasValue) { problems.Add($"{el.Id}: no readable Diameter parameter — not a round pipe?"); continue; }

    double requiredAreaM2 = flowM3s / targetVelocityMs;
    double needMm = Math.Sqrt(4.0 * requiredAreaM2 / Math.PI) * 1000.0;

    double pick = snapUp(needMm);
    if (pick < 0) { problems.Add($"{el.Id}: needs {needMm:F0} mm bore — bigger than the largest entry in pipeSizes"); continue; }

    // Velocity at the size actually chosen. Exact only when the list holds true bores.
    double boreM = pick / 1000.0;
    double actualAreaM2 = Math.PI * boreM * boreM / 4.0;
    double vel = flowM3s / actualAreaM2;

    rows.Add((el, flowM3s * 1000.0, ToMm(dp.AsDouble()), pick, vel, needMm));
}

// ---- report ----
sb.AppendLine($"SIZED: {rows.Count}   no flow (skipped): {noFlow.Count}   problems: {problems.Count}");
sb.AppendLine();

if (rows.Count > 0)
{
    sb.AppendLine("| Element | Flow L/s | Was mm | Needs mm | Becomes mm | Velocity m/s |");
    sb.AppendLine("|---|---|---|---|---|---|");
    foreach (var r in rows.OrderBy(r => r.NowMm).Take(maxReportedRows))
        sb.AppendLine($"| {r.El.Id} | {r.FlowLs:F2} | {r.WasMm:F0} | {r.NeedMm:F1} | {r.NowMm:F0} | {r.VelMs:F2}{(sizesAreBore ? "" : " approx")} |");
    if (rows.Count > maxReportedRows) sb.AppendLine($"\n... and {rows.Count - maxReportedRows} more");

    sb.AppendLine($"\nResulting velocity range: {rows.Min(r => r.VelMs):F2} to {rows.Max(r => r.VelMs):F2} m/s (target {targetVelocityMs:F2}).");
    int unchanged = rows.Count(r => Math.Abs(r.WasMm - r.NowMm) < 0.5);
    sb.AppendLine($"Already correct: {unchanged}   would change: {rows.Count - unchanged}");
}

if (noFlow.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NO FLOW ({noFlow.Count}) — NOT sized, and not a pass. A pipe reads zero flow when nothing is connected to it:");
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
    sb.AppendLine("DRY RUN — no pipe was resized. Check the unit line and the table, then set dryRun = false.");
    return sb.ToString();
}

// ---- write ----
int done = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - size pipes"))
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
                var dp = r.El.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                      ?? r.El.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (dp == null || dp.IsReadOnly) { failures.Add($"{r.El.Id}: diameter is read-only"); continue; }
                dp.Set(ToFeet(r.NowMm));
                done++;
            }
            catch (Exception ex)
            {
                // The usual cause is the pipe type's segment table not offering this size at all.
                failures.Add($"{r.El.Id}: {ex.Message} (is {r.NowMm:F0} mm in this pipe type's segment sizes?)");
            }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (size pipes) — rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"RESIZED: {done} of {rows.Count} pipe(s).");
if (failures.Count > 0)
{
    sb.AppendLine("REFUSED — unchanged in the model:");
    foreach (var f in failures.Take(25)) sb.AppendLine($"  {f}");
    if (failures.Count > 25) sb.AppendLine($"  ... and {failures.Count - 25} more");
}
sb.AppendLine("Resizing re-fits neighbouring fittings — re-run action-check-system-connectivity.cs before treating this as finished.");

return sb.ToString();
