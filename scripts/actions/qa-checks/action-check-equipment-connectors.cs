// ============================================================
// FRAGMENT (action) — action-check-equipment-connectors.cs
// PURPOSE: Check every connector on the equipment in `elements` against what is actually plugged into it
//          — size, shape and domain — and report the mismatches. The "the AHU has a 600x400 supply spigot
//          and somebody ran 400x300 off it" defect, which Revit will happily build with a transition and
//          nobody sees until the airflow does not arrive.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          equipment, e.g. filter-by-category.cs with OST_MechanicalEquipment. Read-only.
//
// ✱✱ IT REPORTS THE UNCONNECTED SPIGOTS TOO, and those are usually the bigger finding. An AHU with four
//    connectors and two of them joined to nothing is a coordination gap that no clash test and no
//    connectivity walk will flag as an error — the system that IS connected traces perfectly. Every
//    connector is listed with its state, so a missing service is visible as an absence.
//
// ✱✱ SIZE COMPARISON IS SHAPE-AWARE. A round connector reports a diameter and a rectangular one reports
//    width x height; comparing them as one number is how a check like this produces confident nonsense.
//    Round-to-round compares diameters, rectangular-to-rectangular compares both dimensions, and a
//    round-to-rectangular joint is reported as SHAPE CHANGE rather than pretending to compare it — a
//    transition there may be entirely correct, but it should be a decision, not an accident.
//
// ✱✱ DOMAIN MISMATCH IS REPORTED AS ITS OWN CLASS because it is a different kind of wrong. A pipe joined
//    to a duct connector is not a tolerance problem; it is a modelling error that usually means the wrong
//    family was placed.
//
// GOTCHA: THIS READS WHAT THE FAMILY DECLARES. If the equipment family's connectors were drawn at the
//         wrong size, everything here will agree with each other and all be wrong together. Check the
//         first equipment of each type against its datasheet once — after that the check is worth
//         trusting for the rest of that type.
// GOTCHA: A CONNECTOR JOINED THROUGH A TRANSITION FITTING is compared against the FITTING, which is the
//         honest answer — that is what is physically bolted to the spigot. The transition's far side is
//         a separate joint and is not this fragment's business.
// GOTCHA: EQUIPMENT WITH NO MEPModel — a Generic Model used as equipment, or an unhosted placeholder —
//         has no connectors at all. Those are counted and named, because "0 mismatches" across a set
//         that carries no connectors is not a pass.
// RELATED: action-report-connectors.cs (the plain listing, all elements, no judgement),
//          action-report-connector-loads.cs (the engineering values), action-check-flow-direction.cs
//          (which way the flow goes), action-check-system-connectivity.cs (is it joined up at all).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one reported mismatch against the
//   equipment in a section before treating the list as real.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double sizeToleranceMm = 5;       // how far two sizes may differ before it counts as a mismatch
bool reportUnconnected = true;    // list spigots joined to nothing (usually the bigger finding)
bool reportMatches = false;       // true = list the connectors that are fine too, for a full record
int maxReportedRows = 80;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the equipment).");
    return sb.ToString();
}

Func<Element, ConnectorManager> managerOf = el =>
{
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    return null;
};

// A shape-aware description, and a shape-aware comparison. Radius throws on a rectangular connector and
// Width/Height throw on a round one, so every read is guarded.
Func<Connector, string> shapeOf = c =>
{
    try { return c.Shape.ToString(); } catch { return "unknown"; }
};

Func<Connector, string> sizeTextOf = c =>
{
    try
    {
        if (c.Shape == ConnectorProfileType.Round) return $"{ToMm(c.Radius * 2.0):F0} dia";
        if (c.Shape == ConnectorProfileType.Rectangular || c.Shape == ConnectorProfileType.Oval)
            return $"{ToMm(c.Width):F0}x{ToMm(c.Height):F0}";
    }
    catch { }
    return "(no size)";
};

// Returns: 0 = same, 1 = different size, 2 = different shape, -1 = cannot tell
Func<Connector, Connector, int> compareSize = (a, b) =>
{
    try
    {
        if (a.Shape != b.Shape) return 2;
        double tolFt = sizeToleranceMm / MM_PER_FOOT;
        if (a.Shape == ConnectorProfileType.Round)
            return Math.Abs(a.Radius - b.Radius) * 2.0 > tolFt ? 1 : 0;
        if (a.Shape == ConnectorProfileType.Rectangular || a.Shape == ConnectorProfileType.Oval)
            return (Math.Abs(a.Width - b.Width) > tolFt || Math.Abs(a.Height - b.Height) > tolFt) ? 1 : 0;
    }
    catch { }
    return -1;
};

// ---- sweep ----
var sizeMismatch = new List<(Element Eq, string Con, string EqSize, Element Nb, string NbSize)>();
var shapeChange = new List<(Element Eq, string Con, string EqSize, Element Nb, string NbSize)>();
var domainMismatch = new List<(Element Eq, string Con, string EqDom, Element Nb, string NbDom)>();
var unconnected = new List<(Element Eq, string Con, string Size, string Domain)>();
var matched = new List<(Element Eq, string Con, string Size, Element Nb)>();
var noConnectors = new List<Element>();

int connectorsSeen = 0;

