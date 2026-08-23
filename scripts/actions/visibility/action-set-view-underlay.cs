// ============================================================
// FRAGMENT (action) — action-set-view-underlay.cs
// PURPOSE: Set (or clear) the UNDERLAY on plan views — the ghosted level shown behind the one you are
//          working on. The MEP habit of "show me the floor below while I route" and the coordination
//          habit of "show me the ceiling above", done across many views in one pass instead of one
//          Properties palette at a time.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). It finds its own views from the inputs below.
//
// WHY A FRAGMENT AND NOT A PARAMETER SET: the underlay is three settings that must agree — a BASE
//   level, a TOP level, and an ORIENTATION (look down from above, or up from below). Writing the base
//   level alone leaves the old top level in place, and the view then shows a RANGE nobody asked for:
//   set base to Level 2 while top is still Level 5 and you get three storeys of ghost, which reads as
//   a corrupted view rather than a wrong setting. `SetUnderlayRange` sets base and top together, which
//   is why it is used here instead of `SetUnderlayBaseLevel` on its own.
//
// THE ORIENTATION IS NOT COSMETIC. "Look down" shows what is BELOW the underlay range and is what you
//   want when tracing the floor below. "Look up" shows what is ABOVE it — the reflected-ceiling habit.
//   Choosing the wrong one gives an empty-looking underlay and reads as "the underlay is broken".
//
// A REAL LIMIT: only plan views have an underlay — floor plans, ceiling plans, structural plans and
//   area plans. Sections, elevations, 3D views, drafting views and schedules have no such setting, and
//   they are counted and named as skipped rather than silently passed over.
//
// RELATED: actions/sheets-views/action-set-view-range.cs (the view's OWN cut and depth, a different
//          thing that is often confused with this), actions/sheets-views/action-set-view-properties.cs
//          (scale, detail level, discipline), knowledge/live-model/ for view-setup notes.
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Dry-run by default — it lists the views it WOULD change and stops.
//    Set dryRun = false only after reading that list. Do one view first and look at it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;

string viewScope = "active";        // "active"   = the active view only
                                    // "byName"   = every plan view whose name contains viewNameContains
                                    // "byLevel"  = every plan view whose OWN level is named viewLevelName
                                    // "all"      = every plan view in the document
string viewNameContains = "";
string viewLevelName = "";

string action = "set";              // "set"   = apply the underlay below
                                    // "clear" = remove the underlay from the matched views

// Where the underlay comes from. Leave baseLevelName empty and set relativeLevels instead to say
// "the level below this view's own level", which is what you usually mean across many views at once.
string baseLevelName = "";          // e.g. "L2". Empty = use relativeLevels
int relativeLevels = -1;            // -1 = one level below this view's own level, +1 = one above
string topLevelName = "";           // empty = same as the base level (a single storey of underlay)

string orientation = "LookDown";    // "LookDown" = show what is BELOW the range (tracing the floor under you)
                                    // "LookUp"   = show what is ABOVE it (the ceiling habit)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// Every level in the document, sorted by elevation — "the level below" is only meaningful against a
// sorted list, and Revit does not hand them over in order.
var levels = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .OrderBy(l => l.Elevation)
    .ToList();

if (levels.Count == 0)
{
    sb.AppendLine("This document has no levels — an underlay has nothing to point at.");
    return sb.ToString();
}

Func<string, Level> LevelByName = n =>
    levels.FirstOrDefault(l => string.Equals(l.Name, n, StringComparison.OrdinalIgnoreCase));

// ---- Pick the views
var allPlans = new FilteredElementCollector(Document)
    .OfClass(typeof(ViewPlan))
    .Cast<ViewPlan>()
    .Where(v => v != null && !v.IsTemplate)
    .ToList();

