// ============================================================
// FRAGMENT (action) — action-align-mep-elevation.cs
// PURPOSE: Line up the MEP runs in `elements` vertically by their TOP, their BOTTOM or their CENTRE —
//          "get all the services in this corridor level with each other", "align the bottoms so the
//          ceiling fits". Works out each element's own vertical half-size from its real size parameter,
//          so a 600 mm duct and a 100 mm pipe genuinely share a soffit line.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — ducts, pipes,
//          cable trays, conduits.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// WHY THIS EXISTS ALONGSIDE `action-align-elements.cs`: that one aligns INSERTION POINTS. For MEP the
//          insertion point is the run's CENTRELINE, so aligning two runs of different sizes by it
//          leaves their tops and bottoms at different heights — which is exactly the thing a coordinated
//          corridor needs to be equal. Use that fragment for centreline-to-centreline; use this one when
//          the user says "top", "bottom", "soffit", "underside" or "level with each other".
//
// GOTCHA: DRY RUN BY DEFAULT — it moves real geometry in bulk.
// GOTCHA: the vertical half-size comes from a DIFFERENT parameter per kind, and getting it wrong makes
//         the alignment quietly wrong rather than failing:
//           rectangular duct -> RBS_CURVE_HEIGHT_PARAM        round duct -> RBS_CURVE_DIAMETER_PARAM
//           pipe             -> RBS_PIPE_DIAMETER_PARAM       cable tray -> RBS_CABLETRAY_HEIGHT_PARAM
//         Anything with none of them is treated as zero-height (its centreline IS its top and bottom)
//         and counted separately in the report, so a wrong assumption is visible.
// GOTCHA: INSULATION IS NOT INCLUDED. These parameters give the bare service size. An insulated duct's
//         real top is higher than this calculates. If the job is about clearance to a ceiling, add the
//         insulation thickness to `extraClearanceMm` yourself — it is not read automatically because
//         insulation is a separate element.
// GOTCHA: this moves in Z ONLY. A sloped run gets its whole line shifted, not re-sloped — its fall is
//         preserved and both ends move by the same amount, so the alignment is exact only at the end
//         used for the measurement. Sloped runs are counted in the report; check them.
// GOTCHA: pinned elements and group members accept MoveElement and DO NOT MOVE — see
//         knowledge/live-model/geometry-and-transforms.md. Positions are re-read after the move and
//         anything that did not actually shift is reported as blocked, not as moved.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Match MEP Element
//   Elevation. Compile-checked on 2020/2024/2027. Dry-run it and read the before/after table first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report the moves only; false = actually move
string align = "bottom";             // "top" | "bottom" | "centre"
int referenceElementIdInt = 0;       // 0 = use the HIGHEST/LOWEST in the set (see targetMode); else this element sets the line
string targetMode = "reference";     // "reference" = match referenceElementIdInt | "highest" | "lowest" | "levelMm"
double levelMm = 0;                  // used only when targetMode = "levelMm" — the absolute Z to align to
double extraClearanceMm = 0;         // shift everything this much after aligning (e.g. to clear insulation)
// ---- END INPUTS ----

const double MM = 304.8;

// half the element's own vertical size, from whichever parameter its kind actually uses
Func<Element, double> halfHeight = el =>
{
    BuiltInParameter[] tries = {
        BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,       // rectangular duct
        BuiltInParameter.RBS_CURVE_DIAMETER_PARAM,     // round duct
        BuiltInParameter.RBS_PIPE_DIAMETER_PARAM,      // pipe
        BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM    // cable tray
    };
    foreach (var bip in tries)
    {
        var p = el.get_Parameter(bip);
        if (p != null && p.HasValue && p.StorageType == StorageType.Double)
        {
            double v = p.AsDouble();
            if (v > 0) return v / 2.0;
        }
    }
    return 0.0;   // unknown kind — centreline is its own top and bottom
};

// the element's current reference Z, for the chosen alignment face
Func<Element, double> refZ = el =>
{
    double z;
    var lc = el.Location as LocationCurve;
    if (lc != null && lc.Curve != null) z = (lc.Curve.GetEndPoint(0).Z + lc.Curve.GetEndPoint(1).Z) / 2.0;
    else
    {
        var lp = el.Location as LocationPoint;
        if (lp == null) return double.NaN;
        z = lp.Point.Z;
    }
    double h = halfHeight(el);
    if (align.Equals("top", StringComparison.OrdinalIgnoreCase)) return z + h;
    if (align.Equals("bottom", StringComparison.OrdinalIgnoreCase)) return z - h;
    return z;
};

