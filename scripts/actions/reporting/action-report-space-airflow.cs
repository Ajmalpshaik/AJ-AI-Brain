// ============================================================
// FRAGMENT (action) — action-report-space-airflow.cs
// PURPOSE: Schedule-style report of MEP Spaces — Number, Name, Level, Area, Volume, and the
//          Design vs Actual Supply/Return/Exhaust airflows, in metric. Answers "list the spaces
//          and their airflow" and, more usefully, "which spaces have no design figure to check
//          the actual against". Read-only. The WRITE counterpart is recipes/set-space-airflow.cs.
//          For a plain Room/Space size table with NO airflow columns (works on Rooms too, not just
//          MEP Spaces), use action-report-room-space-data.cs.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment
//          above. Non-Space elements in `elements` are skipped, not an error, so this is safe
//          after a broad filter.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: airflow parameter names shared with recipes/set-space-airflow.cs.
// ============================================================
// Written live 2026-08-13 on a real model (27 Spaces) after finding no existing fragment
// reported these — everything in scripts/ either SET airflow or counted elements BY space.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool includeUnplaced = true;   // Spaces with Area 0 are unplaced/unbounded — usually worth SEEING, not hiding
bool showVolume      = true;   // set false if the project has volume computation off (every value reads 0)
// ---- END INPUTS ----

// Revit internal units are feet-based, always, regardless of project display units:
//   area   sq ft   -> m2    x 0.09290304
//   volume cu ft   -> m3    x 0.028316846592
//   flow   cu ft/s -> L/s   x 28.3168466
const double TO_M2 = 0.09290304, TO_M3 = 0.028316846592, TO_LPS = 28.3168466;

Func<Element, string, double> D = (e, n) =>
{
    var p = e.LookupParameter(n);
    return (p != null && p.HasValue && p.StorageType == StorageType.Double) ? p.AsDouble() : 0.0;
};

var spaces = elements.OfType<Autodesk.Revit.DB.Mechanical.Space>()
    .Where(s => includeUnplaced || s.Area > 0.0001)
    .OrderBy(s => s.Number, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (spaces.Count == 0)
{
    sb.AppendLine("No MEP Spaces in `elements`.");
}
else
{
    sb.AppendLine($"{spaces.Count} MEP Space(s):");
    sb.AppendLine("No | Name | Level | Area m2" + (showVolume ? " | Vol m3" : "") +
                  " | Design Supply L/s | Actual Supply L/s | Actual Return L/s | Actual Exhaust L/s");
    sb.AppendLine("--- | --- | --- | ---" + (showVolume ? " | ---" : "") + " | --- | --- | --- | ---");

    double tArea = 0, tDesign = 0, tSupply = 0, tReturn = 0, tExhaust = 0;
    int unplaced = 0, noDesign = 0, zeroVolume = 0;

    foreach (var s in spaces)
    {
        double area    = s.Area * TO_M2;
        double vol     = D(s, "Volume") * TO_M3;
        double design  = D(s, "Design Supply Airflow")  * TO_LPS;
        double supply  = D(s, "Actual Supply Airflow")  * TO_LPS;
        double ret     = D(s, "Actual Return Airflow")  * TO_LPS;
        double exhaust = D(s, "Actual Exhaust Airflow") * TO_LPS;

        if (area <= 0.0001) unplaced++;
        if (design <= 0.0001) noDesign++;
        if (vol <= 0.0001) zeroVolume++;

        tArea += area; tDesign += design; tSupply += supply; tReturn += ret; tExhaust += exhaust;

        var lvl = s.Level != null ? s.Level.Name : "-";
        sb.AppendLine($"{(string.IsNullOrEmpty(s.Number) ? "-" : s.Number)} | " +
                      $"{(string.IsNullOrEmpty(s.Name) ? "-" : s.Name)} | {lvl} | " +
                      $"{Math.Round(area, 1)}" + (showVolume ? $" | {Math.Round(vol, 1)}" : "") +
                      $" | {Math.Round(design, 1)} | {Math.Round(supply, 1)} | " +
                      $"{Math.Round(ret, 1)} | {Math.Round(exhaust, 1)}");
    }

    sb.AppendLine($"TOTAL | | | {Math.Round(tArea, 1)}" + (showVolume ? " | " : "") +
                  $" | {Math.Round(tDesign, 1)} | {Math.Round(tSupply, 1)} | " +
                  $"{Math.Round(tReturn, 1)} | {Math.Round(tExhaust, 1)}");

    // The three silent-wrong-answer traps, all three of which were live on the first model this
    // ran against. Each one makes a figure above LOOK fine while meaning nothing.
    if (unplaced > 0)
        sb.AppendLine($"NOTE: {unplaced} Space(s) have Area 0 — unplaced or unbounded, so they " +
                      "cannot carry a real airflow figure.");
    if (noDesign > 0)
        sb.AppendLine($"NOTE: {noDesign} of {spaces.Count} Space(s) have NO Design Supply Airflow. " +
                      "Actual has nothing to be checked against — see recipes/set-space-airflow.cs.");
    if (showVolume && zeroVolume == spaces.Count)
        sb.AppendLine("NOTE: every Volume is 0 — the project has Area and Volume Computations set to " +
                      "'Areas only'. Air changes per hour cannot be derived until that is switched on.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
