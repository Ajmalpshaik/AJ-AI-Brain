// ============================================================
// FRAGMENT (action) — action-copy-from-link.cs
// PURPOSE: Copy elements FROM a linked RVT model INTO this host model, placed at their true linked
//          position (the link's full transform applied) — "bring those walls/fixtures from the arch link
//          into our model". (Dynamo-package equivalent: archi-lab/Orchid cross-document copy nodes.)
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — the source Ids live in the LINKED
//          document, found via filter-by-linked-model-elements.cs (its report includes them).
// PRODUCES: elements (List<Element>, the new HOST-model copies) for chaining.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: sourceElementIdInts are Ids IN THE LINKED DOCUMENT — a host-model Id here silently copies the
//         wrong thing or fails; always take them from filter-by-linked-model-elements.cs output.
// GOTCHA: copies are plain host elements afterwards — no live connection to the link (they will NOT
//         update when the link updates). Say this to the user every time; it changes what they're asking for.
// BLOCKED (0 links in this model) — graceful path only; NOT YET LIVE-VERIFIED
//          (created 2026-07-26, round 3, Dynamo package harvest).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int linkInstanceIdInt = 0;              // the RevitLinkInstance (from filter-by-links.cs)
int[] sourceElementIdInts = new int[] { }; // element Ids IN THE LINKED DOC (see GOTCHA)
// ---- END INPUTS ----

var elements = new List<Element>();

var linkInst = Document.GetElement(new ElementId(linkInstanceIdInt)) as RevitLinkInstance;
if (linkInst == null)
{
    sb.AppendLine($"linkInstanceIdInt {linkInstanceIdInt} is not a RevitLinkInstance — find one via filter-by-links.cs.");
}
else
{
    var linkDoc = linkInst.GetLinkDocument();
    if (linkDoc == null)
    {
        sb.AppendLine($"Link '{linkInst.Name}' is not loaded — reload it first (action-reload-links.cs).");
    }
    else if (sourceElementIdInts.Length == 0)
    {
        sb.AppendLine("sourceElementIdInts is empty — nothing to copy.");
    }
    else
    {
        var srcIds = sourceElementIdInts.Select(i => new ElementId(i)).Where(id => linkDoc.GetElement(id) != null).ToList();
        int missing = sourceElementIdInts.Length - srcIds.Count;

        if (srcIds.Count == 0)
        {
            sb.AppendLine($"None of the {sourceElementIdInts.Length} Id(s) exist in linked doc '{linkDoc.Title}' — were they host-model Ids by mistake? (See GOTCHA.)");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Copy From Link"))
            {
                t.Start();
                try
                {
                    var newIds = ElementTransformUtils.CopyElements(
                        linkDoc, srcIds, Document, linkInst.GetTotalTransform(), new CopyPasteOptions());
                    foreach (var id in newIds)
                    {
                        var e = Document.GetElement(id);
                        if (e != null) elements.Add(e);
                    }
                    t.Commit();
                    sb.AppendLine($"Copied {newIds.Count} element(s) from link '{linkDoc.Title}' into this model{(missing > 0 ? $" ({missing} input Id(s) not found in the link, skipped)" : "")}.");
                    sb.AppendLine($"  New host Ids: {string.Join(", ", elements.Take(30).Select(e => e.Id.IntegerValue.ToString()))}{(elements.Count > 30 ? ", ..." : "")}");
                    sb.AppendLine("  NOTE: these copies will NOT update when the link updates — they're independent host elements now.");
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"FAILED to copy from link — rolled back, nothing changed. Reason: {ex.Message}");
                    elements = new List<Element>();
                }
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
