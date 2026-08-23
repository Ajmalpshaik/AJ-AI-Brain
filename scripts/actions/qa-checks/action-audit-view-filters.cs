// ============================================================
// FRAGMENT (action) — action-audit-view-filters.cs
// PURPOSE: Answer "the filter is on the view, so why is nothing coloured?" — reads every filter on a
//          view and reports the four separate states that each have to be right before a filter draws
//          anything, naming the one that is wrong.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only unless repairDisabled is turned on.
//
// WHY THIS EXISTS — a filter can be present and still do nothing, in four different ways, and Revit
//   reports none of them as an error:
//
//   1. APPLIED BUT DISABLED. `SetIsFilterEnabled(id, false)` leaves the filter listed in the view's
//      filter list, greyed, doing nothing. This is the one that wastes an afternoon: the filter is
//      visibly there. `IsFilterEnabled` is the only way to see it from code, and it appeared in no
//      fragment in this library.
//   2. VISIBILITY OFF. `SetFilterVisibility(id, false)` HIDES every element the filter matches. That
//      is a different switch from the overrides and from "enabled" — same list, three controls.
//   3. ITS CATEGORIES ARE SWITCHED OFF in this view. A filter only ever acts on elements the view is
//      already drawing. If every category the filter names is hidden here, the filter is correct and
//      matches nothing. Read with `View.GetCategoryHidden` over `ParameterFilterElement.GetCategories`.
//   4. A VIEW TEMPLATE OWNS THE FILTERS. Anything set here is discarded the moment the template
//      reasserts — the usual answer to "my change did not save". Read by asking the view which
//      template parameters it does NOT control (`GetNonControlledTemplateParameterIds`) and checking
//      whether the V/G Filters parameter is in that list.
//
// ✱✱ A NOTE ON WHAT IS *NOT* HERE, because it looks like it should be: Revit has no
//    `View.IsFilterApplicable`, no `View.CanApplyFilterOverrides` and no
//    `ParameterFilterElement.IsNameUnique`. Those names read as if they exist and do not, on any Revit
//    installed here — checked against the shipped RevitAPI.dll, 2020 and 2024. Checks 3 and 4 above
//    are built from members that are really there.
//
// ALSO REPORTED: filters whose override is EMPTY. `AddFilter` alone applies a blank override, so a
//   filter copied or added without one is present, enabled, applicable — and visually identical to
//   having no filter. (Same trap action-copy-view-filters.cs was written to avoid.)
//
// RELATED: actions/color-graphics/action-apply-view-filter.cs (add one and set its override),
//          actions/color-graphics/action-copy-view-filters.cs (move them between views),
//          actions/color-graphics/action-create-view-filters-by-value.cs (make one per value),
//          actions/reporting/action-report-category-overrides.cs (the category-level sibling).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only by default. repairDisabled = true opens a Transaction and
//    switches disabled filters back on — nothing else is touched.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null;      // null = active view; or an Element Id to audit any view or template
bool onlyProblems = true;         // true = list only the filters that are not doing what they look like
bool repairDisabled = false;      // true = switch every DISABLED filter back on (nothing else changed)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
    return sb.ToString();
}

// A view that cannot carry overrides at all — schedules, sheets — makes every answer below moot, and
// saying so is more useful than reporting "0 filters".
bool overridesAllowed = true;
try { overridesAllowed = view.AreGraphicsOverridesAllowed(); } catch { }
if (!overridesAllowed)
{
    sb.AppendLine($"View '{view.Name}' ({view.ViewType}) does not accept graphic overrides at all — filters cannot apply here. Nothing to audit.");
    return sb.ToString();
}

List<ElementId> filterIds;
try { filterIds = view.GetFilters().ToList(); }
catch (Exception ex)
{
    sb.AppendLine($"Could not read the filters on '{view.Name}': {ex.Message}");
    return sb.ToString();
}

// A view driven by a template does not own its filters. Worth saying once at the top, because it
// explains every "editable = no" line underneath in one go.
string templateNote = "";
try
{
    if (!view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId)
    {
        var tpl = Document.GetElement(view.ViewTemplateId) as View;
        if (tpl != null) templateNote = tpl.Name;
    }
}
catch { }

