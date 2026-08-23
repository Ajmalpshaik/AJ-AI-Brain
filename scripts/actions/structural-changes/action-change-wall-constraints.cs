// ============================================================
// FRAGMENT (action) — action-change-wall-constraints.cs
// PURPOSE: Move the walls in `elements` onto a DIFFERENT base and/or top Level — WITHOUT the walls
//          themselves moving or changing height. The "we renamed/reorganised the levels and now every
//          wall is attached to the wrong one" repair, and the "re-host this block onto Level 3" job.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — Walls, e.g. from
//          filter-by-category.cs on OST_Walls.
//
// ✱✱ THE WHOLE POINT, AND THE REASON THIS IS NOT A ONE-LINER: a wall's position is (level elevation +
//    offset). Set the level alone and the OFFSET STAYS THE SAME, so the wall jumps by the difference
//    between the two levels — silently, on every wall at once. This fragment measures each wall's REAL
//    world elevation first, sets the new level, then re-solves the offset so the wall ends up exactly
//    where it started. Same trick top and bottom. Nothing in the model appears to move; only what the
//    wall is attached to changes.
//
// GOTCHA: A WALL WHOSE TOP IS "UNCONNECTED" HAS NO TOP LEVEL. Its height is a plain number
//         (WALL_USER_HEIGHT_PARAM) and WALL_HEIGHT_TYPE reads as invalid. Asking to change its top level
//         converts it from unconnected to level-bound, which is a real design change — so those walls are
//         reported and skipped unless allowUnconnectedToBecomeBound is set.
// GOTCHA: IN A WORKSHARED MODEL a wall owned by someone else cannot be changed. Those are named in the
//         report and skipped rather than throwing halfway through the batch.
// GOTCHA: DRY RUN BY DEFAULT — it prints the before/after elevation of every wall so you can see that
//         nothing is going to move before you commit.
// RELATED: creators/create-wall.cs makes them; action-disallow-join.cs handles the ends.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE wall and check in a section
//   that the wall has not moved, then use it for the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool changeBase = true;              // re-host the bottom of the wall
string newBaseLevelName = "";        // exact Level name for the new base (required if changeBase)
bool changeTop = false;              // re-host the top of the wall
string newTopLevelName = "";         // exact Level name for the new top (required if changeTop)
bool allowUnconnectedToBecomeBound = false; // let an unconnected-height wall be bound to a top level
bool dryRun = true;                  // true = report only, change nothing
// ---- END INPUTS ----

Func<double, double> ft2mm = v => v * 304.8;
var walls = elements.OfType<Wall>().ToList();
int changed = 0, skippedOwned = 0, skippedUnconnected = 0, failed = 0;
var notes = new List<string>();

var allLevels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().ToList();
var newBase = string.IsNullOrWhiteSpace(newBaseLevelName) ? null : allLevels.FirstOrDefault(l => l.Name == newBaseLevelName);
var newTop  = string.IsNullOrWhiteSpace(newTopLevelName)  ? null : allLevels.FirstOrDefault(l => l.Name == newTopLevelName);

