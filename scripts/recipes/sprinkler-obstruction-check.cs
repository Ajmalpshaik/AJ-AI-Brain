// ============================================================
// RECIPE — sprinkler-obstruction-check.cs
// PURPOSE: Take a set of sprinkler head positions — proposed centres from recipes/sprinkler-nfpa-grid.cs,
//          or the heads already in the model — and test every one of them against every beam, column,
//          duct, tray and light fitting in the room. Applies the four obstruction rules in the order the
//          code applies them and prints one line per head-obstruction pair with the measured numbers and
//          the rule that judged it. READ-ONLY — it moves nothing.
// STANDALONE — has its own sb and returns; not filter+action shaped.
// SOURCE: ../../knowledge/fire-sprinkler/obstructions.md        (all four rules, and where each number came from)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md       (why this runs between the grid and placing)
//
// *** THE BEAM TABLE BELOW IS UNCONFIRMED AND YOU MUST TYPE YOUR EDITION'S VALUES OVER IT. ***
//   The session that wrote this file (2026-08-20) could not retrieve NFPA 13's own table — every route to
//   it was paywalled or blocked. The values seeded in `beamTable...` are the ones commonly published in
//   secondary summaries for standard spray pendent/upright. They are a starting point, not an answer.
//   The report prints an UNCONFIRMED banner on every run that uses them, and stops printing it the moment
//   you set beamTableConfirmed = true — so an unchecked number can never quietly become a compliance
//   claim on a drawing. Ten minutes with the adopted edition makes this fragment properly trustworthy.
//
// THE FOUR RULES, IN THE ORDER THEY ARE APPLIED (knowledge/fire-sprinkler/obstructions.md has the detail):
//   1. IGNORABLE   — no wider than 4 ft (1,219 mm) AND at least 18 in (457 mm) below the deflector.
//   2. TOO WIDE    — wider than 4 ft: it needs its own heads underneath, deflector within ~12 in of it.
//   3. THREE TIMES — isolated/vertical obstruction: keep 3 x its largest dimension clear, capped at 24 in
//                    (610 mm) — EXCEPT for a vertical obstruction such as a column, where there is NO cap.
//                    A 600 mm column needs 1,800 mm, not 610. This is the trap in a car park.
//   4. BEAM TABLE  — a continuous obstruction: the further the head is from its side, the higher above its
//                    bottom the deflector may sit. Inside 1 ft there is no allowance at all.
//
// GOTCHA: **a head that passes every rule can still be wrong.** These rules are about the spray clearing
//         the obstruction. They say nothing about the floor area SHADOWED behind it. A layout that passes
//         here and leaves an area unreachable is a failed layout — look at the plan, do not just read the
//         PASS count.
// GOTCHA: **fixing one head breaks another.** Move a head to clear a beam and its spacing, wall distance
//         and minimum-separation all change. ALWAYS re-run recipes/sprinkler-nfpa-grid.cs in gridMode
//         "fixed", or recipes/sprinkler-compliance-audit.cs, on the MOVED layout. This is the step most
//         often skipped and it is how a compliant grid quietly becomes a non-compliant one.
// GOTCHA: plan distances here are measured to the obstruction's real geometry — sampled along a location
//         curve, or to the bounding rectangle where there is no curve. A sloped or cranked member is
//         flagged; its bounding box bottom is the low end, not the soffit at the head.
// GOTCHA: this reads the HOST document. Beams and columns in a linked structural model will not appear —
//         and "no obstructions found" is then a false clean. Check the link situation before believing it.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. Every Revit call in it is proven
//   elsewhere in this library (collectors + BoundingBoxIntersectsFilter, Room.IsPointInRoom,
//   LocationCurve sampling). The rule arithmetic is plain C# with no Revit dependency, so the honest test
//   is: run it on ONE head next to ONE known beam, check the two measured numbers against a tape on
//   screen, then trust it for the batch.
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
int roomIdInt = 0;                    // the room whose obstructions are tested
string headSource = "points";         // "points"   = test the candidate centres below (from the grid recipe)
                                      // "existing" = test the Sprinklers already modelled in this room
