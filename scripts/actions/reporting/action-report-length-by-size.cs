// ============================================================
// FRAGMENT (action) — action-report-length-by-size.cs
// PURPOSE: Report count AND total length per size group for linear MEP elements (ducts, pipes, cable
//          trays — anything with a "Size" string parameter and a Length parameter). Different from
//          action-count-and-report.cs's breakdown table, which counts per size but doesn't sum length.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ ADDED 2026-08-24 — SURFACE AREA, AND GROUPING BY SYSTEM AND TYPE, NOT JUST SIZE.
//    Two gaps, both of which made this thinner than a BOQ needs:
//      AREA. `BuiltInParameter.RBS_CURVE_SURFACE_AREA` is REVIT'S OWN surface area for a duct or pipe
//        — perimeter x length, already computed, already in the model. Nothing in this library read
//        it. For ductwork that number IS the sheet-metal area of the straight runs, so a takeoff no
//        longer has to be derived from the perimeter by hand. It is also the free cross-check on
//        action-report-duct-weight.cs, which computes the same quantity from the size band.
//      GROUPING. A BOQ line is "Supply Air / Rectangular Duct / 300x150", not "300x150" summed across
//        every system in the building. `groupBySystem` and `groupByType` add those keys. Both default
//        OFF so the old one-column output is unchanged unless asked for.
//    A blank area is reported as blank, never as 0 — cable tray and conduit have no such parameter,
//    and a zero there would read as "no metal" instead of "not applicable".
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// RBS_CALCULATED_SIZE works for ducts and pipes (string like "300x150" or "DN200"). Cable trays use the
// same BuiltInParameter name too. CURVE_ELEM_LENGTH works for any curve-based MEP element.
BuiltInParameter sizeParam = BuiltInParameter.RBS_CALCULATED_SIZE;
BuiltInParameter lengthParam = BuiltInParameter.CURVE_ELEM_LENGTH;
bool groupBySystem = false;  // add the System Name to the grouping key — a BOQ line per system
bool groupByType   = false;  // add the element Type name to the grouping key
bool showArea      = true;   // Revit's own RBS_CURVE_SURFACE_AREA, in m2. Blank where it does not apply
// ---- END INPUTS ----

// key -> (count, totalLengthMm, totalAreaM2, howManyRowsHadAnArea)
var groups = new Dictionary<string, Tuple<int, double, double, int>>();
var sizeOfKey = new Dictionary<string, string>();   // key -> the size part of it, for the sort

const double SQFT_TO_SQM = 0.09290304;

foreach (var e in elements)
{
    var sp = e.get_Parameter(sizeParam);
    var lp = e.get_Parameter(lengthParam);
    string size = (sp != null && sp.HasValue) ? sp.AsString() : "unknown";
    double lenMm = (lp != null && lp.HasValue) ? lp.AsDouble() * 304.8 : 0;

    // Revit's own surface area. Absent on cable tray/conduit, so a missing parameter is recorded as
    // "not applicable" and never folded in as a zero.
    double areaM2 = 0; bool hasArea = false;
    if (showArea)
    {
        var ap = e.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
        if (ap != null && ap.HasValue) { areaM2 = ap.AsDouble() * SQFT_TO_SQM; hasArea = true; }
    }

    string key = size;
    if (groupByType)
    {
        string tn = "(no type)";
        try { var te = Document.GetElement(e.GetTypeId()); if (te != null) tn = te.Name; } catch { }
        key = tn + " | " + key;
    }
    if (groupBySystem)
    {
        string sys = "(no system)";
        try { var syp = e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM); if (syp != null && syp.HasValue) sys = syp.AsString(); } catch { }
        key = sys + " | " + key;
    }
    sizeOfKey[key] = size;

    if (!groups.ContainsKey(key)) groups[key] = Tuple.Create(0, 0.0, 0.0, 0);
    var cur = groups[key];
    groups[key] = Tuple.Create(cur.Item1 + 1, cur.Item2 + lenMm, cur.Item3 + areaM2, cur.Item4 + (hasArea ? 1 : 0));
}

