// ============================================================
// RECIPE — sprinkler-obstruction-survey.cs
// PURPOSE: Look INSIDE a room before laying out a single sprinkler head — is there a ceiling, what is
//          the deck above, which beams and columns are in here, how deep do they hang, is there a
//          repeating structural bay, and which duct/tray/pipe/light runs are wide enough to matter.
//          Ajmal's ask: "if any beem or colom is there in the room as per that need to place".
//          READ-ONLY — this is the LOOK step. It changes nothing and decides nothing.
// STANDALONE — has its own sb and returns; not filter+action shaped.
// SOURCE: ../../knowledge/fire-sprinkler/obstructions.md            (what each of these does to a head)
// SOURCE: ../../knowledge/fire-sprinkler/revit-modelling.md         (which category holds what, and the traps)
// SOURCE: ../../knowledge/fire-sprinkler/concealed-spaces.md        (whether the ceiling VOID needs its own heads)
//
// RUN THIS FIRST. The grid recipe knows the room OUTLINE and nothing about what hangs inside it, and the
//   single biggest driver of a head count in an exposed-structure room is the BAY MODULE, not the code's
//   maximum spacing. Finding that out after the grid is drawn means drawing it again.
//
// THE QUESTION IT IS REALLY ANSWERING: is this room UNOBSTRUCTED or OBSTRUCTED construction? That one
//   word changes the maximum area per head (light hazard drops 225 -> 130 ft2 with combustible members
//   closer than 3 ft, which is nearly double the heads), the deflector rule (1-12 in below a ceiling
//   vs 1-6 in below a beam soffit), and whether the grid is free or set by bays. The survey gives you
//   the measurements; the classification is still a human call — see the definition in
//   knowledge/fire-sprinkler/deflector-and-ceiling-height.md.
//
// GOTCHA: **a bounding box is not a beam.** A beam at 30 degrees has a bounding box far wider than the
//         beam itself. This reads LocationCurve wherever there is one (framing, ducts, pipes, trays,
//         conduit) and falls back to the bounding box only where there is not — and says which it used,
//         per element, so a wrong-looking width can be traced to the reason.
// GOTCHA: **an element crossing the room is not necessarily IN it.** Beams span several rooms, so
//         membership is tested by sampling points along the element and asking Room.IsPointInRoom at the
//         ROOM's probe height, not the element's own Z — a beam above the room's upper limit fails a
//         naive test and vanishes from the survey silently.
// GOTCHA: **soffit levels here are bounding-box bottoms.** Accurate for a level beam, a flat slab and a
//         horizontal duct; WRONG for anything sloped or cranked, where the box bottom is the low end.
//         Sloped members are flagged rather than quietly averaged.
// GOTCHA: **the VOID DEPTH this reports is a prompt, not an answer.** Ceiling top to slab soffit, from
//         bounding boxes — right for a flat ceiling under a flat slab, approximate for anything sloped or
//         stepped, and it does NOT deduct the beams hanging inside the void (the clear depth under a
//         downstand is less, sometimes much less, and that is the depth a sprinkler actually lives in).
//         It also cannot tell you whether the void needs heads: **NFPA tests what the void is MADE OF,
//         BS EN 12845 tests whether it is deeper than 800 mm, and the two disagree on the same void.**
//         The threshold below is the BS/EN one because it is the only one that IS a depth. On an NFPA job
//         the flag means "go and ask what is in this void", nothing more.
// GOTCHA: **"no ceiling found" can mean the ceiling is a linked-model element.** This reads the host
//         document only. If the architectural model is linked, use
//         filters/by-relationship/filter-by-linked-model-elements.cs and say the survey is host-only.
// GOTCHA: the 18 in (457 mm) "may be ignored" test needs a deflector level to measure from. Give
//         deflectorZmm if you already know it; leave it 0 and the survey reports raw soffit levels
//         without judging them, which is honest rather than guessed.
//
// STATUS: LIVE-VERIFIED 2026-08-20 on Revit 2020 (model 'Project1', Room 3, 118.3 m2). Ran clean and
//   reported correctly: found the one ceiling at 2,400 mm, and correctly reported no beams, no columns
//   and no services in a room that genuinely has none. The "DECK: no floor/slab found" and
//   "VOID: cannot be measured" branches both fired and read sensibly — worth knowing they are the normal
//   output for a model with no slab above, not a failure.
//   STILL UNPROVEN, because the test model had none of it: the BAY MODULE arithmetic, the sloped-member
//   flag, the box-fallback width path, and every services branch. Those need a room with real beams and
//   real ducts before the numbers they print can be trusted. The ceiling/deck/void half is proven.
//   ONE FIX APPLIED: `Autodesk.Revit.DB.Architecture.Room` must be fully qualified — the bridge prelude
//   does not import that namespace, and a bare `Room` is error CS0246. This file already qualifies it;
//   anything composed FROM it must too.
//
// ✱✱ FIXED 2026-08-24 — LEVEL HEIGHTS HERE NOW USE `ProjectElevation`, NOT `Elevation`.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says (Project OR Shared); `ProjectElevation` is always from the project origin, which is
//    the space every XYZ in the model lives in. This fragment mixes a level height with real
//    coordinates, so on a model with a survey offset the old code was wrong by exactly that offset —
//    silently, with a plausible number and no error. See
//    knowledge/live-model/level-elevation-vs-project-elevation.md, and run
//    action-report-level-elevations.cs to see whether a given model is affected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;                    // the room to survey (must be PLACED — Area > 0)
double deflectorZmm = 0;              // known deflector level, project coordinates. 0 = report levels, judge nothing.
double ignoreBelowDeflectorMm = 457;  // NFPA 18 in — an obstruction this far BELOW the deflector, and no
                                      // wider than ignoreWiderThanMm, may be ignored. Confirm against your edition.
