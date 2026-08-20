// ============================================================
// SCRIPT (context) — context-design-options.cs
// PURPOSE: List every Design Option in the document — name, Id, and whether it's the Primary option in
//          its set. Read-only, zero input, safe to call anytime. Orientation step before using
//          filter-by-design-option.cs or action-set-design-option.cs, same role
//          context-workset-info.cs plays for worksets.
// ============================================================

var sb = new System.Text.StringBuilder();
var options = new FilteredElementCollector(Document).OfClass(typeof(DesignOption)).Cast<DesignOption>().ToList();

if (options.Count == 0)
{
    sb.AppendLine("No Design Options in this document (everything is in the Main Model).");
}
else
{
    sb.AppendLine($"Design Options ({options.Count}):");
    foreach (var o in options.OrderBy(o => o.Name))
        sb.AppendLine($"  - {o.Name} (Id {o.Id}){(o.IsPrimary ? " [Primary]" : "")}");
}
return sb.ToString();
