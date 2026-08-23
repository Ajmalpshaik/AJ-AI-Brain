// ============================================================
// FRAGMENT (action) — action-place-views-on-new-sheets.cs
// PURPOSE: Give every view in `elements` its OWN new sheet — create the sheet, number it from a running
//          series, name it after the view, and drop the view on it. The "I have forty sections and I
//          need forty drawings" job.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — Views, e.g. from
//          filter-by-views.cs.
//
// ✱✱ MOST VIEWS CAN ONLY BE ON ONE SHEET, EVER. Revit does not warn you politely — Viewport.Create
//    throws, and if that happens after the sheet is made you are left with an empty sheet that then has
//    to be hunted down and deleted. So this CHECKS FIRST: the view's Sheet Number parameter reads "---"
//    when the view is not placed anywhere. Already-placed views are listed with the sheet they are on
//    and skipped BEFORE any sheet is created.
//
//    ✱ CORRECTED 2026-08-23, same day it was written. The first version treated LEGENDS as the only
//      view type that may repeat across sheets. That is wrong and would have wrongly skipped views that
//      are perfectly placeable. The full list of view types that CAN go on more than one sheet is:
//          Legend · Schedule · DraftingView · ColumnSchedule · PanelSchedule · CostReport · LoadsReport
//      Everything else — plans, sections, elevations, 3D, callouts — is one-sheet-only. Note this also
//      means SCHEDULES are repeatable, which is separate from the fact that they are placed with
//      ScheduleSheetInstance rather than Viewport (see the gotcha below).
//
// ✱✱ A DUPLICATE SHEET NUMBER ALSO THROWS. The series is checked against the numbers already in the
//    model before anything is created, and a clash stops the run with the list, rather than creating
//    half the sheets and failing on the rest.
//
// GOTCHA: SCHEDULES DO NOT GO ON SHEETS AS VIEWPORTS. Revit places them with ScheduleSheetInstance, not
//         Viewport, so schedules in the incoming set are reported and skipped rather than silently lost.
// GOTCHA: the view lands at the sheet's ORIGIN unless centreOnSheet is set, which puts it in the middle
//         of the title block. Neither is a proper layout — use
//         action-align-viewports-across-sheets.cs afterwards to line them all up.
// GOTCHA: DRY RUN BY DEFAULT — it prints the exact sheet number and name each view would get.
// RELATED: action-place-viewport-on-sheet.cs puts views on ONE existing sheet; create-sheet.cs makes a
//          single sheet.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on TWO views first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string titleBlockTypeName = null;  // exact title block type name; null = first one in the project
string numberPrefix = "A-";        // sheet numbers become prefix + running count
int startNumber = 1;               // first number in the series
int numberDigits = 2;              // 2 -> "01", 3 -> "001"
bool nameSheetAfterView = true;    // sheet name = view name
bool centreOnSheet = true;         // drop the view in the middle of the title block, not at 0,0
bool dryRun = true;                // true = report only, create nothing
// ---- END INPUTS ----

var views = elements.OfType<View>().Where(v => !v.IsTemplate && !(v is ViewSheet)).ToList();
int created = 0, skippedPlaced = 0, skippedSchedule = 0, failed = 0;
var notes = new List<string>();

// Revit's own answer to "is this view free to place on a sheet", from 2022 onward. Absent on 2020/2021,
// so it is looked up by name once — naming it in code would stop this fragment compiling there, and a
// try/catch does NOT help because that is a compile error, not a runtime one.
var _mPlacementStatus = typeof(View).GetMethod("GetPlacementOnSheetStatus", Type.EmptyTypes);

var titleBlocks = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType()
    .Cast<FamilySymbol>().ToList();
var tb = titleBlockTypeName == null
    ? titleBlocks.FirstOrDefault()
    : titleBlocks.FirstOrDefault(s => s.Name == titleBlockTypeName
                                   || $"{s.FamilyName}: {s.Name}" == titleBlockTypeName);

var existingNumbers = new HashSet<string>(
    new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
        .Select(s => s.SheetNumber));

