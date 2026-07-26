// ============================================================
// RECIPE — place-sleeves-at-wall-penetrations.cs
// PURPOSE: Find every point where a duct or pipe crosses a straight wall, and (on request) place a sleeve
//          family instance at each crossing, rotated to the run's direction, sized to the service +
//          clearance. Dry-run by default — first pass only REPORTS the crossings; placement is a second,
//          explicitly approved run (explorer-first rule).
// STANDALONE — has its own sb and returns; not filter+action shaped (two intersecting sets + placement).
// GOTCHA: geometry is centerline-based — the crossing point is the duct/pipe centerline hitting the
//         wall's centerline plane in plan, with a Z-range check against the wall's bounding box. Curved
//         walls and non-straight runs are skipped and REPORTED (count them in the output, don't assume
//         zero).
// GOTCHA: needs a point-based sleeve family already loaded (load-family.cs first if not). Size parameters
//         are matched by common names ("Diameter", "Width", "Height") — an office sleeve family with
//         different parameter names needs those names edited in the INPUTS.
// GOTCHA: walls in LINKED models are not scanned — host-model walls only (linked-wall crossing needs the
//         link's transform applied; not built until actually needed).
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions; needs a sleeve family fixture.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mepKind = "duct";            // "duct" | "pipe"
bool dryRun = true;                  // true = report crossings only; false = place sleeves (after approval!)
string sleeveFamilyName = "";        // placement only — family must already be loaded
string sleeveTypeName = null;        // null = first type of that family
double clearanceMm = 25;             // added all around: sleeve size = service size + 2 x clearance
string diameterParamName = "Diameter";  // sleeve family's size parameter names — office-family specific
string widthParamName = "Width";
string heightParamName = "Height";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

// --- collect the MEP curves ---
var mepCurves = new List<MEPCurve>();
if (mepKind == "duct")
    mepCurves.AddRange(new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.Duct)).Cast<MEPCurve>());
else if (mepKind == "pipe")
    mepCurves.AddRange(new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe)).Cast<MEPCurve>());
else
    sb.AppendLine($"Unknown mepKind '{mepKind}' — use \"duct\" or \"pipe\".");

// --- collect straight host-model walls ---
var walls = new FilteredElementCollector(Document).OfClass(typeof(Wall)).Cast<Wall>().ToList();
int curvedWallsSkipped = 0, nonLinearRunsSkipped = 0;

// crossing record: mep, wall, point, direction
var crossings = new List<Tuple<MEPCurve, Wall, XYZ, XYZ>>();

if (mepCurves.Count > 0 && walls.Count > 0)
{
    foreach (var mep in mepCurves)
    {
        var mepLine = (mep.Location as LocationCurve)?.Curve as Line;
        if (mepLine == null) { nonLinearRunsSkipped++; continue; }
        var mp1 = mepLine.GetEndPoint(0); var mp2 = mepLine.GetEndPoint(1);
        var mepDir = (mp2 - mp1);

        foreach (var wall in walls)
        {
            var wallLine = (wall.Location as LocationCurve)?.Curve as Line;
            if (wallLine == null) { curvedWallsSkipped++; continue; }
            var wp1 = wallLine.GetEndPoint(0); var wp2 = wallLine.GetEndPoint(1);

            // 2D (plan) parametric intersection of the two segments
            double rX = mp2.X - mp1.X, rY = mp2.Y - mp1.Y;
            double sX = wp2.X - wp1.X, sY = wp2.Y - wp1.Y;
            double denom = rX * sY - rY * sX;
            if (Math.Abs(denom) < 1e-9) continue; // parallel in plan
            double tPar = ((wp1.X - mp1.X) * sY - (wp1.Y - mp1.Y) * sX) / denom;
            double uPar = ((wp1.X - mp1.X) * rY - (wp1.Y - mp1.Y) * rX) / denom;
            if (tPar < 0 || tPar > 1 || uPar < 0 || uPar > 1) continue; // outside one of the segments

            var hit = new XYZ(mp1.X + tPar * rX, mp1.Y + tPar * rY, mp1.Z + tPar * (mp2.Z - mp1.Z));

            // Z check — run must pass through the wall's height band
            var wbb = wall.get_BoundingBox(null);
            if (wbb == null || hit.Z < wbb.Min.Z || hit.Z > wbb.Max.Z) continue;

            crossings.Add(Tuple.Create(mep, wall, hit, mepDir.Normalize()));
        }
    }

    sb.AppendLine($"Found {crossings.Count} {mepKind}-wall crossing(s) across {mepCurves.Count} run(s) and {walls.Count} wall(s).");
    if (curvedWallsSkipped > 0) sb.AppendLine($"  {curvedWallsSkipped} curved-wall pairing(s) skipped (centerline method is straight-only).");
    if (nonLinearRunsSkipped > 0) sb.AppendLine($"  {nonLinearRunsSkipped} non-straight run(s) skipped.");
    foreach (var c in crossings.Take(30))
        sb.AppendLine($"  - {mepKind} Id {c.Item1.Id.IntegerValue} x wall Id {c.Item2.Id.IntegerValue} at ({toMm(c.Item3.X):F0}, {toMm(c.Item3.Y):F0}, {toMm(c.Item3.Z):F0}) mm");
    if (crossings.Count > 30) sb.AppendLine($"  ... +{crossings.Count - 30} more");
}
else if (mepCurves.Count == 0) sb.AppendLine($"No {mepKind}s in the model — nothing to check.");
else sb.AppendLine("No walls in the model — nothing to check.");

