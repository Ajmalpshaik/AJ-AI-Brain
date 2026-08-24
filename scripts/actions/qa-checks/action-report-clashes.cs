// ============================================================
// FRAGMENT (action) — action-report-clashes.cs
// PURPOSE: Basic clash/overlap report — real geometry intersection (not just bounding box) between every
//          element in `elements` (set A) and every element of a SECOND category (set B, collected inside
//          this action itself since clash detection is inherently two sets, not one). Set B can be in
//          THIS model or in a LINKED model. Read-only: reports colliding pairs, never moves/deletes.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above —
//          that's set A. Set B is a second category, collected here from its own INPUT.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: this is real solid intersection, so touching-but-not-overlapping (e.g. two ducts sharing a face
//         with zero overlap volume) may or may not register depending on how Revit's geometry kernel
//         treats coincident faces — treat a reported clash as "investigate", not an automatic fail.
//
// ✱✱ ADDED 2026-08-24 — SET B CAN NOW BE A LINKED MODEL, AND THAT WAS THE MAIN THING THIS COULD NOT DO.
//    On an MEP job the structure is in a LINK. This fragment collected set B with
//    `new FilteredElementCollector(Document)` — the active document only — so on Ajmal's real models
//    the honest answer to "check my ducts against the structure" was that this could not do it, while
//    reporting "0 clashing pairs" in a tone that reads as a pass. Set `linkInstanceIdInt` and set B is
//    read from the link, with the link's transform applied. Same defect and same shape as the one
//    already flagged on `action-create-from-room-boundaries.cs`.
//    THE MECHANISM CHANGES WITH IT: `ElementIntersectsElementFilter` cannot cross documents — it takes
//    an Element and tests it against elements of the SAME document. So the linked path takes each set-B
//    element's SOLID, moves it into this model's coordinates with `SolidUtils.CreateTransformed`, and
//    uses `ElementIntersectsSolidFilter` instead. Both paths are reported the same way.
//
// ✱✱ ALSO 2026-08-24 — A QUICK FILTER NOW RUNS BEFORE THE SLOW ONE. `ElementIntersectsElementFilter`
//    and `ElementIntersectsSolidFilter` are SLOW filters: Revit expands each candidate's geometry to
//    answer them. `BoundingBoxIntersectsFilter` is a QUICK filter — it reads only the element record.
//    Chained quick-then-slow, the geometry is only ever built for candidates whose boxes already
//    overlap. The old code went straight to the slow filter for every element of set A, once per
//    element of set B, which is what the "don't run this with two huge sets" warning was really about.
//    See knowledge/live-model/query-cost.md.
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
//    Unsupported in practice: annotation, view-specific and 2D elements, and most elements with no solid.
//    filter-by-element-intersection.cs had the same blind spot in the loud direction (it threw) and is
//    fixed the same way.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategoryB = BuiltInCategory.OST_StructuralFraming; // set B — the category `elements` (set A) is checked against
int? linkInstanceIdInt = null;   // null = set B is in THIS model; else the RVT link set B lives in
int maxPairsReported = 200;
// ---- END INPUTS ----

// ---------- resolve set B's document ----------
Document bDoc = Document;
Transform linkTf = Transform.Identity;
if (linkInstanceIdInt.HasValue)
{
    var li = Document.GetElement(new ElementId(linkInstanceIdInt.Value)) as RevitLinkInstance;
    if (li == null) { sb.AppendLine("linkInstanceIdInt is not a Revit link instance in this model."); return sb.ToString(); }
    bDoc = li.GetLinkDocument();
    if (bDoc == null) { sb.AppendLine("That link is not loaded — load it before clashing against it."); return sb.ToString(); }
    linkTf = li.GetTotalTransform();
}

var setBAll = new FilteredElementCollector(bDoc)
    .OfCategory(targetCategoryB)
    .WhereElementIsNotElementType()
    .ToElementIds();

