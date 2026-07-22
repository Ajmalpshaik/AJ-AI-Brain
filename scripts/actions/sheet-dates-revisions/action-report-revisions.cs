// ============================================================
// FRAGMENT (action) — action-report-revisions.cs
// PURPOSE: List every project Revision in order — sequence number, date, description, issued by/to,
//          issued flag, visibility. Read-only. The missing discovery step for action-edit-revision.cs and
//          action-delete-revision.cs, which both need an exact existing SequenceNumber to work from.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

var sb = new System.Text.StringBuilder();
var revisions = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
    .OrderBy(r => r.SequenceNumber).ToList();

sb.AppendLine($"{revisions.Count} revision(s), in order:");
sb.AppendLine("Seq | Date | Description | Issued By | Issued To | Issued | Visibility");
sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");
foreach (var r in revisions)
    sb.AppendLine($"{r.SequenceNumber} | {r.RevisionDate} | {r.Description} | {r.IssuedBy} | {r.IssuedTo} | {r.Issued} | {r.Visibility}");
return sb.ToString();
