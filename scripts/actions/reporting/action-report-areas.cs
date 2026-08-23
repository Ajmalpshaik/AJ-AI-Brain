// ============================================================
// FRAGMENT (action) — action-report-areas.cs
// PURPOSE: Report AREAS — the Area-plan kind, not Rooms — broken down by Area Scheme, level and name,
//          with totals. Gross, rentable, BOMA, common-area schedules: the numbers a landlord, a cost
//          plan or a submission asks for.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only: opens no Transaction and changes nothing.
//
// WHY THIS EXISTS: this library covered Rooms and Spaces thoroughly and Areas not at all —
//   `OST_Areas` appeared in no fragment. They are a third, separate spatial element with their own
//   rules, and asking a room fragment for an area answer gets a confidently wrong number.
//
// THE THING THAT MAKES AREAS DIFFERENT FROM ROOMS, and the reason a naive count is meaningless:
//   AN AREA BELONGS TO A SCHEME, AND THE SCHEMES OVERLAP ON PURPOSE. The same floor is measured twice
//   — once under "Gross Building" and once under "Rentable" — and both sets of Area elements sit in
//   OST_Areas at the same time, in the same place. Collecting the category and summing gives a total
//   that is roughly double the building and belongs to no scheme at all. Every number below is
//   therefore reported PER SCHEME, and there is deliberately no grand total across schemes, because
//   such a total has no meaning.
//
// UNPLACED AND UNBOUNDED, which are two different faults:
//   * UNPLACED — the Area element exists in the schedule but sits nowhere in the model. Area is 0 and
//     `.Location` is null. It still occupies a row in every schedule.
//   * UNBOUNDED (Revit calls it "not enclosed") — it IS placed but its area boundary lines do not
//     close, so Revit gives it an area of 0. It looks placed on the plan and contributes nothing.
//   Both read as "0" in a schedule and have completely different fixes, so they are separated here.
//
// RELATED: actions/reporting/action-count-by-spatial-container.cs (Rooms and Spaces — the other two
//          spatial kinds), actions/reporting/action-report-material-takeoff.cs (quantities by material),
//          skills/ajtools-visual-report/SKILL.md (turn these numbers into the chart he expects).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only, so the worst case is a wrong number rather than a wrong
//    model — but check one scheme's total against Revit's own area schedule before quoting it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string schemeNameContains = "";   // "" = every Area Scheme; else only schemes whose name matches
bool groupByLevel = true;         // true = a sub-total per level inside each scheme
bool listEachArea = false;        // true = one line per Area element (long)
int maxRowsListed = 200;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

const double SQFT_TO_SQM = 0.09290304;

var schemes = new FilteredElementCollector(Document)
    .OfClass(typeof(AreaScheme))
    .Cast<AreaScheme>()
    .Where(s => s != null && s.IsValidObject)
    .ToList();

if (schemes.Count == 0)
{
    sb.AppendLine("This document has no Area Schemes, so it has no Areas. (Rooms and Spaces are separate — see action-count-by-spatial-container.cs.)");
    return sb.ToString();
}

var areas = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Areas)
    .WhereElementIsNotElementType()
    .ToElements()
    .OfType<Autodesk.Revit.DB.Area>()
    .ToList();

if (areas.Count == 0)
{
    sb.AppendLine($"This document has {schemes.Count} Area Scheme(s) but no Area elements placed in any of them.");
    foreach (var s in schemes) sb.AppendLine($"  scheme: '{s.Name}'");
    return sb.ToString();
}

// Which scheme each area belongs to. The scheme is on the area's own AREA_SCHEME_ID parameter, not on
// its level and not on its view — an area's view can be deleted and the area stays in its scheme.
Func<Autodesk.Revit.DB.Area, ElementId> SchemeIdOf = a =>
{
    try
    {
        var p = a.get_Parameter(BuiltInParameter.AREA_SCHEME_ID);
        if (p != null && p.HasValue) return p.AsElementId();
    }
    catch { }
    return ElementId.InvalidElementId;
};

