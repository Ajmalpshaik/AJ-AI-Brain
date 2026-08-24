// ============================================================
// FRAGMENT (action) — action-check-plumbing-fixture-connectivity.cs
// PURPOSE: Check that every plumbing fixture is actually plumbed — each of its connectors (cold, hot,
//          sanitary, vent) either joined to the right kind of system or reported as missing, and each
//          connected one traced to see whether it reaches a real source or outfall rather than stopping
//          in mid-air three fittings away. Covers BOTH the supply side and the DRAINAGE side, because a
//          fixture has both and checking one without the other passes half a defect.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          FIXTURES, e.g. filter-by-category.cs with OST_PlumbingFixtures. Read-only.
//
// ✱✱ IT REPORTS BY SERVICE, WHICH IS THE ONLY USEFUL FORM. "12 fixtures have an unconnected connector"
//    is not actionable; "9 WCs have no vent and 3 basins have no hot water" is. Every connector is
//    classified by its `PipeSystemType` — Domestic Cold Water, Domestic Hot Water, Sanitary, Vent and the
//    rest — and the table is one row per service per fixture.
//
// ✱✱ CONNECTED IS NOT THE SAME AS FED, AND BOTH ARE CHECKED. A basin joined to 400 mm of pipe that ends
//    in nothing is "connected" by every simple test. Each connected service is walked outward through the
//    network until it reaches something that counts as a source or an outfall — equipment, a tank, a
//    stack, or simply a long enough run to be the real system — and a service that runs out before then
//    is reported as REACHES NOTHING, which is a different defect from NOT CONNECTED and gets fixed
//    differently.
//
// ✱✱ WHAT EACH FIXTURE SHOULD HAVE IS AN INPUT, because it varies by fixture and by specification. A WC
//    on a mains-fed system has cold and sanitary and no hot; a basin has all four in some jobs and three
//    in others. `expected` matches on the fixture's family or type name and lists the services it must
//    have. A fixture matching no rule is CHECKED FOR WHAT IT HAS but not failed for what it lacks, and is
//    listed so the rule set can be extended.
//
// GOTCHA: `Connector.IsConnected` describes intent, not always physical reality (START-HERE.md rule 1).
//         A connector flagged connected while the pipe is 50 mm away reads as fine here. Where the
//         geometry is the question, skills/ajtools-mep-trace is the route.
// GOTCHA: THE WALK IS LIMITED TO THE HOST MODEL. A fixture served from a link reports REACHES NOTHING.
// GOTCHA: SOME FAMILIES CARRY NO CONNECTORS AT ALL — a fixture modelled as a Generic Model, or a
//         placeholder. Those are counted and named, because "0 problems" across a set with no connectors
//         is not a pass.
// GOTCHA: VENT IS OFTEN DELIBERATELY ABSENT on a stub-vented or air-admittance system. Leave it out of
//         `expected` rather than accepting a page of false failures.
// RELATED: action-check-system-connectivity.cs (whole-system islands, any domain),
//          action-find-dead-end-system.cs (runs that serve nothing),
//          action-check-slope.cs (whether the drainage that IS connected actually falls),
//          action-check-equipment-connectors.cs (the same question at the plant end).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one fixture's reported services against
//   what you can see in Revit before trusting a whole floor.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Fixture family/type name substring -> the services it must have.
var expected = new List<(string NameContains, List<string> Services)>
{
    ("wc",     new List<string> { "DomesticColdWater", "Sanitary" }),
    ("toilet", new List<string> { "DomesticColdWater", "Sanitary" }),
    ("basin",  new List<string> { "DomesticColdWater", "DomesticHotWater", "Sanitary" }),
    ("lavatory", new List<string> { "DomesticColdWater", "DomesticHotWater", "Sanitary" }),
    ("sink",   new List<string> { "DomesticColdWater", "DomesticHotWater", "Sanitary" }),
    ("shower", new List<string> { "DomesticColdWater", "DomesticHotWater", "Sanitary" }),
    ("urinal", new List<string> { "DomesticColdWater", "Sanitary" }),
};

// What counts as reaching a real system at the far end of a walk.
// Names that exist on EVERY supported Revit go here directly.
var sourceCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_MechanicalEquipment,
};

