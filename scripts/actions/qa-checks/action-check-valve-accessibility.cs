// ============================================================
// FRAGMENT (action) — action-check-valve-accessibility.cs
// PURPOSE: Check that valves, dampers and other in-line accessories can actually be reached and operated
//          — enough room round the handle, not buried above a hard ceiling with no access panel, not
//          three metres up where nobody can turn it. The commissioning question, asked while it is still
//          cheap to move things.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          accessories, e.g. filter-by-multiple-categories.cs over OST_PipeAccessory and
//          OST_DuctAccessory, or filter-by-family.cs on a valve family name. Read-only.
//
// ✱✱ IT ASKS THREE SEPARATE QUESTIONS AND ANSWERS EACH ONE SEPARATELY, because they have different fixes.
//      SPACE   — is anything modelled inside the operating envelope round it? Move the obstruction.
//      CEILING — is it above a ceiling? Then it needs an access panel, which is a drawing item, not a
//                modelling one, and it is the thing most often forgotten.
//      HEIGHT  — is it within reach from the floor? Above that it needs a platform, a chain wheel, or
//                relocating.
//    A valve can pass one and fail another, so the report gives all three per valve rather than one
//    pass/fail that hides which problem it is.
//
// ✱✱ ABOVE A CEILING IS NOT AUTOMATICALLY A FAULT, and this does not pretend it is. Most valves live in
//    the ceiling void; the finding is that each one NEEDS AN ACCESS PANEL. The count is what goes to the
//    architect, so it is reported as a list to coordinate rather than as a failure — a check that cries
//    wolf about every valve in the building gets switched off by lunchtime.
//
// ✱✱ THE CEILING IS FOUND BY RAY-CASTING UP, the same proven technique as recipes/ray-trace-to-ceiling.cs.
//    `ReferenceIntersector` needs a real View3D; the active view is used when it is already 3D, otherwise
//    any non-template 3D view is borrowed. No 3D view at all is reported plainly rather than as zero
//    findings.
//
// GOTCHA: THE OPERATING ENVELOPE IS A PLAIN BOX ROUND THE VALVE, not a real swept volume of a hand and a
//         handle. It is an indication. `envelopeMm` should be the size of the space a fitter actually
//         needs — for a lever valve that is roughly the lever length plus a fist.
// GOTCHA: THE VALVE'S OWN PIPE IS NOT AN OBSTRUCTION. Anything connected to the accessory is excluded, or
//         every valve reports the pipe it sits in as blocking it.
// GOTCHA: CEILINGS AND STRUCTURE IN LINKS ARE NOT SEEN. If the architecture is linked, the ceiling check
//         reports nothing and the space check misses the walls. The host-model counts are printed first
//         so that is visible rather than read as a clean result.
// GOTCHA: HEIGHT IS MEASURED ABOVE THE VALVE'S OWN LEVEL, not above the finished floor. A raised floor or
//         a thick build-up makes the real reach shorter than the number here.
// RELATED: action-check-equipment-clearance.cs (the same question, directional, for big plant),
//          action-check-ceiling-coordination.cs (devices against the ceiling generally),
//          action-check-minimum-clearance.cs (a plain gap in any direction),
//          filter-by-solid-intersection.cs (a hand-built access zone as an actionable set).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one valve's three verdicts against a
//   section before trusting a whole floor.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double envelopeMm = 300;          // clear space required all round the accessory to operate it
double maxReachMm = 2000;         // highest a valve can be above its level and still be operated from the floor
bool checkCeiling = true;         // flag valves sitting above a ceiling as needing an access panel
bool checkSpace = true;
bool checkHeight = true;

