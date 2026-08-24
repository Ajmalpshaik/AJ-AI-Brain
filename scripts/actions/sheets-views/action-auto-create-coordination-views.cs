// ============================================================
// FRAGMENT (action) — action-auto-create-coordination-views.cs
// PURPOSE: Create the standard set of MEP coordination views in one go — a 3D view per level, boxed to
//          that level's slab-to-slab zone, named to a convention and optionally on a view template. The
//          half-hour of clicking at the start of every coordination job, done once.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it works from the project's LEVELS.
//
// ✱✱ THE SECTION BOX IS WHAT MAKES THESE USEFUL. A 3D view of the whole building is not a coordination
//    view; a 3D view clipped to one level's void, with the services in it and nothing above or below, is.
//    Each view's box runs from its level up to the next level (plus the margins below), so the zone is
//    the real one rather than an arbitrary slab.
//
// ✱✱ IT WILL NOT MAKE A VIEW THAT ALREADY EXISTS. Every intended name is checked against the project
//    first and existing ones are SKIPPED, not duplicated. Running it twice is safe, and a re-run after
//    adding a level makes only the missing views — which is the behaviour that lets it be re-run at all.
//
// ✱✱ TOP AND BOTTOM LEVELS ARE HANDLED EXPLICITLY. The highest level has no level above it to take a
//    ceiling from, so `topLevelHeightMm` supplies one rather than the box collapsing to zero height or
//    running to infinity. That single case is what breaks a naive per-level loop.
//
// GOTCHA: DRY RUN BY DEFAULT — it prints the views it would make, with their box heights, and creates
//         nothing. Read the list, then set dryRun = false.
// GOTCHA: A VIEW TEMPLATE IS APPLIED IF NAMED AND FOUND, and if the named template does not exist that is
//         REPORTED rather than silently skipped — a coordination view without its template shows the
//         wrong categories and looks finished.
// GOTCHA: `View3D.CreateIsometric` NEEDS A 3D ViewFamilyType. If the project has none, that is reported
//         and nothing is created. It is also created UNCROPPED; the section box does the clipping.
// GOTCHA: VIEW NAMES MUST BE UNIQUE ACROSS THE WHOLE PROJECT, not per view type. A name clash with an
//         existing PLAN view fails the create — those are reported per view rather than taking the batch
//         down.
// GOTCHA: LEVELS WITH NO SERVICES still get a view, because "is there anything on this level" is a
//         question the coordination view exists to answer. Narrow `levelNameContains` if that is not
//         wanted.
// RELATED: creators/create-view.cs (one view, full control), action-duplicate-views.cs,
//          creators/create-workset-3d-views.cs (a 3D view per workset — the other axis of the same idea),
//          action-apply-view-template.cs, action-section-box-and-zoom.cs (box an existing view),
//          action-place-views-on-new-sheets.cs (get them onto sheets afterwards).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Make ONE view first, look at its section box,
//   then let it do the rest.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                       // true = list what would be created, make nothing

string namePattern = "MEP-COORD - {LEVEL}";   // {LEVEL} is replaced by the level name
string levelNameContains = "";                // "" = every level; otherwise only levels matching this
string viewTemplateName = "";                 // "" = no template; otherwise applied if it exists

double belowLevelMm = 500;                // how far BELOW the level the box starts (services under the slab)
double topLevelHeightMm = 4000;           // box height for the HIGHEST level, which has none above it
double planMarginMm = 2000;               // how far past the model extents the box reaches in plan

bool detachCropBox = true;                 // leave the crop off; the section box does the clipping
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

// ---- the 3D view family type ----
var vft3d = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
    .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
if (vft3d == null)
{
    sb.AppendLine("STOP: this project has no 3D ViewFamilyType, so View3D.CreateIsometric cannot run. Nothing was created.");
    return sb.ToString();
}

// ---- levels ----
var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
    .OrderBy(l => l.ProjectElevation).ToList();
if (levels.Count == 0)
{
    sb.AppendLine("STOP: this project has no Levels — there is nothing to make a per-level view from.");
    return sb.ToString();
}

