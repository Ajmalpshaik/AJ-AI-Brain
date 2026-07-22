// ============================================================
// FRAGMENT (action) — action-find-blank-parameter.cs
// PURPOSE: QA check — flag elements in `elements` where a named parameter is BLANK (no value, or an empty
//          string) — the standards-compliance sweep "which of these are missing Mark/Comments/whatever".
//          Falls back to the Type if the parameter isn't an instance parameter, same convention as
//          action-set-parameter-value.cs. Read-only: reports and can select, never writes anything.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Mark";
bool includeTypeParameters = true;
bool selectFlagged = true;
// ---- END INPUTS ----

Func<Element, (Parameter param, string source)> resolveParam = e =>
{
    var instP = e.LookupParameter(parameterName);
    if (instP != null) return (instP, "Instance");
    if (includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        var typeP = type?.LookupParameter(parameterName);
        if (typeP != null) return (typeP, "Type");
    }
    return (null, null);
};

Func<Parameter, bool> isBlank = p =>
{
    if (!p.HasValue) return true;
    switch (p.StorageType)
    {
        case StorageType.String: return string.IsNullOrWhiteSpace(p.AsString());
        case StorageType.ElementId: return p.AsElementId() == ElementId.InvalidElementId;
        default: return false; // a set numeric value of 0 is still a value, not "blank"
    }
};

var flagged = new List<Element>();
int notFound = 0;

foreach (var e in elements)
{
    var (p, source) = resolveParam(e);
    if (p == null) { notFound++; continue; }
    if (isBlank(p)) flagged.Add(e);
}

sb.AppendLine($"Checked {elements.Count} element(s) for blank '{parameterName}': {flagged.Count} blank, {notFound} don't even have this parameter.");
if (flagged.Count > 0)
    sb.AppendLine("Blank on Id: " + string.Join(", ", flagged.Select(e => e.Id.IntegerValue)));

if (selectFlagged && flagged.Count > 0)
{
    UIDocument.Selection.SetElementIds(flagged.Select(e => e.Id).ToList());
    sb.AppendLine($"Selected {flagged.Count} flagged element(s) in Revit for review.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