var wanted = schemes
    .Where(s => string.IsNullOrEmpty(schemeNameContains)
             || (s.Name ?? "").IndexOf(schemeNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
    .ToList();

if (wanted.Count == 0)
{
    sb.AppendLine($"No Area Scheme's name contains \"{schemeNameContains}\". Schemes in this document:");
    foreach (var s in schemes) sb.AppendLine($"  '{s.Name}'");
    return sb.ToString();
}

sb.AppendLine($"AREAS BY SCHEME — {areas.Count} area element(s) across {schemes.Count} scheme(s)"
    + (wanted.Count < schemes.Count ? $", showing {wanted.Count}" : "") + ".");
sb.AppendLine("Totals are PER SCHEME on purpose: schemes measure the same floor more than once, so a total across them is not a real number.");

int rowsUsed = 0;
int totalUnplaced = 0, totalUnbounded = 0;

foreach (var scheme in wanted.OrderBy(s => s.Name))
{
    var mine = areas.Where(a => SchemeIdOf(a) == scheme.Id).ToList();

    sb.AppendLine();
    sb.AppendLine($"=== {scheme.Name} === ({mine.Count} area(s))");

    if (mine.Count == 0) { sb.AppendLine("  nothing placed in this scheme."); continue; }

    double sum = 0.0;
    int unplaced = 0, unbounded = 0, counted = 0;
    var byLevel = new Dictionary<string, double>();
    var byLevelCount = new Dictionary<string, int>();

    foreach (var a in mine)
    {
        double sqft = 0.0;
        try { sqft = a.Area; } catch { }

        bool placed = true;
        try { placed = a.Location != null; } catch { placed = false; }

        if (!placed) { unplaced++; totalUnplaced++; continue; }
        if (sqft <= 1e-9) { unbounded++; totalUnbounded++; continue; }

        counted++;
        sum += sqft;

        string lvl = "(no level)";
        try { var l = Document.GetElement(a.LevelId) as Level; if (l != null) lvl = l.Name ?? lvl; } catch { }
        if (!byLevel.ContainsKey(lvl)) { byLevel[lvl] = 0.0; byLevelCount[lvl] = 0; }
        byLevel[lvl] += sqft;
        byLevelCount[lvl]++;

        if (listEachArea && rowsUsed < maxRowsListed)
        {
            string num = "", nm = "";
            try { var p = a.get_Parameter(BuiltInParameter.ROOM_NUMBER); num = p != null ? (p.AsString() ?? "") : ""; } catch { }
            try { nm = a.Name ?? ""; } catch { }
            sb.AppendLine($"    {num,-8} {nm,-30} {lvl,-14} {Math.Round(sqft * SQFT_TO_SQM, 2),10} m2");
            rowsUsed++;
        }
    }

    if (groupByLevel && byLevel.Count > 0)
    {
        sb.AppendLine("  Level          | Count |      Area m2");
        foreach (var kv in byLevel.OrderBy(k => k.Key))
            sb.AppendLine($"  {kv.Key,-14} | {byLevelCount[kv.Key],5} | {Math.Round(kv.Value * SQFT_TO_SQM, 2),12}");
    }

    sb.AppendLine($"  SCHEME TOTAL: {counted} placed area(s), {Math.Round(sum * SQFT_TO_SQM, 2)} m2 ({Math.Round(sum, 2)} sq ft).");
    if (unplaced > 0)
        sb.AppendLine($"  ! {unplaced} UNPLACED — the area exists in the schedule but sits nowhere in the model. Place it or delete it.");
    if (unbounded > 0)
        sb.AppendLine($"  ! {unbounded} placed but NOT ENCLOSED — its area boundary lines do not close, so Revit gives it 0. It looks fine on the plan and counts for nothing.");
}

if (totalUnplaced > 0 || totalUnbounded > 0)
{
    sb.AppendLine();
    sb.AppendLine($"Across all schemes shown: {totalUnplaced} unplaced, {totalUnbounded} not enclosed. Both show as 0 in a schedule and have different fixes — see above.");
}

if (listEachArea && rowsUsed >= maxRowsListed)
    sb.AppendLine($"(per-area list stopped at maxRowsListed = {maxRowsListed}.)");

return sb.ToString();
