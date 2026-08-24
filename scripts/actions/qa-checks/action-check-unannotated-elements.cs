// ============================================================
// FRAGMENT (action) — action-check-unannotated-elements.cs
// PURPOSE: Sweep a whole view and report, PER CATEGORY, how much of what is visible carries a tag and
//          what does not — the "is this drawing actually finished" check before a sheet goes out. Gives
//          the element Ids of what is missing, so the gap can be closed rather than just counted.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it reads everything VISIBLE in one
//          view, because "should this have been tagged" is a question about the view, not about a set
//          somebody already filtered. Read-only.
//
// ✱✱ WHAT THIS ADDS OVER filter-by-tag-status.cs. That filter answers "which elements of ONE category are
//    tagged in this view" and hands you an actionable set — it is the right tool when you already know
//    which category you are chasing. This one sweeps EVERY MEP category at once and reports the
//    percentages side by side, which is the form the question takes before an issue: not "are the ducts
//    tagged" but "what have we missed". Different question, different output.
//
// ✱✱ VISIBILITY IS THE WHOLE POINT AND IT IS DONE PROPERLY. A FilteredElementCollector scoped to a view
//    returns what is VISIBLE in it — filters, view range, crop and category visibility all applied. That
//    is why this counts what a person would see on the sheet rather than what exists in the model, and it
//    is the difference between a useful number and a frightening one.
//
// ✱✱ "TAGGED" MEANS TAGGED IN THIS VIEW. Tags are view-specific: an element tagged on the ceiling plan is
//    genuinely untagged in the section, and reporting it as tagged would hide a real gap. Existing tags
//    are read from this view only.
//
// GOTCHA: NOT EVERYTHING SHOULD BE TAGGED, and a blind sweep produces a huge, useless number. Fittings
//         and accessories are usually not tagged individually; a duct run is often tagged once, not once
//         per segment. `categoriesToCheck` is an explicit list for that reason — put in what your
//         drawing standard actually requires tagged, and the percentages become meaningful.
// GOTCHA: TAG COUNTS CAN EXCEED ELEMENT COUNTS. One element can carry several tags, and one tag (2022 and
//         later) can point at several elements. The report counts DISTINCT TAGGED ELEMENTS, so a
//         double-tagged duct counts once — and the number of tags is reported separately so a big gap
//         between the two shows up double-tagging.
// GOTCHA: THE READ IS VERSION-PROOF ACROSS 2020-2027. `TaggedElementId` was replaced by
//         `GetTaggedElementIds()` at Revit 2022 and the old name REMOVED, so naming either directly fails
//         to compile on the other. Both are looked up by name once, here.
// RELATED: filter-by-tag-status.cs (one category, as an actionable set), action-auto-tag-mep.cs (place
//          the missing tags), action-check-annotation-overlap.cs (the opposite problem),
//          action-report-tags-and-targets.cs (what each tag actually points at, links included).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one category's count against what you
//   can see on the view before trusting the whole table.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId viewId = null;          // null = the active view

// What your drawing standard requires tagged. Fittings are deliberately absent — add them if yours differ.
var categoriesToCheck = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_DuctTerminal,
    BuiltInCategory.OST_MechanicalEquipment,
    BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_PlumbingFixtures,
    BuiltInCategory.OST_Sprinklers,
    BuiltInCategory.OST_CableTray,
    BuiltInCategory.OST_Conduit,
    BuiltInCategory.OST_ElectricalEquipment,
    BuiltInCategory.OST_LightingFixtures,
};

bool listUntaggedIds = true;      // print the Ids of what is missing, not just the count
int maxIdsPerCategory = 20;
double minLengthMm = 0;           // ignore linear elements shorter than this (0 = count them all)
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

var view = viewId != null ? Document.GetElement(viewId) as View : Document.ActiveView;
if (view == null) { sb.AppendLine("STOP: no view resolved."); return sb.ToString(); }
if (view.IsTemplate) { sb.AppendLine($"STOP: '{view.Name}' is a view template — nothing is visible in one."); return sb.ToString(); }

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- VERSION-PROOF TAG READ (2020 through 2027) ----
// `TaggedElementId` was replaced by `GetTaggedElementIds()` at 2022 and the old name removed, so this
// cannot be a direct call on either name. Looked up once, here.
var _mAllIds = typeof(IndependentTag).GetMethod("GetTaggedElementIds", Type.EmptyTypes);   // 2022+
var _pAllId  = typeof(IndependentTag).GetProperty("TaggedElementId");                      // pre-2022

if (_mAllIds == null && _pAllId == null)
{
    sb.AppendLine("This Revit exposes neither GetTaggedElementIds nor TaggedElementId — tag targets cannot be read here.");
    return sb.ToString();
}

Func<object, ElementId> hostIdOf = o =>
{
    var lei = o as LinkElementId;
    if (lei == null) return ElementId.InvalidElementId;
    if (lei.LinkInstanceId != ElementId.InvalidElementId) return ElementId.InvalidElementId;  // target is in a link
    return lei.HostElementId;
};

