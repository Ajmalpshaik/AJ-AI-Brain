// ============================================================
// FRAGMENT (filter) — filter-by-elements-on-level.cs
// PURPOSE: Everything on a given Level, across the WHOLE model — no category picked first. Use this
//          when the request is "what's on Level 2" with no category named. If the category is already
//          known, filter-by-category.cs already has a level scope built in and is cheaper — prefer that
//          instead.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Level matching uses the same fallback chain as filter-by-category.cs (Wall/FamilyInstance/etc. each
// store their level differently — no single universal property). Optional categoryScope bounds the scan
// per ../../../knowledge/live-model/core.md's "watch for unbounded output" — leave empty only for a
// genuinely whole-model question.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId levelId = ElementId.InvalidElementId; // set explicitly
BuiltInCategory[] categoryScope = new BuiltInCategory[0]; // empty = every category (can be slow/large)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

if (levelId == ElementId.InvalidElementId)
{
    sb.AppendLine("No levelId set — filter produced 0 elements.");
}
else
{
    var level = Document.GetElement(levelId) as Level;
    if (level == null)
    {
        sb.AppendLine($"levelId {levelId} is not a valid Level.");
    }
    else
    {
        Func<Element, ElementId> resolveLevelId = e =>
        {
            if (e is Wall wall) return wall.LevelId;
            if (e.LevelId != ElementId.InvalidElementId) return e.LevelId;
            // RBS_START_LEVEL_PARAM last, and it matters: on a Duct/Pipe the
            // other four are NOT PRESENT (proved live 2026-08-06), so without
            // it this filter silently returns ZERO MEP curves for any level.
            var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            return p?.AsElementId() ?? ElementId.InvalidElementId;
        };

        IEnumerable<Element> query;
        if (categoryScope.Length > 0)
        {
            var categoryIds = categoryScope.Select(c => new ElementId(c)).ToList();
            query = new FilteredElementCollector(Document)
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsNotElementType();
        }
        else
        {
            query = new FilteredElementCollector(Document).WhereElementIsNotElementType();
        }

        elements = query.Where(e =>
        {
            try { return resolveLevelId(e) == level.Id; }
            catch { return false; }
        }).ToList();

        string categoryLabel = categoryScope.Length == 0 ? "all categories" : string.Join(", ", categoryScope.Select(c => c.ToString()));
        sb.AppendLine($"Filtered {elements.Count} element(s) on level '{level.Name}', categories: {categoryLabel}.");
    }
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
