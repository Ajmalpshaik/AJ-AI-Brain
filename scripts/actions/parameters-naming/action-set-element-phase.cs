// ============================================================
// FRAGMENT (action) — action-set-element-phase.cs
// PURPOSE: Assign every element in `elements` to a named Phase — sets Phase Created and/or Phase
//          Demolished (independently optional) via the BuiltInParameter.PHASE_CREATED/PHASE_DEMOLISHED
//          ElementId-storage parameters. action-set-parameter-value.cs can't do this at all (it only
//          handles String/Double, not ElementId) — this is the dedicated version for Phase specifically.
//          The reverse lookup already exists: filter-by-phase.cs finds elements BY their assigned phase.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string createdPhaseName = "New Construction"; // "" = don't touch Phase Created
string demolishedPhaseName = ""; // "" = don't touch Phase Demolished
// ---- END INPUTS ----

Phase createdPhase = string.IsNullOrEmpty(createdPhaseName) ? null :
    Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(createdPhaseName, StringComparison.OrdinalIgnoreCase));
Phase demolishedPhase = string.IsNullOrEmpty(demolishedPhaseName) ? null :
    Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(demolishedPhaseName, StringComparison.OrdinalIgnoreCase));

if (!string.IsNullOrEmpty(createdPhaseName) && createdPhase == null)
{
    sb.AppendLine($"Phase '{createdPhaseName}' not found — nothing changed.");
}
else if (!string.IsNullOrEmpty(demolishedPhaseName) && demolishedPhase == null)
{
    sb.AppendLine($"Phase '{demolishedPhaseName}' not found — nothing changed.");
}
else if (createdPhase == null && demolishedPhase == null)
{
    sb.AppendLine("No phase specified — set createdPhaseName and/or demolishedPhaseName.");
}
else
{
    int updatedCreated = 0, updatedDemolished = 0, skipped = 0;

    using (var t = new Transaction(Document, "AJ Tools - Set Element Phase"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                bool touched = false;
                if (createdPhase != null)
                {
                    var p = e.get_Parameter(BuiltInParameter.PHASE_CREATED);
                    if (p != null && !p.IsReadOnly) { p.Set(createdPhase.Id); updatedCreated++; touched = true; }
                }
                if (demolishedPhase != null)
                {
                    var p = e.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED);
                    if (p != null && !p.IsReadOnly) { p.Set(demolishedPhase.Id); updatedDemolished++; touched = true; }
                }
                if (!touched) skipped++;
            }
            t.Commit();
            sb.AppendLine($"Set Phase Created on {updatedCreated} element(s)" +
                (demolishedPhase != null ? $", Phase Demolished on {updatedDemolished} element(s)" : "") +
                $", skipped {skipped} (no phasing support on this element, or read-only).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set element phase — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
