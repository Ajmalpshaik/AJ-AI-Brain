// ============================================================
// FRAGMENT (action) — action-report-views-showing-element.cs
// PURPOSE: For each element in `elements`, list every VIEW it is visible in and every SHEET those views
//          are on — "which drawings show this valve", "if I move this, what has to be re-checked",
//          "where is this thing actually drawn". The lookup that turns a model change into a list of
//          drawings to reissue.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-id-list.cs
//          with the Ids read off a clash report or a comment.
//
// ✱✱ THERE IS NO "WHICH VIEWS SHOW THIS" API. Visibility is not a property of an element — it is the
//    result of that view's crop, its view range, its filters, its category settings, its phase and its
//    discipline all applied together. The only truthful way to ask is to run a collector SCOPED TO EACH
//    VIEW and see whether the element comes back:
//        new FilteredElementCollector(Document, view.Id)
//    That is Revit answering with the same machinery it uses to draw, so it accounts for every one of
//    those rules at once. It is also why this is slow, and why the view filters below matter.
//
// ✱✱ WHAT "VISIBLE" HONESTLY MEANS HERE: in this view's element set. An element hidden by a temporary
//    hide/isolate still counts (that state is per-session and not saved), and one behind another object
//    counts (it is drawn, just obscured). Everything permanent — cropped out, filtered out, category off,
//    wrong phase, view range miss — is correctly excluded.
//
// GOTCHA: THIS IS A COLLECTOR PER VIEW. On a model with 800 views it is a real wait. Use
//         `skipViewsNotOnSheets` (the default) — a view nobody has put on a sheet is rarely the answer to
//         "which drawings", and it usually removes most of the count.
// GOTCHA: SCHEDULES do not have a geometric element set; an element scheduled but not drawn will not
//         appear. Schedules are counted separately so the absence is visible rather than misleading.
// GOTCHA: views in a LINKED model are not searched — a different document.
// RELATED: filter-by-elements-in-view.cs goes the other way (what is in THIS view);
//          filter-by-sheets.cs and action-report-sheet-contents.cs work at sheet level.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE element first and check the
//   answer against a view you already know shows it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool skipViewsNotOnSheets = true;   // only search views that are actually placed on a sheet
bool includeTemplates = false;      // templates draw nothing; almost never wanted
int maxElementsReported = 25;
int maxViewsSearched = 400;         // stop runaway on a huge model; 0 = no cap
// ---- END INPUTS ----

if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this (filter-by-id-list.cs if you have Ids).");
    return sb.ToString();
}

// ---- which views are on a sheet, and which sheet ----
var viewToSheet = new Dictionary<ElementId, string>();
foreach (var sheet in new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>())
{
    foreach (var vpId in sheet.GetAllViewports())
    {
        var vp = Document.GetElement(vpId) as Viewport;
        if (vp != null && !viewToSheet.ContainsKey(vp.ViewId))
            viewToSheet[vp.ViewId] = $"{sheet.SheetNumber} — {sheet.Name}";
    }
    // Schedules sit on sheets as ScheduleSheetInstance, not Viewport.
    foreach (var ssi in new FilteredElementCollector(Document, sheet.Id)
                 .OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
    {
        if (!viewToSheet.ContainsKey(ssi.ScheduleId))
            viewToSheet[ssi.ScheduleId] = $"{sheet.SheetNumber} — {sheet.Name}";
    }
}

var views = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => includeTemplates || !v.IsTemplate)
    .Where(v => !(v is ViewSheet))
    .Where(v => !skipViewsNotOnSheets || viewToSheet.ContainsKey(v.Id))
    .ToList();

int schedulesSkipped = views.Count(v => v.ViewType == ViewType.Schedule);
views = views.Where(v => v.ViewType != ViewType.Schedule).ToList();

if (maxViewsSearched > 0 && views.Count > maxViewsSearched)
{
    sb.AppendLine($"STOPPED — {views.Count} views to search, over the cap of {maxViewsSearched}.");
    sb.AppendLine("Set skipViewsNotOnSheets = true (it already is by default) or raise maxViewsSearched.");
    return sb.ToString();
}

var targets = new HashSet<ElementId>(elements.Select(e => e.Id));
var hits = new Dictionary<ElementId, List<View>>();
foreach (var id in targets) hits[id] = new List<View>();

sb.AppendLine($"Searching {views.Count} view(s)"
            + (skipViewsNotOnSheets ? " (only those placed on sheets)" : " (every view)")
            + $" for {targets.Count} element(s).");
if (schedulesSkipped > 0)
    sb.AppendLine($"{schedulesSkipped} schedule(s) not searched — a schedule has no geometric element set.");
sb.AppendLine();

foreach (var v in views)
{
    ICollection<ElementId> inView = null;
    try { inView = new FilteredElementCollector(Document, v.Id).WhereElementIsNotElementType().ToElementIds(); }
    catch { continue; }   // some view types refuse a scoped collector

    foreach (var id in targets)
        if (inView.Contains(id)) hits[id].Add(v);
}

int found = 0, missing = 0;
foreach (var el in elements.Take(maxElementsReported))
{
    var list = hits[el.Id];
    string label = $"{el.Category?.Name ?? el.GetType().Name} {el.Id}"
                 + (string.IsNullOrWhiteSpace(el.Name) ? "" : $" — {el.Name}");

    if (list.Count == 0)
    {
        missing++;
        sb.AppendLine($"{label}");
        sb.AppendLine(skipViewsNotOnSheets
            ? "    NOT ON ANY SHEET. It may still be visible in working views — set skipViewsNotOnSheets = false to check."
            : "    NOT VISIBLE IN ANY VIEW. Check its phase, its workset, and whether its category is switched off everywhere.");
        continue;
    }

    found++;
    var onSheets = list.Where(v => viewToSheet.ContainsKey(v.Id)).ToList();
    sb.AppendLine($"{label}  —  {list.Count} view(s), {onSheets.Count} on sheets");
    foreach (var v in list.OrderBy(x => viewToSheet.ContainsKey(x.Id) ? 0 : 1).ThenBy(x => x.Name).Take(12))
        sb.AppendLine($"    [{v.ViewType}] {v.Name}"
                    + (viewToSheet.ContainsKey(v.Id) ? $"   →  SHEET {viewToSheet[v.Id]}" : ""));
    if (list.Count > 12) sb.AppendLine($"    ... and {list.Count - 12} more view(s).");
    sb.AppendLine();
}

if (elements.Count > maxElementsReported)
    sb.AppendLine($"... {elements.Count - maxElementsReported} more element(s) not listed (raise maxElementsReported).");

sb.AppendLine();
sb.AppendLine($"{found} element(s) appear on at least one view; {missing} appear on none.");
var allSheets = hits.Values.SelectMany(l => l).Where(v => viewToSheet.ContainsKey(v.Id))
                   .Select(v => viewToSheet[v.Id]).Distinct().OrderBy(s => s).ToList();
if (allSheets.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"SHEETS AFFECTED ({allSheets.Count}) — this is the reissue list:");
    foreach (var s in allSheets) sb.AppendLine($"    {s}");
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
