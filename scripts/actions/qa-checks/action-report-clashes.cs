// ============================================================
// FRAGMENT (action) — action-report-clashes.cs
// PURPOSE: Basic clash/overlap report — real geometry intersection (not just bounding box, via Revit's own
//          ElementIntersectsElementFilter) between every element in `elements` (set A) and every element of
//          a SECOND category (set B, collected inside this action itself since clash detection is
//          inherently two sets, not one). Read-only: reports colliding pairs, never moves/deletes anything.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above —
//          that's set A. Set B is a second category, collected here from its own INPUT.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: O(B) filter passes, each restricted to set A via the FilteredElementCollector(Document, ids)
//         constructor — fine at the usual "duct vs structure" scale; don't run this with two huge
//         whole-model sets without narrowing first (level/region/category).
// GOTCHA: this is real solid intersection, so touching-but-not-overlapping (e.g. two ducts sharing a face
//         with zero overlap volume) may or may not register depending on how Revit's geometry kernel
//         treats coincident faces — treat a reported clash as "investigate", not an automatic fail.
// Live-verified 2026-07-22, zero bugs (read-only).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategoryB = BuiltInCategory.OST_StructuralFraming; // set B — the category `elements` (set A) is checked against
int maxPairsReported = 200;
// ---- END INPUTS ----

var setBIds = new FilteredElementCollector(Document)
    .OfCategory(targetCategoryB)
    .WhereElementIsNotElementType()
    .ToElementIds();

var setAIds = elements.Select(e => e.Id).ToList();
var clashes = new List<(ElementId aId, ElementId bId)>();

foreach (var bId in setBIds)
{
    var bElement = Document.GetElement(bId);
    ICollection<ElementId> hits;
    try
    {
        var intersectFilter = new ElementIntersectsElementFilter(bElement);
        hits = new FilteredElementCollector(Document, setAIds).WherePasses(intersectFilter).ToElementIds();
    }
    catch { continue; } // some element types can't be used with ElementIntersectsElementFilter — skip, don't fail the batch

    foreach (var aId in hits)
    {
        if (aId == bId) continue;
        clashes.Add((aId, bId));
    }
}

sb.AppendLine($"Checked {setAIds.Count} element(s) (set A) against {setBIds.Count} element(s) of category {targetCategoryB} (set B): {clashes.Count} clashing pair(s).");
sb.AppendLine("Set A Id | Set B Id");
sb.AppendLine("--- | ---");
foreach (var (aId, bId) in clashes.Take(maxPairsReported))
    sb.AppendLine($"{aId.IntegerValue} | {bId.IntegerValue}");
if (clashes.Count > maxPairsReported) sb.AppendLine($"... {clashes.Count - maxPairsReported} more pair(s) not shown.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
