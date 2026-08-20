// ============================================================
// FRAGMENT (action) — action-set-view-range.cs
// PURPOSE: Read or set a plan view's View Range — cut plane, top, bottom and view depth, each as a level
//          plus an mm offset. The fix for "my ducts/fixtures don't show in this plan" that has nothing to
//          do with visibility settings or filters.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — views are named directly, so pair it
//          with filter-by-views.cs output or a known view Id.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only ViewPlans (floor plans, ceiling plans, area plans) have a view range — sections, 3D views
//         and sheets are skipped and reported, not errors.
// GOTCHA: Revit enforces the order top >= cut >= bottom >= depth; an order-violating set throws and the
//         whole thing rolls back. Run mode="report" first and change one plane at a time.
// GOTCHA: on a CEILING plan the planes are measured looking UP — the same numbers behave differently
//         than on a floor plan. Report first, always.
// ✓ LIVE-VERIFIED (report mode) 2026-07-26 on Project1 — read 4 plan views incl. ceiling plans. Found and
//   fixed a real bug that run: Revit's sentinel level Ids (level above/below/unlimited) printed as raw
//   "Id -2"; now decoded by name. SET mode still unexercised.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "report";         // "report" | "set"
int[] viewIdInts = new int[] { };  // the plan view(s) to read or change
// set mode — null on any offset leaves that plane untouched; levelIdInt 0 = keep its current level
double? topOffsetMm = null;         int topLevelIdInt = 0;
double? cutOffsetMm = null;         int cutLevelIdInt = 0;
double? bottomOffsetMm = null;      int bottomLevelIdInt = 0;
double? depthOffsetMm = null;       int depthLevelIdInt = 0;
// ---- END INPUTS ----

Func<double, double> mm = v => v / 304.8;
Func<double, double> toMm = v => v * 304.8;

// Revit encodes three special planes as SENTINEL ElementIds, not real levels — decode them by name or
// the report prints a meaningless "Id -2" (found live 2026-07-26 on a ceiling plan).
Func<ElementId, string> levelName = id =>
{
    if (id == null || id == ElementId.InvalidElementId) return "(unlimited)";
    if (id == PlanViewRange.Unlimited) return "(unlimited)";
    if (id == PlanViewRange.LevelAbove) return "(level above)";
    if (id == PlanViewRange.LevelBelow) return "(level below)";
    return Document.GetElement(id)?.Name ?? $"Id {id}";
};

var views = viewIdInts.Select(i => Document.GetElement(new ElementId(i))).OfType<ViewPlan>()
    .Where(v => !v.IsTemplate).ToList();
int notPlan = viewIdInts.Length - views.Count;

if (views.Count == 0)
{
    sb.AppendLine($"No usable plan views among the {viewIdInts.Length} Id(s) given — view ranges exist only on ViewPlans.");
}
else if (mode == "report")
{
    foreach (var v in views)
    {
        var r = v.GetViewRange();
        sb.AppendLine($"- '{v.Name}' (Id {v.Id}, {v.ViewType}):");
        sb.AppendLine($"    Top:    {levelName(r.GetLevelId(PlanViewPlane.TopClipPlane))} {toMm(r.GetOffset(PlanViewPlane.TopClipPlane)):+0;-0;0} mm");
        sb.AppendLine($"    Cut:    {levelName(r.GetLevelId(PlanViewPlane.CutPlane))} {toMm(r.GetOffset(PlanViewPlane.CutPlane)):+0;-0;0} mm");
        sb.AppendLine($"    Bottom: {levelName(r.GetLevelId(PlanViewPlane.BottomClipPlane))} {toMm(r.GetOffset(PlanViewPlane.BottomClipPlane)):+0;-0;0} mm");
        sb.AppendLine($"    Depth:  {levelName(r.GetLevelId(PlanViewPlane.ViewDepthPlane))} {toMm(r.GetOffset(PlanViewPlane.ViewDepthPlane)):+0;-0;0} mm");
    }
    sb.AppendLine($"Reported {views.Count} plan view(s){(notPlan > 0 ? $"; {notPlan} given Id(s) were not plan views" : "")}.");
}
else if (mode == "set")
{
    using (var t = new Transaction(Document, "AJ Tools - Set View Range"))
    {
        t.Start();
        try
        {
            int changed = 0;
            foreach (var v in views)
            {
                var r = v.GetViewRange();
                Action<PlanViewPlane, double?, int> apply = (plane, offMm, lvlId) =>
                {
                    if (lvlId != 0) r.SetLevelId(plane, new ElementId(lvlId));
                    if (offMm.HasValue) r.SetOffset(plane, mm(offMm.Value));
                };
                apply(PlanViewPlane.TopClipPlane, topOffsetMm, topLevelIdInt);
                apply(PlanViewPlane.CutPlane, cutOffsetMm, cutLevelIdInt);
                apply(PlanViewPlane.BottomClipPlane, bottomOffsetMm, bottomLevelIdInt);
                apply(PlanViewPlane.ViewDepthPlane, depthOffsetMm, depthLevelIdInt);

                v.SetViewRange(r); // throws if the plane order is invalid — caught below, whole set rolls back
                changed++;
                sb.AppendLine($"  - '{v.Name}' (Id {v.Id}) updated.");
            }
            t.Commit();
            sb.AppendLine($"View range set on {changed} plan view(s){(notPlan > 0 ? $"; {notPlan} given Id(s) were not plan views" : "")}.");
            sb.AppendLine("Re-run mode=\"report\" to confirm the values landed as intended.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to set view range — rolled back, no view changed. Reason: {ex.Message}");
            sb.AppendLine("Most likely cause: the resulting plane order broke top >= cut >= bottom >= depth (see header).");
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"report\" or \"set\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
