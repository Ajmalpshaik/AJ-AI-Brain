// ============================================================
// FRAGMENT (action) — action-set-pipe-slope.cs
// PURPOSE: Put a required fall onto the pipes (or ducts) in `elements` — hold one end still and lift or
//          drop the other until the run sits at the target slope. The fix for what
//          action-check-slope.cs reports: a drainage line drawn flat, or at whatever fall the mouse
//          happened to give it.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the runs
//          to re-slope. Almost always narrowed to one system with filter-by-system-type.cs first.
//
// ✱✱ THIS MOVES REAL GEOMETRY AND IT IS NOT A COSMETIC CHANGE. Setting a run's slope changes where its
//    ends are in space, which changes what its fittings sit on, which can drag or break a connection at
//    either end. That is why it is dry-run by default and why the report names, for every pipe, what is
//    connected to the end it wants to move. Read that list before setting dryRun = false.
//
// ✱✱ WHICH END STAYS PUT IS THE DESIGN DECISION, and there is no safe default for it. On a drainage
//    branch the end that stays is the one at the invert you have already fixed — usually the downstream
//    end at the manhole or stack. `anchor` takes "start", "end", "low" or "high"; "low" holds whichever
//    end is currently lower, which is the one that usually matters on a drain because it is the end
//    already set to a fixed invert level.
//
// ✱✱ IT WORKS ON THE LOCATION CURVE, NOT ON THE SLOPE PARAMETER. Revit's Slope parameter is read-only on
//    a plain pipe in most situations — it reports what the geometry is doing rather than driving it.
//    Setting the curve is what actually moves the pipe. Where the parameter IS writable it is set too,
//    so a schedule reading the parameter agrees with the model.
//
// GOTCHA: A RUN WITH BOTH ENDS CONNECTED USUALLY CANNOT MOVE, and Revit will either refuse or drag the
//         neighbour with it. Neither is silently acceptable, so both ends' connections are reported per
//         pipe and `skipIfBothEndsConnected` defaults to TRUE — the safe behaviour is to re-slope the
//         free runs and tell you which ones need their fittings dealt with first.
// GOTCHA: VERTICAL RUNS ARE NEVER TOUCHED. A stack has no horizontal run to slope against, and forcing
//         a slope onto one would move its top or bottom sideways to nowhere. Skipped and counted.
// GOTCHA: THE FALL DIRECTION FOLLOWS `fallToward`, not the pipe's drawing direction. A pipe drawn
//         upstream-to-downstream and its neighbour drawn the other way would otherwise end up sloping
//         opposite ways from the same input — the classic cause of a drain that runs uphill halfway
//         along. Direction is resolved per pipe against the anchor end.
// GOTCHA: THE SLOPE IS APPLIED PER PIPE, NOT ALONG A CHAIN. Each run gets the right fall on its own; it
//         does NOT work out a continuous invert down a branch of many segments. For a whole branch,
//         re-slope from the downstream end outward and check with action-check-slope.cs after each step.
// GOTCHA: A MOVE ON A GROUP MEMBER IS SILENTLY IGNORED — Revit returns normally and changes nothing
//         (proved live 2026-08-07). Group members are refused up front and named, because counting that
//         call as success is how this report would say "re-sloped 12" over a model where nothing moved.
//         The member reports `Pinned = true` while the GROUP reports false, so checking the group gives
//         the wrong answer. Every write is ALSO read back and compared, so any other silent refusal
//         (a constraint, a pinned neighbour) is reported rather than counted as done.
// SOURCE: ../../../knowledge/live-model/geometry-and-transforms.md § "A move on a GROUP MEMBER is
//         silently ignored" — read it before changing how this fragment writes.
// RELATED: action-check-slope.cs (measure first, and verify after), action-align-mep-elevation.cs (set a
//          run to a flat elevation instead of a fall), action-connect-open-connectors.cs (rejoin what
//          moving an end pulled apart), action-report-constraints.cs ("why won't this element move?").
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Re-slope ONE free-ended pipe, look at it in a
//   section, and re-run action-check-slope.cs on it before doing a branch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                      // true = report what would move and by how much, change nothing
double targetRatio = 100;                // the X in 1:X — 1:100. Must be > 0
string anchor = "low";                   // which end stays put: "start" | "end" | "low" | "high"
string fallToward = "away";              // "away" = falls away from the anchor; "toward" = falls to the anchor
bool skipIfBothEndsConnected = true;     // true = leave runs that are pinned at both ends alone
double minRunMm = 300;                   // horizontal run below this is a stub/vertical — never sloped
double maxMoveMm = 500;                  // refuse to move an end further than this; a guard against a typo
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the runs to re-slope).");
    return sb.ToString();
}
if (targetRatio <= 0)
{
    sb.AppendLine("STOP: targetRatio must be greater than 0 — it is the X in 1:X.");
    return sb.ToString();
}

