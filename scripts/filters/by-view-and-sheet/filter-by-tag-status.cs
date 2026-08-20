// ============================================================
// FRAGMENT (filter) — filter-by-tag-status.cs
// PURPOSE: Category elements that ARE or ARE NOT tagged in a given view — QA sweep ("show me what's not
//          tagged yet"). Tags are view-specific, so this always needs a target view.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../../../knowledge/live-model/tagging.md — this Revit version (2020) has no
//         IndependentTag.GetTaggedLocalElementIds() (that's the 2022+ multi-reference tag API); uses the
//         confirmed single-reference IndependentTag.TaggedLocalElementId property instead.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
int? targetViewIdInt = null; // null = active view
bool wantTagged = false; // true = only elements that ARE tagged; false = only elements NOT yet tagged
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    // Version-proof tagged-element read. Revit 2022 let one tag point at several elements, so the single
    // `TaggedLocalElementId` property was replaced by `GetTaggedLocalElementIds()` and REMOVED - naming it
    // in code stops the fragment compiling on 2024. Looked up by name once, so one source serves 2020-2027.
    var _tagGetIds = typeof(IndependentTag).GetMethod("GetTaggedLocalElementIds", Type.EmptyTypes);
    var _tagOneId  = typeof(IndependentTag).GetProperty("TaggedLocalElementId");
    Func<IndependentTag, IEnumerable<ElementId>> TaggedIdsOf = tg =>
    {
        if (_tagGetIds != null)
            return ((System.Collections.IEnumerable)_tagGetIds.Invoke(tg, null)).Cast<ElementId>();
        if (_tagOneId != null)
            return new[] { (ElementId)_tagOneId.GetValue(tg) };
        return Enumerable.Empty<ElementId>();
    };

    var taggedIds = new HashSet<ElementId>();
    foreach (IndependentTag tag in new FilteredElementCollector(Document, view.Id).OfClass(typeof(IndependentTag)))
    {
        try { foreach (var tid in TaggedIdsOf(tag)) taggedIds.Add(tid); } catch { }
    }

    elements = new FilteredElementCollector(Document, view.Id)
        .OfCategory(targetCategory)
        .WhereElementIsNotElementType()
        .Where(e => taggedIds.Contains(e.Id) == wantTagged)
        .ToList();

    sb.AppendLine($"Filtered {elements.Count} element(s) in view '{view.Name}', category {targetCategory}, {(wantTagged ? "already tagged" : "NOT tagged")}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
