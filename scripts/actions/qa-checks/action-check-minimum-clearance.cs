// ============================================================
// FRAGMENT (action) — action-check-minimum-clearance.cs
// PURPOSE: "Is anything too close to anything?" — measure the real gap between every element in
//          `elements` and a target set, and report every pair closer than the clearance required. Covers
//          BOTH shapes of that question: one flat distance for the whole run ("nothing within 100 mm of
//          my ducts"), or a PER-CATEGORY rules table ("50 mm to structure, 150 mm to electrical, 300 mm
//          to a cable tray") — same sweep, the rules table just changes what counts as a violation.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          services you are checking, e.g. filter-by-multiple-categories.cs over duct/pipe categories.
// Read-only. The model never changes.
//
// ✱✱ A CLASH IS NOT A CLEARANCE FAILURE AND THIS IS THE DIFFERENCE. action-report-clashes.cs answers
//    "do these two occupy the same space" — real intersection, gap zero or less. This answers "are these
//    two closer than they are allowed to be", which is the question that actually fails a coordination
//    review, and it catches the pair that misses by 8 mm and is unbuildable. A hard clash is reported
//    here too, as a gap of 0.
//
// ✱✱ HOW THE DISTANCE IS MEASURED, AND WHAT IT IS WORTH. There is no "minimum distance between two
//    solids" call in the Revit API. Two steps stand in for it:
//      1. BOUNDING-BOX GAP as a pre-filter. The box gap is never larger than the true gap, so a pair
//         whose boxes are further apart than the clearance CANNOT be a violation and is skipped
//         outright. This is what keeps the sweep affordable on a real model.
//      2. For survivors, REAL GEOMETRY: points sampled off one solid's tessellated faces are projected
//         onto the other solid's faces (Face.Project), both ways round, and the smallest result wins.
//    That second number is accurate to roughly the tessellation, not to the micron — it is a SAMPLE, so
//    treat a reported 98 mm against a 100 mm rule as "look at it", not as proof. `sampleStride` trades
//    accuracy against time; the reported figure is honest about being sampled.
//
// ✱✱ ELEMENTS THAT TOUCH ON PURPOSE ARE NOISE, AND THERE IS A SWITCH FOR THEM. A duct passing through
//    its own sleeve, a fitting joined to its duct, a pipe and its own insulation are all "0 mm apart"
//    and none of them is a finding. `ignoreConnected` drops pairs that share a connector, and
//    `ignoreHostAndInserts` drops a pair where one hosts the other.
//
// GOTCHA: LINKED MODELS ARE NOT SCANNED. Structure usually lives in a link, and that is exactly what you
//         want to check against — but a link needs its transform applied to every solid, which is a
//         different sweep. Bind or copy the structure in first, or use filter-by-linked-model-elements.cs
//         to bring the elements across. A run that reports "0 violations" against a linked structural
//         model has checked nothing; the count of scanned targets is printed so that is visible.
// GOTCHA: INSULATION IS NOT INCLUDED unless it is modelled as its own element and caught by the target
//         filter. Clearance measured to the bare duct is optimistic by the insulation thickness on every
//         side — action-check-insulation-clearance.cs is the version that adds it back.
// GOTCHA: this reports BY EXCEPTION. A clean run prints the counts and no table; that is a pass, not a
//         failed script.
// RELATED: action-report-clashes.cs (hard intersection), action-report-nearest-elements.cs (nearest
//          neighbour with no rule attached), action-check-vertical-clearance.cs (separation measured
//          in Z only), action-check-equipment-clearance.cs (a directional access zone, not a gap).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on one duct against one category
//   first and check a reported gap against a dimension in Revit before trusting a whole-floor sweep.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double requiredClearanceMm = 100;        // the flat rule: nothing may be closer than this

// Per-category override. Anything named here uses ITS figure instead of requiredClearanceMm.
// Leave empty to use the flat rule for everything.
var clearanceRules = new Dictionary<BuiltInCategory, double>
{
    // { BuiltInCategory.OST_StructuralFraming, 50 },
    // { BuiltInCategory.OST_CableTray,        300 },
    // { BuiltInCategory.OST_ElectricalFixtures, 150 },
};

// What to measure AGAINST. Empty = measure the `elements` set against itself.
var targetCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_StructuralFraming,
    BuiltInCategory.OST_StructuralColumns,
};

bool ignoreConnected = true;         // skip pairs that share a connector (a fitting and its own duct)
bool ignoreHostAndInserts = true;    // skip pairs where one hosts the other
bool ignoreSameElementPairs = true;  // when measuring a set against itself, skip A-vs-A
int sampleStride = 3;                // 1 = every tessellation vertex (slow, most accurate); 3-5 is usual
int maxReportedRows = 60;            // table cap; the full count is always reported
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the services you want checked).");
    return sb.ToString();
}