// ---- work out, per element, what would happen ----
Func<Element, ConnectorManager> managerOf = el =>
{
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    return null;
};

// Which of this run's two ends is joined to something, tested at the end's actual position.
Func<Element, XYZ, bool> endIsConnected = (el, pt) =>
{
    var cm = managerOf(el);
    if (cm == null) return false;
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.ConnectorType != ConnectorType.End) continue;
            if (c.Origin.DistanceTo(pt) > ToFeet(20)) continue;
            if (c.IsConnected) return true;
        }
    }
    catch { }
    return false;
};

var plan = new List<(Element El, XYZ Fixed, XYZ MovingOld, XYZ MovingNew, double RunMm, double MoveMm, bool MovingEndConnected, bool FixedEndConnected)>();
var skipped = new List<string>();

foreach (var el in elements)
{
    // A MOVE ON A GROUP MEMBER IS SILENTLY IGNORED — no exception, no return value, no change (proved
    // live 2026-08-07, knowledge/live-model/geometry-and-transforms.md). Setting a LocationCurve on one
    // would return normally and change nothing, and this fragment would then report it as re-sloped.
    // That is the confidently-wrong failure, so group members are refused up front and named.
    // NOTE the trap inside the trap: the member reports Pinned = true while the GROUP itself reports
    // false, so "the group isn't pinned, therefore it's movable" gives the wrong answer — and
    // `element.Pinned = false` on a member throws rather than helping.
    if (el.GroupId != null && el.GroupId != ElementId.InvalidElementId)
    {
        var grp = Document.GetElement(el.GroupId);
        skipped.Add($"{el.Id}: inside model group '{(grp != null ? grp.Name : el.GroupId.ToString())}' — a move on a group member is SILENTLY IGNORED by Revit. Ungroup it, or edit the group, before re-sloping");
        continue;
    }

    var lc = el.Location as LocationCurve;
    if (lc == null || lc.Curve == null) { skipped.Add($"{el.Id}: not a linear run (a fitting has no curve to slope)"); continue; }

    var p0 = lc.Curve.GetEndPoint(0);
    var p1 = lc.Curve.GetEndPoint(1);
    double runMm = ToMm(Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2)));

    if (runMm < minRunMm) { skipped.Add($"{el.Id}: vertical or stub ({runMm:F0} mm horizontal, under minRunMm) — never sloped"); continue; }

    // Pick the anchor end.
    XYZ fixedEnd, movingEnd;
    string a = anchor.Trim().ToLower();
    if (a == "start") { fixedEnd = p0; movingEnd = p1; }
    else if (a == "end") { fixedEnd = p1; movingEnd = p0; }
    else if (a == "high") { if (p0.Z >= p1.Z) { fixedEnd = p0; movingEnd = p1; } else { fixedEnd = p1; movingEnd = p0; } }
    else { if (p0.Z <= p1.Z) { fixedEnd = p0; movingEnd = p1; } else { fixedEnd = p1; movingEnd = p0; } }  // "low"

    bool movingConnected = endIsConnected(el, movingEnd);
    bool fixedConnected = endIsConnected(el, fixedEnd);

    if (skipIfBothEndsConnected && movingConnected && fixedConnected)
    {
        skipped.Add($"{el.Id}: both ends connected — pinned. Disconnect one end first, or set skipIfBothEndsConnected = false and accept that a neighbour may be dragged");
        continue;
    }

    // The required drop over this run's own horizontal length.
    double dropMm = runMm / targetRatio;
    // "away" = the moving end ends up BELOW the anchor; "toward" = above it.
    double targetZ = fallToward.Trim().ToLower() == "toward"
        ? fixedEnd.Z + ToFeet(dropMm)
        : fixedEnd.Z - ToFeet(dropMm);

    var newMoving = new XYZ(movingEnd.X, movingEnd.Y, targetZ);
    double moveMm = ToMm(Math.Abs(targetZ - movingEnd.Z));

    if (moveMm > maxMoveMm)
    {
        skipped.Add($"{el.Id}: would move an end {moveMm:F0} mm, over maxMoveMm {maxMoveMm:F0} — check targetRatio, or raise the guard deliberately");
        continue;
    }

    plan.Add((el, fixedEnd, movingEnd, newMoving, runMm, moveMm, movingConnected, fixedConnected));
}

