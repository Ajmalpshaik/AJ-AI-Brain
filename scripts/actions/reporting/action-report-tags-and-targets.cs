// ============================================================
// FRAGMENT (action) — action-report-tags-and-targets.cs
// PURPOSE: Every tag in a view and WHAT IT IS ACTUALLY POINTING AT — including targets that live
//          inside a LINKED model, and tags that point at nothing at all. The "are my tags right"
//          check before a drawing goes out.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only: opens no Transaction and changes nothing.
//
// WHY THIS EXISTS — this library could only see HALF the tags on a coordination drawing:
//   `GetTaggedLocalElementIds()` returns only targets in THIS document. On an MEP job the
//   architecture is a link, so the room tags, door tags and anything tagged on the architect's model
//   come back as an EMPTY set from that call — not an error, just nothing. filter-by-tag-status.cs and
//   tag-elements-in-active-view.cs both use the local call, which is correct for what they do (they
//   ask about host elements), but it left the Brain with no way to answer "what is this tag on?" when
//   the answer is "something in the link".
//   `GetTaggedElementIds()` is the other half: it returns LinkElementId, which carries BOTH a link
//   instance id and the element id inside that link.
//
// THE VERSION SPLIT, and why it is reflection and not try/catch: before Revit 2022 a tag pointed at
//   exactly one element, so the API was the singular properties `TaggedElementId` /
//   `TaggedLocalElementId`. From 2022 a tag can point at several and those became
//   `GetTaggedElementIds()` / `GetTaggedLocalElementIds()`, with the old names REMOVED. Naming either
//   set directly picks a side and stops the fragment compiling on the other — and a try/catch does not
//   help, because a missing member is a COMPILE error, not a runtime one.
//
// WHAT AN ORPHANED TAG IS: a tag whose target has been deleted, or whose link has been unloaded. It
//   still draws — usually as a lone leader or a "?" — and it is the thing that gets noticed on the
//   printed sheet rather than on screen.
//
// RELATED: filters/by-view-and-sheet/filter-by-tag-status.cs (which HOST elements are not yet tagged),
//          recipes/tag-elements-in-active-view.cs (place the missing ones),
//          actions/sheets-views/action-center-room-tags.cs (tidy where they sit).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only, so the worst case is a wrong count rather than a wrong
//    model — but check one tag's reported target against Revit before trusting the totals.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null;      // null = active view; or an Element Id to report on any view
bool wholeModel = false;          // true = every tag in the document, ignoring the view
bool onlyProblems = false;        // true = list only orphaned tags and tags with no target
int maxRowsListed = 150;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;
if (!wholeModel && view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view. Set wholeModel = true to ignore the view.");
    return sb.ToString();
}

var tags = (wholeModel ? new FilteredElementCollector(Document) : new FilteredElementCollector(Document, view.Id))
    .OfClass(typeof(IndependentTag))
    .WhereElementIsNotElementType()
    .Cast<IndependentTag>()
    .ToList();

if (tags.Count == 0)
{
    sb.AppendLine(wholeModel ? "No independent tags in this document." : $"No independent tags in view '{view.Name}'.");
    return sb.ToString();
}

// ---- VERSION-PROOF TAG READ ----------------------------------------------------------------------
// Looked up by name once, here, so ONE source runs on Revit 2020 through 2027. See the header for why
// this cannot be a try/catch.
var _mAllIds   = typeof(IndependentTag).GetMethod("GetTaggedElementIds", Type.EmptyTypes);       // 2022+
var _mLocalIds = typeof(IndependentTag).GetMethod("GetTaggedLocalElementIds", Type.EmptyTypes);  // 2022+
var _pAllId    = typeof(IndependentTag).GetProperty("TaggedElementId");                          // pre-2022
var _pLocalId  = typeof(IndependentTag).GetProperty("TaggedLocalElementId");                     // pre-2022

if (_mAllIds == null && _pAllId == null)
{
    sb.AppendLine("This Revit version exposes neither GetTaggedElementIds nor TaggedElementId — cannot read tag targets here.");
    return sb.ToString();
}

// A LinkElementId is the pair (which link, which element inside it). For a host element the link half
// is InvalidElementId — that is how you tell the two apart, and it is the whole point of this call.
Func<object, Tuple<ElementId, ElementId>> SplitLinkId = o =>
{
    var lei = o as LinkElementId;
    if (lei == null) return Tuple.Create(ElementId.InvalidElementId, ElementId.InvalidElementId);
    return Tuple.Create(lei.LinkInstanceId, lei.LinkedElementId != ElementId.InvalidElementId ? lei.LinkedElementId : lei.HostElementId);
};

Func<IndependentTag, List<Tuple<ElementId, ElementId>>> TargetsOf = tg =>
{
    var outp = new List<Tuple<ElementId, ElementId>>();
    try
    {
        if (_mAllIds != null)
        {
            var raw = _mAllIds.Invoke(tg, null) as System.Collections.IEnumerable;
            if (raw != null) foreach (var o in raw) outp.Add(SplitLinkId(o));
        }
        else if (_pAllId != null)
        {
            outp.Add(SplitLinkId(_pAllId.GetValue(tg)));
        }
    }
    catch { }
    return outp;
};

