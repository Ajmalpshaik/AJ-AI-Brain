// ============================================================
// FRAGMENT (action) — action-connect-open-connectors.cs
// PURPOSE: Find pairs of OPEN connectors in `elements` that are close enough and compatible enough to
//          belong together, and join them. The "these two pieces are touching but Revit doesn't think
//          they're connected" cleanup — after a copy/paste, after a link is bound, after a route is
//          drawn leg by leg, or wherever a run looks continuous on screen and behaves as two systems.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — e.g.
//          filter-by-multiple-categories.cs over the duct or pipe categories, or
//          filter-by-connection-status.cs with the "has an open connector" mode, which is the natural feeder.
//
// ✱✱ IT PAIRS ON FOUR TESTS, AND ALL FOUR HAVE TO PASS. Distance alone produces nonsense — two pipes
//    crossing at the same height have connectors near each other and must NOT be joined. A pair is only
//    offered when: (1) both connectors are open, (2) same Domain (no duct-to-pipe), (3) sizes agree
//    within `sizeToleranceMm`, and (4) they FACE EACH OTHER — the dot product of their directions is
//    negative, i.e. one points into the other rather than both pointing the same way. Test 4 is the one
//    that separates a real end-to-end joint from a crossing.
//
// ✱✱ IT DOES NOT MOVE ANYTHING. Connector.ConnectTo joins two connectors where they already are; it does
//    not drag one element onto another. So `maxGapMm` is small on purpose — a gap this closes is a
//    rounding-level gap, not a modelling error. A 200 mm hole between two ducts is a drawing mistake and
//    wants moving or a new segment, not a connection: raising maxGapMm to hide it produces a model that
//    reports as connected while the geometry still has a hole in it.
//
// ✱✱ EACH CONNECTOR IS USED ONCE. Candidate pairs are scored by gap and taken best-first, and both
//    connectors are struck off as soon as a pair is taken — otherwise one open end at a junction of three
//    pieces gets claimed twice and the second ConnectTo throws.
//
// GOTCHA: DRY RUN BY DEFAULT — the pairing table prints first with every gap and the reason each pair
//         qualified. Read it, then set dryRun = false.
// GOTCHA: THIS IS NOT THE TERMINAL-TO-DUCT JOB. Tapping an air terminal into the side of a duct needs a
//         tap fitting cut into the duct, which is action-connect-air-terminals.cs (and Revit's own
//         MechanicalUtils call). This one joins END to END. A terminal sitting under a duct will not
//         pair here and that is correct, not a miss.
// GOTCHA: connecting raises Revit warnings on a real model (size mismatch, system mismatch). The
//         transaction sets SetForcedModalHandling(false) so a dialog cannot stop the batch — it resolves
//         nothing for you; see ../../../knowledge/live-model/failure-handling-without-a-class.md.
// GOTCHA: `Connector.IsConnected` describes intent, not always physical reality (START-HERE.md rule 1).
//         A connector already joined to something is skipped here, so a pair that LOOKS missing and is
//         not offered may be a case where Revit thinks it is connected and the geometry disagrees —
//         that is skills/ajtools-mep-trace territory, not this fragment's.
// ✱✱ WHAT THIS DELIBERATELY IS NOT. knowledge/live-model/mep-connect-existing-runs.md describes the full
//    job — STRETCH THE TWO RUNS TOGETHER and build the bridging piece and its fittings, with a
//    sub-transaction per attempted bend angle and a proper crank when the ends are offset. That note ends
//    by saying the fragment for it "has not been written... it should be built the day a real job needs
//    it". This is NOT that fragment and must not be mistaken for it: it joins ends that are ALREADY
//    touching and moves nothing. When the ends are genuinely apart, that note is what to build from.
// SOURCE: ../../../knowledge/live-model/mep-connect-existing-runs.md — the pair SCORING rule above, and
//         the refusal cases (both ends facing the same way, non-parallel ends, mismatched domains) which
//         this fragment applies in its four tests.
// RELATED: filter-by-connection-status.cs (find the open ends first), action-check-system-connectivity.cs
//          (what is still in separate islands afterwards), action-connect-air-terminals.cs (tap, not butt),
//          action-trim-extend-elements.cs and action-fillet-elements.cs (stretch two runs to a corner,
//          and put a real elbow between them — the geometry half of the job this one does not do).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Read the dry-run table, connect ONE pair, and
//   check it in a section before letting it join a whole floor.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = print the pairing table only, connect nothing
double maxGapMm = 25;            // biggest gap that still counts as "these two should be joined"
double sizeToleranceMm = 5;      // how far two connector sizes may differ and still pair
bool requireSameSystem = true;   // true = refuse to join two different system types (supply to return)
double facingTolerance = -0.5;   // dot product of the two directions must be BELOW this (-1 = dead-on)
double misalignmentPenaltyMm = 40;  // how much a fully off-axis pair is penalised when scoring; 0 = sort on gap alone
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
double maxGapFt = maxGapMm / MM_PER_FOOT;
double sizeTolFt = sizeToleranceMm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (filter-by-connection-status.cs is the natural one).");
    return sb.ToString();
}

