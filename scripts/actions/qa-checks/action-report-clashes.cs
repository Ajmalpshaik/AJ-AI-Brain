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
//
// ✱✱ FIXED 2026-08-23 — THIS USED TO GIVE A CLEAN BILL OF HEALTH FOR ELEMENTS IT NEVER TESTED.
//    `ElementIntersectsElementFilter` does not accept every element. The old code wrapped it in
//    `catch { continue; }`, so an unsupported element in set B was dropped and the summary still said
//    "checked N elements" — a coordination report that quietly under-reports is worse than one that
//    fails, because nobody goes back to it. Revit will tell you in advance:
//        ElementIntersectsFilter.IsElementSupported(element)   — this element can be geometrically tested
//        ElementIntersectsFilter.IsCategorySupported(element)  — this element's category can be
//    Both are now asked BEFORE the test, on BOTH sets, and anything that cannot be tested is listed by
//    name and Id under UNTESTED. The count line now separates "tested" from "in the set".
//    Unsupported in practice: annotation, view-specific and 2D elements, most elements with no solid,
//    and elements from a linked document (a link needs its own transform — see the linked-model note).
//    filter-by-element-intersection.cs had the same blind spot in the loud direction (it threw) and is
//    fixed the same way.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategoryB = BuiltInCategory.OST_StructuralFraming; // set B — the category `elements` (set A) is checked against
int maxPairsReported = 200;
// ---- END INPUTS ----

var setBAll = new FilteredElementCollector(Document)
    .OfCategory(targetCategoryB)
    .WhereElementIsNotElementType()
    .ToElementIds();

// Ask Revit which elements it can actually test, rather than finding out by swallowing an exception.
// Anything it refuses is recorded so the report can say so out loud instead of implying it was clean.
var untested = new List<(ElementId id, string side, string reason)>();

Func<Element, string> whyUntestable = el =>
{
    if (el == null) return "element could not be resolved";
    bool catOk = true, elOk = true;
    try { catOk = ElementIntersectsFilter.IsCategorySupported(el); } catch { }
    try { elOk = ElementIntersectsFilter.IsElementSupported(el); } catch { }
    if (!catOk) return "category not supported by geometric intersection";
    if (!elOk) return "element not supported (usually no solid geometry)";
    return null;
};

var setAIds = new List<ElementId>();
foreach (var e in elements)
{
    var reason = whyUntestable(e);
    if (reason == null) setAIds.Add(e.Id);
    else untested.Add((e.Id, "A", reason));
}

var setBIds = new List<ElementId>();
foreach (var bId in setBAll)
{
    var bEl = Document.GetElement(bId);
    var reason = whyUntestable(bEl);
    if (reason == null) setBIds.Add(bId);
    else untested.Add((bId, "B", reason));
}

var clashes = new List<(ElementId aId, ElementId bId)>();

if (setAIds.Count > 0)
{
    foreach (var bId in setBIds)
    {
        var bElement = Document.GetElement(bId);
        ICollection<ElementId> hits;
        try
        {
            var intersectFilter = new ElementIntersectsElementFilter(bElement);
            hits = new FilteredElementCollector(Document, setAIds).WherePasses(intersectFilter).ToElementIds();
        }
        catch (Exception ex)
        {
            // Should not happen now the support check runs first — but if it does, it is REPORTED,
            // never swallowed, because a silent skip is what this fragment was fixed to stop doing.
            untested.Add((bId, "B", "intersection test threw: " + ex.Message));
            continue;
        }

        foreach (var aId in hits)
        {
            if (aId == bId) continue;
            clashes.Add((aId, bId));
        }
    }
}

sb.AppendLine($"Tested {setAIds.Count} of {elements.Count} element(s) in set A against {setBIds.Count} of {setBAll.Count} element(s) of category {targetCategoryB} (set B): {clashes.Count} clashing pair(s).");
if (untested.Count > 0)
    sb.AppendLine($"⚠ {untested.Count} element(s) COULD NOT BE TESTED and are NOT covered by the result below — listed at the end.");
if (setAIds.Count == 0)
    sb.AppendLine("Nothing in set A could be geometrically tested — the result above is not a clean result, it is an empty one.");
sb.AppendLine("Set A Id | Set B Id");
sb.AppendLine("--- | ---");
foreach (var (aId, bId) in clashes.Take(maxPairsReported))
    sb.AppendLine($"{aId} | {bId}");
if (clashes.Count > maxPairsReported) sb.AppendLine($"... {clashes.Count - maxPairsReported} more pair(s) not shown.");

if (untested.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"UNTESTED — {untested.Count} element(s) Revit cannot check for solid intersection:");
    sb.AppendLine("Set | Id | Category | Reason");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var (id, side, reason) in untested.Take(maxPairsReported))
    {
        var el = Document.GetElement(id);
        string cat = el?.Category?.Name ?? "(no category)";
        sb.AppendLine($"{side} | {id} | {cat} | {reason}");
    }
    if (untested.Count > maxPairsReported) sb.AppendLine($"... {untested.Count - maxPairsReported} more not shown.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
