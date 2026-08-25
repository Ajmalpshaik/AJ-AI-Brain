// ============================================================
// FRAGMENT (action) — action-find-dead-end-system.cs
// PURPOSE: Find the runs in `elements` that STOP AND SERVE NOTHING — a duct that ends in mid-air, a
//          branch left behind when the layout changed, a spur drawn to a terminal that was later deleted.
//          The difference between this and a plain open-end sweep is that it sorts the deliberate ends
//          from the accidental ones and only reports the accidents.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the system
//          INCLUDING its fittings and accessories. Read-only. The model never changes.
// ✱✱ FOUR FRAGMENTS ANSWER "OPEN ENDS". Pick by the sentence — only the last two can change the model:
//      filters/by-relationship/filter-by-connection-status.cs        FILTER: narrow any category to the
//                                                                    elements with an open connector.
//      actions/qa-checks/action-find-dead-end-system.cs              REPORT: sorts DELIBERATE ends from
//                                                                    accidents, lists only the accidents.
//      actions/qa-checks/action-check-open-pipe-ends.cs              PIPES: report open ends, and cap
//                                                                    them only if you say so.
//      actions/structural-changes/action-connect-open-connectors.cs  JOIN: connects open pairs that
//                                                                    already touch — WRITES, dry-run first.
//
// ✱✱ MOST OPEN ENDS ARE MEANT TO BE THERE, WHICH IS WHY A RAW OPEN-END LIST IS IGNORED. A run that ends
//    at an air terminal, a plumbing fixture, a piece of equipment or a cap is finished, not broken.
//    Reporting all of those as faults is how a QA sweep gets switched off. Every open end is classified
//    first and only the UNEXPLAINED ones are reported:
//      SERVED    — the end sits at/next to a terminal, fixture or equipment. Fine.
//      CAPPED    — a cap fitting closes it. Fine.
//      STUB      — very short and open; usually a deliberate future connection. Reported quietly.
//      DEAD END  — a real run that ends at nothing. THIS is the finding.
//
// ✱✱ "SERVES NOTHING" IS DECIDED BY WALKING, NOT BY LOOKING AT THE LAST ELEMENT. A branch can end at a
//    fitting that ends at another fitting that ends at nothing. The walk follows the chain from each open
//    end back through the connected pieces until it either finds something served or runs out — so a
//    three-fitting tail with nothing on the end is caught, where a one-hop check would call the fitting
//    an explanation and move on.
//
// ✱✱ THE LENGTH OF WHAT IS WASTED IS REPORTED, because that is what makes the finding actionable. A
//    200 mm dead stub is a nothing; 14 m of 400 mm duct feeding nowhere is a real cost and an obvious
//    modelling error, and the two look identical in a list of element Ids.
//
// GOTCHA: THE WALK IS LIMITED TO `elements`. A branch whose terminal is not in your filter reads as a
//         dead end that isn't one. Include the terminal/fixture/equipment categories in the filter — the
//         report says how many neighbours it reached outside the set, which is how that mistake surfaces.
// GOTCHA: A CAP IS DETECTED BY CATEGORY AND FAMILY NAME, so a cap family named something unusual will not
//         be recognised and its run will read as a dead end. `capNameHints` is editable for exactly that.
// GOTCHA: `Connector.IsConnected` describes intent, not always physical reality (START-HERE.md rule 1).
//         A run whose end is FLAGGED connected but is physically 50 mm short of its neighbour is invisible
//         here — that is skills/ajtools-mep-trace territory.
// RELATED: action-check-open-pipe-ends.cs (every open pipe end, and caps them — the pipe-specific tool),
//          action-check-system-connectivity.cs (whole islands that are fed by nothing),
//          action-connect-open-connectors.cs (join an end that should have been joined).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one reported dead end in Revit before
//   trusting a whole-system sweep.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// An open end sitting at one of these is SERVED, not a fault.
var servedByCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctTerminal,
    BuiltInCategory.OST_MechanicalEquipment,
    BuiltInCategory.OST_PlumbingFixtures,
    BuiltInCategory.OST_Sprinklers,
};
var capNameHints = new List<string> { "cap", "plug", "blank", "end cap" };
double stubLengthMm = 500;       // an open run shorter than this is a STUB, reported quietly
double proximityMm = 600;        // how near a served element has to be to explain an open end
int maxReportedRows = 50;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the system, INCLUDING its terminals and fittings).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

Func<Element, ConnectorManager> managerOf = el =>
{
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    return null;
};

var byId = new Dictionary<long, Element>();
foreach (var el in elements) byId[IdValue(el.Id)] = el;

var servedCatIds = new HashSet<long>();
foreach (var c in servedByCategories) servedCatIds.Add((long)c);

Func<Element, bool> isServedThing = el =>
    el != null && el.Category != null && servedCatIds.Contains(IdValue(el.Category.Id));

Func<Element, bool> isCap = el =>
{
    if (el == null) return false;
    string nm = (el.Name ?? "").ToLower();
    var fi = el as FamilyInstance;
    string fam = fi != null && fi.Symbol != null && fi.Symbol.Family != null ? fi.Symbol.Family.Name.ToLower() : "";
    foreach (var hint in capNameHints)
        if (nm.Contains(hint) || fam.Contains(hint)) return true;
    return false;
};

Func<Element, double> lengthMmOf = el =>
{
    var lc = el.Location as LocationCurve;
    if (lc != null && lc.Curve != null) return ToMm(lc.Curve.Length);
    var p = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
    if (p != null && p.HasValue) return ToMm(p.AsDouble());
    return 0;
};

