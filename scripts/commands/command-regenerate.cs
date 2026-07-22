// ============================================================
// SCRIPT: command-regenerate.cs
// PURPOSE: Force Document.Regenerate() — useful after a composed script chains several actions where a
//          later one depends on geometry/parameters the earlier one just changed (new elements' real
//          bounding boxes, a just-duplicated type's properties, etc.) not yet reflected mid-transaction.
// ============================================================

var sb = new System.Text.StringBuilder();
try
{
    Document.Regenerate();
    sb.AppendLine("Document regenerated.");
}
catch (Exception ex)
{
    sb.AppendLine($"FAILED to regenerate. Reason: {ex.Message}");
}
return sb.ToString();
