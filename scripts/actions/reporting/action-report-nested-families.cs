// ============================================================
// FRAGMENT (action) — action-report-nested-families.cs
// PURPOSE: Group `elements` by the WHOLE PIECE OF KIT they belong to, not by the family name they
//          happen to carry. Walks nested families UP to the outermost host and DOWN to every nested
//          part, and reports how far a plain count is from the number of real units. Answers Ajmal's
//          "four different family names can be one piece of kit", and the question behind it —
//          "how many AHUs do I actually have, not how many mechanical equipment elements".
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// READ-ONLY — opens no transaction, changes nothing.
//
// ✱✱ WHY THIS EXISTS — A COUNT OF NESTED EQUIPMENT IS NOT A COUNT OF EQUIPMENT.
//    An AHU family with a nested fan, coil and filter is FOUR family instances in the same category.
//    `action-count-and-report.cs` and every schedule that counts instances return 4. There is one unit
//    on site. The same happens to any assembled kit — a pump set, a packaged unit, a valve set, a
//    light fitting with a nested emergency module. Until now this library could walk DOWN
//    (`GetSubComponentIds`, used by two fragments) but never UP: `FamilyInstance.SuperComponent`
//    appeared in NO fragment, so nothing could say which instance was the real one.
//
// ✱✱ INSULATION AND LINING ARE THE SAME PROBLEM WEARING A DIFFERENT HAT. `DuctInsulation`,
//    `DuctLining` and `PipeInsulation` are separate ELEMENTS that belong to a host, and they inflate
//    a count in exactly the same way. They derive from `InsulationLiningBase`, whose `HostElementId`
//    is the "SuperComponent" of a wrap, so the walk follows both relationships. That is the same fact
//    knowledge/live-model/insulation-follows-host.md records for colouring, applied to counting.
//
// ✱✱ THE WALK IS GUARDED AGAINST A CYCLE. Nothing should ever be its own ancestor, but a corrupt or
//    half-copied family has produced one before, and an unguarded while-loop on SuperComponent hangs
//    Revit rather than erroring. Every id visited is remembered and the walk stops if one repeats;
//    that element is reported as CYCLE rather than silently attached to the wrong root.
//
// GOTCHA: a root's nested parts may be in `elements` or may not, depending on the filter above. Both
//         are handled — the parts found through the model are reported even when the filter missed
//         them, and the row says which parts came from the given set.
// GOTCHA: THE NESTED PART CAN BE IN A DIFFERENT CATEGORY FROM ITS HOST, which is exactly why a
//         category filter can catch a nested fan and miss the AHU it sits inside. When a root is
//         outside `elements`, the row still names it, so the miss is visible.
// GOTCHA: a SHARED nested family is a real, independent instance by design — it is meant to schedule
//         and tag on its own. This fragment does not know shared from non-shared at the instance
//         level, so it reports the nesting and leaves the judgement to you. The count difference is
//         the flag, not the verdict.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only.
//    Check one known assembly — an AHU or a packaged unit — against what you see in Revit first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool listRootsWithNoNesting = false;  // false = only show the assemblies that actually have parts
int maxRootsListed = 60;
bool includeWraps = true;             // count insulation/lining as parts of their host
// ---- END INPUTS ----

// ---------- walk up to the outermost host ----------
Func<Element, Tuple<Element, bool>> rootOf = el =>
{
    var seen = new HashSet<ElementId>();
    var current = el;
    bool cycle = false;
    while (current != null)
    {
        if (!seen.Add(current.Id)) { cycle = true; break; }

        var fi = current as FamilyInstance;
        if (fi != null)
        {
            Element super = null;
            try { super = fi.SuperComponent; } catch { }
            if (super != null) { current = super; continue; }
        }

        if (includeWraps)
        {
            var wrap = current as InsulationLiningBase;
            if (wrap != null)
            {
                Element host = null;
                try { host = Document.GetElement(wrap.HostElementId); } catch { }
                if (host != null) { current = host; continue; }
            }
        }
        break;
    }
    return Tuple.Create(current, cycle);
};

// ---------- everything below a root, however deep ----------
Func<Element, List<Element>> partsOf = root =>
{
    var parts = new List<Element>();
    var seen = new HashSet<ElementId>();
    var queue = new Queue<Element>();
    queue.Enqueue(root);
    seen.Add(root.Id);
    while (queue.Count > 0)
    {
        var cur = queue.Dequeue();

        var fi = cur as FamilyInstance;
        if (fi != null)
        {
            ICollection<ElementId> subs = null;
            try { subs = fi.GetSubComponentIds(); } catch { }
            if (subs != null)
                foreach (var id in subs)
                {
                    if (!seen.Add(id)) continue;
                    var sub = Document.GetElement(id);
                    if (sub != null) { parts.Add(sub); queue.Enqueue(sub); }
                }
        }

        if (includeWraps)
        {
            try
            {
                var wrapIds = new List<ElementId>();
                var ins = InsulationLiningBase.GetInsulationIds(Document, cur.Id);
                if (ins != null) wrapIds.AddRange(ins);
                var lin = InsulationLiningBase.GetLiningIds(Document, cur.Id);
                if (lin != null) wrapIds.AddRange(lin);
                foreach (var id in wrapIds)
                {
                    if (!seen.Add(id)) continue;
                    var w = Document.GetElement(id);
                    if (w != null) { parts.Add(w); queue.Enqueue(w); }
                }
            }
            catch { }
        }
    }
    return parts;
};

