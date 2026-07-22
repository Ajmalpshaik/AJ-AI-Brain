// ============================================================
// FRAGMENT (action) — action-remove-parameter-value.cs
// PURPOSE: Clear one named parameter's value across every element in `elements` — completes the
//          Set/Copy pair already here (action-set-parameter-value.cs, action-copy-parameter-value.cs)
//          with an explicit Remove.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// HONESTY NOTE: Revit's public API has no universal "unset" for a Double/Integer parameter — those can
// only be reset to 0, not returned to a true "no value" state. String resets to "" (genuinely empty) and
// ElementId resets to ElementId.InvalidElementId (genuinely clears the reference) — those two ARE real
// removals. Reported separately below so "cleared" never overstates what actually happened to a number.
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Comments";
// ---- END INPUTS ----

int cleared = 0, zeroedNumeric = 0, skipped = 0;

using (var t = new Transaction(Document, "AJ Tools - Remove Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var p = e.LookupParameter(parameterName);
            if (p == null || p.IsReadOnly) { skipped++; continue; }

            switch (p.StorageType)
            {
                case StorageType.String: p.Set(""); cleared++; break;
                case StorageType.ElementId: p.Set(ElementId.InvalidElementId); cleared++; break;
                case StorageType.Double: p.Set(0.0); zeroedNumeric++; break;
                case StorageType.Integer: p.Set(0); zeroedNumeric++; break;
                default: skipped++; break;
            }
        }
        t.Commit();
        sb.AppendLine($"'{parameterName}': genuinely cleared on {cleared} element(s), zeroed (not truly unset — Double/Integer has no API 'no value' state) on {zeroedNumeric}, skipped {skipped} (missing or read-only).");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to remove parameter value — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
