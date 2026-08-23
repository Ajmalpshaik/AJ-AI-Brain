// ============================================================
// FRAGMENT (action) — action-set-view-crop-to-shape.cs
// PURPOSE: Give a view a NON-RECTANGULAR crop — an L-shape round a wing, a stepped boundary along a
//          match line, the outline of a room or a zone. Takes the shape from lines you already drew, or
//          from a room's own boundary, and makes it the view's crop region.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — either the CurveElements
//          forming ONE closed loop, or a single Room/Space whose boundary should become the crop.
//
// ✱✱ WHY action-set-view-crop.cs DOES NOT COVER THIS. That fragment sets `CropBox`, which is a BOX — two
//    corners and a transform, so it can only ever be a rectangle (correctly rotated, since 2026-08-22,
//    but still a rectangle). A non-rectangular crop is a different mechanism entirely:
//        view.GetCropRegionShapeManager().SetCropShape(curveLoop)
//    and it is the only way to get the stepped or L-shaped crop that a real drawing set needs.
//
// ✱✱ THE THREE THINGS REVIT SILENTLY REQUIRES OF THE LOOP, each of which returns the same unhelpful
//    "invalid curve loop" if you miss it:
//      1. IT MUST BE CLOSED, end to end, with no gap. Lines drawn by eye almost never close — they miss
//         by a fraction of a millimetre. This SORTS the curves into a chain and reports the worst gap it
//         had to bridge, so you can see whether the shape was really closed or was quietly repaired.
//      2. IT MUST BE FLAT AND IN THE VIEW'S OWN PLANE. Curves at different Z, or a loop taken from a
//         plan and pushed at a section, are rejected. Everything is projected onto the view plane first.
//      3. IT MUST NOT SELF-INTERSECT. A bow-tie is rejected. Not detected here — Revit's own error is
//         the honest answer, and it is reported verbatim rather than being swallowed.
//
// GOTCHA: THE CROP MUST BE ON AND VISIBLE, or nothing appears to happen. Both are set for you
//         (`VIEWER_CROP_REGION`, `VIEWER_CROP_REGION_VISIBLE`) and said in the report.
// GOTCHA: A VIEW DRIVEN BY A TEMPLATE may have its crop locked by that template — reported by name, so
//         the fix (change the template, or detach the view) is obvious rather than mysterious.
// GOTCHA: this changes ONE view — the active one, or `targetViewName`. It is not a batch operation,
//         because a shape that fits one view rarely fits another.
// RELATED: action-set-view-crop.cs for the ordinary rectangular case (and it handles rotated views);
//          filter-by-category.cs on OST_Lines to gather the boundary you drew.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Draw a simple L in detail lines, run it, and
//   look at the view before trusting it on a real sheet.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetViewName = null;   // null = the active view
bool useRoomBoundary = false;   // true = take the shape from a Room/Space in `elements` instead of lines
double joinToleranceMm = 5.0;   // how big a gap between line ends may be bridged before it is an error
bool dryRun = true;             // true = validate the loop and report, change nothing
// ---- END INPUTS ----

const double MM = 304.8;
double tol = joinToleranceMm / MM;

var view = string.IsNullOrWhiteSpace(targetViewName)
    ? Document.ActiveView
    : new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .FirstOrDefault(v => !v.IsTemplate && v.Name == targetViewName);

if (view == null)
{
    sb.AppendLine(targetViewName == null
        ? "No active view."
        : $"No view named '{targetViewName}'.");
    return sb.ToString();
}
if (view.IsTemplate)
{
    sb.AppendLine("That is a view TEMPLATE — a crop shape belongs to a real view.");
    return sb.ToString();
}

// ---- gather the curves ----
var curves = new List<Curve>();
string source;

if (useRoomBoundary)
{
    var spatial = elements.OfType<SpatialElement>().FirstOrDefault();
    if (spatial == null)
    {
        sb.AppendLine("useRoomBoundary is on but there is no Room or Space in `elements`.");
        return sb.ToString();
    }
    var opt = new SpatialElementBoundaryOptions();
    var loops = spatial.GetBoundarySegments(opt);
    if (loops == null || loops.Count == 0)
    {
        sb.AppendLine($"'{spatial.Name}' returned no boundary — it is probably not enclosed.");
        return sb.ToString();
    }
    foreach (var seg in loops[0]) curves.Add(seg.GetCurve());   // outer loop only; a crop cannot have holes
    source = $"room '{spatial.Name}' (outer boundary)";
    if (loops.Count > 1)
        sb.AppendLine($"NOTE: '{spatial.Name}' has {loops.Count - 1} internal hole(s). A crop region cannot have holes — using the outer boundary only.");
}
else
{
    foreach (var ce in elements.OfType<CurveElement>())
        if (ce.GeometryCurve != null) curves.Add(ce.GeometryCurve);
    source = $"{curves.Count} line(s) from `elements`";
}

if (curves.Count < 3)
{
    sb.AppendLine($"Only {curves.Count} curve(s) — a crop shape needs at least 3.");
    return sb.ToString();
}

// ---- project onto the view plane (requirement 2) ----
var origin = view.Origin;
var vdir = view.ViewDirection;
Func<XYZ, XYZ> flatten = p => p - vdir.Multiply((p - origin).DotProduct(vdir));

