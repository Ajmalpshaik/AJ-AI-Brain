// ============================================================
// FRAGMENT (action) — action-report-ray-hits.cs
// PURPOSE: Fire rays out of each element in `elements` and report WHAT EACH RAY HITS — direction, the hit
//          element (name/Id/category), and the distance in mm. The general "what is around this thing"
//          probe: above, below, sideways, plan diagonals, or all 26 directions at once (every axis, edge
//          and corner of a cube). Read-only — this is the LOOK step before any move.
//          The user's own idea (2026-07-26), generalising the up-only, ceiling-only
//          `recipes/ray-trace-to-ceiling.cs`. To actually MOVE to a hit, use
//          `action-move-to-ray-hit.cs`.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above. Elements need a
//          LocationPoint, or a bounding box (the fragment falls back to its centre).
// NOT STANDALONE — see scripts/README.md for how to compose. Model never changes.
// GOTCHA: ray-casting needs a real View3D to run in (Revit's rule, not ours) — uses the active view if
//         it's already 3D, else the first unlocked non-template 3D view. With none, it reports and stops.
// GOTCHA: the ray starts INSIDE the source element, so it can hit the element itself — self-hits are
//         dropped. Hits on the same element are never reported.
// GOTCHA: `Proximity` is measured from the ray origin (the element's insertion point), NOT from its
//         outer face — so a 600mm-deep diffuser reports ~300mm less clearance than you'd measure on site.
//         Read it as "distance from the element's centre point".
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 8 rays x 2 walls returned 11 correct hits. A real bug was
//   caught that run: the first version used FindNearest, whose single result is the source element's own
//   face, so dropping the self-hit left "nothing found" (1 hit instead of 11). Fixed to Find-all →
//   drop-self → take-nearest. Geometry cross-checked: the plan-diagonal hits came back at 8910 mm where
//   the axis hits were 6300 mm, and 6300 x root-2 = 8910. The maths is right.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string directionSet = "vertical";   // "up" | "down" | "vertical" | "horizontal" | "plan8" | "axes6" | "all26"
string targetCategoryName = null;   // e.g. "Ceilings", "Walls", "Floors"; null = hit ANYTHING
double maxRayDistanceMm = 5000;     // ignore hits farther than this
bool nearestOnly = true;            // true = first hit per ray; false = every hit along the ray
int maxElementsReported = 25;       // detail cap; the summary always covers the whole set
// ---- END INPUTS ----

Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
double maxDistFeet = UnitUtils.ConvertToInternalUnits(maxRayDistanceMm, DisplayUnitType.DUT_MILLIMETERS);

// --- build the direction list ---
var dirs = new List<KeyValuePair<string, XYZ>>();
Action<string, double, double, double> add = (n, x, y, z) =>
    dirs.Add(new KeyValuePair<string, XYZ>(n, new XYZ(x, y, z).Normalize()));

if (directionSet == "up") { add("up", 0, 0, 1); }
else if (directionSet == "down") { add("down", 0, 0, -1); }
else if (directionSet == "vertical") { add("up", 0, 0, 1); add("down", 0, 0, -1); }
else if (directionSet == "horizontal") { add("+X", 1, 0, 0); add("-X", -1, 0, 0); add("+Y", 0, 1, 0); add("-Y", 0, -1, 0); }
else if (directionSet == "axes6") { add("up", 0, 0, 1); add("down", 0, 0, -1); add("+X", 1, 0, 0); add("-X", -1, 0, 0); add("+Y", 0, 1, 0); add("-Y", 0, -1, 0); }
else if (directionSet == "plan8")
{
    add("+X", 1, 0, 0); add("-X", -1, 0, 0); add("+Y", 0, 1, 0); add("-Y", 0, -1, 0);
    add("+X+Y", 1, 1, 0); add("+X-Y", 1, -1, 0); add("-X+Y", -1, 1, 0); add("-X-Y", -1, -1, 0);
}
else if (directionSet == "all26")
{
    // every axis, edge and corner of a cube — 3^3 combinations minus the zero vector
    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                string n = $"{(x > 0 ? "+X" : x < 0 ? "-X" : "")}{(y > 0 ? "+Y" : y < 0 ? "-Y" : "")}{(z > 0 ? "up" : z < 0 ? "down" : "")}";
                add(n, x, y, z);
            }
}

