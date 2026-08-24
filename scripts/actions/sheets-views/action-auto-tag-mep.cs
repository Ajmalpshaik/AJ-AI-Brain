// ============================================================
// FRAGMENT (action) — action-auto-tag-mep.cs
// PURPOSE: Tag a whole MIXED set of MEP elements in one pass, picking the right tag family for each
//          category automatically — ducts get a duct tag, pipes get a pipe tag, terminals get a terminal
//          tag, equipment gets an equipment tag. The step that turns "tag everything on this view" from
//          fifteen separate runs into one, and skips whatever is already tagged.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — a mixed
//          set is the point, e.g. filter-by-elements-in-view.cs, or filter-by-multiple-categories.cs.
//
// ✱✱ WHAT THIS ADDS OVER THE TWO TAG FRAGMENTS ALREADY HERE. action-tag-elements.cs tags ONE category
//    with ONE tag type at a fixed offset. recipes/tag-elements-in-active-view.cs is the good one for a
//    congested view — it SCORES candidate positions and picks the clearest side, and it is the right tool
//    when placement quality matters. Neither handles a mixed set, because both need to be told which tag
//    family to use, and that answer is different per category. This one carries the CATEGORY -> TAG
//    CATEGORY map, which is a fixed fact about Revit and cannot be derived from the API at run time.
//
// ✱✱ IT NEVER DOUBLE-TAGS. Every existing tag in the view is read first and its targets recorded, so an
//    element that already carries a tag is skipped. Running this twice on the same view is safe and the
//    second run reports "already tagged" rather than stacking a second tag on top — which is the failure
//    that makes a drawing look right and print wrong.
//
// ✱✱ TAG FAMILY COMES FROM REVIT'S OWN DEFAULT, NOT FROM "THE FIRST ONE LOADED".
//    `Document.GetDefaultFamilyTypeId(Category.GetCategory(doc, OST_..Tags).Id)` returns exactly what
//    "Tag by Category" would use — i.e. whatever tag family THIS project standardised on, which is
//    usually not the generic Autodesk one. Taking the first loaded symbol is the guess Ajmal corrected on
//    a live job, and it produces the wrong tag family silently on every element. `ElementTypeGroup` has
//    NO per-MEP-category tag entries (no `DuctTagType` — the whole enum was checked), so the
//    Category-based lookup is the only route. Order: your hint, then the project default, then first
//    loaded — and the report SAYS which of the three it used, so a guess is never invisible.
//
// ✱✱ `IndependentTag.Create` DOES NOT RELIABLY HONOUR THE TYPE YOU PASS IT — and it throws nothing.
//    Measured in this Brain: 38 tags created with an explicit symId all came out as the document's own
//    default type. It was harmless only because the two ids happened to match after a fix. So every tag
//    is checked with `GetTypeId()` immediately after creation and corrected with `ChangeTypeId`; the
//    count of corrections is reported rather than hidden.
//
// GOTCHA: DRY RUN BY DEFAULT — the plan prints per category first. Read it, then set dryRun = false.
// GOTCHA: TAGS ARE VIEW-SPECIFIC. This tags in ONE view. An element tagged in the ceiling plan is
//         untagged in the section, and that is correct Revit behaviour, not a miss.
// GOTCHA: A CATEGORY WITH NO TAG FAMILY LOADED CANNOT BE TAGGED, and there is nothing this can do about
//         it — the tag family has to be loaded into the project first (creators/load-family.cs). Those
//         categories are named in the report with their element counts, so the gap is explicit rather
//         than showing up as a lower number than expected.
// GOTCHA: PLACEMENT IS SIMPLE HERE — the tag head goes at the element's own point (or its curve midpoint)
//         plus a fixed offset. On a congested view that WILL overlap. Follow with
//         action-auto-arrange-tags.cs, or use the scoring recipe instead.
// SOURCE: ../../../knowledge/live-model/tagging.md — READ IT BEFORE CHANGING THIS FILE. It carries the
//         two findings above as live measurements, plus several more this fragment deliberately does not
//         try to reproduce (leader elbow clearance, flow-direction leader sides, view-scale dependence).
// RELATED: recipes/tag-elements-in-active-view.cs (best placement, one category) — **the better tool for
//          a congested view, and it is live-verified**: it scores each tag's side, follows real flow
//          direction, and resolves overlaps as it places. This fragment exists for the MIXED-CATEGORY
//          case it cannot cover, not because it improves on it.
//          action-tag-elements.cs (one category, simple), action-auto-arrange-tags.cs (tidy overlaps
//          afterwards), action-check-unannotated-elements.cs (what is still missing a tag),
//          action-remove-tags.cs (the undo).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23, corrected 2026-08-24 against
//   knowledge/live-model/tagging.md after Ajmal asked whether the new tag fragments would clash with the
//   existing ones. They did: this file originally guessed the tag family and never verified the created
//   type, both of which that note had already measured and settled. Tag ONE category on ONE view, check
//   the tag family that came out, then let it loose on a mixed set.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                 // true = print the plan per category, place nothing
ElementId viewId = null;            // null = the active view
bool addLeader = true;
double offsetXmm = 300;             // tag head offset from the element, mm
double offsetYmm = 300;
bool skipAlreadyTagged = true;      // leave elements that already carry a tag in this view alone