// ---- VERSION-PROOF STATE PROBE -------------------------------------------------------------------
// The enabled/disabled switch does not exist on Revit 2020 — that view has AddFilter, RemoveFilter,
// GetFilterVisibility and the overrides, and nothing else. Naming `GetIsFilterEnabled` directly would
// stop this fragment compiling there, and a try/catch does NOT help, because the file never gets as
// far as running. Looked up by name once, here, so one source serves Revit 2020 through 2027.
var _mIsEnabled  = typeof(View).GetMethod("GetIsFilterEnabled", new[] { typeof(ElementId) })
                ?? typeof(View).GetMethod("IsFilterEnabled",    new[] { typeof(ElementId) });
var _mSetEnabled = typeof(View).GetMethod("SetIsFilterEnabled", new[] { typeof(ElementId), typeof(bool) });

// Which template parameters this view still controls itself. Everything NOT in this list is owned by
// the template — including the V/G Filters slot, when a template is assigned.
var selfControlled = new HashSet<ElementId>();
bool templateOwnsFilters = false;
try
{
    if (!view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId)
    {
        foreach (var pid in view.GetNonControlledTemplateParameterIds()) selfControlled.Add(pid);
        var filtersParam = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
        templateOwnsFilters = !selfControlled.Contains(filtersParam);
    }
}
catch { }

sb.AppendLine($"VIEW FILTER AUDIT — '{view.Name}' ({view.ViewType}), {filterIds.Count} filter(s).");
if (_mIsEnabled == null)
    sb.AppendLine("  NOTE — this Revit version has no enabled/disabled switch for filters (it arrived after 2020). That check is skipped, not passed.");
if (!string.IsNullOrEmpty(templateNote))
    sb.AppendLine($"  This view is driven by view template '{templateNote}'. Filters it controls cannot be edited here — change them ON THE TEMPLATE.");

if (filterIds.Count == 0)
{
    sb.AppendLine("  No filters on this view.");
    return sb.ToString();
}

int problems = 0, disabled = 0, hidden = 0, notApplicable = 0, notEditable = 0, emptyOverride = 0;
var lines = new List<string>();
var toEnable = new List<ElementId>();

