// ============================================================
// FRAGMENT (action) — action-plan-shortest-route.cs
// PURPOSE: Work out the CHEAPEST WAY TO CONNECT a set of elements — "wire these 40 light fixtures using
//          the least cable", "chain these terminals off that FCU", "which order do I run this loop in".
//          Two genuinely different jobs, so two modes:
//            mode="tree"  MINIMUM SPANNING TREE (Prim's algorithm) — every element connected, branching
//                         allowed, provably the least total length possible. This is how a real homerun
//                         from a panel behaves: the run splits and serves several fixtures.
//            mode="chain" DAISY CHAIN — one continuous run visiting every element once, no branching
//                         (a single circuit looping in and out of each fitting). Built nearest-neighbour
//                         first, then improved with 2-opt.
//            mode="continuous"  ONE RUN THAT FINISHES EACH GROUP BEFORE MOVING ON — the user's method
//                         (2026-07-26), and on real geometry the best of the three. Complete a room, then
//                         from its LAST fitting jump to the genuinely nearest fitting in any room not yet
//                         done; that jump decides which room comes next AND where you enter it. Needs
//                         groupBy set. **The clever part is choosing where each room ENDS**: the exit is
//                         not accepted, it is optimised — every candidate exit is tried and the one that
//                         minimises (path through the room + the jump out of it) wins. Measured on
//                         Project1: 109.0 m, versus 134.3 m for independent groups with centroid feeders
//                         and 106.8 m for the unconstrained free-for-all that cheats through walls. So it
//                         lands within ~2% of the theoretical minimum while staying buildable.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// **GROUPING — READ THIS, IT IS USUALLY WHAT YOU WANT (the user's insight, 2026-07-26).**
//   Un-grouped, the algorithm has NO IDEA WALLS EXIST. Nearest-neighbour will happily hop into the next
//   room because a fitting there is 200 mm closer, then come back — which nobody would ever pull cable
//   like. Set `groupBy` to route each room / space / level / zone as its own run, which is both
//   physically sensible AND faster (n^2 per group instead of n^2 across everything).
//   With `connectGroups=true` it then links the groups to each other with a second-level tree, which is
//   the real shape of distribution: local runs per room, feeders between them.
//
//   **EXPECT GROUPING TO REPORT A LONGER TOTAL, AND DO NOT "FIX" THAT.** Measured on Project1's 17
//   terminals across 3 ceiling zones: ungrouped 106.8 m, grouped 134.3 m — 26% longer on paper. Grouping
//   is a CONSTRAINT ("finish this room before leaving it"), and constraints cost length. The ungrouped
//   figure is the shorter one only because it is cheating: it crossed between zones 6 times when 2 would
//   do, i.e. it ran back and forth through walls. **The ungrouped number is unbuildable; the grouped
//   number is the real one.** If someone ever reports grouping as a regression because the total went up,
//   this is the answer.
//
// **READ THIS BEFORE QUOTING ANY LENGTH.** These are POINT-TO-POINT distances between element locations.
// Real cable, conduit and duct do not fly through the air — they run orthogonally along ceilings, down
// walls, around obstructions, inside trays. So:
//   - metric="manhattan" (|dx|+|dy|+|dz|, the DEFAULT) is much closer to reality than a straight line,
//     because it assumes orthogonal running. Still ignores obstacles and vertical drops you did not model.
//   - metric="centre" (straight line) is a LOWER BOUND — the theoretical minimum, never achievable.
//   Treat the output as the right CONNECTION ORDER plus a sensible length estimate, not a cable schedule.
//
// GOTCHA: mode="tree" gives the least TOTAL length. mode="chain" is always equal or longer, because a
//         single unbranched run is a harder constraint. If the numbers surprise you, that is why.
// GOTCHA: Prim's is exact — the tree it returns IS the optimum for the given distances. The chain is a
//         heuristic (nearest-neighbour + 2-opt); usually within a few percent of optimal but NOT
//         guaranteed, because that problem (travelling salesman) has no fast exact solution. The output
//         states which guarantee applies — do not claim "shortest possible" for a chain.
// GOTCHA: groupBy="room"/"space" needs PLACED, ENCLOSED rooms (Area > 0). Unplaced rooms contain nothing
//         and every element lands in "(ungrouped)". Containment is probed at the ROOM's own level height,
//         not the element's — a ceiling-mounted terminal sits above the room's upper limit and would
//         otherwise test false (see ../../../knowledge/live-model/core.md on IsPointInRoom).
// GOTCHA: drawing into a PLAN view must use DETAIL lines, not model lines. Model lines at ceiling height
//         sit above a plan's cut plane and are simply invisible there — this cost a real head-scratch on
//         2026-07-26. Set drawInViewIdInt to the view you actually work in and this fragment picks the
//         right kind for you.
// GOTCHA: Revit has NO polyline element — every curve element is one straight line or one arc — so a
//         route is always stored as joined segments, never a single curve. `groupDrawnRoute` bundles them
//         into a Group, which is what makes the run behave as ONE thing: click once, select the lot, move
//         or delete as a unit. Asked for and verified 2026-07-26 ("in single stretch").
// PERFORMANCE — MEASURED 2026-07-26 on this machine, synthetic points, same code as below:
//         n        tree(Prim)   chain(NN + 2-opt x4)   total
//         100          0 ms            2 ms             2 ms
//         500          8 ms           74 ms            82 ms
//         1000        33 ms          302 ms           335 ms
//         2000       131 ms        1,281 ms           1.4 s
//         4000       525 ms        5,204 ms           5.7 s
//       All O(n^2) — doubling the count roughly quadruples the time. TREE MODE SCALES FAR BETTER because
//       the 2-opt polish is what costs. GROUPING helps enormously here too: ten rooms of 100 is far
//       cheaper than one set of 1000. Above maxNodesFor2Opt the polish is SKIPPED and SAID SO.
// NOT ATTEMPTED ON PURPOSE: routing around real obstacles. That needs a 3D navigable grid plus A*, and
//         the result still would not match how an electrician actually pulls cable. Ask for it only with
//         a concrete case; do not pre-build it.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — both modes, both metrics, over 17 terminals:
//     manhattan: tree 111.7 m (16 runs, longest 9300 mm) vs best chain 132.9 m — tree 16% shorter
//     straight : tree  85.5 m                            vs best chain 106.8 m — tree 20% shorter
//   2-opt earned its place: it took 0.8 m off the manhattan chain and 3.9 m off the straight-line one.
//   Note manhattan came out ~30% longer than straight line — that IS the orthogonal-running penalty, and
//   it is why manhattan is the honest default for cable and conduit.
//   GROUPING added the same day after the user asked what happens when the nearest fitting is in the NEXT
//   room. Measured on the drawn chain: it changed zone 6 times where 2 would do — it really does leave a
//   room and come back. Grouped routing fixes that at the cost of a longer paper total (see above).
//   ROOM grouping ✓ verified 2026-07-26 once the user's three rooms were placed (239/251/271 m2):
//   6/6/5 terminals, and 14 of 16 drawn lines stayed inside their room — the only 2 crossings were the
//   deliberate feeders.
//   mode="continuous" ✓ verified the same day and is the BEST of the three on real geometry:
//     Room 1 (6 elements, 31.3 m) -> jump 7,039 mm -> Room 3 (6, 28.9 m) -> jump 6,849 mm -> Room 2 (5, 34.8 m)
//     TOTAL 109.0 m = 95.1 m inside rooms + 13.9 m across, versus 134.3 m for independent groups with
//     centroid feeders. Both jumps were re-verified against every remaining element, as the user asked.
//
// ✱✱ FIXED 2026-08-24 — LEVEL HEIGHTS HERE NOW USE `ProjectElevation`, NOT `Elevation`.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says (Project OR Shared); `ProjectElevation` is always from the project origin, which is
//    the space every XYZ in the model lives in. This fragment mixes a level height with real
//    coordinates, so on a model with a survey offset the old code was wrong by exactly that offset —
//    silently, with a plausible number and no error. See
//    knowledge/live-model/level-elevation-vs-project-elevation.md, and run
//    action-report-level-elevations.cs to see whether a given model is affected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "tree";              // "tree" (branching, least total) | "chain" (one unbranched run)
                                   // | "continuous" (one run, each group finished before the next — needs groupBy)