// What counts as blocking the operating envelope.
var obstructionCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_CableTray,
    BuiltInCategory.OST_Conduit,
    BuiltInCategory.OST_Walls,
    BuiltInCategory.OST_StructuralFraming,
    BuiltInCategory.OST_StructuralColumns,
    BuiltInCategory.OST_MechanicalEquipment,
};
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the valves, dampers and accessories).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- host-model context, so a link problem is visible up front ----
var candidates = new List<Element>();
foreach (var cat in obstructionCategories)
{
    try { candidates.AddRange(new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType().ToList()); }
    catch { }
}
int hostCeilings = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Ceilings).WhereElementIsNotElementType().GetElementCount();

View3D rayView = Document.ActiveView as View3D;
if (rayView == null)
    rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
        .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);

sb.AppendLine($"VALVE / DAMPER ACCESSIBILITY — {elements.Count} accessor(y/ies)");
sb.AppendLine($"Envelope {envelopeMm:F0} mm all round   max reach {maxReachMm:F0} mm above level");
sb.AppendLine($"Host-model context: {candidates.Count} possible obstruction(s), {hostCeilings} ceiling(s)" +
              (hostCeilings == 0 && checkCeiling ? "  <- NO ceilings in the host model. If they are LINKED, the ceiling check below finds nothing and has told you nothing." : ""));
if (checkCeiling && rayView == null)
    sb.AppendLine("NOTE: no usable 3D view — the ceiling check cannot run (ReferenceIntersector needs one). Create any 3D view and re-run.");
sb.AppendLine();

ReferenceIntersector ceilingIntersector = null;
if (checkCeiling && rayView != null)
    ceilingIntersector = new ReferenceIntersector(
        new ElementCategoryFilter(BuiltInCategory.OST_Ceilings), FindReferenceTarget.Face, rayView);

// ---- levels, for the height check ----
var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
    .OrderBy(l => l.Elevation).ToList();
Func<double, double> levelBelowZ = z =>
{
    double best = double.MinValue;
    foreach (var l in levels) if (l.Elevation <= z + 1e-6 && l.Elevation > best) best = l.Elevation;
    return best == double.MinValue ? (levels.Count > 0 ? levels[0].Elevation : 0) : best;
};

// ---- check each ----
var rows = new List<(Element El, string Name, string SpaceVerdict, string CeilingVerdict, double HeightMm, string HeightVerdict, int Blockers)>();
var noLocation = new List<Element>();
double envFt = ToFeet(envelopeMm);

foreach (var v in elements)
{
    XYZ pt = null;
    var lp = v.Location as LocationPoint;
    if (lp != null) pt = lp.Point;
    else
    {
        BoundingBoxXYZ bb = null;
        try { bb = v.get_BoundingBox(null); } catch { }
        if (bb != null) pt = (bb.Min + bb.Max) * 0.5;
    }
    if (pt == null) { noLocation.Add(v); continue; }

    string name = v.Name ?? "";
    var fi = v as FamilyInstance;
    if (fi != null && fi.Symbol != null && fi.Symbol.Family != null) name = $"{fi.Symbol.Family.Name} : {fi.Symbol.Name}";

    // ---- 1. SPACE ----
    string spaceVerdict = "not checked";
    int blockers = 0;
    if (checkSpace)
    {
        // Things connected to this accessory are its own pipework, not obstructions.
        var ignore = new HashSet<long> { IdValue(v.Id) };
        if (fi != null && fi.MEPModel != null && fi.MEPModel.ConnectorManager != null)
        {
            try
            {
                foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
                    foreach (Connector r in c.AllRefs)
                        if (r.Owner != null) ignore.Add(IdValue(r.Owner.Id));
            }
            catch { }
        }

        var min = new XYZ(pt.X - envFt, pt.Y - envFt, pt.Z - envFt);
        var max = new XYZ(pt.X + envFt, pt.Y + envFt, pt.Z + envFt);
        try
        {
            var outline = new Outline(min, max);
            var bbFilter = new BoundingBoxIntersectsFilter(outline);
            var hits = new FilteredElementCollector(Document, candidates.Select(c => c.Id).ToList())
                .WherePasses(bbFilter).ToList();
            foreach (var h in hits) if (!ignore.Contains(IdValue(h.Id))) blockers++;
        }
        catch { }

        spaceVerdict = blockers == 0 ? "clear" : $"BLOCKED by {blockers}";
    }

    // ---- 2. CEILING ----
    string ceilingVerdict = "not checked";
    if (checkCeiling && ceilingIntersector != null)
    {
        ReferenceWithContext up = null;
        try { up = ceilingIntersector.FindNearest(pt, XYZ.BasisZ); }
        catch { }
        if (up == null) ceilingVerdict = "open above — reachable";
        else
        {
            double dMm = ToMm(up.Proximity);
            ceilingVerdict = $"ABOVE A CEILING ({dMm:F0} mm below it) — needs an access panel";
        }
    }

    // ---- 3. HEIGHT ----
    double heightMm = 0;
    string heightVerdict = "not checked";
    if (checkHeight)
    {
        heightMm = ToMm(pt.Z - levelBelowZ(pt.Z));
        heightVerdict = heightMm <= maxReachMm ? "within reach" : $"TOO HIGH — {heightMm:F0} mm above level";
    }

    rows.Add((v, name, spaceVerdict, ceilingVerdict, heightMm, heightVerdict, blockers));
}