var tags = new FilteredElementCollector(Document, view.Id)
    .OfClass(typeof(IndependentTag)).Cast<IndependentTag>().ToList();

var taggedIds = new HashSet<long>();
int tagTargetsInLinks = 0;
foreach (var tg in tags)
{
    try
    {
        if (_mAllIds != null)
        {
            var raw = _mAllIds.Invoke(tg, null) as System.Collections.IEnumerable;
            if (raw != null) foreach (var o in raw)
            {
                var id = hostIdOf(o);
                if (id != ElementId.InvalidElementId) taggedIds.Add(IdValue(id));
                else tagTargetsInLinks++;
            }
        }
        else
        {
            var id = hostIdOf(_pAllId.GetValue(tg));
            if (id != ElementId.InvalidElementId) taggedIds.Add(IdValue(id));
            else tagTargetsInLinks++;
        }
    }
    catch { }
}

sb.AppendLine($"UNANNOTATED SWEEP — view '{view.Name}' (1:{(view.Scale <= 0 ? 100 : view.Scale)})");
sb.AppendLine($"Tags in this view: {tags.Count}, pointing at {taggedIds.Count} distinct host element(s)" +
              (tagTargetsInLinks > 0 ? $"   ({tagTargetsInLinks} tag target(s) live in a LINK and are not counted here)" : ""));
if (tags.Count > taggedIds.Count + tagTargetsInLinks)
    sb.AppendLine($"NOTE: more tags than tagged elements — {tags.Count - taggedIds.Count - tagTargetsInLinks} element(s) appear to carry more than one tag.");
sb.AppendLine();

// ---- per category ----
Func<Element, double> lengthMmOf = el =>
{
    var p = el.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
    if (p != null && p.HasValue) return ToMm(p.AsDouble());
    var lc = el.Location as LocationCurve;
    if (lc != null && lc.Curve != null) return ToMm(lc.Curve.Length);
    return -1;   // not a linear element
};

var rows = new List<(string Cat, int Visible, int Tagged, int Untagged, double Pct, List<ElementId> Missing)>();
int grandVisible = 0, grandTagged = 0;

foreach (var cat in categoriesToCheck)
{
    List<Element> visible;
    try
    {
        visible = new FilteredElementCollector(Document, view.Id)
            .OfCategory(cat).WhereElementIsNotElementType().ToList();
    }
    catch { continue; }

    if (minLengthMm > 0)
        visible = visible.Where(e => { double L = lengthMmOf(e); return L < 0 || L >= minLengthMm; }).ToList();

    if (visible.Count == 0) continue;

    var missing = new List<ElementId>();
    int tagged = 0;
    foreach (var e in visible)
    {
        if (taggedIds.Contains(IdValue(e.Id))) tagged++;
        else missing.Add(e.Id);
    }

    string catName = visible[0].Category != null ? visible[0].Category.Name : cat.ToString();
    double pct = visible.Count > 0 ? (tagged * 100.0 / visible.Count) : 0;
    rows.Add((catName, visible.Count, tagged, missing.Count, pct, missing));

    grandVisible += visible.Count;
    grandTagged += tagged;
}

if (rows.Count == 0)
{
    sb.AppendLine("None of the categories in categoriesToCheck is visible in this view — nothing to report.");
    return sb.ToString();
}

sb.AppendLine("| Category | Visible | Tagged | Untagged | Tagged % |");
sb.AppendLine("|---|---|---|---|---|");
foreach (var r in rows.OrderBy(r => r.Pct))
    sb.AppendLine($"| {r.Cat} | {r.Visible} | {r.Tagged} | {r.Untagged} | {r.Pct:F0}% |");

double grandPct = grandVisible > 0 ? grandTagged * 100.0 / grandVisible : 0;
sb.AppendLine($"| **TOTAL** | **{grandVisible}** | **{grandTagged}** | **{grandVisible - grandTagged}** | **{grandPct:F0}%** |");

sb.AppendLine();
if (grandVisible == grandTagged)
{
    sb.AppendLine("COMPLETE — everything visible in the checked categories carries a tag.");
    return sb.ToString();
}

if (listUntaggedIds)
{
    sb.AppendLine("UNTAGGED ELEMENT IDs:");
    foreach (var r in rows.Where(r => r.Untagged > 0).OrderByDescending(r => r.Untagged))
    {
        sb.AppendLine($"  {r.Cat} ({r.Untagged}):");
        sb.AppendLine("    " + string.Join(", ", r.Missing.Take(maxIdsPerCategory).Select(i => i.ToString())) +
                      (r.Missing.Count > maxIdsPerCategory ? $" ... and {r.Missing.Count - maxIdsPerCategory} more" : ""));
    }
    sb.AppendLine();
}

sb.AppendLine("To close the gap: filter-by-tag-status.cs gives one category's untagged set as an actionable list, and action-auto-tag-mep.cs places the tags.");

return sb.ToString();
