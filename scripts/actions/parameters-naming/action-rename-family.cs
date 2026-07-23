// ============================================================
// FRAGMENT (action) — action-rename-family.cs
// PURPOSE: Bulk-rename the FAMILY behind each element in `elements` — not the instance. Resolves
//          FamilyInstance -> Symbol.Family automatically (also accepts a FamilySymbol or Family passed
//          directly), then DEDUPES so each distinct family is renamed exactly ONCE even if `elements`
//          holds many instances of the same family — e.g. filter-by-category.cs on "Duct Accessories"
//          returns every placed instance, but if they come from 3 families, only those 3 get renamed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: renaming a Family renames it everywhere in the project (Project Browser, schedules, tags) —
//         a document-wide change, not per-instance. Explorer-first discipline applies: run the filter
//         alone first and confirm the family list/count before appending this action.
// GOTCHA: prefix/suffix mode skips a family that already has that prefix/suffix (case-insensitive), so
//         re-running the same script is a no-op on families already done instead of stacking
//         "AJ_AJ_DuctAccessory".
// GOTCHA: mode = "replace" sends every distinct family to the SAME newName — fine for one family, but
//         renaming several distinct families to one literal name will succeed on the first and skip the
//         rest (Revit requires unique family names) — same behavior as action-rename-element.cs.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "prefix";       // "prefix" | "suffix" | "find_replace" | "replace"
string prefix = "AJ_";        // used when mode == "prefix"
string suffix = "";           // used when mode == "suffix"
string findText = "";         // used when mode == "find_replace"
string replaceText = "";      // used when mode == "find_replace"
string newName = "";          // used when mode == "replace"
// ---- END INPUTS ----

Func<Element, Family> resolveFamily = e =>
{
    if (e is FamilyInstance fi) return fi.Symbol?.Family;
    if (e is FamilySymbol fs) return fs.Family;
    if (e is Family fam) return fam;
    return null;
};

var families = elements
    .Select(resolveFamily)
    .Where(f => f != null)
    .GroupBy(f => f.Id)
    .Select(g => g.First())
    .ToList();

int unresolved = elements.Count(e => resolveFamily(e) == null);
int renamed = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Rename Family"))
{
    t.Start();
    try
    {
        foreach (var fam in families)
        {
            string oldName = fam.Name;
            string candidate;
            switch (mode)
            {
                case "prefix":
                    if (oldName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = prefix + oldName;
                    break;
                case "suffix":
                    if (oldName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = oldName + suffix;
                    break;
                case "find_replace":
                    candidate = oldName.Replace(findText, replaceText);
                    break;
                case "replace":
                    candidate = newName;
                    break;
                default:
                    failures.Add($"Family '{oldName}': unknown mode '{mode}'");
                    skipped++;
                    continue;
            }

            if (candidate == oldName) { skipped++; continue; }

            try
            {
                fam.Name = candidate;
                renamed++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Family '{oldName}' (Id {fam.Id.IntegerValue}) -> '{candidate}': {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Renamed {renamed} distinct famil{(renamed == 1 ? "y" : "ies")} (mode='{mode}'), skipped {skipped}" +
            (unresolved > 0 ? $", {unresolved} element(s) had no resolvable family (not a FamilyInstance/FamilySymbol/Family)." : "."));
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rename families — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
