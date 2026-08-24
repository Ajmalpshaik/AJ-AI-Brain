// ============================================================
// FRAGMENT (action) — action-report-mep-clearance.cs
// PURPOSE: The EXACT gap between MEP runs, in mm — every pair in `elements` whose clearance is below
//          a limit, worst first. Answers "is there 50 mm between those two pipes", "which services are
//          too close to each other to insulate or to bracket", "show me the tight spots before the
//          contractor finds them". Read-only: measures, changes nothing.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — linear MEP
//          (ducts, pipes, cable tray, conduit — anything with a LocationCurve).
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ WHY THIS EXISTS — NOTHING HERE COULD PUT A NUMBER ON A GAP.
//    `action-report-nearest-elements.cs` measures with BOUNDING BOXES and says so in its own header:
//    "for a ROTATED element the box is bigger than the element and 'gap' reads slightly optimistic...
//    use action-report-clashes.cs when the number must be exact." But `action-report-clashes.cs`
//    returns a yes/no — it has no distance in it at all. So the advice pointed at a fragment that
//    cannot answer, and a diagonal pipe's real clearance was not obtainable in this library.
//    `Curve.ComputeClosestPoints` is the exact answer: the true minimum distance between two curves,
//    with the point on each. No boxes, no sampling, no rotation error.
//
// ✱✱ IT MEASURES CENTRELINE TO CENTRELINE, THEN TAKES THE SERVICES' OWN THICKNESS OFF.
//    Revit's LocationCurve is the CENTRELINE. Two 300 mm pipes whose centrelines are 320 mm apart have
//    a 20 mm gap, not 320. Each run's outer half-size is subtracted:
//      round  -> Diameter / 2                                   (exact)
//      rect   -> half the DIAGONAL of Width x Height            (conservative — see below)
//    and the insulation thickness on top of that, when there is any.
//
// ✱✱ THE RECTANGULAR NUMBER IS DELIBERATELY PESSIMISTIC, AND THAT IS THE SAFE DIRECTION. A rectangular
//    duct's half-size depends on which way the other service approaches it — half the height from
//    above, half the width from the side, something between at an angle. Half the diagonal is the
//    largest of those, so the reported gap is the SMALLEST it could be. A rectangular pair that passes
//    this really passes; one that fails may still be fine, and the row says RECT so it can be checked
//    by eye. Round-to-round is exact and needs no such caveat.
//
// ✱✱ INSULATION COUNTS, AND LEAVING IT OUT IS THE CLASSIC WAY TO GET THIS WRONG. A 100 mm pipe with
//    25 mm insulation is 150 mm across, and the gap that matters is between the JACKETS, not the
//    pipes — that is what has to be fitted, and what a bracket has to clear. `includeInsulation`
//    defaults to true, and each row says whether insulation was found. Ajmal's standing rule about
//    insulation following its host applies to measurement as much as to colour — see
//    knowledge/live-model/insulation-follows-host.md.
//
// GOTCHA: PAIRS ARE O(n²). 200 runs is 19,900 comparisons and is instant; 5,000 runs is 12.5 million
//         and is not. A cheap bounding-box reject runs first so only plausible pairs reach the real
//         computation — but narrow `elements` to a level, a system or a region before pointing this at
//         a whole model.
// GOTCHA: only straight/curved runs with a LocationCurve are measured. FITTINGS, EQUIPMENT AND
//         ACCESSORIES HAVE NO CENTRELINE and are counted as skipped, by name, so a tight elbow is
//         never silently reported as clear. For those, use action-report-clashes.cs (contact yes/no)
//         or action-report-nearest-elements.cs (approximate gap).
// GOTCHA: two runs that are CONNECTED to each other meet by design. `ignoreConnected` drops any pair
//         sharing a connector, otherwise every elbow in the model reports a zero gap.
// GOTCHA: `minClearanceMm` is a PROJECT NUMBER — ask for it. The value below is a placeholder so the
//         fragment runs, not a recommendation.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only.
//    Measure one reported pair in Revit with the Measure tool before trusting a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double minClearanceMm = 50;     // ASK. report every pair closer than this
bool includeInsulation = true;  // measure jacket-to-jacket, not bare-pipe-to-bare-pipe
bool ignoreConnected = true;    // drop pairs that share a connector — they touch by design
bool ignoreSameSystem = false;  // true = only report clashes BETWEEN systems, not within one
int maxRowsListed = 100;
// ---- END INPUTS ----

const double MM = 304.8;
double limitFt = minClearanceMm / MM;

