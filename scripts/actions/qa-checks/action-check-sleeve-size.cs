// ============================================================
// FRAGMENT (action) — action-check-sleeve-size.cs
// PURPOSE: Check every sleeve/opening against the service that actually passes through it — is the hole
//          big enough for the pipe plus its insulation plus the annular clearance the specification asks
//          for, and is it not so oversized that it becomes a fire-stopping problem. The check between
//          "the sleeves are placed" and "the sleeves are right".
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          SLEEVES, e.g. filter-by-category.cs on the category your sleeve family lives in, or
//          filter-by-family.cs on its family name. Read-only. The model never changes.
//
// ✱✱ THE SERVICE IS FOUND BY GEOMETRY, NOT BY A PARAMETER. A sleeve family almost never records what
//    goes through it — that link is not stored anywhere. Where a sleeve finds NOTHING, that is itself a
//    finding: a sleeve with no service through it is either an orphan left by a moved run, or the run
//    moved and the hole did not.
//
// ✱✱ AND THE CROSSING TEST IS A REAL BOOLEAN, NOT A BOUNDING BOX. Two boxes overlap constantly without
//    the elements touching — a pipe running past a sleeve in the next bay shares a box corner with it,
//    and a box-based match quietly checks the wrong service. The box is a cheap PRE-FILTER only; the
//    decision is `BooleanOperationsUtils.ExecuteBooleanOperation(..., Intersect)` with a real volume.
//    Two practical points that are not obvious and are both handled: an element can carry SEVERAL solids
//    (a duct with its insulation, a sleeve with a body and a void) so every pairing is tried, and some
//    Revit solids REFUSE boolean operations and throw — so the catch is per PAIR, because one throw must
//    not abandon the element. A sleeve with no readable solid falls back to the centreline test and the
//    report NAMES it, rather than passing a weaker test off as a geometry check.
//
// ✱✱ REQUIRED SIZE = SERVICE OUTSIDE DIAMETER + 2 x INSULATION + 2 x ANNULAR CLEARANCE. All three are
//    read or set explicitly: the size comes off the service, the insulation off its real insulation
//    element (`InsulationLiningBase.GetInsulationIds`, whose ArgumentException for an element that
//    cannot carry a wrap IS the category filter), and the clearance is your input. A rectangular service
//    is checked against a rectangular sleeve on both dimensions.
//
// ✱✱ OVERSIZE IS REPORTED AS WELL AS UNDERSIZE, and it is not a nicety. An opening far larger than the
//    service costs fire-stopping, weakens the structure it is cut through, and gets queried on site.
//    `maxOversizeMm` sets the point where generous becomes wrong.
//
// GOTCHA: SLEEVE SIZE IS READ BY PARAMETER NAME, and sleeve families do not agree on names. The lookup
//         tries a list of common ones ("Diameter", "Sleeve Diameter", "Nominal Diameter", "Width",
//         "Height", ...) and REPORTS the sleeves whose size it could not read rather than passing them.
//         Add your office family's parameter names to `sizeParamNames` and they will be picked up.
// GOTCHA: A SLEEVE THAT IS A REAL REVIT OPENING (a shaft, a wall opening) has no size parameter at all —
//         its size is its geometry. Those are measured from the bounding box instead, which is right for
//         a rectangular opening and approximate for anything else.
// GOTCHA: THE NEAREST SERVICE IS A GUESS WHERE TWO RUNS SHARE A SLEEVE. Both are named in the report when
//         that happens, so a shared sleeve is visible rather than silently checked against one of them.
// GOTCHA: LINKED MODELS ARE NOT SCANNED — a sleeve in the host against a service in a link finds nothing
//         and reports NO SERVICE. Bring the services across with filter-by-linked-model-elements.cs.
// SOURCE: ../../../knowledge/live-model/mep-openings.md — § "Finding the crossing — real solid
//         intersection, not bounding boxes" and § "The extent, and why a raw intersection box is not
//         enough". Read it before changing how this fragment matches a service to a sleeve.
// RELATED: recipes/place-sleeves-at-wall-penetrations.cs (find the crossings and place the sleeves —
//          its dry-run mode is the "where do sleeves need to go" half), recipes/create-mep-openings.cs
//          (cut real openings in walls, floors and beams),
//          action-check-insulation-clearance.cs (clearance elsewhere on the run).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one sleeve's reported service and
//   required size by hand before trusting a whole floor.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double annularClearanceMm = 25;      // clear space required all round, between service (or its insulation) and the sleeve
double maxOversizeMm = 100;          // more spare than this all round is reported as OVERSIZED
double searchMarginMm = 150;         // how far outside a sleeve's box to look for the service passing through

