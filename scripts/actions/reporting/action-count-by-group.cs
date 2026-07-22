// ============================================================
// FRAGMENT (action) — action-count-by-group.cs
// PURPOSE: Count `elements`, broken down by the actual value of ANY named parameter — "count by Level",
//          "count by System Type", "count by Family", "count by Comments". action-count-and-report.cs's
//          breakdown table is hardcoded to size-related parameters (Width/Height/Diameter/...); this is
//          the general case for any other grouping. Read-only. Falls back to the element's TYPE if the
//          parameter isn't an instance parameter, same as the parameters-naming actions.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: storage-type-aware group-key logic reused from action-color-by-group.cs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string groupByParameterName = "Level"; // any parameter display name — "Level", "Comments", "System Type", "Family", "Family and Type", "Category", etc.
// ---- END INPUTS ----

Func<Element, string> groupKey = e =>
{
    if (string.Equals(groupByParameterName, "Family", StringComparison.OrdinalIgnoreCase))
    {
        if (e is FamilyInstance fi && fi.Symbol != null) return fi.Symbol.FamilyName ?? "None";
        return (Document.GetElement(e.GetTypeId()) as ElementType)?.FamilyName ?? "None";
    }
    if (string.Equals(groupByParameterName, "Family and Type", StringComparison.OrdinalIgnoreCase))
    {
        if (e is FamilyInstance fi && fi.Symbol != null) return $"{fi.Symbol.FamilyName} : {fi.Symbol.Name}";
        var t = Document.GetElement(e.GetTypeId()) as ElementType;
        return t != null ? $"{t.FamilyName} : {t.Name}" : "None";
    }
    if (string.Equals(groupByParameterName, "Category", StringComparison.OrdinalIgnoreCase))
        return e.Category?.Name ?? "None";

    Parameter p = e.LookupParameter(groupByParameterName);
    if (p == null || !p.HasValue)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        p = type?.LookupParameter(groupByParameterName);
    }
    if (p == null || !p.HasValue) return "None";

    switch (p.StorageType)
    {
        case StorageType.Double:
            return p.AsValueString() ?? p.AsDouble().ToString();
        case StorageType.ElementId:
            var id = p.AsElementId();
            if (id == ElementId.InvalidElementId) return "None";
            var refElem = Document.GetElement(id);
            return refElem?.Name ?? id.ToString();
        case StorageType.Integer:
            var valueString = p.AsValueString();
            if (!string.IsNullOrEmpty(valueString) && (valueString == "Yes" || valueString == "No")) return valueString;
            return p.AsValueString() ?? p.AsInteger().ToString();
        case StorageType.String:
            return p.AsString() ?? "None";
        default:
            return "None";
    }
};

var groups = elements.GroupBy(groupKey).OrderByDescending(g => g.Count()).ToList();

sb.AppendLine($"Count by '{groupByParameterName}', {elements.Count} element(s) total, {groups.Count} group(s):");
sb.AppendLine($"{groupByParameterName} | Qty");
sb.AppendLine("--- | ---");
foreach (var g in groups) sb.AppendLine($"{g.Key} | {g.Count()}");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
