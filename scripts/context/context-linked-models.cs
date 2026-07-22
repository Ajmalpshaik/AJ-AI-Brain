// ============================================================
// SCRIPT (context) — context-linked-models.cs
// PURPOSE: List every RVT link in the document — name, loaded/unloaded/not-found status, pinned, and
//          workset (if workshared). Read-only, zero input, safe to call anytime. Orientation step before
//          filter-by-links.cs, filter-by-linked-model-elements.cs, or action-reload-links.cs — same role
//          context-workset-info.cs plays for worksets.
// ============================================================

var sb = new System.Text.StringBuilder();
var linkInstances = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();

if (linkInstances.Count == 0)
{
    sb.AppendLine("No RVT links in this document.");
}
else
{
    sb.AppendLine($"RVT links ({linkInstances.Count}):");
    foreach (var li in linkInstances)
    {
        var linkType = Document.GetElement(li.GetTypeId()) as RevitLinkType;
        string status = linkType?.GetLinkedFileStatus().ToString() ?? "Unknown";
        string workset = "-";
        if (Document.IsWorkshared)
        {
            var ws = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).FirstOrDefault(w => w.Id == li.WorksetId);
            workset = ws?.Name ?? "(unknown)";
        }
        sb.AppendLine($"  - '{li.Name}' (Id {li.Id.IntegerValue}) — status: {status}, pinned: {li.Pinned}, workset: {workset}");
    }
}
return sb.ToString();