// ---- report the plan ----
sb.AppendLine($"SET SLOPE 1:{targetRatio:F0}   anchor = {anchor}   fall {fallToward} the anchor");
sb.AppendLine($"Runs to change: {plan.Count}   skipped: {skipped.Count}");
sb.AppendLine();

if (plan.Count > 0)
{
    sb.AppendLine("| Element | Category | Run mm | Drop mm | End moves mm | Moving end connected? |");
    sb.AppendLine("|---|---|---|---|---|---|");
    foreach (var p in plan.OrderByDescending(p => p.MoveMm).Take(60))
        sb.AppendLine($"| {p.El.Id} | {p.El.Category?.Name ?? "-"} | {p.RunMm:F0} | {(p.RunMm / targetRatio):F1} | {p.MoveMm:F1} | {(p.MovingEndConnected ? "YES — something is joined there" : "no, free")} |");
    if (plan.Count > 60) sb.AppendLine($"\n... and {plan.Count - 60} more");

    int connectedMovers = plan.Count(p => p.MovingEndConnected);
    if (connectedMovers > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"WARNING: {connectedMovers} run(s) have something CONNECTED to the end being moved. Revit will either drag that neighbour or break the joint. Re-check with action-check-system-connectivity.cs afterwards.");
    }
}

if (skipped.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"SKIPPED ({skipped.Count}):");
    foreach (var s in skipped.Take(40)) sb.AppendLine($"  {s}");
    if (skipped.Count > 40) sb.AppendLine($"  ... and {skipped.Count - 40} more");
}

if (plan.Count == 0)
{
    sb.AppendLine();
    sb.AppendLine("Nothing to change.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing moved. Read the table above, then set dryRun = false.");
    return sb.ToString();
}

// ---- apply ----
int done = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - set slope"))
{
    tx.Start();
    var opts = tx.GetFailureHandlingOptions();
    opts.SetForcedModalHandling(false);
    tx.SetFailureHandlingOptions(opts);
    try
    {
        foreach (var p in plan)
        {
            try
            {
                var lc = p.El.Location as LocationCurve;
                if (lc == null) { failures.Add($"{p.El.Id}: lost its location curve between the plan and the write"); continue; }

                lc.Curve = Line.CreateBound(p.Fixed, p.MovingNew);

                // READ IT BACK. Revit has more than one way to accept a write and do nothing with it
                // (group membership is the proven one, but a constraint or a pinned neighbour will do it
                // too), and every one of them returns normally. Counting the call as success is how a
                // report says "re-sloped 12" over a model where nothing moved.
                var after = (p.El.Location as LocationCurve)?.Curve;
                if (after == null)
                {
                    failures.Add($"{p.El.Id}: lost its curve after the write");
                    continue;
                }
                double movedMm = Math.Min(
                    ToMm(after.GetEndPoint(0).DistanceTo(p.MovingNew)),
                    ToMm(after.GetEndPoint(1).DistanceTo(p.MovingNew)));
                if (movedMm > 1.0)
                {
                    failures.Add($"{p.El.Id}: the write was accepted but the pipe did NOT move (end is still {movedMm:F0} mm from where it was asked to go) — a constraint, a group, or a pinned neighbour is holding it");
                    continue;
                }

                // Keep the Slope parameter honest where Revit lets it be written.
                var sp = p.El.get_Parameter(BuiltInParameter.RBS_PIPE_SLOPE);
                if (sp != null && !sp.IsReadOnly)
                {
                    try { sp.Set(1.0 / targetRatio); } catch { }
                }
                done++;
            }
            catch (Exception ex)
            {
                failures.Add($"{p.El.Id}: {ex.Message}");
            }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (set slope) — rolled back, nothing moved. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"RE-SLOPED: {done} of {plan.Count} run(s).");
if (failures.Count > 0)
{
    sb.AppendLine("REFUSED — unchanged in the model:");
    foreach (var f in failures) sb.AppendLine($"  {f}");
}
sb.AppendLine("Verify rather than trusting this count: re-run action-check-slope.cs, then action-check-system-connectivity.cs for joints that moving may have broken.");

return sb.ToString();
