// ============================================================
// FRAGMENT (filter) — filter-by-design-option.cs
// PURPOSE: Elements belonging to one named Design Option instead of the Main Model — for comparing or
//          acting on a specific design alternative. Set designOptionName = "" to instead target the Main
//          Model (elements with no Design Option at all).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: not yet live-verified against a real model containing Design Options.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string designOptionName = ""; // "" = Main Model (elements with no Design Option); else exact option name
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

DesignOption targetOption = null;
bool optionResolved = string.IsNullOrEmpty(designOptionName);

if (!optionResolved)
{
    targetOption = new FilteredElementCollector(Document)
        .OfClass(typeof(DesignOption))
        .Cast<DesignOption>()
        .FirstOrDefault(o => o.Name.Equals(designOptionName, StringComparison.OrdinalIgnoreCase));

    if (targetOption == null)
    {
        var allOptions = new FilteredElementCollector(Document).OfClass(typeof(DesignOption)).Cast<DesignOption>();
        sb.AppendLine($"Design Option '{designOptionName}' not found. Available: {string.Join(", ", allOptions.Select(o => o.Name))}");
    }
    else
    {
        optionResolved = true;
    }
}

if (optionResolved)
{
    var collector = useCategoryFilter
        ? new FilteredElementCollector(Document).OfCategory(targetCategory)
        : new FilteredElementCollector(Document);

    elements = collector
        .WhereElementIsNotElementType()
        .Where(e => targetOption == null ? e.DesignOption == null : e.DesignOption?.Id == targetOption.Id)
        .ToList();

    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    string optionLabel = targetOption?.Name ?? "Main Model";
    sb.AppendLine($"Filtered {elements.Count} element(s) in Design Option '{optionLabel}', category: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
