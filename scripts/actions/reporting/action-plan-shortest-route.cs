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
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// **READ THIS BEFORE QUOTING ANY LENGTH.** These are POINT-TO-POINT distances between element locations.
// Real cable, conduit and duct do not fly through the air — they run orthogonally along ceilings, down
// walls, around obstructions, inside trays. So:
//   - metric="manhattan" (|dx|+|dy|+|dz|, the DEFAULT) is much closer to reality than a straight line,
//     because it assumes orthogonal running. Still ignores obstacles and vertical drops you did not model.
//   - metric="centre" (straight line) is a LOWER BOUND — the theoretical minimum, never achievable.
//   Treat the output as the right CONNECTION ORDER plus a sensible length estimate, not a cable schedule.
//   Obstacle-aware routing (A* around real geometry) is deliberately NOT attempted here — see the note at
//   the end of this header.
//
// GOTCHA: mode="tree" gives the least TOTAL length. mode="chain" is always equal or longer, because a
//         single unbranched run is a harder constraint. If the numbers surprise you, that is why.
// GOTCHA: Prim's is exact — the tree it returns IS the optimum for the given distances. The chain is a
//         heuristic (nearest-neighbour + 2-opt); it is usually within a few percent of optimal but is NOT
//         guaranteed optimal, because that problem (travelling salesman) has no fast exact solution.
//         The output says which guarantee you have — do not claim "shortest possible" for a chain.
// PERFORMANCE: Prim's is O(n^2) — 500 fixtures is 250k comparisons, milliseconds. 2-opt is O(n^2) per
//         improving pass and is capped by twoOptPasses below so it cannot spin on a large set.
// NOT ATTEMPTED ON PURPOSE: routing around real obstacles. That needs a 3D navigable grid plus A*, and
//         the result still would not match how an electrician actually pulls cable. Ask for it only with
//         a concrete case; do not pre-build it.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — both modes, both metrics, over 17 terminals:
//     manhattan: tree 111.7 m (16 runs, longest 9300 mm) vs best chain 132.9 m — tree 16% shorter
//     straight : tree  85.5 m                            vs best chain 106.8 m — tree 20% shorter
//   2-opt earned its place: it took 0.8 m off the manhattan chain and 3.9 m off the straight-line one.
//   Note manhattan came out ~30% longer than straight line — that IS the orthogonal-running penalty, and
//   it is why manhattan is the honest default for cable and conduit.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "tree";              // "tree" (least total, branching) | "chain" (single unbranched run)
string metric = "manhattan";       // "manhattan" (orthogonal, cable-realistic) | "centre" (straight line)
int startElementIdInt = 0;         // 0 = start anywhere; else the panel/FCU/source the run begins at
bool drawRoute = false;            // true = draw Model Lines along the route so you can SEE it
int twoOptPasses = 4;              // chain mode only — improvement passes; higher = slower, slightly better
int maxSegmentsListed = 40;        // detail cap; totals always cover every segment
// ---- END INPUTS ----

Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

Func<Element, XYZ> centreOf = el =>
{
    var lp = el.Location as LocationPoint;
    if (lp != null) return lp.Point;
    var b = el.get_BoundingBox(null);
    return b == null ? null : (b.Min + b.Max) / 2.0;
};

var nodes = elements.Where(e => centreOf(e) != null).ToList();
var pts = nodes.Select(centreOf).ToList();
int n = nodes.Count;

