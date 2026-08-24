// ============================================================
// FRAGMENT (recipe) — maximize-level-extents.cs
// PURPOSE: Stretch every LEVEL's 3D extent so it spans the active 3D view's SECTION BOX, written into
//          every elevation, section and 3D view that shows each level — "my level lines are short in
//          the sections, make them reach across the whole building". You size the section box around
//          what you want covered, then run this.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
// SOURCE: knowledge/live-model/datums.md — read the 2D/3D section before running this.
//
// GOTCHA: NEEDS AN ACTIVE 3D VIEW WITH ITS SECTION BOX ON. `IsSectionBoxActive` is checked first,
//         because `GetSectionBox()` still returns a box when the box is switched off — so without the
//         check you would silently size levels to a box nobody can see.
// GOTCHA: THE SECTION BOX IS IN ITS OWN TRANSFORM. Its Min/Max are NOT world coordinates. All eight
//         corners go through `box.Transform` before min/max X and Y are taken. Same trap as the view
//         crop box in `actions/visibility/action-set-view-crop.cs`.
// GOTCHA: THE NEW LINE IS BUILT ALONG THE LEVEL'S OWN DIRECTION, not along X or Y. The box's four plan
//         corners are projected onto the level's unbound line and the min/max PARAMETER taken. An
//         axis-aligned line would be wrong the moment the building is rotated — and most are.
// GOTCHA: BOTH ENDS ARE SET TO `Model` BEFORE the curve is written. Writing a Model curve while an end
//         is still view-specific does not land where you meant.
// GOTCHA: THIS IS A WHOLE-MODEL CHANGE. A Model extent is shared, so this rewrites how levels look in
//         every elevation, section and 3D view at once. Dry run first, read the counts, then commit.
// GOTCHA: only levels that already have a Model curve in a given view are touched. A level with only a
//         2D override in some view is left alone there — run `action-reset-datum-extents.cs` first if
//         you want those pulled back onto the shared extent.
// NOTE: levels are lines only in elevation/section/3D views, so those are the only view types collected.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Maximize Levels by
//   Section Box, which does exactly this on real jobs. Dry-run it and check the reported span first.
//
// ✱✱ FIXED 2026-08-24 — LEVEL HEIGHTS HERE NOW USE `ProjectElevation`, NOT `Elevation`.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says (Project OR Shared); `ProjectElevation` is always from the project origin, which is
//    the space every XYZ in the model lives in. This fragment mixes a level height with real
//    coordinates, so on a model with a survey offset the old code was wrong by exactly that offset —
//    silently, with a plausible number and no error. See
//    knowledge/live-model/level-elevation-vs-project-elevation.md, and run
//    action-report-level-elevations.cs to see whether a given model is affected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = report the spans only; false = actually rewrite the extents
double marginMm = 0;             // push the ends out past the section box by this much (0 = exactly the box)
// ---- END INPUTS ----

const double MM = 304.8;
const double TOL = 1e-6;

var sb = new System.Text.StringBuilder();

if (Document.IsFamilyDocument)
{
    sb.AppendLine("This is the Family Editor — there are no project levels here.");
    return sb.ToString();
}

View3D view3D = Document.ActiveView as View3D;
if (view3D == null || view3D.IsTemplate)
{
    sb.AppendLine("Run this from an active 3D view — the section box of that view is what sizes the levels.");
    return sb.ToString();
}
if (!view3D.IsSectionBoxActive)
{
    sb.AppendLine("The active 3D view's section box is switched OFF. Turn it on and size it around what " +
                  "the levels should span, then run this again.");
    return sb.ToString();
}

BoundingBoxXYZ box = view3D.GetSectionBox();
if (box == null || box.Min == null || box.Max == null)
{
    sb.AppendLine("Could not read the section box of the active 3D view.");
    return sb.ToString();
}

// ---- 1. section box corners -> world ----
Transform boxT = box.Transform ?? Transform.Identity;
var worldCorners = new XYZ[] {
    boxT.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Min.Z)),
    boxT.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Min.Z)),
    boxT.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Min.Z)),
    boxT.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Min.Z)),
    boxT.OfPoint(new XYZ(box.Min.X, box.Min.Y, box.Max.Z)),
    boxT.OfPoint(new XYZ(box.Max.X, box.Min.Y, box.Max.Z)),
    boxT.OfPoint(new XYZ(box.Max.X, box.Max.Y, box.Max.Z)),
    boxT.OfPoint(new XYZ(box.Min.X, box.Max.Y, box.Max.Z))
};

