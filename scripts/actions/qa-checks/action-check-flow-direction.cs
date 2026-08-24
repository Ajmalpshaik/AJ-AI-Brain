// ============================================================
// FRAGMENT (action) — action-check-flow-direction.cs
// PURPOSE: Check that the flow through the system in `elements` actually agrees with itself — every
//          joined pair of connectors should be one OUT meeting one IN. Two connectors both pushing OUT
//          at the same joint, or both pulling IN, is a system that cannot work as drawn, and it is
//          invisible on screen: the ducts touch, the fittings look right, and the direction arrows on
//          the drawing come out wrong.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the whole
//          system INCLUDING fittings, usually via filter-by-system-type.cs. Read-only.
//
// ✱✱ WHY THIS IS NOT VISIBLE ANY OTHER WAY. Revit does not refuse a back-to-front connection; it makes
//    the joint and carries on. The symptom appears much later — a terminal that shows no flow, a
//    pressure-drop report that stops halfway, an arrow annotation pointing up a branch instead of down
//    it. action-place-flow-arrows.cs DRAWS the direction; this one asks whether the direction is right.
//
// ✱✱ BIDIRECTIONAL IS NOT A FAULT, AND TREATING IT AS ONE MAKES THE CHECK USELESS. Plenty of real
//    connectors are Bidirectional by design — most fitting connectors are. A pair is only reported when
//    BOTH ends are committed to the same direction and those directions collide. Bidirectional pairs are
//    counted and passed over.
//
// ✱✱ IT ALSO CHECKS THE ENDS AGAINST THE SYSTEM'S OWN INTENT. A supply system should DELIVER at its
//    terminals and a return should COLLECT at them, so a terminal whose connector direction is the wrong
//    way round for the system it belongs to is reported separately as WRONG WAY FOR SYSTEM. That is the
//    error that survives every other check, because the joint itself is perfectly legal.
//
// GOTCHA: THE SET MUST INCLUDE THE FITTINGS. A run filtered to ducts alone has almost no connected pairs
//         inside it, so the check passes by having nothing to look at. The count of pairs examined is
//         printed first for exactly that reason — a low number against a big system means the filter,
//         not the model, is the problem.
// GOTCHA: DIRECTION IS SET BY THE FAMILY, so a fault here is usually fixed in the family editor, not in
//         the project. A whole family type reading the wrong way shows up as many rows sharing one type
//         name — the report groups by type at the end so that is obvious.
// GOTCHA: ELECTRICAL CONNECTORS have no meaningful flow direction and are skipped, counted separately.
// RELATED: action-place-flow-arrows.cs (draw the direction on the drawing),
//          action-report-connector-loads.cs (the engineering values on each connector),
//          action-check-system-connectivity.cs (whether it is joined up at all),
//          action-check-equipment-connectors.cs (equipment connectors against what they are joined to).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one reported pair against the family's
//   connector settings before treating a batch of rows as real.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Words in a system name that mean air/water is DELIVERED at the terminals.
var supplyHints = new List<string> { "supply", "sply", "sa", "chw supply", "cold", "hot water" };
// Words that mean it is COLLECTED at the terminals.
var returnHints = new List<string> { "return", "ret", "ra", "exhaust", "extract", "ea", "waste", "drain" };
bool checkTerminalDirection = true;   // the WRONG WAY FOR SYSTEM check
int maxReportedRows = 50;
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the system, INCLUDING its fittings).");
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

Func<Element, string> typeNameOf = el =>
{
    var te = Document.GetElement(el.GetTypeId());
    return te != null ? te.Name : "(no type)";
};

Func<Element, string> systemNameOf = el =>
{
    var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
    if (p != null && p.HasValue) { var v = p.AsString(); if (!string.IsNullOrWhiteSpace(v)) return v.Trim(); }
    // ✱✱ FIXED 2026-08-24 — `RBS_SYSTEM_TYPE_PARAM` IS NOT A REAL BuiltInParameter ON ANY REVIT VERSION.
    //    Compile-checked against the shipped RevitAPI.dll for 2020, 2024 and 2027: absent from all three,
    //    so this fragment could not compile and therefore could not run anywhere. The system TYPE is
    //    domain-specific, so ask duct first and pipe second — both exist on 2020 through 2027, and
    //    `get_Parameter` returns null when the element has neither, which the guard below already handles.
    var t = el.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)
         ?? el.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
    if (t != null && t.HasValue) { var v = t.AsValueString(); if (!string.IsNullOrWhiteSpace(v)) return v.Trim(); }
    return "";
};

var byId = new Dictionary<long, Element>();
foreach (var el in elements) byId[IdValue(el.Id)] = el;

// ---- walk every connected pair once ----
var conflicts = new List<(Element A, string ADir, Element B, string BDir, string SystemName)>();
var seenPairs = new HashSet<string>();
int pairsExamined = 0, bidirectional = 0, electricalSkipped = 0, agreed = 0;

