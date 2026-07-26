// ============================================================
// FRAGMENT (action) — action-move-to-ray-hit.cs
// PURPOSE: Fire ONE ray out of each element in `elements` and move that element to whatever the ray hits,
//          plus an optional offset. The general "snap this to the thing above/below/beside it" job —
//          air terminal up to the SLAB, or to the ceiling, sprinkler up to the soffit, hanger down to the
//          floor, bracket sideways to the wall. **The target is named per request** (`targetCategoryName`),
//          never baked in — that was the whole point of generalising this (user, 2026-07-26).
//          To LOOK before moving, run `../reporting/action-report-ray-hits.cs` first — same ray, no change.
// ASSUMES: elements (List<Element>, point-based) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: DRY RUN BY DEFAULT. This moves real geometry in bulk — show the user the distances first, get a
//         go-ahead, then rerun with dryRun=false. Same explorer-first rule as every bulk fragment here.
// GOTCHA: ray-casting needs a real View3D (Revit's rule) — active view if it's 3D, else the first
//         unlocked non-template 3D view. With none, it reports and stops.
// GOTCHA: the ray starts INSIDE the source element so it can hit itself — self-hits are dropped.
// GOTCHA: the element moves so its INSERTION POINT lands on the hit point. A family whose insertion point
//         isn't its top face (most air terminals) will look half-buried in the slab — that's what
//         `offsetMm` is for: negative pulls it back along the ray. Check one element, then run the batch.
// GOTCHA: replaces the narrow `recipes/ray-trace-to-ceiling.cs` for everything except its exact
//         ceiling-only case; that recipe stays as the named shortcut.
// NOTE: this fragment already uses Find-all → drop-self → take-nearest, which is the CORRECT pattern.
//       Its sibling `../reporting/action-report-ray-hits.cs` was first written with FindNearest and
//       silently reported "nothing found" because the only hit was the element's own face (caught live
//       2026-07-26). Never use FindNearest when the ray starts inside the source element.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — moved all 17 air terminals from Z=0 up to their ceilings in
//   one pass, direction "up", target "Ceilings", offset 0. The real proof was that the three ceilings sat
//   at DIFFERENT heights (2100 / 2400 / 3000 mm) and every terminal found the one above ITSELF: 6, 6 and 5
//   respectively, 0 misses. A fixed-Z approach would have buried 11 of them in the wrong slab. Dry run was
//   run first and matched the result exactly; read-back confirmed each Z equals its ceiling's underside.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report the moves only; false = actually move
string direction = "up";             // "up" | "down" | "+X" | "-X" | "+Y" | "-Y"
string targetCategoryName = null;    // WHAT to snap to this time — "Floors", "Ceilings", "Walls", ...; null = nearest anything
double offsetMm = 0;                 // signed, along the ray: negative pulls back toward the element
double maxRayDistanceMm = 5000;      // ignore hits farther than this
// ---- END INPUTS ----

Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
double maxDistFeet = mm(maxRayDistanceMm);

XYZ dir = null;
if (direction == "up") dir = XYZ.BasisZ;
else if (direction == "down") dir = -XYZ.BasisZ;
else if (direction == "+X") dir = XYZ.BasisX;
else if (direction == "-X") dir = -XYZ.BasisX;
else if (direction == "+Y") dir = XYZ.BasisY;
else if (direction == "-Y") dir = -XYZ.BasisY;

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
        sb.AppendLine("No usable 3D view to ray-cast in — Revit requires one. Create a 3D view (creators/create-view.cs, viewKind=\"three_d\") and rerun.");
    }
    else
    {
        ReferenceIntersector intersector = null;
        bool ready = true;
        if (!string.IsNullOrEmpty(targetCategoryName))
        {
            Category cat = null;
            foreach (Category c in Document.Settings.Categories) if (c.Name == targetCategoryName) { cat = c; break; }
            if (cat == null) { sb.AppendLine($"Target category '{targetCategoryName}' not found — list them with context/context-model-categories.cs."); ready = false; }
            else intersector = new ReferenceIntersector(new ElementCategoryFilter(cat.Id), FindReferenceTarget.Face, rayView);
        }
        else
        {
            intersector = new ReferenceIntersector(rayView);
            intersector.TargetType = FindReferenceTarget.Face;
        }

        if (ready)
        {
            // work out every move first, so a dry run and a real run report identically
            var plan = new List<Tuple<Element, XYZ, double, Element>>(); // element, translation, distance(ft), hit element
            int noPoint = 0, noHit = 0;

            foreach (var el in elements)
            {
                var lp = el.Location as LocationPoint;
                if (lp == null) { noPoint++; continue; }
                var origin = lp.Point;

                ReferenceWithContext hit = null;
                try
                {
                    var all = intersector.Find(origin, dir);
                    if (all != null)
                        hit = all.Where(h => h.GetReference().ElementId != el.Id)
                                 .Where(h => h.Proximity <= maxDistFeet)
                                 .OrderBy(h => h.Proximity).FirstOrDefault();
                }
                catch (Exception exRay) { sb.AppendLine($"  Id {el.Id.IntegerValue}: ray failed — {exRay.Message}"); continue; }

                if (hit == null) { noHit++; continue; }

                var hitPoint = hit.GetReference().GlobalPoint;
                var target = hitPoint + dir * mm(offsetMm);
                plan.Add(Tuple.Create(el, target - origin, hit.Proximity, Document.GetElement(hit.GetReference().ElementId)));
            }

            sb.AppendLine($"Ray '{direction}'{(targetCategoryName != null ? $" -> {targetCategoryName}" : " -> nearest anything")}, offset {offsetMm} mm, in 3D view '{rayView.Name}':");
            sb.AppendLine($"  {plan.Count} element(s) would move, {noHit} found nothing within {maxRayDistanceMm} mm, {noPoint} had no insertion point (of {elements.Count}).");
            foreach (var p in plan.Take(20))
                sb.AppendLine($"  - '{p.Item1.Name}' (Id {p.Item1.Id.IntegerValue}) moves {toMm(p.Item2.GetLength()):F0} mm to hit '{p.Item4?.Name ?? "?"}' (Id {p.Item4?.Id.IntegerValue}, {p.Item4?.Category?.Name ?? "?"}) at {toMm(p.Item3):F0} mm");
            if (plan.Count > 20) sb.AppendLine($"  ... +{plan.Count - 20} more");

            if (dryRun)
            {
                sb.AppendLine("DRY RUN — nothing moved. Check one distance against the model, then rerun with dryRun=false.");
            }
            else if (plan.Count == 0)
            {
                sb.AppendLine("Nothing to move.");
            }
            else
            {
                using (var t = new Transaction(Document, "AJ Tools - Move To Ray Hit"))
                {
                    t.Start();
                    try
                    {
                        int moved = 0;
                        foreach (var p in plan)
                        {
                            ElementTransformUtils.MoveElement(Document, p.Item1.Id, p.Item2);
                            moved++;
                        }
                        t.Commit();
                        sb.AppendLine($"MOVED {moved} element(s). Verify with a fresh read-back — ../reporting/action-report-location.cs, or rerun the probe.");
                    }
                    catch (Exception ex)
                    {
                        try { t.RollBack(); } catch { }
                        sb.AppendLine($"FAILED mid-move — rolled back, NOTHING moved. Reason: {ex.Message}");
                    }
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
