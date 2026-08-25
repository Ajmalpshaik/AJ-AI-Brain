// ============================================================
// FRAGMENT (action) — action-align-viewports-across-sheets.cs
// PURPOSE: Make the plan sit in exactly the SAME position on every sheet — pick one sheet as the master
//          and every other sheet's viewport moves to match it. The "why does the plan jump when I flick
//          through the drawings" fix, done in one pass instead of nudging each sheet by eye.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — ViewSheets, e.g. from
//          filter-by-sheets.cs. The master sheet is named by number and may be inside or outside that set.
//
// ✱✱ THE TWO THINGS THAT DECIDE WHETHER THIS IS EVEN MEANINGFUL, both checked before anything moves:
//    1. THE TITLE BLOCK MUST BE AT THE SAME PLACE ON EVERY SHEET. A viewport's box centre is measured in
//       SHEET space, not from the title block. If one sheet's title block sits at a different origin,
//       copying the box centre lands the plan in the right sheet coordinate and the WRONG place on the
//       paper. Revit does not stop you and nothing looks wrong until it prints. This reports any title
//       block whose origin differs from the master's, and (on request) moves them to match.
//    2. THE VIEW SCALE MUST MATCH. Two viewports at different scales cannot line up by their centres —
//       the same centre puts different amounts of building in the same box. Mismatched sheets are
//       reported and skipped, never silently "aligned".
//
// GOTCHA: this moves VIEWPORTS, not views — the model is untouched and nothing in it moves.
// GOTCHA: a sheet with more than one plan viewport is ambiguous (which one is "the" plan?) and is
//         skipped with a note, unless alignAllPlanViewports is set.
// GOTCHA: DRY RUN BY DEFAULT — read the list, then set dryRun = false.
// RELATED: action-place-viewport-on-sheet.cs puts a view on a sheet in the first place.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. GetBoxCenter/SetBoxCenter are the same calls
//   already proven in action-duplicate-sheet.cs. Run it on two sheets first and look at the paper.
// ⚠ The alignLegends CODE was only added 2026-08-25 — an audit found the input declared and described
//   while the body never read it (setting it true did nothing). Legends move by matching view NAME
//   against the master sheet's legends, no scale gate. Untested like the rest of the file.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string masterSheetNumber = "";        // REQUIRED — the sheet whose layout everything copies
bool alignPlans = true;               // move plan/ceiling-plan viewports
bool alignLegends = false;            // also move legend viewports whose view NAME matches the master's
bool moveTitleBlocksToMatch = false;  // fix title blocks that sit at a different origin (see note 1)
bool alignAllPlanViewports = false;   // true = align every plan viewport, not just single-plan sheets
bool dryRun = true;                   // true = report only, move nothing
// ---- END INPUTS ----

var sheets = elements.OfType<ViewSheet>().ToList();
int moved = 0, movedLegends = 0, skippedScale = 0, skippedAmbiguous = 0, skippedNoPlan = 0, titleBlocksMoved = 0;
var notes = new List<string>();

// Find the master among ALL sheets, not just the incoming set — it is a reference, not a target.
var master = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .FirstOrDefault(s => s.SheetNumber == masterSheetNumber);

// The plan viewports on a sheet, and the sheet's title block origin.
Func<ViewSheet, List<Viewport>> planViewports = (sheet) =>
    sheet.GetAllViewports().Select(id => Document.GetElement(id) as Viewport)
         .Where(vp => vp != null)
         .Where(vp => { var v = Document.GetElement(vp.ViewId) as View;
                        return v != null && (v.ViewType == ViewType.FloorPlan
                                          || v.ViewType == ViewType.CeilingPlan
                                          || v.ViewType == ViewType.EngineeringPlan
                                          || v.ViewType == ViewType.AreaPlan); })
         .ToList();

Func<ViewSheet, List<Viewport>> legendViewports = (sheet) =>
    sheet.GetAllViewports().Select(id => Document.GetElement(id) as Viewport)
         .Where(vp => vp != null)
         .Where(vp => { var v = Document.GetElement(vp.ViewId) as View;
                        return v != null && v.ViewType == ViewType.Legend; })
         .ToList();