double ignoreWiderThanMm = 1219;      // NFPA 4 ft — wider than this and it needs heads underneath instead
double narrowObstructionMm = 610;     // NFPA 24 in — under this counts as an isolated obstruction (three-times rule)
bool surveyStructure = true;          // beams / joists / trusses  + columns
bool surveyServices = true;           // ducts, flex ducts, cable tray, conduit, pipes, lighting
bool surveyCeilingAndDeck = true;     // is there a ceiling, and what is the slab above
double searchAboveRoomMm = 6000;      // how far above the room's own top to look for the deck and framing
double voidDepthThresholdMm = 800;    // ceiling void depth that triggers a flag. 800 mm is the BS 5306-2 /
                                      // BS EN 12845 trigger for sprinklers IN the void. NFPA has no depth
                                      // trigger at all — it tests combustibility. Set 0 to skip the flag.
int maxListedPerCategory = 30;        // detail cap; counts always cover everything
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0)
{
    sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to survey. Place it first.");
}
else
{
    var rbb = room.get_BoundingBox(null);
    double zProbe = room.Level.ProjectElevation + mm(1000);
    double zRoomBase = room.Level.ProjectElevation;
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    sb.AppendLine($"OBSTRUCTION SURVEY — '{room.Name}' (Id {roomIdInt}), {room.Area / 10.763910416709722:F1} m2,"
        + $" level '{room.Level.Name}' at {toMm(zRoomBase):N0} mm");
    sb.AppendLine("READ-ONLY. Measurements only — the construction classification is still yours to make.");
    sb.AppendLine();

    // the search volume: the room's plan extent, from its base up to well above it
    var outline = new Outline(
        new XYZ(rbb.Min.X, rbb.Min.Y, zRoomBase - mm(500)),
        new XYZ(rbb.Max.X, rbb.Max.Y, rbb.Max.Z + mm(searchAboveRoomMm)));
    var volumeFilter = new BoundingBoxIntersectsFilter(outline);

    // is this element really in the room? sample its curve or its box footprint, project to the probe height
    Func<Element, List<XYZ>> planSamples = el =>
    {
        var pts = new List<XYZ>();
        var lc = el.Location as LocationCurve;
        if (lc != null && lc.Curve != null)
        {
            var c = lc.Curve;
            for (double t = 0; t <= 1.0001; t += 0.1)
            { try { pts.Add(c.Evaluate(Math.Min(t, 1.0), true)); } catch { } }
        }
        else
        {
            var b = el.get_BoundingBox(null);
            if (b != null)
            {
                pts.Add(new XYZ(b.Min.X, b.Min.Y, 0)); pts.Add(new XYZ(b.Max.X, b.Min.Y, 0));
                pts.Add(new XYZ(b.Min.X, b.Max.Y, 0)); pts.Add(new XYZ(b.Max.X, b.Max.Y, 0));
                pts.Add(new XYZ((b.Min.X + b.Max.X) / 2.0, (b.Min.Y + b.Max.Y) / 2.0, 0));
            }
        }
        return pts;
    };
    Func<Element, bool> inThisRoom = el => planSamples(el).Any(p => insideRoom(p));

    Func<BuiltInCategory, List<Element>> collectIn = cat =>
        new FilteredElementCollector(Document)
            .OfCategory(cat).WhereElementIsNotElementType()
            .WherePasses(volumeFilter)
            .Where(inThisRoom).ToList();

    // ---------- 1. CEILING AND DECK — which height rule this room is under ----------
    double ceilingZ = double.NaN, ceilingTopZ = double.NaN, deckZ = double.NaN;
    if (surveyCeilingAndDeck)
    {
        sb.AppendLine("1. WHAT IS ABOVE — this decides pendent vs upright, and which deflector rule applies");

        var ceilings = collectIn(BuiltInCategory.OST_Ceilings);
        if (ceilings.Count == 0)
        {
            sb.AppendLine("   CEILINGS: none found in this room (host document only — a LINKED architectural ceiling");
            sb.AppendLine("             would not appear here). If that is right, this is an EXPOSED room: upright heads,");
            sb.AppendLine("             and the deck/beams below become the reference plane, not a ceiling.");
        }
        else
        {
            foreach (var c in ceilings.Take(maxListedPerCategory))
            {
                var b = c.get_BoundingBox(null);
                if (b == null) continue;
                if (double.IsNaN(ceilingZ) || b.Min.Z < ceilingZ) ceilingZ = b.Min.Z;
                if (double.IsNaN(ceilingTopZ) || b.Max.Z < ceilingTopZ) ceilingTopZ = b.Max.Z;
                sb.AppendLine($"   CEILING '{c.Name}' (Id {c.Id}) underside about {toMm(b.Min.Z):N0} mm"
                    + $" — {toMm(b.Min.Z - zRoomBase):N0} mm above this level");
            }
            sb.AppendLine($"   -> {ceilings.Count} ceiling(s). Lowest underside {toMm(ceilingZ):N0} mm.");
            sb.AppendLine("      Pendent/concealed heads: deflector 25-305 mm below this (unobstructed construction).");
            if (ceilings.Count > 1)
                sb.AppendLine("      *** MORE THAN ONE CEILING LEVEL — this is not one layout. Split by ceiling and lay out each.");
        }

        var floorsAbove = new FilteredElementCollector(Document)
            .OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType()
            .WherePasses(volumeFilter)
            .Where(f => { var b = f.get_BoundingBox(null); return b != null && b.Min.Z > zRoomBase + mm(500); })
            .Where(inThisRoom).ToList();
        foreach (var f in floorsAbove)
        {
            var b = f.get_BoundingBox(null);
            if (double.IsNaN(deckZ) || b.Min.Z < deckZ) deckZ = b.Min.Z;
        }
        if (floorsAbove.Count == 0) sb.AppendLine("   DECK: no floor/slab found above this room within the search height.");
        else sb.AppendLine($"   DECK: {floorsAbove.Count} slab(s) above; lowest soffit {toMm(deckZ):N0} mm"
            + $" — {toMm(deckZ - zRoomBase):N0} mm above this level.");

        // --- THE CEILING VOID: does it need sprinklers of its own? ---
        if (!double.IsNaN(ceilingTopZ) && !double.IsNaN(deckZ))
        {
            double voidMm = toMm(deckZ - ceilingTopZ);
            sb.AppendLine($"   VOID: {voidMm:N0} mm clear between the ceiling top ({toMm(ceilingTopZ):N0} mm) and the slab soffit ({toMm(deckZ):N0} mm).");
            if (voidMm <= 0)
            {
                sb.AppendLine("      That is zero or negative, which is not a real void — the two bounding boxes overlap in Z.");
                sb.AppendLine("      Usual causes: a sloped or stepped ceiling, or the slab found above is not the one over this room.");
                sb.AppendLine("      Measure it in a section before using any of it.");
            }
            else if (voidDepthThresholdMm > 0 && voidMm >= voidDepthThresholdMm)
            {
                sb.AppendLine($"      *** OVER THE {voidDepthThresholdMm:N0} mm THRESHOLD. This is the BS 5306-2 / BS EN 12845 trigger for");
                sb.AppendLine("          sprinklers INSIDE the void — upright in the void, pendent below the ceiling, TWO layouts.");
                sb.AppendLine("      *** NFPA 13 HAS NO DEPTH TRIGGER. It asks what the void is MADE OF: combustible construction needs");
                sb.AppendLine("          protection, noncombustible/limited-combustible with minimal combustible loading may be omitted.");
                sb.AppendLine("          So the two standards DISAGREE on this void. Which one does this project's specification call up?");
                sb.AppendLine("          QCDD enforces NFPA by default, but a spec can call up BS EN 12845 instead — and plenty do.");
                sb.AppendLine("          Settle it before laying anything out: knowledge/fire-sprinkler/concealed-spaces.md.");
            }
            else if (voidDepthThresholdMm > 0)
            {
                sb.AppendLine($"      Under the {voidDepthThresholdMm:N0} mm BS/EN threshold — but that says nothing about NFPA, which tests");
                sb.AppendLine("      combustibility, not depth. A shallow void of combustible construction still needs protecting.");
            }
            if (voidMm > 0)
            {
                sb.AppendLine("      NOTE: beams inside the void are NOT deducted from that figure — see the beam list below for how far");
                sb.AppendLine("      they hang. The clear depth under a downstand is what a void sprinkler actually lives in.");
                sb.AppendLine("      If the void IS protected, run this whole chain again for it as its own job: it is usually OBSTRUCTED");
                sb.AppendLine("      construction, so its grid is not the ceiling grid copied upward.");
            }
        }
        else if (surveyCeilingAndDeck)
            sb.AppendLine("   VOID: cannot be measured — need both a ceiling and a slab above, and one of them was not found.");
        sb.AppendLine();
    }

    // ---------- 2. BEAMS — the bay module, and how far they hang ----------
    var beamRows = new List<string>();
    if (surveyStructure)
    {
        sb.AppendLine("2. BEAMS AND FRAMING — the thing most likely to decide the head count");
        var framing = collectIn(BuiltInCategory.OST_StructuralFraming);
        if (framing.Count == 0) sb.AppendLine("   None in this room. If that matches what you see on screen, treat the soffit as flat.");
        else
        {
            // group by direction so a bay module can be spotted: perpendicular offsets of parallel beams
            var byDir = new Dictionary<int, List<Tuple<double, Element, double, double>>>();  // angleKey -> (offset, elem, depthBelowDeck, width)
            int sloped = 0, noCurve = 0;
            foreach (var e in framing)
            {
                var b = e.get_BoundingBox(null);
                if (b == null) continue;
                var lc = e.Location as LocationCurve;
                double widthMm, soffit = b.Min.Z;
                double angle; double offset;
                if (lc != null && lc.Curve is Line)
                {
                    var ln = lc.Curve as Line;
                    var d = ln.Direction;
                    if (Math.Abs(d.Z) > 0.01) sloped++;
                    angle = Math.Atan2(d.Y, d.X);
                    if (angle < 0) angle += Math.PI;                      // direction, not sense
                    // perpendicular offset of the line from the origin: how parallel beams are separated
                    var p0 = ln.GetEndPoint(0);
                    offset = -p0.X * Math.Sin(angle) + p0.Y * Math.Cos(angle);
                    // width across the beam = the smaller plan dimension of its box (good enough for an orthogonal beam)
                    widthMm = Math.Min(toMm(b.Max.X - b.Min.X), toMm(b.Max.Y - b.Min.Y));
                }
                else
                {
                    noCurve++;
                    angle = 0; offset = b.Min.Y;
                    widthMm = Math.Min(toMm(b.Max.X - b.Min.X), toMm(b.Max.Y - b.Min.Y));
                }
                double depthBelowDeck = double.IsNaN(deckZ) ? double.NaN : toMm(deckZ - soffit);
                int key = (int)Math.Round(angle * 180.0 / Math.PI / 5.0);   // 5-degree buckets
                if (!byDir.ContainsKey(key)) byDir[key] = new List<Tuple<double, Element, double, double>>();
                byDir[key].Add(Tuple.Create(offset, e, depthBelowDeck, widthMm));

                beamRows.Add($"   BEAM '{e.Name}' (Id {e.Id}) soffit {toMm(soffit):N0} mm"
                    + $" | width across {widthMm:N0} mm"
                    + (double.IsNaN(depthBelowDeck) ? "" : $" | hangs {depthBelowDeck:N0} mm below the deck")
                    + (lc == null ? "  [box fallback — no location curve, width may be overstated]" : ""));
            }
            sb.AppendLine($"   {framing.Count} framing member(s) in this room.");
            foreach (var r in beamRows.Take(maxListedPerCategory)) sb.AppendLine(r);
            if (beamRows.Count > maxListedPerCategory) sb.AppendLine($"   ... and {beamRows.Count - maxListedPerCategory} more.");
            if (sloped > 0) sb.AppendLine($"   *** {sloped} member(s) are SLOPED — their soffit level above is the LOW end of the box, not the real soffit. Check by hand.");
            if (noCurve > 0) sb.AppendLine($"   *** {noCurve} member(s) had no location curve — width came from the bounding box and may be overstated.");

            // the bay module: the commonest perpendicular gap between parallel members
            foreach (var kv in byDir.OrderByDescending(k => k.Value.Count))
            {
                if (kv.Value.Count < 2) continue;
                var offs = kv.Value.Select(t => toMm(t.Item1)).OrderBy(v => v).ToList();
                var gaps = new List<double>();
                for (int i = 1; i < offs.Count; i++) { double g = offs[i] - offs[i - 1]; if (g > 100) gaps.Add(g); }
                if (gaps.Count == 0) continue;
                var rounded = gaps.Select(g => Math.Round(g / 50.0) * 50.0).ToList();
                var modal = rounded.GroupBy(g => g).OrderByDescending(g => g.Count()).First();
                double dirDeg = kv.Key * 5.0;
                sb.AppendLine($"   BAY MODULE at about {dirDeg:F0} degrees: {kv.Value.Count} parallel member(s), "
                    + $"commonest gap {modal.Key:N0} mm ({modal.Count()} of {gaps.Count} gaps)"
                    + (gaps.Max() - gaps.Min() > 200 ? $" — but gaps run {gaps.Min():N0}..{gaps.Max():N0} mm, so it is NOT a clean module" : " — a clean module"));
                sb.AppendLine($"      -> if heads go in bays, feed baySpacingMm = {modal.Key:N0} into recipes/sprinkler-nfpa-grid.cs");
                sb.AppendLine($"         and search only the other axis. That spacing is then FIXED, not a choice.");
            }

            var deep = byDir.SelectMany(k => k.Value).Where(t => !double.IsNaN(t.Item3) && t.Item3 > 457).Count();
            if (deep > 0)
            {
                sb.AppendLine($"   *** {deep} member(s) hang more than 457 mm (18 in) below the deck — the commonly cited trigger");
                sb.AppendLine("       for 'a head in every bay' rather than throwing across the beams. Confirm against your edition.");
            }
        }
        sb.AppendLine();

        // ---------- 3. COLUMNS — the three-times rule, with NO 24 in cap ----------
        sb.AppendLine("3. COLUMNS — three-times rule, and the 610 mm (24 in) cap does NOT apply to these");
        var cols = collectIn(BuiltInCategory.OST_StructuralColumns);
        cols.AddRange(collectIn(BuiltInCategory.OST_Columns));
        if (cols.Count == 0) sb.AppendLine("   None in this room.");
        else
        {
            int shown = 0;
            foreach (var c in cols)
            {
                var b = c.get_BoundingBox(null);
                if (b == null) continue;
                double wx = toMm(b.Max.X - b.Min.X), wy = toMm(b.Max.Y - b.Min.Y);
                double maxDim = Math.Max(wx, wy);
                double keepOut = 3.0 * maxDim;
                var cx = (b.Min.X + b.Max.X) / 2.0; var cy = (b.Min.Y + b.Max.Y) / 2.0;
                if (shown++ < maxListedPerCategory)
                    sb.AppendLine($"   COLUMN '{c.Name}' (Id {c.Id}) at {toMm(cx):N0}, {toMm(cy):N0} mm"
                        + $" | {wx:N0} x {wy:N0} mm | KEEP HEADS AT LEAST {keepOut:N0} mm CLEAR (3 x {maxDim:N0})");
            }
            sb.AppendLine($"   -> {cols.Count} column(s) (structural + architectural). A vertical obstruction gets the FULL 3x its");
            sb.AppendLine("      largest dimension — the 610 mm relief that applies to other isolated obstructions is not available here.");
            sb.AppendLine("      Some editions use a FOUR-times variant for large vertical obstructions; check yours before relying on 3x.");
        }
        sb.AppendLine();
    }

    // ---------- 4. SERVICES — width and how far below the deflector ----------
    if (surveyServices)
    {
        sb.AppendLine("4. SERVICES IN THE WAY — width decides whether they can be ignored, need clearance, or need heads underneath");
        var svcCats = new[]
        {
            BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_CableTray, BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_LightingFixtures
        };
        int total = 0, needHeadsUnder = 0, ignorable = 0, undecided = 0, shown = 0;
        foreach (var cat in svcCats)
        {
            List<Element> items;
            try { items = collectIn(cat); } catch { continue; }
            foreach (var e in items)
            {
                var b = e.get_BoundingBox(null);
                if (b == null) continue;
                total++;
                double wx = toMm(b.Max.X - b.Min.X), wy = toMm(b.Max.Y - b.Min.Y);
                double width = Math.Min(wx, wy);          // across the run, for a straight orthogonal run
                double soffit = b.Min.Z;
                string call;
                if (width > ignoreWiderThanMm) { call = $"WIDER THAN {ignoreWiderThanMm:N0} mm — needs sprinklers UNDERNEATH"; needHeadsUnder++; }
                else if (deflectorZmm > 0 && toMm(soffit) <= deflectorZmm - ignoreBelowDeflectorMm)
                { call = $"ignorable — {deflectorZmm - toMm(soffit):N0} mm below the deflector, over the {ignoreBelowDeflectorMm:N0} mm threshold"; ignorable++; }
                else if (deflectorZmm > 0)
                { call = $"NOT ignorable — only {deflectorZmm - toMm(soffit):N0} mm below the deflector; apply the three-times rule or the beam table"; undecided++; }
                else { call = "no deflector level given — level reported, not judged"; undecided++; }

                if (shown++ < maxListedPerCategory)
                    sb.AppendLine($"   {e.Category.Name} '{e.Name}' (Id {e.Id}) soffit {toMm(soffit):N0} mm"
                        + $" | across {width:N0} mm | {call}");
            }
        }
        if (total == 0) sb.AppendLine("   None found in this room.");
        else
        {
            if (shown > maxListedPerCategory) sb.AppendLine($"   ... and {shown - maxListedPerCategory} more.");
            sb.AppendLine($"   -> {total} service run(s)/fitting(s): {needHeadsUnder} need heads underneath, "
                + $"{ignorable} ignorable, {undecided} still to judge.");
            sb.AppendLine($"      Under {narrowObstructionMm:N0} mm across counts as an ISOLATED obstruction (three-times rule);");
            sb.AppendLine($"      a continuous run uses the beam table instead. See knowledge/fire-sprinkler/obstructions.md.");
        }
        sb.AppendLine();
    }

    sb.AppendLine("NEXT STEPS");
    sb.AppendLine("  a) Say out loud whether this room is UNOBSTRUCTED or OBSTRUCTED construction, and why. Everything");
    sb.AppendLine("     downstream — max area per head, the deflector rule, whether the grid is free — hangs on that word.");
    sb.AppendLine("  b) recipes/sprinkler-nfpa-grid.cs for the head positions (feed it baySpacingMm if there is a module).");
    sb.AppendLine("  c) recipes/sprinkler-obstruction-check.cs to test those positions against everything listed above.");
    sb.AppendLine("  Services move between coordination rounds. Beams and columns do not. Re-run (c) after every round.");
}

return sb.ToString();