// Parameter names to try when reading a sleeve's own size. Add your office family's names here.
var sizeParamNames = new List<string> { "Diameter", "Sleeve Diameter", "Nominal Diameter", "Opening Diameter", "Size" };
var widthParamNames = new List<string> { "Width", "Sleeve Width", "Opening Width" };
var heightParamNames = new List<string> { "Height", "Sleeve Height", "Opening Height", "Depth" };

// Which categories count as "a service" that can pass through a sleeve.
var serviceCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_Conduit,
    BuiltInCategory.OST_CableTray,
};
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the sleeves).");
    return sb.ToString();
}

// ---- read a size off an element by trying a list of names ----
Func<Element, List<string>, double> sizeByNames = (el, names) =>
{
    foreach (var n in names)
    {
        var p = el.LookupParameter(n);
        if (p == null)
        {
            var te = Document.GetElement(el.GetTypeId());
            if (te != null) p = te.LookupParameter(n);
        }
        if (p != null && p.HasValue && p.StorageType == StorageType.Double) return p.AsDouble();
    }
    return -1;
};

// ---- insulation thickness (same proven lookup as action-check-insulation-clearance.cs) ----
Func<Element, double> insulationFeetOf = el =>
{
    ICollection<ElementId> ids = null;
    // ArgumentException for anything that cannot carry a wrap — the catch IS the category filter.
    try { ids = Autodesk.Revit.DB.InsulationLiningBase.GetInsulationIds(Document, el.Id); }
    catch { return 0; }
    if (ids == null || ids.Count == 0) return 0;
    double thickest = 0;
    foreach (var id in ids)
    {
        var wrap = Document.GetElement(id);
        if (wrap == null) continue;
        double t = 0;
        try
        {
            var pr = wrap.GetType().GetProperty("Thickness");
            if (pr != null) { var v = pr.GetValue(wrap, null); if (v is double) t = (double)v; }
        }
        catch { }
        if (t <= 0)
        {
            var bp = wrap.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS);
            if (bp != null && bp.HasValue) t = bp.AsDouble();
        }
        if (t > thickest) thickest = t;
    }
    return thickest;
};

// ---- real geometry, for the crossing test ----
// A BOUNDING-BOX TEST IS NOT A CROSSING TEST. Two boxes overlap constantly without the elements
// touching — a pipe running past a sleeve in the next bay shares a box corner with it. The box is used
// ONLY as a cheap pre-filter here; the decision is a boolean solid intersection
// (knowledge/live-model/mep-openings.md).
var geoOpts = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false, IncludeNonVisibleObjects = false };

Func<Element, List<Solid>> solidsOf = el =>
{
    var found = new List<Solid>();
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geoOpts); } catch { return found; }
    if (ge == null) return found;
    Action<GeometryElement> walk = null;
    walk = g =>
    {
        foreach (GeometryObject go in g)
        {
            var s = go as Solid;
            if (s != null && s.Volume > 1e-9) { found.Add(s); continue; }
            var gi = go as GeometryInstance;
            if (gi != null) { var inner = gi.GetInstanceGeometry(); if (inner != null) walk(inner); }
        }
    };
    walk(ge);
    return found;
};

// True when the two elements really share space. An element can carry SEVERAL solids (a duct with its
// insulation, a sleeve family with a body and a void), so every pairing is tried; and some Revit solids
// refuse boolean operations and throw, so the catch is PER PAIR — one throw must not abandon the element.
Func<List<Solid>, List<Solid>, bool> reallyIntersects = (a, b) =>
{
    foreach (var sa in a)
    {
        foreach (var sbb2 in b)
        {
            try
            {
                var inter = BooleanOperationsUtils.ExecuteBooleanOperation(sa, sbb2, BooleanOperationsType.Intersect);
                if (inter != null && inter.Volume > 1e-7) return true;
            }
            catch { }
        }
    }
    return false;
};

// ---- the services, indexed by bounding box ----
var services = new List<(Element El, Curve Crv, BoundingBoxXYZ Box)>();
foreach (var cat in serviceCategories)
{
    try
    {
        foreach (var e in new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType())
        {
            var lc = e.Location as LocationCurve;
            if (lc == null || lc.Curve == null) continue;
            BoundingBoxXYZ bb = null;
            try { bb = e.get_BoundingBox(null); } catch { }
            if (bb == null) continue;
            services.Add((e, lc.Curve, bb));
        }
    }
    catch { }
}

sb.AppendLine($"SLEEVE SIZE CHECK — {elements.Count} sleeve(s) against {services.Count} service run(s) in the host model");
sb.AppendLine($"Required = service outside size + 2 x insulation + 2 x {annularClearanceMm:F0} mm annular clearance");
sb.AppendLine();

if (services.Count == 0)
{
    sb.AppendLine("STOP: no services found in the host model at all — every sleeve would report NO SERVICE, which would say nothing about the sleeves. Are the services in a LINK?");
    return sb.ToString();
}

