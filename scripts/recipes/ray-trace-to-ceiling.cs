// ============================================================
// RECIPE — ray-trace-to-ceiling.cs
// PURPOSE: Snap each element in `elements` (e.g. a diffuser/air terminal) to the nearest ceiling
//          directly above it — casts a ray straight up from the element's current point using Revit's
//          real ray-casting API, finds where it first hits a Ceiling face, and moves the element up/down
//          (X/Y unchanged) to that exact hit height. This is the user's own idea (2026-07-14): "send a ray
//          from [the diffuser] above, it hits the ceiling, move the diffuser to that hitting point."
// ASSUMES: elements (List<Element>) are point-based (LocationPoint) — typically air terminals from a
//          filter fragment (e.g. filter-by-category.cs with OST_DuctTerminal). sb (StringBuilder) exists.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: ReferenceIntersector needs a real View3D to run in. Uses the active view if it's already 3D;
//          otherwise falls back to the first unlocked, non-template 3D view in the project.
// SUPERSEDED FOR EVERYTHING BUT ITS EXACT CASE (2026-07-26): this is the ceiling-only, up-only shortcut.
// For any other target — snap to the SLAB, a wall, a beam, or whatever is simply nearest — use
// ../actions/move-copy-rotate/action-move-to-ray-hit.cs, where the target category and direction are
// per-request inputs. To LOOK before moving, ../actions/reporting/action-report-ray-hits.cs fires rays in
// up to 26 directions and just reports. Both came from generalising this recipe at the user's request.
// GOTCHA (carried back from that work): this recipe uses FindNearest, which is only safe HERE because the
// filter is ceilings-only, so the source element can never be its own first hit. Do not copy the
// FindNearest pattern into an unfiltered ray — it silently reports "nothing found".
// NOT YET LIVE-VERIFIED FOR THE POSITIVE CASE: this project's live model currently has 0 Ceiling
// elements, so the "actually finds and snaps to a real ceiling" path is unverified — confirmed only that
// the "no ceiling found above" path runs cleanly with no exception on 720 real duct terminals. Re-verify
// against a real ceiling the first time one exists in the model.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double maxRayDistanceMm = 5000; // give up if the nearest ceiling hit is farther than this (mm)
// ---- END INPUTS ----

View3D rayView = Document.ActiveView as View3D;
if (rayView == null)
{
    rayView = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
        .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked);
}

if (rayView == null)
{
    sb.AppendLine("No usable 3D view found to ray-cast in — nothing changed.");
}
else
{
    var ceilingFilter = new ElementCategoryFilter(BuiltInCategory.OST_Ceilings);
    var intersector = new ReferenceIntersector(ceilingFilter, FindReferenceTarget.Face, rayView);
    double maxDistFeet = UnitUtils.ConvertToInternalUnits(maxRayDistanceMm, DisplayUnitType.DUT_MILLIMETERS);

    int snapped = 0, noHit = 0, skipped = 0;

    using (var t = new Transaction(Document, "AJ Tools - Ray-Trace Snap To Ceiling"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                if (!(e.Location is LocationPoint lp)) { skipped++; continue; }

                var origin = lp.Point;
                var hit = intersector.FindNearest(origin, XYZ.BasisZ);
                if (hit == null || hit.Proximity > maxDistFeet) { noHit++; continue; }

                var hitPoint = hit.GetReference().GlobalPoint;
                var translation = new XYZ(0, 0, hitPoint.Z - origin.Z);
                ElementTransformUtils.MoveElement(Document, e.Id, translation);
                snapped++;
            }
            t.Commit();
            sb.AppendLine($"Ray-traced {elements.Count} element(s) in view '{rayView.Name}': snapped {snapped}, no ceiling found above {noHit}, skipped {skipped} (not point-based).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to ray-trace/snap — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