// ---------- gather the runs that actually have a centreline ----------
var runs = new List<Tuple<Element, Curve>>();
var skipped = new List<string>();

foreach (var e in elements)
{
    if (e == null) continue;
    var lc = e.Location as LocationCurve;
    if (lc == null || lc.Curve == null || !lc.Curve.IsBound)
    {
        string cat = e.Category != null ? e.Category.Name : "(no category)";
        skipped.Add($"{e.Id} | {cat} | {e.Name} | no bound centreline (fitting, equipment or accessory)");
        continue;
    }
    runs.Add(Tuple.Create(e, lc.Curve));
}

if (runs.Count < 2)
{
    sb.AppendLine($"Only {runs.Count} element(s) with a centreline — need at least two to measure a gap.");
    if (skipped.Count > 0) sb.AppendLine($"{skipped.Count} element(s) were skipped for having no centreline; see the note in this fragment's header.");
    return sb.ToString();
}

// ---------- each run's outer half-size, insulation included ----------
Func<Element, double> insulationThicknessOf = el =>
{
    if (!includeInsulation) return 0;
    double t = 0;
    try
    {
        var ids = InsulationLiningBase.GetInsulationIds(Document, el.Id);
        if (ids != null)
            foreach (var id in ids)
            {
                var ins = Document.GetElement(id) as InsulationLiningBase;
                if (ins != null && ins.Thickness > t) t = ins.Thickness;
            }
    }
    catch { }
    return t;
};

// Returns (halfSize, shapeLabel, hasInsulation). Round is exact; rectangular takes half the diagonal,
// which is the worst case and therefore the safe direction — see the header.
Func<Element, Tuple<double, string, bool>> halfSizeOf = el =>
{
    double ins = insulationThicknessOf(el);
    var mc = el as MEPCurve;
    double half = 0; string shape = "UNKNOWN";
    if (mc != null)
    {
        double dia = 0, w = 0, h = 0;
        try { dia = mc.Diameter; } catch { }
        try { w = mc.Width; } catch { }
        try { h = mc.Height; } catch { }
        if (w > 0 && h > 0) { half = Math.Sqrt(w * w + h * h) / 2.0; shape = "RECT"; }
        else if (dia > 0) { half = dia / 2.0; shape = "ROUND"; }
    }
    if (half <= 0)
    {
        // Not an MEPCurve, or it reported nothing usable — fall back to the bounding box's half
        // diagonal. Same conservative direction, and the row is labelled so nobody reads it as exact.
        try
        {
            var bb = el.get_BoundingBox(null);
            if (bb != null)
            {
                var d = bb.Max - bb.Min;
                half = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z) / 2.0;
                shape = "BOX";
            }
        }
        catch { }
    }
    return Tuple.Create(half + ins, shape, ins > 0);
};

var sizeCache = new Dictionary<ElementId, Tuple<double, string, bool>>();
Func<Element, Tuple<double, string, bool>> sizeOf = el =>
{
    Tuple<double, string, bool> v;
    if (sizeCache.TryGetValue(el.Id, out v)) return v;
    v = halfSizeOf(el); sizeCache[el.Id] = v; return v;
};

// ---------- connected-to-each-other, so they touch by design ----------
var connectedTo = new Dictionary<ElementId, HashSet<ElementId>>();
if (ignoreConnected)
{
    foreach (var r in runs)
    {
        var set = new HashSet<ElementId>();
        try
        {
            var mc = r.Item1 as MEPCurve;
            if (mc != null && mc.ConnectorManager != null)
                foreach (Connector c in mc.ConnectorManager.Connectors)
                    foreach (Connector other in c.AllRefs)
                        if (other.Owner != null) set.Add(other.Owner.Id);
        }
        catch { }
        connectedTo[r.Item1.Id] = set;
    }
}

Func<Element, string> systemOf = el =>
{
    try
    {
        var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
        if (p != null && p.HasValue) return p.AsString() ?? "";
    }
    catch { }
    return "";
};

// ---------- the measurement ----------
// Cheap box reject first: if the two boxes are further apart than the limit plus both half-sizes,
// no curve computation can bring them closer. That is what keeps O(n2) affordable.
var boxes = new Dictionary<ElementId, BoundingBoxXYZ>();
foreach (var r in runs) { try { boxes[r.Item1.Id] = r.Item1.get_BoundingBox(null); } catch { } }

Func<BoundingBoxXYZ, BoundingBoxXYZ, double> boxGap = (a, b) =>
{
    if (a == null || b == null) return 0;   // unknown -> do not reject
    double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
    double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
    double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
};

