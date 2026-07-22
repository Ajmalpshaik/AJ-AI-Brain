// ============================================================
// FRAGMENT (action) — action-remove-revision-from-sheet.cs
// PURPOSE: Detach one or more named Revisions from every sheet in `elements` (from filter-by-sheets.cs)
//          — the reverse of action-assign-revisions-by-sheet-date.cs, which only ever adds. Matched by
//          Revision.Description (the same field the Filters dialog and most schedules show), not by
//          RevisionDate — cleaner when you know which issue you're pulling back, not which date it fell on.
// CONSUMES: elements (List<Element>, each really a ViewSheet — from filter-by-sheets.cs)
// PRODUCES: sb (StringBuilder) — appends the report; add `return sb.ToString();` after this fragment.
// WRITES the model — wrapped in a Transaction with RollBack on failure.
// GOTCHA (see ../../../knowledge/live-model/revisions.md): removing the last sheet reference to a
// revision with no cloud can trigger Revit to silently purge it project-wide the next time something
// touches sheet-revision association — same caveat as action-assign-revisions-by-sheet-date.cs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] revisionDescriptionsToRemove = { };
// ---- END INPUTS ----

var revisionIds = Revision.GetAllRevisionIds(Document);
var idsToRemove = new HashSet<ElementId>();
foreach (var id in revisionIds)
{
    var rev = Document.GetElement(id) as Revision;
    if (rev != null && revisionDescriptionsToRemove.Any(d => d.Equals(rev.Description, StringComparison.OrdinalIgnoreCase)))
        idsToRemove.Add(id);
}

using (var t = new Transaction(Document, "AJ Tools - Remove Revision From Sheet"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var sheet = el as ViewSheet;
            if (sheet == null) continue;

            var current = new HashSet<ElementId>(sheet.GetAdditionalRevisionIds());
            int before = current.Count;
            current.ExceptWith(idsToRemove);
            if (current.Count != before)
            {
                sheet.SetAdditionalRevisionIds(current.ToList());
                sb.AppendLine($"{sheet.SheetNumber} - {sheet.Name}: removed {before - current.Count} revision(s).");
            }
        }
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine("FAILED, rolled back: " + ex.Message);
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