foreach (var eq in elements)
{
    var cm = managerOf(eq);
    if (cm == null) { noConnectors.Add(eq); continue; }

    int onThis = 0;
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.ConnectorType != ConnectorType.End && c.ConnectorType != ConnectorType.Curve) continue;
            onThis++;
            connectorsSeen++;

            string label = $"{c.Domain}";
            try { label = $"{c.Domain} #{c.Id}"; } catch { }
            string mySize = sizeTextOf(c);

            bool connected;
            try { connected = c.IsConnected; } catch { connected = false; }

            if (!connected)
            {
                if (reportUnconnected) unconnected.Add((eq, label, mySize, c.Domain.ToString()));
                continue;
            }

            // Compare against whatever is really on the other side.
            foreach (Connector r in c.AllRefs)
            {
                if (r.Owner == null) continue;
                if (r.Owner.Id == eq.Id) continue;

                if (r.Domain != c.Domain)
                {
                    domainMismatch.Add((eq, label, c.Domain.ToString(), r.Owner, r.Domain.ToString()));
                    continue;
                }

                int cmp = compareSize(c, r);
                if (cmp == 1) sizeMismatch.Add((eq, label, mySize, r.Owner, sizeTextOf(r)));
                else if (cmp == 2) shapeChange.Add((eq, label, mySize + " " + shapeOf(c), r.Owner, sizeTextOf(r) + " " + shapeOf(r)));
                else if (cmp == 0) matched.Add((eq, label, mySize, r.Owner));
            }
        }
    }
    catch { }
    if (onThis == 0) noConnectors.Add(eq);
}

// ---- report ----
sb.AppendLine($"EQUIPMENT CONNECTORS — {elements.Count} equipment item(s), {connectorsSeen} connector(s) read");
sb.AppendLine($"  SIZE MISMATCH:   {sizeMismatch.Count}");
sb.AppendLine($"  SHAPE CHANGE:    {shapeChange.Count}");
sb.AppendLine($"  DOMAIN MISMATCH: {domainMismatch.Count}");
sb.AppendLine($"  connected and correct: {matched.Count}");
if (reportUnconnected) sb.AppendLine($"  SPIGOTS JOINED TO NOTHING: {unconnected.Count}");
if (noConnectors.Count > 0)
    sb.AppendLine($"  NO CONNECTORS AT ALL: {noConnectors.Count} item(s) — these were NOT checked and are not a pass: " +
                  string.Join(", ", noConnectors.Take(12).Select(e => e.Id.ToString())) + (noConnectors.Count > 12 ? " ..." : ""));
sb.AppendLine();

bool anything = sizeMismatch.Count > 0 || shapeChange.Count > 0 || domainMismatch.Count > 0 || (reportUnconnected && unconnected.Count > 0);
if (!anything)
{
    sb.AppendLine("CLEAR — every connected spigot matches what is joined to it.");
    if (!reportMatches) return sb.ToString();
}

if (domainMismatch.Count > 0)
{
    sb.AppendLine("DOMAIN MISMATCH — the wrong service is plugged in. Usually the wrong family was placed:");
    sb.AppendLine("| Equipment | Connector | Its domain | Joined to | Its domain |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var d in domainMismatch.Take(maxReportedRows))
        sb.AppendLine($"| {d.Eq.Id} ({d.Eq.Name}) | {d.Con} | {d.EqDom} | {d.Nb.Id} ({d.Nb.Category?.Name}) | {d.NbDom} |");
    sb.AppendLine();
}

if (sizeMismatch.Count > 0)
{
    sb.AppendLine("SIZE MISMATCH — the service does not match the spigot it is on:");
    sb.AppendLine("| Equipment | Connector | Spigot size | Joined to | Its size |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var m in sizeMismatch.Take(maxReportedRows))
        sb.AppendLine($"| {m.Eq.Id} ({m.Eq.Name}) | {m.Con} | {m.EqSize} | {m.Nb.Id} ({m.Nb.Category?.Name}) | {m.NbSize} |");
    if (sizeMismatch.Count > maxReportedRows) sb.AppendLine($"\n... and {sizeMismatch.Count - maxReportedRows} more");
    sb.AppendLine();
}

if (shapeChange.Count > 0)
{
    sb.AppendLine("SHAPE CHANGE — round meets rectangular at the equipment. May be right, but it should be deliberate:");
    sb.AppendLine("| Equipment | Connector | Spigot | Joined to | Its shape |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var s in shapeChange.Take(maxReportedRows))
        sb.AppendLine($"| {s.Eq.Id} ({s.Eq.Name}) | {s.Con} | {s.EqSize} | {s.Nb.Id} ({s.Nb.Category?.Name}) | {s.NbSize} |");
    sb.AppendLine();
}

if (reportUnconnected && unconnected.Count > 0)
{
    sb.AppendLine("SPIGOTS JOINED TO NOTHING — a service that was never run. No clash test or trace will flag these:");
    sb.AppendLine("| Equipment | Connector | Size | Domain |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var u in unconnected.Take(maxReportedRows))
        sb.AppendLine($"| {u.Eq.Id} ({u.Eq.Name}) | {u.Con} | {u.Size} | {u.Domain} |");
    if (unconnected.Count > maxReportedRows) sb.AppendLine($"\n... and {unconnected.Count - maxReportedRows} more");

    sb.AppendLine();
    sb.AppendLine("By equipment — items with the most services missing:");
    foreach (var g in unconnected.GroupBy(u => u.Eq.Id).OrderByDescending(g => g.Count()).Take(10))
        sb.AppendLine($"  {g.Key}: {g.Count()} spigot(s) unconnected");
    sb.AppendLine();
}

if (reportMatches && matched.Count > 0)
{
    sb.AppendLine("CORRECT — for the record:");
    foreach (var m in matched.Take(maxReportedRows))
        sb.AppendLine($"  {m.Eq.Id} {m.Con} {m.Size} -> {m.Nb.Id} ({m.Nb.Category?.Name})");
}

return sb.ToString();
