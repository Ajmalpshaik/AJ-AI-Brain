// ============================================================
// FRAGMENT (action) — action-maximize-datum-extents.cs
// PURPOSE: Make the GRIDS and LEVELS in `elements` span the WHOLE model — "the grids only go half way",
//          "maximize the grids to the entire model", "some grids are short and they all end in a
//          different place". Lengthens the datum's own 3D extent, so every view using Model extent gets
//          the new length. Leaves every grid of the same direction ending on exactly the same line.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
//          Anything in `elements` that is not a Grid or a Level is ignored and counted.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✓ LIVE-VERIFIED 2026-08-22 on `school.rvt`, Revit 2020.2.9, on all 6 grids. Before: four Y-grids at
//   25096–26514 mm all ending at different places, two X-grids at 44257 / 46112 mm. After: every Y-grid
//   exactly 40995 mm (Y -18303..22693) and every X-grid exactly 47280 mm (X -23034..24246) — measured
//   ragged-by 0 mm at both ends in both directions. Confirmed by eye on an exported PNG of the plan.
//
// ⚠⚠ THE TRAP THIS FRAGMENT EXISTS TO AVOID — do NOT compute the model extent yourself and push a
//    longer curve in with SetCurveInView. Two separate things go wrong, and the first one is silent:
//
//    (1) SetCurveInView SIMPLY DOES NOT WORK ON GRIDS HERE. It throws "The curve is unbound or not
//        coincident with the original one of the datum plane" — and it throws that even when you hand
//        the grid back its OWN curve, unmodified, straight from `grid.Curve`. That was tested directly:
//        Model extent, ViewSpecific extent and the untouched-own-curve case all three failed on the
//        same grid in the same transaction. So a "not coincident" error is NOT telling you your maths
//        is off — checking your geometry harder is wasted time. `Maximize3DExtents()` is the method
//        that works, and it is what Revit's own "maximize 3D extents" runs.
//
//    (2) MEASURING THE MODEL YOURSELF GIVES A POISONED NUMBER. A naive bounding box over all model
//        elements returns ±30480 mm on this model — exactly ±100 ft. That is SHEETS: every sheet
//        carries a nominal ±100 ft box in model space. Cameras also sit outside the building. Extending
//        grids to that box makes them 61 m long on a 41 m building. If you ever DO need the real extent
//        (for a scope box, a section, a crop), measure only physical categories — Walls, Floors, Doors,
//        Windows, Rooms, Ceilings, Roofs, Columns, Framing, Stairs — and never trust a whole-model box.
//
// GOTCHA: this changes the datum's SHARED 3D extent, so it lands in every view set to Model extent —
//         which is what "maximize to the model" means. A view where an end was dragged short is on
//         ViewSpecific extent and will NOT show the new length. Run
//         actions/structural-changes/action-reset-datum-extents.cs on those views first, or the fix
//         looks like it did nothing there. This fragment reports how many such crops it found.
// GOTCHA: Revit's idea of "the model" for this purpose INCLUDES the datums themselves. On the verified
//         run the X-grids finished at X -23034..24246 — wider than the building's -22166..19534 —
//         because two grids already reached further out than any wall. So the result is the union of
//         building and existing datums, never shorter than what you started with.
// GOTCHA: there is no margin control. Maximize3DExtents lands flush on the model extent, so a grid can
//         finish exactly level with the slab edge with no overhang (it did on the verified run, at the
//         bottom). If drafting wants the lines projecting past the building, that is a separate move —
//         and note SetCurveInView is not available to do it, see the trap above.
// GOTCHA: Levels behave the same way but are maximized in section/elevation, not plan. Passing levels
//         in plan is harmless — they are counted as done, and you check them in a section.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = report the lengths that WOULD change, touch nothing
bool reportPerViewCrops = true;  // scan every plan/3D/section view for ViewSpecific ends that would hide the result
// ---- END INPUTS ----

const double MM_PER_FT = 304.8;

var datums = elements.OfType<DatumPlane>().Where(d => d is Grid || d is Level).ToList();
int ignored = elements.Count - datums.Count;

// DatumPlane itself has NO Curve property on any Revit version - only Grid does, and a Level is a
// horizontal plane with no curve at all. Reading curveOf(d) off a DatumPlane is a COMPILE error, which is
// why this fragment could not build on 2020, 2024 or 2027 until 2026-08-23 despite its verified note
// (the note is real: it was proved while this list was typed as Grid, then widened to DatumPlane to
// take Levels, and the reporting helpers below were never re-checked). Maximize3DExtents - the actual
// work - IS on DatumPlane and is untouched; only the length/span REPORTING needed the cast.
Func<DatumPlane, Curve> curveOf = d => (d as Grid)?.Curve;

