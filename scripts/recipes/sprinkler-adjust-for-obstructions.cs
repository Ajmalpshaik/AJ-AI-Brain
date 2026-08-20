// ============================================================
// RECIPE — sprinkler-adjust-for-obstructions.cs
// PURPOSE: For each sprinkler head that fails an obstruction rule, find the SMALLEST move that clears it
//          without breaking anything else — still inside the room, still off the walls, still far enough
//          from its neighbours. Reports the proposed move for every head; only touches the model when you
//          explicitly turn the dry run off, and only for heads that already exist.
// STANDALONE — has its own sb and returns.
// SOURCE: ../../knowledge/fire-sprinkler/obstructions.md      (the rules being cleared)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md     (step 5, and why the re-check after is mandatory)
//
// *** THE WHOLE POINT, AND THE THING THAT MAKES THIS DANGEROUS: MOVING A HEAD CHANGES ITS SPACING. ***
//   A grid that satisfied every code limit stops satisfying them the moment one head shifts 400 mm to
//   clear a beam — head-to-head spacing grows on one side, the wall distance grows on the other, and the
//   area that head covers grows with both. This recipe therefore checks the CONSEQUENCES of each move as
//   well as the move itself, and refuses any candidate that breaks a limit you gave it. It still is not a
//   substitute for re-running the full check: after applying moves, run
//   recipes/sprinkler-compliance-audit.cs on the result. That is not belt and braces, it is the step that
//   catches what a per-head search cannot see.
//
// GOTCHA: **a clear move may not exist.** A head boxed in by a column on one side and the spacing cap on
//         the other has no legal position, and the honest output is "no move found", not a move that
//         breaks a different rule. Those heads are listed separately and they are a DESIGN decision:
//         change the grid, add a head, use a different head type, or accept and document it.
// GOTCHA: **moving is not always the right answer.** Rule 2 (an obstruction wider than 4 ft) is not fixed
//         by moving at all — it needs a head UNDERNEATH. Those are reported and skipped, never nudged
//         sideways to make a report look green.
// GOTCHA: the search is a plan search on a fixed grid of offsets. It will not find a solution that needs
//         the head to change height, and it does not re-order the branch runs. Both are real options a
//         person has and this fragment does not.
// GOTCHA: with dryRun = false and headSource = "existing" this MOVES REAL ELEMENTS. Confirm the count
//         with Ajmal first — START-HERE rule 5. Undo is Revit's own (commands/native-undo.cs).
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. ElementTransformUtils.MoveElement
//   is used in the same shape as actions/move-copy-rotate/action-move-to-ray-hit.cs, which IS live-proven
//   (17 air terminals, 0 misses, 2026-07-26). The search arithmetic is plain C#. Test with dryRun = true
//   on one room, read the proposed moves, then let it move ONE head and look at it on screen.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;
string headSource = "points";         // "points" = adjust the candidate list below (nothing in the model changes)
                                      // "existing" = adjust the Sprinklers already in this room
List<(double xMm, double yMm)> candidatePointsMm = new List<(double, double)> { };
double deflectorZmm = 0;              // REQUIRED — every vertical test measures from it

bool dryRun = true;                   // TRUE = report only. Set false ONLY for headSource "existing", and
                                      // only after Ajmal has seen the count and said go.

// --- the limits a move must not break (same numbers you gave recipes/sprinkler-nfpa-grid.cs)
double maxSpacingMm = 0;              // 0 = don't constrain (and the report will say so)
double minSpacingMm = 1829;
double maxWallDistanceMm = 0;
double minWallDistanceMm = 102;

// --- the obstruction rules (same as recipes/sprinkler-obstruction-check.cs — keep them identical)
double ignoreBelowDeflectorMm = 457;
double wideObstructionMm = 1219;
double threeTimesCapMm = 610;
double beamRuleMinClearMm = 305;
bool beamTableConfirmed = false;
double[] beamTableDistanceMm = new[] { 305.0, 457, 610, 762, 914, 1067, 1219, 1372, 1524, 1676, 1829 };
double[] beamTableAllowedMm  = new[] {  64.0,  89, 140, 191, 241,  305,  356,  419,  457,  508,  610 };