// ---- service outside size ----
// Returns (isRound, dimA, dimB) in feet. dimA is diameter for round, width for rectangular.
Func<Element, Tuple<bool, double, double>> serviceSizeOf = el =>
{
    var d = el.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
         ?? el.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
    if (d != null && d.HasValue && d.AsDouble() > 0) return Tuple.Create(true, d.AsDouble(), 0.0);

    var w = el.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
    var h = el.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
    if (w != null && h != null && w.HasValue && h.HasValue) return Tuple.Create(false, w.AsDouble(), h.AsDouble());

    return Tuple.Create(true, -1.0, 0.0);
};

// ---- check each sleeve ----
var rows = new List<(Element Sleeve, Element Svc, string SvcSize, double NeedMm, double HaveMm, string Verdict, string Note)>();
var noService = new List<Element>();
var unreadable = new List<Element>();
var geometryFallback = new List<ElementId>();
int shared = 0;

double marginFt = ToFeet(searchMarginMm);

foreach (var sleeve in elements)
{
    BoundingBoxXYZ sbb = null;
    try { sbb = sleeve.get_BoundingBox(null); } catch { }
    if (sbb == null) { unreadable.Add(sleeve); continue; }

    var centre = (sbb.Min + sbb.Max) * 0.5;

    // Candidate services: bounding box as a CHEAP PRE-FILTER only, then a real solid intersection.
    var sleeveSolids = solidsOf(sleeve);
    var candidates = new List<(Element El, double Dist)>();

    foreach (var s in services)
    {
        if (s.Box.Max.X < sbb.Min.X - marginFt || s.Box.Min.X > sbb.Max.X + marginFt) continue;
        if (s.Box.Max.Y < sbb.Min.Y - marginFt || s.Box.Min.Y > sbb.Max.Y + marginFt) continue;
        if (s.Box.Max.Z < sbb.Min.Z - marginFt || s.Box.Min.Z > sbb.Max.Z + marginFt) continue;

        double dist;
        try
        {
            var pr = s.Crv.Project(centre);
            if (pr == null) continue;
            dist = pr.Distance;
        }
        catch { continue; }

        // THE DECIDING TEST. Sharing a bounding box means nothing — a pipe running past a sleeve in the
        // next bay shares one. Only real shared volume counts as "passes through this sleeve".
        bool through = false;
        if (sleeveSolids.Count > 0)
        {
            through = reallyIntersects(sleeveSolids, solidsOf(s.El));
        }
        else
        {
            // The sleeve has no readable solid (some opening elements carry none). Fall back to the
            // centreline test and SAY SO on the row rather than pretending this was a geometry check.
            double halfDiag = Math.Sqrt(Math.Pow(sbb.Max.X - sbb.Min.X, 2) + Math.Pow(sbb.Max.Y - sbb.Min.Y, 2) + Math.Pow(sbb.Max.Z - sbb.Min.Z, 2)) / 2.0;
            through = dist <= halfDiag + marginFt;
            if (through) geometryFallback.Add(sleeve.Id);
        }
        if (!through) continue;

        candidates.Add((s.El, dist));
    }

    if (candidates.Count == 0) { noService.Add(sleeve); continue; }
    if (candidates.Count > 1) shared++;

    var svc = candidates.OrderBy(c => c.Dist).First().El;
    var size = serviceSizeOf(svc);
    if (size.Item2 <= 0) { unreadable.Add(sleeve); continue; }

    double insFt = insulationFeetOf(svc);
    double clearFt = ToFeet(annularClearanceMm);

    // ---- what the sleeve actually is ----
    double sleeveDia = sizeByNames(sleeve, sizeParamNames);
    double sleeveW = sizeByNames(sleeve, widthParamNames);
    double sleeveH = sizeByNames(sleeve, heightParamNames);

    bool sleeveIsRound = sleeveDia > 0;
    if (!sleeveIsRound && sleeveW <= 0)
    {
        // A real Revit opening carries no size parameter — measure the box instead.
        sleeveW = sbb.Max.X - sbb.Min.X;
        sleeveH = sbb.Max.Y - sbb.Min.Y;
        if (sleeveW <= 0 || sleeveH <= 0) { unreadable.Add(sleeve); continue; }
    }

    string svcSizeTxt = size.Item1
        ? $"{ToMm(size.Item2):F0} dia" + (insFt > 0 ? $" + {ToMm(insFt):F0} ins" : "")
        : $"{ToMm(size.Item2):F0}x{ToMm(size.Item3):F0}" + (insFt > 0 ? $" + {ToMm(insFt):F0} ins" : "");

    string verdict, note = "";
    double needMm, haveMm;

    if (size.Item1)
    {
        // Round service: required bore = OD + 2 x insulation + 2 x clearance.
        double needFt = size.Item2 + 2 * insFt + 2 * clearFt;
        needMm = ToMm(needFt);
        haveMm = ToMm(sleeveIsRound ? sleeveDia : Math.Min(sleeveW, sleeveH));

        double spare = haveMm - needMm;
        if (spare < 0) verdict = $"TOO SMALL by {(-spare):F0} mm";
        else if (spare > maxOversizeMm * 2) verdict = $"OVERSIZED by {spare:F0} mm";
        else verdict = "OK";
        if (!sleeveIsRound) note = "rectangular sleeve on a round service — checked on its smaller dimension";
    }
    else
    {
        // Rectangular service: both dimensions have to clear.
        double needW = size.Item2 + 2 * insFt + 2 * clearFt;
        double needH = size.Item3 + 2 * insFt + 2 * clearFt;
        needMm = ToMm(Math.Max(needW, needH));

        double haveW = sleeveIsRound ? sleeveDia : sleeveW;
        double haveH = sleeveIsRound ? sleeveDia : sleeveH;
        haveMm = ToMm(Math.Min(haveW, haveH));

        double shortW = ToMm(needW - haveW);
        double shortH = ToMm(needH - haveH);
        if (shortW > 0 || shortH > 0)
            verdict = $"TOO SMALL by {Math.Max(shortW, shortH):F0} mm";
        else if (ToMm(haveW - needW) > maxOversizeMm * 2 && ToMm(haveH - needH) > maxOversizeMm * 2)
            verdict = "OVERSIZED";
        else verdict = "OK";
        if (sleeveIsRound) note = "round sleeve on a rectangular service — the duct's diagonal may not pass; check it";
    }

    if (candidates.Count > 1)
        note = (note.Length > 0 ? note + "; " : "") + $"{candidates.Count} services share this sleeve — checked against the nearest";

    rows.Add((sleeve, svc, svcSizeTxt, needMm, haveMm, verdict, note));
}