if (dirs.Count == 0)
{
    sb.AppendLine($"Unknown directionSet '{directionSet}' — use up, down, vertical, horizontal, plan8, axes6 or all26.");
}
else
{
    View3D rayView = Document.ActiveView as View3D;
    if (rayView == null)
        rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);

    if (rayView == null)
    {
        sb.AppendLine("No usable 3D view to ray-cast in — Revit requires one. Create a 3D view (creators/create-view.cs, viewKind=\"three_d\") and rerun.");
    }
    else
    {
        ReferenceIntersector intersector;
        if (!string.IsNullOrEmpty(targetCategoryName))
        {
            Category cat = null;
            foreach (Category c in Document.Settings.Categories) if (c.Name == targetCategoryName) { cat = c; break; }
            if (cat == null)
            {
                sb.AppendLine($"Category '{targetCategoryName}' not found — list them with context/context-model-categories.cs.");
                intersector = null;
            }
            else intersector = new ReferenceIntersector(new ElementCategoryFilter(cat.Id), FindReferenceTarget.Face, rayView);
        }
        else
        {
            intersector = new ReferenceIntersector(rayView);
            intersector.TargetType = FindReferenceTarget.Face;
        }

        if (intersector != null)
        {
            int reported = 0, noPoint = 0, totalHits = 0;
            sb.AppendLine($"Ray probe — {dirs.Count} direction(s) [{directionSet}] from {elements.Count} element(s), in 3D view '{rayView.Name}', up to {maxRayDistanceMm} mm{(targetCategoryName != null ? $", looking only for {targetCategoryName}" : ", hitting anything")}:");

            foreach (var el in elements)
            {
                XYZ origin = null;
                var lp = el.Location as LocationPoint;
                if (lp != null) origin = lp.Point;
                else { var bb = el.get_BoundingBox(null); if (bb != null) origin = (bb.Min + bb.Max) / 2.0; }
                if (origin == null) { noPoint++; continue; }

                if (reported >= maxElementsReported) continue;
                sb.AppendLine($"- '{el.Name}' (Id {el.Id.IntegerValue}) from ({toMm(origin.X):F0}, {toMm(origin.Y):F0}, {toMm(origin.Z):F0}) mm:");

                foreach (var d in dirs)
                {
                    // ALWAYS Find() (every hit), never FindNearest() — the ray starts inside the source
                    // element, so FindNearest returns the element's own face; dropping that self-hit would
                    // then leave nothing and wrongly report "clear". Found live 2026-07-26: a wall reported
                    // nothing in all 8 directions with real walls 6 m away. Collect all, drop self, THEN
                    // take the nearest.
                    var found = new List<ReferenceWithContext>();
                    try
                    {
                        var many = intersector.Find(origin, d.Value);
                        if (many != null) found.AddRange(many);
                    }
                    catch (Exception exRay) { sb.AppendLine($"    {d.Key,-8} ray failed: {exRay.Message}"); continue; }

                    // drop self-hits and anything past the limit
                    var real = found
                        .Where(h => h.GetReference().ElementId != el.Id)
                        .Where(h => h.Proximity <= maxDistFeet)
                        .OrderBy(h => h.Proximity)
                        .ToList();

                    if (real.Count == 0) { sb.AppendLine($"    {d.Key,-8} nothing within {maxRayDistanceMm} mm"); continue; }

                    foreach (var h in real.Take(nearestOnly ? 1 : 5))
                    {
                        var hitEl = Document.GetElement(h.GetReference().ElementId);
                        var gp = h.GetReference().GlobalPoint;
                        sb.AppendLine($"    {d.Key,-8} {toMm(h.Proximity):F0} mm -> '{hitEl?.Name ?? "?"}' (Id {h.GetReference().ElementId.IntegerValue}, {hitEl?.Category?.Name ?? "?"}) at ({toMm(gp.X):F0}, {toMm(gp.Y):F0}, {toMm(gp.Z):F0})");
                        totalHits++;
                    }
                }
                reported++;
            }

            sb.AppendLine($"Probe done: {totalHits} hit(s) across {Math.Min(reported, maxElementsReported)} element(s) shown{(elements.Count > maxElementsReported ? $" (of {elements.Count}; detail capped at {maxElementsReported})" : "")}{(noPoint > 0 ? $"; {noPoint} element(s) had no usable point" : "")}.");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
