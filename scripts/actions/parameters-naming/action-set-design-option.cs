// ============================================================
// *** NOT CHECKED — BLOCKED: the core copy-into-option behavior has never run against a real Design
// Option. This model has none, and there is no API to create one. See the BLOCKED note below for detail. ***
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
// BLOCKED — full live verification of the copy-while-active-option behavior could not be completed this
// session: this model has 0 Design Options and the public API cannot create one either (UI-only, Manage >
// Design Options > New — see scripts/README.md prerequisites). The API calls used below
// (DesignOption.GetActiveDesignOptionId, ElementTransformUtils.CopyElements, DesignOption.Name/Id) are
// all confirmed to exist and compile correctly; the actual copy-into-option behavior is unverified against
// a real option. Re-verify once a Design Option exists and can be made active.
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
                if (newElementIds.Count > 0) sb.AppendLine($"newElementIds: {string.Join(", ", newElementIds.Select(id => id.IntegerValue))}");
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
