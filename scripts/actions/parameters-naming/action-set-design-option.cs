// ============================================================
// FRAGMENT (action) — action-set-design-option.cs
// PURPOSE: Add every element in `elements` (from the Main Model) into a named Design Option — the write
//          counterpart to filter-by-design-option.cs, which can find elements already IN an option but has
//          no way to put them there.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// PRODUCES: newElementIds (List<ElementId>) — the copies created inside the Design Option.
// NOT STANDALONE — see scripts/README.md for how to compose.
// HONESTY NOTE: Element.DesignOption is READ-ONLY in the public API — there is no direct "reassign this
// existing element to a different Design Option" call. The standard workaround (same one Revit's own UI
// "Add to Set" ultimately relies on) is: activate the target option via Document.SetActiveDesignOptionId,
// then COPY the elements (zero offset) while it's active — the copies belong to the option, the originals
// stay in the Main Model untouched. Set deleteOriginals = true below to also remove the Main Model
// originals afterward, simulating a true "move" — that's a heavier, less-reversible combination, so it's
// off by default.
// GOTCHA: this fragment resets the active design option back to Main Model (InvalidElementId) when done,
// so it doesn't leave the Revit session sitting inside Design Option editing mode.
// NOT YET LIVE-VERIFIED — this technique has not been run against a real model with Design Options in this
// session. Verify carefully before trusting it, more so than most other fragments here.
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
    var newElementIds = new List<ElementId>();
    var sourceIds = elements.Select(e => e.Id).ToList();

    using (var t = new Transaction(Document, "AJ Tools - Set Design Option"))
    {
        t.Start();
        try
        {
            Document.SetActiveDesignOptionId(targetOption.Id);
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

            Document.SetActiveDesignOptionId(ElementId.InvalidElementId); // always leave editing mode
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
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