// Sort key: parse the leading number(s) out of the size string ("450x250" -> 450,250; "250ø" -> 250,250)
// so rows read smallest-to-largest like a Revit schedule, not by qty/length. the user's standing rule
// (2026-07-18, see reply-style.md) — never sort a size breakdown by qty or length.
// Strip every non-numeric decoration rather than one named symbol: round sizes arrive as "250ø", "ø250"
// or "DN200" depending on the family, and a literal diameter symbol in this file is itself fragile — it
// was silently double-encoded once by an ANSI read-modify-write, after which the Replace matched nothing,
// TryParse failed, and every round size sorted as 0 (see brain-log 2026-08-04). Keeping the comparison
// logic pure ASCII means that class of corruption can't break this sort again.
Func<string, Tuple<double, double>> sizeSortKey = s =>
{
    var cleaned = new string((s ?? "").Select(c => char.IsDigit(c) || c == '.' || c == 'x' || c == 'X' ? c : ' ').ToArray());
    var parts = cleaned.Replace('X', 'x').Split(new[] { 'x' }, StringSplitOptions.RemoveEmptyEntries);
    double a = 0, b = 0;
    // "unknown" / a blank Size parameter legitimately yields no parts — sort those first at 0,0 rather
    // than indexing into an empty array.
    if (parts.Length > 0) double.TryParse(parts[0], out a);
    if (parts.Length > 1) double.TryParse(parts[1], out b); else b = a;
    return Tuple.Create(a, b);
};

sb.AppendLine("Total: " + elements.Count);
string keyHeading = (groupBySystem ? "System | " : "") + (groupByType ? "Type | " : "") + "Size (mm)";
int keyCols = 1 + (groupBySystem ? 1 : 0) + (groupByType ? 1 : 0);
sb.AppendLine(keyHeading + " | Qty | Total Length (m)" + (showArea ? " | Area (m2)" : ""));
sb.AppendLine(string.Join(" | ", Enumerable.Repeat("---", keyCols + 2 + (showArea ? 1 : 0))));
double grandTotalM = 0, grandTotalArea = 0;
int rowsWithoutArea = 0;
// The sort still runs on the SIZE part only — smallest to largest, never by qty or length (the user's
// standing rule) — with the system/type prefix ordering above it so each group reads as its own block.
foreach (var kv in groups
    .OrderBy(kv => groupBySystem || groupByType ? kv.Key.Substring(0, kv.Key.LastIndexOf('|') + 1) : "", StringComparer.OrdinalIgnoreCase)
    .ThenBy(kv => sizeSortKey(sizeOfKey[kv.Key]).Item1)
    .ThenBy(kv => sizeSortKey(sizeOfKey[kv.Key]).Item2))
{
    double totalM = kv.Value.Item2 / 1000.0;
    grandTotalM += totalM;
    grandTotalArea += kv.Value.Item3;
    if (kv.Value.Item4 == 0) rowsWithoutArea++;
    string areaCell = !showArea ? "" : (kv.Value.Item4 == 0 ? " | " : " | " + Math.Round(kv.Value.Item3, 2));
    sb.AppendLine($"{kv.Key} | {kv.Value.Item1} | {Math.Round(totalM, 2)}{areaCell}");
}
sb.AppendLine(string.Join(" | ", Enumerable.Repeat("---", keyCols + 2 + (showArea ? 1 : 0))));
sb.AppendLine($"TOTAL{string.Concat(Enumerable.Repeat(" |", keyCols - 1))} | {elements.Count} | {Math.Round(grandTotalM, 2)}"
    + (showArea ? " | " + Math.Round(grandTotalArea, 2) : ""));
if (showArea && rowsWithoutArea > 0)
    sb.AppendLine($"({rowsWithoutArea} row(s) have a BLANK area — RBS_CURVE_SURFACE_AREA does not apply to that category, e.g. cable tray and conduit. Blank is not zero.)");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
