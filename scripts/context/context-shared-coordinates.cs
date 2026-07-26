// ============================================================
// SCRIPT (context) — context-shared-coordinates.cs
// PURPOSE: Report the document's coordinate setup — Project Base Point, Survey Point, active Project
//          Location name, and the True North rotation. The orientation step before any "does this model
//          sit in the right place / is it rotated correctly" coordination question.
// STANDALONE — read-only, zero input, safe anytime. Same role as the other context scripts.
// GOTCHA: values are reported in METERS (survey convention) with mm in brackets — E/W is X, N/S is Y.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

var sb = new System.Text.StringBuilder();

Func<double, string> fmt = ft =>
{
    double mmV = UnitUtils.ConvertFromInternalUnits(ft, DisplayUnitType.DUT_MILLIMETERS);
    return $"{mmV / 1000.0:F3} m ({mmV:F0} mm)";
};

sb.AppendLine($"Active Project Location: '{Document.ActiveProjectLocation.Name}'");

var pos = Document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
sb.AppendLine($"True North rotation: {pos.Angle * 180.0 / Math.PI:F3}° (positive = counter-clockwise from Project North)");
sb.AppendLine($"Model origin in shared coordinates — E/W: {fmt(pos.EastWest)}, N/S: {fmt(pos.NorthSouth)}, Elevation: {fmt(pos.Elevation)}");

var basePoints = new FilteredElementCollector(Document).OfClass(typeof(BasePoint)).Cast<BasePoint>().ToList();
foreach (var bp in basePoints)
{
    string kind = bp.IsShared ? "Survey Point" : "Project Base Point";
    Func<BuiltInParameter, string> rd = bip =>
    {
        var p = bp.get_Parameter(bip);
        return p == null ? "(n/a)" : fmt(p.AsDouble());
    };
    sb.AppendLine($"{kind} (Id {bp.Id.IntegerValue}) — E/W: {rd(BuiltInParameter.BASEPOINT_EASTWEST_PARAM)}, N/S: {rd(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM)}, Elev: {rd(BuiltInParameter.BASEPOINT_ELEVATION_PARAM)}"
        + (bp.IsShared ? "" : $", Angle to True North: {(bp.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM) != null ? (bp.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).AsDouble() * 180.0 / Math.PI).ToString("F3") + "°" : "(n/a)")}"));
}

return sb.ToString();