// `OST_PlumbingEquipment` DOES NOT EXIST ON REVIT 2020 — it arrived at 2024. Naming a missing enum
// member is a COMPILE error, so a try/catch around it cannot help: the file simply will not build on
// 2020, and one fragment that fails to compile is one the whole library cannot ship to that version.
// Resolving it BY NAME at run time is the version-proof route the rest of this library uses for the
// same problem (the ElementId.Value/IntegerValue and IndependentTag lookups) — on 2020 the parse fails,
// the category is skipped, and everything else still works.
var optionalSourceCategories = new List<string> { "OST_PlumbingEquipment" };
foreach (var name in optionalSourceCategories)
{
    try
    {
        BuiltInCategory parsed;
        if (Enum.TryParse(name, out parsed) && Enum.IsDefined(typeof(BuiltInCategory), parsed))
            sourceCategories.Add(parsed);
    }
    catch { }
}
int walkLimit = 60;             // pieces to follow before calling a run "real system" rather than a stub
double minRealRunMm = 3000;     // total run length that also counts as reaching the real system
int maxReportedRows = 80;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the plumbing fixtures).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

Func<Element, ConnectorManager> managerOf = el =>
{
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    return null;
};

var sourceCatIds = new HashSet<long>();
foreach (var c in sourceCategories) sourceCatIds.Add((long)c);

Func<Element, double> lengthMmOf = el =>
{
    var p = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
    if (p != null && p.HasValue) return ToMm(p.AsDouble());
    return 0;
};

// Follow a connected service outward and decide whether it reaches a real system.
Func<Connector, Element, (bool Reaches, int Pieces, double LengthMm, string Why)> walkOut = (start, owner) =>
{
    var visited = new HashSet<long> { IdValue(owner.Id) };
    var queue = new Queue<Element>();
    foreach (Connector r in start.AllRefs)
        if (r.Owner != null && IdValue(r.Owner.Id) != IdValue(owner.Id))
        {
            visited.Add(IdValue(r.Owner.Id));
            queue.Enqueue(r.Owner);
        }

    int pieces = 0;
    double totalMm = 0;

    while (queue.Count > 0 && pieces < walkLimit)
    {
        var cur = queue.Dequeue();
        pieces++;
        totalMm += lengthMmOf(cur);

        if (cur.Category != null && sourceCatIds.Contains(IdValue(cur.Category.Id)))
            return (true, pieces, totalMm, $"reaches {cur.Category.Name} {cur.Id}");

        if (totalMm >= minRealRunMm)
            return (true, pieces, totalMm, $"{totalMm / 1000.0:F1} m of run — the real system");

        var cm = managerOf(cur);
        if (cm == null) continue;
        try
        {
            foreach (Connector c in cm.Connectors)
            {
                if (!c.IsConnected) continue;
                foreach (Connector r in c.AllRefs)
                {
                    if (r.Owner == null) continue;
                    long oid = IdValue(r.Owner.Id);
                    if (visited.Contains(oid)) continue;
                    visited.Add(oid);
                    queue.Enqueue(r.Owner);
                }
            }
        }
        catch { }
    }

    if (pieces >= walkLimit) return (true, pieces, totalMm, "long network");
    return (false, pieces, totalMm, $"runs out after {pieces} piece(s), {totalMm:F0} mm");
};

// ---- check each fixture ----
var missing = new List<(Element Fx, string Name, string Service)>();
var deadService = new List<(Element Fx, string Name, string Service, string Why)>();
var present = new List<(Element Fx, string Service)>();
var noConnectors = new List<Element>();
var noRule = new List<string>();
int fixturesChecked = 0, fixturesClean = 0;

