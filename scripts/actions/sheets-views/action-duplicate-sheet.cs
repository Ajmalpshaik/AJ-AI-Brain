// ============================================================
// FRAGMENT (action) — action-duplicate-sheet.cs
// PURPOSE: Duplicate each ViewSheet in `elements` — new sheet with the same title block, plus (on
//          request) duplicates of every placed view dropped at the SAME viewport positions, and
//          schedules re-placed at their same spots. The job Revit's UI still doesn't do in one click.
// ASSUMES: elements (List<Element>, ViewSheets — from filters/by-view-and-sheet/filter-by-sheets.cs) and sb exist from a
//          filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Produces `newSheetIds` for chaining.
// GOTCHA: sheet numbers must be unique — the new number is old number + numberSuffix; a collision fails
//         that one sheet (reported), the part-built sheet is deleted, and the rest continue.
//         FIXED 2026-08-07 — this used to be a false promise. The sheet is created before its number is
//         assigned, so a collision threw AFTER creation and the per-sheet catch just counted it as
//         failed: the sheet stayed, committed, carrying Revit's own auto-number. Measured live —
//         duplicating 'A-101' into an already-taken 'A-101-COPY' reported "Duplicated 0 of 1 sheet(s),
//         1 failed" while the sheet count went 2 -> 3 and an orphan 'A-103' remained.
// NEEDS allowDestructive: true on the bridge call — only to delete its own half-built sheet on failure,
//         but the bridge's guard matches on the text `Document.Delete` regardless of the target.
// GOTCHA: what carries over: title block, duplicated views at same positions, schedules. What does NOT:
//         loose annotations drawn directly on the sheet, guide grids, revisions on the sheet.
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED — created 2026-07-26, round 3.", which the README had already superseded. The verification was recorded there and
//          never here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string numberSuffix = "-COPY";          // new sheet number = old number + this (must yield unique numbers)
string namePrefix = "";                 // optional prefix on the new sheet's NAME
bool copyViews = true;                  // true = duplicate placed views at same positions; false = empty sheet
string viewDuplicateMode = "with_detailing"; // "with_detailing" | "plain" | "as_dependent"
// ---- END INPUTS ----

var newSheetIds = new List<ElementId>();
var sheets = elements.OfType<ViewSheet>().ToList();

if (sheets.Count == 0)
{
    sb.AppendLine("No ViewSheets in `elements` — feed this from filter-by-sheets.cs.");
}
else
{
    var dupOpt = viewDuplicateMode == "plain" ? ViewDuplicateOption.Duplicate
               : viewDuplicateMode == "as_dependent" ? ViewDuplicateOption.AsDependent
               : ViewDuplicateOption.WithDetailing;

    using (var t = new Transaction(Document, "AJ Tools - Duplicate Sheets"))
    {
        t.Start();
        try
        {
            int done = 0, failed = 0;
            foreach (var sheet in sheets)
            {
                // Declared OUTSIDE the try so the catch can clean it up. The sheet is created BEFORE its
                // number is assigned, and assigning a colliding number throws — which used to leave the
                // freshly-created sheet behind, committed, carrying Revit's own auto-number. Measured
                // live 2026-08-07: duplicating 'A-101' into an already-taken 'A-101-COPY' reported
                // "Duplicated 0 of 1 sheet(s), 1 failed" while the sheet count went 2 -> 3 and an orphan
                // 'A-103' stayed in the model. The header's promise that a collision "fails that one
                // sheet cleanly" was simply not true.
                ViewSheet newSheet = null;
                try
                {
                    // same title block type as the source sheet (first title block instance on it)
                    var tb = new FilteredElementCollector(Document, sheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks).OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>().FirstOrDefault();
                    newSheet = ViewSheet.Create(Document, tb != null ? tb.GetTypeId() : ElementId.InvalidElementId);
                    newSheet.SheetNumber = sheet.SheetNumber + numberSuffix; // throws on collision -> caught per sheet
                    newSheet.Name = namePrefix + sheet.Name;

                    int viewsCopied = 0, viewsSkipped = 0;
                    if (copyViews)
                    {
                        foreach (var vpId in sheet.GetAllViewports())
                        {
                            var vp = Document.GetElement(vpId) as Viewport;
                            if (vp == null) continue;
                            var srcView = Document.GetElement(vp.ViewId) as View;
                            if (srcView == null) continue;
                            try
                            {
                                if (!srcView.CanViewBeDuplicated(dupOpt)) { viewsSkipped++; continue; }
                                var newViewId = srcView.Duplicate(dupOpt);
                                Viewport.Create(Document, newSheet.Id, newViewId, vp.GetBoxCenter());
                                viewsCopied++;
                            }
                            catch { viewsSkipped++; }
                        }
                        // schedules sit on sheets as ScheduleSheetInstances, not Viewports — same schedule can repeat
                        foreach (var ssi in new FilteredElementCollector(Document, sheet.Id).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
                        {
                            try { ScheduleSheetInstance.Create(Document, newSheet.Id, ssi.ScheduleId, ssi.Point); viewsCopied++; }
                            catch { viewsSkipped++; }
                        }
                    }

                    newSheetIds.Add(newSheet.Id);
                    done++;
                    sb.AppendLine($"  - '{sheet.SheetNumber}' -> '{newSheet.SheetNumber}' (Id {newSheet.Id}){(copyViews ? $", {viewsCopied} view(s)/schedule(s) placed, {viewsSkipped} skipped" : ", empty")}");
                }
                catch (Exception exOne)
                {
                    failed++;
                    // Remove the half-built sheet, so "failed" really does mean nothing was left behind.
                    // Anything already placed on it (duplicated views, schedule instances) goes with it.
                    string cleanup = "";
                    if (newSheet != null)
                    {
                        try { Document.Delete(newSheet.Id); cleanup = " (the part-built sheet was removed)"; }
                        catch (Exception exDel) { cleanup = $" (WARNING: could not remove the part-built sheet Id {newSheet.Id} — {exDel.Message})"; }
                    }
                    sb.AppendLine($"  - '{sheet.SheetNumber}' FAILED: {exOne.Message}{cleanup}");
                }
            }
            t.Commit();
            sb.AppendLine($"Duplicated {done} of {sheets.Count} sheet(s), {failed} failed. Loose sheet annotations/guide grids/revisions are NOT copied (see header).");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED mid-duplication — rolled back, NO sheets were created. Reason: {ex.Message}");
            newSheetIds.Clear();
        }
    }
}
// ---- continue with another action fragment below (newSheetIds available), or add return sb.ToString(); to finish ----
