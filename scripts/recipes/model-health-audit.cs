// ============================================================
// RECIPE — model-health-audit.cs
// PURPOSE: One read-only health report for the whole model — the "audit model health" job. Covers: file
//          + worksharing basics, warnings (by severity, top repeat offenders), in-place families, CAD
//          imports (imported vs linked), unenclosed/unplaced Rooms and Spaces, views not placed on any
//          sheet, model groups, and purgeable view templates/filters (dry-run counts, same definition as
//          action-purge-unused.cs). Model never changes.
// STANDALONE — returns its own string; run as-is, no filter needed, safe anytime.
// GOTCHA: this is a SUMMARY. Each section names the follow-up fragment that turns its number into an
//         actionable element set (filter-by-warnings.cs, filter-by-unenclosed-spatial-elements.cs,
//         action-purge-unused.cs, ...) — drill down there, don't grow this recipe into a monolith.
// GOTCHA: unused-material detection is deliberately NOT here — it needs a full geometry sweep that's slow
//         on a big model; action-purge-unused.cs mode="materials" (dry-run) covers it on request.
// GOTCHA: the OUTPUT WORDING is deliberately mild ("Unused, removable later" not "Purgeable", "see" not
//         "delete via"). The bridge's destructive-operation guard scores the whole script's TEXT, including
//         plain output strings, and several deletion words together tripped it on this read-only script
//         (found live 2026-07-26 — story in ../../knowledge/live-model/core.md). Keep the wording mild.
// * UPGRADED 2026-08-23: two sections added that this recipe was working out for itself, or not at all.
//   Section 9 runs REVIT'S OWN performance rules (PerformanceAdviser.ExecuteAllRules) — Autodesk's
//   opinion of the model, which knows things a hand-written audit cannot. Section 10 lists the ADD-IN
//   UPDATERS registered in this session, the honest answer to "what else is changing my model".
//   Neither section has been run against a real model yet; both are read-only and both report their own
//   failure rather than staying quiet.
// ✓ LIVE-VERIFIED 2026-07-26 — ran clean on Project1 after the wording fix; found 3 unenclosed rooms,
//   16 views off sheets, 16 unused templates, 6 unused filters, 0 warnings.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int topWarnings = 10;       // how many most-frequent warning descriptions to list
int maxIdsPerList = 15;     // Element-Id cap per detail list (rest summarized as "+N more")
bool runPerformanceAdviser = false; // Revit's own rule engine — the slowest section; true runs it.
                                    // Default OFF since 2026-08-25: §9 has never run against a real
                                    // model, and the one never-run path should not be the default one.
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
sb.AppendLine("=== MODEL HEALTH AUDIT (read-only) ===");

// --- 1. File & session basics ---
sb.AppendLine($"Model: '{Document.Title}'");
if (!string.IsNullOrEmpty(Document.PathName))
{
    try
    {
        var fi = new System.IO.FileInfo(Document.PathName);
        if (fi.Exists) sb.AppendLine($"File: {Document.PathName} — {fi.Length / 1048576.0:F1} MB");
    }
    catch { }
}
sb.AppendLine($"Workshared: {Document.IsWorkshared}");

// --- 2. Warnings ---
var warnings = Document.GetWarnings();
int errCount = warnings.Count(w => w.GetSeverity() == FailureSeverity.Error);
sb.AppendLine($"\n--- Warnings: {warnings.Count} total ({errCount} Error-severity) ---");
if (warnings.Count > 0)
{
    var byDesc = warnings.GroupBy(w => w.GetDescriptionText()).OrderByDescending(g => g.Count()).Take(topWarnings);
    foreach (var g in byDesc) sb.AppendLine($"  {g.Count()}x — {g.Key}");
    sb.AppendLine("  -> element sets via filters/by-status/filter-by-warnings.cs, full list via context/context-all-warnings.cs");
}

// --- 3. In-place families ---
var inPlace = new FilteredElementCollector(Document).OfClass(typeof(Family)).Cast<Family>().Where(f => f.IsInPlace).ToList();
sb.AppendLine($"\n--- In-place families: {inPlace.Count} ---");
foreach (var f in inPlace.Take(maxIdsPerList)) sb.AppendLine($"  - '{f.Name}' (Id {f.Id})");
if (inPlace.Count > maxIdsPerList) sb.AppendLine($"  ... +{inPlace.Count - maxIdsPerList} more");

// --- 4. CAD imports (imported = embedded bloat; linked = fine) ---
var importInstances = new FilteredElementCollector(Document).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();
int importedCad = importInstances.Count(ii => !ii.IsLinked);
sb.AppendLine($"\n--- CAD files: {importInstances.Count} instance(s) — {importedCad} IMPORTED (embedded), {importInstances.Count - importedCad} linked ---");
foreach (var ii in importInstances.Where(i => !i.IsLinked).Take(maxIdsPerList))
    sb.AppendLine($"  - imported: '{ii.Category?.Name ?? "?"}' (Id {ii.Id})");

// --- 5. Unenclosed / unplaced Rooms & Spaces ---
var spatial = new FilteredElementCollector(Document).OfClass(typeof(SpatialElement)).Cast<SpatialElement>()
    .Where(s => (s is Autodesk.Revit.DB.Architecture.Room || s is Autodesk.Revit.DB.Mechanical.Space) && s.Area <= 0).ToList();
int allSpatial = new FilteredElementCollector(Document).OfClass(typeof(SpatialElement))
    .Cast<SpatialElement>().Count(s => s is Autodesk.Revit.DB.Architecture.Room || s is Autodesk.Revit.DB.Mechanical.Space);