// Ask Revit which elements it can actually test, rather than finding out by swallowing an exception.
// Anything it refuses is recorded so the report can say so out loud instead of implying it was clean.
var untested = new List<Tuple<ElementId, string, string>>();   // id, side, reason

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
    else untested.Add(Tuple.Create(e.Id, "A", reason));
}

var setBIds = new List<ElementId>();
foreach (var bId in setBAll)
{
    var bEl = bDoc.GetElement(bId);
    // A linked element is tested through its SOLID, not through ElementIntersectsElementFilter, so the
    // element-support question does not apply to it — only "does it have geometry", which the solid
    // extraction answers for itself below.
    if (linkInstanceIdInt.HasValue) { setBIds.Add(bId); continue; }
    var reason = whyUntestable(bEl);
    if (reason == null) setBIds.Add(bId);
    else untested.Add(Tuple.Create(bId, "B", reason));
}

// ---------- geometry helpers, used only on the linked path ----------
var geomOpts = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };

// A solid with no faces or no edges cannot take part in a boolean or a solid filter, and the test must
// run on the ORIGINAL solid: SolidUtils.CreateTransformed gives an empty solid faces and edges, so a
// transformed copy looks healthy and then throws.
Func<Solid, bool> usable = s =>
{
    if (s == null) return false;
    try { return !s.Faces.IsEmpty && !s.Edges.IsEmpty && s.Volume > 0; }
    catch { return false; }
};

Func<Element, List<Solid>> solidsOf = el =>
{
    var found = new List<Solid>();
    if (el == null) return found;
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geomOpts); } catch { }
    if (ge == null) return found;
    var stack = new Stack<GeometryElement>();
    stack.Push(ge);
    while (stack.Count > 0)
    {
        foreach (var go in stack.Pop())
        {
            var s = go as Solid;
            if (s != null) { if (usable(s)) found.Add(s); continue; }
            var gi = go as GeometryInstance;
            if (gi != null) { try { stack.Push(gi.GetInstanceGeometry()); } catch { } }
        }
    }
    return found;
};

// Solid.GetBoundingBox() is in the SOLID's own coordinates plus a transform — unlike Element.BoundingBox,
// which is already in model coordinates. All eight corners are transformed before the outline is taken,
// or the quick filter looks in the wrong place and finds nothing.
Func<Solid, Outline> outlineOf = s =>
{
    BoundingBoxXYZ bb = null;
    try { bb = s.GetBoundingBox(); } catch { }
    if (bb == null) return null;
    var t = bb.Transform ?? Transform.Identity;
    var mn = bb.Min; var mx = bb.Max;
    double lo0 = 0, lo1 = 0, lo2 = 0, hi0 = 0, hi1 = 0, hi2 = 0;
    bool first = true;
    for (int i = 0; i < 8; i++)
    {
        var c = t.OfPoint(new XYZ((i & 1) == 0 ? mn.X : mx.X, (i & 2) == 0 ? mn.Y : mx.Y, (i & 4) == 0 ? mn.Z : mx.Z));
        if (first) { lo0 = hi0 = c.X; lo1 = hi1 = c.Y; lo2 = hi2 = c.Z; first = false; }
        else
        {
            lo0 = Math.Min(lo0, c.X); hi0 = Math.Max(hi0, c.X);
            lo1 = Math.Min(lo1, c.Y); hi1 = Math.Max(hi1, c.Y);
            lo2 = Math.Min(lo2, c.Z); hi2 = Math.Max(hi2, c.Z);
        }
    }
    return new Outline(new XYZ(lo0, lo1, lo2), new XYZ(hi0, hi1, hi2));
};

// ---------- the test ----------
var clashes = new List<Tuple<ElementId, ElementId>>();

