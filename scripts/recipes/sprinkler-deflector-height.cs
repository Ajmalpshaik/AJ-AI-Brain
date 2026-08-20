// ============================================================
// RECIPE — sprinkler-deflector-height.cs
// PURPOSE: Answer "how high does this head sit" by READING what is really above it, not by assuming a
//          void depth. Fires a ray straight up from each head position, identifies what it hits — a
//          Ceiling, a Floor/slab soffit, or a structural member — and from that decides WHICH deflector
//          rule applies, computes the target level, and reports whether the head as modelled is inside
//          the window. Optionally writes the height back.
// STANDALONE — has its own sb and returns. Read-only unless applyHeights is turned on.
// SOURCE: ../../knowledge/fire-sprinkler/deflector-and-ceiling-height.md   (all four cases, with the numbers)
// SOURCE: ../../knowledge/live-model/geometry-and-transforms.md            (ray-casting technique and its traps)
//
// THE CASE IT DECIDES, WHICH IS THE WHOLE POINT:
//   hits a CEILING            -> case 1: deflector 25..305 mm below it (pendent, unobstructed construction)
//   hits a FLOOR/slab, clear  -> case 2a: exposed room, flat soffit — same 25..305 mm window, off the slab
//   hits STRUCTURAL FRAMING   -> case 2b: obstructed construction — 25..152 mm below the MEMBER's soffit,
//                                AND no more than maxBelowDeckMm below the deck. TWO limits, and a deep
//                                beam can make them impossible at once — which is the signal to put a head
//                                in each bay instead of trying to throw across the beams.
//   Ajmal's question in his words: "with celling how, without celling how" — this is the fragment that
//   answers it per head, from the model, instead of from a remembered ceiling void.
//
// *** THE FAMILY ORIGIN IS NOT THE DEFLECTOR. *** Every number in the code is measured to the flat plate
//   the water hits. The Z you read off a FamilyInstance is its insertion point, and in a real sprinkler
//   family those are commonly 20-80 mm apart. Measure it ONCE for the family you are using and put it in
//   originToDeflectorMm. Left at 0 this fragment assumes they coincide and SAYS SO in the report — an
//   assumption printed is survivable, an assumption hidden is the 50 mm that gets found on site.
//
// GOTCHA: **rays only see what the 3D view shows.** ReferenceIntersector obeys the view's visibility
//         completely — a hidden Ceilings category returns "nothing above" with a ceiling right there.
//         Proven live in this Brain: same element, same code, 0 hits in one view and 4 in another. This
//         fragment warns per category rather than reporting a false clear.
// GOTCHA: **one ray per head misses things.** A ray from the insertion point sees only what is directly
//         overhead. A beam passing 100 mm to the side is invisible to it, and that beam is exactly what
//         decides the case. sampleFan fires a small cross instead of a single line — leave it on unless
//         you are checking a single known-clear position.
// GOTCHA: heights that come back suspiciously round (exactly the level elevation, exactly 2700) usually
//         mean the ray found nothing and a fallback was used. The report names its source for every head.
// GOTCHA: with applyHeights = true this WRITES a parameter on real elements. The parameter name differs
//         by family — read it first, do not assume "Offset from Host" exists.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. The ray-casting shape is lifted
//   from actions/reporting/action-report-ray-hits.cs, which IS live-proven (2026-07-26, 8 rays x 2 walls
//   = 11 correct hits, geometry cross-checked against root-2). What is new is the case decision and the
//   window arithmetic. Run it read-only on ONE head first and check the reported "what is above" against
//   what you can see in a section.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;                    // the room whose heads are checked (0 = every sprinkler in the model)
string headSource = "existing";       // "existing" = the Sprinklers in the model | "points" = the list below
List<(double xMm, double yMm)> candidatePointsMm = new List<(double, double)> { };

// --- the deflector windows. Confirm against your adopted edition.
double belowCeilingMinMm = 25;        // 1 in  — unobstructed construction, pendent/upright
double belowCeilingMaxMm = 305;       // 12 in — unobstructed construction
double belowMemberMinMm = 25;         // 1 in  — obstructed construction, below the structural member
double belowMemberMaxMm = 152;        // 6 in  — obstructed construction
double maxBelowDeckMm = 559;          // 22 in — the second limit in obstructed construction [UNCONFIRMED]
double originToDeflectorMm = 0;       // family insertion point to deflector plate. 0 = assumed identical, and said so.