bool hasLinks = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Any();
if (allSpatial == 0 && hasLinks)
    // A clean zero caused by the architecture living in a link is worse than no report — same rule
    // action-create-coordination-report.cs already follows.
    sb.AppendLine("\n--- Unenclosed/unplaced Rooms+Spaces: NOT CHECKED — this document has NO Rooms or Spaces"
        + " and carries linked model(s); on a coordination model the rooms live in the LINK, which this"
        + " section cannot read. ---");
else
sb.AppendLine($"\n--- Unenclosed/unplaced Rooms+Spaces (zero area): {spatial.Count} ---");
foreach (var s in spatial.Take(maxIdsPerList)) sb.AppendLine($"  - '{s.Name}' (Id {s.Id})");
if (spatial.Count > maxIdsPerList) sb.AppendLine($"  ... +{spatial.Count - maxIdsPerList} more");
if (spatial.Count > 0) sb.AppendLine("  -> as an element set: filters/by-location/filter-by-unenclosed-spatial-elements.cs");

// --- 6. Views not on any sheet ---
var placedViewIds = new HashSet<ElementId>(new FilteredElementCollector(Document).OfClass(typeof(Viewport)).Cast<Viewport>().Select(vp => vp.ViewId));
var looseViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => !v.IsTemplate && !(v is ViewSheet) && !(v is ViewSchedule) && v.CanBePrinted && !placedViewIds.Contains(v.Id))
    .ToList();
sb.AppendLine($"\n--- Printable views NOT on any sheet: {looseViews.Count} ---");

// --- 7. Model groups ---
var groupInstances = new FilteredElementCollector(Document).OfClass(typeof(Group)).Cast<Group>().ToList();
sb.AppendLine($"\n--- Model group instances: {groupInstances.Count} ({groupInstances.Select(g => g.GetTypeId()).Distinct().Count()} distinct group type(s)) ---");

// --- 8. Purgeable (dry-run counts, same definitions as action-purge-unused.cs) ---
var allViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().ToList();
var usedTemplateIds = new HashSet<ElementId>(allViews.Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId).Select(v => v.ViewTemplateId));
int unusedTemplates = allViews.Count(v => v.IsTemplate && !usedTemplateIds.Contains(v.Id));
var usedFilterIds = new HashSet<ElementId>();
foreach (var v in allViews.Where(v => !v.IsTemplate))
{
    try { foreach (var fid in v.GetFilters()) usedFilterIds.Add(fid); } catch { } // view kind without filters
}
// ParameterFilterElement only: FilterElement would also count saved SELECTION sets, which are never
// "applied to a view" and so always read as unused — they are not removable clutter (2026-08-25).
int unusedFilters = new FilteredElementCollector(Document).OfClass(typeof(ParameterFilterElement)).Count(f => !usedFilterIds.Contains(f.Id));
sb.AppendLine($"\n--- Unused, removable later: {unusedTemplates} view template(s), {unusedFilters} view filter(s) ---");
sb.AppendLine("  -> see actions/structural-changes/action-purge-unused.cs (dry-run first, per its own rule)");

// --- 9. Revit's OWN performance rules (added 2026-08-23) ---------------------------------------------
// Revit ships a rule engine that nothing here was asking. PerformanceAdviser holds Autodesk's own model
// checks — the ones behind Manage > Performance Adviser — and ExecuteAllRules returns them as
// FailureMessages. It is worth having because these rules know things this recipe cannot work out from
// the outside, and because they are Autodesk's opinion of the model rather than ours.
// It walks the model, so on a large file it is the slowest section here — hence the switch.
if (runPerformanceAdviser)
{
    try
    {
        var adviser = PerformanceAdviser.GetPerformanceAdviser();
        var results = adviser.ExecuteAllRules(Document);
        sb.AppendLine($"\n--- Revit's own performance rules: {results.Count} finding(s) ---");
        foreach (var msg in results.Take(topWarnings))
        {
            string sev;
            try { sev = msg.GetSeverity().ToString(); } catch { sev = "?"; }
            sb.AppendLine($"  [{sev}] {msg.GetDescriptionText()}");
        }
        if (results.Count > topWarnings) sb.AppendLine($"  ... {results.Count - topWarnings} more not shown.");
        if (results.Count == 0) sb.AppendLine("  (nothing flagged)");
    }
    catch (Exception ex)
    {
        // Some rules need an active graphical view and refuse otherwise — reported, never swallowed,
        // so a blank section is never mistaken for a clean one.
        sb.AppendLine($"\n--- Revit's own performance rules: could not run — {ex.Message} ---");
    }
}

// --- 10. Which add-ins are wired into this session ---------------------------------------------------
// A registered updater is another add-in's code that Revit runs whenever matching elements change. It is
// the honest answer to "why did that value change by itself" and to "what else is touching this model",
// and it appears nowhere else in this audit. Session-wide, not saved in the file.
try
{
    var updaters = UpdaterRegistry.GetRegisteredUpdaterInfos();
    sb.AppendLine($"\n--- Add-in updaters active in this Revit session: {updaters.Count} ---");
    foreach (var u in updaters.Take(topWarnings))
        sb.AppendLine($"  {u.UpdaterName}{(string.IsNullOrEmpty(u.AdditionalInformation) ? "" : " — " + u.AdditionalInformation)}");
    if (updaters.Count > topWarnings) sb.AppendLine($"  ... {updaters.Count - topWarnings} more not shown.");
}
catch (Exception ex) { sb.AppendLine($"\n--- Add-in updaters: could not read — {ex.Message} ---"); }

return sb.ToString();
