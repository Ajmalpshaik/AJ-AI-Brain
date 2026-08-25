// ============================================================
// FRAGMENT (filter) — filter-by-category.cs
// PURPOSE: Every instance of one category, optionally scoped to a level. The simplest filter — use
//          this when there's no family/size/room condition at all. Same job as
//          filter-by-category-name.cs — the only difference is the input: this one takes the
//          BuiltInCategory enum member, that one takes the plain display name ("Ducts"). Prefer this
//          when the enum is known; the name one when working straight from the user's wording.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Level matching tries several element-type-specific properties in order, since there is no single
// universal "get this element's level" API — Wall/Floor/FamilyInstance each store it differently.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
ElementId levelIdFilter = ElementId.InvalidElementId; // InvalidElementId = whole model, any level
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<Element, ElementId> resolveLevelId = e =>
{
    if (e is Wall wall) return wall.LevelId;
    if (e.LevelId != ElementId.InvalidElementId) return e.LevelId;
    // RBS_START_LEVEL_PARAM last, and it is the one that matters for MEP: on a
    // Duct/Pipe/CableTray/Conduit the other four are NOT PRESENT at all (proved
    // live 2026-08-06 — all four "parameter not present", this one returned
    // Level 1). Without it, setting levelIdFilter silently matches ZERO ducts
    // instead of erroring, which is the confidently-wrong failure this library
    // exists to prevent.
    var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
    return p?.AsElementId() ?? ElementId.InvalidElementId;
};

var query = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .AsEnumerable();

if (levelIdFilter != ElementId.InvalidElementId)
{
    query = query.Where(e => resolveLevelId(e) == levelIdFilter);
}

List<Element> elements = query.ToList();
sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