// ---- chain them into a closed loop (requirement 1) ----
var remaining = new List<Curve>(curves);
var ordered = new List<Curve>();
var first = remaining[0]; remaining.RemoveAt(0);
ordered.Add(first);
var chainEnd = flatten(first.GetEndPoint(1));
var chainStart = flatten(first.GetEndPoint(0));
double worstGap = 0;
bool broke = false;

while (remaining.Count > 0)
{
    int bestIdx = -1; bool flip = false; double bestDist = double.MaxValue;
    for (int i = 0; i < remaining.Count; i++)
    {
        var a = flatten(remaining[i].GetEndPoint(0));
        var b = flatten(remaining[i].GetEndPoint(1));
        double da = chainEnd.DistanceTo(a), db = chainEnd.DistanceTo(b);
        if (da < bestDist) { bestDist = da; bestIdx = i; flip = false; }
        if (db < bestDist) { bestDist = db; bestIdx = i; flip = true; }
    }
    if (bestIdx < 0 || bestDist > tol) { broke = true; worstGap = Math.Max(worstGap, bestDist); break; }
    worstGap = Math.Max(worstGap, bestDist);

    var c = remaining[bestIdx]; remaining.RemoveAt(bestIdx);
    var s = flatten(c.GetEndPoint(flip ? 1 : 0));
    var e = flatten(c.GetEndPoint(flip ? 0 : 1));
    ordered.Add(Line.CreateBound(chainEnd, e));   // snap to the running end so the chain is exact
    chainEnd = e;
}

double closingGap = chainEnd.DistanceTo(chainStart);

sb.AppendLine($"View: '{view.Name}'   Shape from: {source}");
sb.AppendLine($"Chained {ordered.Count} segment(s). Worst joint gap bridged: {worstGap * MM:F2} mm. "
            + $"Closing gap: {closingGap * MM:F2} mm.");

if (broke)
{
    sb.AppendLine($"FAILED — the lines do not form ONE continuous loop. A gap wider than {joinToleranceMm} mm was hit "
                + $"with {remaining.Count} line(s) still unused.");
    sb.AppendLine("Either the shape is open, or there are stray lines in the selection. "
                + "action-find-overlapping-lines.cs will show duplicates if that is the cause.");
    return sb.ToString();
}
if (closingGap > tol)
{
    sb.AppendLine($"FAILED — the loop does not close: {closingGap * MM:F2} mm between the last end and the first start.");
    sb.AppendLine($"Raise joinToleranceMm above that, or fix the drawing.");
    return sb.ToString();
}

// close it exactly
ordered.Add(Line.CreateBound(chainEnd, chainStart));

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine($"DRY RUN — the loop is valid and closed ({ordered.Count} segments). Nothing changed.");

    // ASK REVIT, don't guess. `IsCropRegionShapeValid` is Revit's own verdict on the loop, and it is
    // available on every version here (checked against the shipped RevitAPI.dll on 2020 and 2024). A
    // loop can chain and close perfectly and still be refused — self-intersecting, not planar, or not
    // in the view's plane — and this is the only way to know that BEFORE the write. Added 2026-08-23;
    // before that the dry run said "valid" on the strength of its own geometry checks alone, which is
    // a weaker claim than it sounded.
    try
    {
        var mgrCheck = view.GetCropRegionShapeManager();
        var testLoop = CurveLoop.Create(ordered);
        if (mgrCheck.IsCropRegionShapeValid(testLoop))
            sb.AppendLine("Revit's own check (IsCropRegionShapeValid) ACCEPTS this loop.");
        else
        {
            sb.AppendLine("! Revit's own check (IsCropRegionShapeValid) REJECTS this loop — applying it would fail.");
            sb.AppendLine("  Usually: the loop crosses itself (a bow-tie), is not flat, or does not lie in this view's plane.");
        }
    }
    catch (Exception exChk)
    {
        sb.AppendLine($"(Could not run Revit's own shape check: {exChk.Message})");
    }

    sb.AppendLine("Set dryRun = false to apply it as the crop region.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Set view crop to shape"))
    {
        t.Start();
        try
        {
            var pOn = view.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION);
            if (pOn != null && !pOn.IsReadOnly && pOn.AsInteger() == 0) pOn.Set(1);
            var pVis = view.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION_VISIBLE);
            if (pVis != null && !pVis.IsReadOnly) pVis.Set(1);

            var loop = CurveLoop.Create(ordered);
            view.GetCropRegionShapeManager().SetCropShape(loop);
            t.Commit();

            sb.AppendLine();
            sb.AppendLine($"Crop region set on '{view.Name}' from {ordered.Count} segments. Crop turned ON and made visible.");
            sb.AppendLine("Read it back by eye — a crop that Revit accepted can still be the wrong shape.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            sb.AppendLine();
            sb.AppendLine($"Revit refused the shape: {msg}");
            sb.AppendLine("Most often that is a self-intersecting loop (a bow-tie), or a view whose template locks the crop"
                        + (view.ViewTemplateId != ElementId.InvalidElementId
                            ? $" — this view IS governed by template '{Document.GetElement(view.ViewTemplateId)?.Name}'." : "."));
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
