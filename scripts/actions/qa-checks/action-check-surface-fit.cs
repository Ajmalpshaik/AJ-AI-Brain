// ============================================================
// FRAGMENT (action) — action-check-surface-fit.cs
// PURPOSE: QA check before (or after) snapping elements to a surface — for each element, fire rays from
//          its FOOTPRINT (centre + corners, or a 3x3 grid) toward a target and report whether the element
//          actually sits properly against what it finds. Flags the four ways a single centre ray lies:
//          STRADDLING (corners hit different elements — the unit spans a ceiling edge or level change),
//          OVERHANGING (some corners hit nothing — the unit hangs off the edge),
//          SLOPED (the hit face isn't perpendicular to the ray — it cannot sit flush),
//          UNEVEN (corners hit the same element at different heights).
//          Use it to decide which elements are safe to auto-move with
//          ../move-copy-rotate/action-move-to-ray-hit.cs and which need a human eye.
// ASSUMES: elements (List<Element>, point-based) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
//
// WHY THIS EXISTS: the single centre ray in action-report-ray-hits.cs / action-move-to-ray-hit.cs is
//   correct whenever the element sits well inside a large flat surface — measured true for all 17
//   terminals on Project1, 2026-07-26. It lies exactly at the edges, and edges are where mistakes live.
//   This is supersampling: same intersection maths, more sample points, and an honest confidence verdict.
//
// PERFORMANCE (measured on Project1, 3239 instances, 2026-07-26): ~0.07 ms per category-filtered ray,
//   ~0.11 ms unfiltered. Footprint mode is 5 rays per element, grid9 is 9. So 1000 elements in footprint
//   mode is ~5000 rays, well under a second — RAY COUNT IS NOT THE BOTTLENECK. Output volume is. That is
//   why this fragment reports BY EXCEPTION: a summary line always, then detail only for the elements that
//   failed, capped. A clean run on 5000 elements prints a handful of lines, not 5000.
//
// GOTCHA: ray-casting needs a real View3D (Revit's rule) — active view if it's 3D, else the first
//         unlocked non-template one. With none, it reports and stops.
// **RAYS ONLY SEE WHAT THE 3D VIEW SHOWS.** A category hidden in that view is invisible to the probe, so
//         every element comes back "NOTHING found" and the check looks like a clean pass when it never
//         tested anything. Proven live 2026-07-26 (0 hits vs 4 for identical code, purely from Walls being
//         hidden in one view). A suspiciously perfect result on a real model is the symptom — check the
//         view's visibility before trusting it.
// GOTCHA: rays start slightly OFF the element (startOffsetMm along the ray) so they clear its own body;
//         self-hits are dropped as well. Both are needed — the offset alone is not reliable for a family
//         whose insertion point sits inside deep geometry.
// GOTCHA: corner samples come from the element's BOUNDING BOX, which for a rotated or non-rectangular
//         family is bigger than the real footprint — a corner may miss when the actual unit does not.
//         Treat OVERHANGING on a rotated family as "look at it", not as proof.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1, BOTH paths:
//   - clean path: all 17 ceiling-mounted terminals reported OK (flush, level, one surface).
//   - detection path: a terminal was deliberately parked on a real boundary between the 3000 mm and
//     2100 mm ceilings. The 5 footprint rays came back [920058, 920058, 920087] with 2 misses ->
//     OVERHANGING. A single centre ray would have answered "ceiling 920058", confidently and wrongly.
//     That is the whole case for this fragment, demonstrated rather than argued.
//   Test terminal was restored to its original position afterwards.
// GOTCHA CONFIRMED THE HARD WAY: do NOT reason about which surface is above a point from BOUNDING BOXES.
//   Two of these ceilings have overlapping boxes but non-overlapping real shapes, which made a first
//   attempt at this test produce a false "OK". Probe with a ray; never infer from extents.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string direction = "up";            // "up" | "down" | "+X" | "-X" | "+Y" | "-Y"
string targetCategoryName = "Ceilings";  // what it should be sitting against THIS time; null = anything
string sampleMode = "footprint";    // "footprint" (centre + 4 corners) | "grid9" (3x3) | "centre" (1 ray)
double maxRayDistanceMm = 5000;
double heightToleranceMm = 2;       // corner hits within this of each other count as level
double slopeToleranceDeg = 2;       // hit face within this of perpendicular counts as flat
double startOffsetMm = 10;          // start each ray this far along it, to clear the element's own body
int maxProblemsListed = 25;         // detail cap — the summary always covers every element
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

