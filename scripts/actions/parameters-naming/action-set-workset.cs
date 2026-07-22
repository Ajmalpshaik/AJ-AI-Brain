// ============================================================
// FRAGMENT (action) — action-set-workset.cs
// PURPOSE: Assign every element in `elements` to a named user workset — the write counterpart to
//          filter-by-workset.cs, which can find elements ON a workset but has no way to put them there.
//          Sets the ELEM_PARTITION_PARAM parameter (Integer storage holding the Workset's own Id) since
//          Element.WorksetId itself has no public setter — this parameter route is Revit's own documented
//          way to move an element between worksets via the API.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only meaningful on a workshared (central/local) model — reports plainly and changes nothing on
//         a non-workshared model instead of throwing.
// NOT YET LIVE-VERIFIED — test on one element first before trusting it on a batch.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string worksetName = "Shared Levels and Grids";
// ---- END INPUTS ----

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is not workshared — nothing to set, no user worksets exist.");
}
else
{
    var targetWorkset = new FilteredWorksetCollector(Document)
        .OfKind(WorksetKind.UserWorkset)
        .FirstOrDefault(ws => ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

    if (targetWorkset == null)
    {
        var available = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).Select(ws => ws.Name);
        sb.AppendLine($"Workset '{worksetName}' not found. Available: {string.Join(", ", available)}");
    }
    else
    {
        int updated = 0, skipped = 0;

        using (var t = new Transaction(Document, "AJ Tools - Set Workset"))
        {
            t.Start();
            try
            {
                foreach (var e in elements)
                {
                    var p = e.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (p == null || p.IsReadOnly) { skipped++; continue; }

                    try
                    {
                        p.Set(targetWorkset.Id.IntegerValue);
                        updated++;
                    }
                    catch { skipped++; } // some elements (e.g. workset-independent categories) can't be moved
                }
                t.Commit();
                sb.AppendLine($"Set workset '{targetWorkset.Name}' on {updated} element(s), skipped {skipped} (no workset parameter, or not movable).");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set workset — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
