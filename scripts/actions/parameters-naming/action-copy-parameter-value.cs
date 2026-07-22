// ============================================================
// FRAGMENT (action) — action-copy-parameter-value.cs
// PURPOSE: Copy one parameter's value into a different parameter, across every element in `elements` —
//          e.g. stamp Type Mark into Comments, or mirror one shared parameter into another. Storage-type
//          aware: only copies between matching storage types (String<-String, Double<-Double, etc.);
//          skips anything that doesn't match rather than guessing a conversion. Falls back to the
//          element's TYPE (independently for source AND target — either one, or both, may live at the
//          Type level) — matches action-report-parameters.cs/action-report-parameter-inventory.cs.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// Writing to a TYPE-level target changes the TYPE, so it applies to every instance sharing that type —
// reported separately below so the count isn't misread as "this many instances each individually
// changed."
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceParameterName = "Type Mark";
string targetParameterName = "Comments";
bool overwriteExisting = true; // false = skip elements where target already has a non-empty value
bool includeTypeParameters = true;
// ---- END INPUTS ----

Func<Element, string, Parameter> resolveAny = (e, name) =>
{
    var instP = e.LookupParameter(name);
    if (instP != null) return instP;
    if (includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        return type?.LookupParameter(name);
    }
    return null;
};

int updatedInstance = 0, updatedType = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Copy Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var src = resolveAny(e, sourceParameterName);
            var dst = resolveAny(e, targetParameterName);
            if (src == null || dst == null || !src.HasValue || dst.IsReadOnly) { skipped++; continue; }
            if (src.StorageType != dst.StorageType) { skipped++; continue; }

            if (!overwriteExisting)
            {
                bool targetHasValue = dst.StorageType == StorageType.String
                    ? !string.IsNullOrEmpty(dst.AsString())
                    : dst.HasValue && dst.AsValueString() != null;
                if (targetHasValue) { skipped++; continue; }
            }

            bool dstIsType = dst.Element is ElementType;

            switch (src.StorageType)
            {
                case StorageType.String: dst.Set(src.AsString()); break;
                case StorageType.Double: dst.Set(src.AsDouble()); break;
                case StorageType.Integer: dst.Set(src.AsInteger()); break;
                case StorageType.ElementId: dst.Set(src.AsElementId()); break;
                default: skipped++; continue;
            }
            if (dstIsType) updatedType++; else updatedInstance++;
        }
        t.Commit();
        sb.AppendLine($"Copied '{sourceParameterName}' -> '{targetParameterName}' on {updatedInstance} element(s) at Instance level" +
            (updatedType > 0 ? $", {updatedType} at Type level (applies to every instance sharing that type)" : "") +
            $", skipped {skipped} (missing, read-only, type mismatch, or already had a value).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to copy parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