XYZ dir = direction == "up" ? XYZ.BasisZ : direction == "down" ? -XYZ.BasisZ
        : direction == "+X" ? XYZ.BasisX : direction == "-X" ? -XYZ.BasisX
        : direction == "+Y" ? XYZ.BasisY : direction == "-Y" ? -XYZ.BasisY : null;

if (dir == null)
{
    sb.AppendLine($"Unknown direction '{direction}' — use up, down, +X, -X, +Y or -Y.");
}
else
{
    View3D rayView = Document.ActiveView as View3D;
    if (rayView == null)
        rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);

    if (rayView == null)
    {
        sb.AppendLine("No usable 3D view to ray-cast in — Revit requires one (creators/create-view.cs, viewKind=\"three_d\").");
    }
    else
    {
        ReferenceIntersector intersector = null;
        bool ready = true;
        if (!string.IsNullOrEmpty(targetCategoryName))
        {
            Category cat = null;
            foreach (Category c in Document.Settings.Categories) if (c.Name == targetCategoryName) { cat = c; break; }
            if (cat == null) { sb.AppendLine($"Target category '{targetCategoryName}' not found — see context/context-model-categories.cs."); ready = false; }
            else intersector = new ReferenceIntersector(new ElementCategoryFilter(cat.Id), FindReferenceTarget.Face, rayView);
        }
        else { intersector = new ReferenceIntersector(rayView); intersector.TargetType = FindReferenceTarget.Face; }

        if (ready)
        {
            double maxDistFeet = mm(maxRayDistanceMm);
            double startOff = mm(startOffsetMm);
            double heightTol = mm(heightToleranceMm);
            double slopeTolRad = slopeToleranceDeg * Math.PI / 180.0;

            int ok = 0, straddling = 0, overhanging = 0, sloped = 0, uneven = 0, noHit = 0, noPoint = 0;
            var problems = new List<string>();

            foreach (var el in elements)
            {
                var lp = el.Location as LocationPoint;
                if (lp == null) { noPoint++; continue; }
                var bb = el.get_BoundingBox(null);
                if (bb == null) { noPoint++; continue; }

                // sample points across the element's footprint, on the plane through its insertion point
                var samples = new List<XYZ>();
                var p = lp.Point;
                if (sampleMode == "centre") samples.Add(p);
                else if (sampleMode == "grid9")
                {
                    for (int i = 0; i <= 2; i++)
                        for (int j = 0; j <= 2; j++)
                            samples.Add(new XYZ(bb.Min.X + (bb.Max.X - bb.Min.X) * i / 2.0,
                                                bb.Min.Y + (bb.Max.Y - bb.Min.Y) * j / 2.0, p.Z));
                }
                else // footprint: centre + 4 plan corners
                {
                    samples.Add(p);
                    samples.Add(new XYZ(bb.Min.X, bb.Min.Y, p.Z));
                    samples.Add(new XYZ(bb.Max.X, bb.Min.Y, p.Z));
                    samples.Add(new XYZ(bb.Min.X, bb.Max.Y, p.Z));
                    samples.Add(new XYZ(bb.Max.X, bb.Max.Y, p.Z));
                }

                var hitIds = new List<ElementId>();
                var hitDists = new List<double>();
                double worstSlopeRad = 0;
                int misses = 0;

                foreach (var s in samples)
                {
                    var start = s + dir * startOff;
                    ReferenceWithContext h = null;
                    try
                    {
                        var all = intersector.Find(start, dir);
                        if (all != null)
                            h = all.Where(x => x.GetReference().ElementId != el.Id)
                                   .Where(x => x.Proximity <= maxDistFeet)
                                   .OrderBy(x => x.Proximity).FirstOrDefault();
                    }
                    catch { }

                    if (h == null) { misses++; continue; }
                    hitIds.Add(h.GetReference().ElementId);
                    hitDists.Add(h.Proximity + startOff);

                    // how far off perpendicular is the surface we hit?
                    try
                    {
                        var face = Document.GetElement(h.GetReference()).GetGeometryObjectFromReference(h.GetReference()) as Face;
                        if (face != null)
                        {
                            // A surface square to the ray has a normal either along it or exactly against
                            // it (a ceiling's underside faces down; the ray goes up). So "off flat" is the
                            // smaller of the angle and its supplement — 0 means perfectly square.
                            var n = face.ComputeNormal(new UV(0.5, 0.5)).Normalize();
                            double ang = n.AngleTo(dir);
                            double fromFlat = Math.Min(ang, Math.PI - ang);
                            if (fromFlat > worstSlopeRad) worstSlopeRad = fromFlat;
                        }
                    }
                    catch { }
                }

                string verdict = null;
                if (hitIds.Count == 0) { noHit++; verdict = null; }
                else if (misses > 0) { overhanging++; verdict = $"OVERHANGING — {misses} of {samples.Count} sample(s) found nothing"; }
                else if (hitIds.Distinct().Count() > 1) { straddling++; verdict = $"STRADDLING — samples hit {hitIds.Distinct().Count()} different elements: {string.Join(", ", hitIds.Distinct())}"; }
                else if ((hitDists.Max() - hitDists.Min()) > heightTol) { uneven++; verdict = $"UNEVEN — same element but {toMm(hitDists.Max() - hitDists.Min()):F0} mm difference across the footprint"; }
                else if (worstSlopeRad > slopeTolRad) { sloped++; verdict = $"SLOPED — hit face is {worstSlopeRad * 180 / Math.PI:F1}° off perpendicular, cannot sit flush"; }
                else ok++;

                if (verdict != null && problems.Count < maxProblemsListed)
                    problems.Add($"  - '{el.Name}' (Id {el.Id}) at ({toMm(p.X):F0}, {toMm(p.Y):F0}): {verdict}");
            }

            int checkedCount = elements.Count - noPoint;
            sb.AppendLine($"Surface-fit check — ray '{direction}' -> {targetCategoryName ?? "nearest anything"}, {sampleMode} sampling, {checkedCount} element(s) checked in view '{rayView.Name}':");
            sb.AppendLine($"  OK (flush, level, one surface) : {ok}");
            if (straddling > 0)  sb.AppendLine($"  STRADDLING two+ surfaces      : {straddling}");
            if (overhanging > 0) sb.AppendLine($"  OVERHANGING an edge           : {overhanging}");
            if (uneven > 0)      sb.AppendLine($"  UNEVEN across footprint       : {uneven}");
            if (sloped > 0)      sb.AppendLine($"  SLOPED surface                : {sloped}");
            if (noHit > 0)       sb.AppendLine($"  NOTHING found within {maxRayDistanceMm} mm : {noHit}");
            if (noPoint > 0)     sb.AppendLine($"  skipped, no insertion point   : {noPoint}");

            if (problems.Count > 0)
            {
                sb.AppendLine($"Needs a look ({Math.Min(problems.Count, maxProblemsListed)} shown):");
                foreach (var line in problems) sb.AppendLine(line);
                int totalProblems = straddling + overhanging + uneven + sloped;
                if (totalProblems > maxProblemsListed)
                    sb.AppendLine($"  ... +{totalProblems - maxProblemsListed} more not listed (raise maxProblemsListed to see them — NOT silently dropped)");
            }
            else if (ok == checkedCount && checkedCount > 0)
            {
                sb.AppendLine("Every element sits flush on a single level surface — safe to auto-move.");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