List<(double xMm, double yMm)> candidatePointsMm = new List<(double, double)>
{
    // paste the HEAD CENTRES block straight out of recipes/sprinkler-nfpa-grid.cs
};
double deflectorZmm = 0;              // deflector level, project coordinates. REQUIRED — every vertical
                                      // test measures from it. 0 refuses to run rather than guess.

// --- the rule numbers. Confirm each against your adopted edition.
double ignoreBelowDeflectorMm = 457;  // 18 in — this far below the deflector and narrow enough, ignore it
double wideObstructionMm = 1219;      // 4 ft  — wider than this needs sprinklers underneath
double threeTimesCapMm = 610;         // 24 in — the cap on the three-times rule...
bool capAppliesToColumns = false;     // ...which does NOT apply to a vertical obstruction. Leave false.
double beamRuleMinClearMm = 305;      // 1 ft  — inside this, the deflector may not be above the bottom at all
double underWideObstructionMm = 305;  // 12 in — how close a head under a wide obstruction must be to its underside

// --- THE BEAM TABLE. Horizontal distance head-to-side-of-obstruction, and how far the deflector may then
//     sit ABOVE the bottom of that obstruction. Both in mm, ascending, same length. See the banner above.
bool beamTableConfirmed = false;      // set true ONLY after typing your edition's own values in below
double[] beamTableDistanceMm = new[] { 305.0, 457, 610, 762, 914, 1067, 1219, 1372, 1524, 1676, 1829 };
double[] beamTableAllowedMm  = new[] {  64.0,  89, 140, 191, 241,  305,  356,  419,  457,  508,  610 };

