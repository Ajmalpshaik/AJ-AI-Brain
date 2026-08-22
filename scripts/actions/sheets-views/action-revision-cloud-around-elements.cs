// ============================================================
// FRAGMENT (action) — action-revision-cloud-around-elements.cs
// PURPOSE: Draw revision clouds AROUND the elements in `elements` — "cloud what changed", where the
//          cloud comes from the model rather than from you typing corner coordinates. Elements that sit
//          near each other get ONE cloud between them; separate groups get separate clouds.
//          The element-driven counterpart to `creators/create-revision-cloud.cs`, which needs a
//          rectangle in millimetres and knows nothing about what is inside it.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// GOTCHA: THE CLOUDS ARE RECTANGLES PER CLUSTER — NOT AN ORTHOGONAL OUTLINE. The add-in's version
//         rasterises the element footprints into a grid, extracts connected components, traces each
//         boundary loop and simplifies it, so its cloud follows the actual STEPPED SHAPE of what
//         changed. That is a genuinely subtle algorithm and half-implementing it would produce
//         plausible-looking wrong outlines, so this does the honest simpler thing: cluster by
//         proximity, one rectangle per cluster. Written up in knowledge/live-model/revisions.md if the
//         full outline is ever wanted.
// GOTCHA: `clusterGapMm` IS THE WHOLE BEHAVIOUR. Too small and you get one cloud per element; too large
//         and everything merges into one cloud round the whole floor. Dry-run and read the cluster count
//         before committing — that number is the thing to tune.
// GOTCHA: the rectangle is built in the VIEW'S OWN PLANE (Origin + RightDirection/UpDirection), so the
//         same code works in a plan, a section or on a sheet. Element positions are PROJECTED onto that
//         plane — an element behind the view still contributes its footprint.
// GOTCHA: placing a cloud in a view AUTO-ADDS that view's sheet to the Revision's sheet list. That is
//         Revit's own behaviour, not this fragment's — see knowledge/live-model/revisions.md.
// GOTCHA: the Revision must already exist. `creators/create-revision.cs` makes one;
//         `actions/sheet-dates-revisions/action-report-revisions.cs` lists what is there to pick from.
// NOTE: dry-run by default, and it reports the cluster count and each cloud's size before drawing.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Revision Clouds by
//   Elements. `RevisionCloud.Create` itself is PROVEN here (create-revision-cloud.cs, 2026-08-07);
//   what is unproven is the clustering and the projection. Compile-checked on 2020/2024/2027.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;                   // REQUIRED — the view or sheet to draw the clouds in
int revisionSequenceNumber = 1;      // which project Revision the clouds belong to
double marginMm = 300;               // how far outside the elements each cloud sits
double clusterGapMm = 2000;          // elements closer than this share a cloud — TUNE THIS
bool dryRun = true;                  // true = report the clusters only; false = actually draw
int maxClouds = 50;                  // hard stop, so a bad gap value cannot draw hundreds
// ---- END INPUTS ----

const double MM = 304.8;

var view = Document.GetElement(new ElementId(viewIdInt)) as View;
var revision = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
    .FirstOrDefault(r => r.SequenceNumber == revisionSequenceNumber);

