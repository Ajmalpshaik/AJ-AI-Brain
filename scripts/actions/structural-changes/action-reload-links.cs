// ============================================================
// FRAGMENT (action) — action-reload-links.cs
// PURPOSE: Reload every distinct RVT link TYPE behind the link instance(s) in `elements` (from
//          filter-by-links.cs) — pulls in the latest saved version of a coordination link from disk,
//          Revit's own "Reload" in Manage Links, scripted. Dedupes by link Type so one file shared by
//          several instances only reloads once.
// ASSUMES: elements (List<Element>, each really a RevitLinkInstance — from filters/filter-by-links.cs) and
//          sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: Reload reads from the file path Revit already has stored for that link (or its last-known Model
//         Path for a workshared central) — it does NOT let you point at a different file; use
//         RevitLinkType.LoadFrom for that, not covered here.
// GOTCHA: no Transaction wraps this — Reload is a document-management I/O operation, not a regular model
//         edit (same reasoning as action-export-schedule-to-csv.cs not needing one).
// NOT YET LIVE-VERIFIED — test on one link first before trusting it on a batch.
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
        if (result.LoadResult == LinkLoadResultType.LinkLoaded || result.LoadResult == LinkLoadResultType.LinkNotNeeded)
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
