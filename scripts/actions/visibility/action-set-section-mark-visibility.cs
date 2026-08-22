// ============================================================
// FRAGMENT (action) — action-set-section-mark-visibility.cs
// PURPOSE: Show only the SECTION MARKS whose section view is actually ON A SHEET, and hide the rest —
//          the pre-issue tidy-up so a plan is not covered in markers pointing at working sections
//          nobody will ever receive. Or unhide them all again in one call.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// GOTCHA: THE MARK IS NOT THE SECTION. What you hide in a plan is the section's **`Viewer` element**
//         (`BuiltInCategory.OST_Viewers`) — the little marker. The `ViewSection` itself is a view and
//         is not hidden at all. Hiding the view would be a different, much worse thing.
// GOTCHA: "IS IT ON A SHEET" IS READ FROM THE SECTION VIEW'S OWN `VIEWER_SHEET_NUMBER` PARAMETER —
//         Revit fills it in when the view is placed. Do not go hunting through Viewports for this; the
//         parameter is the answer and it is one read.
// GOTCHA: IT ALWAYS UNHIDES EVERYTHING FIRST, then hides what should be hidden. Without that reset, a
//         mark hidden on a previous run stays hidden after its section is placed on a sheet — the tool
//         would only ever hide, never restore, and the drawing would quietly lose marks over time.
// GOTCHA: DRY RUN BY DEFAULT — it changes what a drawing shows.
// GOTCHA: this is PER VIEW. `View.HideElements`/`UnhideElements` are view-specific overrides, so the
//         same plan on another sheet is unaffected. Run it on each plan you are issuing.
// GOTCHA: an element that is hidden by CATEGORY or by a view FILTER will not reappear from
//         `UnhideElements` — that is a different mechanism entirely. If marks stay invisible after
//         `mode = "showAll"`, check Visibility/Graphics, see
//         knowledge/live-model/graphic-override-precedence.md.
// NOTE: matching a marker to its section is by NAME. A model with two sections of the same name in
//       different view types is ambiguous — those are reported rather than guessed at.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Section Mark Visibility.
//   Compile-checked on 2020/2024/2027. Dry-run it and read the on-sheet / not-on-sheet split first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "onSheetOnly";         // "onSheetOnly" = hide marks whose section is on no sheet | "showAll"
bool dryRun = true;                  // true = report only; false = actually change visibility
int? targetViewIdInt = null;         // null = active view
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

View view = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (view == null || view.IsTemplate)
{
    sb.AppendLine("Target view not found or is a template.");
    return sb.ToString();
}

// ---------- which sections are on a sheet ----------
var onSheet = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (ViewSection vs in new FilteredElementCollector(Document).OfClass(typeof(ViewSection)).Cast<ViewSection>())
{
    if (vs == null || vs.IsTemplate) continue;
    string name = vs.Name ?? "";
    if (name.Length == 0) continue;

    string sheetNo = "";
    try
    {
        var p = vs.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER);
        if (p != null && p.HasValue) sheetNo = p.AsString() ?? "";
    }
    catch { }

    bool placed = !string.IsNullOrWhiteSpace(sheetNo) && sheetNo.Trim() != "---";

    if (onSheet.ContainsKey(name) && onSheet[name] != placed) ambiguous.Add(name);
    onSheet[name] = placed;
}

// ---------- the MARKS in this view — Viewers, not the sections themselves ----------
var markers = new FilteredElementCollector(Document, view.Id)
    .OfCategory(BuiltInCategory.OST_Viewers)
    .WhereElementIsNotElementType()
    .ToElements();

if (markers.Count == 0)
{
    sb.AppendLine($"No section/elevation marks found in '{view.Name}'.");
    return sb.ToString();
}

var toHide = new List<ElementId>();
var toUnhide = new List<ElementId>();
int keptVisible = 0, unknown = 0;
var rows = new List<string>();

foreach (var m in markers)
{
    string name = m.Name ?? "";
    bool known = onSheet.ContainsKey(name);
    bool placed = known && onSheet[name];

    bool shouldHide = mode.Equals("showAll", StringComparison.OrdinalIgnoreCase) ? false : (known && !placed);

    if (!known) unknown++;

    if (shouldHide) toHide.Add(m.Id); else { toUnhide.Add(m.Id); keptVisible++; }

    if (rows.Count < 30)
        rows.Add(m.Id + " | " + name + " | " +
                 (!known ? "no matching section view" : (placed ? "on a sheet" : "NOT on a sheet")) + " | " +
                 (shouldHide ? "hide" : "show"));
}

sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
              $"'{view.Name}': {markers.Count} section/elevation mark(s) — " +
              $"{(mode.Equals("showAll", StringComparison.OrdinalIgnoreCase) ? "SHOW ALL" : "keep only those on a sheet")}: " +
              $"show {keptVisible}, hide {toHide.Count}.");
if (unknown > 0)
    sb.AppendLine($"{unknown} mark(s) had no section view matching by name — left VISIBLE rather than guessed at.");
if (ambiguous.Count > 0)
    sb.AppendLine("AMBIGUOUS names (two sections share a name and disagree on sheet placement): " +
                  string.Join(", ", ambiguous.Take(10)));

if (!dryRun)
{
    using (var t = new Transaction(Document, "AJ Tools - Section Mark Visibility"))
    {
        t.Start();
        try
        {
            // ALWAYS unhide first — otherwise a mark hidden on an earlier run never comes back once
            // its section is placed on a sheet, and the drawing quietly loses marks over time.
            var currentlyHidden = toUnhide.Concat(toHide)
                .Where(id => { try { return Document.GetElement(id).IsHidden(view); } catch { return false; } })
                .ToList();
            if (currentlyHidden.Count > 0) view.UnhideElements(currentlyHidden);

            if (toHide.Count > 0) view.HideElements(toHide);
            t.Commit();
            sb.AppendLine($"Applied: unhid {currentlyHidden.Count} first, then hid {toHide.Count}.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
            return sb.ToString();
        }
    }
}

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Mark Id | Name | Section status | Action");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (markers.Count > rows.Count) sb.AppendLine("... and " + (markers.Count - rows.Count) + " more");
}

return sb.ToString();