var usable = new List<Element>();
int noLocation = 0, noSize = 0, sloped = 0;
foreach (var e in elements)
{
    if (e == null) { noLocation++; continue; }
    if (double.IsNaN(refZ(e))) { noLocation++; continue; }
    if (halfHeight(e) <= 0) noSize++;
    var lc = e.Location as LocationCurve;
    if (lc != null && lc.Curve != null && Math.Abs(lc.Curve.GetEndPoint(0).Z - lc.Curve.GetEndPoint(1).Z) > 1e-6) sloped++;
    usable.Add(e);
}

if (usable.Count == 0)
{
    sb.AppendLine("No element in the set has a usable location — nothing to align.");
}
else
{
    // ---- what Z is everything aligning to? ----
    double targetZ;
    string targetLabel;
    if (targetMode.Equals("levelMm", StringComparison.OrdinalIgnoreCase))
    { targetZ = levelMm / MM; targetLabel = $"the fixed level {levelMm} mm"; }
    else if (targetMode.Equals("highest", StringComparison.OrdinalIgnoreCase))
    { targetZ = usable.Max(e => refZ(e)); targetLabel = "the highest in the set"; }
    else if (targetMode.Equals("lowest", StringComparison.OrdinalIgnoreCase))
    { targetZ = usable.Min(e => refZ(e)); targetLabel = "the lowest in the set"; }
    else
    {
        var reference = referenceElementIdInt > 0 ? Document.GetElement(new ElementId(referenceElementIdInt)) : null;
        if (reference == null || double.IsNaN(refZ(reference)))
        {
            sb.AppendLine("targetMode is \"reference\" but referenceElementIdInt is not a usable element. " +
                          "Set it, or use targetMode \"highest\" / \"lowest\" / \"levelMm\".");
            return sb.ToString();
        }
        targetZ = refZ(reference);
        targetLabel = $"element {reference.Id} ({reference.Name})";
    }

    targetZ += extraClearanceMm / MM;

    int moved = 0, alreadyThere = 0, blocked = 0;
    var rows = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Align MEP Elevation"))
    {
        t.Start();
        try
        {
            foreach (var e in usable)
            {
                double before = refZ(e);
                double dz = targetZ - before;
                if (Math.Abs(dz) < 1e-9) { alreadyThere++; continue; }

                if (!dryRun)
                {
                    try { ElementTransformUtils.MoveElement(Document, e.Id, new XYZ(0, 0, dz)); } catch { blocked++; continue; }
                    // MoveElement returns normally on a pinned/grouped element WITHOUT moving it — re-read.
                    Document.Regenerate();
                    double after = refZ(e);
                    if (Math.Abs(after - before) < 1e-9) { blocked++; continue; }
                }

                moved++;
                if (rows.Count < 25)
                    rows.Add(e.Id + " | " + (e.Category != null ? e.Category.Name : "?") + " | " +
                             Math.Round(before * MM, 1) + " | " + Math.Round(targetZ * MM, 1) + " | " +
                             Math.Round(dz * MM, 1));
            }

            if (dryRun) t.RollBack(); else t.Commit();
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED — rolled back, nothing moved. Reason: {ex.Message}");
            return sb.ToString();
        }
    }

    sb.AppendLine((dryRun ? "DRY RUN — nothing moved. " : "") +
                  $"Aligning {align.ToUpperInvariant()} of {usable.Count} service(s) to {targetLabel}" +
                  (extraClearanceMm != 0 ? $" plus {extraClearanceMm} mm" : "") +
                  $" = {Math.Round(targetZ * MM, 1)} mm.");
    sb.AppendLine((dryRun ? "Would move: " : "Moved: ") + moved + " | already aligned: " + alreadyThere +
                  (blocked > 0 ? " | BLOCKED (pinned or grouped — accepted the move and did not move): " + blocked : ""));
    if (noSize > 0) sb.AppendLine($"{noSize} element(s) have no readable size parameter — treated as zero-height " +
                                  "(centreline = top = bottom). Check those if the result looks off.");
    if (sloped > 0) sb.AppendLine($"{sloped} run(s) are SLOPED — their fall is preserved and the whole line shifts, " +
                                  "so they match exactly only at the measured end.");
    if (noLocation > 0) sb.AppendLine($"{noLocation} element(s) had no usable location and were ignored.");

    if (rows.Count > 0)
    {
        sb.AppendLine("");
        sb.AppendLine($"Id | Category | {align} was (mm) | now (mm) | shift (mm)");
        sb.AppendLine("--- | --- | --- | --- | ---");
        foreach (var r in rows) sb.AppendLine(r);
        if (moved > rows.Count) sb.AppendLine("... and " + (moved - rows.Count) + " more");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