var findings = new List<Tuple<double, string>>();
int compared = 0, rejected = 0, failed = 0, touching = 0;

for (int i = 0; i < runs.Count; i++)
{
    for (int j = i + 1; j < runs.Count; j++)
    {
        var ea = runs[i].Item1; var eb = runs[j].Item1;

        if (ignoreConnected)
        {
            HashSet<ElementId> set;
            if (connectedTo.TryGetValue(ea.Id, out set) && set.Contains(eb.Id)) continue;
        }

        string sysA = systemOf(ea), sysB = systemOf(eb);
        if (ignoreSameSystem && sysA.Length > 0 && sysA == sysB) continue;

        var sa = sizeOf(ea); var sbz = sizeOf(eb);
        double slack = limitFt + sa.Item1 + sbz.Item1;

        BoundingBoxXYZ ba = null, bb2 = null;
        boxes.TryGetValue(ea.Id, out ba); boxes.TryGetValue(eb.Id, out bb2);
        if (boxGap(ba, bb2) > slack) { rejected++; continue; }

        IList<ClosestPointsPairBetweenTwoCurves> pairs = null;
        try { runs[i].Item2.ComputeClosestPoints(runs[j].Item2, true, true, false, out pairs); }
        catch { failed++; continue; }
        compared++;
        if (pairs == null || pairs.Count == 0) continue;

        double centreline = double.MaxValue;
        XYZ at = null;
        foreach (var p in pairs)
        {
            if (p.Distance < centreline) { centreline = p.Distance; at = p.XYZPointOnFirstCurve; }
        }
        if (centreline == double.MaxValue) continue;

        double gapFt = centreline - sa.Item1 - sbz.Item1;
        if (gapFt > limitFt) continue;
        if (gapFt < 0) touching++;

        string shapeNote = (sa.Item2 == "ROUND" && sbz.Item2 == "ROUND") ? "exact" : $"{sa.Item2}/{sbz.Item2} conservative";
        string insNote = (sa.Item3 || sbz.Item3) ? "incl. insulation" : "no insulation";
        string where = at == null ? "" : $"at ({Math.Round(at.X * MM):N0}, {Math.Round(at.Y * MM):N0}, {Math.Round(at.Z * MM):N0}) mm";
        string verdict = gapFt < 0 ? "OVERLAPPING" : "TIGHT";

        findings.Add(Tuple.Create(gapFt,
            $"{Math.Round(gapFt * MM):N0} | {verdict} | {ea.Id} ({ea.Name}) | {eb.Id} ({eb.Name}) | "
            + $"centres {Math.Round(centreline * MM):N0} mm | {shapeNote}, {insNote} | {where}"));
    }
}

// ---------- output ----------
sb.AppendLine($"MEP CLEARANCE — {runs.Count} run(s) with a centreline, limit {minClearanceMm:N0} mm"
    + (includeInsulation ? ", measured jacket to jacket" : ", measured to the bare service"));
sb.AppendLine($"{compared:N0} pair(s) measured, {rejected:N0} rejected on bounding box, {findings.Count} below the limit"
    + (touching > 0 ? $", of which {touching} OVERLAP" : ""));
if (failed > 0) sb.AppendLine($"⚠ {failed} pair(s) could not be measured — those pairs are NOT covered by the result.");
sb.AppendLine();

if (findings.Count == 0)
{
    sb.AppendLine($"Nothing closer than {minClearanceMm:N0} mm among the runs that could be measured.");
}
else
{
    sb.AppendLine("Gap (mm) | Verdict | Run A | Run B | Centreline | Basis | Location");
    sb.AppendLine("---: | --- | --- | --- | --- | --- | ---");
    foreach (var f in findings.OrderBy(x => x.Item1).Take(maxRowsListed)) sb.AppendLine(f.Item2);
    if (findings.Count > maxRowsListed)
        sb.AppendLine($"... {findings.Count - maxRowsListed} more not listed (raise maxRowsListed).");
}

if (skipped.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NOT MEASURED — {skipped.Count} element(s) have no centreline, so no gap could be computed for them:");
    sb.AppendLine("Id | Category | Name | Reason");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var s in skipped.Take(30)) sb.AppendLine(s);
    if (skipped.Count > 30) sb.AppendLine($"... {skipped.Count - 30} more.");
    sb.AppendLine("A fitting between two clear runs can still be the tight spot — check those with action-report-clashes.cs.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
