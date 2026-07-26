// ============================================================
// FRAGMENT (filter) — filter-by-group.cs
// PURPOSE: Every member element of a specific Model Group instance — for "everything inside this
//          group", e.g. a repeated toilet-room block or a typical FCU-and-terminals cluster.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int groupElementIdInt = 0; // set this OR groupName below — Id is checked first if both are set
string groupName = "";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

Group group = null;
if (groupElementIdInt != 0)
{
    group = Document.GetElement(new ElementId(groupElementIdInt)) as Group;
}
if (group == null && !string.IsNullOrEmpty(groupName))
{
    group = new FilteredElementCollector(Document)
        .OfClass(typeof(Group))
        .Cast<Group>()
        .FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
}

if (group == null)
{
    sb.AppendLine("Group not found — set groupElementIdInt or groupName to a real Model Group instance.");
}
else
{
    elements = group.GetMemberIds()
        .Select(id => Document.GetElement(id))
        .Where(e => e != null)
        .ToList();

    sb.AppendLine($"Filtered {elements.Count} member element(s) of group '{group.Name}' (Id {group.Id.IntegerValue}).");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
