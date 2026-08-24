// ============================================================
// FRAGMENT (action) — action-check-insulation-clearance.cs
// PURPOSE: Clearance measured to the OUTSIDE OF THE INSULATION, not to the bare duct or pipe. The same
//          sweep as action-check-minimum-clearance.cs with the one correction that changes the answer on
//          a real MEP job: a 50 mm jacket on both services eats 100 mm of the gap, so a pair that reads
//          120 mm clear on the bare geometry has 20 mm in reality and cannot be built.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          services, e.g. filter-by-multiple-categories.cs over duct and pipe categories. Read-only.
//
// ✱✱ WHY THE BARE-GEOMETRY CHECK IS OPTIMISTIC AND THIS ONE IS NOT. Revit models insulation as SEPARATE
//    elements wrapped around the host, so a duct's own bounding box and its own solid stop at the sheet
//    metal. Every distance measured off the host is therefore too generous by the jacket thickness at
//    each end. This reads each element's real insulation thickness and takes it off the measured gap.
//
// ✱✱ INSULATION IS FOUND THROUGH THE HOST, AND THE LOOKUP THROWS BY DESIGN.
//    `InsulationLiningBase.GetInsulationIds(doc, id)` raises ArgumentException for any element that
//    cannot carry a wrap — a wall, a column, a fitting of the wrong kind. THE CATCH IS THE CATEGORY
//    FILTER: pre-filtering by category instead would miss the cases the API accepts and nobody predicted.
//    Proven behaviour, recorded in action-highlight-vs-rest.cs and reused here rather than rediscovered.
//
// ✱✱ LINING IS DELIBERATELY NOT COUNTED. Duct lining sits INSIDE the duct — it takes air away, not space
//    outside. Adding it to an external clearance figure would inflate every gap and produce false
//    failures. Only insulation counts here, and the report says so.
//
// ✱✱ AN ELEMENT WITH NO INSULATION MODELLED IS THE SILENT PROBLEM, so it is counted and reported. If the
//    specification says the chilled water is insulated and the model carries no insulation elements, this
//    check reads exactly like the bare one and passes things that will not fit. The "insulated / not
//    insulated" split is printed BEFORE any result, so that assumption is visible rather than buried.
//
// GOTCHA: DISTANCE IS BOUNDING-BOX BASED here, deliberately. Once both jackets are subtracted the answer
//         is already an engineering figure rather than a precise one, and the box keeps a whole-floor
//         sweep affordable. For a precise gap on one specific pair, use action-check-minimum-clearance.cs,
//         which samples real faces, and take the insulation off by hand.
// GOTCHA: INSULATION THICKNESS IS READ PER ELEMENT, so a run with mixed thicknesses is handled correctly
//         and a run where somebody forgot to insulate three segments shows up as three uninsulated rows.
// GOTCHA: LINKED MODELS ARE NOT SCANNED — the same limit as the other clearance fragments.
// RELATED: action-check-minimum-clearance.cs (bare geometry, real face sampling, per-category rules),
//          action-check-vertical-clearance.cs (Z separation only),
//          filter-by-insulation-status.cs (which runs still need insulating),
//          action-add-remove-insulation.cs (put it on).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check the insulated/uninsulated split first;
//   if everything reads uninsulated, this check is telling you about the model, not about clearance.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double requiredClearanceMm = 100;    // clear space required between the OUTSIDE faces of the insulation
bool ignoreConnected = true;         // skip pairs that share a connector
bool ignoreSameCategory = false;     // true = only compare across categories
double assumeThicknessMm = 0;        // >0 = assume this much on any element with NO insulation modelled
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the services).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- insulation thickness per element ----
// Thickness lives on the derived wrap classes (DuctInsulation, PipeInsulation), so it is read by name
// off the base reference; where that is unavailable the built-in parameter is the fallback. Neither is
// assumed to exist.
Func<Element, double> thicknessOfWrap = wrap =>
{
    if (wrap == null) return 0;
    try
    {
        var p = wrap.GetType().GetProperty("Thickness");
        if (p != null)
        {
            var v = p.GetValue(wrap, null);
            if (v is double) return (double)v;
        }
    }
    catch { }
    var bp = wrap.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS);
    if (bp != null && bp.HasValue) return bp.AsDouble();
    return 0;
};

// The THICKEST insulation on the element — a host can legitimately carry more than one wrap, and the
// clearance is governed by the fattest one, not by their sum.
Func<Element, double> insulationFeetOf = el =>
{
    ICollection<ElementId> ids = null;
    // Revit THROWS ArgumentException for an element that cannot host a wrap, so this catch IS the
    // category filter — do not pre-filter by category (proven in action-highlight-vs-rest.cs).
    try { ids = Autodesk.Revit.DB.InsulationLiningBase.GetInsulationIds(Document, el.Id); }
    catch { return 0; }
    if (ids == null || ids.Count == 0) return 0;

    double thickest = 0;
    foreach (var id in ids)
    {
        var wrap = Document.GetElement(id);
        double t = thicknessOfWrap(wrap);
        if (t > thickest) thickest = t;
    }
    return thickest;
};

// ---- gather ----
var items = new List<(Element El, BoundingBoxXYZ Box, double InsFt, bool Assumed)>();
int noBox = 0, insulated = 0, bare = 0;

