// ============================================================
// *** PARTLY CHECKED — the copy-into-option behaviour still has not run, but the reason CHANGED on
// 2026-08-07 and the old reason is no longer true. The test model now HAS 6 Design Options in 3 sets
// (built as a fixture), so "this model has none" is retired. What still blocks it is narrower: no option
// can be made ACTIVE from a script, and the copy only lands in an option while that option is active.
// Both guard paths ARE now verified live (see below). To finish this off, make an option active in the
// Revit UI (status bar dropdown, or Manage > Design Options > Edit Selected) and re-run. ***
// FRAGMENT (action) — action-set-design-option.cs
// PURPOSE: Add every element in `elements` (from the Main Model) into a named Design Option — the write
//          counterpart to filter-by-design-option.cs, which can find elements already IN an option but has
//          no way to put them there.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the copies created inside the Design Option.
// NOT STANDALONE — see scripts/README.md for how to compose.
// HONESTY NOTE: Element.DesignOption is READ-ONLY in the public API — there is no direct "reassign this
// existing element to a different Design Option" call. The standard workaround (same one Revit's own UI
// "Add to Set" ultimately relies on) is: with the target option ACTIVE, COPY the elements (zero offset) —
// the copies belong to the option, the originals stay in the Main Model untouched. Set deleteOriginals =
// true below to also remove the Main Model originals afterward, simulating a true "move" — that's a
// heavier, less-reversible combination, so it's off by default.
// LIVE-VERIFIED 2026-07-22 (API surface only — see BLOCKED note): the original fragment called
// `Document.SetActiveDesignOptionId`, which DOES NOT EXIST — confirmed by reflecting every type in the
// RevitAPI assembly for any member containing "ActiveDesignOption": the ONLY match anywhere is the
// read-only static `DesignOption.GetActiveDesignOptionId(Document)`. There is no setter, on Document,
// DesignOption, or UIDocument. This is not a wrong-method-name typo — Revit 2020's public API has NO way
// to programmatically activate a Design Option at all (the internal `DesignOptionSet` class is also
// `internal`, not public, so there's no back door there either). PostableCommand.DesignOptions /
// StatusBarDesignOptions exist but only open a UI dialog/dropdown with no way to pass a target option
// name, so they're not a scriptable substitute.
// REWORKED APPROACH: since activation can't be scripted, this fragment now requires the target Design
// Option to ALREADY be the active one (set manually first: status bar Design Option dropdown, or Manage >
// Design Options > select the option > Edit Selected) before running. It checks that with the one real
// API that exists (DesignOption.GetActiveDesignOptionId), and refuses with a clear message if it doesn't
// match, instead of silently copying into the wrong option or throwing an opaque error. It also no longer
// resets the active design option afterward (the old GOTCHA about "leaving editing mode" no longer
// applies — this fragment never activates one itself, so it must not deactivate one the user deliberately
// set up, before or after running).
// STATUS 2026-08-07 — updated on the day the fixture changed, per the standing lesson in
// knowledge/brain-log.md that a blocker list is only useful if it is edited when the blocker moves:
//   ✓ VERIFIED live — the "Design Option not found" guard: it lists the 6 real options by name.
//   ✓ VERIFIED live — the "not the ACTIVE editing option" guard: with the Main Model active it refuses
//     and prints the exact UI steps, rather than copying into the wrong option or throwing.
//   ✓ RE-CONFIRMED by reflection on this Revit build: the only member matching "ActiveDesignOption"
//     anywhere on DesignOption or Document is the read-only `GetActiveDesignOptionId`. Still no setter.
//   ✗ NOT VERIFIED — the copy itself (ElementTransformUtils.CopyElements landing the copies inside the
//     active option). That needs an option to be active, which only the UI can do.
// The old note said this was blocked because the model had 0 Design Options. It has 6 now; the remaining
// blocker is activation, not existence.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string designOptionName = "Option 1";
bool deleteOriginals = false; // true = also delete the Main Model originals after copying into the option
// ---- END INPUTS ----

var targetOption = new FilteredElementCollector(Document)
    .OfClass(typeof(DesignOption))
    .Cast<DesignOption>()
    .FirstOrDefault(o => o.Name.Equals(designOptionName, StringComparison.OrdinalIgnoreCase));

if (targetOption == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(DesignOption)).Cast<DesignOption>();
    sb.AppendLine($"Design Option '{designOptionName}' not found. Available: {string.Join(", ", available.Select(o => o.Name))}");
}
else
{
    var activeId = DesignOption.GetActiveDesignOptionId(Document);
    if (activeId == null || activeId != targetOption.Id)
    {
        sb.AppendLine($"Design Option '{targetOption.Name}' is not currently the ACTIVE editing option, and there is no public API in this Revit version to activate one (see HONESTY NOTE in this file). " +
            $"Manually make '{targetOption.Name}' active first — status bar Design Option dropdown, or Manage > Design Options > select the option > Edit Selected — then re-run this script; it will copy `elements` into whichever option is active at that point.");
    }
    else
    {
        var newElementIds = new List<ElementId>();
        var sourceIds = elements.Select(e => e.Id).ToList();

        using (var t = new Transaction(Document, "AJ Tools - Set Design Option"))
        {
            t.Start();
            try
            {
                var copied = ElementTransformUtils.CopyElements(Document, sourceIds, XYZ.Zero);
                newElementIds.AddRange(copied);

                int deleted = 0;
                if (deleteOriginals)
                {
                    foreach (var id in sourceIds)
                    {
                        try { Document.Delete(id); deleted++; } catch { }
                    }
                }

                t.Commit();
                sb.AppendLine($"Copied {copied.Count} element(s) into Design Option '{targetOption.Name}'" +
                    (deleteOriginals ? $", deleted {deleted} Main Model original(s)." : " — Main Model originals left untouched."));
                if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id))}");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set design option — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