// --- what to test against
bool testStructure = true;            // beams/framing and columns
bool testServices = true;             // ducts, flex, tray, conduit, pipes, lighting
double searchAboveRoomMm = 6000;
int maxPairsListed = 200;             // detail cap; the counts always cover everything
bool listPassesToo = false;           // false = only report the pairs that need attention
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (room.Area <= 0) { sb.AppendLine($"'{room.Name}' is UNPLACED (0 m2) — nothing to check."); }
else if (deflectorZmm <= 0)
{
    sb.AppendLine("NOT RUN — deflectorZmm is not set, and every vertical test measures from it.");
    sb.AppendLine("  Get it from recipes/sprinkler-deflector-height.cs (which reads what is really above each head),");
    sb.AppendLine("  or state it: ceiling underside minus 25..305 mm for a pendent under unobstructed construction.");
}
else if (beamTableDistanceMm.Length != beamTableAllowedMm.Length)
{
    sb.AppendLine($"NOT RUN — the beam table is malformed: {beamTableDistanceMm.Length} distances against {beamTableAllowedMm.Length} allowances.");
}
else
{
    var rbb = room.get_BoundingBox(null);
    double zProbe = room.Level.ProjectElevation + mm(1000);
    double zRoomBase = room.Level.ProjectElevation;
    Func<XYZ, bool> insideRoom = p =>
    { bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { } return i; };

    sb.AppendLine($"OBSTRUCTION CHECK — '{room.Name}' (Id {roomIdInt}), deflector level {deflectorZmm:N0} mm");
    if (!beamTableConfirmed)
    {
        sb.AppendLine("*** THE BEAM TABLE IN THIS RUN IS UNCONFIRMED — commonly published values, NOT verified against");
        sb.AppendLine("    the standard. Every Rule 4 result below inherits that uncertainty. Type your adopted");
        sb.AppendLine("    edition's table into beamTableDistanceMm/beamTableAllowedMm and set beamTableConfirmed = true.");
    }
    sb.AppendLine();

    // ---- 1. gather the head positions ----
    var heads = new List<Tuple<string, XYZ>>();      // label, plan point (Z ignored)
    if (headSource == "existing")
    {
        var placed = new FilteredElementCollector(Document)
            .OfCategory(BuiltInCategory.OST_Sprinklers).WhereElementIsNotElementType().ToList();
        foreach (var h in placed)
        {
            var lp = h.Location as LocationPoint;
            if (lp == null) continue;
            if (!insideRoom(lp.Point)) continue;
            heads.Add(Tuple.Create($"Id {h.Id}", lp.Point));
        }
        sb.AppendLine($"HEADS: {heads.Count} existing sprinkler(s) found inside this room (category Sprinklers, host document).");
        if (heads.Count == 0)
            sb.AppendLine("  None. If you expected some, check they are the Sprinklers category and not Plumbing Fixtures.");
    }
    else
    {
        int n = 0;
        foreach (var p in candidatePointsMm)
            heads.Add(Tuple.Create($"#{++n}", new XYZ(mm(p.xMm), mm(p.yMm), 0)));
        sb.AppendLine($"HEADS: {heads.Count} candidate centre(s) supplied.");
        int outside = heads.Count(h => !insideRoom(h.Item2));
        if (outside > 0) sb.AppendLine($"  *** {outside} of them are OUTSIDE this room and cannot be mounted — fix the layout first.");
    }

    if (heads.Count == 0) { sb.AppendLine("Nothing to check."); }
    else
    {
        // ---- 2. gather the obstructions, with the geometry each rule needs ----
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
            if (b == null) return false;
            return insideRoom(new XYZ((b.Min.X + b.Max.X) / 2.0, (b.Min.Y + b.Max.Y) / 2.0, 0))
                || insideRoom(new XYZ(b.Min.X, b.Min.Y, 0)) || insideRoom(new XYZ(b.Max.X, b.Max.Y, 0));
        };

        // one obstruction record: kind, label, soffit, plan width, and a plan-distance function
        var obstructions = new List<Tuple<string, string, double, double, Func<XYZ, double>, bool>>();
        //                          kind    label   soffitZ  widthMm  distanceToPlan            sloped

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
                double soffit = b.Min.Z;
                double wx = toMm(b.Max.X - b.Min.X), wy = toMm(b.Max.Y - b.Min.Y);
                var samples = curveSamples(e);
                bool sloped = false;
                Func<XYZ, double> dist;

                if (samples.Count > 1)
                {
                    // continuous run: nearest sampled point on its centreline, in plan, then take off half its width
                    var lc = e.Location as LocationCurve;
                    var ln = lc == null ? null : lc.Curve as Line;
                    if (ln != null && Math.Abs(ln.Direction.Z) > 0.01) sloped = true;
                    double halfWidthFt = mm(Math.Min(wx, wy) / 2.0);
                    var flat = samples.Select(p => new XYZ(p.X, p.Y, 0)).ToList();
                    dist = pt =>
                    {
                        double best = double.MaxValue;
                        foreach (var s in flat)
                        { double dx = pt.X - s.X, dy = pt.Y - s.Y; double d = Math.Sqrt(dx * dx + dy * dy); if (d < best) best = d; }
                        return Math.Max(0, best - halfWidthFt);        // to the SIDE of it, not its centreline
                    };
                }
                else
                {
                    // a box: distance from the point to the rectangle, 0 if inside it
                    double minX = b.Min.X, maxX = b.Max.X, minY = b.Min.Y, maxY = b.Max.Y;
                    dist = pt =>
                    {
                        double dx = Math.Max(Math.Max(minX - pt.X, pt.X - maxX), 0);
                        double dy = Math.Max(Math.Max(minY - pt.Y, pt.Y - maxY), 0);
                        return Math.Sqrt(dx * dx + dy * dy);
                    };
                }
                string nm = null;
                try { nm = e.Name; } catch { }
                obstructions.Add(Tuple.Create(kind,
                    $"{(e.Category != null ? e.Category.Name : "?")} '{nm ?? "?"}' (Id {e.Id})",
                    soffit, Math.Min(wx, wy), dist, sloped));
            }
        };

        if (testStructure)
        {
            gather(BuiltInCategory.OST_StructuralFraming, "continuous");
            gather(BuiltInCategory.OST_StructuralColumns, "vertical");
            gather(BuiltInCategory.OST_Columns, "vertical");
        }
        if (testServices)
        {
            gather(BuiltInCategory.OST_DuctCurves, "continuous");
            gather(BuiltInCategory.OST_FlexDuctCurves, "continuous");
            gather(BuiltInCategory.OST_CableTray, "continuous");
            gather(BuiltInCategory.OST_Conduit, "continuous");
            gather(BuiltInCategory.OST_PipeCurves, "continuous");
            gather(BuiltInCategory.OST_LightingFixtures, "isolated");
        }

        sb.AppendLine($"OBSTRUCTIONS: {obstructions.Count} element(s) in this room "
            + $"({obstructions.Count(o => o.Item1 == "vertical")} vertical, "
            + $"{obstructions.Count(o => o.Item1 == "continuous")} continuous, "
            + $"{obstructions.Count(o => o.Item1 == "isolated")} isolated).");
        if (obstructions.Count == 0)
            sb.AppendLine("  Nothing found. Before treating that as clean: are the beams and columns in a LINKED model?");
        sb.AppendLine();

        // ---- 3. apply the rules, head by head ----
        // the beam table lookup: how far above the bottom the deflector may sit at this plan distance
        Func<double, double> allowedAbove = distMm =>
        {
            if (distMm < beamRuleMinClearMm) return 0;
            double allowed = 0;
            for (int i = 0; i < beamTableDistanceMm.Length; i++)
                if (distMm >= beamTableDistanceMm[i]) allowed = beamTableAllowedMm[i];
            return allowed;
        };

        int pairs = 0, listed = 0;
        int ignorable = 0, needUnder = 0, passed = 0, failed = 0, slopedFlags = 0;
        var failingHeads = new HashSet<string>();

        foreach (var head in heads)
        {
            foreach (var o in obstructions)
            {
                string kind = o.Item1, label = o.Item2;
                double soffitMm = toMm(o.Item3), widthMm = o.Item4;
                double dMm = toMm(o.Item5(head.Item2));
                bool sloped = o.Item6;
                pairs++;
                if (sloped) slopedFlags++;

                double belowDeflector = deflectorZmm - soffitMm;     // + = the obstruction hangs below the deflector
                string rule, result;
                bool isFail = false, wasIgnorable = false;

                if (widthMm > wideObstructionMm)
                {
                    rule = "2 TOO WIDE";
                    result = $"{widthMm:N0} mm across, over the {wideObstructionMm:N0} mm limit — needs its own head(s) "
                        + $"UNDERNEATH, deflector within {underWideObstructionMm:N0} mm of its underside";
                    needUnder++; isFail = true;
                }
                else if (belowDeflector >= ignoreBelowDeflectorMm)
                {
                    rule = "1 IGNORABLE";
                    result = $"hangs {belowDeflector:N0} mm below the deflector (>= {ignoreBelowDeflectorMm:N0}) and is only "
                        + $"{widthMm:N0} mm across — the spray closes over behind it";
                    ignorable++; wasIgnorable = true;
                }
                else if (kind == "vertical")
                {
                    double need = 3.0 * widthMm;
                    if (capAppliesToColumns) need = Math.Min(need, threeTimesCapMm);
                    rule = "3 THREE TIMES (vertical — no cap)";
                    isFail = dMm < need;
                    result = $"{dMm:N0} mm clear against {need:N0} mm needed (3 x {widthMm:N0} mm)";
                }
                else if (kind == "isolated")
                {
                    // ISOLATED, not continuous — a light fitting, a single hanger, a beam seen end-on.
                    // A narrow BEAM is still continuous and still uses the table below; width alone must
                    // never route something into this branch or a 200 mm downstand escapes the beam rule.
                    double need = Math.Min(3.0 * widthMm, threeTimesCapMm);
                    rule = "3 THREE TIMES";
                    isFail = dMm < need;
                    result = $"{dMm:N0} mm clear against {need:N0} mm needed (3 x {widthMm:N0} mm, capped at {threeTimesCapMm:N0})";
                }
                else
                {
                    double allowed = allowedAbove(dMm);
                    double actualAbove = deflectorZmm - soffitMm;    // + = deflector ABOVE the obstruction's bottom
                    rule = "4 BEAM TABLE" + (beamTableConfirmed ? "" : " [UNCONFIRMED]");
                    if (actualAbove <= 0)
                    {
                        isFail = false;
                        result = $"deflector is {-actualAbove:N0} mm BELOW the bottom of it at {dMm:N0} mm away — clear";
                    }
                    else if (dMm < beamRuleMinClearMm)
                    {
                        isFail = true;
                        result = $"only {dMm:N0} mm away (under the {beamRuleMinClearMm:N0} mm floor) and the deflector sits "
                            + $"{actualAbove:N0} mm ABOVE its bottom — no allowance exists inside {beamRuleMinClearMm:N0} mm";
                    }
                    else
                    {
                        isFail = actualAbove > allowed;
                        result = $"deflector {actualAbove:N0} mm above its bottom, at {dMm:N0} mm away — table allows {allowed:N0} mm";
                    }
                }

                if (isFail) { failed++; failingHeads.Add(head.Item1); }
                else if (!wasIgnorable) passed++;

                if ((isFail || listPassesToo) && listed < maxPairsListed)
                {
                    listed++;
                    sb.AppendLine($"  {(isFail ? "FAIL" : "ok  ")} head {head.Item1} vs {label}");
                    sb.AppendLine($"        rule {rule}: {result}" + (sloped ? "  [SLOPED member — soffit is the box's low end, check by hand]" : ""));
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine($"RESULT — {heads.Count} head(s) x {obstructions.Count} obstruction(s) = {pairs:N0} pair(s) tested");
        sb.AppendLine($"  ignorable (rule 1)            {ignorable,6:N0}");
        sb.AppendLine($"  need heads underneath (rule 2){needUnder,6:N0}");
        sb.AppendLine($"  cleared (rules 3 and 4)       {passed,6:N0}");
        sb.AppendLine($"  FAILED                        {failed,6:N0}   across {failingHeads.Count} head(s)");
        if (listed >= maxPairsListed) sb.AppendLine($"  (listing stopped at {maxPairsListed} — raise maxPairsListed to see the rest)");
        if (slopedFlags > 0) sb.AppendLine($"  {slopedFlags:N0} pair(s) involved a sloped member — those soffit levels are approximate.");

        if (failed == 0 && obstructions.Count > 0)
        {
            sb.AppendLine("  Every head clears every obstruction found. That is NOT the same as a good layout —");
            sb.AppendLine("  look at the plan for floor area SHADOWED behind bulkheads and wide members, which no rule here tests.");
        }
        else if (failed > 0)
        {
            sb.AppendLine("  NEXT: recipes/sprinkler-adjust-for-obstructions.cs proposes the smallest move that clears each one");
            sb.AppendLine("  (dry-run by default), and then the moved layout MUST go back through the spacing check —");
            sb.AppendLine("  recipes/sprinkler-nfpa-grid.cs in gridMode \"fixed\", or recipes/sprinkler-compliance-audit.cs.");
        }
        if (!beamTableConfirmed)
            sb.AppendLine("  REMINDER: rule 4 above used an UNCONFIRMED table. Confirm it before any of this reaches a drawing.");
    }
}

return sb.ToString();