// Preferred tag family/type per category — a substring is enough. Anything not named here takes the
// first tag loaded for that category, and the report says which one that was.
var tagTypeHints = new Dictionary<BuiltInCategory, string>
{
    // { BuiltInCategory.OST_DuctCurves, "Size" },
    // { BuiltInCategory.OST_PipeCurves, "Size and System" },
};
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this.");
    return sb.ToString();
}

var view = viewId != null ? Document.GetElement(viewId) as View : Document.ActiveView;
if (view == null) { sb.AppendLine("STOP: no view resolved."); return sb.ToString(); }
if (view.IsTemplate) { sb.AppendLine($"STOP: '{view.Name}' is a view template — tags cannot be placed in one."); return sb.ToString(); }

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- CATEGORY -> TAG CATEGORY. A fixed fact about Revit; the API offers no way to derive it. ----
var tagCategoryFor = new Dictionary<long, BuiltInCategory>
{
    { (long)BuiltInCategory.OST_DuctCurves,           BuiltInCategory.OST_DuctTags },
    { (long)BuiltInCategory.OST_FlexDuctCurves,       BuiltInCategory.OST_FlexDuctTags },
    { (long)BuiltInCategory.OST_DuctFitting,          BuiltInCategory.OST_DuctFittingTags },
    { (long)BuiltInCategory.OST_DuctAccessory,        BuiltInCategory.OST_DuctAccessoryTags },
    { (long)BuiltInCategory.OST_DuctTerminal,         BuiltInCategory.OST_DuctTerminalTags },
    { (long)BuiltInCategory.OST_PipeCurves,           BuiltInCategory.OST_PipeTags },
    { (long)BuiltInCategory.OST_FlexPipeCurves,       BuiltInCategory.OST_FlexPipeTags },
    { (long)BuiltInCategory.OST_PipeFitting,          BuiltInCategory.OST_PipeFittingTags },
    { (long)BuiltInCategory.OST_PipeAccessory,        BuiltInCategory.OST_PipeAccessoryTags },
    { (long)BuiltInCategory.OST_PlumbingFixtures,     BuiltInCategory.OST_PlumbingFixtureTags },
    { (long)BuiltInCategory.OST_Sprinklers,           BuiltInCategory.OST_SprinklerTags },
    { (long)BuiltInCategory.OST_MechanicalEquipment,  BuiltInCategory.OST_MechanicalEquipmentTags },
    { (long)BuiltInCategory.OST_CableTray,            BuiltInCategory.OST_CableTrayTags },
    { (long)BuiltInCategory.OST_CableTrayFitting,     BuiltInCategory.OST_CableTrayFittingTags },
    { (long)BuiltInCategory.OST_Conduit,              BuiltInCategory.OST_ConduitTags },
    { (long)BuiltInCategory.OST_ConduitFitting,       BuiltInCategory.OST_ConduitFittingTags },
    { (long)BuiltInCategory.OST_ElectricalEquipment,  BuiltInCategory.OST_ElectricalEquipmentTags },
    { (long)BuiltInCategory.OST_ElectricalFixtures,   BuiltInCategory.OST_ElectricalFixtureTags },
    { (long)BuiltInCategory.OST_LightingFixtures,     BuiltInCategory.OST_LightingFixtureTags },
    { (long)BuiltInCategory.OST_LightingDevices,      BuiltInCategory.OST_LightingDeviceTags },
    { (long)BuiltInCategory.OST_FireAlarmDevices,     BuiltInCategory.OST_FireAlarmDeviceTags },
    { (long)BuiltInCategory.OST_DataDevices,          BuiltInCategory.OST_DataDeviceTags },
    { (long)BuiltInCategory.OST_CommunicationDevices, BuiltInCategory.OST_CommunicationDeviceTags },
    { (long)BuiltInCategory.OST_SecurityDevices,      BuiltInCategory.OST_SecurityDeviceTags },
    { (long)BuiltInCategory.OST_TelephoneDevices,     BuiltInCategory.OST_TelephoneDeviceTags },
    { (long)BuiltInCategory.OST_NurseCallDevices,     BuiltInCategory.OST_NurseCallDeviceTags },
};