// ---- report ----
var blocked = rows.Where(r => r.SpaceVerdict.StartsWith("BLOCKED")).ToList();
var aboveCeiling = rows.Where(r => r.CeilingVerdict.StartsWith("ABOVE")).ToList();
var tooHigh = rows.Where(r => r.HeightVerdict.StartsWith("TOO HIGH")).ToList();

sb.AppendLine($"CHECKED: {rows.Count}");
if (checkSpace) sb.AppendLine($"  ENVELOPE BLOCKED:        {blocked.Count}   (move the obstruction)");
if (checkCeiling) sb.AppendLine($"  ABOVE A CEILING:         {aboveCeiling.Count}   (each needs an access panel — a drawing item, not a modelling one)");
if (checkHeight) sb.AppendLine($"  OUT OF REACH:            {tooHigh.Count}   (platform, chain wheel, or relocate)");
if (noLocation.Count > 0)
    sb.AppendLine($"  NO LOCATION: {noLocation.Count} element(s) — NOT checked and NOT a pass.");
sb.AppendLine();

if (blocked.Count == 0 && tooHigh.Count == 0 && aboveCeiling.Count == 0)
{
    sb.AppendLine("CLEAR — every accessory has its operating space, is reachable, and none is buried above a ceiling.");
    return sb.ToString();
}

if (blocked.Count > 0 || tooHigh.Count > 0)
{
    sb.AppendLine("PROBLEMS THAT NEED A MODEL CHANGE:");
    sb.AppendLine("| Element | Type | Space | Height mm | Height verdict |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var r in blocked.Concat(tooHigh.Where(t => !blocked.Contains(t))).Take(maxReportedRows))
        sb.AppendLine($"| {r.El.Id} | {r.Name} | {r.SpaceVerdict} | {r.HeightMm:F0} | {r.HeightVerdict} |");
    if (blocked.Count + tooHigh.Count > maxReportedRows)
        sb.AppendLine($"\n... and more (raise maxReportedRows to see them).");
    sb.AppendLine();
}

if (aboveCeiling.Count > 0)
{
    sb.AppendLine($"ACCESS PANELS REQUIRED ({aboveCeiling.Count}) — this is the list to send to the architect, not a defect:");
    sb.AppendLine("| Element | Type | Below the ceiling by |");
    sb.AppendLine("|---|---|---|");
    foreach (var r in aboveCeiling.Take(maxReportedRows))
        sb.AppendLine($"| {r.El.Id} | {r.Name} | {r.CeilingVerdict.Replace("ABOVE A CEILING (", "").Replace(") — needs an access panel", "")} |");
    if (aboveCeiling.Count > maxReportedRows)
        sb.AppendLine($"\n... and {aboveCeiling.Count - maxReportedRows} more");
}

return sb.ToString();
