// ============================================================
// FRAGMENT (action) — action-unload-remove-links.cs
// PURPOSE: Unload (keep the link, drop it from memory/view) or REMOVE (delete from the project entirely)
//          the distinct RVT link TYPE(s) behind the link instances in `elements` — the two Manage Links
//          operations action-reload-links.cs doesn't cover. Dedupe by Type, same as reload.
// ASSUMES: elements (List<Element>, each a RevitLinkInstance — from filters/by-relationship/filter-by-links.cs) and
//          sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: mode="unload" needs NO transaction (document-management I/O, same reasoning as
//         action-reload-links.cs); mode="remove" is a real model edit (Document.Delete) and runs inside
//         one — the two paths are structurally different on purpose.
// GOTCHA: mode="remove" is DESTRUCTIVE and not undoable by the link reappearing on its own — explorer-first
//         is mandatory, and the bridge call needs allowDestructive: true, same as action-delete-elements.cs.
// BLOCKED (0 links in this model) — graceful path only; both real paths NOT YET LIVE-VERIFIED
//          (created 2026-07-26 from the tool-gap backlog).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "unload";     // "unload" | "remove"
// ---- END INPUTS ----

var linkTypeIds = elements
    .OfType<RevitLinkInstance>()
    .Select(li => li.GetTypeId())
    .Distinct()
    .ToList();

if (linkTypeIds.Count == 0)
{
    sb.AppendLine("No RVT link instances in `elements` — nothing to do (feed this from filter-by-links.cs).");
}
else if (mode == "unload")
{
    int done = 0, failed = 0;
    var results = new List<string>();
    foreach (var typeId in linkTypeIds)
    {
        var linkType = Document.GetElement(typeId) as RevitLinkType;
        if (linkType == null) { failed++; continue; }
        try
        {
            linkType.Unload(null);
            done++;
            results.Add($"'{linkType.Name}': unloaded");
        }
        catch (Exception ex) { failed++; results.Add($"'{linkType.Name}': FAILED — {ex.Message}"); }
    }
    sb.AppendLine($"Unloaded {done} link type(s), {failed} failed, out of {linkTypeIds.Count}. Reload later via action-reload-links.cs.");
    if (results.Count > 0) sb.AppendLine(string.Join("; ", results));
}
else if (mode == "remove")
{
    using (var t = new Transaction(Document, "AJ Tools - Remove Links"))
    {
        t.Start();
        try
        {
            var names = linkTypeIds.Select(id => Document.GetElement(id)?.Name ?? id.ToString()).ToList();
            var deleted = Document.Delete(linkTypeIds);
            t.Commit();
            sb.AppendLine($"REMOVED {linkTypeIds.Count} link type(s) from the project ({deleted.Count} element(s) gone incl. their instances): {string.Join(", ", names.Select(n => $"'{n}'"))}.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to remove link(s) — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"unload\" or \"remove\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