foreach (var fx in elements)
{
    var fi = fx as FamilyInstance;
    string famName = fi != null && fi.Symbol != null && fi.Symbol.Family != null ? fi.Symbol.Family.Name : "";
    string typName = fi != null && fi.Symbol != null ? fi.Symbol.Name : (fx.Name ?? "");
    string label = $"{famName} : {typName}".Trim(' ', ':');
    string hay = (famName + " " + typName).ToLower();

    var cm = managerOf(fx);
    if (cm == null) { noConnectors.Add(fx); continue; }

    // What this fixture actually HAS, by service.
    var have = new Dictionary<string, Connector>();
    int connectorCount = 0;
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.Domain != Domain.DomainPiping) continue;
            connectorCount++;
            string svc;
            try { svc = c.PipeSystemType.ToString(); } catch { svc = "Unknown"; }
            if (!have.ContainsKey(svc)) have[svc] = c;
        }
    }
    catch { }

    if (connectorCount == 0) { noConnectors.Add(fx); continue; }
    fixturesChecked++;
    bool clean = true;

    // ---- what it SHOULD have ----
    var rule = expected.FirstOrDefault(e => hay.IndexOf(e.NameContains, StringComparison.OrdinalIgnoreCase) >= 0);
    if (rule.Services == null)
    {
        if (!noRule.Contains(label)) noRule.Add(label);
    }
    else
    {
        foreach (var svc in rule.Services)
        {
            if (!have.ContainsKey(svc))
            {
                missing.Add((fx, label, svc));
                clean = false;
            }
        }
    }

    // ---- of what it has, is each one actually joined and fed? ----
    foreach (var kv in have)
    {
        bool connected;
        try { connected = kv.Value.IsConnected; } catch { connected = false; }

        if (!connected)
        {
            missing.Add((fx, label, kv.Key + " (present but NOT CONNECTED)"));
            clean = false;
            continue;
        }

        var w = walkOut(kv.Value, fx);
        if (!w.Reaches)
        {
            deadService.Add((fx, label, kv.Key, w.Why));
            clean = false;
        }
        else present.Add((fx, kv.Key));
    }

    if (clean) fixturesClean++;
}

// ---- report ----
sb.AppendLine($"PLUMBING FIXTURE CONNECTIVITY — {elements.Count} fixture(s)");
sb.AppendLine($"CHECKED: {fixturesChecked}   FULLY PLUMBED: {fixturesClean}   WITH A PROBLEM: {fixturesChecked - fixturesClean}");
sb.AppendLine($"  MISSING OR UNCONNECTED SERVICES: {missing.Count}");
sb.AppendLine($"  CONNECTED BUT REACHING NOTHING:  {deadService.Count}");
if (noConnectors.Count > 0)
    sb.AppendLine($"  NO PIPING CONNECTORS AT ALL: {noConnectors.Count} fixture(s) — NOT checked and NOT a pass: " +
                  string.Join(", ", noConnectors.Take(15).Select(e => e.Id.ToString())) + (noConnectors.Count > 15 ? " ..." : ""));
if (noRule.Count > 0)
{
    sb.AppendLine($"  NO RULE MATCHED for {noRule.Count} fixture type(s) — checked for what they have, NOT failed for what they lack:");
    foreach (var n in noRule.Take(10)) sb.AppendLine($"      {n}");
    if (noRule.Count > 10) sb.AppendLine($"      ... and {noRule.Count - 10} more");
}
sb.AppendLine();

if (missing.Count == 0 && deadService.Count == 0)
{
    sb.AppendLine("CLEAR — every fixture has the services its rule requires, and each one reaches a real system.");
    return sb.ToString();
}

if (missing.Count > 0)
{
    sb.AppendLine("MISSING OR UNCONNECTED:");
    sb.AppendLine("| Fixture | Type | Service |");
    sb.AppendLine("|---|---|---|");
    foreach (var m in missing.OrderBy(m => m.Service).Take(maxReportedRows))
        sb.AppendLine($"| {m.Fx.Id} | {m.Name} | {m.Service} |");
    if (missing.Count > maxReportedRows) sb.AppendLine($"\n... and {missing.Count - maxReportedRows} more");

    sb.AppendLine();
    sb.AppendLine("By service:");
    foreach (var g in missing.GroupBy(m => m.Service).OrderByDescending(g => g.Count()))
        sb.AppendLine($"  {g.Key}: {g.Count()} fixture(s)");
    sb.AppendLine();
}

if (deadService.Count > 0)
{
    sb.AppendLine("CONNECTED BUT REACHING NOTHING — a stub of pipe that goes nowhere. Passes a simple connectivity test:");
    sb.AppendLine("| Fixture | Type | Service | What the walk found |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var d in deadService.Take(maxReportedRows))
        sb.AppendLine($"| {d.Fx.Id} | {d.Name} | {d.Service} | {d.Why} |");
    if (deadService.Count > maxReportedRows) sb.AppendLine($"\n... and {deadService.Count - maxReportedRows} more");

    sb.AppendLine();
    sb.AppendLine("By service:");
    foreach (var g in deadService.GroupBy(d => d.Service).OrderByDescending(g => g.Count()))
        sb.AppendLine($"  {g.Key}: {g.Count()} fixture(s)");
}

return sb.ToString();
