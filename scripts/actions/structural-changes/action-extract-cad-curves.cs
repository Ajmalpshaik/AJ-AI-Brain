// ============================================================
// FRAGMENT (action) — action-extract-cad-curves.cs
// PURPOSE: Trace a linked/imported CAD file into real Revit lines — read the curves on chosen DWG
//          layer(s) from the ImportInstances in `elements` and recreate them as Model Lines or Detail
//          Lines. The "convert this CAD background to Revit linework" job every modeller knows.
//          (Dynamo-package equivalent: Bimorph's CurvesFromCADLayers.)
// ASSUMES: elements (List<Element>, ImportInstances — from filters/filter-by-links.cs with CAD included)
//          and sb exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: run with dryRun=true first — it reports curve counts PER LAYER so the layer name filter can be
//         chosen from reality instead of guessed (CAD layer names are case-sensitive here).
// GOTCHA: Model Lines need a sketch plane — flat (horizontal) curves get a plane at their own Z; curves
//         that aren't flat are skipped and COUNTED. Detail Lines land in one view (targetViewIdInt) and
//         only take flat curves too.
// GOTCHA: PolyLines are exploded into straight segments; splines/ellipses are skipped and counted —
//         extend deliberately if a real case needs them, don't pre-build.
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 3 (Dynamo package harvest); needs a CAD fixture.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                 // true = report layers + curve counts only, create nothing
string[] layerNames = new string[] { };  // exact CAD layer names to trace; empty = ALL layers (careful!)
string lineKind = "detail";         // "model" | "detail"
int targetViewIdInt = 0;            // detail mode only — the view the detail lines go into
int maxCurves = 500;                // safety cap on created lines per run
// ---- END INPUTS ----

Func<GeometryObject, string> layerOf = g =>
{
    var gs = Document.GetElement(g.GraphicsStyleId) as GraphicsStyle;
    return gs?.GraphicsStyleCategory?.Name ?? "(no layer)";
};

// gather curves per layer from every ImportInstance (instance geometry = transforms already applied)
var perLayer = new Dictionary<string, List<Curve>>();
int importCount = 0, splineSkipped = 0;

foreach (var el in elements)
{
    var imp = el as ImportInstance;
    if (imp == null) continue;
    importCount++;

    var geo = imp.get_Geometry(new Options());
    if (geo == null) continue;
    foreach (GeometryObject obj in geo)
    {
        var inst = obj as GeometryInstance;
        if (inst == null) continue;
        foreach (GeometryObject g in inst.GetInstanceGeometry())
        {
            string layer = layerOf(g);
            if (layerNames.Length > 0 && !layerNames.Contains(layer)) continue;

            var curves = new List<Curve>();
            if (g is Line || g is Arc) curves.Add((Curve)g);
            else if (g is PolyLine)
            {
                var pts = ((PolyLine)g).GetCoordinates();
                for (int i = 0; i < pts.Count - 1; i++)
                    if (pts[i].DistanceTo(pts[i + 1]) > 1e-6)
                        curves.Add(Line.CreateBound(pts[i], pts[i + 1]));
            }
            else if (g is Curve) { splineSkipped++; continue; } // spline/ellipse — deliberate skip

            if (curves.Count == 0) continue;
            if (!perLayer.ContainsKey(layer)) perLayer[layer] = new List<Curve>();
            perLayer[layer].AddRange(curves);
        }
    }
}

if (importCount == 0)
{
    sb.AppendLine("No ImportInstances in `elements` — feed this from filter-by-links.cs (CAD links/imports).");
}
else
{
    sb.AppendLine($"CAD curves found across {importCount} import(s){(layerNames.Length > 0 ? $" (layer filter: {string.Join(", ", layerNames)})" : " (ALL layers)")}:");
    foreach (var kv in perLayer.OrderByDescending(k => k.Value.Count))
        sb.AppendLine($"  - layer '{kv.Key}': {kv.Value.Count} curve segment(s)");
    if (splineSkipped > 0) sb.AppendLine($"  ({splineSkipped} spline/ellipse object(s) skipped — see header)");

    if (dryRun)
    {
        sb.AppendLine("DRY RUN — nothing created. Rerun with dryRun=false + the layer list confirmed by the user.");
    }
    else
    {
        var all = perLayer.SelectMany(kv => kv.Value).Take(maxCurves).ToList();
        int totalFound = perLayer.Sum(kv => kv.Value.Count);
        View targetView = lineKind == "detail" ? Document.GetElement(new ElementId(targetViewIdInt)) as View : null;

        if (lineKind == "detail" && (targetView == null || targetView.IsTemplate))
        {
            sb.AppendLine($"targetViewIdInt {targetViewIdInt} is not a usable view — no detail lines created.");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Extract CAD Curves"))
            {
                t.Start();
                try
                {
                    int created = 0, nonFlat = 0;
                    foreach (var c in all)
                    {
                        var p0 = c.GetEndPoint(0); var p1 = c.GetEndPoint(1);
                        bool flat = Math.Abs(p0.Z - p1.Z) < 1e-6;
                        if (!flat) { nonFlat++; continue; }

                        if (lineKind == "model")
                        {
                            var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p0);
                            var sp = SketchPlane.Create(Document, plane);
                            Document.Create.NewModelCurve(c, sp);
                        }
                        else
                        {
                            Document.Create.NewDetailCurve(targetView, c);
                        }
                        created++;
                    }
                    t.Commit();
                    sb.AppendLine($"Created {created} {lineKind} line(s){(nonFlat > 0 ? $", {nonFlat} non-flat curve(s) skipped" : "")}{(totalFound > maxCurves ? $", capped at {maxCurves} of {totalFound} found" : "")}.");
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"FAILED mid-creation — rolled back, NO lines were created. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
