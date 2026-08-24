// ============================================================
// FRAGMENT (action) — action-check-ceiling-coordination.cs
// PURPOSE: Check ceiling-mounted MEP devices against the ceiling they are supposed to be in — is there a
//          ceiling above each one at all, is the device sitting AT the ceiling or floating above or
//          hanging below it, and is there enough void between the ceiling and whatever is over it. The
//          sweep before a reflected ceiling plan is issued.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          DEVICES, e.g. filter-by-multiple-categories.cs over air terminals, sprinklers, lighting
//          fixtures and detectors. Read-only.
//
// ✱✱ THE FAILURE THIS CATCHES IS INVISIBLE IN PLAN. A diffuser 40 mm above the ceiling and a diffuser
//    exactly in it look identical from above, and both look fine in the model browser. It shows up as a
//    site query, or on a section nobody cut. Every device's own Z is compared against the ceiling
//    actually above it, and the difference is reported per device in millimetres.
//
// ✱✱ THE CEILING IS FOUND BY CASTING A RAY STRAIGHT UP, which is the technique that works — a device is
//    not "hosted by" the ceiling in any readable way when it was placed unhosted, and a room's ceiling
//    cannot be inferred from the room. `ReferenceIntersector` needs a real View3D to run in; the active
//    view is used when it is already 3D, otherwise any non-template 3D view is borrowed. If the project
//    has no 3D view at all this reports that plainly rather than returning zero findings.
//
// ✱✱ "NO CEILING ABOVE" IS A FINDING, NOT AN ERROR. A device in an open-soffit area is fine; a device
//    that should be in a ceiling and has none above it is either in the wrong place or the ceiling is
//    missing from the model. Those are counted separately and listed, because the two cases look the
//    same in every other check.
//
// GOTCHA: THE RAY GOES UP FROM THE DEVICE'S INSERTION POINT. For a family whose insertion point is not
//         on its own centre — some linear diffusers, some light fittings — the ray can miss the ceiling
//         beside it. Devices reporting NO CEILING in an area you know has one are usually this, not a
//         missing ceiling; check one in a section before believing a large count.
// GOTCHA: CEILINGS IN LINKS ARE NOT HIT by default. Architecture is normally linked, so this will report
//         everything as having no ceiling above unless the ceilings are in the host model. That is the
//         first thing to check if the result looks absurd — the report says which document the ceilings
//         came from.
// GOTCHA: THE VOID CHECK IS BOUNDING-BOX BASED and only looks at what is directly over the device, so it
//         is an indication rather than a clearance calculation. action-check-minimum-clearance.cs is the
//         precise version.
// RELATED: recipes/ray-trace-to-ceiling.cs (the FIX — snap devices onto the ceiling above them),
//          action-snap-to-ceiling-grid.cs (move them onto tile centres),
//          action-report-ceiling-heights.cs (clear height per room),
//          action-check-minimum-clearance.cs (what else is in the void).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one device's reported offset against a
//   section in Revit before acting on a whole floor.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double flushToleranceMm = 15;       // how far off the ceiling plane still counts as "in the ceiling"
double maxRayDistanceMm = 5000;     // give up if the nearest ceiling above is further than this
bool checkVoidAbove = true;         // also report how much space there is between ceiling and the next thing up
double minVoidMm = 200;             // flag a void tighter than this
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the ceiling-mounted devices).");
    return sb.ToString();
}

// ---- a 3D view to cast in ----
View3D rayView = Document.ActiveView as View3D;
if (rayView == null)
{
    rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
        .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);
}
if (rayView == null)
{
    sb.AppendLine("STOP: no usable 3D view in this project — ReferenceIntersector cannot run without one. Create any 3D view and re-run.");
    return sb.ToString();
}

int hostCeilings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Ceilings).WhereElementIsNotElementType().GetElementCount();

sb.AppendLine($"CEILING COORDINATION — {elements.Count} device(s), ray-casting in 3D view '{rayView.Name}'");
sb.AppendLine($"Ceilings in the HOST model: {hostCeilings}" + (hostCeilings == 0 ? "  <- NONE. If the architecture is LINKED, every device below will report NO CEILING and this check has told you nothing." : ""));
sb.AppendLine($"Flush tolerance {flushToleranceMm:F0} mm");
sb.AppendLine();

var ceilingFilter = new ElementCategoryFilter(BuiltInCategory.OST_Ceilings);
var intersector = new ReferenceIntersector(ceilingFilter, FindReferenceTarget.Face, rayView);
double maxDistFt = ToFeet(maxRayDistanceMm);

// Everything that could be sitting on top of the ceiling, for the void check.
ReferenceIntersector aboveIntersector = null;
if (checkVoidAbove)
{
    var aboveFilter = new LogicalOrFilter(new List<ElementFilter>
    {
        new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
        new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
        new ElementCategoryFilter(BuiltInCategory.OST_CableTray),
        new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
        new ElementCategoryFilter(BuiltInCategory.OST_Floors),
    });
    aboveIntersector = new ReferenceIntersector(aboveFilter, FindReferenceTarget.Face, rayView);
}

