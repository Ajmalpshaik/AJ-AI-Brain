// ============================================================
// FRAGMENT (action) — action-edit-revision.cs
// PURPOSE: Update one existing project Revision's fields — description, date, issued by/to, the issued
//          flag, and/or cloud/tag visibility. Any input left null is not touched. Completes the revision
//          Create/Edit/Delete lifecycle alongside creators/create-revision.cs.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// GOTCHA: matched by SequenceNumber (the number shown in Manage > Revisions), NOT by description or date
//         — run action-report-revisions.cs first if you don't already know it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int sequenceNumber = 1; // find this revision via action-report-revisions.cs first
string newDescription = null;
string newDate = null;
string newIssuedBy = null;
string newIssuedTo = null;
bool? newIssued = null;          // null = don't change
string newVisibility = null;     // "CloudAndTagVisible" | "TagVisible" | "Hidden"; null = don't change
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var revision = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
    .FirstOrDefault(r => r.SequenceNumber == sequenceNumber);

if (revision == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>().OrderBy(r => r.SequenceNumber);
    sb.AppendLine($"No revision with SequenceNumber {sequenceNumber}. Available: {string.Join(", ", available.Select(r => $"#{r.SequenceNumber} ({r.Description})"))}");
}
else
{
    var changed = new List<string>();
    var failures = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Edit Revision"))
    {
        t.Start();
        try
        {
            if (newDescription != null) { try { revision.Description = newDescription; changed.Add("Description"); } catch (Exception ex) { failures.Add($"Description: {ex.Message}"); } }
            if (newDate != null) { try { revision.RevisionDate = newDate; changed.Add("Date"); } catch (Exception ex) { failures.Add($"Date: {ex.Message}"); } }
            if (newIssuedBy != null) { try { revision.IssuedBy = newIssuedBy; changed.Add("IssuedBy"); } catch (Exception ex) { failures.Add($"IssuedBy: {ex.Message}"); } }
            if (newIssuedTo != null) { try { revision.IssuedTo = newIssuedTo; changed.Add("IssuedTo"); } catch (Exception ex) { failures.Add($"IssuedTo: {ex.Message}"); } }
            if (newIssued.HasValue) { try { revision.Issued = newIssued.Value; changed.Add("Issued"); } catch (Exception ex) { failures.Add($"Issued: {ex.Message}"); } }
            if (!string.IsNullOrEmpty(newVisibility) && Enum.TryParse<RevisionVisibility>(newVisibility, true, out var vis))
            {
                try { revision.Visibility = vis; changed.Add("Visibility"); } catch (Exception ex) { failures.Add($"Visibility: {ex.Message}"); }
            }

            t.Commit();
            sb.AppendLine($"Revision #{revision.SequenceNumber}: updated {string.Join(", ", changed)}" + (changed.Count == 0 ? "nothing — no fields set." : "."));
            if (failures.Count > 0) sb.AppendLine("Failed fields: " + string.Join("; ", failures));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to edit revision — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