// --- placement pass (only after the dry-run count was approved) ---
if (!dryRun && crossings.Count > 0)
{
    var symbol = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
        .Where(s => s.FamilyName == sleeveFamilyName)
        .Where(s => sleeveTypeName == null || s.Name == sleeveTypeName)
        .FirstOrDefault();

    if (symbol == null)
    {
        sb.AppendLine($"Sleeve family '{sleeveFamilyName}'{(sleeveTypeName != null ? $" type '{sleeveTypeName}'" : "")} not found in the project — load it first (creators/load-family.cs).");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Place Sleeves"))
        {
            t.Start();
            try
            {
                if (!symbol.IsActive) { symbol.Activate(); Document.Regenerate(); }
                int placed = 0; var placedIds = new List<int>();

                foreach (var c in crossings)
                {
                    var inst = Document.Create.NewFamilyInstance(c.Item3, symbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    // rotate about Z so the sleeve's X axis follows the run direction
                    double angle = Math.Atan2(c.Item4.Y, c.Item4.X);
                    if (Math.Abs(angle) > 1e-6)
                    {
                        var axis = Line.CreateBound(c.Item3, c.Item3 + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(Document, inst.Id, axis, angle);
                    }

                    // size from the service + clearance — round first, else rectangular
                    var svcDia = c.Item1.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    var svcW = c.Item1.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    var svcH = c.Item1.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    if (svcDia != null && svcDia.HasValue)
                    {
                        var pD = inst.LookupParameter(diameterParamName);
                        if (pD != null && !pD.IsReadOnly) pD.Set(svcDia.AsDouble() + 2 * mm(clearanceMm));
                    }
                    else if (svcW != null && svcW.HasValue && svcH != null && svcH.HasValue)
                    {
                        var pW = inst.LookupParameter(widthParamName);
                        var pH = inst.LookupParameter(heightParamName);
                        if (pW != null && !pW.IsReadOnly) pW.Set(svcW.AsDouble() + 2 * mm(clearanceMm));
                        if (pH != null && !pH.IsReadOnly) pH.Set(svcH.AsDouble() + 2 * mm(clearanceMm));
                    }

                    placed++; placedIds.Add(inst.Id.IntegerValue);
                }
                t.Commit();
                sb.AppendLine($"Placed {placed} sleeve(s) ('{symbol.FamilyName}' : '{symbol.Name}', +{clearanceMm} mm clearance all around). Ids: {string.Join(", ", placedIds.Take(30))}{(placedIds.Count > 30 ? ", ..." : "")}");
                sb.AppendLine("Verify visually + re-run the dry-run to confirm every crossing now has its sleeve.");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED mid-placement — rolled back, NO sleeves were placed. Reason: {ex.Message}");
            }
        }
    }
}
else if (dryRun && crossings.Count > 0)
{
    sb.AppendLine("DRY RUN — nothing placed. Rerun with dryRun=false + the sleeve family INPUTS after the user approves this count.");
}

return sb.ToString();