if (n < 2)
{
    sb.AppendLine($"Need at least 2 elements with a location to plan a route — got {n}.");
}
else
{
    Func<int, int, double> dist = (a, b) =>
    {
        var p = pts[a]; var q = pts[b];
        return metric == "centre"
            ? p.DistanceTo(q)
            : Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y) + Math.Abs(p.Z - q.Z);
    };

    int start = 0;
    if (startElementIdInt != 0)
    {
        int idx = nodes.FindIndex(e => e.Id.IntegerValue == startElementIdInt);
        if (idx < 0) sb.AppendLine($"NOTE: start element {startElementIdInt} is not in the set — starting from the first element instead.");
        else start = idx;
    }

    var segments = new List<Tuple<int, int, double>>();
    double total = 0;
    string guarantee;

    if (mode == "tree")
    {
        // Prim's algorithm: grow one tree, always adding the cheapest edge that reaches a new node.
        // Exact — the result is the provably shortest total connection for these distances.
        var inTree = new bool[n];
        var bestCost = new double[n];
        var bestFrom = new int[n];
        for (int i = 0; i < n; i++) { bestCost[i] = double.MaxValue; bestFrom[i] = -1; }
        bestCost[start] = 0;

        for (int iter = 0; iter < n; iter++)
        {
            int pick = -1; double cheapest = double.MaxValue;
            for (int i = 0; i < n; i++)
                if (!inTree[i] && bestCost[i] < cheapest) { cheapest = bestCost[i]; pick = i; }
            if (pick < 0) break;
            inTree[pick] = true;
            if (bestFrom[pick] >= 0) { segments.Add(Tuple.Create(bestFrom[pick], pick, cheapest)); total += cheapest; }
            for (int i = 0; i < n; i++)
            {
                if (inTree[i]) continue;
                double d = dist(pick, i);
                if (d < bestCost[i]) { bestCost[i] = d; bestFrom[i] = pick; }
            }
        }
        guarantee = "EXACT — Prim's minimum spanning tree; no branching layout can total less for these distances.";
    }
    else if (mode == "chain")
    {
        // nearest-neighbour to get a decent order, then 2-opt to untangle crossings
        var tour = new List<int> { start };
        var used = new bool[n]; used[start] = true;
        for (int k = 1; k < n; k++)
        {
            int last = tour[tour.Count - 1], next = -1; double best = double.MaxValue;
            for (int i = 0; i < n; i++)
                if (!used[i]) { double d = dist(last, i); if (d < best) { best = d; next = i; } }
            tour.Add(next); used[next] = true;
        }
        // 2-opt: reversing a run of the tour removes crossings and shortens the total
        for (int pass = 0; pass < twoOptPasses; pass++)
        {
            bool improved = false;
            for (int i = 1; i < tour.Count - 2; i++)
                for (int j = i + 1; j < tour.Count - 1; j++)
                {
                    double before = dist(tour[i - 1], tour[i]) + dist(tour[j], tour[j + 1]);
                    double after = dist(tour[i - 1], tour[j]) + dist(tour[i], tour[j + 1]);
                    if (after < before - 1e-9)
                    {
                        tour.Reverse(i, j - i + 1);
                        improved = true;
                    }
                }
            if (!improved) break;
        }
        for (int i = 0; i < tour.Count - 1; i++)
        {
            double d = dist(tour[i], tour[i + 1]);
            segments.Add(Tuple.Create(tour[i], tour[i + 1], d));
            total += d;
        }
        guarantee = "HEURISTIC — nearest-neighbour plus 2-opt. Usually within a few percent of optimal, but NOT proven shortest (travelling-salesman problem).";
    }
    else
    {
        sb.AppendLine($"Unknown mode '{mode}' — use \"tree\" or \"chain\".");
        segments = null;
    }

    if (segments != null)
    {
        sb.AppendLine($"Route plan — mode '{mode}', metric '{metric}', {n} element(s)"
            + (startElementIdInt != 0 ? $", starting at Id {nodes[start].Id.IntegerValue}" : "") + ":");
        sb.AppendLine($"  TOTAL LENGTH: {toMm(total):N0} mm  ({toMm(total)/1000.0:F1} m) across {segments.Count} run(s)");
        sb.AppendLine($"  Longest single run: {toMm(segments.Max(s => s.Item3)):N0} mm");
        sb.AppendLine($"  {guarantee}");
        sb.AppendLine($"  NOTE: point-to-point estimate, not a routed cable length — see this fragment's header.");
        sb.AppendLine("  Runs:");
        foreach (var s in segments.Take(maxSegmentsListed))
            sb.AppendLine($"    Id {nodes[s.Item1].Id.IntegerValue} -> Id {nodes[s.Item2].Id.IntegerValue} : {toMm(s.Item3):N0} mm");
        if (segments.Count > maxSegmentsListed)
            sb.AppendLine($"    ... +{segments.Count - maxSegmentsListed} more run(s) not listed (raise maxSegmentsListed — NOT silently dropped)");

        if (drawRoute)
        {
            using (var t = new Transaction(Document, "AJ Tools - Draw Planned Route"))
            {
                t.Start();
                try
                {
                    int drawn = 0;
                    foreach (var s in segments)
                    {
                        var p1 = pts[s.Item1]; var p2 = pts[s.Item2];
                        if (p1.DistanceTo(p2) < 1e-6) continue;
                        var d = (p2 - p1).Normalize();
                        // any vector not parallel to the run gives us a plane that contains it
                        var helper = Math.Abs(d.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX;
                        var normal = d.CrossProduct(helper).Normalize();
                        var sp = SketchPlane.Create(Document, Plane.CreateByNormalAndOrigin(normal, p1));
                        Document.Create.NewModelCurve(Line.CreateBound(p1, p2), sp);
                        drawn++;
                    }
                    t.Commit();
                    sb.AppendLine($"  Drew {drawn} model line(s) along the route — delete them with a Model Lines filter + action-delete-elements.cs when done.");
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"  FAILED to draw the route — rolled back, no lines created. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