if (walls.Count == 0)
{
    sb.AppendLine("No Walls in `elements` — put filter-by-category.cs on OST_Walls above this.");
}
else if (!changeBase && !changeTop)
{
    sb.AppendLine("Neither changeBase nor changeTop is set — nothing to do.");
}
else if (changeBase && newBase == null)
{
    sb.AppendLine($"Base level '{newBaseLevelName}' not found. Levels here: {string.Join(", ", allLevels.Select(l => l.Name))}");
}
else if (changeTop && newTop == null)
{
    sb.AppendLine($"Top level '{newTopLevelName}' not found. Levels here: {string.Join(", ", allLevels.Select(l => l.Name))}");
}
else
{
    sb.AppendLine($"{walls.Count} wall(s) in.");
    if (changeBase) sb.AppendLine($"  base -> '{newBase.Name}' (elevation {ft2mm(newBase.Elevation):F0} mm)");
    if (changeTop)  sb.AppendLine($"  top  -> '{newTop.Name}' (elevation {ft2mm(newTop.Elevation):F0} mm)");
    sb.AppendLine();

    using (var t = new Transaction(Document, "AJ Tools - Change wall constraints"))
    {
        if (!dryRun) t.Start();

        foreach (var wall in walls)
        {
            string label = $"Wall {wall.Id.IntegerValue}";

            // Worksharing: someone else's wall cannot be touched.
            try
            {
                var status = WorksharingUtils.GetCheckoutStatus(Document, wall.Id);
                if (status == CheckoutStatus.OwnedByOtherUser)
                {
                    skippedOwned++;
                    notes.Add($"  {label}: owned by another user — skipped.");
                    continue;
                }
            }
            catch { /* not a workshared model — carry on */ }

            var pBaseLevel  = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            var pBaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);
            var pTopLevel   = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            var pTopOffset  = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
            var pHeight     = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);

            if (pBaseLevel == null || pBaseOffset == null)
            {
                failed++; notes.Add($"  {label}: no base constraint parameters — skipped.");
                continue;
            }

            var oldBaseLevel = Document.GetElement(pBaseLevel.AsElementId()) as Level;
            if (oldBaseLevel == null)
            {
                failed++; notes.Add($"  {label}: base level could not be read — skipped.");
                continue;
            }

            // Measure where the wall REALLY is, before anything changes.
            double baseElev = oldBaseLevel.Elevation + pBaseOffset.AsDouble();
            double height   = pHeight != null ? pHeight.AsDouble() : 0;
            bool topIsUnconnected = pTopLevel == null || pTopLevel.AsElementId() == ElementId.InvalidElementId;
            double topElev;
            if (topIsUnconnected)
            {
                topElev = baseElev + height;
            }
            else
            {
                var oldTopLevel = Document.GetElement(pTopLevel.AsElementId()) as Level;
                topElev = oldTopLevel == null ? baseElev + height
                        : oldTopLevel.Elevation + (pTopOffset != null ? pTopOffset.AsDouble() : 0);
            }

            if (changeTop && topIsUnconnected && !allowUnconnectedToBecomeBound)
            {
                skippedUnconnected++;
                notes.Add($"  {label}: top is Unconnected — binding it to a level is a design change. "
                        + "Set allowUnconnectedToBecomeBound = true if that is what you want.");
                if (!changeBase) continue;
            }

            if (dryRun)
            {
                var parts = new List<string>();
                if (changeBase)
                    parts.Add($"base '{oldBaseLevel.Name}' {ft2mm(pBaseOffset.AsDouble()):F0}mm -> "
                            + $"'{newBase.Name}' {ft2mm(baseElev - newBase.Elevation):F0}mm");
                if (changeTop && (!topIsUnconnected || allowUnconnectedToBecomeBound))
                    parts.Add($"top -> '{newTop.Name}' {ft2mm(topElev - newTop.Elevation):F0}mm");
                sb.AppendLine($"  {label}: {string.Join("; ", parts)}  "
                            + $"[stays at {ft2mm(baseElev):F0}..{ft2mm(topElev):F0} mm]");
                changed++;
                continue;
            }

            try
            {
                if (changeBase)
                {
                    pBaseLevel.Set(newBase.Id);
                    pBaseOffset.Set(baseElev - newBase.Elevation);   // re-solve so the wall does not move
                }
                if (changeTop && (!topIsUnconnected || allowUnconnectedToBecomeBound))
                {
                    pTopLevel.Set(newTop.Id);
                    if (pTopOffset != null && !pTopOffset.IsReadOnly)
                        pTopOffset.Set(topElev - newTop.Elevation);  // same, at the top
                }
                changed++;
                sb.AppendLine($"  {label}: re-hosted, still at {ft2mm(baseElev):F0}..{ft2mm(topElev):F0} mm");
            }
            catch (Exception ex)
            {
                failed++;
                notes.Add($"  {label}: FAILED — {ex.Message}");
            }
        }

        if (!dryRun) t.Commit();
    }

    sb.AppendLine();
    sb.AppendLine(dryRun
        ? $"DRY RUN — nothing changed. {changed} wall(s) would be re-hosted. Set dryRun = false to apply."
        : $"Re-hosted {changed} wall(s). None of them moved.");
    if (skippedOwned > 0)       sb.AppendLine($"Skipped (owned by another user): {skippedOwned}.");
    if (skippedUnconnected > 0) sb.AppendLine($"Skipped (top is Unconnected): {skippedUnconnected}.");
    if (failed > 0)             sb.AppendLine($"Failed: {failed}.");
    foreach (var n in notes) sb.AppendLine(n);
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
