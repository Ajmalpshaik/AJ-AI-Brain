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
// **THE BIGGEST TRAP IN THIS FILE — RAYS ONLY SEE WHAT THE 3D VIEW SHOWS.** `ReferenceIntersector` runs
//         inside a View3D and respects that view's visibility completely: hidden categories, section
//         boxes, view filters, closed worksets. A hidden category is INVISIBLE TO A RAY, so the probe
//         reports "clear" with a wall standing right there — silently, with no error.
//         Proven live 2026-07-26: the same element, same code, same direction —
//           view '{3D}'        (Walls category hidden) -> 0 neighbours
//           view '3D Plumbing' (Walls visible)         -> 4 neighbours
//         The fragment therefore WARNS when the target category is hidden in the view it picked. If a ray
//         result looks impossibly empty, check the view before doubting the geometry.
// GOTCHA: **ONE RAY PER DIRECTION MISSES THINGS ON ANYTHING PHYSICALLY LARGE.** A single ray from the
//         insertion point only sees what is directly in line with the element's CENTRE — a pipe passing
//         the corner of an AHU is invisible to it. Verified live 2026-07-26 on a 1980 mm-wide element in
//         a view where the walls were visible: centre = 4 rays from 1 point found 4 neighbours;
//         sampleMode="fan" = 36 rays from 24 distinct start points found 5. For a 600 mm diffuser
//         "centre" is fine; for equipment a metre or more across, use "fan" or the answer to "what is
//         around this unit" is quietly incomplete. Same supersampling idea as
//         ../qa-checks/action-check-surface-fit.cs.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 8 rays x 2 walls returned 11 correct hits. A real bug was
//   caught that run: the first version used FindNearest, whose single result is the source element's own
//   face, so dropping the self-hit left "nothing found" (1 hit instead of 11). Fixed to Find-all →
//   drop-self → take-nearest. Geometry cross-checked: the plan-diagonal hits came back at 8910 mm where
//   the axis hits were 6300 mm, and 6300 x root-2 = 8910. The maths is right.
// ✱ UPGRADED 2026-08-22 — RAYS NOW SEE INTO LINKED MODELS (`FindReferencesInRevitLinks = true`).
//   Before this they could only hit elements in THIS document, so on a normal project — where the
//   architecture, and therefore the CEILINGS and SLABS, live in a linked model — every ray found
//   nothing and the fragment reported "no hit". That reads as the tool being broken when the model is
//   simply arranged the usual way. Harvested from the add-in's Game Mode collision service, whose note
//   says it plainly: "architecture usually lives in a linked model".
// GOTCHA: A LINKED HIT'S `Reference.ElementId` IS THE `RevitLinkInstance`, NOT WHAT YOU HIT. The thing
//         you actually hit is `Reference.LinkedElementId`, and it must be fetched from the LINK's own
//         document via `RevitLinkInstance.GetLinkDocument()`. Resolve it the lazy way and the report
//         names the RVT file instead of the ceiling.
// GOTCHA: set `includeLinkedModels = false` to get exactly the behaviour that was live-verified in
//         July — that verification was done on a model with no links, so it neither proves nor
//         disproves the linked path. The linked path is compile-checked only.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string directionSet = "vertical";   // "up" | "down" | "vertical" | "horizontal" | "plan8" | "axes6" | "all26"
string targetCategoryName = null;   // e.g. "Ceilings", "Walls", "Floors"; null = hit ANYTHING
double maxRayDistanceMm = 5000;     // ignore hits farther than this
bool nearestOnly = true;            // true = first hit per ray; false = every hit along the ray
string sampleMode = "centre";       // "centre" = 1 ray per direction from the insertion point
                                    // "fan"    = 3x3 rays spread across each FACE — see the header;
                                    //            essential for anything physically large (AHU, FCU)
