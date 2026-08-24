// ============================================================
// FRAGMENT (action) — action-rename-element.cs
// PURPOSE: Rename each element in `elements` to `newName` via Element.Name (works for most nameable
//          elements — views, sheets, levels, families/types, groups, materials — not for elements that
//          don't have an independent Name, like most instance-placed geometry).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ THE NAME IS VALIDATED BEFORE THE TRANSACTION OPENS (2026-08-24), AND THAT FIXES A WRONG DIAGNOSIS.
//    Revit rejects a handful of characters in any object name. Before this, a `newName` containing one
//    of them made EVERY element fail, and the summary then said "skipped N (name collision, or this
//    element type doesn't support renaming)" — sending you to look for a collision that does not exist.
//    `NamingUtils.IsValidName(name)` answers in advance, so the fragment says what is actually wrong and
//    opens no transaction at all. Note what it does NOT check, in Autodesk's own words: "This routine
//    checks only for prohibited characters... the same name cannot be used twice for different elements
//    of the same type... This routine does not check those conditions." So uniqueness is still found the
//    old way, per element, which is correct — a collision is a real per-element outcome, a bad character
//    is not.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// Revit requires names to be unique within their own scope
// (e.g. two Levels can't share a name) — renaming more than one element to the exact same `newName` will
// succeed for the first and fail (skipped, not silently overwritten) for the rest; that's expected, not a
// bug, unless the request was actually a sequential rename (use action-renumber-sequential.cs for that).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string newName = "New Name";
// ---- END INPUTS ----

// Prohibited characters are a property of the STRING, not of any element — so this is asked once, and
// asked before anything is opened. A name Revit will not accept cannot rename anything.
bool nameOk = true;
try { nameOk = NamingUtils.IsValidName(newName); } catch { }
if (!nameOk)
{
    sb.AppendLine($"NOT RENAMED — '{newName}' contains a character Revit does not allow in a name.");
    sb.AppendLine("  Revit rejects these in any object name:  \\  :  {  }  [  ]  |  ;  <  >  ?  `  ~");
    sb.AppendLine("  Nothing was changed and no transaction was opened. Fix the name and run it again.");
    return sb.ToString();
}

int renamed = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Rename Element"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                e.Name = newName;
                renamed++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"Id {e.Id} ('{e.Name}'): {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Renamed {renamed} element(s) to '{newName}', skipped {skipped} (name collision, or this element type doesn't support renaming).");
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
