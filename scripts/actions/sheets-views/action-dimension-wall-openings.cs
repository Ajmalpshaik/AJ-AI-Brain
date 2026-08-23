// ============================================================
// FRAGMENT (action) — action-dimension-wall-openings.cs
// PURPOSE: Dimension ALONG each wall in `elements`, picking up every DOOR and WINDOW opening in it —
//          a running string (wall end -> jamb -> jamb -> wall end) plus a second OVERALL dimension
//          outside it. "dimension the wall with the door in it", "put the door positions on the plan",
//          "and give the overall as well". Real Dimension elements, not text.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter fragment above.
//          Takes WALLS — or pass ROOMS and it resolves each room's bounding walls itself, which is
//          the usual way to ask ("dimension room 4's walls"). Anything else is ignored and counted.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// Ajmal, 2026-08-23: *"if you are adding dimention one wall is there and door or sonme ting is that
// need to include that also and if you include for that wall one over all dimention also need to
// give."* That is the two-tier dimension string every drawing uses, and nothing in this library did it —
// action-dimension-rooms.cs measures a room's overall size and cannot see an opening at all.
//
// ✱✱ THE FACT THAT MAKES THIS WORK, AND IT IS NOT THE OBVIOUS ROUTE: **take the jambs off the WALL, not
//    off the DOOR.** A door exposes `FamilyInstanceReferenceType.Left` / `.Right` / `.CenterLeftRight`
//    and it is tempting to dimension to those. Measured on this model: they exist (1 each), but
//    `GetGeometryObjectFromReference` hands back a bare `GeometryObject`, **not a PlanarFace** — they are
//    reference PLANES, so there is no `.Origin` to read and no way to work out where along the wall each
//    one sits. Without a position you cannot ORDER them, and an unordered ReferenceArray gives a
//    scrambled dimension string.
//    The wall's own geometry already carries the opening: an opening CUTS the wall, leaving planar faces
//    whose normal runs parallel to the wall. Measured on wall 924021 with one 762 mm door in it, the
//    faces parallel to the run came out at **-100, 7738, 8500, 9000 mm** — the two ends and the two
//    jambs, and 8500 - 7738 = **762**, the door width exactly. One geometry walk gives ends AND jambs,
//    all as real references on ONE element, each with a position you can sort by.
//
// GOTCHA: `Dimension.Value` IS NULL ON A MULTI-SEGMENT DIMENSION. It is not an error and it is not a
//         failure to create — a running string reports through `NumberOfSegments` and `Segments`, each
//         segment carrying its own nullable `Value`. Read the overall off the separate overall dimension,
//         never off the running one. (`Dimension.Curve` throws outright — see knowledge/live-model/dimensioning.md.)
// GOTCHA: THE SUM OF THE SEGMENTS IS THE ONLY CHECK THAT CATCHES A MISSED OR SPURIOUS FACE, so it is
//         built in: the run adds the segments up and compares them to the overall dimension, printing
//         MATCH or OFF BY. A join notch or a second solid can put an extra face parallel to the run, and
//         an extra face is an extra segment that still looks like a valid dimension. Read the segment
//         list, not just the count.
// GOTCHA: the dimension measures the wall's SOLID extent, not its location line. A wall joined to
//         another is extended by the join — this one's solid ran -100 to 9000 while its location curve
//         ran 0 to 9100. Both happened to total 9100 here because the joins were symmetric; do not rely
//         on that. The solid is the right thing to dimension: it is what is drawn.
// GOTCHA: CURVED WALLS ARE SKIPPED AND SAID SO. "Distance along the wall" is a projection onto a
//         straight direction vector, which is meaningless on an arc — the faces would sort into the
//         wrong order and produce a plausible, wrong string rather than an error.
// GOTCHA: `offsetSide` follows THE DIRECTION THE WALL WAS DRAWN, not the plan. "left" is left of the
//         way the wall runs, so two walls drawn in opposite directions take their dimensions to
//         opposite sides of the same room. Use "auto" whenever rooms were passed in — it pushes the
//         string away from that room's centre, which is what you actually mean. Same family of trap as
//         DatumEnds.End0 and ShellLayerType.Interior — see knowledge/live-model/datums.md.
// GOTCHA: a wall with NO opening produces a two-reference string that IS the overall, so only one
//         dimension is made rather than two identical ones stacked on top of each other.
// NOTE: an opening that does not cut the wall (a face-hosted item, a sticker) leaves no face and so
//       does not appear. That is correct — it has no jamb to dimension.
//
// ⚠ NOT YET RUN AS A WHOLE FILE — written 2026-08-23. **Every mechanism in it was measured live first**
//   on `school.rvt` (Revit 2020.2.9) inside a rolled-back transaction: the face walk returned
//   -100/7738/8500/9000, the chained `NewDimension` returned NumberOfSegments 3 with segments
//   7838 + 762 + 500, the overall returned 9100, and the two agreed exactly. Run it on ONE wall, read
//   the segment list, then use it for a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = report the string it WOULD draw, create nothing
double offsetMm = 500;           // ASK. gap from the wall FACE to the running string. Scale it with the
                                 // view: 1000 at 1:100, 500 at 1:50 — same distance on paper
