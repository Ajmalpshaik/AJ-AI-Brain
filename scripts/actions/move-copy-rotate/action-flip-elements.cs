// ============================================================
// FRAGMENT (action) — action-flip-elements.cs
// PURPOSE: Flip the hand and/or facing orientation of every FamilyInstance in `elements` (a door swinging
//          the wrong way, equipment facing the wrong direction) — Revit's own "Flip" arrows, scripted.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only FamilyInstance supports hand/facing flip — not every family instance CAN flip either way
//         (CanFlipHand/CanFlipFacing checked first, so an unsupported flip is reported as skipped, not a
//         crash).
// NOT YET LIVE-VERIFIED — test on one element first before trusting it on a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool flipHand = false;
bool flipFacing = false;
// ---- END INPUTS ----

int handFlipped = 0, facingFlipped = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Flip Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var fi = e as FamilyInstance;
            if (fi == null) { skipped++; continue; }

            if (flipHand)
            {
                if (fi.CanFlipHand)
                {
                    try { fi.flipHand(); handFlipped++; }
                    catch (Exception ex) { failures.Add($"Id {fi.Id.IntegerValue} hand: {ex.Message}"); }
                }
                else { skipped++; }
            }
            if (flipFacing)
            {
                if (fi.CanFlipFacing)
                {
                    try { fi.flipFacing(); facingFlipped++; }
                    catch (Exception ex) { failures.Add($"Id {fi.Id.IntegerValue} facing: {ex.Message}"); }
                }
                else { skipped++; }
            }
        }
        t.Commit();
        sb.AppendLine($"Hand flipped on {handFlipped}, facing flipped on {facingFlipped} element(s), skipped {skipped} (not a FamilyInstance, or that flip isn't supported).");
        if (failures.Count > 0) sb.AppendLine("Failures: " + string.Join("; ", failures.Take(10)) + (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to flip elements — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
