// ============================================================
// FRAGMENT (action) — action-find-overlapping-lines.cs
// PURPOSE: Find LINES DRAWN ON TOP OF EACH OTHER in `elements` — exact duplicates, and partly-overlapping
//          collinear segments — and on request delete the redundant ones. AutoCAD's OVERKILL, for Revit
//          detail lines, model lines and room separation lines. The cleanup for a view that prints heavy
//          for no visible reason, and for imported CAD that was exploded into hundreds of stacked lines.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — CurveElements, e.g. from
//          filter-by-category.cs on OST_Lines.
//
// ✱✱ WHY "ARE THESE TWO LINES THE SAME" IS NOT A COMPARISON YOU CAN DO DIRECTLY. Two lines that look
//    identical on screen almost never have equal endpoints in the API — they were drawn in different
//    directions, or they overlap only partly, or floating point puts them a millionth of a foot apart.
//    Comparing endpoints finds almost nothing. What works is to give every line the identity of the
//    INFINITE LINE IT SITS ON, and group by that:
//
//      key = (normalised direction, perpendicular distance from the origin, [line weight])
//
//    Three things make that key work, and each one is a bug if you skip it:
//      1. NORMALISE THE DIRECTION. A line drawn left-to-right and the same line drawn right-to-left have
//         opposite direction vectors. Flip every direction into one consistent half-plane first or the
//         two never match — this is the single most common reason an overlap check "finds nothing".
//      2. ROUND BOTH the direction and the offset before they go in the key. Unrounded floats mean two
//         genuinely collinear lines land in different groups. `resolutionMm` is that tolerance.
//      3. ONLY THEN compare along the line. Within a group every line is collinear, so each becomes a
//         1-D interval and "overlapping" is simple interval arithmetic — no geometry needed.
//
// GOTCHA: THIS IS PLAN-BASED (X/Y). Detail lines are 2D by nature; model lines at different heights on
//         the same plan alignment WILL group together. Check `showZ` output before deleting model lines.
// GOTCHA: LINE STYLE IS PART OF THE IDENTITY BY DEFAULT (`separateByLineStyle`). Two lines on the same
//         alignment in different styles are usually deliberate — a grid line and a hidden line — so they
//         are NOT treated as duplicates unless you say so.
// GOTCHA: only straight lines are handled. Arcs and splines are counted and reported as skipped;
//         collinearity has no meaning for them and pretending otherwise would delete real geometry.
// GOTCHA: DELETING IS DESTRUCTIVE AND NOT UNDONE BY RE-RUNNING. Dry run by default, and the delete path
//         needs allowDestructive on the bridge call.
// RELATED: action-find-duplicates.cs finds coincident ELEMENTS generally; this is the line-specific case
//          it cannot do, because two lines can overlap without being duplicates.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it in report mode on one view first and
//   look at what it claims before deleting anything.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool separateByLineStyle = true;   // treat different line styles as genuinely different lines
double resolutionMm = 1.0;         // how close counts as "the same line" (1 mm is a sensible start)
bool deleteRedundant = false;      // false = report only. true ALSO needs allowDestructive on the call
bool exactDuplicatesOnly = false;  // true = only identical segments; false = partial overlaps too
int maxListed = 40;
// ---- END INPUTS ----

const double MM = 304.8;
double res = resolutionMm / MM;                       // tolerance in feet
Func<double, double> snap = v => Math.Round(v / res) * res;

var curves = elements.OfType<CurveElement>().ToList();
int skippedCurved = 0;

// key -> list of (element, start-along-line, end-along-line, z)
var groups = new Dictionary<string, List<Tuple<CurveElement, double, double, double>>>();

foreach (var ce in curves)
{
    Line ln = null;
    try { ln = ce.GeometryCurve as Line; } catch { }
    if (ln == null || !ln.IsBound) { skippedCurved++; continue; }

    var p1 = ln.GetEndPoint(0);
    var p2 = ln.GetEndPoint(1);

    double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
    double len = Math.Sqrt(dx * dx + dy * dy);
    if (len < 1e-9) { skippedCurved++; continue; }
    dx /= len; dy /= len;

    // 1. normalise direction into one half-plane — see the header
    if (dx < -1e-12 || (Math.Abs(dx) <= 1e-12 && dy < 0)) { dx = -dx; dy = -dy; }

    // 2. perpendicular distance from the origin to this infinite line, signed
    double offset = p1.X * (-dy) + p1.Y * dx;

    string style = "";
    if (separateByLineStyle)
    {
        try { style = ce.LineStyle?.Name ?? ""; } catch { }
    }

    string key = $"{snap(dx):F6}|{snap(dy):F6}|{snap(offset):F6}|{style}";

    // 3. position ALONG the line — turns overlap into interval arithmetic
    double t1 = p1.X * dx + p1.Y * dy;
    double t2 = p2.X * dx + p2.Y * dy;
    if (t1 > t2) { var s = t1; t1 = t2; t2 = s; }

    if (!groups.ContainsKey(key)) groups[key] = new List<Tuple<CurveElement, double, double, double>>();
    groups[key].Add(Tuple.Create(ce, t1, t2, p1.Z));
}

