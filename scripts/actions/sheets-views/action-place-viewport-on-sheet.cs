// ============================================================
// FRAGMENT (action) — action-place-viewport-on-sheet.cs
// PURPOSE: Place each view in `elements` onto one target sheet as a Viewport, at a given point (or
//          centered on the sheet's title block if no point given).
// GOTCHA: a normal view (plan/section/elevation/3D) can only be placed on ONE sheet at a time — already-
//          placed views are skipped, never moved. Schedules/legends are different (see the two other
//          sheet-placement fragments) — they CAN appear on multiple sheets.
// ASSUMES: elements (List<Element>) are View objects (from a filter/creator), sb (StringBuilder) exists.
// NOT STANDALONE — see scripts/README.md for how to compose.
// FIXED 2026-08-07: the default-point branch used to compute the sheet CENTRE inside the per-element
//          loop, with no per-element offset — so placing N views put all N viewports on the exact same
//          point and still reported "Placed N". Measured live: 3 views -> 3 viewports all centred on
//          (422.5, 296.0) mm, one distinct position out of three. The sheet looked successfully
//          populated and was unusable. It now lays them out on a grid across the sheet and reports the
//          position of each. An explicit pointXmm/pointYmm still stacks them, because that is then the
//          caller's stated intent — but it says so.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetSheetNumberText = "A-101";
double? pointXmm = null, pointYmm = null; // null = auto-arrange across the sheet; set BOTH to stack every viewport on one point
// ---- END INPUTS ----

var targetSheet = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
    .FirstOrDefault(s => s.SheetNumber.Equals(targetSheetNumberText, StringComparison.OrdinalIgnoreCase));

int placed = 0, skipped = 0;

if (targetSheet == null)
{
    sb.AppendLine($"Target sheet '{targetSheetNumberText}' not found — nothing placed.");
}
else
{
    // Work out the layout BEFORE the loop. Only the views that will actually be placed get a slot, so
    // a skipped view doesn't leave a hole in the grid.
    var placeable = elements.Select(el => el as View)
        .Where(v => v != null && Viewport.CanAddViewToSheet(Document, targetSheet.Id, v.Id))
        .ToList();
    skipped = elements.Count - placeable.Count;

    int columns = (int)Math.Ceiling(Math.Sqrt(Math.Max(1, placeable.Count)));
    int rows = (int)Math.Ceiling(placeable.Count / (double)columns);
    var sheetOutline = targetSheet.Outline;
    double usableU = (sheetOutline.Max.U - sheetOutline.Min.U);
    double usableV = (sheetOutline.Max.V - sheetOutline.Min.V);

    using (var t = new Transaction(Document, "AJ Tools - Place Viewport On Sheet"))
    {
        t.Start();
        try
        {
            var positions = new List<string>();
            for (int i = 0; i < placeable.Count; i++)
            {
                var view = placeable[i];

                XYZ point;
                if (pointXmm.HasValue && pointYmm.HasValue)
                {
                    // Explicit point: every viewport lands here, stacked. That is the caller's choice.
                    point = new XYZ(
                        pointXmm.Value / 304.8,
                        pointYmm.Value / 304.8,
                        0);
                }
                else
                {
                    // Grid across the sheet — cell CENTRES, so a single viewport still lands mid-sheet
                    // exactly as before, and N viewports never share a point.
                    int col = i % columns;
                    int row = i / columns;
                    double u = sheetOutline.Min.U + usableU * (col + 0.5) / columns;
                    double v = sheetOutline.Max.V - usableV * (row + 0.5) / rows;
                    point = new XYZ(u, v, 0);
                }

                Viewport.Create(Document, targetSheet.Id, view.Id, point);
                placed++;
                positions.Add($"'{view.Name}' at ({point.X * 304.8:F0}, {point.Y * 304.8:F0})mm");
            }
            t.Commit();
            sb.AppendLine($"Placed {placed} viewport(s) on sheet '{targetSheet.SheetNumber} - {targetSheet.Name}', skipped {skipped} (already placed elsewhere, or not a placeable view type).");
            // Report WHERE, not just how many — a stacked pile and a laid-out sheet both used to print
            // the same line, which is how the stacking bug survived.
            if (placed > 0) sb.AppendLine("  " + string.Join("; ", positions));
            if (placed > 1 && pointXmm.HasValue && pointYmm.HasValue)
                sb.AppendLine($"  NOTE: an explicit point was given, so all {placed} viewports are stacked on it. Leave pointXmm/pointYmm null to arrange them across the sheet instead.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to place viewports — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