foreach (var el in elements)
{
    BoundingBoxXYZ box = null;
    try { box = el.get_BoundingBox(null); } catch { }
    if (box == null) { noBox++; continue; }

    double ins = insulationFeetOf(el);
    bool assumed = false;
    if (ins <= 0 && assumeThicknessMm > 0) { ins = ToFeet(assumeThicknessMm); assumed = true; }

    if (ins > 0 && !assumed) insulated++; else bare++;
    items.Add((el, box, ins, assumed));
}

sb.AppendLine($"INSULATION-AWARE CLEARANCE — rule {requiredClearanceMm:F0} mm between the OUTSIDE faces of the insulation");
sb.AppendLine($"Elements: {items.Count}   insulated in the model: {insulated}   NO insulation modelled: {bare}" +
              (assumeThicknessMm > 0 ? $"   (uninsulated treated as {assumeThicknessMm:F0} mm by assumeThicknessMm)" : ""));
if (noBox > 0) sb.AppendLine($"NOTE: {noBox} element(s) had no bounding box and were skipped — NOT a pass.");
if (bare > 0 && assumeThicknessMm <= 0)
    sb.AppendLine($"WARNING: {bare} element(s) carry NO insulation in the model, so for those this check gives exactly the bare-geometry answer. If they are supposed to be insulated, set assumeThicknessMm or model the insulation — otherwise this passes pairs that will not fit.");
sb.AppendLine("Lining is deliberately NOT counted — it sits inside the duct and takes no outside space.");
sb.AppendLine();

if (items.Count < 2)
{
    sb.AppendLine("Fewer than two measurable elements — nothing to compare.");
    return sb.ToString();
}

// ---- connector relationships ----
Func<Element, HashSet<long>> connectedIdsOf = el =>
{
    var ids = new HashSet<long>();
    if (!ignoreConnected) return ids;
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
                if (r.Owner != null) ids.Add(IdValue(r.Owner.Id));
    }
    catch { }
    return ids;
};

// Gap between two boxes; 0 where they already overlap.
Func<BoundingBoxXYZ, BoundingBoxXYZ, double> boxGap = (a, b) =>
{
    double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
    double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
    double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
};

// ---- sweep ----
double requiredFt = ToFeet(requiredClearanceMm);
// Search envelope has to allow for the fattest possible pair of jackets, or a violation caused ENTIRELY
// by insulation would never be looked at.
double maxInsFt = items.Count > 0 ? items.Max(i => i.InsFt) : 0;
double envelopeFt = requiredFt + 2 * maxInsFt;

var findings = new List<(Element A, Element B, double BareMm, double InsAMm, double InsBMm, double RealMm)>();
int pairsTested = 0;

for (int i = 0; i < items.Count; i++)
{
    var a = items[i];
    var aConnected = connectedIdsOf(a.El);

    for (int j = i + 1; j < items.Count; j++)
    {
        var b = items[j];

        if (ignoreSameCategory && a.El.Category != null && b.El.Category != null &&
            IdValue(a.El.Category.Id) == IdValue(b.El.Category.Id)) continue;
        if (ignoreConnected && aConnected.Contains(IdValue(b.El.Id))) continue;

        double bare_ = boxGap(a.Box, b.Box);
        if (bare_ > envelopeFt) continue;

        pairsTested++;

        // The real gap: what is left of the bare gap once both jackets are taken off.
        double real = bare_ - a.InsFt - b.InsFt;
        if (real <= requiredFt)
            findings.Add((a.El, b.El, ToMm(bare_), ToMm(a.InsFt), ToMm(b.InsFt), ToMm(real)));
    }
}

sb.AppendLine($"PAIRS TESTED: {pairsTested}   VIOLATIONS: {findings.Count}");
sb.AppendLine();

if (findings.Count == 0)
{
    sb.AppendLine("CLEAR — every pair keeps the required space once the insulation is allowed for.");
    return sb.ToString();
}

// The ones caused BY the insulation are the interesting rows — they pass a bare-geometry check.
int causedByInsulation = findings.Count(f => f.BareMm > requiredClearanceMm);

sb.AppendLine("| Element | Category | Ins mm | Too close to | Category | Ins mm | Bare gap mm | REAL gap mm | |");
sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
foreach (var f in findings.OrderBy(f => f.RealMm).Take(maxReportedRows))
{
    string flag = f.RealMm < 0 ? "JACKETS TOUCH" : (f.BareMm > requiredClearanceMm ? "insulation causes this" : "");
    sb.AppendLine($"| {f.A.Id} | {f.A.Category?.Name ?? "-"} | {f.InsAMm:F0} | {f.B.Id} | {f.B.Category?.Name ?? "-"} | {f.InsBMm:F0} | {f.BareMm:F0} | {f.RealMm:F0} | {flag} |");
}
if (findings.Count > maxReportedRows)
    sb.AppendLine($"\n... and {findings.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
sb.AppendLine($"MISSED BY A BARE-GEOMETRY CHECK: {causedByInsulation} of {findings.Count}. Those pairs pass action-check-minimum-clearance.cs and still will not fit — they are the reason this fragment exists.");
int touching = findings.Count(f => f.RealMm < 0);
if (touching > 0) sb.AppendLine($"JACKETS ALREADY OVERLAPPING: {touching} pair(s) — the insulation is interpenetrating, not merely tight.");

return sb.ToString();
