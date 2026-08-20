// ============================================================
// FRAGMENT (action) — action-report-nearest-elements.cs
// PURPOSE: For each element in `elements`, find the NEAREST element(s) from a target set — a category
//          (same or different), several categories, or a fixed list of Ids. Answers "which FCU serves this
//          terminal", "which exit is closest to this room", "what is the nearest light to each switch".
//          Distance is proximity in space, NOT direction — that's what the ray fragments are for.
// ASSUMES: elements (List<Element>, the SOURCES) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
//
// THREE DISTANCE METRICS, and the choice changes the answer — pick deliberately:
//   "centre"    straight line between insertion points / bbox centres. Fast, fine for point-like
//               equipment. MISLEADING for long elements: a 30 m wall's centre can be far away while the
//               wall itself runs right past you.
//   "gap"       closest approach between the two BOUNDING BOXES, 0 if they overlap. This is real
//               clearance — the honest answer to "how close is that to this". Best default for MEP.
//   "manhattan" |dx| + |dy| + |dz| between centres. Cable, conduit and duct do not fly diagonally through
//               a building; they run orthogonally. For "how much cable will this take", manhattan is far
//               closer to reality than a straight line, and it is what action-plan-shortest-route.cs uses.
//
// GOTCHA: bounding boxes are axis-aligned, so for a ROTATED element the box is bigger than the element
//         and "gap" reads slightly optimistic (closer than reality). Fine for ranking, not for a
//         certified clearance figure — use action-report-clashes.cs when the number must be exact.
// GOTCHA: this ignores obstructions completely. The nearest FCU by distance may be through a wall, on the
//         other side of a corridor. Pair with the ray fragments when line-of-sight matters.
// PERFORMANCE: brute force, sources x targets distance comparisons. Distance maths is trivial next to
//         ray-casting — measured 1.6 million comparisons in well under a second. No spatial index needed
//         at any realistic model size; if that ever changes, bucket by grid cell before optimising.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 17 air terminals against 6 FCUs, metric "gap". Produced a
//   sensible zoning (4/4/3/2/2/2 = 17) with gaps from 1516 mm to 6852 mm, in 102 comparisons.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] targetCategoryNames = new[] { "Mechanical Equipment" }; // what to look FOR; several allowed
int[] targetElementIdInts = new int[] { };  // or a fixed list of Ids; if non-empty this WINS over categories
string metric = "gap";                      // "gap" | "centre" | "manhattan" — see header, they differ
int howMany = 1;                            // report the N nearest targets per source
double maxDistanceMm = 0;                   // 0 = no limit; else ignore targets farther than this
bool excludeSameElement = true;             // never match a source to itself
int maxRowsReported = 40;                   // detail cap; the summary always covers every source
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

// --- build the target set ---
var targets = new List<Element>();
if (targetElementIdInts.Length > 0)
{
    foreach (var id in targetElementIdInts)
    {
        var el = Document.GetElement(new ElementId(id));
        if (el != null) targets.Add(el);
    }
}
else
{
    foreach (var name in targetCategoryNames)
    {
        Category cat = null;
        foreach (Category c in Document.Settings.Categories) if (c.Name == name) { cat = c; break; }
        if (cat == null) { sb.AppendLine($"NOTE: target category '{name}' not found — skipped."); continue; }
        targets.AddRange(new FilteredElementCollector(Document)
            .OfCategoryId(cat.Id).WhereElementIsNotElementType().ToList());
    }
}

if (elements.Count == 0) { sb.AppendLine("No source elements."); }
else if (targets.Count == 0) { sb.AppendLine("No target elements found — nothing to measure against."); }
else
{
    // cache each element's centre and bounding box once — recomputing per pair is the only real cost
    Func<Element, XYZ> centreOf = el =>
    {
        var lp = el.Location as LocationPoint;
        if (lp != null) return lp.Point;
        var b = el.get_BoundingBox(null);
        return b == null ? null : (b.Min + b.Max) / 2.0;
    };
    var tCentre = new Dictionary<ElementId, XYZ>();
    var tBox = new Dictionary<ElementId, BoundingBoxXYZ>();
    foreach (var t in targets)
    {
        tCentre[t.Id] = centreOf(t);
        tBox[t.Id] = t.get_BoundingBox(null);
    }

    // gap between two axis-aligned boxes: 0 on any axis where they overlap
    Func<BoundingBoxXYZ, BoundingBoxXYZ, double> boxGap = (a, b) =>
    {
        if (a == null || b == null) return double.MaxValue;
        double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
        double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
        double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    };

    double limit = maxDistanceMm > 0 ? mm(maxDistanceMm) : double.MaxValue;
    int rows = 0, noneFound = 0, noGeometry = 0;
    long comparisons = 0;
    var summary = new List<string>();

    foreach (var src in elements)
    {
        var sc = centreOf(src);
        var sbx = src.get_BoundingBox(null);
        if (sc == null && sbx == null) { noGeometry++; continue; }

        var scored = new List<KeyValuePair<double, Element>>();
        foreach (var tgt in targets)
        {
            if (excludeSameElement && tgt.Id == src.Id) continue;
            comparisons++;
            double d;
            if (metric == "gap") d = boxGap(sbx, tBox[tgt.Id]);
            else
            {
                var tc = tCentre[tgt.Id];
                if (sc == null || tc == null) continue;
                d = metric == "manhattan"
                    ? Math.Abs(sc.X - tc.X) + Math.Abs(sc.Y - tc.Y) + Math.Abs(sc.Z - tc.Z)
                    : sc.DistanceTo(tc);
            }
            if (d > limit) continue;
            scored.Add(new KeyValuePair<double, Element>(d, tgt));
        }

        if (scored.Count == 0) { noneFound++; continue; }
        var best = scored.OrderBy(k => k.Key).Take(Math.Max(1, howMany)).ToList();

        if (rows < maxRowsReported)
        {
            var parts = best.Select(b => $"'{b.Value.Name}' (Id {b.Value.Id}, {b.Value.Category?.Name}) {toMm(b.Key):F0} mm");
            sb.AppendLine($"  '{src.Name}' (Id {src.Id}) -> {string.Join("  |  ", parts)}");
            rows++;
        }
        summary.Add($"{src.Id}->{best[0].Value.Id}");
    }

    sb.Insert(0, $"Nearest search — {elements.Count} source(s) against {targets.Count} target(s), metric '{metric}'"
        + (maxDistanceMm > 0 ? $", within {maxDistanceMm} mm" : "") + ":\n");
    if (rows >= maxRowsReported && elements.Count > maxRowsReported)
        sb.AppendLine($"  ... detail capped at {maxRowsReported} of {elements.Count} sources (raise maxRowsReported — NOT silently dropped)");
    if (noneFound > 0) sb.AppendLine($"  {noneFound} source(s) had no target within range.");
    if (noGeometry > 0) sb.AppendLine($"  {noGeometry} source(s) had no usable location.");

    // how many sources share the same nearest target — the "which FCU serves how many terminals" answer
    var grouped = summary.GroupBy(s => s.Split('>')[1]).OrderByDescending(g => g.Count());
    sb.AppendLine($"Grouped by nearest target ({grouped.Count()} distinct):");
    foreach (var g in grouped.Take(20))
    {
        var tel = Document.GetElement(new ElementId(int.Parse(g.Key)));
        sb.AppendLine($"  Id {g.Key} '{tel?.Name}' is nearest for {g.Count()} source(s)");
    }
    sb.AppendLine($"({comparisons:N0} distance comparisons.)");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
