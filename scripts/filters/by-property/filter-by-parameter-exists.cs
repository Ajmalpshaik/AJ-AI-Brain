// ============================================================
// FRAGMENT (filter) — filter-by-parameter-exists.cs
// PURPOSE: Elements that HAVE a specific parameter attached, whether or not it holds a value — a QA
//          sweep ("find everything missing the Fire Rating parameter"). Different job from
//          filter-by-parameter-text.cs, which matches a parameter's VALUE, not just its presence.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
string parameterName = "Fire Rating";
bool includeTypeParameters = true;
// mode: "has" = parameter exists (any value, including blank) — "missing" = parameter exists but is
// blank/unset — "hasvalue" = parameter exists AND has a real value
string mode = "has";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<Element, Parameter> findParam = e =>
{
    var p = e.LookupParameter(parameterName);
    if (p == null && includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        p = type?.LookupParameter(parameterName);
    }
    return p;
};

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var p = findParam(e);
        if (p == null) return false;
        if (mode == "missing") return !p.HasValue;
        if (mode == "hasvalue") return p.HasValue;
        return true; // "has" — parameter exists at all
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory} where parameter '{parameterName}' {mode}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