double overallGapMm = 400;       // how much further out again the OVERALL string sits
bool doOverall = true;           // false = running string only
string offsetSide = "auto";      // "auto" (away from the room — needs rooms passed in) | "left" | "right",
                                 // left/right being relative to the way the wall was DRAWN
int? targetViewIdInt = null;     // null = active view. Must be a plan view
// ---- END INPUTS ----

const double MM_PER_FT = 304.8;
double offFt = offsetMm / MM_PER_FT;
double gapFt = overallGapMm / MM_PER_FT;
string sideKey = (offsetSide ?? "auto").Trim().ToLowerInvariant();
bool sideKnown = sideKey == "auto" || sideKey == "left" || sideKey == "right";

View dimView = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (dimView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view. Nothing done.");
}
else if (!sideKnown)
{
    sb.AppendLine($"offsetSide = '{offsetSide}' is not one of: auto / left / right. Nothing done.");
}
else
{
    var planView = dimView as ViewPlan;
    double planZ = (planView != null && planView.GenLevel != null) ? planView.GenLevel.Elevation : 0;
    if (planView == null)
        sb.AppendLine($"WARNING — '{dimView.Name}' is a {dimView.ViewType}, not a plan. Dimensions may refuse to place.");

    // ---- resolve the input set to walls, remembering the room each came from so "auto" has a side ----
    var targets = new List<Tuple<Wall, XYZ>>();      // wall, room centre (null when a wall was passed directly)
    int ignored = 0, fromRooms = 0;
    foreach (var el in elements)
    {
        var w = el as Wall;
        if (w != null)
        {
            if (!targets.Any(t => t.Item1.Id == w.Id)) targets.Add(Tuple.Create(w, (XYZ)null));
            continue;
        }
        var room = el as Autodesk.Revit.DB.Architecture.Room;
        if (room == null) { ignored++; continue; }

        var bopts = new SpatialElementBoundaryOptions
        { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        IList<IList<BoundarySegment>> loops = null;
        try { loops = room.GetBoundarySegments(bopts); } catch { }
        if (loops == null || loops.Count == 0)
        { sb.AppendLine($"  {room.Name}: not enclosed — no boundary to read walls from, skipped."); continue; }

        var corners = loops[0].Select(s => s.GetCurve().GetEndPoint(0)).ToList();
        var ctr = new XYZ(corners.Average(p => p.X), corners.Average(p => p.Y), planZ);
        foreach (var s in loops[0])
        {
            var bw = Document.GetElement(s.ElementId) as Wall;      // null for a room separation line
            if (bw != null && !targets.Any(t => t.Item1.Id == bw.Id))
            { targets.Add(Tuple.Create(bw, ctr)); fromRooms++; }
        }
    }

    var report = new List<string>();
    int madeRun = 0, madeOverall = 0, failed = 0, skipped = 0;

    Action buildAll = () =>
    {
        foreach (var pair in targets)
        {
            Wall wall = pair.Item1; XYZ roomCtr = pair.Item2;
            string name = "Wall " + wall.Id;  // not .IntegerValue - removed in Revit 2027

            var lc = wall.Location as LocationCurve;
            var runLine = lc != null ? lc.Curve as Line : null;
            if (runLine == null)
            { report.Add($"{name} | — | curved or has no location line — skipped, see the header"); skipped++; continue; }

            XYZ a = runLine.GetEndPoint(0), b = runLine.GetEndPoint(1);
            XYZ dir = (b - a).Normalize();
            XYZ perp = XYZ.BasisZ.CrossProduct(dir);                 // 90 degrees LEFT of the run direction

            // "auto": push away from the room's centre. Falls back to left when no room came in.
            double sign = sideKey == "right" ? -1 : 1;
            if (sideKey == "auto")
                sign = (roomCtr != null && (a + perp - roomCtr).GetLength() < (a - perp - roomCtr).GetLength()) ? -1 : 1;

            // ---- every planar face whose normal runs ALONG the wall: its two ends and every jamb ----
            var cuts = new List<Tuple<double, Reference>>();
            try
            {
                foreach (var g in wall.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine }))
                {
                    var sol = g as Solid; if (sol == null || sol.Faces.Size == 0) continue;
                    foreach (Face f in sol.Faces)
                    {
                        var pf = f as PlanarFace; if (pf == null || f.Reference == null) continue;
                        if (Math.Abs(pf.FaceNormal.DotProduct(dir)) > 0.99)
                            cuts.Add(Tuple.Create((pf.Origin - a).DotProduct(dir), f.Reference));
                    }
                }
            }
            catch (Exception ex)
            { report.Add($"{name} | — | could not read geometry: {ex.Message.Split('\n')[0]}"); failed++; continue; }

            // Two solids can each carry a face at the same station — one dimension reference per station.
            cuts = cuts.GroupBy(t => Math.Round(t.Item1, 4)).Select(gr => gr.First()).OrderBy(t => t.Item1).ToList();

            if (cuts.Count < 2)
            { report.Add($"{name} | — | only {cuts.Count} usable face across the run — skipped"); skipped++; continue; }

            int openings = (cuts.Count - 2) / 2;
            double overallExpect = (cuts.Last().Item1 - cuts.First().Item1) * MM_PER_FT;
            string segList = string.Join(" + ", Enumerable.Range(0, cuts.Count - 1)
                .Select(i => ((cuts[i + 1].Item1 - cuts[i].Item1) * MM_PER_FT).ToString("F0")));

            if (dryRun)
            {
                report.Add($"{name} | {openings} opening(s) | would draw {segList} = {overallExpect:F0} mm");
                continue;
            }

            double dRun = wall.Width / 2 + offFt;
            var pRunA = a + perp.Multiply(sign * dRun); var pRunB = b + perp.Multiply(sign * dRun);
            var runDimLine = Line.CreateBound(new XYZ(pRunA.X, pRunA.Y, planZ), new XYZ(pRunB.X, pRunB.Y, planZ));

            double sumMm = 0; bool runOk = false;
            // A wall with no opening has exactly two faces, so the running string IS the overall —
            // make one dimension, not two identical ones stacked.
            if (cuts.Count > 2)
            {
                try
                {
                    var ra = new ReferenceArray(); foreach (var c in cuts) ra.Append(c.Item2);
                    var d = Document.Create.NewDimension(dimView, runDimLine, ra);
                    foreach (DimensionSegment s in d.Segments) sumMm += (s.Value ?? 0) * MM_PER_FT;
                    report.Add($"{name} | {openings} opening(s) | {d.NumberOfSegments} segments: {segList}");
                    madeRun++; runOk = true;
                }
                catch (Exception ex)
                { report.Add($"{name} | run | FAILED — {ex.Message.Split('\n')[0]}"); failed++; }
            }

            if (doOverall || cuts.Count == 2)
            {
                try
                {
                    double dAll = dRun + (cuts.Count > 2 ? gapFt : 0);
                    var qA = a + perp.Multiply(sign * dAll); var qB = b + perp.Multiply(sign * dAll);
                    var ra2 = new ReferenceArray(); ra2.Append(cuts.First().Item2); ra2.Append(cuts.Last().Item2);
                    var d2 = Document.Create.NewDimension(dimView,
                        Line.CreateBound(new XYZ(qA.X, qA.Y, planZ), new XYZ(qB.X, qB.Y, planZ)), ra2);
                    double got = (d2.Value ?? 0) * MM_PER_FT;
                    // The check the header argues for: an extra or missing face shows up here and nowhere else.
                    string cross = !runOk ? ""
                        : (Math.Abs(sumMm - got) < 1 ? $" | segments sum to {sumMm:F0} — MATCH"
                                                     : $" | segments sum to {sumMm:F0} — OFF BY {sumMm - got:F0} mm, read the segment list");
                    report.Add($"{name} | overall | {got:F0} mm | expected {overallExpect:F0}" +
                               (Math.Abs(got - overallExpect) < 1 ? "" : $" — OFF BY {got - overallExpect:F0} mm") + cross);
                    madeOverall++;
                }
                catch (Exception ex)
                { report.Add($"{name} | overall | FAILED — {ex.Message.Split('\n')[0]}"); failed++; }
            }
        }
    };

    if (dryRun) buildAll();
    else
    {
        using (var tDim = new Transaction(Document, "AJ Tools - Dimension wall openings"))
        {
            tDim.Start();
            try { buildAll(); tDim.Commit(); }
            catch (Exception ex) { tDim.RollBack(); sb.AppendLine("FAILED — rolled back, nothing created. " + ex.Message); }
        }
    }

    sb.AppendLine($"{targets.Count} wall(s) targeted" +
        (fromRooms > 0 ? $" ({fromRooms} resolved from the room boundaries passed in)" : "") +
        $", side '{sideKey}', in view '{dimView.Name}'.");
    sb.AppendLine(dryRun
        ? "DRY RUN — nothing created."
        : $"Created {madeRun} running string(s) and {madeOverall} overall dimension(s); {failed} failed, {skipped} skipped.");
    if (ignored > 0) sb.AppendLine($"  ({ignored} element(s) were neither a Wall nor a Room — ignored.)");
    if (report.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("Wall | What | Result");
        sb.AppendLine("--- | --- | ---");
        foreach (var r in report) sb.AppendLine(r);
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