// The widest rule in play decides the search envelope — search at the flat figure and every element
// governed by a LARGER per-category rule would be missed.
double widestMm = Math.Max(requiredClearanceMm, clearanceRules.Count > 0 ? clearanceRules.Values.Max() : 0);
double widestFt = ToFeet(widestMm);

// ---- geometry helpers ----
var geoOpts = new Options { DetailLevel = ViewDetailLevel.Medium, ComputeReferences = false, IncludeNonVisibleObjects = false };

Func<Element, List<Solid>> solidsOf = el =>
{
    var found = new List<Solid>();
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geoOpts); } catch { return found; }
    if (ge == null) return found;

    Action<GeometryElement, Transform> walk = null;
    walk = (g, xf) =>
    {
        foreach (GeometryObject go in g)
        {
            var s = go as Solid;
            if (s != null && s.Volume > 1e-9)
            {
                found.Add(xf == null || xf.IsIdentity ? s : SolidUtils.CreateTransformed(s, xf));
                continue;
            }
            var gi = go as GeometryInstance;
            if (gi != null)
            {
                var inner = gi.GetInstanceGeometry();
                if (inner != null) walk(inner, null);   // GetInstanceGeometry is already in model coords
            }
        }
    };
    walk(ge, null);
    return found;
};

// Sampled points off a solid's faces — the "one side" of the projection.
Func<List<Solid>, List<XYZ>, List<XYZ>> samplePoints = (solids, into) =>
{
    foreach (var s in solids)
    {
        foreach (Face f in s.Faces)
        {
            Mesh m = null;
            try { m = f.Triangulate(0.4); } catch { }
            if (m == null) continue;
            for (int i = 0; i < m.Vertices.Count; i += Math.Max(1, sampleStride))
                into.Add(m.Vertices[i]);
        }
    }
    return into;
};

// Smallest distance from a set of points to a set of solids' faces.
Func<List<XYZ>, List<Solid>, double> minPointToSolids = (pts, solids) =>
{
    double best = double.MaxValue;
    foreach (var p in pts)
    {
        foreach (var s in solids)
        {
            foreach (Face f in s.Faces)
            {
                IntersectionResult ir = null;
                try { ir = f.Project(p); } catch { }
                if (ir == null) continue;
                if (ir.Distance < best) best = ir.Distance;
                if (best <= 1e-9) return 0.0;
            }
        }
    }
    return best;
};

Func<Element, BoundingBoxXYZ> bboxOf = el =>
{
    try { return el.get_BoundingBox(null); } catch { return null; }
};

// Gap between two boxes, per axis, 0 if they overlap. Never larger than the true solid gap, which is
// what makes it a safe pre-filter.
Func<BoundingBoxXYZ, BoundingBoxXYZ, double> boxGap = (a, b) =>
{
    if (a == null || b == null) return 0.0;
    double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
    double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
    double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
};

// ---- connector / host relationships worth ignoring ----
Func<Element, HashSet<long>> connectedIdsOf = el =>
{
    var ids = new HashSet<long>();
    if (!ignoreConnected) return ids;
    var idProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
    ConnectorManager cm = null;
    var mc = el as MEPCurve;
    if (mc != null) cm = mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (cm == null && fi != null && fi.MEPModel != null) cm = fi.MEPModel.ConnectorManager;
    if (cm == null) return ids;
    try
    {
        foreach (Connector c in cm.Connectors)
            foreach (Connector r in c.AllRefs)
                if (r.Owner != null) ids.Add(Convert.ToInt64(idProp.GetValue(r.Owner.Id)));
    }
    catch { }
    return ids;
};

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- build the target set ----
var targets = new List<Element>();
if (targetCategories.Count == 0)
{
    targets.AddRange(elements);
    sb.AppendLine("TARGET: the same set, measured against itself.");
}
else
{
    foreach (var cat in targetCategories)
    {
        try
        {
            targets.AddRange(new FilteredElementCollector(Document)
                .OfCategory(cat).WhereElementIsNotElementType().ToList());
        }
        catch { }
    }
    sb.AppendLine($"TARGET: {targets.Count} element(s) across {targetCategories.Count} categor(y/ies) — HOST MODEL ONLY, links are not scanned.");
}

if (targets.Count == 0)
{
    sb.AppendLine("STOP: the target set is empty — nothing to measure against, so a '0 violations' result would be meaningless.");
    return sb.ToString();
}