foreach (var fid in filterIds)
{
    string name;
    try
    {
        var fe = Document.GetElement(fid);
        name = fe != null ? (fe.Name ?? "(unnamed)") : $"(missing filter {fid})";
    }
    catch { name = $"(unreadable filter {fid})"; }

    bool isEnabled = true, isVisible = true, isApplicable = true, isEditable = true;
    bool hasOverride = false;
    var faults = new List<string>();

    // Enabled: reflected, because the switch does not exist before Revit 2021. A version that cannot
    // be asked reports "enabled" — fewer fault kinds seen, never a fault invented.
    if (_mIsEnabled != null)
    {
        try { isEnabled = Convert.ToBoolean(_mIsEnabled.Invoke(view, new object[] { fid })); }
        catch { isEnabled = true; }
    }

    // Visibility: on every version.
    try { isVisible = view.GetFilterVisibility(fid); } catch { }

    // "Applicable" built from what really exists: a filter can only act on elements the view draws, so
    // if EVERY category it names is hidden here, it matches nothing however correct the rule is.
    try
    {
        var pfe = Document.GetElement(fid) as ParameterFilterElement;
        if (pfe != null)
        {
            var cats = pfe.GetCategories();
            if (cats != null && cats.Count > 0)
            {
                bool anyShown = false;
                foreach (var cid in cats)
                {
                    try { if (!view.GetCategoryHidden(cid)) { anyShown = true; break; } }
                    catch { anyShown = true; break; }   // cannot tell — do not accuse it
                }
                isApplicable = anyShown;
            }
        }
    }
    catch { }

    // "Editable": the view template owns the V/G Filters slot, worked out once above the loop.
    isEditable = !templateOwnsFilters;

    // "Empty" means every slot is unset — that is exactly what AddFilter leaves behind on its own.
    try
    {
        var ogs = view.GetFilterOverrides(fid);
        if (ogs != null)
        {
            hasOverride =
                   ogs.ProjectionLineColor.IsValid
                || ogs.CutLineColor.IsValid
                || ogs.SurfaceForegroundPatternColor.IsValid
                || ogs.CutForegroundPatternColor.IsValid
                || ogs.SurfaceBackgroundPatternColor.IsValid
                || ogs.CutBackgroundPatternColor.IsValid
                || ogs.Halftone
                // The SETTER is SetSurfaceTransparency; the GETTER is plain `Transparency`. The names
                // do not match, and reaching for the symmetrical one is a compile error, not a null.
                || ogs.Transparency > 0
                || ogs.ProjectionLineWeight > 0
                || ogs.CutLineWeight > 0
                || ogs.SurfaceForegroundPatternId != ElementId.InvalidElementId
                || ogs.CutForegroundPatternId != ElementId.InvalidElementId;
        }
    }
    catch { hasOverride = true; }   // could not read it — do not accuse it of being empty

    if (!isEnabled) { faults.Add("DISABLED — listed on the view but switched off, so it draws nothing"); disabled++; toEnable.Add(fid); }
    if (!isVisible) { faults.Add("VISIBILITY OFF — every element this filter matches is HIDDEN in this view"); hidden++; }
    if (!isApplicable) { faults.Add("EVERY CATEGORY IT NAMES IS HIDDEN in this view — the rule may be perfect and it still matches nothing here"); notApplicable++; }
    if (!isEditable) { faults.Add("OVERRIDES LOCKED by a view template — edits here are discarded"); notEditable++; }
    if (!hasOverride && isVisible) { faults.Add("EMPTY OVERRIDE — nothing is actually set, so the view looks unchanged"); emptyOverride++; }

    if (faults.Count > 0) problems++;

    if (faults.Count > 0 || !onlyProblems)
    {
        lines.Add($"  {name}");
        foreach (var f in faults) lines.Add($"      ! {f}");
        if (faults.Count == 0) lines.Add("      ok — enabled, visible, applicable, editable, override set");
    }
}

foreach (var l in lines) sb.AppendLine(l);

sb.AppendLine();
if (problems == 0)
    sb.AppendLine("All filters on this view are enabled, visible, applicable, editable and carry a real override.");
else
{
    sb.AppendLine($"{problems} of {filterIds.Count} filter(s) are not doing what the filter list suggests:");
    if (disabled > 0)      sb.AppendLine($"  {disabled} disabled        -> set repairDisabled = true, or tick them in Visibility/Graphics");
    if (hidden > 0)        sb.AppendLine($"  {hidden} hiding elements -> tick the Visibility box if you meant to colour them, not hide them");
    if (notApplicable > 0) sb.AppendLine($"  {notApplicable} categories hidden -> switch the filter's categories back on in this view's V/G");
    if (notEditable > 0)   sb.AppendLine($"  {notEditable} template-locked -> change them on the view template instead");
    if (emptyOverride > 0) sb.AppendLine($"  {emptyOverride} empty override  -> use action-apply-view-filter.cs to give them a colour");
}

if (repairDisabled && toEnable.Count > 0)
{
    using (var t = new Transaction(Document, "AJ Tools - Enable View Filters"))
    {
        t.Start();
        try
        {
            int fixedCount = 0;
            foreach (var fid in toEnable)
            {
                if (_mSetEnabled == null) break;
                try { _mSetEnabled.Invoke(view, new object[] { fid, true }); fixedCount++; } catch { }
            }
            if (_mSetEnabled == null)
                sb.AppendLine("This Revit version has no SetIsFilterEnabled — re-enable them in Visibility/Graphics by hand.");
            t.Commit();
            sb.AppendLine();
            sb.AppendLine($"REPAIRED — switched {fixedCount} disabled filter(s) back on. Nothing else was changed.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine();
            sb.AppendLine($"FAILED to re-enable filters — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
else if (repairDisabled)
{
    sb.AppendLine();
    sb.AppendLine("repairDisabled was on, but no disabled filters were found — nothing to repair.");
}

return sb.ToString();