if (views.Count == 0)
{
    sb.AppendLine("No placeable Views in `elements` — put filter-by-views.cs above this.");
}
else if (tb == null)
{
    sb.AppendLine(titleBlockTypeName == null
        ? "This project has no title block family loaded — load one first."
        : $"Title block '{titleBlockTypeName}' not found. Available: {string.Join(", ", titleBlocks.Select(s => $"{s.FamilyName}: {s.Name}"))}");
}
else
{
    // ---- work out the plan before creating anything ----
    var plan = new List<Tuple<View, string>>();
    int n = startNumber;
    var wouldUse = new HashSet<string>();
    var clashes = new List<string>();

    foreach (var v in views)
    {
        if (v.ViewType == ViewType.Schedule || v is ViewSchedule)
        {
            skippedSchedule++;
            notes.Add($"  '{v.Name}': schedules are placed differently (not as a Viewport) — skipped.");
            continue;
        }

        // The one check that has to happen before any sheet exists — but only for the view types that
        // genuinely cannot repeat. See the corrected list in the header.
        //
        // 2026-08-23: Revit answers this itself, from 2022 onward, with `View.GetPlacementOnSheetStatus()`
        // — no sheet needed, no list to maintain. It is used FIRST where it exists and the hand-written
        // list below is the fallback for Revit 2020/2021, which do not have it. Reached by reflection
        // because naming a method that is absent is a COMPILE error and a try/catch does not help.
        // (Confirmed against the shipped RevitAPI.dll: absent on 2020, present on 2024.)
        // The list stayed wrong once already — it is a rule written by hand where Revit has a fact.
        bool askedRevit = false;
        if (_mPlacementStatus != null)
        {
            try
            {
                var status = _mPlacementStatus.Invoke(v, null);
                string s = status != null ? status.ToString() : "";
                askedRevit = true;
                // "NotPlaced" is the only status that means "free to place on a new sheet". Anything
                // else — already on a sheet, or not placeable at all — is a skip, and the status name
                // is reported so the reason is Revit's own words rather than an inference.
                if (!string.Equals(s, "NotPlaced", StringComparison.OrdinalIgnoreCase))
                {
                    skippedPlaced++;
                    notes.Add($"  '{v.Name}': Revit reports placement status {s} — not free to place on a new sheet.");
                    continue;
                }
            }
            catch { askedRevit = false; }
        }

        bool mayRepeat = v.ViewType == ViewType.Legend
                      || v.ViewType == ViewType.Schedule
                      || v.ViewType == ViewType.DraftingView
                      || v.ViewType == ViewType.ColumnSchedule
                      || v.ViewType == ViewType.PanelSchedule
                      || v.ViewType == ViewType.CostReport
                      || v.ViewType == ViewType.LoadsReport;

        // Only reached when Revit could not be asked (2020/2021). Where it could, the status above is
        // authoritative and this list is not consulted at all.
        if (!askedRevit && !mayRepeat)
        {
            var p = v.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER);
            var on = p?.AsString();
            if (!string.IsNullOrEmpty(on) && on != "---")
            {
                skippedPlaced++;
                notes.Add($"  '{v.Name}': already on sheet {on} — a view can only be on one sheet.");
                continue;
            }
        }

        string num = numberPrefix + n.ToString("D" + numberDigits);
        if (existingNumbers.Contains(num) || wouldUse.Contains(num)) clashes.Add(num);
        wouldUse.Add(num);
        plan.Add(Tuple.Create(v, num));
        n++;
    }

    if (clashes.Count > 0)
    {
        sb.AppendLine($"STOPPED — {clashes.Count} sheet number(s) in this series already exist: {string.Join(", ", clashes.Distinct())}");
        sb.AppendLine("Change numberPrefix or startNumber and run again. Nothing was created.");
    }
    else if (plan.Count == 0)
    {
        sb.AppendLine("Nothing left to place after the checks below.");
        foreach (var note in notes) sb.AppendLine(note);
    }
    else
    {
        sb.AppendLine($"{views.Count} view(s) in; {plan.Count} will get a sheet. Title block: '{tb.FamilyName}: {tb.Name}'.");
        sb.AppendLine();

        using (var t = new Transaction(Document, "AJ Tools - Place views on new sheets"))
        {
            if (!dryRun) t.Start();

            foreach (var item in plan)
            {
                var v = item.Item1; var num = item.Item2;

                if (dryRun)
                {
                    sb.AppendLine($"  '{v.Name}'  ->  sheet {num}" + (nameSheetAfterView ? $" — \"{v.Name}\"" : ""));
                    created++;
                    continue;
                }

                ViewSheet sheet = null;
                try
                {
                    sheet = ViewSheet.Create(Document, tb.Id);
                    sheet.SheetNumber = num;
                    if (nameSheetAfterView) { try { sheet.Name = v.Name; } catch { } }

                    var centre = XYZ.Zero;
                    if (centreOnSheet)
                    {
                        var tbInst = new FilteredElementCollector(Document, sheet.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType()
                            .FirstOrDefault();
                        var bb = tbInst?.get_BoundingBox(sheet);
                        if (bb != null) centre = (bb.Min + bb.Max) / 2.0;
                    }

                    if (!Viewport.CanAddViewToSheet(Document, sheet.Id, v.Id))
                        throw new Exception("Revit will not put this view on a sheet (CanAddViewToSheet said no)");

                    Viewport.Create(Document, sheet.Id, v.Id, centre);
                    created++;
                    sb.AppendLine($"  sheet {num} — \"{sheet.Name}\"  <-  '{v.Name}'");
                }
                catch (Exception ex)
                {
                    failed++;
                    notes.Add($"  '{v.Name}': FAILED — {ex.Message}");
                    // Do not leave an empty sheet behind.
                    if (sheet != null) { try { Document.Delete(sheet.Id); } catch { } }
                }
            }

            if (!dryRun) t.Commit();
        }

        sb.AppendLine();
        sb.AppendLine(dryRun
            ? $"DRY RUN — nothing created. {created} sheet(s) would be made. Set dryRun = false to create."
            : $"Created {created} sheet(s), each with its view on it.");
        if (skippedPlaced > 0)   sb.AppendLine($"Already on a sheet, skipped: {skippedPlaced}.");
        if (skippedSchedule > 0) sb.AppendLine($"Schedules, skipped: {skippedSchedule}.");
        if (failed > 0)          sb.AppendLine($"Failed: {failed}.");
        if (!dryRun && created > 0)
            sb.AppendLine("Next: action-align-viewports-across-sheets.cs to line the plans up in the same place on every sheet.");
        foreach (var note in notes) sb.AppendLine(note);
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