if (view == null || view.IsTemplate)
{
    sb.AppendLine($"viewIdInt {viewIdInt} is not a usable view.");
}
else if (revision == null)
{
    var have = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
        .OrderBy(r => r.SequenceNumber).Select(r => r.SequenceNumber + ": " + r.Description);
    sb.AppendLine($"No Revision with SequenceNumber {revisionSequenceNumber}. Available: " + string.Join(" | ", have));
}
else if (elements.Count == 0)
{
    sb.AppendLine("No elements to cloud — nothing to do.");
}
else
{
    // ---------- project every element onto the VIEW'S plane ----------
    XYZ o = view.Origin, rt = view.RightDirection, up = view.UpDirection;
    var boxes = new List<double[]>();   // {minU, maxU, minV, maxV}
    int noGeom = 0;

    foreach (var e in elements)
    {
        var bb = e == null ? null : e.get_BoundingBox(null);
        if (bb == null || bb.Min == null || bb.Max == null) { noGeom++; continue; }

        var t = bb.Transform ?? Transform.Identity;
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var c in new[] {
            new XYZ(bb.Min.X, bb.Min.Y, bb.Min.Z), new XYZ(bb.Max.X, bb.Min.Y, bb.Min.Z),
            new XYZ(bb.Max.X, bb.Max.Y, bb.Min.Z), new XYZ(bb.Min.X, bb.Max.Y, bb.Min.Z),
            new XYZ(bb.Min.X, bb.Min.Y, bb.Max.Z), new XYZ(bb.Max.X, bb.Min.Y, bb.Max.Z),
            new XYZ(bb.Max.X, bb.Max.Y, bb.Max.Z), new XYZ(bb.Min.X, bb.Max.Y, bb.Max.Z) })
        {
            var w = t.OfPoint(c) - o;
            double u = w.DotProduct(rt), v = w.DotProduct(up);
            if (u < u0) u0 = u; if (u > u1) u1 = u;
            if (v < v0) v0 = v; if (v > v1) v1 = v;
        }
        boxes.Add(new[] { u0, u1, v0, v1 });
    }

    if (boxes.Count == 0)
    {
        sb.AppendLine($"None of the {elements.Count} element(s) gave a bounding box — nothing to cloud.");
    }
    else
    {
        // ---------- cluster boxes that are within clusterGapMm of each other ----------
        double gap = clusterGapMm / MM, margin = marginMm / MM;
        var clusterOf = Enumerable.Range(0, boxes.Count).ToArray();   // union-find, flat
        Func<int, int> root = null;
        root = i => clusterOf[i] == i ? i : (clusterOf[i] = root(clusterOf[i]));

        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i]; var b = boxes[j];
                // gap between two rectangles, 0 when they overlap
                double du = Math.Max(0, Math.Max(a[0] - b[1], b[0] - a[1]));
                double dv = Math.Max(0, Math.Max(a[2] - b[3], b[2] - a[3]));
                if (Math.Sqrt(du * du + dv * dv) <= gap)
                {
                    int ra = root(i), rb = root(j);
                    if (ra != rb) clusterOf[ra] = rb;
                }
            }

        var merged = new Dictionary<int, double[]>();
        for (int i = 0; i < boxes.Count; i++)
        {
            int r = root(i);
            if (!merged.ContainsKey(r)) { merged[r] = (double[])boxes[i].Clone(); continue; }
            var m = merged[r]; var b = boxes[i];
            m[0] = Math.Min(m[0], b[0]); m[1] = Math.Max(m[1], b[1]);
            m[2] = Math.Min(m[2], b[2]); m[3] = Math.Max(m[3], b[3]);
        }

        var clusters = merged.Values.ToList();

        sb.AppendLine((dryRun ? "DRY RUN — nothing drawn. " : "") +
                      $"{elements.Count} element(s) -> {clusters.Count} cluster(s) at a {clusterGapMm} mm gap, " +
                      $"revision {revision.SequenceNumber} \"{revision.Description}\", view '{view.Name}'.");
        if (noGeom > 0) sb.AppendLine($"{noGeom} element(s) had no bounding box and were ignored.");
        sb.AppendLine("");
        sb.AppendLine("Cloud | Width (mm) | Height (mm)");
        sb.AppendLine("--- | --- | ---");
        int n = 0;
        foreach (var c in clusters.OrderByDescending(c => (c[1] - c[0]) * (c[3] - c[2])).Take(25))
            sb.AppendLine((++n) + " | " + Math.Round((c[1] - c[0] + 2 * margin) * MM) + " | " +
                          Math.Round((c[3] - c[2] + 2 * margin) * MM));
        if (clusters.Count > 25) sb.AppendLine("... and " + (clusters.Count - 25) + " more");

        if (clusters.Count > maxClouds)
        {
            sb.AppendLine("");
            sb.AppendLine($"STOPPED: {clusters.Count} clusters exceeds maxClouds ({maxClouds}). Raise " +
                          "clusterGapMm so fewer, bigger clouds are drawn — that is the dial for this.");
        }
        else if (!dryRun)
        {
            int drawn = 0, failed = 0;
            var madeIds = new List<string>();

            using (var t = new Transaction(Document, "AJ Tools - Revision Clouds Around Elements"))
            {
                t.Start();
                try
                {
                    foreach (var c in clusters)
                    {
                        double u0 = c[0] - margin, u1 = c[1] + margin;
                        double v0 = c[2] - margin, v1 = c[3] + margin;

                        Func<double, double, XYZ> pt = (u, v) => o + rt.Multiply(u) + up.Multiply(v);
                        var curves = new List<Curve> {
                            Line.CreateBound(pt(u0, v0), pt(u1, v0)),
                            Line.CreateBound(pt(u1, v0), pt(u1, v1)),
                            Line.CreateBound(pt(u1, v1), pt(u0, v1)),
                            Line.CreateBound(pt(u0, v1), pt(u0, v0))
                        };

                        try
                        {
                            var cloud = RevisionCloud.Create(Document, view, revision.Id, curves);
                            if (cloud != null) { drawn++; if (madeIds.Count < 100) madeIds.Add(cloud.Id.ToString()); }
                            else failed++;
                        }
                        catch { failed++; }
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED — rolled back, nothing drawn. Reason: {ex.Message}");
                    return sb.ToString();
                }
            }

            sb.AppendLine("");
            sb.AppendLine($"Drew {drawn} revision cloud(s)" + (failed > 0 ? $", {failed} failed" : "") + ".");
            if (madeIds.Count > 0) sb.AppendLine("New cloud ids (delete these to undo): " + string.Join(", ", madeIds));
            sb.AppendLine("Note: placing a cloud in a view AUTO-ADDS that view's sheet to the Revision's " +
                          "sheet list — Revit's own behaviour, not this fragment's.");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