var redundant = new List<Tuple<CurveElement, CurveElement, string>>();  // loser, winner, why

foreach (var g in groups.Where(g => g.Value.Count > 1))
{
    // Longest first — a shorter line buried inside a longer one is the one to lose.
    var items = g.Value.OrderByDescending(i => i.Item3 - i.Item2).ToList();
    var kept = new List<Tuple<CurveElement, double, double, double>>();

    foreach (var cand in items)
    {
        Tuple<CurveElement, double, double, double> covers = null;
        foreach (var k in kept)
        {
            bool identical = Math.Abs(cand.Item2 - k.Item2) <= res && Math.Abs(cand.Item3 - k.Item3) <= res;
            bool inside = cand.Item2 >= k.Item2 - res && cand.Item3 <= k.Item3 + res;
            bool touches = cand.Item2 < k.Item3 - res && cand.Item3 > k.Item2 + res;

            if (identical) { covers = k; break; }
            if (!exactDuplicatesOnly && inside) { covers = k; break; }
            if (!exactDuplicatesOnly && touches) { covers = k; break; }
        }

        if (covers != null)
        {
            bool identical = Math.Abs(cand.Item2 - covers.Item2) <= res && Math.Abs(cand.Item3 - covers.Item3) <= res;
            double dz = Math.Abs(cand.Item4 - covers.Item4);
            string why = identical ? "exact duplicate" : "overlaps / sits inside";
            if (dz > res) why += $" (but {dz * MM:F0} mm apart in Z — check before deleting)";
            redundant.Add(Tuple.Create(cand.Item1, covers.Item1, why));
        }
        else kept.Add(cand);
    }
}

sb.AppendLine($"{curves.Count} line element(s) in; {groups.Count} distinct alignment(s) found.");
if (skippedCurved > 0)
    sb.AppendLine($"{skippedCurved} skipped — arcs, splines or zero-length (collinearity does not apply).");
sb.AppendLine($"Tolerance {resolutionMm} mm. Line style {(separateByLineStyle ? "IS" : "is NOT")} part of the identity. "
            + $"{(exactDuplicatesOnly ? "Exact duplicates only." : "Partial overlaps included.")}");
sb.AppendLine();

if (redundant.Count == 0)
{
    sb.AppendLine("NOTHING REDUNDANT FOUND.");
    sb.AppendLine("If you can see doubled lines, try: separateByLineStyle = false (they may be in different");
    sb.AppendLine("styles), or raise resolutionMm (they may be further apart than you think).");
}
else
{
    sb.AppendLine($"{redundant.Count} redundant line(s) found:");
    sb.AppendLine($"{"Delete",-10}{"Covered by",-12}Why");
    foreach (var r in redundant.Take(maxListed))
        sb.AppendLine($"{r.Item1.Id.ToString(),-10}{r.Item2.Id.ToString(),-12}{r.Item3}");
    if (redundant.Count > maxListed)
        sb.AppendLine($"... and {redundant.Count - maxListed} more (raise maxListed to see them).");

    sb.AppendLine();
    if (!deleteRedundant)
    {
        sb.AppendLine($"REPORT ONLY — nothing deleted. Set deleteRedundant = true (and allowDestructive on the call) to remove these {redundant.Count}.");
        sb.AppendLine("Look at a few of the Ids in Revit first. A line that overlaps is not always a line you want gone.");
    }
    else
    {
        int deleted = 0, failed = 0;
        using (var t = new Transaction(Document, "AJ Tools - Delete overlapping lines"))
        {
            t.Start();
            foreach (var r in redundant)
            {
                try { Document.Delete(r.Item1.Id); deleted++; }
                catch (Exception ex) { failed++; sb.AppendLine($"  {r.Item1.Id}: {ex.Message}"); }
            }
            t.Commit();
        }
        sb.AppendLine($"Deleted {deleted} line(s). Failed: {failed}.");
        sb.AppendLine("Re-run in report mode to confirm the view is clean — verify, do not assume.");
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