// --- the search
double searchStepMm = 50;             // how finely to try offsets
double maxMoveMm = 900;               // how far a head may be nudged before we call it hopeless
double searchAboveRoomMm = 6000;
int maxListed = 100;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0) { sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to adjust."); }
else if (deflectorZmm <= 0) { sb.AppendLine("NOT RUN — deflectorZmm is required; every vertical test measures from it."); }
else if (!dryRun && headSource != "existing")
{
    sb.AppendLine("NOT RUN — dryRun is false but headSource is \"points\", so there is nothing in the model to move.");
    sb.AppendLine("  Either set headSource = \"existing\", or leave dryRun = true and take the proposed centres away as a list.");
}
else
{
    var rbb = room.get_BoundingBox(null);
    double zProbe = room.Level.Elevation + mm(1000);
    double zRoomBase = room.Level.Elevation;
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    // ---- heads ----
    var heads = new List<XYZ>();
    var headElems = new List<Element>();
    var headLabels = new List<string>();
    if (headSource == "existing")
    {
        foreach (var h in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Sprinklers)
                     .WhereElementIsNotElementType().ToList())
        {
            var lp = h.Location as LocationPoint;
            if (lp == null || !insideRoom(lp.Point)) continue;
            heads.Add(lp.Point); headElems.Add(h); headLabels.Add($"Id {h.Id.IntegerValue}");
        }
    }
    else
    {
        int n = 0;
        foreach (var p in candidatePointsMm)
        { heads.Add(new XYZ(mm(p.xMm), mm(p.yMm), 0)); headElems.Add(null); headLabels.Add($"#{++n}"); }
    }

    sb.AppendLine($"ADJUST FOR OBSTRUCTIONS — '{room.Name}' (Id {roomIdInt}), {heads.Count} head(s), deflector {deflectorZmm:N0} mm");
    sb.AppendLine(dryRun ? "DRY RUN — nothing in the model will change." : "*** LIVE — heads WILL be moved. ***");
    if (!beamTableConfirmed) sb.AppendLine("*** The beam table is UNCONFIRMED — see recipes/sprinkler-obstruction-check.cs. ***");

    if (heads.Count == 0) { sb.AppendLine("No heads to adjust."); }
    else
    {
        // ---- obstructions, same gathering as the check recipe ----
        var outline = new Outline(
            new XYZ(rbb.Min.X, rbb.Min.Y, zRoomBase - mm(500)),
            new XYZ(rbb.Max.X, rbb.Max.Y, rbb.Max.Z + mm(searchAboveRoomMm)));
        var volumeFilter = new BoundingBoxIntersectsFilter(outline);

        Func<Element, List<XYZ>> curveSamples = el =>
        {
            var pts = new List<XYZ>();
            var lc = el.Location as LocationCurve;
            if (lc != null && lc.Curve != null)
                for (double t = 0; t <= 1.0001; t += 0.05)
                { try { pts.Add(lc.Curve.Evaluate(Math.Min(t, 1.0), true)); } catch { } }
            return pts;
        };
        Func<Element, bool> inThisRoom = el =>
        {
            var s = curveSamples(el);
            if (s.Count > 0) return s.Any(p => insideRoom(p));
            var b = el.get_BoundingBox(null);
            return b != null && insideRoom(new XYZ((b.Min.X + b.Max.X) / 2.0, (b.Min.Y + b.Max.Y) / 2.0, 0));
        };

        var obs = new List<Tuple<string, string, double, double, Func<XYZ, double>>>();  // kind,label,soffitZ,widthMm,dist
        Action<BuiltInCategory, string> gather = (cat, kind) =>
        {
            List<Element> items;
            try
            {
                items = new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType()
                    .WherePasses(volumeFilter).Where(inThisRoom).ToList();
            }
            catch { return; }
            foreach (var e in items)
            {
                var b = e.get_BoundingBox(null);
                if (b == null) continue;
                double wx = toMm(b.Max.X - b.Min.X), wy = toMm(b.Max.Y - b.Min.Y);
                var s = curveSamples(e);
                Func<XYZ, double> dist;
                if (s.Count > 1)
                {
                    double halfW = mm(Math.Min(wx, wy) / 2.0);
                    var flat = s.Select(p => new XYZ(p.X, p.Y, 0)).ToList();
                    dist = pt =>
                    {
                        double best = double.MaxValue;
                        foreach (var q in flat)
                        { double dx = pt.X - q.X, dy = pt.Y - q.Y; double d = Math.Sqrt(dx * dx + dy * dy); if (d < best) best = d; }
                        return Math.Max(0, best - halfW);
                    };
                }
                else
                {
                    double minX = b.Min.X, maxX = b.Max.X, minY = b.Min.Y, maxY = b.Max.Y;
                    dist = pt =>
                    {
                        double dx = Math.Max(Math.Max(minX - pt.X, pt.X - maxX), 0);
                        double dy = Math.Max(Math.Max(minY - pt.Y, pt.Y - maxY), 0);
                        return Math.Sqrt(dx * dx + dy * dy);
                    };
                }
                string nm = null; try { nm = e.Name; } catch { }
                obs.Add(Tuple.Create(kind, $"{(e.Category != null ? e.Category.Name : "?")} '{nm ?? "?"}' (Id {e.Id.IntegerValue})",
                    b.Min.Z, Math.Min(wx, wy), dist));
            }
        };
        gather(BuiltInCategory.OST_StructuralFraming, "continuous");
        gather(BuiltInCategory.OST_StructuralColumns, "vertical");
        gather(BuiltInCategory.OST_Columns, "vertical");
        gather(BuiltInCategory.OST_DuctCurves, "continuous");
        gather(BuiltInCategory.OST_FlexDuctCurves, "continuous");
        gather(BuiltInCategory.OST_CableTray, "continuous");
        gather(BuiltInCategory.OST_Conduit, "continuous");
        gather(BuiltInCategory.OST_PipeCurves, "continuous");
        gather(BuiltInCategory.OST_LightingFixtures, "isolated");

        // boundary curves for the wall checks
        var boundary = new List<Curve>();
        try
        {
            var opt = new Autodesk.Revit.DB.SpatialElementBoundaryOptions();
            foreach (var loop in room.GetBoundarySegments(opt)) foreach (var seg in loop) boundary.Add(seg.GetCurve());
        }
        catch { }

        Func<double, double> allowedAbove = distMm =>
        {
            if (distMm < beamRuleMinClearMm) return 0;
            double a = 0;
            for (int i = 0; i < beamTableDistanceMm.Length && i < beamTableAllowedMm.Length; i++)
                if (distMm >= beamTableDistanceMm[i]) a = beamTableAllowedMm[i];
            return a;
        };

        // does this position clear every obstruction? returns the first blocker, or null
        Func<XYZ, string> firstBlocker = pt =>
        {
            foreach (var o in obs)
            {
                string kind = o.Item1; double soffitMm = toMm(o.Item3), widthMm = o.Item4;
                double dMm = toMm(o.Item5(pt));
                double below = deflectorZmm - soffitMm;
                if (widthMm > wideObstructionMm) continue;                 // rule 2 — a move never fixes it
                if (below >= ignoreBelowDeflectorMm) continue;             // rule 1 — ignorable
                if (kind == "vertical")
                { if (dMm < 3.0 * widthMm) return $"{o.Item2} (needs {3.0 * widthMm:N0} mm, has {dMm:N0})"; }
                else if (kind == "isolated")
                { double need = Math.Min(3.0 * widthMm, threeTimesCapMm); if (dMm < need) return $"{o.Item2} (needs {need:N0} mm, has {dMm:N0})"; }
                else
                {
                    double above = deflectorZmm - soffitMm;
                    if (above <= 0) continue;
                    if (dMm < beamRuleMinClearMm) return $"{o.Item2} (only {dMm:N0} mm away, no allowance under {beamRuleMinClearMm:N0})";
                    if (above > allowedAbove(dMm)) return $"{o.Item2} (deflector {above:N0} mm above its bottom, table allows {allowedAbove(dMm):N0} at {dMm:N0} mm)";
                }
            }
            return null;
        };

        // would this position break a spacing/wall limit, given where all the OTHER heads are?
        Func<XYZ, int, string> breaksLimits = (pt, selfIndex) =>
        {
            if (!insideRoom(pt)) return "outside the room";
            for (int i = 0; i < heads.Count; i++)
            {
                if (i == selfIndex) continue;
                double dx = toMm(pt.X - heads[i].X), dy = toMm(pt.Y - heads[i].Y);
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (minSpacingMm > 0 && d < minSpacingMm) return $"only {d:N0} mm from head {headLabels[i]} (min {minSpacingMm:N0})";
            }
            if (maxSpacingMm > 0 && heads.Count > 1)
            {
                double nearest = double.MaxValue;
                for (int i = 0; i < heads.Count; i++)
                {
                    if (i == selfIndex) continue;
                    double dx = toMm(pt.X - heads[i].X), dy = toMm(pt.Y - heads[i].Y);
                    nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy));
                }
                if (nearest > maxSpacingMm) return $"nearest neighbour would be {nearest:N0} mm away (max {maxSpacingMm:N0})";
            }
            foreach (var crv in boundary)
            {
                double zc = crv.GetEndPoint(0).Z;
                double d = toMm(crv.Distance(new XYZ(pt.X, pt.Y, zc)));
                if (d < minWallDistanceMm) return $"{d:N0} mm from a wall (min {minWallDistanceMm:N0})";
            }
            if (maxWallDistanceMm > 0 && boundary.Count > 0)
            {
                // only meaningful for the walls this head is the nearest to — checked properly by the audit,
                // so here we only reject a move that walks a head AWAY past the cap from its own nearest wall
                double nearestWall = boundary.Min(crv => toMm(crv.Distance(new XYZ(pt.X, pt.Y, crv.GetEndPoint(0).Z))));
                double wasNearest = boundary.Min(crv => toMm(crv.Distance(new XYZ(heads[selfIndex].X, heads[selfIndex].Y, crv.GetEndPoint(0).Z))));
                if (nearestWall > maxWallDistanceMm && nearestWall > wasNearest)
                    return $"would sit {nearestWall:N0} mm from its nearest wall (max {maxWallDistanceMm:N0})";
            }
            return null;
        };

        // ---- search a move for each failing head ----
        var moves = new List<Tuple<int, XYZ, double, string>>();   // index, newPt, distanceMm, blockerCleared
        var stuck = new List<string>();
        var needUnder = new List<string>();
        int alreadyClear = 0, listed = 0;

        for (int h = 0; h < heads.Count; h++)
        {
            // rule 2 first — a move is the wrong tool
            foreach (var o in obs)
                if (o.Item4 > wideObstructionMm && toMm(o.Item5(heads[h])) < 1000)
                    needUnder.Add($"head {headLabels[h]} sits under {o.Item2} ({o.Item4:N0} mm across) — needs a head UNDERNEATH it, not a nudge");

            var blocker = firstBlocker(heads[h]);
            if (blocker == null) { alreadyClear++; continue; }

            XYZ found = null; double foundDist = 0;
            double stepFt = mm(searchStepMm), maxFt = mm(maxMoveMm);
            for (double r = stepFt; r <= maxFt + 1e-9 && found == null; r += stepFt)
            {
                int steps = Math.Max(8, (int)Math.Ceiling(2 * Math.PI * r / stepFt));
                for (int a = 0; a < steps; a++)
                {
                    double ang = 2 * Math.PI * a / steps;
                    var cand = new XYZ(heads[h].X + r * Math.Cos(ang), heads[h].Y + r * Math.Sin(ang), heads[h].Z);
                    if (firstBlocker(cand) != null) continue;
                    if (breaksLimits(cand, h) != null) continue;
                    found = cand; foundDist = toMm(r); break;
                }
            }

            if (found == null)
                stuck.Add($"head {headLabels[h]} — blocked by {blocker}, and no position within {maxMoveMm:N0} mm clears it without breaking another limit");
            else
                moves.Add(Tuple.Create(h, found, foundDist, blocker));
        }

        sb.AppendLine();
        sb.AppendLine($"  already clear      {alreadyClear,4:N0}");
        sb.AppendLine($"  move proposed      {moves.Count,4:N0}");
        sb.AppendLine($"  no legal move      {stuck.Count,4:N0}");
        sb.AppendLine($"  need head beneath  {needUnder.Count,4:N0}");
        sb.AppendLine();

        foreach (var m in moves)
        {
            if (listed++ >= maxListed) break;
            sb.AppendLine($"  MOVE {headLabels[m.Item1]}: {toMm(heads[m.Item1].X):N0},{toMm(heads[m.Item1].Y):N0}"
                + $"  ->  {toMm(m.Item2.X):N0},{toMm(m.Item2.Y):N0} mm  ({m.Item3:N0} mm)");
            sb.AppendLine($"        clears: {m.Item4}");
        }
        foreach (var s in stuck.Take(maxListed)) sb.AppendLine($"  STUCK  {s}");
        foreach (var s in needUnder.Distinct().Take(maxListed)) sb.AppendLine($"  UNDER  {s}");

        if (stuck.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  A head with no legal move is a DESIGN decision, not a bug: change the grid, add a head,");
            sb.AppendLine("  change the head type, or accept and document it. Do not widen a limit to make this list empty.");
        }

        // ---- apply, if asked ----
        if (!dryRun && moves.Count > 0)
        {
            using (var t = new Transaction(Document, "AJ Tools - Adjust Sprinklers for Obstructions"))
            {
                t.Start();
                try
                {
                    int moved = 0;
                    foreach (var m in moves)
                    {
                        var el = headElems[m.Item1];
                        if (el == null) continue;
                        var delta = new XYZ(m.Item2.X - heads[m.Item1].X, m.Item2.Y - heads[m.Item1].Y, 0);
                        ElementTransformUtils.MoveElement(Document, el.Id, delta);
                        moved++;
                    }
                    t.Commit();
                    sb.AppendLine();
                    sb.AppendLine($"  MOVED {moved:N0} head(s).");
                    sb.AppendLine("  NOW RE-CHECK: run recipes/sprinkler-compliance-audit.cs on this room, in a SEPARATE call.");
                    sb.AppendLine("  This report is not evidence the model changed — read it back out of the model.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED (move) — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
        else if (dryRun && moves.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  DRY RUN — nothing moved. Show Ajmal the count above and get a clear go-ahead before setting dryRun = false.");
        }
    }
}

return sb.ToString();
