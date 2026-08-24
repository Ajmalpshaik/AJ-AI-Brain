// ============================================================
// FRAGMENT (action) — action-check-vertical-clearance.cs
// PURPOSE: Service-to-service SEPARATION IN Z — for every pair of services whose plan footprints overlap,
//          how much clear vertical space is between the bottom of the upper one and the top of the lower
//          one, and which pairs are tighter than the rule. The "keep 150 mm between the duct and the
//          sprinkler main so the insulation and the hangers fit" check, and the one that decides whether
//          a ceiling void actually works.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          services in one zone, e.g. filter-by-multiple-categories.cs over duct/pipe/tray/conduit,
//          usually narrowed to one level with filter-by-elements-on-level.cs.
// Read-only. The model never changes.
//
// ✱✱ WHY THIS IS NOT action-check-minimum-clearance.cs WITH A SMALLER NUMBER. A straight-line gap does
//    not answer the coordination question. Two ducts 200 mm apart diagonally have 200 mm of straight-line
//    clearance and may have only 40 mm of VERTICAL room — which is what a hanger, a flange and an
//    insulation jacket actually need. This measures the Z gap only, and only for pairs that are ACTUALLY
//    ABOVE ONE ANOTHER (their plan footprints overlap). Services that pass at different plan positions
//    are not a stacking problem and are correctly silent here.
//
// ✱✱ IT SAYS WHICH ONE IS ON TOP, because that is half the answer. The report reads "A is above B by
//    N mm", so a service that is in the wrong order (drainage above the duct it has to cross under) is
//    visible as a fact, not inferred from two elevations.
//
// ✱✱ A NEGATIVE GAP IS AN OVERLAP, NOT A SMALL GAP. Where the boxes interpenetrate in Z the figure is
//    reported as a negative number and flagged CLASH, so it can never be read as "just tight".
//
// GOTCHA: MEASURED ON BOUNDING BOXES, and for a horizontal duct or pipe that is the right answer — the
//         box hugs the real extent. For a SLOPED or VERTICAL run the box is the whole rise, so the
//         reported gap is pessimistic (it will over-report a problem, never miss one). Sloped and
//         vertical runs are counted separately in the output so that is visible; take those to a section.
// GOTCHA: INSULATION IS NOT INCLUDED unless modelled as its own element and caught by the filter. Add
//         the insulation categories to the filter, or use action-check-insulation-clearance.cs.
// GOTCHA: PLAN OVERLAP IS TESTED ON BOXES TOO, so two runs that cross at 45 degrees count as overlapping
//         over the whole corner of their boxes. That is deliberate — at a crossing, the box corner is
//         where the hanger goes.
// GOTCHA: reports BY EXCEPTION. A clean run prints counts and no table.
// RELATED: action-check-minimum-clearance.cs (straight-line gap, any direction),
//          action-align-mep-elevation.cs (the fix — set a run to a chosen elevation),
//          action-report-ceiling-heights.cs (how much void there is to work in at all).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one reported pair against a section in
//   Revit before trusting a whole-corridor sweep.
//
// ✱✱ FIVE FRAGMENTS ANSWER "IS THERE ENOUGH ROOM" AND THEY MEASURE DIFFERENT THINGS DIFFERENT WAYS.
//    Pick deliberately. The wrong one is not slower, it is a different number:
//      action-report-mep-clearance.cs        EXACT mm between LINEAR MEP runs. Centreline maths
//                                            (ComputeClosestPoints) minus each run own half-size and
//                                            its insulation. "clearance between the services", "gap
//                                            between those two pipes", "how close are they". No
//                                            sampling error.
//      action-check-minimum-clearance.cs     ANY element against a target set, including equipment and
//                                            structure. Samples solid faces, so the number is a
//                                            SAMPLE, not exact. Takes a per-category rules table.
//      action-check-vertical-clearance.cs    Z SEPARATION ONLY, for pairs whose plan footprints
//                                            overlap. "how much room between the duct and the tray
//                                            above it".
//      action-check-insulation-clearance.cs  As action-check-minimum-clearance.cs but measured to the
//                                            OUTSIDE OF THE JACKET. 50 mm each side eats 100 mm.
//      action-check-equipment-clearance.cs   The MAINTENANCE ACCESS ZONE in front of, beside and above
//                                            a piece of kit. Can it be serviced, not can it be built.
//    A CLASH IS NOT A CLEARANCE FAILURE: action-report-clashes.cs answers "do these overlap", yes/no,
//    with no distance in it. To DRAW the dimension rather than report the gap, see
//    actions/sheets-views/action-dimension-mep-runs.cs. Measured 2026-08-24: "check the clearance
//    between services" did not return action-report-mep-clearance.cs at all, though it is the only one
//    of the five that is exact, which is why the spoken phrasings are written in above.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double requiredVerticalMm = 150;     // clear vertical space required between stacked services
double minPlanOverlapMm = 50;        // ignore pairs whose plan footprints barely graze each other
bool ignoreSameCategory = false;     // true = only compare ACROSS categories (duct vs pipe, not duct vs duct)
bool ignoreConnected = true;         // skip pairs that share a connector
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the services in one zone).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

double requiredFt = ToFeet(requiredVerticalMm);
double minOverlapFt = ToFeet(minPlanOverlapMm);

// ---- collect boxes, and note which runs are not horizontal ----
var items = new List<(Element El, BoundingBoxXYZ Box, bool Horizontal, string Kind)>();
int noBox = 0;