// Anything nearby that would explain an open end, even if it is not connected to it.
var nearbyServed = new List<(Element El, XYZ Pt)>();
foreach (var el in elements)
{
    if (!isServedThing(el)) continue;
    XYZ pt = null;
    var lp = el.Location as LocationPoint;
    if (lp != null) pt = lp.Point;
    else
    {
        BoundingBoxXYZ bb = null;
        try { bb = el.get_BoundingBox(null); } catch { }
        if (bb != null) pt = (bb.Min + bb.Max) * 0.5;
    }
    if (pt != null) nearbyServed.Add((el, pt));
}

// ---- find every open end, and classify it ----
int outsideReached = 0;
var findings = new List<(Element El, XYZ At, string Verdict, double TailLengthMm, int TailPieces, string Detail)>();
int served = 0, capped = 0, stubs = 0;

foreach (var el in elements)
{
    var cm = managerOf(el);
    if (cm == null) continue;

    List<Connector> openEnds = new List<Connector>();
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.ConnectorType != ConnectorType.End) continue;
            if (c.Domain == Domain.DomainUndefined) continue;
            if (!c.IsConnected) openEnds.Add(c);
        }
    }
    catch { continue; }

    foreach (var oc in openEnds)
    {
        XYZ at;
        try { at = oc.Origin; } catch { continue; }

        // Is this open end explained by something sitting at it?
        var near = nearbyServed
            .Where(s => s.El.Id != el.Id && s.Pt.DistanceTo(at) <= ToFeet(proximityMm))
            .OrderBy(s => s.Pt.DistanceTo(at))
            .FirstOrDefault();
        if (near.El != null) { served++; continue; }

        // Walk back from this element through its connected neighbours, to see whether the TAIL this
        // end belongs to reaches anything served. One hop is not enough — a tail of fittings would
        // otherwise explain itself.
        var visited = new HashSet<long> { IdValue(el.Id) };
        var queue = new Queue<Element>();
        queue.Enqueue(el);
        bool tailIsServed = false, tailHasCap = false;
        double tailLength = 0;
        int tailPieces = 0;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            tailPieces++;
            tailLength += lengthMmOf(cur);
            if (isServedThing(cur)) { tailIsServed = true; break; }
            if (isCap(cur)) tailHasCap = true;

            var curCm = managerOf(cur);
            if (curCm == null) continue;
            try
            {
                foreach (Connector c in curCm.Connectors)
                {
                    if (!c.IsConnected) continue;
                    foreach (Connector r in c.AllRefs)
                    {
                        if (r.Owner == null) continue;
                        long oid = IdValue(r.Owner.Id);
                        if (visited.Contains(oid)) continue;
                        if (!byId.ContainsKey(oid)) { outsideReached++; continue; }
                        visited.Add(oid);
                        queue.Enqueue(byId[oid]);
                    }
                }
            }
            catch { }
            // A tail that runs on and on is not a dead end, it is the system. Stop early.
            if (tailPieces > 40) { tailIsServed = true; break; }
        }

        if (tailIsServed) { served++; continue; }
        if (tailHasCap) { capped++; continue; }

        double runLen = lengthMmOf(el);
        if (runLen > 0 && runLen < stubLengthMm && tailPieces <= 2) { stubs++; continue; }

        findings.Add((el, at, "DEAD END", tailLength, tailPieces,
            $"at ({ToMm(at.X):F0}, {ToMm(at.Y):F0}, {ToMm(at.Z):F0}) mm"));
    }
}

// ---- report ----
sb.AppendLine($"DEAD-END SWEEP — {elements.Count} element(s) checked");
sb.AppendLine($"Open ends explained: {served} served (terminal/fixture/equipment), {capped} capped, {stubs} short stub(s)");
sb.AppendLine($"UNEXPLAINED DEAD ENDS: {findings.Count}");
if (outsideReached > 0)
    sb.AppendLine($"NOTE: the walk hit {outsideReached} connection(s) leading OUTSIDE your filter. A tail whose terminal is not in the set will read as a dead end — widen the filter before acting.");
sb.AppendLine();

if (findings.Count == 0)
{
    sb.AppendLine("CLEAR — every open end is explained by something it serves, a cap, or is a short stub.");
    return sb.ToString();
}

sb.AppendLine("| Element | Category | Size | Wasted run mm | Pieces in tail | Open end at |");
sb.AppendLine("|---|---|---|---|---|---|");
foreach (var f in findings.OrderByDescending(f => f.TailLengthMm).Take(maxReportedRows))
{
    string size = "";
    var sp = f.El.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
    if (sp != null && sp.HasValue) { try { size = sp.AsString() ?? ""; } catch { } }
    sb.AppendLine($"| {f.El.Id} | {f.El.Category?.Name ?? "-"} | {size} | {f.TailLengthMm:F0} | {f.TailPieces} | {f.Detail} |");
}
if (findings.Count > maxReportedRows)
    sb.AppendLine($"\n... and {findings.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
sb.AppendLine($"Total run feeding nothing: {findings.Sum(f => f.TailLengthMm) / 1000.0:F1} m across {findings.Count} dead end(s).");
sb.AppendLine("Each of these either wants deleting, capping, or joining to what it was meant to serve — action-connect-open-connectors.cs for the last of those.");

return sb.ToString();
