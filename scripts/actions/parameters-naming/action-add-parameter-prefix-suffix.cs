// ============================================================
// FRAGMENT (action) — action-add-parameter-prefix-suffix.cs
// PURPOSE: Edit the EXISTING text of one named parameter across `elements` — add a prefix, add a suffix,
//          or find/replace a substring inside it — instead of overwriting it with a flat new value. The
//          general-purpose version of what action-rename-family.cs does for Family names specifically:
//          same "AJ_" in front of every Duct Accessory family name" idea, but for ANY String parameter
//          (Mark, Comments, Type Mark, Type Comments, ...) on ANY category. For a flat overwrite (same
//          fixed value for every element) use action-set-parameter-value.cs instead — that's a different
//          job, not duplicated here.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: falls back to the element's TYPE if the parameter isn't an instance parameter (Type Mark, Type
//         Comments, Manufacturer, Model, ...) — matches action-set-parameter-value.cs's own fallback. A
//         Type-level edit changes the TYPE, so it applies to every instance sharing it, not just the ones
//         in `elements` — deduped so a shared type is only edited ONCE even if 40 instances resolve to it,
//         not stacked 40 times.
// GOTCHA: only String-storage parameters are supported — a numeric parameter has no meaningful "prefix",
//         use action-set-parameter-value.cs's numericValueMm for those.
// GOTCHA: prefix/suffix mode skips a value that already has that prefix/suffix (case-insensitive), so
//         re-running the same script is a no-op on values already done instead of stacking "AJ_AJ_...".
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Comments";
string mode = "prefix";          // "prefix" | "suffix" | "find_replace"
string prefix = "AJ_";           // used when mode == "prefix"
string suffix = "";              // used when mode == "suffix"
string findText = "";            // used when mode == "find_replace"
string replaceText = "";         // used when mode == "find_replace"
bool includeTypeParameters = true;
// ---- END INPUTS ----

Func<Element, (Element owner, Parameter param, string source)> resolveOwner = e =>
{
    var instP = e.LookupParameter(parameterName);
    if (instP != null && !instP.IsReadOnly && instP.StorageType == StorageType.String) return (e, instP, "Instance");
    if (includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        var typeP = type?.LookupParameter(parameterName);
        if (typeP != null && !typeP.IsReadOnly && typeP.StorageType == StorageType.String) return (type, typeP, "Type");
    }
    return (null, null, null);
};

var targets = elements
    .Select(resolveOwner)
    .Where(o => o.owner != null)
    .GroupBy(o => o.source + ":" + o.owner.Id.IntegerValue)
    .Select(g => g.First())
    .ToList();

int unresolved = elements.Count(e => resolveOwner(e).owner == null);
int updatedInstance = 0, updatedType = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Add Parameter Prefix/Suffix"))
{
    t.Start();
    try
    {
        foreach (var target in targets)
        {
            string oldValue = target.param.AsString() ?? "";
            string candidate;
            switch (mode)
            {
                case "prefix":
                    if (oldValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = prefix + oldValue;
                    break;
                case "suffix":
                    if (oldValue.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = oldValue + suffix;
                    break;
                case "find_replace":
                    candidate = oldValue.Replace(findText, replaceText);
                    break;
                default:
                    failures.Add($"{target.source} owner Id {target.owner.Id}: unknown mode '{mode}'");
                    skipped++;
                    continue;
            }

            if (candidate == oldValue) { skipped++; continue; }

            try
            {
                target.param.Set(candidate);
                if (target.source == "Type") updatedType++; else updatedInstance++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"{target.source} owner Id {target.owner.Id} ('{oldValue}' -> '{candidate}'): {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Edited '{parameterName}' (mode='{mode}') on {updatedInstance} element(s) at Instance level" +
            (updatedType > 0 ? $", {updatedType} at Type level (applies to every instance sharing that type)" : "") +
            $", skipped {skipped}" +
            (unresolved > 0 ? $", {unresolved} element(s) had no matching String parameter." : "."));
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to edit parameter — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
