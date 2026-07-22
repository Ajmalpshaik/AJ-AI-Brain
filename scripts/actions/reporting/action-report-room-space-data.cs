// ============================================================
// FRAGMENT (action) — action-report-room-space-data.cs
// PURPOSE: Area/Volume/Level table for every Room or Space in `elements` — read-only, no transaction.
//          Works on either category since both derive from the same SpatialElement base (Area, Volume,
//          Level, Name, Number are shared); Occupancy is read via LookupParameter since it isn't
//          guaranteed present on every project template.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: Room Volume only has a real value if "Areas and Volumes" computation is turned on (Room & Area >
//         Area and Volume Computations) — otherwise every Room reports Volume = 0, that's expected, not a
//         bug. Spaces compute Volume by default, no such switch.
// ============================================================

bool anyVolumeZero = false;
var rows = new List<(int id, string name, string number, string level, double areaSqm, double volumeCum, string occupancy)>();
int skipped = 0;

foreach (var e in elements)
{
    var sp = e as SpatialElement;
    if (sp == null) { skipped++; continue; }

    double areaSqm = UnitUtils.ConvertFromInternalUnits(sp.Area, DisplayUnitType.DUT_SQUARE_METERS);
    double volumeCum = UnitUtils.ConvertFromInternalUnits(sp.Volume, DisplayUnitType.DUT_CUBIC_METERS);
    if (sp.Volume <= 0) anyVolumeZero = true;

    string occupancy = sp.LookupParameter("Occupancy")?.AsString() ?? "";
    string levelName = sp.Level?.Name ?? "";

    rows.Add((sp.Id.IntegerValue, sp.Name, sp.Number, levelName, areaSqm, volumeCum, occupancy));
}

sb.AppendLine($"Room/Space data for {rows.Count} element(s) ({skipped} skipped, not a Room/Space):");
sb.AppendLine("Id | Name | Number | Level | Area (sqm) | Volume (cum) | Occupancy");
sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");
foreach (var r in rows.OrderBy(r => r.level).ThenBy(r => r.number))
    sb.AppendLine($"{r.id} | {r.name} | {r.number} | {r.level} | {r.areaSqm:F2} | {r.volumeCum:F2} | {r.occupancy}");

double totalArea = rows.Sum(r => r.areaSqm);
double totalVolume = rows.Sum(r => r.volumeCum);
sb.AppendLine($"TOTAL: {totalArea:F2} sqm, {totalVolume:F2} cum.");
if (anyVolumeZero) sb.AppendLine("NOTE: one or more elements show Volume = 0 — for Rooms, turn on \"Areas and Volumes\" computation if Room volumes are needed.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
