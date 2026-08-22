// ============================================================
// FRAGMENT (action) - action-set-pin-state.cs
// PURPOSE: Pin or unpin every element in `elements`. Generic live-script version of AJ Tools'
//          Pin Elements operation, but driven by any reusable filter.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE - see scripts/README.md for how to compose.
// SOURCE: AJ Tools PinElementsService TrySetPinned pattern.
//
// WHY THIS MATTERS BEYOND TIDINESS: a pinned element ACCEPTS `MoveElement`/`RotateElement` and DOES NOT
//   MOVE, without throwing. That produced a false "Moved 5 element(s)" report in this Brain once - see
//   knowledge/live-model/geometry-and-transforms.md. So "unpin, move, re-pin" is the real fix when a
//   bulk move reports blocked elements, and this fragment is both halves of it.
//
// * UPGRADED 2026-08-22 (harvested from the add-in's Pin/Unpin Elements): added a DRY RUN and a
//   READ-BACK. `Element.Pinned` is a plain property that does NOT throw on an element which cannot take
//   the state - it simply does not stick, and the old version counted that as `updated`. Every write is
//   now read back and anything that did not take is reported as REFUSED. A group member cannot be
//   pinned apart from its group, which is the usual reason.
// GOTCHA: pinning does NOT stop deletion, parameter edits or type changes. It stops moving and
//         rotating. Do not treat it as protection.
// GOTCHA: `unchanged` is a success, not a failure - it means those elements were already in the state
//         asked for. "0 updated" on an already-pinned set reads as "nothing to do", not "it broke".
// + verified live 2026-07-22 in its pre-upgrade form; the dry run and read-back are compile-checked on
//   2020/2024/2027 but not yet live-run.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first and confirm the count before
// appending this action.

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
bool pinState = true; // true = pin, false = unpin
bool dryRun = false;  // true = report what would change, change nothing
// ---- END INPUTS ----

int updated = 0, unchanged = 0, skipped = 0, refused = 0;
var refusedIds = new List<string>();

using (var t = new Transaction(Document, pinState ? "AJ Tools - Pin Elements" : "AJ Tools - Unpin Elements"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            if (e == null || !e.IsValidObject)
            {
                skipped++;
                continue;
            }

            bool current;
            try
            {
                current = e.Pinned;
            }
            catch
            {
                skipped++;
                continue;
            }

            if (current == pinState)
            {
                unchanged++;
                continue;
            }

            if (dryRun) { updated++; continue; }

            try
            {
                e.Pinned = pinState;
            }
            catch
            {
                skipped++;
                continue;
            }

            // Pinned does NOT throw when it will not stick - read it back rather than trusting the set.
            bool after;
            try { after = e.Pinned; } catch { after = current; }
            if (after != pinState)
            {
                refused++;
                if (refusedIds.Count < 20) refusedIds.Add(e.Id.ToString());
                continue;
            }

            updated++;
        }

        if (dryRun) t.RollBack(); else t.Commit();
        sb.AppendLine((dryRun ? "DRY RUN - nothing changed. Would " : "")
            + $"{(pinState ? "Pinned" : "Unpinned")} {updated} element(s), unchanged {unchanged}, skipped {skipped}"
            + (refused > 0 ? $", REFUSED (state did not stick) {refused}" : "") + ".");
        if (refusedIds.Count > 0)
            sb.AppendLine("Refused ids: " + string.Join(", ", refusedIds)
                + " - a group member cannot be pinned apart from its group.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to change pin state - rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
