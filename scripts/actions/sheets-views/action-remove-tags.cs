// ============================================================
// FRAGMENT (action) — action-remove-tags.cs
// PURPOSE: Delete every IndependentTag element in `elements` — the paired undo for
//          action-tag-elements.cs (and a cleanup for tags placed by the scored recipe). Only deletes the
//          tag annotation itself, never the tagged model element.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. a category filter scoped to a Tag category, or filter-by-current-selection.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ TWO FRAGMENTS DELETE, AND THE GUARD IS THE DIFFERENCE. action-remove-tags.cs deletes ONLY
//    IndependentTag annotations (a type check skips everything else) — "remove the tags", "clean the
//    tags off the view". actions/structural-changes/action-delete-elements.cs deletes WHATEVER the
//    filter handed it, model elements included, behind the allowDestructive gate — "delete these".
//    When the sentence is about tags, use this one: it cannot touch a model element even by mistake.
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
                failures.Add($"Id {e.Id}: {exOne.Message}");
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