foreach (var el in elements)
{
    var cm = managerOf(el);
    if (cm == null) continue;
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.Domain == Domain.DomainElectrical) { electricalSkipped++; continue; }
            if (!c.IsConnected) continue;

            FlowDirectionType dirA;
            try { dirA = c.Direction; } catch { continue; }

            foreach (Connector r in c.AllRefs)
            {
                if (r.Owner == null) continue;
                long otherId = IdValue(r.Owner.Id);
                if (otherId == IdValue(el.Id)) continue;
                if (!byId.ContainsKey(otherId)) continue;

                // Each pair once, whichever side reaches it first.
                long a = IdValue(el.Id), b = otherId;
                string key = a < b ? $"{a}-{b}-{c.Id}" : $"{b}-{a}-{r.Id}";
                if (seenPairs.Contains(key)) continue;
                seenPairs.Add(key);

                FlowDirectionType dirB;
                try { dirB = r.Direction; } catch { continue; }

                pairsExamined++;

                if (dirA == FlowDirectionType.Bidirectional || dirB == FlowDirectionType.Bidirectional)
                {
                    bidirectional++;
                    continue;
                }
                if (dirA == dirB)
                    conflicts.Add((el, dirA.ToString(), byId[otherId], dirB.ToString(), systemNameOf(el)));
                else
                    agreed++;
            }
        }
    }
    catch { }
}

// ---- terminals facing the wrong way for their system ----
var wrongWay = new List<(Element El, string Dir, string SystemName, string Expected)>();
if (checkTerminalDirection)
{
    foreach (var el in elements)
    {
        if (el.Category == null) continue;
        long cat = IdValue(el.Category.Id);
        bool isTerminal = cat == (long)BuiltInCategory.OST_DuctTerminal
                       || cat == (long)BuiltInCategory.OST_PlumbingFixtures
                       || cat == (long)BuiltInCategory.OST_Sprinklers;
        if (!isTerminal) continue;

        string sys = systemNameOf(el).ToLower();
        if (string.IsNullOrWhiteSpace(sys)) continue;

        bool looksSupply = supplyHints.Any(h => sys.Contains(h));
        bool looksReturn = returnHints.Any(h => sys.Contains(h));
        if (looksSupply == looksReturn) continue;   // ambiguous or unknown — say nothing rather than guess

        var cm = managerOf(el);
        if (cm == null) continue;
        try
        {
            foreach (Connector c in cm.Connectors)
            {
                if (c.Domain == Domain.DomainElectrical) continue;
                FlowDirectionType d;
                try { d = c.Direction; } catch { continue; }
                if (d == FlowDirectionType.Bidirectional) continue;

                // A supply terminal RECEIVES from the duct: its own connector reads In.
                // A return/exhaust terminal SENDS back up the duct: its own connector reads Out.
                string expected = looksSupply ? "In" : "Out";
                if (d.ToString() != expected)
                    wrongWay.Add((el, d.ToString(), systemNameOf(el), expected));
            }
        }
        catch { }
    }
}

// ---- report ----
sb.AppendLine($"FLOW DIRECTION — {elements.Count} element(s), {pairsExamined} connected pair(s) examined");
sb.AppendLine($"  agreed (one In meets one Out): {agreed}");
sb.AppendLine($"  bidirectional at one or both ends (not a fault): {bidirectional}");
sb.AppendLine($"  CONFLICTS (both ends the same direction): {conflicts.Count}");
if (checkTerminalDirection) sb.AppendLine($"  TERMINALS FACING THE WRONG WAY FOR THEIR SYSTEM: {wrongWay.Count}");
if (electricalSkipped > 0) sb.AppendLine($"  electrical connectors skipped (no flow direction): {electricalSkipped}");
if (pairsExamined < elements.Count / 2)
    sb.AppendLine("  NOTE: very few pairs for a set this size — does the filter include the FITTINGS? Without them there is almost nothing to check.");
sb.AppendLine();

if (conflicts.Count == 0 && wrongWay.Count == 0)
{
    sb.AppendLine("CLEAR — every committed pair meets one In to one Out, and no terminal faces the wrong way.");
    return sb.ToString();
}

if (conflicts.Count > 0)
{
    sb.AppendLine("CONFLICTS — both ends of the joint push the same way. This cannot flow as drawn:");
    sb.AppendLine("| Element | Its direction | Joined to | Its direction | System |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var c in conflicts.Take(maxReportedRows))
        sb.AppendLine($"| {c.A.Id} ({c.A.Category?.Name}) | {c.ADir} | {c.B.Id} ({c.B.Category?.Name}) | {c.BDir} | {(string.IsNullOrEmpty(c.SystemName) ? "-" : c.SystemName)} |");
    if (conflicts.Count > maxReportedRows) sb.AppendLine($"\n... and {conflicts.Count - maxReportedRows} more");

    var byType = conflicts.GroupBy(c => typeNameOf(c.A)).OrderByDescending(g => g.Count()).ToList();
    if (byType.Count > 0 && byType[0].Count() > 1)
    {
        sb.AppendLine();
        sb.AppendLine("Grouped by type — a type appearing many times is a FAMILY problem, fix it once in the family editor:");
        foreach (var g in byType.Take(10)) sb.AppendLine($"  {g.Key}: {g.Count()}");
    }
}

if (wrongWay.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("WRONG WAY FOR SYSTEM — the joint is legal, but the device faces the wrong way for what its system does:");
    sb.AppendLine("| Element | Category | Reads | Should read | System |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var w in wrongWay.Take(maxReportedRows))
        sb.AppendLine($"| {w.El.Id} | {w.El.Category?.Name} | {w.Dir} | {w.Expected} | {w.SystemName} |");
    if (wrongWay.Count > maxReportedRows) sb.AppendLine($"\n... and {wrongWay.Count - maxReportedRows} more");
    sb.AppendLine();
    sb.AppendLine("If a whole family type is listed, the direction is set wrong in the FAMILY — fixing it there fixes every instance.");
}

return sb.ToString();
