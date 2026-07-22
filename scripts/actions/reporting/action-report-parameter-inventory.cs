// ============================================================
// FRAGMENT (action) — action-report-parameter-inventory.cs
// PURPOSE: Discover every parameter an element ACTUALLY HAS — name, storage type, Instance vs Type,
//          kind (Built-in/System, Shared, or Project/Family — see the honesty note below), parameter
//          group, read-only flag, and its current value. Different job from action-report-parameters.cs:
//          that one reports VALUES for parameter names you already know; this one answers "what
//          parameters does this duct/category even have" BEFORE you know the names — the exact discovery
//          step ../../../knowledge/live-model/core.md already calls for ("Discover a category's real
//          parameter names/IDs before bulk reading or writing on it, don't guess from a plausible name").
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// HONESTY NOTE: Revit's public API cannot reliably distinguish a "Family parameter" (defined inside the
// Family Editor) from a "Project parameter" (added via Manage > Project Parameters) once both are loaded
// onto a project element — neither is Shared (p.IsShared == false) nor tied to a BuiltInParameter, and
// there's no queryable provenance flag differentiating the two. Kind is reported as "Built-in", "Shared",
// or "Project/Family (not shared)" — the last bucket genuinely can't be split further from this API.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool sampleOnly = true; // true = inventory just the FIRST element (fast, representative); false = union across every element in `elements` (catches variation between different families/types in a mixed set)
bool includeTypeParameters = true;
int maxRows = 200;
// ---- END INPUTS ----

Func<Parameter, string> kindOf = p =>
{
    if (p.Definition is InternalDefinition internalDef && internalDef.BuiltInParameter != BuiltInParameter.INVALID)
        return "Built-in";
    if (p.IsShared) return "Shared";
    return "Project/Family (not shared)";
};

Func<Parameter, string> valueText = p =>
{
    if (!p.HasValue) return "";
    switch (p.StorageType)
    {
        case StorageType.String: return p.AsString() ?? p.AsValueString() ?? "";
        case StorageType.Integer: return p.AsValueString() ?? p.AsInteger().ToString();
        case StorageType.Double: return p.AsValueString() ?? p.AsDouble().ToString("0.###");
        case StorageType.ElementId:
            var id = p.AsElementId();
            if (id == null || id == ElementId.InvalidElementId) return "";
            return Document.GetElement(id)?.Name ?? ("#" + id.IntegerValue);
        default: return "";
    }
};

var rows = new List<(string source, string name, string kind, string group, string storage, bool readOnly, string value)>();
var seenKeys = new HashSet<string>();

Action<Element, string> collectFrom = (el, source) =>
{
    foreach (Parameter p in el.Parameters)
    {
        string key = source + "|" + p.Definition.Name;
        if (!seenKeys.Add(key)) continue;
        rows.Add((source, p.Definition.Name, kindOf(p), p.Definition.ParameterGroup.ToString(), p.StorageType.ToString(), p.IsReadOnly, valueText(p)));
    }
};

var sampleElements = sampleOnly ? elements.Take(1).ToList() : elements;

foreach (var el in sampleElements)
{
    collectFrom(el, "Instance");
    if (includeTypeParameters)
    {
        var type = Document.GetElement(el.GetTypeId()) as ElementType;
        if (type != null) collectFrom(type, "Type");
    }
}

sb.AppendLine($"Parameter inventory from {sampleElements.Count} element(s) ({(sampleOnly ? "sample" : "union")}), {rows.Count} distinct parameter(s):");
sb.AppendLine("Source | Name | Kind | Group | Storage | ReadOnly | Value");
sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");
foreach (var r in rows.OrderBy(r => r.source).ThenBy(r => r.name).Take(maxRows))
{
    sb.AppendLine($"{r.source} | {r.name} | {r.kind} | {r.group} | {r.storage} | {r.readOnly} | {r.value}");
}
if (rows.Count > maxRows) sb.AppendLine($"... {rows.Count - maxRows} more parameter(s) not shown.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
