// ============================================================
// FRAGMENT (filter) — filter-by-phase.cs
// PURPOSE: Elements whose Phase Created and/or Phase Demolished matches a named project Phase — e.g.
//          "everything added in Phase 2", "what's being demolished in Phase 1". Optionally narrowed to
//          one or more categories.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
//
// ✱✱ "CREATED IN PHASE 3" AND "EXISTING IN PHASE 3" ARE DIFFERENT QUESTIONS, and until 2026-08-23 this
//    fragment could only answer the first one. Phase Created is authorship — the phase an element was
//    put in. STATUS is what a drawing set in a given phase actually shows it as, and it changes phase by
//    phase without the element changing at all: a wall CREATED in Phase 1 is EXISTING when the view is
//    set to Phase 3, and DEMOLISHED once its demolition phase is reached. A phase plan is drawn from
//    status, not from authorship, so "give me the existing services" cannot be answered by Phase Created.
//    Revit computes it: `element.GetPhaseStatus(phaseId)` -> ElementOnPhaseStatus
//        New         created in THIS phase
//        Existing    created earlier and still standing in this phase
//        Demolished  demolished in THIS phase
//        Temporary   created AND demolished within this one phase
//        Future      not yet created as at this phase
//        Past        demolished before this phase was reached
//        None        the element does not participate in phasing at all
//    Set statusPhaseName plus wantedStatuses below to filter that way. The two modes combine: leaving
//    the created/demolished names null and giving only a status phase is the usual phase-plan query.
// GOTCHA: an element that does not take part in phasing returns None (or throws on some types) — those
//         are skipped, and the count of skipped ones is reported rather than hidden.
// ⚠ THE STATUS MODE HAS NOT BEEN RUN AGAINST A REAL MODEL — documented 2026-08-23, but the CODE for it
//   was only added 2026-08-25: an audit found statusPhaseName and wantedStatuses declared and described
//   above while the body never read either — the mode answered "No phase specified". Check one wall you
//   already know the phase of before trusting a whole-model sweep. (The Created/Demolished mode is the
//   part that was live-verified.)
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string createdPhaseName = null;    // e.g. "Phase 2" — leave null to not filter on Phase Created
string demolishedPhaseName = null; // e.g. "Phase 1" — leave null to not filter on Phase Demolished
string statusPhaseName = null;     // e.g. "Phase 3" — the phase to ASK ABOUT; null = no status filtering
string[] wantedStatuses = new[] { "Existing" }; // any of: New, Existing, Demolished, Temporary, Future, Past
BuiltInCategory[] categoryScope = new BuiltInCategory[0]; // empty = every model category
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (string.IsNullOrEmpty(createdPhaseName) && string.IsNullOrEmpty(demolishedPhaseName)
    && string.IsNullOrEmpty(statusPhaseName))
{
    sb.AppendLine("No phase specified — set createdPhaseName, demolishedPhaseName and/or statusPhaseName.");
}
else
{
    var allPhases = new FilteredElementCollector(Document).OfClass(typeof(Phase)).Cast<Phase>().ToList();
    Phase createdPhase = string.IsNullOrEmpty(createdPhaseName) ? null :
        allPhases.FirstOrDefault(ph => ph.Name.Equals(createdPhaseName, StringComparison.OrdinalIgnoreCase));
    Phase demolishedPhase = string.IsNullOrEmpty(demolishedPhaseName) ? null :
        allPhases.FirstOrDefault(ph => ph.Name.Equals(demolishedPhaseName, StringComparison.OrdinalIgnoreCase));
    Phase statusPhase = string.IsNullOrEmpty(statusPhaseName) ? null :
        allPhases.FirstOrDefault(ph => ph.Name.Equals(statusPhaseName, StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrEmpty(createdPhaseName) && createdPhase == null)
        sb.AppendLine($"Phase '{createdPhaseName}' not found. Available: {string.Join(", ", allPhases.Select(p => p.Name))}");
    else if (!string.IsNullOrEmpty(demolishedPhaseName) && demolishedPhase == null)
        sb.AppendLine($"Phase '{demolishedPhaseName}' not found. Available: {string.Join(", ", allPhases.Select(p => p.Name))}");
    else if (!string.IsNullOrEmpty(statusPhaseName) && statusPhase == null)
        sb.AppendLine($"Phase '{statusPhaseName}' not found. Available: {string.Join(", ", allPhases.Select(p => p.Name))}");
    else
    {
        IEnumerable<Element> query;
        if (categoryScope.Length > 0)
        {
            var categoryIds = categoryScope.Select(c => new ElementId(c)).ToList();
            query = new FilteredElementCollector(Document)
                .WherePasses(new ElementMulticategoryFilter(categoryIds))
                .WhereElementIsNotElementType();
        }
        else
        {
            query = new FilteredElementCollector(Document).WhereElementIsNotElementType();
        }

        var wanted = new HashSet<string>(wantedStatuses ?? new string[0], StringComparer.OrdinalIgnoreCase);
        int skippedNoPhasing = 0;
        foreach (var e in query)
        {
            if (e == null) continue;
            try
            {
                bool createdOk = createdPhase == null || e.CreatedPhaseId == createdPhase.Id;
                bool demolishedOk = demolishedPhase == null || e.DemolishedPhaseId == demolishedPhase.Id;
                if (!createdOk || !demolishedOk) continue;
                if (statusPhase != null)
                {
                    var st = e.GetPhaseStatus(statusPhase.Id);
                    if (st == ElementOnPhaseStatus.None) { skippedNoPhasing++; continue; }
                    if (!wanted.Contains(st.ToString())) continue;
                }
                elements.Add(e);
            }
            catch { skippedNoPhasing++; } // some element types don't support phasing — skip, don't error
        }

        string categoryLabel = categoryScope.Length == 0 ? "all categories" : string.Join(", ", categoryScope.Select(c => c.ToString()));
        string statusLabel = statusPhase == null ? "(off)" : $"{string.Join("/", wanted)} as at '{statusPhase.Name}'";
        sb.AppendLine($"Filtered {elements.Count} element(s), categories: {categoryLabel}, Created={createdPhaseName ?? "(any)"}, Demolished={demolishedPhaseName ?? "(any)"}, Status={statusLabel}.");
        if (skippedNoPhasing > 0)
            sb.AppendLine($"  {skippedNoPhasing} element(s) skipped — no part in phasing (status None, or the type rejects the phase query).");
    }
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
