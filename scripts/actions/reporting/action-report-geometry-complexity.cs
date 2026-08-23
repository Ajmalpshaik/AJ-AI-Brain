// ============================================================
// FRAGMENT (action) — action-report-geometry-complexity.cs
// PURPOSE: Find the families that are making the model SLOW — how much actual geometry each family type
//          carries, measured as triangle count, at each detail level. The answer to "the model has got
//          heavy and nobody knows why", and the check to run on a downloaded manufacturer family before
//          it goes into the library.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs.
//          Pass a whole category, or the whole model, and read the top of the list.
//
// ✱✱ WHY TRIANGLES AND NOT FILE SIZE. A family's file size tells you what it weighs on disk. What slows
//    Revit down is what it has to DRAW — every face of every solid, tessellated into triangles, for every
//    instance in every open view. `face.Triangulate().NumTriangles` is that number directly. A cast-in
//    sleeve modelled with a chamfered thread can carry more triangles than an entire air handling unit,
//    and nothing in the family's name or size hints at it.
//
// ✱✱ MEASURED PER TYPE, NOT PER INSTANCE — and that is the point. Geometry belongs to the type, so 400
//    instances of a 12,000-triangle family is 4.8 million triangles from ONE library choice. The report
//    multiplies the two out, because the type's own count is not what hurts; the product is.
//
// ✱✱ ALL THREE DETAIL LEVELS ARE MEASURED, because that comparison is the actionable part. A family that
//    reads 40 triangles Coarse and 40,000 Fine is BEHAVING — it has proper detail-level control and costs
//    nothing on a working plan. A family with the same big number at all three levels has none, and it
//    pays full price in every view. That gap is what tells you whether to fix the family or replace it.
//
// GOTCHA: this reads geometry for every distinct TYPE in `elements`, which is real work. On a whole
//         model expect it to take a while — `maxTypes` stops it running away, and passing one category
//         at a time is the sane way to use it.
// GOTCHA: triangle counts are Revit's tessellation at the moment of asking, not a fixed property. Use
//         them to COMPARE families against each other, not as an absolute score.
// GOTCHA: a family with no solid geometry (an annotation, a line-based family, a face-hosted void)
//         reports 0 and is not broken.
// RELATED: model-health-audit.cs covers warnings, worksets and purge candidates — the other half of
//          "why is this model unhappy". This is the geometry half, which it does not do.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE category first and see whether
//   the ranking matches what you already suspect is heavy.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxTypes = 60;             // stop after this many distinct types (0 = no cap, slow on a whole model)
int listTop = 25;              // how many rows to print
bool measureAllDetailLevels = true;   // false = Fine only, roughly 3x faster
long flagAboveTriangles = 5000;       // per-type count worth calling out
// ---- END INPUTS ----

var perType = new Dictionary<ElementId, long[]>();      // typeId -> [coarse, medium, fine]
var typeLabel = new Dictionary<ElementId, string>();
var instanceCount = new Dictionary<ElementId, int>();
int noGeometry = 0, failed = 0;

Func<Element, ViewDetailLevel, long> triangles = (el, level) =>
{
    long n = 0;
    try
    {
        var opt = new Options { DetailLevel = level, IncludeNonVisibleObjects = false };
        var geo = el.get_Geometry(opt);
        if (geo == null) return 0;

        // Walk instances as well as top-level solids — most family geometry sits one level down.
        Action<GeometryElement> walk = null;
        walk = (ge) =>
        {
            foreach (GeometryObject g in ge)
            {
                var solid = g as Solid;
                if (solid != null && solid.Faces.Size > 0)
                {
                    foreach (Face f in solid.Faces)
                    {
                        try { var m = f.Triangulate(); if (m != null) n += m.NumTriangles; } catch { }
                    }
                    continue;
                }
                var inst = g as GeometryInstance;
                if (inst != null)
                {
                    var sub = inst.GetInstanceGeometry();
                    if (sub != null) walk(sub);
                }
            }
        };
        walk(geo);
    }
    catch { }
    return n;
};