var chosen = string.IsNullOrWhiteSpace(levelNameContains)
    ? levels
    : levels.Where(l => l.Name.IndexOf(levelNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

// ---- existing view names, so nothing is duplicated ----
var existingNames = new HashSet<string>(
    new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Select(v => v.Name ?? ""), StringComparer.OrdinalIgnoreCase);

// ---- the view template, if one was asked for ----
View template = null;
if (!string.IsNullOrWhiteSpace(viewTemplateName))
{
    template = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .FirstOrDefault(v => v.IsTemplate && v.Name.IndexOf(viewTemplateName, StringComparison.OrdinalIgnoreCase) >= 0);
}

// ---- model extents in plan, so the box covers the building ----
double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
int extentSources = 0;
foreach (var cat in new[] { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Grids,
                            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_PipeCurves })
{
    try
    {
        foreach (var e in new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType())
        {
            BoundingBoxXYZ bb = null;
            try { bb = e.get_BoundingBox(null); } catch { }
            if (bb == null) continue;
            extentSources++;
            if (bb.Min.X < minX) minX = bb.Min.X;
            if (bb.Min.Y < minY) minY = bb.Min.Y;
            if (bb.Max.X > maxX) maxX = bb.Max.X;
            if (bb.Max.Y > maxY) maxY = bb.Max.Y;
        }
    }
    catch { }
}

if (extentSources == 0)
{
    sb.AppendLine("STOP: nothing in the model has a bounding box, so a section box would have no extent to cover. Is the model empty, or is everything in a LINK?");
    return sb.ToString();
}

double margin = ToFeet(planMarginMm);
minX -= margin; minY -= margin; maxX += margin; maxY += margin;

// ---- plan the views ----
var plan = new List<(Level Lvl, string Name, double BottomFt, double TopFt, bool Exists)>();

for (int i = 0; i < chosen.Count; i++)
{
    var lvl = chosen[i];
    string name = namePattern.Replace("{LEVEL}", lvl.Name);

    // `ProjectElevation`, NOT `Elevation`. A level has TWO heights: `Elevation` is measured from whatever
    // the level type's Elevation Base says (Project OR Shared); `ProjectElevation` is always from the
    // project origin. The section box built below is a world-coordinate BoundingBoxXYZ whose X and Y come
    // from real element bounding boxes, so its Z has to come from the same space. Using `Elevation` on a
    // project set out to a survey datum puts every coordination view's box at the wrong height — the
    // views get made, they look right in the browser, and each one clips the wrong slice of the building.
    double bottom = lvl.ProjectElevation - ToFeet(belowLevelMm);

    // The next level UP in the full list, not in the filtered one — a filtered list would give the wrong
    // ceiling whenever levels are skipped.
    var above = levels.FirstOrDefault(l => l.ProjectElevation > lvl.ProjectElevation + 1e-6);
    double top = above != null ? above.ProjectElevation : lvl.ProjectElevation + ToFeet(topLevelHeightMm);

    plan.Add((lvl, name, bottom, top, existingNames.Contains(name)));
}

sb.AppendLine("MEP COORDINATION VIEWS");
sb.AppendLine($"3D view type: {vft3d.Name}");
sb.AppendLine($"Levels in project: {levels.Count}   selected: {chosen.Count}" + (string.IsNullOrWhiteSpace(levelNameContains) ? "" : $" (matching '{levelNameContains}')"));
sb.AppendLine($"Plan extent: {ToMm(maxX - minX):F0} x {ToMm(maxY - minY):F0} mm, including a {planMarginMm:F0} mm margin");
if (!string.IsNullOrWhiteSpace(viewTemplateName))
    sb.AppendLine(template != null
        ? $"View template: '{template.Name}' — will be applied"
        : $"VIEW TEMPLATE '{viewTemplateName}' NOT FOUND — views will be created WITHOUT a template and will show the wrong categories. Fix the name or create the template first.");
sb.AppendLine();

sb.AppendLine("| Level | View name | Box bottom mm | Box top mm | Height mm | |");
sb.AppendLine("|---|---|---|---|---|---|");
foreach (var p in plan)
    sb.AppendLine($"| {p.Lvl.Name} | {p.Name} | {ToMm(p.BottomFt):F0} | {ToMm(p.TopFt):F0} | {ToMm(p.TopFt - p.BottomFt):F0} | {(p.Exists ? "ALREADY EXISTS — will be skipped" : "")} |");

var toMake = plan.Where(p => !p.Exists).ToList();
sb.AppendLine();
sb.AppendLine($"TO CREATE: {toMake.Count}   already there: {plan.Count - toMake.Count}");

if (toMake.Count == 0)
{
    sb.AppendLine("Nothing to do — every view already exists.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing created. Check the names and box heights, then set dryRun = false.");
    return sb.ToString();
}

// ---- create ----
int made = 0;
var failures = new List<string>();
var madeIds = new List<ElementId>();

using (var tx = new Transaction(Document, "AJ Tools - create coordination views"))
{
    tx.Start();
    var opts = tx.GetFailureHandlingOptions();
    opts.SetForcedModalHandling(false);
    tx.SetFailureHandlingOptions(opts);
    try
    {
        foreach (var p in toMake)
        {
            try
            {
                var v = View3D.CreateIsometric(Document, vft3d.Id);
                if (v == null) { failures.Add($"{p.Name}: Revit returned no view"); continue; }

                // Name first — a clash fails the whole view, so find out before doing any more to it.
                try { v.Name = p.Name; }
                catch (Exception nameEx)
                {
                    failures.Add($"{p.Name}: name refused ({nameEx.Message}) — a view of ANY type may already use it");
                    Document.Delete(v.Id);
                    continue;
                }

                var box = new BoundingBoxXYZ();
                box.Min = new XYZ(minX, minY, p.BottomFt);
                box.Max = new XYZ(maxX, maxY, p.TopFt);
                v.SetSectionBox(box);
                try { v.IsSectionBoxActive = true; } catch { }

                if (detachCropBox)
                {
                    try { v.CropBoxActive = false; v.CropBoxVisible = false; } catch { }
                }

                if (template != null)
                {
                    // Applied LAST: a template can own the section-box setting, and applying it first
                    // would discard the box that was just set.
                    try { v.ViewTemplateId = template.Id; }
                    catch (Exception tEx) { failures.Add($"{p.Name}: created, but the view template would not apply ({tEx.Message})"); }
                }

                madeIds.Add(v.Id);
                made++;
            }
            catch (Exception ex)
            {
                failures.Add($"{p.Name}: {ex.Message}");
            }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (create coordination views) — rolled back, nothing created. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"CREATED: {made} of {toMake.Count} view(s).");
if (madeIds.Count > 0) sb.AppendLine("View Ids: " + string.Join(", ", madeIds.Select(i => i.ToString())));
if (failures.Count > 0)
{
    sb.AppendLine("NOT CREATED:");
    foreach (var f in failures) sb.AppendLine($"  {f}");
}
if (template == null && !string.IsNullOrWhiteSpace(viewTemplateName))
    sb.AppendLine("REMINDER: these have NO view template — they will show every category until one is applied (action-apply-view-template.cs).");
sb.AppendLine("Next: action-place-views-on-new-sheets.cs to get them onto sheets.");

return sb.ToString();
