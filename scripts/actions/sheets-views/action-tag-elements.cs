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
// Live-verified 2026-07-22, zero bugs (this exact simplified fragment, not just the underlying API
// pattern from the recipe).
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
            UnitUtils.ConvertToInternalUnits(offsetXmm, DisplayUnitType.DUT_MILLIMETERS),
            UnitUtils.ConvertToInternalUnits(offsetYmm, DisplayUnitType.DUT_MILLIMETERS),
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
                if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id.IntegerValue))}");
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