Func<ViewSheet, FamilyInstance> titleBlockOf = (sheet) =>
    new FilteredElementCollector(Document, sheet.Id)
        .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType()
        .Cast<FamilyInstance>().FirstOrDefault();

Func<View, int> scaleOf = (v) => { try { return v.Scale; } catch { return -1; } };

if (string.IsNullOrWhiteSpace(masterSheetNumber))
{
    sb.AppendLine("masterSheetNumber is required — name the sheet whose layout everything should copy.");
}
else if (master == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                      .Select(s => s.SheetNumber).OrderBy(s => s).Take(30);
    sb.AppendLine($"No sheet numbered '{masterSheetNumber}'. First 30 sheet numbers in this model: {string.Join(", ", available)}");
}
else if (sheets.Count == 0)
{
    sb.AppendLine("No ViewSheets in `elements` — put filter-by-sheets.cs above this.");
}
else
{
    var masterPlans = planViewports(master);
    if (masterPlans.Count == 0)
    {
        sb.AppendLine($"Master sheet '{masterSheetNumber}' has no plan viewport on it — nothing to copy from.");
    }
    else if (masterPlans.Count > 1 && !alignAllPlanViewports)
    {
        sb.AppendLine($"Master sheet '{masterSheetNumber}' has {masterPlans.Count} plan viewports — ambiguous. "
                    + "Use a single-plan sheet as the master, or set alignAllPlanViewports = true.");
    }
    else
    {
        var masterVp = masterPlans.First();
        var masterView = Document.GetElement(masterVp.ViewId) as View;
        var masterCentre = masterVp.GetBoxCenter();
        int masterScale = scaleOf(masterView);
        var masterTb = titleBlockOf(master);
        XYZ masterTbOrigin = (masterTb != null && masterTb.Location is LocationPoint)
            ? ((LocationPoint)masterTb.Location).Point : null;

        sb.AppendLine($"Master: sheet '{master.SheetNumber} — {master.Name}', view '{masterView.Name}', scale 1:{masterScale}.");
        sb.AppendLine($"Target box centre: ({masterCentre.X:F4}, {masterCentre.Y:F4}) in sheet feet.");
        sb.AppendLine($"{sheets.Count} sheet(s) in.");
        sb.AppendLine();

        // Legend targets: view name -> the master sheet's box centre for that legend (note: the SAME
        // legend view can legally sit on many sheets, which is what makes name-matching meaningful).
        var masterLegendCentres = new Dictionary<string, XYZ>();
        if (alignLegends)
        {
            foreach (var lvp in legendViewports(master))
            {
                var lv = Document.GetElement(lvp.ViewId) as View;
                if (lv != null && !masterLegendCentres.ContainsKey(lv.Name))
                    masterLegendCentres[lv.Name] = lvp.GetBoxCenter();
            }
            if (masterLegendCentres.Count == 0)
                notes.Add("  alignLegends is on, but the master sheet carries no legend viewport — no legend targets.");
        }

        using (var t = new Transaction(Document, "AJ Tools - Align viewports across sheets"))
        {
            if (!dryRun) t.Start();

            foreach (var sheet in sheets)
            {
                if (sheet.Id == master.Id) continue;

                // --- title block origin check (note 1) ---
                if (masterTbOrigin != null)
                {
                    var tb = titleBlockOf(sheet);
                    if (tb != null && tb.Location is LocationPoint)
                    {
                        var o = ((LocationPoint)tb.Location).Point;
                        if (!o.IsAlmostEqualTo(masterTbOrigin))
                        {
                            if (moveTitleBlocksToMatch && !dryRun)
                            {
                                try { ((LocationPoint)tb.Location).Point = masterTbOrigin; titleBlocksMoved++; }
                                catch (Exception ex) { notes.Add($"  '{sheet.SheetNumber}': title block would not move — {ex.Message}"); }
                            }
                            else
                            {
                                notes.Add($"  '{sheet.SheetNumber}': TITLE BLOCK sits at a different origin than the master's. "
                                        + "Alignment will be correct in sheet coordinates but wrong on the paper. "
                                        + "Set moveTitleBlocksToMatch = true to fix it.");
                            }
                        }
                    }
                }

                // --- legends (alignLegends) — by matching view NAME against the master's legends ---
                if (alignLegends && masterLegendCentres.Count > 0)
                {
                    foreach (var lvp in legendViewports(sheet))
                    {
                        var lv = Document.GetElement(lvp.ViewId) as View;
                        XYZ target;
                        if (lv == null || !masterLegendCentres.TryGetValue(lv.Name, out target)) continue;
                        var cur = lvp.GetBoxCenter();
                        if (cur.IsAlmostEqualTo(target)) continue;
                        if (dryRun)
                        {
                            sb.AppendLine($"  would move legend '{sheet.SheetNumber}' / '{lv.Name}': "
                                        + $"({cur.X:F4}, {cur.Y:F4}) -> ({target.X:F4}, {target.Y:F4})");
                            movedLegends++;
                        }
                        else
                        {
                            try { lvp.SetBoxCenter(target); movedLegends++;
                                  sb.AppendLine($"  moved legend '{sheet.SheetNumber}' / '{lv.Name}'"); }
                            catch (Exception ex) { notes.Add($"  '{sheet.SheetNumber}': legend '{lv.Name}' would not move — {ex.Message}"); }
                        }
                    }
                }

                if (!alignPlans) continue;

                var vps = planViewports(sheet);
                if (vps.Count == 0) { skippedNoPlan++; notes.Add($"  '{sheet.SheetNumber}': no plan viewport — skipped."); continue; }
                if (vps.Count > 1 && !alignAllPlanViewports)
                {
                    skippedAmbiguous++;
                    notes.Add($"  '{sheet.SheetNumber}': {vps.Count} plan viewports — ambiguous, skipped. Set alignAllPlanViewports = true to move them all.");
                    continue;
                }

                foreach (var vp in vps)
                {
                    var v = Document.GetElement(vp.ViewId) as View;
                    int s = scaleOf(v);
                    if (s != masterScale)
                    {
                        skippedScale++;
                        notes.Add($"  '{sheet.SheetNumber}': view '{v.Name}' is 1:{s}, master is 1:{masterScale} — cannot align across scales, skipped.");
                        continue;
                    }

                    var current = vp.GetBoxCenter();
                    if (current.IsAlmostEqualTo(masterCentre)) continue;

                    if (dryRun)
                    {
                        sb.AppendLine($"  would move '{sheet.SheetNumber}' / '{v.Name}': "
                                    + $"({current.X:F4}, {current.Y:F4}) -> ({masterCentre.X:F4}, {masterCentre.Y:F4})");
                        moved++;
                    }
                    else
                    {
                        try { vp.SetBoxCenter(masterCentre); moved++;
                              sb.AppendLine($"  moved '{sheet.SheetNumber}' / '{v.Name}'"); }
                        catch (Exception ex) { notes.Add($"  '{sheet.SheetNumber}': would not move — {ex.Message}"); }
                    }
                }
            }

            if (!dryRun) t.Commit();
        }

        sb.AppendLine();
        sb.AppendLine(dryRun
            ? $"DRY RUN — nothing moved. {moved} plan viewport(s) and {movedLegends} legend viewport(s) would move. Set dryRun = false to apply."
            : $"Moved {moved} plan viewport(s) and {movedLegends} legend viewport(s). Title blocks re-origined: {titleBlocksMoved}.");
        if (skippedScale > 0)     sb.AppendLine($"Skipped for scale mismatch: {skippedScale}.");
        if (skippedAmbiguous > 0) sb.AppendLine($"Skipped as ambiguous (more than one plan): {skippedAmbiguous}.");
        if (skippedNoPlan > 0)    sb.AppendLine($"Skipped with no plan viewport: {skippedNoPlan}.");
        if (notes.Count > 0) { sb.AppendLine(); foreach (var n in notes) sb.AppendLine(n); }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
