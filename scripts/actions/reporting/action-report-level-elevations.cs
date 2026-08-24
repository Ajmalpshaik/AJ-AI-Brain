// ============================================================
// FRAGMENT (action) — action-report-level-elevations.cs
// PURPOSE: Every level's TWO heights side by side — `Elevation` (what the drawing shows) and
//          `ProjectElevation` (what the geometry uses) — plus each level type's "Elevation Base"
//          setting and the project base point / survey point. Ends with a one-line verdict:
//          IS THIS MODEL AFFECTED, yes or no.
//          Run it before trusting any height-based result on a model you have not checked.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`,
//          ends with its own `return`). Collects the levels itself.
// READ-ONLY — opens no transaction, changes nothing.
// SOURCE: knowledge/live-model/level-elevation-vs-project-elevation.md — the rule, the API wording it
//         comes from, and the list of fragments that were fixed. Read it if this reports AFFECTED.
//
// ✱✱ WHY THIS EXISTS — IT IS THE DIAGNOSTIC FOR A DEFECT FOUND IN THIS LIBRARY, 2026-08-24.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says — Project OR Shared. `ProjectElevation` is always measured from the project
//    origin. **Every XYZ the Revit API hands you is in project-internal coordinates** — location
//    points, bounding boxes, solid vertices, ray origins, `Room.IsPointInRoom`. So the moment a
//    level's height meets a coordinate, only `ProjectElevation` is in the same space.
//    Fifteen fragments here mixed the wrong one. On a test model the two numbers are identical and
//    nothing shows; on a real site model with a survey offset every affected answer is wrong by
//    exactly that offset — silently, with a plausible number.
//
// ✱✱ SO THE ONE-LINE VERDICT IS THE POINT OF THIS FRAGMENT. If it says NOT AFFECTED, height results
//    on this model can be read at face value. If it says AFFECTED, anything computed from a level
//    height on this model needs checking — and the offset it prints is exactly how wrong it is.
//
// GOTCHA: "Elevation Base" lives on the level TYPE, not the level. Two level types in one model can
//         disagree — half the levels on Project and half on Shared is legal, and is the nastiest
//         version of this because a spot check on one level proves nothing. Every type is reported.
// GOTCHA: a difference of zero does NOT prove Elevation Base is Project. It also happens when the
//         base IS Shared and the shared origin simply sits on the project origin — true until someone
//         relocates the model. Both facts are reported separately for that reason.
// NOTE: `Elevation` is not the wrong property, it is the right one for a HUMAN — it is what the level
//       head and the Properties palette show. Ordering is safe either way: both are monotonic.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only,
//    so the worst case is a wrong reading. Check one level's Elevation against Revit's own Properties
//    palette before trusting the table.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxLevelsListed = 80;
bool showBasePoints = true;    // project base point + survey point, and the shared offset between them
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
const double MM = 304.8;

var levels = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.ProjectElevation)
    .ToList();

if (levels.Count == 0)
{
    sb.AppendLine("No levels in this model.");
    return sb.ToString();
}

// ---------- per-level table ----------
// A level type's "Elevation Base" is LEVEL_RELATIVE_BASE_TYPE, present on 2020 through 2027. It is
// read off the TYPE, not the level, so two level types in one model can disagree.
// ✱✱ THE VALUE IS READ AS REVIT'S OWN DISPLAY TEXT, NOT AS AN INTEGER. The API documentation names the
//    parameter and does NOT publish an enum for its values, so "0 means Project" is an assumption, not
//    a fact — and a report that mislabels this is worse than one that does not try. `AsValueString()`
//    returns exactly what Revit shows in the Properties palette. It is LOCALISED, so no logic below
//    depends on the words; the verdict is measured from the two elevations instead.
Func<Level, string> elevationBaseOf = lvl =>
{
    try
    {
        var typ = Document.GetElement(lvl.GetTypeId());
        if (typ == null) return "(no type)";
        var p = typ.get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE);
        if (p == null || !p.HasValue) return "(not set)";
        string shown = null;
        try { shown = p.AsValueString(); } catch { }
        return string.IsNullOrEmpty(shown) ? ("raw value " + p.AsInteger()) : shown;
    }
    catch { return "(unreadable)"; }
};

double maxAbsDiffMm = 0;
var basesSeen = new Dictionary<string, int>();
double surveyOffsetMm = 0; bool surveyOffsetKnown = false;
var rows = new List<string>();

