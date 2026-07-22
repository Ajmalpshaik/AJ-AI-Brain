// ============================================================
// FRAGMENT (action) — action-set-view-properties.cs
// PURPOSE: Batch-set Scale, Detail Level, Visual Style (Display Style), and/or Phase/Phase Filter across
//          every View in `elements` — the lightweight direct version of what applying a View Template does
//          all-at-once. Any input left null is not touched on that view. Phase/Phase Filter is what makes
//          a view "as per phase" (existing/demo/new-work shown per that view alone) — Revit's native,
//          built-in per-view mechanism, unlike workset visibility (see action-set-view-workset-visibility.cs)
//          which has no such single property and is set per-workset instead.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. filter-by-views.cs — pair with creators/create-view.cs right after creating a fresh one).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: not every view type supports every property (a schedule has no Scale/DisplayStyle, a 3D view's
//         Scale behaves differently than a plan) — each is set independently in its own try/catch so one
//         unsupported property on a view doesn't block the others from being set on it.
// NOT YET LIVE-VERIFIED — test on one view first before trusting it on a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? scale = 100;                 // e.g. 100 = "1:100"; null = don't change
string detailLevel = null;        // "Coarse" | "Medium" | "Fine"; null = don't change
string visualStyle = null;        // matches Autodesk.Revit.DB.DisplayStyle enum name (e.g. "Wireframe", "ShadingWithEdges", "Realistic"); null = don't change
string phaseName = null;          // exact project Phase name (see action-report-phases.cs); null = don't change
string phaseFilterName = null;    // exact Phase Filter name (Manage > Phases > Phase Filters tab, e.g. "Show All"); null = don't change
// ---- END INPUTS ----

Phase targetPhase = !string.IsNullOrEmpty(phaseName)
    ? Document.Phases.Cast<Phase>().FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase))
    : null;
PhaseFilter targetPhaseFilter = !string.IsNullOrEmpty(phaseFilterName)
    ? new FilteredElementCollector(Document).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().FirstOrDefault(f => f.Name.Equals(phaseFilterName, StringComparison.OrdinalIgnoreCase))
    : null;
if (!string.IsNullOrEmpty(phaseName) && targetPhase == null) sb.AppendLine($"Phase '{phaseName}' not found — skipping phase changes.");
if (!string.IsNullOrEmpty(phaseFilterName) && targetPhaseFilter == null) sb.AppendLine($"Phase Filter '{phaseFilterName}' not found — skipping phase filter changes.");

int scaleSet = 0, detailSet = 0, styleSet = 0, phaseSet = 0, phaseFilterSet = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set View Properties"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var view = el as View;
            if (view == null) { skipped++; continue; }

            if (scale.HasValue)
            {
                try { view.Scale = scale.Value; scaleSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' Scale: {ex.Message}"); }
            }
            if (!string.IsNullOrEmpty(detailLevel) && Enum.TryParse<ViewDetailLevel>(detailLevel, true, out var dl))
            {
                try { view.DetailLevel = dl; detailSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' DetailLevel: {ex.Message}"); }
            }
            if (!string.IsNullOrEmpty(visualStyle) && Enum.TryParse<DisplayStyle>(visualStyle, true, out var ds))
            {
                try { view.DisplayStyle = ds; styleSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' DisplayStyle: {ex.Message}"); }
            }
            if (targetPhase != null)
            {
                try { view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.Set(targetPhase.Id); phaseSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' Phase: {ex.Message}"); }
            }
            if (targetPhaseFilter != null)
            {
                try { view.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.Set(targetPhaseFilter.Id); phaseFilterSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' Phase Filter: {ex.Message}"); }
            }
        }
        t.Commit();
        sb.AppendLine($"Scale set on {scaleSet}, Detail Level set on {detailSet}, Visual Style set on {styleSet}, Phase set on {phaseSet}, Phase Filter set on {phaseFilterSet} view(s), {skipped} non-View element(s) skipped.");
        if (failures.Count > 0)
            sb.AppendLine("Failures: " + string.Join("; ", failures.Take(10)) + (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set view properties — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
