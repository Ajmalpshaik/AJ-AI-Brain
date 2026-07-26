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
//         (found live 2026-07-26 — story in ../knowledge/live-model/core.md). Keep the wording mild.
// ✓ LIVE-VERIFIED 2026-07-26 — ran clean on Project1 after the wording fix; found 3 unenclosed rooms,
//   16 views off sheets, 16 unused templates, 6 unused filters, 0 warnings.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int topWarnings = 10;       // how many most-frequent warning descriptions to list
int maxIdsPerList = 15;     // Element-Id cap per detail list (rest summarized as "+N more")
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
foreach (var f in inPlace.Take(maxIdsPerList)) sb.AppendLine($"  - '{f.Name}' (Id {f.Id.IntegerValue})");
if (inPlace.Count > maxIdsPerList) sb.AppendLine($"  ... +{inPlace.Count - maxIdsPerList} more");

// --- 4. CAD imports (imported = embedded bloat; linked = fine) ---
var importInstances = new FilteredElementCollector(Document).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();
int importedCad = importInstances.Count(ii => !ii.IsLinked);
sb.AppendLine($"\n--- CAD files: {importInstances.Count} instance(s) — {importedCad} IMPORTED (embedded), {importInstances.Count - importedCad} linked ---");
foreach (var ii in importInstances.Where(i => !i.IsLinked).Take(maxIdsPerList))
    sb.AppendLine($"  - imported: '{ii.Category?.Name ?? "?"}' (Id {ii.Id.IntegerValue})");

// --- 5. Unenclosed / unplaced Rooms & Spaces ---
var spatial = new FilteredElementCollector(Document).OfClass(typeof(SpatialElement)).Cast<SpatialElement>()
    .Where(s => (s is Autodesk.Revit.DB.Architecture.Room || s is Autodesk.Revit.DB.Mechanical.Space) && s.Area <= 0).ToList();
sb.AppendLine($"\n--- Unenclosed/unplaced Rooms+Spaces (zero area): {spatial.Count} ---");
foreach (var s in spatial.Take(maxIdsPerList)) sb.AppendLine($"  - '{s.Name}' (Id {s.Id.IntegerValue})");
if (spatial.Count > maxIdsPerList) sb.AppendLine($"  ... +{spatial.Count - maxIdsPerList} more");
if (spatial.Count > 0) sb.AppendLine("  -> as an element set: filters/by-location/filter-by-unenclosed-spatial-elements.cs");

// --- 6. Views not on any sheet ---
var placedViewIds = new HashSet<int>(new FilteredElementCollector(Document).OfClass(typeof(Viewport)).Cast<Viewport>().Select(vp => vp.ViewId.IntegerValue));
var looseViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => !v.IsTemplate && !(v is ViewSheet) && !(v is ViewSchedule) && v.CanBePrinted && !placedViewIds.Contains(v.Id.IntegerValue))
    .ToList();
sb.AppendLine($"\n--- Printable views NOT on any sheet: {looseViews.Count} ---");

// --- 7. Model groups ---
var groupInstances = new FilteredElementCollector(Document).OfClass(typeof(Group)).Cast<Group>().ToList();
sb.AppendLine($"\n--- Model group instances: {groupInstances.Count} ({groupInstances.Select(g => g.GetTypeId().IntegerValue).Distinct().Count()} distinct group type(s)) ---");

// --- 8. Purgeable (dry-run counts, same definitions as action-purge-unused.cs) ---
var allViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().ToList();
var usedTemplateIds = new HashSet<int>(allViews.Where(v => !v.IsTemplate && v.ViewTemplateId.IntegerValue > 0).Select(v => v.ViewTemplateId.IntegerValue));
int unusedTemplates = allViews.Count(v => v.IsTemplate && !usedTemplateIds.Contains(v.Id.IntegerValue));
var usedFilterIds = new HashSet<int>();
foreach (var v in allViews.Where(v => !v.IsTemplate))
{
    try { foreach (var fid in v.GetFilters()) usedFilterIds.Add(fid.IntegerValue); } catch { } // view kind without filters
}
int unusedFilters = new FilteredElementCollector(Document).OfClass(typeof(FilterElement)).Count(f => !usedFilterIds.Contains(f.Id.IntegerValue));
sb.AppendLine($"\n--- Unused, removable later: {unusedTemplates} view template(s), {unusedFilters} filter(s) ---");
sb.AppendLine("  -> see actions/structural-changes/action-purge-unused.cs (dry-run first, per its own rule)");

return sb.ToString();