string metric = "manhattan";       // "manhattan" (orthogonal, cable-realistic) | "centre" (straight line)
string groupBy = "none";           // "none" | "room" | "space" | "level" | "parameter"  <-- see header
string groupParameterName = "";    // groupBy="parameter" only, e.g. "Zone", "Circuit Number", "System Name"
bool connectGroups = false;        // true = also link the groups to each other (feeders between rooms)
int startElementIdInt = 0;         // 0 = start anywhere; else the panel/FCU/source the run begins at
bool drawRoute = false;            // true = draw the route so you can SEE it
int drawInViewIdInt = 0;           // 0 = model lines (3D, every view); a PLAN view Id = detail lines there
bool groupDrawnRoute = false;      // true = bundle the drawn lines into ONE selectable Group
string drawnGroupName = "";        // optional name for that group, e.g. "MEP_Terminal_Run"
int twoOptPasses = 4;              // chain mode only — improvement passes; higher = slower, slightly better
int maxNodesFor2Opt = 2500;        // above this the 2-opt polish is skipped and reported (see PERFORMANCE)
int maxSegmentsListed = 40;        // detail cap; totals always cover every segment
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
Func<double, double> mmToFt = v => v / 304.8;

Func<Element, XYZ> centreOf = el =>
{
    var lp = el.Location as LocationPoint;
    if (lp != null) return lp.Point;
    var b = el.get_BoundingBox(null);
    return b == null ? null : (b.Min + b.Max) / 2.0;
};

