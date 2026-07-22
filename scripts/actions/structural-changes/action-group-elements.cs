// ============================================================
// FRAGMENT (action) — action-group-elements.cs
// PURPOSE: Bundle every element in `elements` into a new Model Group — e.g. a repeated toilet-room
//          block or an FCU-and-terminals cluster, so it can be copied/moved/mirrored as one unit.
//          Model Groups are their own category (OST_IOSModelGroups) — filter-by-category.cs already
//          finds them for later operations, no dedicated filter needed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string groupName = ""; // "" = keep Revit's default auto-generated name (e.g. "Group 1")
// ---- END INPUTS ----

var ids = elements.Select(e => e.Id).ToList();

using (var t = new Transaction(Document, "AJ Tools - Group Elements"))
{
    t.Start();
    try
    {
        Group group = Document.Create.NewGroup(ids);
        if (!string.IsNullOrEmpty(groupName)) group.GroupType.Name = groupName;
        t.Commit();
        sb.AppendLine($"Grouped {ids.Count} element(s) into new group '{group.GroupType.Name}' (Id {group.Id.IntegerValue}).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to group — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