// ---- gather every OPEN connector on the set ----
Func<Element, ConnectorManager> managerOf = el =>
{
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    return null;
};

// Connector size is Radius on a round connector and Width/Height on a rectangular one — there is no one
// property that covers both, so "size" here is a comparable nominal figure per shape.
Func<Connector, double> nominalSize = c =>
{
    try
    {
        if (c.Shape == ConnectorProfileType.Round) return c.Radius * 2.0;
        return Math.Max(c.Width, c.Height);
    }
    catch { return -1; }
};

Func<Connector, string> sizeLabel = c =>
{
    try
    {
        if (c.Shape == ConnectorProfileType.Round) return $"{ToMm(c.Radius * 2.0):F0} round";
        return $"{ToMm(c.Width):F0}x{ToMm(c.Height):F0}";
    }
    catch { return "(size unreadable)"; }
};

var open = new List<(Element Owner, Connector Con)>();
int noManager = 0;
foreach (var el in elements)
{
    var cm = managerOf(el);
    if (cm == null) { noManager++; continue; }
    foreach (Connector c in cm.Connectors)
    {
        if (c.ConnectorType != ConnectorType.End) continue;
        if (c.Domain == Domain.DomainUndefined) continue;
        bool connected;
        try { connected = c.IsConnected; } catch { continue; }
        if (connected) continue;
        open.Add((el, c));
    }
}

sb.AppendLine($"OPEN CONNECTORS: {open.Count} across {elements.Count} element(s)" + (noManager > 0 ? $"  ({noManager} element(s) carry no connectors at all)" : ""));
if (open.Count < 2)
{
    sb.AppendLine("Nothing to pair — fewer than two open connectors in the set.");
    return sb.ToString();
}

// ---- score every candidate pair ----
Func<Element, string> systemOf = el =>
{
    var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_TYPE_PARAM)
         ?? el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
    if (p == null) return "";
    var v = p.AsValueString();
    return string.IsNullOrEmpty(v) ? (p.AsString() ?? "") : v;
};

var candidates = new List<(int A, int B, double GapMm, double Dot, string Why)>();
var rejected = new List<string>();