double minX = worldCorners.Min(p => p.X), maxX = worldCorners.Max(p => p.X);
double minY = worldCorners.Min(p => p.Y), maxY = worldCorners.Max(p => p.Y);

if ((maxX - minX) <= TOL || (maxY - minY) <= TOL)
{
    sb.AppendLine("The section box has no usable X/Y extent. Nothing changed.");
    return sb.ToString();
}

double marginFt = marginMm / MM;
minX -= marginFt; maxX += marginFt;
minY -= marginFt; maxY += marginFt;

// ---- 2. the levels, and the views that draw them as lines ----
var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().ToList();
if (levels.Count == 0) { sb.AppendLine("No levels in this project."); return sb.ToString(); }

var datumViews = new FilteredElementCollector(Document)
    .OfClass(typeof(View)).Cast<View>()
    .Where(v => v != null && !v.IsTemplate &&
                (v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section || v.ViewType == ViewType.ThreeD))
    .ToList();

if (datumViews.Count == 0)
{
    sb.AppendLine("No elevation, section or 3D views found — a level is only a line in those, so there " +
                  "is nowhere to write an extent. Nothing changed.");
    return sb.ToString();
}

// ---- 3. per level, per view: project the box onto the level's OWN line ----
int levelsTouched = 0, writes = 0, refused = 0;
double longestM = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Maximize Level Extents by Section Box"))
{
    t.Start();
    try
    {
        foreach (Level level in levels)
        {
            bool thisLevelTouched = false;
            double thisSpanM = 0;

            foreach (View v in datumViews)
            {
                Curve existing = null;
                try
                {
                    var curves = level.GetCurvesInView(DatumExtentType.Model, v);
                    if (curves != null && curves.Count > 0) existing = curves.FirstOrDefault();
                }
                catch { }

                Line line = existing as Line;
                if (line == null || !line.IsBound) continue;   // no Model curve here — leave this view alone

                // Unbound line along the level's own direction, then project the box's plan corners.
                Line unbound = Line.CreateUnbound(line.GetEndPoint(0), line.Direction);
                double z = level.ProjectElevation;
                var planCorners = new XYZ[] {
                    new XYZ(minX, minY, z), new XYZ(maxX, minY, z),
                    new XYZ(maxX, maxY, z), new XYZ(minX, maxY, z)
                };

                double minParam = double.MaxValue, maxParam = double.MinValue;
                foreach (XYZ c in planCorners)
                {
                    IntersectionResult res = unbound.Project(c);
                    if (res == null) continue;
                    if (res.Parameter < minParam) minParam = res.Parameter;
                    if (res.Parameter > maxParam) maxParam = res.Parameter;
                }

                if (maxParam - minParam <= TOL) continue;

                try
                {
                    // Both ends to Model FIRST, then write the curve.
                    try { level.SetDatumExtentType(DatumEnds.End0, v, DatumExtentType.Model); } catch { }
                    try { level.SetDatumExtentType(DatumEnds.End1, v, DatumExtentType.Model); } catch { }

                    XYZ p0 = unbound.Evaluate(minParam, false);
                    XYZ p1 = unbound.Evaluate(maxParam, false);
                    Line newLine = Line.CreateBound(p0, p1);

                    if (!dryRun) level.SetCurveInView(DatumExtentType.Model, v, newLine);

                    writes++;
                    thisLevelTouched = true;
                    thisSpanM = newLine.Length * MM / 1000.0;
                }
                catch { refused++; }
            }

            if (thisLevelTouched)
            {
                levelsTouched++;
                if (thisSpanM > longestM) longestM = thisSpanM;
                if (rows.Count < 25) rows.Add(level.Id + " | " + level.Name + " | " +
                                              Math.Round(level.Elevation * MM) + " | " + Math.Round(thisSpanM, 2));
            }
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
              $"Section box spans {Math.Round((maxX - minX) * MM / 1000.0, 2)} x {Math.Round((maxY - minY) * MM / 1000.0, 2)} m" +
              (marginMm > 0 ? $" (incl. {marginMm} mm margin)" : "") + ".");
sb.AppendLine($"{levelsTouched} of {levels.Count} level(s) " + (dryRun ? "would be" : "were") +
              $" extended, across {datumViews.Count} elevation/section/3D view(s) — {writes} write(s).");
if (refused > 0) sb.AppendLine($"{refused} write(s) were refused and skipped.");
if (levelsTouched > 0) sb.AppendLine($"Longest resulting level line: {Math.Round(longestM, 2)} m");

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Id | Level | Elevation (mm) | New span (m)");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (levelsTouched > rows.Count) sb.AppendLine("... and " + (levelsTouched - rows.Count) + " more");
}

return sb.ToString();