// ---------- group the given set by root ----------
var givenIds = new HashSet<ElementId>();
foreach (var e in elements) if (e != null) givenIds.Add(e.Id);

var rootsOrder = new List<ElementId>();
var rootEl = new Dictionary<ElementId, Element>();
var givenUnderRoot = new Dictionary<ElementId, List<Element>>();
int cycles = 0;

foreach (var e in elements)
{
    if (e == null) continue;
    var r = rootOf(e);
    if (r.Item2) cycles++;
    var root = r.Item1 ?? e;
    if (!rootEl.ContainsKey(root.Id))
    {
        rootEl[root.Id] = root;
        givenUnderRoot[root.Id] = new List<Element>();
        rootsOrder.Add(root.Id);
    }
    givenUnderRoot[root.Id].Add(e);
}

// ---------- report ----------
int rootsWithParts = 0, totalParts = 0, rootsOutsideSet = 0;
var rows = new List<string>();

foreach (var rid in rootsOrder)
{
    var root = rootEl[rid];
    var parts = partsOf(root);
    int inSet = givenUnderRoot[rid].Count(g => g.Id != rid);
    bool rootWasGiven = givenIds.Contains(rid);
    if (!rootWasGiven) rootsOutsideSet++;
    if (parts.Count > 0) { rootsWithParts++; totalParts += parts.Count; }

    if (parts.Count == 0 && !listRootsWithNoNesting) continue;

    var byCat = parts
        .GroupBy(p => p.Category != null ? p.Category.Name : "(no category)")
        .OrderByDescending(g => g.Count())
        .Select(g => $"{g.Count()} x {g.Key}");

    string rootCat = root.Category != null ? root.Category.Name : "(no category)";
    string flag = rootWasGiven ? "" : " ** ROOT NOT IN THE GIVEN SET **";
    rows.Add($"{root.Id} | {root.Name} | {rootCat} | {parts.Count} | {inSet} | {string.Join(", ", byCat)}{flag}");
}

sb.AppendLine($"NESTED FAMILIES — {elements.Count} element(s) given, {rootsOrder.Count} distinct piece(s) of kit");
sb.AppendLine();

int difference = elements.Count - rootsOrder.Count;
if (difference > 0)
{
    sb.AppendLine($"*** A PLAIN COUNT OVERSTATES THIS SET BY {difference}. {elements.Count} elements are"
        + $" {rootsOrder.Count} actual units — the rest are nested parts and wraps of those units.");
    sb.AppendLine("    Any schedule or count over this set carries the same overstatement.");
}
else
{
    sb.AppendLine($"No nesting found: all {elements.Count} element(s) are their own root, so a plain count is correct here.");
}

if (rootsOutsideSet > 0)
{
    sb.AppendLine($"*** {rootsOutsideSet} of those units are NOT THEMSELVES IN THE GIVEN SET — the filter above caught");
    sb.AppendLine("    a nested part and missed the thing it is part of. A nested family can be in a different");
    sb.AppendLine("    category from its host, which is how that happens. Those rows are marked below.");
}
if (cycles > 0)
    sb.AppendLine($"⚠ {cycles} element(s) hit a CYCLE walking up to their host — reported against the last element reached, not silently reattached.");

sb.AppendLine();
if (rows.Count == 0)
{
    sb.AppendLine("(no assemblies with nested parts — set listRootsWithNoNesting = true to list every root)");
}
else
{
    sb.AppendLine($"{rootsWithParts} assembl(y/ies) with {totalParts} nested part(s) in total.");
    sb.AppendLine();
    sb.AppendLine("Root Id | Root name | Root category | Parts | Of which in the given set | What the parts are");
    sb.AppendLine("--- | --- | --- | ---: | ---: | ---");
    foreach (var r in rows.Take(maxRootsListed)) sb.AppendLine(r);
    if (rows.Count > maxRootsListed)
        sb.AppendLine($"... {rows.Count - maxRootsListed} more not listed (raise maxRootsListed).");
}

sb.AppendLine();
sb.AppendLine("A SHARED nested family is meant to count on its own — this reports the nesting, it does not");
sb.AppendLine("decide whether a part should have been counted. The difference above is the flag to check.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
