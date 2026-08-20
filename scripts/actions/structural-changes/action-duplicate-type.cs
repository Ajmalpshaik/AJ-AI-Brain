// ============================================================
// FRAGMENT (action) — action-duplicate-type.cs
// PURPOSE: Duplicate the TYPE(s) behind `elements` into new, separately-named type(s) — e.g. make a new
//          250x250 duct accessory type from an existing 200x200 one. Resolves each element's own type,
//          DEDUPES so a shared type is only duplicated ONCE even if 40 instances share it, then calls
//          Revit's own ElementType.Duplicate(name) — works for both Family types (FamilySymbol) and
//          system-family types (duct/pipe/wall types, etc.) since Duplicate is a base ElementType method.
//          The Type-level counterpart to action-rename-family.cs. For reassigning existing instances onto
//          a different (e.g. newly duplicated) type, compose with action-change-element-type.cs afterward
//          — kept separate on purpose, one job per fragment.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the freshly duplicated TYPES (not instances), for chaining.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: mode="fixed" sends every distinct source type to the SAME new name — fine for one type, but
//         duplicating several distinct types to one literal name will succeed on the first and skip the
//         rest (Revit requires unique type names) — same caveat as action-rename-family.cs's "replace" mode.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "suffix";       // "fixed" | "prefix" | "suffix"
string fixedName = "New Type"; // used when mode == "fixed"
string prefix = "AJ_";         // used when mode == "prefix"
string suffix = " - Copy";     // used when mode == "suffix"
// ---- END INPUTS ----

var types = elements
    .Select(e => Document.GetElement(e.GetTypeId()) as ElementType)
    .Where(t => t != null)
    .GroupBy(t => t.Id)
    .Select(g => g.First())
    .ToList();

int unresolved = elements.Count(e => e.GetTypeId() == ElementId.InvalidElementId);
var newElementIds = new List<ElementId>();
int duplicated = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Duplicate Type"))
{
    t.Start();
    try
    {
        foreach (var srcType in types)
        {
            string candidate;
            switch (mode)
            {
                case "fixed": candidate = fixedName; break;
                case "prefix": candidate = prefix + srcType.Name; break;
                case "suffix": candidate = srcType.Name + suffix; break;
                default:
                    failures.Add($"Type '{srcType.Name}': unknown mode '{mode}'");
                    skipped++;
                    continue;
            }

            try
            {
                var newType = srcType.Duplicate(candidate);
                newElementIds.Add(newType.Id);
                duplicated++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Type '{srcType.Name}' (Id {srcType.Id}) -> '{candidate}': {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Duplicated {duplicated} distinct type(s) (mode='{mode}'), skipped {skipped}" +
            (unresolved > 0 ? $", {unresolved} element(s) had no resolvable type." : "."));
        if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id))}");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to duplicate type — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below (e.g. action-change-element-type.cs to reassign
//      instances onto the new type), or add return sb.ToString(); to finish ----
