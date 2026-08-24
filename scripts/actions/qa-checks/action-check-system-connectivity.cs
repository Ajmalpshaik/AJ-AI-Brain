// ============================================================
// FRAGMENT (action) — action-check-system-connectivity.cs
// PURPOSE: Walk the whole set through its connectors and report how many SEPARATE PIECES it is really in.
//          A system that looks like one network on screen is very often three: the main run, a branch
//          that was copied and never rejoined, and a spur that lost its fitting. This finds each island,
//          says how big it is, and — the part that matters — says which islands contain NO source
//          equipment, because those are the branches that will never see air or water.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the whole
//          system, e.g. filter-by-system-type.cs or filter-by-multiple-categories.cs over the duct or
//          pipe categories. Read-only. The model never changes.
//
// ✱✱ THIS IS THE GENERAL, ANY-DOMAIN VERSION. recipes/verify-duct-connectivity.cs is the proven, specific
//    one: it traces each air terminal's chain back to its FCU and is the right tool for that job. This
//    fragment asks a different question — "how many disconnected pieces is this system in AT ALL" —
//    across duct, pipe, tray or conduit, without needing to know what the endpoints are supposed to be.
//    Use that one for a terminal-to-FCU check; use this one when the shape of the system is unknown.
//
// ✱✱ ISLANDS ARE FOUND BY WALKING, NOT BY READING THE SYSTEM NAME. Two runs can carry the same System
//    Name and be physically separate — the name is inherited or typed, the connection is geometric. This
//    walks Connector.AllRefs hop by hop and groups whatever is genuinely reachable. Where the walked
//    islands DISAGREE with the System Name, the report says so, because that disagreement is usually the
//    actual defect: a schedule reading the name will show a complete system that does not exist.
//
// ✱✱ AN ISLAND WITH NO EQUIPMENT IN IT IS THE FINDING. Islands are sorted with the orphans first. An
//    island of 40 elements containing an AHU is a system; an island of 3 elements containing nothing is
//    a piece of dead pipework. `sourceCategories` decides what counts as a source — change it for the
//    domain you are checking rather than accepting the default list.
//
// GOTCHA: THE WALK IS LIMITED TO `elements`. A branch joined to the rest of the system through a fitting
//         that your filter did not include will read as a separate island — a false alarm caused by the
//         filter, not by the model. Include the FITTING categories (Duct Fittings, Pipe Fittings,
//         Flex Duct, Accessories) in the filter, or the count will be wrong. The report prints how many
//         neighbours were reached that are OUTSIDE the set, which is how that mistake shows itself.
// GOTCHA: `Connector.IsConnected` describes intent, not always physical reality (START-HERE.md rule 1).
//         Two elements can be flagged connected and be 50 mm apart. This trusts the flag for the walk —
//         where the geometry is the question, skills/ajtools-mep-trace is the right route.
// GOTCHA: reports BY EXCEPTION on a healthy model — one island, all good, short output. That is a pass.
// RELATED: recipes/verify-duct-connectivity.cs (terminal-to-FCU chains, live-proven),
//          recipes/trace-mep-circuits.cs (real circuits when naming cannot be trusted),
//          action-find-dead-end-system.cs (runs that end in nothing),
//          action-connect-open-connectors.cs (the fix for a gap that should be a joint).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on a system you already understand and
//   confirm the island count matches what you know before trusting it on an unfamiliar one.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// What counts as a SOURCE — an island containing one of these is fed; an island without one is orphaned.
var sourceCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_MechanicalEquipment,
    BuiltInCategory.OST_PlumbingFixtures,
};
int maxIslandsListed = 25;       // how many islands to detail
int maxMembersPerIsland = 15;    // element Ids printed per island
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the whole system, INCLUDING its fittings).");
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

// ---- index the set ----
var byId = new Dictionary<long, Element>();
foreach (var el in elements) byId[IdValue(el.Id)] = el;

var sourceIds = new HashSet<long>();
foreach (var cat in sourceCategories) sourceIds.Add((long)cat);

// ---- build the adjacency, and count what we reach outside the set ----
var adjacency = new Dictionary<long, HashSet<long>>();
var outsideReached = new HashSet<long>();
int noConnectors = 0;

foreach (var kv in byId)
{
    if (!adjacency.ContainsKey(kv.Key)) adjacency[kv.Key] = new HashSet<long>();
    var cm = managerOf(kv.Value);
    if (cm == null) { noConnectors++; continue; }
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (!c.IsConnected) continue;
            foreach (Connector r in c.AllRefs)
            {
                if (r.Owner == null) continue;
                long otherId = IdValue(r.Owner.Id);
                if (otherId == kv.Key) continue;
                if (byId.ContainsKey(otherId))
                {
                    adjacency[kv.Key].Add(otherId);
                    if (!adjacency.ContainsKey(otherId)) adjacency[otherId] = new HashSet<long>();
                    adjacency[otherId].Add(kv.Key);
                }
                else outsideReached.Add(otherId);
            }
        }
    }
    catch { }
}

// ---- flood fill into islands ----
var seen = new HashSet<long>();
var islands = new List<List<long>>();