// ---- check each device ----
var rows = new List<(Element El, double OffsetMm, string Verdict, double VoidMm)>();
var noCeiling = new List<Element>();
var notPointBased = new List<Element>();

foreach (var el in elements)
{
    var lp = el.Location as LocationPoint;
    if (lp == null) { notPointBased.Add(el); continue; }

    var origin = lp.Point;
    ReferenceWithContext hit = null;
    try { hit = intersector.FindNearest(origin, XYZ.BasisZ); }
    catch { }

    if (hit == null || hit.Proximity > maxDistFt)
    {
        // Nothing above — but it may be sitting just BELOW a ceiling it is meant to be flush with, in
        // which case the upward ray from a point already at ceiling level can miss. Try downward too
        // before declaring there is no ceiling.
        ReferenceWithContext down = null;
        try { down = intersector.FindNearest(origin, -XYZ.BasisZ); }
        catch { }
        if (down == null || down.Proximity > ToFeet(flushToleranceMm * 4))
        {
            noCeiling.Add(el);
            continue;
        }
        hit = down;
    }

    double ceilingZ;
    try { ceilingZ = hit.GetReference().GlobalPoint.Z; }
    catch { noCeiling.Add(el); continue; }

    double offsetMm = ToMm(origin.Z - ceilingZ);   // positive = device sits ABOVE the ceiling plane

    string verdict;
    if (Math.Abs(offsetMm) <= flushToleranceMm) verdict = "OK — in the ceiling";
    else if (offsetMm > 0) verdict = $"ABOVE the ceiling by {offsetMm:F0} mm";
    else verdict = $"BELOW the ceiling by {(-offsetMm):F0} mm";

    // How much room there is between the ceiling and the next thing up.
    double voidMm = -1;
    if (checkVoidAbove && aboveIntersector != null)
    {
        try
        {
            var start = new XYZ(origin.X, origin.Y, ceilingZ + ToFeet(5));
            var up = aboveIntersector.FindNearest(start, XYZ.BasisZ);
            if (up != null && up.Proximity <= maxDistFt)
                voidMm = ToMm(up.Proximity) + 5;
        }
        catch { }
    }

    rows.Add((el, offsetMm, verdict, voidMm));
}

// ---- report ----
var misplaced = rows.Where(r => !r.Verdict.StartsWith("OK")).ToList();
var tightVoid = rows.Where(r => r.VoidMm >= 0 && r.VoidMm < minVoidMm).ToList();

sb.AppendLine($"CHECKED: {rows.Count}   IN THE CEILING: {rows.Count - misplaced.Count}   NOT FLUSH: {misplaced.Count}");
if (noCeiling.Count > 0)
    sb.AppendLine($"NO CEILING FOUND ABOVE: {noCeiling.Count} device(s) — open soffit, wrong place, or the ceiling is in a LINK: " +
                  string.Join(", ", noCeiling.Take(20).Select(e => e.Id.ToString())) + (noCeiling.Count > 20 ? " ..." : ""));
if (notPointBased.Count > 0)
    sb.AppendLine($"NOT POINT-BASED: {notPointBased.Count} element(s) skipped — a linear device has no single insertion point to cast from.");
if (checkVoidAbove) sb.AppendLine($"VOID TIGHTER THAN {minVoidMm:F0} mm: {tightVoid.Count}");
sb.AppendLine();

if (misplaced.Count == 0 && tightVoid.Count == 0)
{
    sb.AppendLine("CLEAR — every device that found a ceiling sits in it, and the void above is adequate.");
    return sb.ToString();
}

if (misplaced.Count > 0)
{
    sb.AppendLine("NOT FLUSH WITH THE CEILING:");
    sb.AppendLine("| Device | Category | Offset mm | Verdict | Void above mm |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var r in misplaced.OrderByDescending(r => Math.Abs(r.OffsetMm)).Take(maxReportedRows))
        sb.AppendLine($"| {r.El.Id} | {r.El.Category?.Name ?? "-"} | {r.OffsetMm:F0} | {r.Verdict} | {(r.VoidMm < 0 ? "-" : r.VoidMm.ToString("F0"))} |");
    if (misplaced.Count > maxReportedRows)
        sb.AppendLine($"\n... and {misplaced.Count - maxReportedRows} more");

    int above = misplaced.Count(r => r.OffsetMm > 0);
    sb.AppendLine();
    sb.AppendLine($"Above the ceiling (hidden from the room): {above}   Below it (hanging into the room): {misplaced.Count - above}");
    sb.AppendLine("recipes/ray-trace-to-ceiling.cs snaps these onto the ceiling above them.");
}

if (tightVoid.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"TIGHT VOID ABOVE THE CEILING (under {minVoidMm:F0} mm) — no room for the device body or its connection:");
    sb.AppendLine("| Device | Category | Void mm |");
    sb.AppendLine("|---|---|---|");
    foreach (var r in tightVoid.OrderBy(r => r.VoidMm).Take(30))
        sb.AppendLine($"| {r.El.Id} | {r.El.Category?.Name ?? "-"} | {r.VoidMm:F0} |");
}

return sb.ToString();