// ---- what is already tagged in this view (version-proof read, 2020 through 2027) ----
var _mAllIds = typeof(IndependentTag).GetMethod("GetTaggedElementIds", Type.EmptyTypes);      // 2022+
var _pAllId  = typeof(IndependentTag).GetProperty("TaggedElementId");                         // pre-2022

Func<object, ElementId> localIdOf = o =>
{
    var lei = o as LinkElementId;
    if (lei == null) return ElementId.InvalidElementId;
    if (lei.LinkInstanceId != ElementId.InvalidElementId) return ElementId.InvalidElementId; // lives in a link
    return lei.HostElementId;
};

var alreadyTagged = new HashSet<long>();
if (skipAlreadyTagged)
{
    var existing = new FilteredElementCollector(Document, view.Id)
        .OfClass(typeof(IndependentTag)).Cast<IndependentTag>().ToList();
    foreach (var tg in existing)
    {
        try
        {
            if (_mAllIds != null)
            {
                var raw = _mAllIds.Invoke(tg, null) as System.Collections.IEnumerable;
                if (raw != null) foreach (var o in raw)
                {
                    var id = localIdOf(o);
                    if (id != ElementId.InvalidElementId) alreadyTagged.Add(IdValue(id));
                }
            }
            else if (_pAllId != null)
            {
                var id = localIdOf(_pAllId.GetValue(tg));
                if (id != ElementId.InvalidElementId) alreadyTagged.Add(IdValue(id));
            }
        }
        catch { }
    }
}

// ---- pick a tag type per category ----
var tagTypeForCat = new Dictionary<long, FamilySymbol>();
var tagChoiceNote = new Dictionary<long, string>();
var noTagLoaded = new Dictionary<long, int>();

var byCategory = elements
    .Where(e => e.Category != null)
    .GroupBy(e => IdValue(e.Category.Id))
    .ToList();