foreach (var startId in byId.Keys)
{
    if (seen.Contains(startId)) continue;
    var island = new List<long>();
    var queue = new Queue<long>();
    queue.Enqueue(startId);
    seen.Add(startId);
    while (queue.Count > 0)
    {
        long cur = queue.Dequeue();
        island.Add(cur);
        if (!adjacency.ContainsKey(cur)) continue;
        foreach (var nxt in adjacency[cur])
        {
            if (seen.Contains(nxt)) continue;
            seen.Add(nxt);
            queue.Enqueue(nxt);
        }
    }
    islands.Add(island);
}

// ---- describe each island ----
Func<List<long>, (int Sources, string SystemNames, int Curves, int Fittings)> describe = island =>
{
    int sources = 0, curves = 0, fittings = 0;
    var names = new HashSet<string>();
    foreach (var id in island)
    {
        var el = byId[id];
        if (el.Category != null && sourceIds.Contains(IdValue(el.Category.Id))) sources++;
        if (el is MEPCurve) curves++; else fittings++;
        var p = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
        if (p != null && p.HasValue)
        {
            var v = p.AsString();
            if (!string.IsNullOrWhiteSpace(v)) names.Add(v.Trim());
        }
    }
    return (sources, names.Count == 0 ? "(none)" : string.Join(" / ", names.OrderBy(n => n).Take(4)), curves, fittings);
};

var described = islands
    .Select(i => new { Members = i, Info = describe(i) })
    .OrderBy(x => x.Info.Sources > 0 ? 1 : 0)          // orphans first — they are the finding
    .ThenByDescending(x => x.Members.Count)
    .ToList();

int orphanIslands = described.Count(x => x.Info.Sources == 0);
int orphanElements = described.Where(x => x.Info.Sources == 0).Sum(x => x.Members.Count);

// ---- report ----
sb.AppendLine($"SYSTEM CONNECTIVITY — {elements.Count} element(s) walked");
sb.AppendLine($"SEPARATE ISLANDS: {islands.Count}" + (islands.Count == 1 ? "  (one connected network — this is the healthy answer)" : ""));
sb.AppendLine($"ISLANDS WITH NO SOURCE EQUIPMENT: {orphanIslands}, holding {orphanElements} element(s)");
if (noConnectors > 0) sb.AppendLine($"NOTE: {noConnectors} element(s) carry no connectors at all — each is its own island by definition.");
if (outsideReached.Count > 0)
    sb.AppendLine($"NOTE: the walk reached {outsideReached.Count} connected element(s) OUTSIDE your filter. Islands may be joined through them — widen the filter to include fittings/accessories before trusting the island count.");
sb.AppendLine();

if (islands.Count == 1 && orphanIslands == 0)
{
    sb.AppendLine("CLEAR — one network, and it contains source equipment.");
    return sb.ToString();
}

sb.AppendLine("| # | Elements | Runs | Fittings | Sources | System name(s) | |");
sb.AppendLine("|---|---|---|---|---|---|---|");
int n = 0;
foreach (var x in described.Take(maxIslandsListed))
{
    n++;
    string flag = x.Info.Sources == 0 ? "ORPHAN — fed by nothing" : "";
    sb.AppendLine($"| {n} | {x.Members.Count} | {x.Info.Curves} | {x.Info.Fittings} | {x.Info.Sources} | {x.Info.SystemNames} | {flag} |");
}
if (described.Count > maxIslandsListed) sb.AppendLine($"\n... and {described.Count - maxIslandsListed} more island(s).");

// The orphans in detail — these are what someone has to go and fix.
var orphans = described.Where(x => x.Info.Sources == 0).ToList();
if (orphans.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("ORPHANED ISLANDS IN DETAIL — nothing feeds these:");
    int oi = 0;
    foreach (var o in orphans.Take(maxIslandsListed))
    {
        oi++;
        sb.AppendLine($"  Island {oi} — {o.Members.Count} element(s), system name {o.Info.SystemNames}:");
        sb.AppendLine("    " + string.Join(", ", o.Members.Take(maxMembersPerIsland).Select(id => id.ToString())) +
                      (o.Members.Count > maxMembersPerIsland ? $" ... and {o.Members.Count - maxMembersPerIsland} more" : ""));
    }
}

// Name-vs-reality disagreement: one system name spread across several islands.
var nameToIslands = new Dictionary<string, int>();
foreach (var x in described)
    foreach (var nm in x.Info.SystemNames.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries))
    {
        if (nm == "(none)") continue;
        nameToIslands[nm] = nameToIslands.ContainsKey(nm) ? nameToIslands[nm] + 1 : 1;
    }
var split = nameToIslands.Where(kv => kv.Value > 1).ToList();
if (split.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("NAME vs REALITY — these system names span more than one physically separate island, so a schedule reading the name shows a system that is not joined up:");
    foreach (var kv in split.OrderByDescending(k => k.Value).Take(15))
        sb.AppendLine($"  '{kv.Key}' appears in {kv.Value} separate islands");
}

return sb.ToString();