foreach (var el in elements)
{
    var tid = el.GetTypeId();
    if (tid == null || tid == ElementId.InvalidElementId) continue;

    if (!perType.ContainsKey(tid))
    {
        if (maxTypes > 0 && perType.Count >= maxTypes) break;

        var te = Document.GetElement(tid);
        string label;
        try
        {
            var fs = te as FamilySymbol;
            label = fs != null ? $"{fs.Family?.Name} : {fs.Name}" : (te?.Name ?? tid.ToString());
        }
        catch { label = tid.ToString(); }

        long c = 0, m = 0, f = 0;
        try
        {
            f = triangles(el, ViewDetailLevel.Fine);
            if (measureAllDetailLevels)
            {
                c = triangles(el, ViewDetailLevel.Coarse);
                m = triangles(el, ViewDetailLevel.Medium);
            }
        }
        catch { failed++; }

        if (f == 0 && c == 0 && m == 0) noGeometry++;

        perType[tid] = new[] { c, m, f };
        typeLabel[tid] = $"{el.Category?.Name} | {label}";
        instanceCount[tid] = 0;
    }
    instanceCount[tid] = instanceCount[tid] + 1;
}

if (perType.Count == 0)
{
    sb.AppendLine("No typed elements in — put a filter fragment above this (filter-by-category.cs works well).");
}
else
{
    var ranked = perType.Keys
        .OrderByDescending(k => perType[k][2] * (long)instanceCount[k])
        .ToList();

    long grandTotal = ranked.Sum(k => perType[k][2] * (long)instanceCount[k]);

    sb.AppendLine($"{perType.Count} distinct type(s) measured across {elements.Count} element(s).");
    sb.AppendLine($"TOTAL triangles drawn at Fine detail: {grandTotal:N0}");
    if (noGeometry > 0) sb.AppendLine($"{noGeometry} type(s) carry no solid geometry (annotation, line-based, void) — not a fault.");
    if (failed > 0) sb.AppendLine($"{failed} type(s) could not be read.");
    sb.AppendLine();

    sb.AppendLine(measureAllDetailLevels
        ? $"{"Coarse",10}{"Medium",10}{"Fine",10}{"Inst",7}{"Total fine",14}  Type"
        : $"{"Fine",10}{"Inst",7}{"Total fine",14}  Type");

    foreach (var k in ranked.Take(listTop))
    {
        var v = perType[k];
        long total = v[2] * (long)instanceCount[k];
        sb.AppendLine(measureAllDetailLevels
            ? $"{v[0],10:N0}{v[1],10:N0}{v[2],10:N0}{instanceCount[k],7}{total,14:N0}  {typeLabel[k]}"
            : $"{v[2],10:N0}{instanceCount[k],7}{total,14:N0}  {typeLabel[k]}");
    }
    if (ranked.Count > listTop) sb.AppendLine($"... and {ranked.Count - listTop} more type(s).");

    // The comparison that tells you whether the family is behaving.
    if (measureAllDetailLevels)
    {
        var noDetailControl = ranked
            .Where(k => perType[k][2] >= flagAboveTriangles
                     && perType[k][0] > 0
                     && perType[k][0] >= perType[k][2] * 0.8)
            .ToList();

        sb.AppendLine();
        if (noDetailControl.Count > 0)
        {
            sb.AppendLine($"NO DETAIL-LEVEL CONTROL — {noDetailControl.Count} type(s) draw nearly as much Coarse as Fine,");
            sb.AppendLine("so they cost full price in every working view. These are the ones worth fixing or replacing:");
            foreach (var k in noDetailControl.Take(10))
                sb.AppendLine($"    {perType[k][0]:N0} coarse vs {perType[k][2]:N0} fine  x{instanceCount[k]}  {typeLabel[k]}");
        }
        else
        {
            sb.AppendLine("Every heavy type drops off at Coarse — detail-level control is working across this set.");
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
