// ============================================================
// FRAGMENT (action) — action-purge-unplaced-views.cs
// PURPOSE: Find (and on request DELETE) views that are not on any sheet — 3D views, sections,
//          schedules, legends and drafting views. The "clean up the working views nobody put on a
//          sheet" job, before issue.
//          Different from `action-purge-unused.cs`, which clears unused view TEMPLATES, filters and
//          materials — those are definitions nothing uses; these are real views nobody placed.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// ✱✱ THE FACT THAT MAKES THIS CORRECT: A SCHEDULE IS NOT PLACED VIA A VIEWPORT. Sheets carry schedules
//    as **`ScheduleSheetInstance`**, a completely different class. Collect only `Viewport` and every
//    schedule in the project reports as unplaced — so a "purge unplaced" run would offer to delete every
//    schedule on every sheet. Both mechanisms are read here.
//
// ⚠⚠ DELETING A VIEW DELETES WHAT IS DRAWN IN IT. A drafting view or legend takes its contents with it.
//    DRY RUN BY DEFAULT — read the list, then commit. Rule 5 of START-HERE.md: this is bulk and hard to
//    reverse, so get an explicit go-ahead.
// GOTCHA: THE DEFAULT {3D} VIEW IS NEVER OFFERED. It is unplaced by nature and deleting it is a
//         nuisance nobody wants. It is excluded by name.
// GOTCHA: VIEW TEMPLATES ARE EXCLUDED (`IsTemplate`). A template is never "on a sheet" and is not a
//         working view — `action-purge-unused.cs` handles unused templates properly.
// GOTCHA: "not on a sheet" is NOT the same as "not needed". Working views, coordination views and
//         views referenced by a callout or section marker are all legitimately unplaced. This finds
//         candidates for a human to look at; it is not an automatic tidy-up.
// GOTCHA: a view that is the TARGET of a section/callout marker still shows as unplaced here. Deleting
//         it removes the marker from the parent view too.
// NOTE: `kinds` picks which view types to consider — do them one at a time on a real project so the
//       list stays readable.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Purge Unplaced Views.
//   Compile-checked on 2020/2024/2027. Dry-run and eyeball the list before ever committing.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = list only; false = ACTUALLY DELETE
string kinds = "3d,section,schedule,legend,drafting";   // any of: 3d | section | schedule | legend | drafting
string nameContains = "";            // "" = all; else only names containing this
int maxDelete = 100;                 // hard stop, so a wide filter cannot wipe a project's views
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var want = new HashSet<string>(kinds.Split(',').Select(s => s.Trim().ToLowerInvariant())
                                    .Where(s => s.Length > 0));

// ---------- what IS placed — BOTH mechanisms ----------
var placed = new HashSet<ElementId>();

foreach (Viewport vp in new FilteredElementCollector(Document).OfClass(typeof(Viewport)).Cast<Viewport>())
{
    try { if (vp != null) placed.Add(vp.ViewId); } catch { }
}
// Schedules do NOT use Viewport — this second pass is the whole point.
foreach (ScheduleSheetInstance ssi in new FilteredElementCollector(Document)
             .OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
{
    try { if (ssi != null) placed.Add(ssi.ScheduleId); } catch { }
}

// ---------- candidates ----------
var candidates = new List<View>();
foreach (View v in new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>())
{
    if (v == null || !v.IsValidObject) continue;
    if (v.IsTemplate) continue;                     // a template is never "on a sheet"
    if (placed.Contains(v.Id)) continue;

    string k;
    switch (v.ViewType)
    {
        case ViewType.ThreeD:        k = "3d"; break;
        case ViewType.Section:       k = "section"; break;
        case ViewType.Schedule:      k = "schedule"; break;
        case ViewType.Legend:        k = "legend"; break;
        case ViewType.DraftingView:  k = "drafting"; break;
        default: continue;
    }
    if (!want.Contains(k)) continue;

    // The default {3D} view is unplaced by nature — never offer it.
    if (v.ViewType == ViewType.ThreeD && (v.Name ?? "").Trim() == "{3D}") continue;

    // Revit's own internal schedules are not user views.
    var vs = v as ViewSchedule;
    if (vs != null && (vs.IsTitleblockRevisionSchedule || vs.IsInternalKeynoteSchedule)) continue;

    if (!string.IsNullOrWhiteSpace(nameContains) &&
        (v.Name ?? "").IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

    candidates.Add(v);
}

sb.AppendLine((dryRun ? "DRY RUN — nothing deleted. " : "") +
              $"{candidates.Count} view(s) are on no sheet, of kinds: {string.Join(", ", want)}." +
              $"  ({placed.Count} view placement(s) found — Viewports AND ScheduleSheetInstances.)");

if (candidates.Count == 0) return sb.ToString();

var byKind = candidates.GroupBy(v => v.ViewType.ToString()).OrderBy(g => g.Key);
sb.AppendLine("");
foreach (var g in byKind) sb.AppendLine("  " + g.Key + ": " + g.Count());

sb.AppendLine("");
sb.AppendLine("NOT on a sheet does NOT mean not needed — working views, coordination views and the " +
              "targets of section/callout markers are all legitimately unplaced. Read this list.");
sb.AppendLine("");
sb.AppendLine("Id | Type | Name");
sb.AppendLine("--- | --- | ---");
foreach (var v in candidates.OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name).Take(50))
    sb.AppendLine(v.Id + " | " + v.ViewType + " | " + (v.Name ?? ""));
if (candidates.Count > 50) sb.AppendLine("... and " + (candidates.Count - 50) + " more");

if (dryRun) return sb.ToString();

if (candidates.Count > maxDelete)
{
    sb.AppendLine("");
    sb.AppendLine($"STOPPED: {candidates.Count} views exceeds maxDelete ({maxDelete}). Narrow with " +
                  "`kinds` or `nameContains`, or raise the cap deliberately.");
    return sb.ToString();
}

int deleted = 0, failed = 0;
using (var t = new Transaction(Document, "AJ Tools - Purge Unplaced Views"))
{
    t.Start();
    try
    {
        foreach (var v in candidates)
        {
            try { Document.Delete(v.Id); deleted++; } catch { failed++; }
        }
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing deleted. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine("");
sb.AppendLine($"DELETED {deleted} view(s)" + (failed > 0 ? $", {failed} refused" : "") +
              ". Anything drawn inside a deleted drafting view or legend went with it.");

return sb.ToString();