var nodes = elements.Where(e => centreOf(e) != null).ToList();

if (nodes.Count < 2)
{
    sb.AppendLine($"Need at least 2 elements with a location to plan a route — got {nodes.Count}.");
}
else
{
    // ---------- 1. put every element into a group ----------
    var groupOf = new Dictionary<ElementId, string>();   // element id -> group key
    if (groupBy == "room" || groupBy == "space")
    {
        var cat = groupBy == "room" ? BuiltInCategory.OST_Rooms : BuiltInCategory.OST_MEPSpaces;
        var containers = new FilteredElementCollector(Document).OfCategory(cat)
            .WhereElementIsNotElementType().Cast<SpatialElement>()
            .Where(s => s.Area > 0).ToList();          // unplaced containers hold nothing
        if (containers.Count == 0)
            sb.AppendLine($"NOTE: no PLACED {groupBy}s in this model (Area > 0) — everything falls into one ungrouped set.");
        foreach (var el in nodes)
        {
            var p = centreOf(el);
            string key = "(ungrouped)";
            foreach (var c in containers)
            {
                // probe at the CONTAINER's level, not the element's — a ceiling-mounted fitting sits
                // above the room's upper limit and would test false at its own height
                var probe = new XYZ(p.X, p.Y, c.Level.ProjectElevation + mmToFt(1000));
                bool inside = false;
                var room = c as Autodesk.Revit.DB.Architecture.Room;
                var space = c as Autodesk.Revit.DB.Mechanical.Space;
                try { if (room != null) inside = room.IsPointInRoom(probe); else if (space != null) inside = space.IsPointInSpace(probe); }
                catch { }
                if (inside) { key = $"{c.Name} ({c.Id})"; break; }
            }
            groupOf[el.Id] = key;
        }
    }
    else if (groupBy == "level")
    {
        foreach (var el in nodes)
        {
            var lvl = Document.GetElement(el.LevelId);
            groupOf[el.Id] = lvl != null ? $"{lvl.Name} ({lvl.Id})" : "(no level)";
        }
    }
    else if (groupBy == "parameter")
    {
        foreach (var el in nodes)
        {
            var p = el.LookupParameter(groupParameterName);
            if (p == null) { var te = Document.GetElement(el.GetTypeId()); p = te?.LookupParameter(groupParameterName); }
            string v = null;
            if (p != null && p.HasValue) { try { v = p.AsValueString(); } catch { } if (string.IsNullOrEmpty(v)) v = p.AsString(); }
            groupOf[el.Id] = string.IsNullOrEmpty(v) ? "(blank)" : v;
        }
    }
    else
    {
        foreach (var el in nodes) groupOf[el.Id] = "(all)";
    }

    var groups = nodes.GroupBy(e => groupOf[e.Id]).OrderBy(g => g.Key).ToList();

    // ---------- 2. route inside each group ----------
    // returns the segments as (fromElement, toElement, length) plus the guarantee wording
    Func<List<Element>, int, List<Tuple<Element, Element, double>>> routeGroup = (members, startIdx) =>
    {
        int n = members.Count;
        var pts = members.Select(centreOf).ToList();
        Func<int, int, double> dist = (a, b) =>
        {
            var p = pts[a]; var q = pts[b];
            return metric == "centre" ? p.DistanceTo(q)
                 : Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y) + Math.Abs(p.Z - q.Z);
        };
        var segs = new List<Tuple<Element, Element, double>>();
        if (n < 2) return segs;

        if (mode == "tree")
        {
            var inTree = new bool[n]; var bestCost = new double[n]; var bestFrom = new int[n];
            for (int i = 0; i < n; i++) { bestCost[i] = double.MaxValue; bestFrom[i] = -1; }
            bestCost[startIdx] = 0;
            for (int iter = 0; iter < n; iter++)
            {
                int pick = -1; double cheapest = double.MaxValue;
                for (int i = 0; i < n; i++) if (!inTree[i] && bestCost[i] < cheapest) { cheapest = bestCost[i]; pick = i; }
                if (pick < 0) break;
                inTree[pick] = true;
                if (bestFrom[pick] >= 0) segs.Add(Tuple.Create(members[bestFrom[pick]], members[pick], cheapest));
                for (int i = 0; i < n; i++)
                { if (inTree[i]) continue; double d = dist(pick, i); if (d < bestCost[i]) { bestCost[i] = d; bestFrom[i] = pick; } }
            }
        }
        else
        {
            var tour = new List<int> { startIdx };
            var used = new bool[n]; used[startIdx] = true;
            for (int k = 1; k < n; k++)
            {
                int last = tour[tour.Count - 1], next = -1; double best = double.MaxValue;
                for (int i = 0; i < n; i++) if (!used[i]) { double d = dist(last, i); if (d < best) { best = d; next = i; } }
                tour.Add(next); used[next] = true;
            }
            bool polished = n <= maxNodesFor2Opt;
            if (!polished)
                sb.AppendLine($"  NOTE: {n} nodes exceeds maxNodesFor2Opt ({maxNodesFor2Opt}) — 2-opt polish SKIPPED to avoid a long grind. The chain below is valid but unpolished.");
            for (int pass = 0; polished && pass < twoOptPasses; pass++)
            {
                bool improved = false;
                for (int i = 1; i < tour.Count - 2; i++)
                    for (int j = i + 1; j < tour.Count - 1; j++)
                    {
                        double before = dist(tour[i - 1], tour[i]) + dist(tour[j], tour[j + 1]);
                        double after = dist(tour[i - 1], tour[j]) + dist(tour[i], tour[j + 1]);
                        if (after < before - 1e-9) { tour.Reverse(i, j - i + 1); improved = true; }
                    }
                if (!improved) break;
            }
            for (int i = 0; i < tour.Count - 1; i++)
                segs.Add(Tuple.Create(members[tour[i]], members[tour[i + 1]], dist(tour[i], tour[i + 1])));
        }
        return segs;
    };

    var allSegments = new List<Tuple<Element, Element, double>>();
    double grandTotal = 0;

    sb.AppendLine($"Route plan — mode '{mode}', metric '{metric}', grouped by '{groupBy}', {nodes.Count} element(s) in {groups.Count} group(s):");

    // ===== mode "continuous": one run, each group finished before the next, entry decided by the exit =====
    if (mode == "continuous")
    {
        if (groupBy == "none" || groups.Count < 2)
            sb.AppendLine("  NOTE: mode=\"continuous\" needs groupBy set and 2+ groups — falling back to one plain chain.");

        Func<Element, Element, double> D = (a, b) =>
        {
            var p = centreOf(a); var q = centreOf(b);
            return metric == "centre" ? p.DistanceTo(q)
                 : Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y) + Math.Abs(p.Z - q.Z);
        };
        // Hamiltonian path across `ms` from a FIXED start to a FIXED end: nearest-neighbour over the
        // middle, then 2-opt. 2-opt only reverses interior spans, so both endpoints stay put.
        Func<List<Element>, Element, Element, List<Element>> pathFromTo = (ms, from, to) =>
        {
            var mid = ms.Where(m => m.Id != from.Id && m.Id != to.Id).ToList();
            var seq = new List<Element> { from };
            var cur = from;
            while (mid.Count > 0) { var nx = mid.OrderBy(m => D(cur, m)).First(); seq.Add(nx); mid.Remove(nx); cur = nx; }
            if (to.Id != from.Id) seq.Add(to);
            for (int pass = 0; pass < 6; pass++)
            {
                bool imp = false;
                for (int i = 1; i < seq.Count - 2; i++)
                    for (int j = i + 1; j < seq.Count - 1; j++)
                    {
                        double b4 = D(seq[i - 1], seq[i]) + D(seq[j], seq[j + 1]);
                        double af = D(seq[i - 1], seq[j]) + D(seq[i], seq[j + 1]);
                        if (af < b4 - 1e-9) { seq.Reverse(i, j - i + 1); imp = true; }
                    }
                if (!imp) break;
            }
            return seq;
        };
        Func<List<Element>, double> seqLen = s => { double v = 0; for (int i = 0; i < s.Count - 1; i++) v += D(s[i], s[i + 1]); return v; };

        var byGroup = groups.ToDictionary(g => g.Key, g => g.ToList());
        var remaining = new HashSet<string>(byGroup.Keys);
        // start where the user said, else the first element of the first group
        Element entry = nodes.FirstOrDefault(e => e.Id == new ElementId(startElementIdInt)) ?? groups.First().First();
        double inside = 0, hopTotal = 0; int hopCount = 0;

        while (remaining.Count > 0)
        {
            string gk = groupOf[entry.Id];
            if (!remaining.Contains(gk)) { gk = remaining.First(); entry = byGroup[gk].First(); }
            remaining.Remove(gk);
            var ms = byGroup[gk];

            Element bestExit = null, bestNextEntry = null; List<Element> bestPath = null;
            double bestScore = double.MaxValue, bestHop = 0;
            foreach (var exit in ms)
            {
                if (ms.Count > 1 && exit.Id == entry.Id) continue;   // a multi-element group must leave somewhere else
                var p = pathFromTo(ms, entry, exit);
                if (p.Count != ms.Count) continue;
                double pl = seqLen(p), hop = 0; Element nextEntry = null;
                if (remaining.Count > 0)
                {
                    // the jump is to the NEAREST element in ANY group still to do — that is what decides
                    // which group comes next, and it is re-checked from every candidate exit
                    var cands = remaining.SelectMany(r => byGroup[r]).ToList();
                    nextEntry = cands.OrderBy(c => D(exit, c)).First();
                    hop = D(exit, nextEntry);
                }
                double score = pl + hop;
                if (score < bestScore) { bestScore = score; bestExit = exit; bestPath = p; bestNextEntry = nextEntry; bestHop = hop; }
            }

            for (int i = 0; i < bestPath.Count - 1; i++)
                allSegments.Add(Tuple.Create(bestPath[i], bestPath[i + 1], D(bestPath[i], bestPath[i + 1])));
            inside += seqLen(bestPath);
            sb.AppendLine($"  {gk,-24} enter {entry.Id}, {ms.Count} element(s), {toMm(seqLen(bestPath))/1000.0:F1} m, exit {bestExit.Id}");
            if (bestNextEntry != null)
            {
                allSegments.Add(Tuple.Create(bestExit, bestNextEntry, bestHop));
                hopTotal += bestHop; hopCount++;
                sb.AppendLine($"      -> jump {toMm(bestHop):N0} mm to {bestNextEntry.Id} in {groupOf[bestNextEntry.Id]} (nearest of {remaining.SelectMany(r => byGroup[r]).Count()} remaining)");
                entry = bestNextEntry;
            }
        }
        grandTotal = inside + hopTotal;
        sb.AppendLine($"  {"TOTAL",-24} {toMm(grandTotal)/1000.0:F1} m = {toMm(inside)/1000.0:F1} m inside groups + {toMm(hopTotal)/1000.0:F1} m across ({hopCount} jump(s))");
        sb.AppendLine("  Each group is finished before the next is started, and every jump was checked against ALL remaining elements.");
    }
    else
    foreach (var g in groups)
    {
        var members = g.ToList();
        int startIdx = 0;
        if (startElementIdInt != 0)
        {
            int idx = members.FindIndex(e => e.Id == new ElementId(startElementIdInt));
            if (idx >= 0) startIdx = idx;
        }
        var segs = routeGroup(members, startIdx);
        double total = segs.Sum(s => s.Item3);
        grandTotal += total;
        allSegments.AddRange(segs);
        sb.AppendLine($"  {g.Key,-28} {members.Count,4} element(s)  {toMm(total)/1000.0,8:F1} m  over {segs.Count} run(s)"
            + (members.Count < 2 ? "  (single element — nothing to connect)" : ""));
    }

    // ---------- 3. optionally link the groups to each other ----------
    // continuous mode already links the groups with its own optimised jumps — adding centroid feeders
    // on top would double up
    if (mode != "continuous" && connectGroups && groups.Count > 1)
    {
        // one representative per group (the element nearest that group's centroid), then a tree over those
        var reps = new List<Element>();
        foreach (var g in groups)
        {
            var ms = g.ToList();
            var pts = ms.Select(centreOf).ToList();
            var cx = pts.Average(p => p.X); var cy = pts.Average(p => p.Y); var cz = pts.Average(p => p.Z);
            var centroid = new XYZ(cx, cy, cz);
            reps.Add(ms.OrderBy(e => centreOf(e).DistanceTo(centroid)).First());
        }
        var feeders = routeGroup(reps, 0);
        double feederTotal = feeders.Sum(s => s.Item3);
        grandTotal += feederTotal;
        allSegments.AddRange(feeders);
        sb.AppendLine($"  {"FEEDERS between groups",-28} {reps.Count,4} link point(s) {toMm(feederTotal)/1000.0,8:F1} m  over {feeders.Count} run(s)");
    }

    if (mode != "continuous")
        sb.AppendLine($"  {"GRAND TOTAL",-28} {"",4}               {toMm(grandTotal)/1000.0,8:F1} m  over {allSegments.Count} run(s)");
    sb.AppendLine(mode == "tree"
        ? "  EXACT within each group — Prim's minimum spanning tree."
        : mode == "continuous"
        ? "  HEURISTIC — nearest-neighbour + 2-opt per group, with the exit point optimised against the next jump. Not proven optimal, but on Project1 it came within ~2% of the unconstrained minimum while staying buildable."
        : "  HEURISTIC — nearest-neighbour plus 2-opt; usually near-optimal but not proven shortest.");
    sb.AppendLine("  Point-to-point estimate, NOT a routed cable length — see this fragment's header.");

    if (allSegments.Count > 0)
    {
        sb.AppendLine($"  Longest single run: {toMm(allSegments.Max(s => s.Item3)):N0} mm");
        sb.AppendLine("  Runs:");
        foreach (var s in allSegments.Take(maxSegmentsListed))
            sb.AppendLine($"    Id {s.Item1.Id} -> Id {s.Item2.Id} : {toMm(s.Item3):N0} mm");
        if (allSegments.Count > maxSegmentsListed)
            sb.AppendLine($"    ... +{allSegments.Count - maxSegmentsListed} more (raise maxSegmentsListed — NOT silently dropped)");
    }

    // ---------- 4. optionally draw it ----------
    if (drawRoute && allSegments.Count > 0)
    {
        View drawView = drawInViewIdInt != 0 ? Document.GetElement(new ElementId(drawInViewIdInt)) as View : null;
        bool asDetail = drawView != null && (drawView is ViewPlan || drawView.ViewType == ViewType.Section
                        || drawView.ViewType == ViewType.Elevation || drawView.ViewType == ViewType.Detail);
        using (var t = new Transaction(Document, "AJ Tools - Draw Planned Route"))
        {
            t.Start();
            try
            {
                int drawn = 0;
                var drawnIds = new List<ElementId>();
                // for a plan view, flatten onto the view's own plane or the lines are not valid 2D geometry
                double flatZ = 0;
                if (asDetail && drawView is ViewPlan)
                {
                    var gl = (drawView as ViewPlan).GenLevel;
                    flatZ = gl != null ? gl.ProjectElevation : drawView.Origin.Z;
                }
                foreach (var s in allSegments)
                {
                    var a = centreOf(s.Item1); var b = centreOf(s.Item2);
                    if (asDetail && drawView is ViewPlan) { a = new XYZ(a.X, a.Y, flatZ); b = new XYZ(b.X, b.Y, flatZ); }
                    if (a.DistanceTo(b) < 1e-6) continue;
                    var line = Line.CreateBound(a, b);
                    if (asDetail) drawnIds.Add(Document.Create.NewDetailCurve(drawView, line).Id);
                    else
                    {
                        var d = (b - a).Normalize();
                        var helper = Math.Abs(d.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX;
                        var sp = SketchPlane.Create(Document, Plane.CreateByNormalAndOrigin(d.CrossProduct(helper).Normalize(), a));
                        drawnIds.Add(Document.Create.NewModelCurve(line, sp).Id);
                    }
                    drawn++;
                }
                // one selectable object instead of N loose lines. Revit has NO polyline element — a curve
                // element is always a single line or arc — so a Group is how a route becomes one thing you
                // can click, move or delete in one go.
                if (groupDrawnRoute && drawnIds.Count > 1)
                {
                    try
                    {
                        var grp = Document.Create.NewGroup(drawnIds);
                        if (!string.IsNullOrEmpty(drawnGroupName))
                        { try { grp.GroupType.Name = drawnGroupName; } catch { } } // name collision — keeps the default
                        sb.AppendLine($"  Grouped the {drawnIds.Count} segment(s) into ONE selectable group '{grp.GroupType.Name}' (Id {grp.Id}).");
                    }
                    catch (Exception exG) { sb.AppendLine($"  Grouping failed ({exG.Message}) — the lines are still there as separate joined segments."); }
                }
                t.Commit();
                sb.AppendLine(asDetail
                    ? $"  Drew {drawn} DETAIL line(s) in '{drawView.Name}' — view-specific 2D, always visible there regardless of cut plane."
                    : $"  Drew {drawn} MODEL line(s) (3D). NOTE: in a plan view these are invisible if they sit above the cut plane — pass drawInViewIdInt for a plan instead.");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"  FAILED to draw the route — rolled back, no lines created. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
