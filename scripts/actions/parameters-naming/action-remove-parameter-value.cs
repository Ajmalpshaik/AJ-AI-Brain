// ============================================================
// FRAGMENT (action) — action-remove-parameter-value.cs
// PURPOSE: Clear one named parameter's value across every element in `elements` — completes the
//          Set/Copy pair already here (action-set-parameter-value.cs, action-copy-parameter-value.cs)
//          with an explicit Remove. Falls back to the element's TYPE if the parameter isn't an instance
//          parameter — matches the other two.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// HONESTY NOTE: Revit's public API has no universal "unset" for a Double/Integer parameter — those can
// only be reset to 0, not returned to a true "no value" state. String resets to "" (genuinely empty) and
// ElementId resets to ElementId.InvalidElementId (genuinely clears the reference) — those two ARE real
// removals. Reported separately below so "cleared" never overstates what actually happened to a number.
// A TYPE-level clear changes the TYPE, so it applies to every instance sharing that type — reported
// separately too.
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "Comments";
bool includeTypeParameters = true;
// ---- END INPUTS ----

Func<Element, Parameter> resolveWritable = e =>
{
    var instP = e.LookupParameter(parameterName);
    if (instP != null && !instP.IsReadOnly) return instP;
    if (includeTypeParameters)
    {
        var type = Document.GetElement(e.GetTypeId()) as ElementType;
        var typeP = type?.LookupParameter(parameterName);
        if (typeP != null && !typeP.IsReadOnly) return typeP;
    }
    return null;
};

int clearedInstance = 0, clearedType = 0, zeroedNumeric = 0, skipped = 0, rejected = 0;
var rejectedIds = new List<int>();

using (var t = new Transaction(Document, "AJ Tools - Remove Parameter Value"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var p = resolveWritable(e);
            if (p == null) { skipped++; continue; }
            bool isType = p.Element is ElementType;

            // Parameter.Set() RETURNS A BOOL, and Revit uses it: a value the parameter will not accept
            // is refused by returning false, WITHOUT throwing. Discarding it reported "zeroed on 3"
            // for three ducts whose Width was still 300 mm — a duct cannot be 0 wide, so Set(0.0)
            // returned false every time (proved live 2026-08-07). Numeric "removal" is exactly where
            // this bites, because 0 is so often the one value the parameter rejects.
            bool ok;
            switch (p.StorageType)
            {
                case StorageType.String: ok = p.Set(""); if (ok) { if (isType) clearedType++; else clearedInstance++; } break;
                case StorageType.ElementId: ok = p.Set(ElementId.InvalidElementId); if (ok) { if (isType) clearedType++; else clearedInstance++; } break;
                case StorageType.Double: ok = p.Set(0.0); if (ok) zeroedNumeric++; break;
                case StorageType.Integer: ok = p.Set(0); if (ok) zeroedNumeric++; break;
                default: skipped++; continue;
            }
            if (!ok) { rejected++; if (rejectedIds.Count < 20) rejectedIds.Add(e.Id.IntegerValue); }
        }
        t.Commit();
        sb.AppendLine($"'{parameterName}': genuinely cleared on {clearedInstance} element(s) at Instance level" +
            (clearedType > 0 ? $", {clearedType} at Type level (applies to every instance sharing that type)" : "") +
            $", zeroed (not truly unset — Double/Integer has no API 'no value' state) on {zeroedNumeric}, skipped {skipped} (missing or read-only).");
        if (rejected > 0)
            sb.AppendLine($"  {rejected} REFUSED THE VALUE — Revit's Set() returned false and the old value is still there (a duct cannot be 0 wide, a level cannot be unset, etc.). Unchanged Id(s): {string.Join(", ", rejectedIds)}{(rejected > rejectedIds.Count ? ", ..." : "")}");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to remove parameter value — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