// Link instance names, resolved once — a tag report that says "Id 418291" instead of "ARCH_Podium" is
// not a report anyone can act on.
var linkNames = new Dictionary<ElementId, string>();
foreach (var li in new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
{
    string n = "(unnamed link)";
    try { var lt = Document.GetElement(li.GetTypeId()) as RevitLinkType; if (lt != null) n = lt.Name ?? n; } catch { }
    linkNames[li.Id] = n;
}

int host = 0, linked = 0, orphan = 0, noTarget = 0, multi = 0;
var byLink = new Dictionary<string, int>();
var byCategory = new Dictionary<string, int>();
var rows = new List<string>();

foreach (var tg in tags)
{
    var targets = TargetsOf(tg);

    if (targets.Count == 0)
    {
        noTarget++;
        if (rows.Count < maxRowsListed)
            rows.Add($"  tag {tg.Id}  NO TARGET — points at nothing (target deleted, or the tag was never associated)");
        continue;
    }
    if (targets.Count > 1) multi++;

    foreach (var pair in targets)
    {
        var linkId = pair.Item1;
        var elemId = pair.Item2;

        if (linkId == ElementId.InvalidElementId)
        {
            // A host-document target.
            var e = Document.GetElement(elemId);
            if (e == null)
            {
                orphan++;
                if (rows.Count < maxRowsListed)
                    rows.Add($"  tag {tg.Id}  ORPHANED — host element {elemId} no longer exists");
                continue;
            }
            host++;
            string cat = e.Category != null ? (e.Category.Name ?? "(no category)") : "(no category)";
            if (!byCategory.ContainsKey(cat)) byCategory[cat] = 0;
            byCategory[cat]++;
            if (!onlyProblems && rows.Count < maxRowsListed)
                rows.Add($"  tag {tg.Id}  host    {cat}  '{e.Name}'  (element {elemId})");
        }
        else
        {
            // A target inside a link — the half the local-only call never sees.
            string lname = linkNames.ContainsKey(linkId) ? linkNames[linkId] : $"(link {linkId})";
            if (!byLink.ContainsKey(lname)) byLink[lname] = 0;
            byLink[lname]++;

            var li = Document.GetElement(linkId) as RevitLinkInstance;
            Document linkDoc = null;
            try { if (li != null) linkDoc = li.GetLinkDocument(); } catch { }

            if (linkDoc == null)
            {
                // The link is unloaded or the instance is gone — the tag still draws, pointing nowhere.
                orphan++;
                if (rows.Count < maxRowsListed)
                    rows.Add($"  tag {tg.Id}  ORPHANED — link '{lname}' is not loaded, so its target cannot be resolved");
                continue;
            }

            var le = linkDoc.GetElement(elemId);
            if (le == null)
            {
                orphan++;
                if (rows.Count < maxRowsListed)
                    rows.Add($"  tag {tg.Id}  ORPHANED — element {elemId} no longer exists inside link '{lname}'");
                continue;
            }
            linked++;
            string lcat = le.Category != null ? (le.Category.Name ?? "(no category)") : "(no category)";
            if (!byCategory.ContainsKey(lcat)) byCategory[lcat] = 0;
            byCategory[lcat]++;
            if (!onlyProblems && rows.Count < maxRowsListed)
                rows.Add($"  tag {tg.Id}  LINK    {lcat}  '{le.Name}'  (in '{lname}')");
        }
    }
}

sb.AppendLine($"TAGS AND THEIR TARGETS — {(wholeModel ? "whole model" : $"view '{view.Name}'")}, {tags.Count} tag(s).");
sb.AppendLine($"  {host} point at HOST elements, {linked} point INSIDE A LINK, {orphan} are ORPHANED, {noTarget} have no target at all.");
if (multi > 0) sb.AppendLine($"  {multi} tag(s) point at more than one element (Revit 2022+ allows this) — each target is counted separately above.");
if (_mAllIds == null)
    sb.AppendLine("  NOTE — this Revit version predates multi-reference tags, so each tag has exactly one target.");

if (linked > 0)
{
    sb.AppendLine();
    sb.AppendLine("TAGS POINTING INTO LINKS");
    foreach (var kv in byLink.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
}

if (byCategory.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("TAGGED ELEMENTS BY CATEGORY");
    foreach (var kv in byCategory.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
}

if (orphan > 0 || noTarget > 0)
{
    sb.AppendLine();
    sb.AppendLine($"! {orphan + noTarget} tag(s) resolve to nothing. They still DRAW on the sheet — usually as a lone leader");
    sb.AppendLine("  or a '?' — so they get noticed in print rather than on screen. Delete them, or reload the link they need.");
}

if (rows.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"DETAIL (first {rows.Count})");
    foreach (var r in rows) sb.AppendLine(r);
    if (host + linked + orphan + noTarget > rows.Count)
        sb.AppendLine($"  ... more not listed (raise maxRowsListed, or set onlyProblems = true).");
}

return sb.ToString();
