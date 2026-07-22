// ============================================================
// FRAGMENT (action) — action-find-replace-element-name.cs
// PURPOSE: Find/replace, prefix, or suffix EACH element's own Name (`Element.Name`) in `elements` — works
//          on ANY nameable element, not just one category: Rooms, Sheets, Views, Levels, Grids, Groups,
//          Materials, Types. This is the generic version of what action-rename-family.cs does specifically
//          for Family names — same 3 text-editing modes, but targeting the plain Element.Name property so
//          "find and replace in the room name" / "sheet name" / "view name" (or anything else nameable)
//          is all this ONE fragment, just paired with a different filter (filter-by-room.cs,
//          filter-by-sheets.cs, filter-by-views.cs, filter-by-levels.cs, filter-by-grid.cs, ...).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: for a flat "rename everything to this exact same name" instead, use action-rename-element.cs —
//         kept separate, same division of labor as action-set-parameter-value.cs vs
//         action-add-parameter-prefix-suffix.cs.
// GOTCHA: prefix/suffix mode skips a name that already has that prefix/suffix (case-insensitive), so
//         re-running the same script is a no-op on names already done instead of stacking "AJ_AJ_...".
// GOTCHA: Revit requires names to be unique within their own scope (two Levels can't share a name, etc.)
//         — a collision on one element is skipped (reported), not silently overwritten, same as
//         action-rename-element.cs.
// NOT YET LIVE-VERIFIED — test on one element first before trusting it on a batch.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "find_replace";  // "find_replace" | "prefix" | "suffix"
string findText = "";          // used when mode == "find_replace"
string replaceText = "";       // used when mode == "find_replace"
string prefix = "";            // used when mode == "prefix"
string suffix = "";            // used when mode == "suffix"
// ---- END INPUTS ----

int renamed = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Find/Replace Element Name"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            string oldName = e.Name ?? "";
            string candidate;
            switch (mode)
            {
                case "find_replace":
                    candidate = oldName.Replace(findText, replaceText);
                    break;
                case "prefix":
                    if (oldName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = prefix + oldName;
                    break;
                case "suffix":
                    if (oldName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    candidate = oldName + suffix;
                    break;
                default:
                    failures.Add($"Id {e.Id.IntegerValue}: unknown mode '{mode}'");
                    skipped++;
                    continue;
            }

            if (candidate == oldName) { skipped++; continue; }

            try
            {
                e.Name = candidate;
                renamed++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id.IntegerValue} ('{oldName}' -> '{candidate}'): {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Renamed {renamed} element(s) (mode='{mode}'), skipped {skipped}.");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to rename — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