sb.AppendLine($"RULE: {requiredClearanceMm:F0} mm" + (clearanceRules.Count > 0 ? $", overridden for {clearanceRules.Count} categor(y/ies)" : "") + $"   (search envelope {widestMm:F0} mm)");
sb.AppendLine($"Checking {elements.Count} element(s). Distances are SAMPLED off tessellated faces — accurate to about the tessellation, not exact.");
sb.AppendLine();

// ---- the sweep ----
var violations = new List<(Element A, Element B, double GapMm, double RuleMm)>();
var targetBoxes = targets.Select(t => new { El = t, Box = bboxOf(t) }).Where(x => x.Box != null).ToList();
var solidCache = new Dictionary<long, List<Solid>>();
Func<Element, List<Solid>> cachedSolids = el =>
{
    long k = IdValue(el.Id);
    if (!solidCache.ContainsKey(k)) solidCache[k] = solidsOf(el);
    return solidCache[k];
};

int noGeometry = 0, pairsMeasured = 0;

foreach (var a in elements)
{
    var boxA = bboxOf(a);
    if (boxA == null) { noGeometry++; continue; }
    var solidsA = cachedSolids(a);
    if (solidsA.Count == 0) { noGeometry++; continue; }

    var connected = connectedIdsOf(a);
    long aId = IdValue(a.Id);
    var aHost = (a as FamilyInstance)?.Host;

    var ptsA = samplePoints(solidsA, new List<XYZ>());

    foreach (var tb in targetBoxes)
    {
        var b = tb.El;
        long bId = IdValue(b.Id);
        if (ignoreSameElementPairs && aId == bId) continue;
        if (ignoreConnected && connected.Contains(bId)) continue;
        if (ignoreHostAndInserts)
        {
            if (aHost != null && IdValue(aHost.Id) == bId) continue;
            var bHost = (b as FamilyInstance)?.Host;
            if (bHost != null && IdValue(bHost.Id) == aId) continue;
        }

        // Which rule governs this pair — the target's category override, else the flat figure.
        double ruleMm = requiredClearanceMm;
        if (b.Category != null)
        {
            foreach (var kv in clearanceRules)
            {
                if (IdValue(b.Category.Id) == (long)kv.Key) { ruleMm = kv.Value; break; }
            }
        }
        double ruleFt = ToFeet(ruleMm);

        double bg = boxGap(boxA, tb.Box);
        if (bg > ruleFt) continue;              // cannot be a violation — skip before touching geometry

        var solidsB = cachedSolids(b);
        if (solidsB.Count == 0) continue;

        pairsMeasured++;
        double d = minPointToSolids(ptsA, solidsB);
        if (d > ruleFt)
        {
            // Sampling A onto B can miss where B's vertices are the closest points; check the other way
            // before clearing the pair. Only done for pairs that ALMOST failed, to keep the cost down.
            if (d < ruleFt * 1.5)
            {
                var ptsB = samplePoints(solidsB, new List<XYZ>());
                double d2 = minPointToSolids(ptsB, solidsA);
                if (d2 < d) d = d2;
            }
        }
        if (d <= ruleFt)
            violations.Add((a, b, ToMm(d), ruleMm));
    }
}

// ---- report ----
sb.AppendLine($"PAIRS MEASURED: {pairsMeasured}   VIOLATIONS: {violations.Count}");
if (noGeometry > 0) sb.AppendLine($"NOTE: {noGeometry} element(s) carried no usable solid geometry and were not checked — they are NOT a pass.");
sb.AppendLine();

if (violations.Count == 0)
{
    sb.AppendLine("CLEAR — nothing measured closer than the rule.");
    return sb.ToString();
}

sb.AppendLine("| Element | Category | Too close to | Its category | Gap mm | Rule mm | Short by |");
sb.AppendLine("|---|---|---|---|---|---|---|");
foreach (var v in violations.OrderBy(v => v.GapMm).Take(maxReportedRows))
{
    sb.AppendLine($"| {v.A.Id} | {v.A.Category?.Name ?? "-"} | {v.B.Id} | {v.B.Category?.Name ?? "-"} | {v.GapMm:F0} | {v.RuleMm:F0} | {(v.RuleMm - v.GapMm):F0} |");
}
if (violations.Count > maxReportedRows)
    sb.AppendLine($"\n... and {violations.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
sb.AppendLine("Worst offenders by element:");
foreach (var g in violations.GroupBy(v => v.A.Id).OrderByDescending(g => g.Count()).Take(10))
    sb.AppendLine($"  {g.Key}: {g.Count()} violation(s), tightest {g.Min(x => x.GapMm):F0} mm");

return sb.ToString();
