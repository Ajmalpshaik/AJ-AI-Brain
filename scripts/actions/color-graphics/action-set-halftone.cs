// ============================================================
// FRAGMENT (action) — action-set-halftone.cs
// PURPOSE: Turn halftone ON or OFF for every element in `elements` — a distinct override from color
//          (OverrideGraphicSettings.SetHalftone), covered nowhere else in this group. Read-modify-write:
//          reads each element's EXISTING overrides first and only changes the halftone flag, so a prior
//          color/transparency override on the same element isn't wiped out.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool halftone = true;
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Set Halftone"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                var ogs = view.GetElementOverrides(e.Id);
                ogs.SetHalftone(halftone);
                view.SetElementOverrides(e.Id, ogs);
            }
            t.Commit();
            sb.AppendLine($"Set halftone = {halftone} on {elements.Count} element(s) in view '{view.Name}' (existing color/transparency overrides preserved).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set halftone — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