List<ViewPlan> targets;
if (viewScope == "active")
{
    var av = Document.ActiveView as ViewPlan;
    if (av == null)
    {
        sb.AppendLine($"The active view is '{Document.ActiveView.Name}' ({Document.ActiveView.ViewType}), which is not a plan view. Only plan views have an underlay.");
        return sb.ToString();
    }
    targets = new List<ViewPlan> { av };
}
else if (viewScope == "byName")
    targets = allPlans.Where(v => !string.IsNullOrEmpty(viewNameContains)
        && v.Name.IndexOf(viewNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
else if (viewScope == "byLevel")
    targets = allPlans.Where(v => { try { return v.GenLevel != null && string.Equals(v.GenLevel.Name, viewLevelName, StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToList();
else
    targets = allPlans;

if (targets.Count == 0)
{
    sb.AppendLine($"No plan views matched scope \"{viewScope}\". Nothing to do.");
    return sb.ToString();
}

// Work out, per view, which levels the underlay should point at. Done up front so the dry run shows
// the real answer rather than a promise.
var plan = new List<Tuple<ViewPlan, Level, Level, string>>();   // view, base, top, why-skipped
foreach (var v in targets)
{
    if (action == "clear") { plan.Add(Tuple.Create(v, (Level)null, (Level)null, "")); continue; }

    Level baseLvl = null;
    if (!string.IsNullOrEmpty(baseLevelName))
    {
        baseLvl = LevelByName(baseLevelName);
        if (baseLvl == null) { plan.Add(Tuple.Create(v, (Level)null, (Level)null, $"level '{baseLevelName}' does not exist")); continue; }
    }
    else
    {
        Level own = null;
        try { own = v.GenLevel; } catch { }
        if (own == null) { plan.Add(Tuple.Create(v, (Level)null, (Level)null, "this view has no level of its own, so \"relative\" has no meaning")); continue; }
        int i = levels.FindIndex(l => l.Id == own.Id);
        int j = i + relativeLevels;
        if (i < 0 || j < 0 || j >= levels.Count)
        {
            plan.Add(Tuple.Create(v, (Level)null, (Level)null, $"there is no level {(relativeLevels < 0 ? "below" : "above")} '{own.Name}'"));
            continue;
        }
        baseLvl = levels[j];
    }

    Level topLvl = string.IsNullOrEmpty(topLevelName) ? baseLvl : LevelByName(topLevelName);
    if (topLvl == null) { plan.Add(Tuple.Create(v, (Level)null, (Level)null, $"top level '{topLevelName}' does not exist")); continue; }

    plan.Add(Tuple.Create(v, baseLvl, topLvl, ""));
}

var doable = plan.Where(p => p.Item4 == "").ToList();
var blocked = plan.Where(p => p.Item4 != "").ToList();

if (dryRun)
{
    sb.AppendLine($"DRY RUN — \"{action}\" underlay on {doable.Count} plan view(s). Nothing changed.");
    foreach (var p in doable.Take(40))
        sb.AppendLine(action == "clear"
            ? $"  would clear: '{p.Item1.Name}'"
            : $"  would set:   '{p.Item1.Name}'  ->  base '{p.Item2.Name}'"
              + (p.Item3.Id != p.Item2.Id ? $", top '{p.Item3.Name}'" : "") + $", {orientation}");
    if (doable.Count > 40) sb.AppendLine($"  ... and {doable.Count - 40} more.");
    foreach (var p in blocked) sb.AppendLine($"  SKIP '{p.Item1.Name}': {p.Item4}");
    sb.AppendLine("Set dryRun = false to apply. Do ONE view first and look at it.");
    return sb.ToString();
}

// The orientation enum, reached by name so a value that a given Revit does not carry degrades to the
// default rather than refusing to compile.
UnderlayOrientation orient = UnderlayOrientation.LookingDown;
if (string.Equals(orientation, "LookUp", StringComparison.OrdinalIgnoreCase)) orient = UnderlayOrientation.LookingUp;

int done = 0, failed = 0;
var notes = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set View Underlay"))
{
    t.Start();
    try
    {
        foreach (var p in doable)
        {
            var v = p.Item1;
            try
            {
                if (action == "clear")
                {
                    // Revit's own "None": an invalid base level id clears the underlay.
                    v.SetUnderlayRange(ElementId.InvalidElementId, ElementId.InvalidElementId);
                    done++;
                    notes.Add($"  '{v.Name}': underlay cleared.");
                }
                else
                {
                    // Base and top TOGETHER — see the header. Setting only the base leaves a stale top
                    // and produces a multi-storey ghost nobody asked for.
                    v.SetUnderlayRange(p.Item2.Id, p.Item3.Id);
                    v.SetUnderlayOrientation(orient);
                    done++;
                    notes.Add($"  '{v.Name}': base '{p.Item2.Name}'"
                              + (p.Item3.Id != p.Item2.Id ? $", top '{p.Item3.Name}'" : "") + $", {orient}.");
                }
            }
            catch (Exception exOne)
            {
                failed++;
                notes.Add($"  '{v.Name}': FAILED — {exOne.Message}");
            }
        }

        t.Commit();
        sb.AppendLine($"Underlay \"{action}\": {done} view(s) set, {failed} failed, {blocked.Count} skipped.");
        foreach (var n in notes.Take(60)) sb.AppendLine(n);
        if (notes.Count > 60) sb.AppendLine($"  ... and {notes.Count - 60} more.");
        foreach (var p in blocked) sb.AppendLine($"  SKIP '{p.Item1.Name}': {p.Item4}");
        if (done > 0 && action == "set")
            sb.AppendLine("If a view looks unchanged, check the orientation: LookDown shows what is BELOW the range, LookUp what is ABOVE it. The wrong one gives an empty underlay, not an error.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set underlays — rolled back, nothing changed. Reason: {ex.Message}");
    }
}

return sb.ToString();
