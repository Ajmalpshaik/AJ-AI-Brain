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

int updatedInstance = 0, updatedType = 0, skipped = 0, rejected = 0;
var rejectedIds = new List<ElementId>();

using (var t = new Transaction(Document, "AJ Tools - Set Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var (p, source) = resolveParam(e);
            if (p == null) { skipped++; continue; }

            // Parameter.Set() RETURNS A BOOL and Revit uses it to refuse a value it will not accept —
            // no exception, the old value simply stays. Ignoring it means reporting a write that never
            // happened (proved live 2026-08-07: Set(0.0) on a duct's Width returned false and left it
            // at 300 mm). Out-of-range numbers and invalid enum/text values fail exactly this way.
            bool ok;
            if (numericValueMm.HasValue && p.StorageType == StorageType.Double)
            {
                ok = p.Set(numericValueMm.Value / 304.8);
                if (ok) { if (source == "Type") updatedType++; else updatedInstance++; }
            }
            else if (stringValue != null && (p.StorageType == StorageType.String))
            {
                ok = p.Set(stringValue);
                if (ok) { if (source == "Type") updatedType++; else updatedInstance++; }
            }
            else
            {
                skipped++;
                continue;
            }
            if (!ok) { rejected++; if (rejectedIds.Count < 20) rejectedIds.Add(e.Id); }
        }
        t.Commit();
        sb.AppendLine($"Set '{parameterName}' on {updatedInstance} element(s) at Instance level" +
            (updatedType > 0 ? $", {updatedType} at Type level (applies to every instance sharing that type)" : "") +
            $", skipped {skipped} (read-only, missing, or wrong type).");
        if (rejected > 0)
            sb.AppendLine($"  {rejected} REFUSED THE VALUE — Revit's Set() returned false and the old value is still there (out of range, or not valid for this parameter). Unchanged Id(s): {string.Join(", ", rejectedIds)}{(rejected > rejectedIds.Count ? ", ..." : "")}");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