foreach (var el in elements)
{
    BoundingBoxXYZ box = null;
    try { box = el.get_BoundingBox(null); } catch { }
    if (box == null) { noBox++; continue; }

    bool horizontal = true;
    string kind = "point/other";
    var lc = el.Location as LocationCurve;
    if (lc != null && lc.Curve != null)
    {
        var p0 = lc.Curve.GetEndPoint(0);
        var p1 = lc.Curve.GetEndPoint(1);
        double dz = Math.Abs(p1.Z - p0.Z);
        double dxy = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
        if (dxy < 1e-6) { horizontal = false; kind = "vertical"; }
        else if (dz / dxy > 0.02) { horizontal = false; kind = "sloped"; }
        else kind = "horizontal";
    }
    items.Add((el, box, horizontal, kind));
}

int nonHorizontal = items.Count(i => !i.Horizontal);

sb.AppendLine($"VERTICAL CLEARANCE — rule {requiredVerticalMm:F0} mm clear between stacked services");
sb.AppendLine($"Checking {items.Count} element(s)" + (noBox > 0 ? $"  ({noBox} had no bounding box and were skipped — NOT a pass)" : ""));
if (nonHorizontal > 0)
    sb.AppendLine($"NOTE: {nonHorizontal} run(s) are sloped or vertical — their box spans the whole rise, so any gap reported for them is PESSIMISTIC. Check those in a section.");
sb.AppendLine();

// ---- connector relationships ----
Func<Element, HashSet<long>> connectedIdsOf = el =>
{
    var ids = new HashSet<long>();
    if (!ignoreConnected) return ids;
    ConnectorManager cm = null;
    var mc = el as MEPCurve;
    if (mc != null) cm = mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (cm == null && fi != null && fi.MEPModel != null) cm = fi.MEPModel.ConnectorManager;
    if (cm == null) return ids;
    try
    {
        foreach (Connector c in cm.Connectors)
            foreach (Connector r in c.AllRefs)
                if (r.Owner != null) ids.Add(IdValue(r.Owner.Id));
    }
    catch { }
    return ids;
};

// ---- the sweep ----
var findings = new List<(Element Upper, Element Lower, double GapMm, bool Clash, double OverlapMm)>();
int pairsTested = 0;

for (int i = 0; i < items.Count; i++)
{
    var a = items[i];
    var aConnected = connectedIdsOf(a.El);

    for (int j = i + 1; j < items.Count; j++)
    {
        var b = items[j];

        if (ignoreSameCategory && a.El.Category != null && b.El.Category != null &&
            IdValue(a.El.Category.Id) == IdValue(b.El.Category.Id)) continue;
        if (ignoreConnected && aConnected.Contains(IdValue(b.El.Id))) continue;

        // Plan overlap — the pair only stacks if their footprints share ground.
        double ox = Math.Min(a.Box.Max.X, b.Box.Max.X) - Math.Max(a.Box.Min.X, b.Box.Min.X);
        double oy = Math.Min(a.Box.Max.Y, b.Box.Max.Y) - Math.Max(a.Box.Min.Y, b.Box.Min.Y);
        if (ox < minOverlapFt || oy < minOverlapFt) continue;

        pairsTested++;

        // Which is on top, and the clear space between them.
        Element upper, lower;
        double gapFt;
        if (a.Box.Min.Z >= b.Box.Min.Z)
        {
            upper = a.El; lower = b.El;
            gapFt = a.Box.Min.Z - b.Box.Max.Z;
        }
        else
        {
            upper = b.El; lower = a.El;
            gapFt = b.Box.Min.Z - a.Box.Max.Z;
        }

        if (gapFt < requiredFt)
            findings.Add((upper, lower, ToMm(gapFt), gapFt < 0, ToMm(Math.Min(ox, oy))));
    }
}

sb.AppendLine($"STACKED PAIRS TESTED: {pairsTested}   TIGHTER THAN THE RULE: {findings.Count}   (of those, {findings.Count(f => f.Clash)} actually overlap in Z)");
sb.AppendLine();

if (findings.Count == 0)
{
    sb.AppendLine("CLEAR — every stacked pair has at least the required vertical space.");
    return sb.ToString();
}

sb.AppendLine("| Upper | Category | Lower | Category | Clear mm | Short by | Plan overlap mm | |");
sb.AppendLine("|---|---|---|---|---|---|---|---|");
foreach (var f in findings.OrderBy(f => f.GapMm).Take(maxReportedRows))
{
    string flag = f.Clash ? "CLASH" : "";
    sb.AppendLine($"| {f.Upper.Id} | {f.Upper.Category?.Name ?? "-"} | {f.Lower.Id} | {f.Lower.Category?.Name ?? "-"} | {f.GapMm:F0} | {(requiredVerticalMm - f.GapMm):F0} | {f.OverlapMm:F0} | {flag} |");
}
if (findings.Count > maxReportedRows)
    sb.AppendLine($"\n... and {findings.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
sb.AppendLine("Tightest by element:");
foreach (var g in findings.GroupBy(f => f.Upper.Id).OrderBy(g => g.Min(x => x.GapMm)).Take(10))
    sb.AppendLine($"  {g.Key} sits {g.Min(x => x.GapMm):F0} mm above its nearest service ({g.Count()} pair(s))");

return sb.ToString();