foreach (var grp in byCategory)
{
    if (!tagCategoryFor.ContainsKey(grp.Key))
    {
        noTagLoaded[grp.Key] = grp.Count();
        tagChoiceNote[grp.Key] = "no tag category is mapped for this element category";
        continue;
    }
    var tagCat = tagCategoryFor[grp.Key];
    var available = new FilteredElementCollector(Document)
        .OfClass(typeof(FamilySymbol)).OfCategory(tagCat).Cast<FamilySymbol>().ToList();

    if (available.Count == 0)
    {
        noTagLoaded[grp.Key] = grp.Count();
        tagChoiceNote[grp.Key] = $"NO {tagCat} family is loaded in this project — load one first";
        continue;
    }

    FamilySymbol pick = null;
    string hint = tagTypeHints.ContainsKey((BuiltInCategory)grp.Key) ? tagTypeHints[(BuiltInCategory)grp.Key] : "";
    if (!string.IsNullOrWhiteSpace(hint))
    {
        pick = available.FirstOrDefault(s =>
            s.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (s.Family != null && s.Family.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    if (pick == null)
    {
        // REVIT'S OWN DEFAULT, NOT THE FIRST ONE IN THE LIST. `GetDefaultFamilyTypeId` returns exactly
        // what "Tag by Category" would use for this category — i.e. whatever tag family THIS project has
        // standardised on, which is usually not the generic Autodesk one. Taking the first loaded symbol
        // instead is the guess Ajmal corrected on a live job (knowledge/live-model/tagging.md), and it
        // silently produces the wrong tag family on every element rather than erroring.
        // ElementTypeGroup has NO per-MEP-category tag entries (no DuctTagType etc. — the full enum was
        // checked), so this Category-based lookup is the only route.
        try
        {
            var catObj = Category.GetCategory(Document, tagCat);
            if (catObj != null)
            {
                var defId = Document.GetDefaultFamilyTypeId(catObj.Id);
                if (defId != null && defId != ElementId.InvalidElementId)
                {
                    var defSym = Document.GetElement(defId) as FamilySymbol;
                    if (defSym != null)
                    {
                        pick = defSym;
                        tagChoiceNote[grp.Key] = $"'{defSym.Family?.Name} : {defSym.Name}' — Revit's own default for this category (what Tag by Category uses)";
                    }
                }
            }
        }
        catch { }
    }

    if (pick == null)
    {
        pick = available.First();
        tagChoiceNote[grp.Key] = string.IsNullOrWhiteSpace(hint)
            ? $"picked '{pick.Family?.Name} : {pick.Name}' (no project default set; first of {available.Count} — A GUESS, check it)"
            : $"hint '{hint}' matched nothing and no project default is set; fell back to '{pick.Family?.Name} : {pick.Name}' (first of {available.Count} — A GUESS)";
    }
    else if (!tagChoiceNote.ContainsKey(grp.Key))
        tagChoiceNote[grp.Key] = $"'{pick.Family?.Name} : {pick.Name}' (matched hint '{hint}', {available.Count} available)";

    tagTypeForCat[grp.Key] = pick;
}

// ---- the plan ----
sb.AppendLine($"AUTO-TAG MEP — view '{view.Name}'");
sb.AppendLine();
sb.AppendLine("| Category | In set | Already tagged | To tag | Tag family chosen |");
sb.AppendLine("|---|---|---|---|---|");

var toTag = new List<(Element El, FamilySymbol Sym)>();
foreach (var grp in byCategory.OrderBy(g => g.First().Category?.Name))
{
    string catName = grp.First().Category?.Name ?? grp.Key.ToString();
    int already = grp.Count(e => alreadyTagged.Contains(IdValue(e.Id)));
    int plan = 0;
    if (tagTypeForCat.ContainsKey(grp.Key))
    {
        foreach (var e in grp)
        {
            if (skipAlreadyTagged && alreadyTagged.Contains(IdValue(e.Id))) continue;
            toTag.Add((e, tagTypeForCat[grp.Key]));
            plan++;
        }
    }
    sb.AppendLine($"| {catName} | {grp.Count()} | {already} | {plan} | {tagChoiceNote[grp.Key]} |");
}

sb.AppendLine();
sb.AppendLine($"TOTAL TO TAG: {toTag.Count}");
if (noTagLoaded.Count > 0)
{
    int missed = noTagLoaded.Values.Sum();
    sb.AppendLine($"CANNOT TAG: {missed} element(s) across {noTagLoaded.Count} categor(y/ies) — see the reasons in the table. These are NOT tagged and NOT a pass.");
}

if (toTag.Count == 0) return sb.ToString();

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing placed. Check the tag family chosen for each category, then set dryRun = false.");
    return sb.ToString();
}

// ---- place ----
Func<Element, XYZ> anchorOf = el =>
{
    var lp = el.Location as LocationPoint;
    if (lp != null) return lp.Point;
    var lc = el.Location as LocationCurve;
    if (lc != null && lc.Curve != null) return lc.Curve.Evaluate(0.5, true);
    BoundingBoxXYZ bb = null;
    try { bb = el.get_BoundingBox(view); } catch { }
    return bb != null ? (bb.Min + bb.Max) * 0.5 : null;
};

int placed = 0, retyped = 0;
var failures = new List<string>();
var wrongType = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - auto-tag MEP"))
{
    tx.Start();
    var opts = tx.GetFailureHandlingOptions();
    opts.SetForcedModalHandling(false);
    tx.SetFailureHandlingOptions(opts);
    try
    {
        // A tag family that is not activated cannot be placed, and the failure reads as a mystery.
        foreach (var sym in toTag.Select(t => t.Sym).Distinct())
            if (!sym.IsActive) { sym.Activate(); }
        Document.Regenerate();

        foreach (var t in toTag)
        {
            try
            {
                var pt = anchorOf(t.El);
                if (pt == null) { failures.Add($"{t.El.Id}: no location to tag from"); continue; }
                var head = new XYZ(pt.X + ToFeet(offsetXmm), pt.Y + ToFeet(offsetYmm), pt.Z);

                var tag = IndependentTag.Create(Document, t.Sym.Id, view.Id,
                    new Reference(t.El), addLeader, TagOrientation.Horizontal, head);
                if (tag == null) { failures.Add($"{t.El.Id}: Revit returned no tag"); continue; }

                // `Create` DOES NOT RELIABLY HONOUR THE TYPE YOU PASS IT. Measured live on this Brain:
                // 38 tags created with an explicit symId all came out as the document's own default type,
                // with no exception thrown (knowledge/live-model/tagging.md). It was harmless that time
                // only because the two happened to be the same id after a fix. So verify and correct.
                try
                {
                    if (tag.GetTypeId() != t.Sym.Id)
                    {
                        tag.ChangeTypeId(t.Sym.Id);
                        retyped++;
                    }
                }
                catch (Exception typeEx)
                {
                    wrongType.Add($"{t.El.Id}: tag created but is the WRONG TYPE and would not change — {typeEx.Message}");
                }
                placed++;
            }
            catch (Exception ex) { failures.Add($"{t.El.Id}: {ex.Message}"); }
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (auto-tag MEP) — rolled back, nothing placed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"PLACED: {placed} of {toTag.Count} tag(s) in '{view.Name}'.");
if (retyped > 0)
    sb.AppendLine($"TYPE CORRECTED ON {retyped} tag(s) — Revit created them with a different type than the one asked for. Expected behaviour, caught and fixed; see knowledge/live-model/tagging.md.");
if (wrongType.Count > 0)
{
    sb.AppendLine($"WRONG TYPE AND COULD NOT BE CORRECTED ({wrongType.Count}) — these tags exist but are the wrong family:");
    foreach (var w in wrongType.Take(15)) sb.AppendLine($"  {w}");
}
if (failures.Count > 0)
{
    sb.AppendLine($"NOT PLACED ({failures.Count}):");
    foreach (var f in failures.Take(25)) sb.AppendLine($"  {f}");
    if (failures.Count > 25) sb.AppendLine($"  ... and {failures.Count - 25} more");
}
sb.AppendLine("Placement here is a fixed offset, so tags WILL overlap on a busy view — run action-auto-arrange-tags.cs next.");
sb.AppendLine("For a congested view, recipes/tag-elements-in-active-view.cs is the better tool: it SCORES each tag's side and resolves overlaps as it places, and it is live-verified.");

return sb.ToString();
