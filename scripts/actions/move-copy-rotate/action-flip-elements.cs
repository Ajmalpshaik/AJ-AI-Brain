// ============================================================
// FRAGMENT (action) — action-flip-elements.cs
// PURPOSE: Flip the hand and/or facing orientation of every FamilyInstance in `elements` (a door swinging
//          the wrong way, equipment facing the wrong direction) — Revit's own "Flip" arrows, scripted.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: only FamilyInstance supports hand/facing flip — not every family instance CAN flip either way
//         (CanFlipHand/CanFlipFacing checked first, so an unsupported flip is reported as skipped, not a
//         crash).
// ============================================================
// LIVE-VERIFIED 2026-08-07 — the positive path is finally proven: a door fixture (921817, CanFlipHand
// and CanFlipFacing both true) ran hand+facing and reported "Hand flipped on 1, facing flipped on 1,
// skipped 0", with HandFlipped/FacingFlipped genuinely going False -> True and back on rollback. The
// header said "NOT CHECKED — BLOCKED" for eighteen days after that run (the 2026-08-07 campaign updated
// the scripts/README.md row and never this file); corrected 2026-08-25 by the audit that widened the
// header/README status cross-check to see "NOT CHECKED" wording.
// Earlier graceful-path evidence, 2026-07-23 — tested against real Mechanical Equipment (AHU, Boiler,
// Radiator, VAV, Inline Pump) and real Duct Terminal families (Supply/Return Diffuser, Exhaust Grill,
// Supply Grille variants) — 13 loaded families checked, NONE supported flip on that project
// (CanFlipHand/CanFlipFacing both False on every one). Confirmed the skip-not-crash paths work correctly:
// ran against a non-flip-capable FamilyInstance + a plain Duct (not a FamilyInstance at all) together —
// correctly skipped both (3 skips: 2 for the unsupported flips, 1 for not-a-FamilyInstance), clean commit,
// zero exceptions. STILL BLOCKED for the positive path (an actual flip occurring) — no door/window or other
// flip-capable family is loaded in this project. Needs the user to load one to verify `flipHand()`/
// `flipFacing()` actually flip as expected, not just that unsupported cases are skipped safely.

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
                    catch (Exception ex) { failures.Add($"Id {fi.Id} hand: {ex.Message}"); }
                }
                else { skipped++; }
            }
            if (flipFacing)
            {
                if (fi.CanFlipFacing)
                {
                    try { fi.flipFacing(); facingFlipped++; }
                    catch (Exception ex) { failures.Add($"Id {fi.Id} facing: {ex.Message}"); }
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