foreach (var lvl in levels)
{
    double e = lvl.Elevation;
    double pe = lvl.ProjectElevation;
    double diffMm = (e - pe) * MM;
    if (Math.Abs(diffMm) > maxAbsDiffMm) maxAbsDiffMm = Math.Abs(diffMm);

    string bse = elevationBaseOf(lvl);
    if (!basesSeen.ContainsKey(bse)) basesSeen[bse] = 0;
    basesSeen[bse]++;

    string typeName = "(unknown)";
    try { var t = Document.GetElement(lvl.GetTypeId()); if (t != null) typeName = t.Name; } catch { }

    rows.Add($"{lvl.Name} | {typeName} | {bse} | {Math.Round(e * MM):N0} | {Math.Round(pe * MM):N0} | {Math.Round(diffMm):N0}");
}

sb.AppendLine($"LEVEL ELEVATIONS — {levels.Count} level(s) in '{Document.Title}'");
sb.AppendLine();
sb.AppendLine("Level | Level type | Elevation Base | Elevation (mm) | ProjectElevation (mm) | Difference (mm)");
sb.AppendLine("--- | --- | --- | ---: | ---: | ---:");
foreach (var r in rows.Take(maxLevelsListed)) sb.AppendLine(r);
if (rows.Count > maxLevelsListed)
    sb.AppendLine($"... {rows.Count - maxLevelsListed} more level(s) not listed (raise maxLevelsListed).");

sb.AppendLine();
sb.AppendLine("Elevation       = what the level head and the Properties palette show.");
sb.AppendLine("ProjectElevation = what every XYZ in the model is measured from. Use this one in any calculation.");

// ---------- base points ----------
if (showBasePoints)
{
    sb.AppendLine();
    sb.AppendLine("BASE POINTS");
    try
    {
        // Both the Project Base Point and the Survey Point are BasePoint elements in OST_ProjectBasePoint
        // and OST_SharedBasePoint. IsShared distinguishes them on every version from 2020.
        var pts = new FilteredElementCollector(Document)
            .OfClass(typeof(BasePoint))
            .Cast<BasePoint>()
            .ToList();
        if (pts.Count == 0)
        {
            sb.AppendLine("  none found (unusual — a project normally has both).");
        }
        foreach (var bp in pts)
        {
            string which = bp.IsShared ? "Survey point" : "Project base point";
            var pos = bp.Position;
            var shared = bp.SharedPosition;
            sb.AppendLine($"  {which}: position ({Math.Round(pos.X * MM):N0}, {Math.Round(pos.Y * MM):N0}, {Math.Round(pos.Z * MM):N0}) mm"
                        + $" | shared ({Math.Round(shared.X * MM):N0}, {Math.Round(shared.Y * MM):N0}, {Math.Round(shared.Z * MM):N0}) mm");
            // The VERTICAL gap between a base point's project position and its shared position is the
            // offset a Shared-based level height would carry. Measured, not inferred from a parameter.
            if (bp.IsShared) { surveyOffsetMm = (shared.Z - pos.Z) * MM; surveyOffsetKnown = true; }
        }
    }
    catch (Exception ex)
    {
        sb.AppendLine("  could not read the base points: " + ex.Message);
    }
}

// ---------- the verdict ----------
sb.AppendLine();
// The verdict is MEASURED from the two elevations and the survey point, never inferred from the
// Elevation Base wording — which is localised, and whose integer values the API does not publish.
var realBases = basesSeen.Keys.Where(k => !k.StartsWith("(")).ToList();
bool mixedBases = realBases.Count > 1;

sb.AppendLine("Elevation Base across the level types (Revit's own wording): "
    + string.Join(", ", basesSeen.Select(kv => $"{kv.Key} x{kv.Value}")));

if (maxAbsDiffMm > 0.5)
{
    sb.AppendLine($"*** AFFECTED. The two heights differ by up to {Math.Round(maxAbsDiffMm):N0} mm on this model.");
    sb.AppendLine("    Anything computed from a level height here is wrong by that amount unless it used");
    sb.AppendLine("    ProjectElevation. Re-check any height result taken from this model before quoting it.");
}
else if (surveyOffsetKnown && Math.Abs(surveyOffsetMm) > 0.5)
{
    sb.AppendLine($"NOT AFFECTED TODAY, BUT AT RISK. The two heights agree right now, yet the survey point sits");
    sb.AppendLine($"    {Math.Round(surveyOffsetMm):N0} mm above the project origin in Z. Any level type switched to a");
    sb.AppendLine("    shared base — or a relocate — makes the two diverge by that amount, with no other warning.");
}
else
{
    sb.AppendLine("NOT AFFECTED. The two heights agree on every level"
        + (surveyOffsetKnown ? " and the survey point has no vertical offset" : "")
        + ", so height results on this model can be read at face value.");
}

if (mixedBases)
{
    sb.AppendLine("*** AND THE LEVEL TYPES DISAGREE WITH EACH OTHER — more than one Elevation Base setting is in");
    sb.AppendLine("    use. Checking one level proves nothing about the rest on this model.");
}

return sb.ToString();
