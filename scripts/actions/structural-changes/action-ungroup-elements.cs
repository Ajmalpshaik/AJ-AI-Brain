// ============================================================
// FRAGMENT (action) — action-ungroup-elements.cs
// PURPOSE: Dissolve every Group instance in `elements` back into its individual members — the paired
//          undo for action-group-elements.cs. Find the group instances first via filter-by-category.cs
//          (targetCategory = BuiltInCategory.OST_IOSModelGroups), or filter-by-id-list.cs if the Id is
//          already known. Non-Group elements in the set are skipped, not an error.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

int ungrouped = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Ungroup Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var group = e as Group;
            if (group == null) { skipped++; continue; }
            group.UngroupMembers();
            ungrouped++;
        }
        t.Commit();
        sb.AppendLine($"Ungrouped {ungrouped} group(s), skipped {skipped} (not a Group instance).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to ungroup — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
