// ============================================================
// FRAGMENT (action) — action-set-parameter-value.cs
// PURPOSE: Bulk-set one named parameter to one value across every element in `elements` — a generic
//          version of the Flow-parameter-refresh / any other bulk parameter edit. Falls back to the
//          element's TYPE if the parameter isn't an instance parameter (Manufacturer, Model, Type
//          Comments, and plenty of others genuinely live at the Type level) — matches
//          action-report-parameters.cs/action-report-parameter-inventory.cs, which already do this;
//          without the fallback, a Type-level name silently skipped every element with no clear reason.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// A Type-level edit changes the TYPE, so it applies to every instance sharing that type, not just the
// ones in `elements` — reported separately below so the count isn't misread as "this many instances each
// individually changed."
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Comments";
string stringValue = null;   // set this OR numericValueMm, not both
double? numericValueMm = null; // for Double-storage parameters, given in mm and converted internally
bool includeTypeParameters = true;
// ---- END INPUTS ----

Func<Element, (Parameter param, string source)> resolveParam = e =>
{
    var instP = e.LookupParameter(parameterName);
    if (instP != null && !instP.IsReadOnly) return (instP, "Instance");
    if (includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        var typeP = type?.LookupParameter(parameterName);
        if (typeP != null && !typeP.IsReadOnly) return (typeP, "Type");
    }
    return (null, null);
};

int updatedInstance = 0, updatedType = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Set Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var (p, source) = resolveParam(e);
            if (p == null) { skipped++; continue; }

            if (numericValueMm.HasValue && p.StorageType == StorageType.Double)
            {
                p.Set(UnitUtils.ConvertToInternalUnits(numericValueMm.Value, DisplayUnitType.DUT_MILLIMETERS));
                if (source == "Type") updatedType++; else updatedInstance++;
            }
            else if (stringValue != null && (p.StorageType == StorageType.String))
            {
                p.Set(stringValue);
                if (source == "Type") updatedType++; else updatedInstance++;
            }
            else
            {
                skipped++;
            }
        }
        t.Commit();
        sb.AppendLine($"Set '{parameterName}' on {updatedInstance} element(s) at Instance level" +
            (updatedType > 0 ? $", {updatedType} at Type level (applies to every instance sharing that type)" : "") +
            $", skipped {skipped} (read-only, missing, or wrong type).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
