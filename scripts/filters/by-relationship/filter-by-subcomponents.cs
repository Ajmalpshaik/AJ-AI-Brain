// ============================================================
// FRAGMENT (filter) — filter-by-subcomponents.cs
// PURPOSE: Produce the NESTED sub-components of one or more parent FamilyInstances (shared nested
//          families inside MEP equipment, multi-part fixtures...) — the members a category filter never
//          finds because they hide inside a parent. (Dynamo-package equivalent: Clockwork's
//          Element.SubComponents.) Reverse direction of filter-by-host.cs (which finds hostED elements,
//          not nested members).
// PRODUCES: elements (List<Element>, the sub-components), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only FamilyInstance parents have sub-components; a parent with none contributes zero rows —
//         that's a valid empty result, not an error (empty-result-is-valid rule).
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 3 (Dynamo package harvest).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int[] parentIdInts = new int[] { }; // parent FamilyInstance Ids (from any other filter run first)
bool recursive = true;              // true = also dig into sub-components' own sub-components
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var toVisit = new Queue<ElementId>(parentIdInts.Select(i => new ElementId(i)));
var seen = new HashSet<int>();
int invalidParents = 0;

while (toVisit.Count > 0)
{
    var id = toVisit.Dequeue();
    var fi = Document.GetElement(id) as FamilyInstance;
    if (fi == null) { invalidParents++; continue; }

    foreach (var subId in fi.GetSubComponentIds())
    {
        if (!seen.Add(subId.IntegerValue)) continue;
        var sub = Document.GetElement(subId);
        if (sub == null) continue;
        elements.Add(sub);
        if (recursive) toVisit.Enqueue(subId);
    }
}

sb.AppendLine($"Sub-components: {elements.Count} found under {parentIdInts.Length} parent(s){(recursive ? " (recursive)" : "")}{(invalidParents > 0 ? $"; {invalidParents} input Id(s) were not FamilyInstances" : "")}.");
foreach (var e in elements.Take(30))
    sb.AppendLine($"  - '{e.Name}' (Id {e.Id}) — {e.Category?.Name ?? "?"}");
if (elements.Count > 30) sb.AppendLine($"  ... +{elements.Count - 30} more");
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
