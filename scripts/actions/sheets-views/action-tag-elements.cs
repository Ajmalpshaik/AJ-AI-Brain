// ============================================================
// FRAGMENT (action) — action-tag-elements.cs
// PURPOSE: Tag every element in `elements` in one given view — simple placement (each tag head offset
//          from the element's own point/curve-midpoint by a fixed vector, straight leader optional), NOT
//          the scored clash-avoiding placement recipes/tag-elements-in-active-view.cs does. Use this for
//          "just tag these" on a set small/sparse enough that clash-scoring isn't needed; use the recipe
//          for a dense duct/pipe run where tags would otherwise overlap each other or the geometry.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the newly created IndependentTag elements.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: IndependentTag.Create/GetDefaultFamilyTypeId/ChangeTypeId pattern reused from
//         recipes/tag-elements-in-active-view.cs, which already validated it against a real model.
// GOTCHA: offsetXmm/offsetYmm is a flat vector in MODEL space, not view space (unlike the recipe, which
//         projects along the view's own Right/Up directions) — fine for a plan view, will offset in an
//         unexpected direction on a rotated or non-plan view.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
//
// ✱✱ THREE FRAGMENTS PLACE TAGS AND THE CHOICE IS ALREADY SETTLED IN
//    knowledge/live-model/tagging.md - read that section before overriding this:
//      recipes/tag-elements-in-active-view.cs        THE DEFAULT for ONE category on a normal or busy
//                                                    view. LIVE-VERIFIED. Scores each tag side, follows
//                                                    real flow direction, computes elbows and resolves
//                                                    its own overlaps. Nothing else here does placement
//                                                    properly. "tag it", "tag the ducts".
//      actions/sheets-views/action-auto-tag-mep.cs   The MIXED-CATEGORY case the default cannot reach -
//                                                    ducts and pipes and terminals and equipment in one
//                                                    pass, each getting its own tag family. Carries the
//                                                    CATEGORY -> TAG CATEGORY map. Placement is a plain
//                                                    offset, so follow it with the tidy-up below.
//                                                    Its own row in scripts/README.md is the status.
//      actions/sheets-views/action-tag-elements.cs   QUICK, one category, when placement does not
//                                                    matter. Fixed offset, no scoring, no overlap
//                                                    handling. Verified 2026-07-22.
//    They do not collide, they are SEQUENTIAL: place (the recipe, or auto-tag-mep for a mixed set),
//    then tidy. Tags ALREADY PLACED and sitting on top of each other is that separate tidy-up job -
//    action-auto-arrange-tags.cs (push apart in place), action-arrange-tags-to-view-edges.cs (park them
//    down the crop edges), action-stack-tags.cs (one tidy column). Measured 2026-08-24: "tag the
//    elements" returned the NOT-YET-VERIFIED auto-tag-mep above both verified fragments, and "tag all
//    the ducts in this view" returned neither verified one in the top four. This table is in all three
//    files so that landing on any one of them routes correctly whatever the ranking does.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0; // 0 = use Document.ActiveView
BuiltInCategory tagCategory = BuiltInCategory.OST_DuctTags; // must match the category of `elements`
string tagFamilyTypeNameOverride = null; // null = use the project's default tag type for tagCategory
double offsetXmm = 0, offsetYmm = 400; // tag head position relative to the element's own point
bool addLeader = true;
// ---- END INPUTS ----

var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;
if (view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else
{
    FamilySymbol tagType;
    if (string.IsNullOrEmpty(tagFamilyTypeNameOverride))
    {
        var tagCategoryObj = Category.GetCategory(Document, tagCategory);
        var defaultTypeId = Document.GetDefaultFamilyTypeId(tagCategoryObj.Id);
        tagType = Document.GetElement(defaultTypeId) as FamilySymbol;
    }
    else
    {
        tagType = new FilteredElementCollector(Document).OfCategory(tagCategory).WhereElementIsElementType()
            .FirstOrDefault(e => e.Name == tagFamilyTypeNameOverride) as FamilySymbol;
    }

    if (tagType == null)
    {
        sb.AppendLine($"No tag type resolved for {tagCategory}" + (string.IsNullOrEmpty(tagFamilyTypeNameOverride) ? "." : $" named '{tagFamilyTypeNameOverride}'."));
    }
    else
    {
        Func<Element, XYZ> getPoint = e =>
        {
            if (e.Location is LocationPoint lp) return lp.Point;
            if (e.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
            try { var bb = e.get_BoundingBox(view); if (bb != null) return (bb.Min + bb.Max) * 0.5; } catch { }
            return null;
        };

        XYZ offset = new XYZ(
            offsetXmm / 304.8,
            offsetYmm / 304.8,
            0);

        var newElementIds = new List<ElementId>();
        int tagged = 0, skipped = 0;

        using (var t = new Transaction(Document, "AJ Tools - Tag Elements"))
        {
            t.Start();
            try
            {
                if (!tagType.IsActive) { tagType.Activate(); Document.Regenerate(); }

                foreach (var e in elements)
                {
                    var pt = getPoint(e);
                    if (pt == null) { skipped++; continue; }

                    try
                    {
                        var reference = new Reference(e);
                        var tag = IndependentTag.Create(Document, tagType.Id, view.Id, reference, addLeader, TagOrientation.Horizontal, pt + offset);
                        if (tag.GetTypeId() != tagType.Id) tag.ChangeTypeId(tagType.Id);
                        newElementIds.Add(tag.Id);
                        tagged++;
                    }
                    catch { skipped++; } // element not visible/taggable in this view, or category mismatch
                }
                t.Commit();
                sb.AppendLine($"Tagged {tagged} element(s) in view '{view.Name}', skipped {skipped}.");
                if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id))}");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to tag — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