// --- the ray
double maxRayDistanceMm = 8000;
bool sampleFan = true;                // fire a small cross, not one line — a beam beside the head decides the case
double fanSpreadMm = 200;             // how far out the cross arms start

// --- writing back (default OFF)
bool applyHeights = false;            // true = write the computed level into the heads
string heightParameterName = "";      // MUST be the real parameter on your family — read it, do not assume
double targetBelowCeilingMm = 100;    // where in the window to aim when applying, measured below the reference
int maxListed = 100;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

Autodesk.Revit.DB.Architecture.Room room = null;
if (roomIdInt != 0) room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

if (roomIdInt != 0 && room == null) { sb.AppendLine($"roomIdInt {roomIdInt} is not a Room."); }
else if (applyHeights && string.IsNullOrWhiteSpace(heightParameterName))
{
    sb.AppendLine("NOT RUN — applyHeights is on but heightParameterName is blank.");
    sb.AppendLine("  The height parameter differs by family ('Offset from Host', 'Elevation from Level', 'Offset', ...).");
    sb.AppendLine("  Read it off one head first: actions/reporting/action-report-parameters.cs. Do not assume.");
}
else
{
    double zProbe = room != null ? room.Level.Elevation + mm(1000) : 0;
    Func<XYZ, bool> insideRoom = p =>
    {
        if (room == null) return true;
        bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { }
        return i;
    };

    // --- the 3D view the rays live in ---
    View3D rayView = Document.ActiveView as View3D;
    if (rayView == null)
        rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);

    if (rayView == null)
    {
        sb.AppendLine("No usable 3D view to ray-cast in — Revit requires one.");
        sb.AppendLine("  Create one (creators/create-view.cs, viewKind=\"three_d\") and rerun.");
    }
    else
    {
        // a hidden category is INVISIBLE to a ray — warn instead of reporting a false clear
        foreach (var cn in new[] { "Ceilings", "Floors", "Structural Framing" })
        {
            Category cat = null;
            foreach (Category c in Document.Settings.Categories) if (c.Name == cn) { cat = c; break; }
            if (cat == null) continue;
            try
            {
                if (rayView.GetCategoryHidden(cat.Id))
                    sb.AppendLine($"*** WARNING: '{cn}' is HIDDEN in 3D view '{rayView.Name}'. Rays cannot see it, so every"
                        + " head above one will be misclassified. Use a 3D view where it is visible.");
            }
            catch { }
        }

        var intersector = new ReferenceIntersector(rayView);
        intersector.TargetType = FindReferenceTarget.Face;
        double maxDistFt = mm(maxRayDistanceMm);

        // --- the heads ---
        var heads = new List<Tuple<string, XYZ, Element>>();
        if (headSource == "existing")
        {
            foreach (var h in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Sprinklers)
                         .WhereElementIsNotElementType().ToList())
            {
                var lp = h.Location as LocationPoint;
                if (lp == null || !insideRoom(lp.Point)) continue;
                heads.Add(Tuple.Create($"Id {h.Id}", lp.Point, h));
            }
        }
        else
        {
            double zStart = room != null ? room.Level.Elevation + mm(2500) : 0;
            int n = 0;
            foreach (var p in candidatePointsMm)
                heads.Add(Tuple.Create($"#{++n}", new XYZ(mm(p.xMm), mm(p.yMm), zStart), (Element)null));
        }

        sb.AppendLine($"DEFLECTOR HEIGHT — {heads.Count} head(s)"
            + (room != null ? $" in '{room.Name}'" : " (whole model)")
            + $", rays in 3D view '{rayView.Name}'");
        sb.AppendLine(originToDeflectorMm > 0
            ? $"  family insertion point sits {originToDeflectorMm:N0} mm above the deflector plate (as given)."
            : "  *** originToDeflectorMm is 0 — this run ASSUMES the family's insertion point IS the deflector."
              + " Measure it once and set it; a real family is usually 20-80 mm out.");
        sb.AppendLine();

        // the ray start points: one, or a small cross so a beam beside the head is not missed
        Func<XYZ, List<XYZ>> starts = p =>
        {
            var l = new List<XYZ> { p };
            if (sampleFan)
            {
                double d = mm(fanSpreadMm);
                l.Add(new XYZ(p.X + d, p.Y, p.Z)); l.Add(new XYZ(p.X - d, p.Y, p.Z));
                l.Add(new XYZ(p.X, p.Y + d, p.Z)); l.Add(new XYZ(p.X, p.Y - d, p.Z));
            }
            return l;
        };

        int ceilingCase = 0, flatCase = 0, obstructedCase = 0, nothingAbove = 0;
        int inWindow = 0, outOfWindow = 0, conflicts = 0, listed = 0;
        var writes = new List<Tuple<Element, double>>();

        foreach (var head in heads)
        {
            // fire up, keep the NEAREST hit per category so the case can be decided
            double ceilZ = double.NaN, floorZ = double.NaN, framingZ = double.NaN;
            string ceilName = null, framingName = null;

            foreach (var s in starts(head.Item2))
            {
                IList<ReferenceWithContext> hits = null;
                try { hits = intersector.Find(s, XYZ.BasisZ); } catch { }
                if (hits == null) continue;
                foreach (var hit in hits)
                {
                    if (hit.Proximity > maxDistFt) continue;
                    var r = hit.GetReference();
                    var el = Document.GetElement(r.ElementId);
                    if (el == null) continue;
                    if (head.Item3 != null && el.Id == head.Item3.Id) continue;      // drop self-hits
                    double z = s.Z + hit.Proximity;
                    var catName = el.Category != null ? el.Category.Name : "";
                    if (catName == "Ceilings") { if (double.IsNaN(ceilZ) || z < ceilZ) { ceilZ = z; try { ceilName = el.Name; } catch { } } }
                    else if (catName == "Floors") { if (double.IsNaN(floorZ) || z < floorZ) floorZ = z; }
                    else if (catName == "Structural Framing") { if (double.IsNaN(framingZ) || z < framingZ) { framingZ = z; try { framingName = el.Name; } catch { } } }
                }
            }

            // --- decide the case. The LOWEST thing above the head is what governs. ---
            string caseName, note = "";
            double refZ, winMinMm, winMaxMm;
            if (!double.IsNaN(framingZ) && (double.IsNaN(ceilZ) || framingZ < ceilZ))
            {
                caseName = "2b OBSTRUCTED (beam above)";
                refZ = framingZ; winMinMm = belowMemberMinMm; winMaxMm = belowMemberMaxMm;
                obstructedCase++;
                if (!double.IsNaN(floorZ))
                {
                    double memberDepth = toMm(floorZ - framingZ);
                    note = $" | member hangs {memberDepth:N0} mm below the deck";
                    // TWO limits at once: below the member, AND not too far below the deck. The highest the
                    // deflector may sit is member soffit - belowMemberMinMm; the deepest the deck rule allows
                    // is deck - maxBelowDeckMm. If even the HIGHEST allowed position is already too deep,
                    // no height satisfies both and the answer is a head in each bay, not a compromise.
                    double deckFloorMm = toMm(floorZ) - maxBelowDeckMm;        // deepest the deck rule permits
                    double highestAllowedMm = toMm(framingZ) - belowMemberMinMm;
                    if (highestAllowedMm < deckFloorMm)
                    {
                        conflicts++;
                        note += $" | *** CONFLICT: even {belowMemberMinMm:N0} mm below this member is deeper than"
                            + $" {maxBelowDeckMm:N0} mm under the deck. No single height satisfies both —"
                            + " this is the signal for a head IN EACH BAY.";
                    }
                }
            }
            else if (!double.IsNaN(ceilZ))
            {
                caseName = "1 CEILING";
                refZ = ceilZ; winMinMm = belowCeilingMinMm; winMaxMm = belowCeilingMaxMm;
                ceilingCase++;
                note = ceilName != null ? $" | '{ceilName}'" : "";
            }
            else if (!double.IsNaN(floorZ))
            {
                caseName = "2a EXPOSED, flat soffit";
                refZ = floorZ; winMinMm = belowCeilingMinMm; winMaxMm = belowCeilingMaxMm;
                flatCase++;
            }
            else
            {
                nothingAbove++;
                if (listed++ < maxListed)
                    sb.AppendLine($"  head {head.Item1}: NOTHING FOUND ABOVE within {maxRayDistanceMm:N0} mm — "
                        + "no case decided. Check the 3D view's visibility and whether the structure is in a LINK.");
                continue;
            }

            double refMm = toMm(refZ);
            double deflectorMm = toMm(head.Item2.Z) - originToDeflectorMm;
            double gap = refMm - deflectorMm;                       // how far the deflector sits below the reference
            bool ok = head.Item3 != null && gap >= winMinMm - 1e-6 && gap <= winMaxMm + 1e-6;
            if (head.Item3 != null) { if (ok) inWindow++; else outOfWindow++; }

            double target = refMm - Math.Min(Math.Max(targetBelowCeilingMm, winMinMm), winMaxMm);
            if (applyHeights && head.Item3 != null && !ok) writes.Add(Tuple.Create(head.Item3, target));

            if (listed++ < maxListed)
            {
                sb.AppendLine($"  head {head.Item1}: case {caseName}, reference {refMm:N0} mm{note}");
                sb.AppendLine($"        window {winMinMm:N0}..{winMaxMm:N0} mm below it  ->  deflector between "
                    + $"{refMm - winMaxMm:N0} and {refMm - winMinMm:N0} mm");
                if (head.Item3 != null)
                    sb.AppendLine($"        as modelled: deflector {deflectorMm:N0} mm, gap {gap:N0} mm — {(ok ? "IN THE WINDOW" : "OUT OF THE WINDOW")}");
                else
                    sb.AppendLine($"        not yet placed — aim for {target:N0} mm");
            }
        }

        sb.AppendLine();
        sb.AppendLine("SUMMARY BY CASE");
        sb.AppendLine($"  1  under a ceiling            {ceilingCase,5:N0}   (deflector {belowCeilingMinMm:N0}..{belowCeilingMaxMm:N0} mm below it)");
        sb.AppendLine($"  2a exposed, flat soffit       {flatCase,5:N0}   (same window, off the slab)");
        sb.AppendLine($"  2b obstructed, beam above     {obstructedCase,5:N0}   ({belowMemberMinMm:N0}..{belowMemberMaxMm:N0} mm below the member, max {maxBelowDeckMm:N0} mm below the deck)");
        sb.AppendLine($"     nothing found above        {nothingAbove,5:N0}");
        if (headSource == "existing")
            sb.AppendLine($"  as modelled: {inWindow:N0} in the window, {outOfWindow:N0} out of it.");
        if (conflicts > 0)
            sb.AppendLine($"  *** {conflicts:N0} head(s) sit under a member so deep that no height satisfies both limits."
                + " Put a head in each bay — knowledge/fire-sprinkler/obstructions.md, Rule C.");
        if (obstructedCase > 0 && ceilingCase > 0)
            sb.AppendLine("  *** This room has BOTH cases. It is not one layout — split it and lay out each part.");

        // ---- write back, if asked ----
        if (applyHeights && writes.Count > 0)
        {
            using (var t = new Transaction(Document, "AJ Tools - Set Sprinkler Deflector Heights"))
            {
                t.Start();
                try
                {
                    int done = 0, missing = 0;
                    foreach (var w in writes)
                    {
                        var p = w.Item1.LookupParameter(heightParameterName);
                        if (p == null || p.IsReadOnly) { missing++; continue; }
                        // the parameter is an offset in most families, so this writes an ABSOLUTE target only
                        // where the parameter is itself absolute. Read one back before trusting a batch.
                        p.Set(mm(w.Item2 + originToDeflectorMm));
                        done++;
                    }
                    t.Commit();
                    sb.AppendLine();
                    sb.AppendLine($"  WROTE {done:N0} height(s); {missing:N0} head(s) had no writable '{heightParameterName}'.");
                    sb.AppendLine($"  *** READ ONE BACK IN A SEPARATE CALL before trusting the batch. If '{heightParameterName}' is an");
                    sb.AppendLine("      OFFSET FROM A HOST rather than an absolute elevation, this wrote the wrong quantity —");
                    sb.AppendLine("      the report above cannot tell the difference and neither can this fragment.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED (heights) — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
        else if (applyHeights)
        {
            sb.AppendLine();
            sb.AppendLine("  applyHeights was on but nothing needed changing — every head is already in its window.");
        }
    }
}

return sb.ToString();