if (setAIds.Count > 0)
{
    foreach (var bId in setBIds)
    {
        var bElement = bDoc.GetElement(bId);

        if (!linkInstanceIdInt.HasValue)
        {
            // SAME DOCUMENT — Revit's own element-to-element filter, with a quick box filter in front.
            ICollection<ElementId> hits;
            try
            {
                var box = bElement.get_BoundingBox(null);
                var coll = new FilteredElementCollector(Document, setAIds);
                if (box != null) coll = coll.WherePasses(new BoundingBoxIntersectsFilter(new Outline(box.Min, box.Max)));
                hits = coll.WherePasses(new ElementIntersectsElementFilter(bElement)).ToElementIds();
            }
            catch (Exception ex)
            {
                // Should not happen now the support check runs first — but if it does, it is REPORTED,
                // never swallowed, because a silent skip is what this fragment was fixed to stop doing.
                untested.Add(Tuple.Create(bId, "B", "intersection test threw: " + ex.Message));
                continue;
            }
            foreach (var aId in hits) { if (aId != bId) clashes.Add(Tuple.Create(aId, bId)); }
        }
        else
        {
            // LINKED DOCUMENT — the element's solids are brought into THIS model's coordinates and tested
            // with ElementIntersectsSolidFilter. ElementIntersectsElementFilter cannot cross documents.
            var raw = solidsOf(bElement);
            if (raw.Count == 0) { untested.Add(Tuple.Create(bId, "B", "linked element has no usable solid geometry")); continue; }
            var seen = new HashSet<ElementId>();
            bool anyTested = false;
            foreach (var s in raw)
            {
                Solid moved;
                try { moved = SolidUtils.CreateTransformed(s, linkTf); }
                catch { continue; }
                var ol = outlineOf(moved);
                if (ol == null) continue;
                try
                {
                    var hits = new FilteredElementCollector(Document, setAIds)
                        .WherePasses(new BoundingBoxIntersectsFilter(ol))
                        .WherePasses(new ElementIntersectsSolidFilter(moved))
                        .ToElementIds();
                    anyTested = true;
                    foreach (var aId in hits) if (seen.Add(aId)) clashes.Add(Tuple.Create(aId, bId));
                }
                catch (Exception ex) { untested.Add(Tuple.Create(bId, "B", "solid intersection threw: " + ex.Message)); }
            }
            if (!anyTested && raw.Count > 0)
                untested.Add(Tuple.Create(bId, "B", "none of this linked element's solids could be transformed or tested"));
        }
    }
}

// ---------- output ----------
string whereB = linkInstanceIdInt.HasValue ? $"linked model '{bDoc.Title}'" : "this model";
sb.AppendLine($"Tested {setAIds.Count} of {elements.Count} element(s) in set A against {setBIds.Count} of {setBAll.Count} element(s) of category {targetCategoryB} in {whereB} (set B): {clashes.Count} clashing pair(s).");
if (!linkInstanceIdInt.HasValue)
    sb.AppendLine("NOTE: set B was read from THIS model. On an MEP job the structure is usually in a LINK — set linkInstanceIdInt, or this checks nothing that matters.");
if (untested.Count > 0)
    sb.AppendLine($"⚠ {untested.Count} element(s) COULD NOT BE TESTED and are NOT covered by the result below — listed at the end.");
if (setAIds.Count == 0)
    sb.AppendLine("Nothing in set A could be geometrically tested — the result above is not a clean result, it is an empty one.");
sb.AppendLine("Set A Id | Set B Id");
sb.AppendLine("--- | ---");
foreach (var pair in clashes.Take(maxPairsReported))
    sb.AppendLine($"{pair.Item1} | {pair.Item2}");
if (clashes.Count > maxPairsReported) sb.AppendLine($"... {clashes.Count - maxPairsReported} more pair(s) not shown.");

if (untested.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"UNTESTED — {untested.Count} element(s) Revit cannot check for solid intersection:");
    sb.AppendLine("Set | Id | Category | Reason");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var u in untested.Take(maxPairsReported))
    {
        var el = (u.Item2 == "B" ? bDoc : Document).GetElement(u.Item1);
        string cat = el != null && el.Category != null ? el.Category.Name : "(no category)";
        sb.AppendLine($"{u.Item2} | {u.Item1} | {cat} | {u.Item3}");
    }
    if (untested.Count > maxPairsReported) sb.AppendLine($"... {untested.Count - maxPairsReported} more not shown.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