for (int i = 0; i < open.Count; i++)
{
    for (int j = i + 1; j < open.Count; j++)
    {
        var a = open[i]; var b = open[j];
        if (a.Owner.Id == b.Owner.Id) continue;   // an element's own two ends are never a pair

        double gapFt;
        try { gapFt = a.Con.Origin.DistanceTo(b.Con.Origin); } catch { continue; }
        if (gapFt > maxGapFt) continue;

        if (a.Con.Domain != b.Con.Domain)
        {
            rejected.Add($"{a.Owner.Id} <-> {b.Owner.Id}: different domain ({a.Con.Domain} vs {b.Con.Domain})");
            continue;
        }

        double sa = nominalSize(a.Con), sbz = nominalSize(b.Con);
        if (sa < 0 || sbz < 0 || Math.Abs(sa - sbz) > sizeTolFt)
        {
            rejected.Add($"{a.Owner.Id} <-> {b.Owner.Id}: size {sizeLabel(a.Con)} vs {sizeLabel(b.Con)} (gap {ToMm(gapFt):F1} mm)");
            continue;
        }

        double dot;
        try { dot = a.Con.CoordinateSystem.BasisZ.Normalize().DotProduct(b.Con.CoordinateSystem.BasisZ.Normalize()); }
        catch { continue; }
        if (dot > facingTolerance)
        {
            rejected.Add($"{a.Owner.Id} <-> {b.Owner.Id}: not facing each other (dot {dot:F2}, needs <= {facingTolerance:F2}) — probably a crossing, not a joint");
            continue;
        }

        if (requireSameSystem)
        {
            string sysA = systemOf(a.Owner), sysB = systemOf(b.Owner);
            if (!string.IsNullOrEmpty(sysA) && !string.IsNullOrEmpty(sysB) &&
                !string.Equals(sysA, sysB, StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add($"{a.Owner.Id} <-> {b.Owner.Id}: different system ('{sysA}' vs '{sysB}')");
                continue;
            }
        }

        candidates.Add((i, j, ToMm(gapFt), dot, $"{sizeLabel(a.Con)} / gap {ToMm(gapFt):F1} mm / dot {dot:F2}"));
    }
}

// SCORED BEST-FIRST, NOT NEAREST-FIRST — each connector used once.
// "Nearest pair of connectors" is named in knowledge/live-model/mep-connect-existing-runs.md as the
// WRONG rule: it picks a pair that then needs a long awkward crank. The recorded principle is SMALLEST
// TOTAL INTERVENTION. Nothing moves in this fragment, so there is no shift to count — what is left is
// how square the joint is. Two ends 5 mm apart but 30 degrees off-axis make a worse connection than two
// 8 mm apart and dead-on, and a pure gap sort prefers the bad one. The misalignment term fixes that.
Func<double, double, double> scoreOf = (gapMm, dot) => gapMm + (1.0 + dot) * misalignmentPenaltyMm;

var taken = new HashSet<int>();
var pairs = new List<(int A, int B, double GapMm, string Why)>();
foreach (var c in candidates.OrderBy(c => scoreOf(c.GapMm, c.Dot)))
{
    if (taken.Contains(c.A) || taken.Contains(c.B)) continue;
    taken.Add(c.A); taken.Add(c.B);
    pairs.Add((c.A, c.B, c.GapMm, c.Why));
}

sb.AppendLine($"PAIRS TO CONNECT: {pairs.Count}");
foreach (var p in pairs)
    sb.AppendLine($"  {open[p.A].Owner.Id} ({open[p.A].Owner.Category?.Name}) <-> {open[p.B].Owner.Id} ({open[p.B].Owner.Category?.Name})   {p.Why}");

if (rejected.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NEAR MISSES ({rejected.Count}) — close enough to look at, rejected on a test:");
    foreach (var r in rejected.Take(30)) sb.AppendLine($"  {r}");
    if (rejected.Count > 30) sb.AppendLine($"  ... and {rejected.Count - 30} more");
}

int stillOpen = open.Count - (pairs.Count * 2);
sb.AppendLine();
sb.AppendLine($"Open connectors left unpaired after this: {stillOpen}");

if (pairs.Count == 0)
{
    sb.AppendLine("Nothing qualified. If you expected pairs, the usual causes are a gap over maxGapMm (this fragment never moves anything) or a size mismatch.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine("DRY RUN — nothing was connected. Read the table above, then set dryRun = false.");
    return sb.ToString();
}

// ---- connect ----
int done = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - connect open connectors"))
{
    tx.Start();
    // Warnings are expected on a real model; a modal dialog would stop the batch dead.
    var opts = tx.GetFailureHandlingOptions();
    opts.SetForcedModalHandling(false);
    tx.SetFailureHandlingOptions(opts);
    try
    {
        foreach (var p in pairs)
        {
            var ca = open[p.A].Con; var cbz = open[p.B].Con;
            try
            {
                // Re-check: an earlier ConnectTo in this same loop can have joined one of these as a
                // side effect, and connecting an already-connected connector throws.
                if (ca.IsConnected || cbz.IsConnected)
                {
                    failures.Add($"{open[p.A].Owner.Id} <-> {open[p.B].Owner.Id}: one end was already connected by an earlier pair in this run");
                    continue;
                }
                ca.ConnectTo(cbz);
                done++;
            }
            catch (Exception ex)
            {
                failures.Add($"{open[p.A].Owner.Id} <-> {open[p.B].Owner.Id}: {ex.Message}");
            }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (connect open connectors) — rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine($"CONNECTED: {done} of {pairs.Count} pair(s).");
if (failures.Count > 0)
{
    sb.AppendLine("REFUSED — these are unchanged in the model:");
    foreach (var f in failures) sb.AppendLine($"  {f}");
}
sb.AppendLine("Verify the result rather than trusting this count — action-check-system-connectivity.cs, or the verify_connectivity native tool.");

return sb.ToString();
