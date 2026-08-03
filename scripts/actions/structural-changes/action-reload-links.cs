// ============================================================
// FRAGMENT (action) — action-reload-links.cs
// PURPOSE: Reload every distinct RVT link TYPE behind the link instance(s) in `elements` (from
//          filter-by-links.cs) — pulls in the latest saved version of a coordination link from disk,
//          Revit's own "Reload" in Manage Links, scripted. Dedupes by link Type so one file shared by
//          several instances only reloads once.
// ASSUMES: elements (List<Element>, each really a RevitLinkInstance — from filters/by-relationship/filter-by-links.cs) and
//          sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: Reload reads from the file path Revit already has stored for that link (or its last-known Model
//         Path for a workshared central) — it does NOT let you point at a different file; use
//         RevitLinkType.LoadFrom for that, not covered here.
// GOTCHA: no Transaction wraps this — Reload is a document-management I/O operation, not a regular model
//         edit (same reasoning as action-export-schedule-to-csv.cs not needing one).
// NOT YET LIVE-VERIFIED against a real link — test on one link first before trusting it on a batch.
// RESOLVED 2026-08-04 (live, Revit 2020): the 2026-07-23 flag was correct — `LinkLoadResultType.LinkNotNeeded`
// does NOT exist and this fragment could never have compiled ("error CS0117: 'LinkLoadResultType' does not
// contain a definition for 'LinkNotNeeded'"). The enum's 13 real members are: Uninitialized, LinkLoaded,
// LinkNotFound, LinkNotOpenable, LinkOpenAsHost, SameModelAsHost, SameCentralModelAsHost,
// LinkNotLoadedOtherError, LinkMayBeUpgraded, ExternalServerMissing, LinkExists, CouldNotChangeViewReference,
// UsedExisting. `UsedExisting` is the closest in meaning to the intended "already current, nothing to do"
// — it is counted as success below, but which value `Reload()` ACTUALLY returns for an up-to-date link is
// still UNVERIFIED (needs a model with a real link; this one had none). The raw `result.LoadResult` is
// printed for every link precisely so the first real run reveals the truth instead of hiding it.
// ============================================================

var linkTypeIds = elements
    .OfType<RevitLinkInstance>()
    .Select(li => li.GetTypeId())
    .Distinct()
    .ToList();

int reloaded = 0, skipped = 0;
var results = new List<string>();

foreach (var typeId in linkTypeIds)
{
    var linkType = Document.GetElement(typeId) as RevitLinkType;
    if (linkType == null) { skipped++; continue; }

    try
    {
        var result = linkType.Reload();
        results.Add($"'{linkType.Name}': {result.LoadResult}");
        if (result.LoadResult == LinkLoadResultType.LinkLoaded || result.LoadResult == LinkLoadResultType.UsedExisting)
            reloaded++;
        else
            skipped++;
    }
    catch (Exception ex)
    {
        skipped++;
        results.Add($"'{linkType.Name}': FAILED — {ex.Message}");
    }
}

sb.AppendLine($"Reloaded {reloaded} link(s), {skipped} skipped, out of {linkTypeIds.Count} distinct link type(s) from {elements.Count} instance(s).");
if (results.Count > 0) sb.AppendLine(string.Join("; ", results));
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
