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
string designOptionSetName = ""; // "" = don't care which set — but see the AMBIGUITY note below
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
// ---- END INPUTS ----

// AMBIGUITY (found live 2026-08-06): Revit names the primary option of EVERY option set
// "Option 1 (primary)", so on a model with 3 sets there are 3 options with identical names. The
// original `FirstOrDefault` on name silently picked whichever came first and reported success —
// the wrong-answer-with-confidence failure. This now refuses to guess: set designOptionSetName to
// disambiguate, or pass the option's Id via a different fragment.

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

DesignOption targetOption = null;
bool optionResolved = string.IsNullOrEmpty(designOptionName);

if (!optionResolved)
{
    Func<DesignOption, string> setNameOf = o =>
    {
        var p = o.get_Parameter(BuiltInParameter.OPTION_SET_ID);
        return p == null ? "" : (Document.GetElement(p.AsElementId())?.Name ?? "");
    };

    var byName = new FilteredElementCollector(Document)
        .OfClass(typeof(DesignOption))
        .Cast<DesignOption>()
        .Where(o => o.Name.Equals(designOptionName, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (!string.IsNullOrEmpty(designOptionSetName))
        byName = byName.Where(o => setNameOf(o).Equals(designOptionSetName, StringComparison.OrdinalIgnoreCase)).ToList();

    if (byName.Count == 0)
    {
        var allOptions = new FilteredElementCollector(Document).OfClass(typeof(DesignOption)).Cast<DesignOption>();
        sb.AppendLine($"Design Option '{designOptionName}'" +
            (string.IsNullOrEmpty(designOptionSetName) ? "" : $" in set '{designOptionSetName}'") +
            $" not found. Available: {string.Join(", ", allOptions.Select(o => $"'{o.Name}' (set '{setNameOf(o)}')"))}");
    }
    else if (byName.Count > 1)
    {
        // Do NOT pick one. Silently choosing here is how the wrong option gets filtered.
        sb.AppendLine($"AMBIGUOUS: {byName.Count} Design Options are named '{designOptionName}' — " +
            $"set designOptionSetName to one of: {string.Join(", ", byName.Select(o => $"'{setNameOf(o)}'"))}");
    }
    else
    {
        targetOption = byName[0];
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
