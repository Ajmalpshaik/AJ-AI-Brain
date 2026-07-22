// ============================================================
// FRAGMENT (action) — action-remove-tags.cs
// PURPOSE: Delete every IndependentTag element in `elements` — the paired undo for
//          action-tag-elements.cs (and a cleanup for tags placed by the scored recipe). Only deletes the
//          tag annotation itself, never the tagged model element.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. a category filter scoped to a Tag category, or filter-by-current-selection.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

int deleted = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Remove Tags"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            if (!(e is IndependentTag)) { skipped++; continue; }

            try
            {
                Document.Delete(e.Id);
                deleted++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id.IntegerValue}: {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Removed {deleted} tag(s), skipped {skipped} (not a tag, or couldn't delete).");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to remove tags — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
