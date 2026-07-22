// ============================================================
// FRAGMENT (creator) — create-revision.cs
// PURPOSE: Create one or more project-level Revisions (Manage > Revisions) directly, in the order given —
//          the plain, non-date-scanning version of recipes/create-revisions-from-sheet-dates.cs, for when
//          the revision date/description/issued-to/by are already known rather than mined from sheets.
// PRODUCES: elements (List<Element>, the newly created Revision(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so an action fragment (or action-assign-revisions-by-sheet-date.cs)
//          can chain onto it.
// ============================================================
// ORDER MATTERS: each Revision's SequenceNumber is assigned in creation order — pass revisionsToCreate
// oldest-first if the sequence should read chronologically.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(string date, string description, string issuedBy, string issuedTo)> revisionsToCreate = new List<(string, string, string, string)>
{
    ("22-Jul-2026", "IFI - ISSUED FOR INFORMATION", "", "")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Create Revisions"))
{
    t.Start();
    try
    {
        foreach (var (date, description, issuedBy, issuedTo) in revisionsToCreate)
        {
            var rev = Revision.Create(Document);
            rev.RevisionDate = date;
            rev.Description = description;
            if (!string.IsNullOrEmpty(issuedBy)) rev.IssuedBy = issuedBy;
            if (!string.IsNullOrEmpty(issuedTo)) rev.IssuedTo = issuedTo;
            elements.Add(rev);
        }
        t.Commit();
        sb.AppendLine($"Created {elements.Count} revision(s):");
        foreach (var r in elements.Cast<Revision>())
            sb.AppendLine($"  Seq {r.SequenceNumber}: {r.RevisionDate} | {r.Description}");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create revisions — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below (e.g. action-assign-revisions-by-sheet-date.cs), or add
//      return sb.ToString(); to stop here ----