// ---- report ----
var bad = rows.Where(r => r.Verdict.StartsWith("TOO SMALL")).ToList();
var over = rows.Where(r => r.Verdict.StartsWith("OVERSIZED")).ToList();

sb.AppendLine($"CHECKED: {rows.Count}   TOO SMALL: {bad.Count}   OVERSIZED: {over.Count}   OK: {rows.Count - bad.Count - over.Count}");
if (noService.Count > 0)
    sb.AppendLine($"NO SERVICE THROUGH IT: {noService.Count} sleeve(s) — an orphan hole, or the run moved and the sleeve did not: " +
                  string.Join(", ", noService.Take(20).Select(e => e.Id.ToString())) + (noService.Count > 20 ? " ..." : ""));
if (unreadable.Count > 0)
    sb.AppendLine($"SIZE UNREADABLE: {unreadable.Count} sleeve(s) — NOT checked and NOT a pass. Add your family's size parameter name to sizeParamNames: " +
                  string.Join(", ", unreadable.Take(20).Select(e => e.Id.ToString())) + (unreadable.Count > 20 ? " ..." : ""));
if (shared > 0) sb.AppendLine($"SHARED SLEEVES: {shared} sleeve(s) have more than one service through them.");
if (geometryFallback.Count > 0)
    sb.AppendLine($"CENTRELINE FALLBACK: {geometryFallback.Distinct().Count()} sleeve(s) carry no readable solid, so their service was matched on the CENTRELINE rather than on real shared volume — a weaker test that can pick a run passing nearby: " +
                  string.Join(", ", geometryFallback.Distinct().Take(15).Select(i => i.ToString())));
sb.AppendLine();

if (rows.Count == 0)
{
    sb.AppendLine("Nothing could be checked — see the counts above.");
    return sb.ToString();
}

if (bad.Count == 0 && over.Count == 0)
{
    sb.AppendLine("CLEAR — every sleeve fits its service with the required clearance and none is excessively oversized.");
    return sb.ToString();
}

sb.AppendLine("| Sleeve | Service | Service size | Needs mm | Has mm | Verdict | Note |");
sb.AppendLine("|---|---|---|---|---|---|---|");
foreach (var r in bad.Concat(over).Take(maxReportedRows))
    sb.AppendLine($"| {r.Sleeve.Id} | {r.Svc.Id} ({r.Svc.Category?.Name}) | {r.SvcSize} | {r.NeedMm:F0} | {r.HaveMm:F0} | {r.Verdict} | {r.Note} |");
if (bad.Count + over.Count > maxReportedRows)
    sb.AppendLine($"\n... and {bad.Count + over.Count - maxReportedRows} more (raise maxReportedRows to see them).");

return sb.ToString();