Func<DatumPlane, double> lengthMm = d =>
{ try { return curveOf(d) != null && curveOf(d).IsBound ? curveOf(d).Length * MM_PER_FT : double.NaN; } catch { return double.NaN; } };
Func<DatumPlane, string> spanMm = d =>
{
    try
    {
        var c = curveOf(d); if (c == null || !c.IsBound) return "(unbounded)";
        var a = c.GetEndPoint(0); var b = c.GetEndPoint(1);
        bool alongX = Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y);
        return alongX
            ? $"X {Math.Min(a.X, b.X) * MM_PER_FT,9:F0} .. {Math.Max(a.X, b.X) * MM_PER_FT,9:F0}"
            : $"Y {Math.Min(a.Y, b.Y) * MM_PER_FT,9:F0} .. {Math.Max(a.Y, b.Y) * MM_PER_FT,9:F0}";
    }
    catch { return "(unreadable)"; }
};

var was = datums.ToDictionary(d => d.Id, d => lengthMm(d));

sb.AppendLine(dryRun
    ? $"DRY RUN — {datums.Count} datum(s) would be maximized to the model extent. Nothing changed."
    : $"Maximizing {datums.Count} datum(s) to the model extent.");
if (ignored > 0) sb.AppendLine($"  ({ignored} element(s) in `elements` were not a Grid or Level — ignored.)");

int done = 0, failed = 0;
if (!dryRun)
{
    using (var tMax = new Transaction(Document, "AJ Tools - Maximize datum extents"))
    {
        tMax.Start();
        foreach (var d in datums)
        {
            try { d.Maximize3DExtents(); done++; }
            catch (Exception ex)
            { sb.AppendLine($"  {d.Name}: FAILED — {ex.Message.Split('\n')[0]}"); failed++; }
        }
        tMax.Commit();
    }
}

sb.AppendLine();
sb.AppendLine($"{"Datum",-10}{"length before",16}{(dryRun ? "" : "   length after      change")}   extent now");
foreach (var d in datums.OrderBy(x => x.Name))
{
    double b4 = was[d.Id], now = lengthMm(d);
    sb.AppendLine(dryRun
        ? $"{d.Name,-10}{b4,13:F0} mm                                {spanMm(d)}"
        : $"{d.Name,-10}{b4,13:F0} mm{now,15:F0} mm{now - b4,+11:F0}   {spanMm(d)}");
}

// Are all datums of a direction now ending on the same line? That is the visible test of "maximized".
if (!dryRun && datums.Count > 1)
{
    sb.AppendLine();
    sb.AppendLine("ALIGNMENT CHECK — every datum of one direction should now share both ends:");
    foreach (var grp in datums.Where(d => { try { return curveOf(d) != null && curveOf(d).IsBound; } catch { return false; } })
        .GroupBy(d => { var a = curveOf(d).GetEndPoint(0); var b = curveOf(d).GetEndPoint(1);
                        return Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y); }))
    {
        var ends = grp.Select(d => { var a = curveOf(d).GetEndPoint(0); var b = curveOf(d).GetEndPoint(1);
            return grp.Key ? new[] { Math.Min(a.X, b.X) * MM_PER_FT, Math.Max(a.X, b.X) * MM_PER_FT }
                           : new[] { Math.Min(a.Y, b.Y) * MM_PER_FT, Math.Max(a.Y, b.Y) * MM_PER_FT }; }).ToList();
        double sLo = ends.Max(e => e[0]) - ends.Min(e => e[0]);
        double sHi = ends.Max(e => e[1]) - ends.Min(e => e[1]);
        sb.AppendLine($"  {(grp.Key ? "running along X" : "running along Y"),-18} ({grp.Count()}): ragged by " +
                      $"{sLo:F0} mm / {sHi:F0} mm" + (Math.Max(sLo, sHi) < 1 ? "   -> ALIGNED" : "   <- still ragged"));
    }
}

// A per-view crop silently hides all of the above in that view — say so rather than let it look broken.
if (reportPerViewCrops)
{
    var crops = new List<string>();
    var scanViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => !v.IsTemplate && (v is ViewPlan || v is View3D || v is ViewSection)).ToList();
    foreach (var d in datums)
        foreach (var v in scanViews)
            foreach (DatumEnds end in new[] { DatumEnds.End0, DatumEnds.End1 })
            { try { if (d.GetDatumExtentTypeInView(end, v) == DatumExtentType.ViewSpecific)
                        crops.Add($"{d.Name}/{v.Name}/{end}"); } catch { } }
    sb.AppendLine();
    sb.AppendLine(crops.Count == 0
        ? $"No per-view crop found across {scanViews.Count} view(s) — the new extent shows everywhere."
        : $"WARNING — {crops.Count} per-view crop(s) will still show the OLD short length. Run " +
          $"action-reset-datum-extents.cs on those views: " + string.Join("; ", crops.Take(15)) +
          (crops.Count > 15 ? $" ...and {crops.Count - 15} more" : ""));
}

if (!dryRun) sb.AppendLine($"\n{done} maximized, {failed} failed.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