int maxElementsReported = 25;       // detail cap; the summary always covers the whole set
bool includeLinkedModels = true;    // rays see into LINKED models too — architecture usually IS linked
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
double maxDistFeet = maxRayDistanceMm / 304.8;

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
            else
            {
                // a hidden category is invisible to a ray — warn rather than report a false "clear"
                try {
                    if (rayView.GetCategoryHidden(cat.Id))
                        sb.AppendLine($"*** WARNING: category '{targetCategoryName}' is HIDDEN in view '{rayView.Name}'. Rays cannot see it — every result below will be empty. Use a 3D view where it is visible.");
                } catch { }
                intersector = new ReferenceIntersector(new ElementCategoryFilter(cat.Id), FindReferenceTarget.Face, rayView);
                if (includeLinkedModels) intersector.FindReferencesInRevitLinks = true;
            }
        }
        else
        {
            intersector = new ReferenceIntersector(rayView);
            intersector.TargetType = FindReferenceTarget.Face;
            if (includeLinkedModels) intersector.FindReferencesInRevitLinks = true;
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
                sb.AppendLine($"- '{el.Name}' (Id {el.Id}) from ({toMm(origin.X):F0}, {toMm(origin.Y):F0}, {toMm(origin.Z):F0}) mm:");

                var elBox = el.get_BoundingBox(null);

                foreach (var d in dirs)
                {
                    // where the rays start for this direction: one from the centre, or a 3x3 fan spread
                    // across the face the ray leaves through. The fan is what catches a neighbour sitting
                    // beside the element rather than dead in front of its middle.
                    var starts = new List<XYZ> { origin };
                    if (sampleMode == "fan" && elBox != null)
                    {
                        // sit the 9 start points ON the face the ray leaves through, spread across the
                        // element's other two dimensions. Built in world coordinates directly — an
                        // earlier vector-offset version of this was unreadable and wrong.
                        starts.Clear();
                        var dv2 = d.Value;
                        bool alongX = Math.Abs(dv2.X) > 0.5, alongY = Math.Abs(dv2.Y) > 0.5;
                        for (int i = 0; i <= 2; i++)
                            for (int j = 0; j <= 2; j++)
                            {
                                double fi = i / 2.0, fj = j / 2.0;
                                double x, y, z;
                                if (alongX)
                                {
                                    x = dv2.X > 0 ? elBox.Max.X : elBox.Min.X;
                                    y = elBox.Min.Y + (elBox.Max.Y - elBox.Min.Y) * fi;
                                    z = elBox.Min.Z + (elBox.Max.Z - elBox.Min.Z) * fj;
                                }
                                else if (alongY)
                                {
                                    y = dv2.Y > 0 ? elBox.Max.Y : elBox.Min.Y;
                                    x = elBox.Min.X + (elBox.Max.X - elBox.Min.X) * fi;
                                    z = elBox.Min.Z + (elBox.Max.Z - elBox.Min.Z) * fj;
                                }
                                else   // up/down, and the diagonal cases fall here too
                                {
                                    z = dv2.Z > 0 ? elBox.Max.Z : elBox.Min.Z;
                                    x = elBox.Min.X + (elBox.Max.X - elBox.Min.X) * fi;
                                    y = elBox.Min.Y + (elBox.Max.Y - elBox.Min.Y) * fj;
                                }
                                starts.Add(new XYZ(x, y, z));
                            }
                    }

                    // ALWAYS Find() (every hit), never FindNearest() — the ray starts inside the source
                    // element, so FindNearest returns the element's own face; dropping that self-hit would
                    // then leave nothing and wrongly report "clear". Found live 2026-07-26: a wall reported
                    // nothing in all 8 directions with real walls 6 m away. Collect all, drop self, THEN
                    // take the nearest.
                    var found = new List<ReferenceWithContext>();
                    try
                    {
                        foreach (var s in starts)
                        {
                            var many = intersector.Find(s, d.Value);
                            if (many != null) found.AddRange(many);
                        }
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
                        // A LINKED hit's ElementId is the RevitLinkInstance, not the thing you hit —
                        // resolve through the link or the report names the RVT file, not the ceiling.
                        var href = h.GetReference();
                        Element hitEl = null; string hitFrom = "";
                        if (href.LinkedElementId != ElementId.InvalidElementId)
                        {
                            var li = Document.GetElement(href.ElementId) as RevitLinkInstance;
                            var ldoc = li != null ? li.GetLinkDocument() : null;
                            if (ldoc != null) { hitEl = ldoc.GetElement(href.LinkedElementId); hitFrom = "  [link: " + li.Name + "]"; }
                        }
                        if (hitEl == null) hitEl = Document.GetElement(href.ElementId);
                        var gp = href.GlobalPoint;
                        sb.AppendLine($"    {d.Key,-8} {toMm(h.Proximity):F0} mm -> '{hitEl?.Name ?? "?"}' (Id {h.GetReference().ElementId}, {hitEl?.Category?.Name ?? "?"}) at ({toMm(gp.X):F0}, {toMm(gp.Y):F0}, {toMm(gp.Z):F0}){hitFrom}");
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
