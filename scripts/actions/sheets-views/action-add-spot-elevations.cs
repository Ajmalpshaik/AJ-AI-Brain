// ============================================================
// FRAGMENT (action) — action-add-spot-elevations.cs
// PURPOSE: Place a Spot Elevation annotation on each element in `elements` in one view — the "annotate
//          the levels of these" job, same annotation family as action-tag-elements.cs but reporting
//          height instead of identity.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: NewSpotElevation needs a geometry REFERENCE, not just a point — this fragment digs one out of
//         each element's solid geometry (ComputeReferences=true, first usable planar face). Elements whose
//         geometry yields no usable reference are skipped and REPORTED, not errors.
// FLAGGED 2026-07-26 (static review): reference-from-geometry is the fragile part — some categories only
//         accept specific reference kinds for spot elevations. If placements fail broadly in one category,
//         that's this, not the transaction logic.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;              // the view the spot elevations are placed in
double leaderOffsetXMm = 300;   // leader bend/end offset from the measured point
double leaderOffsetYMm = 300;
bool hasLeader = true;
// ---- END INPUTS ----

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var spotView = Document.GetElement(new ElementId(viewIdInt)) as View;
if (spotView == null || spotView.IsTemplate)
{
    sb.AppendLine($"viewIdInt {viewIdInt} is not a usable view.");
}
else
{
    int placed = 0, skipped = 0;
    var notes = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Add Spot Elevations"))
    {
        t.Start();
        try
        {
            var geoOpts = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };

            foreach (var el in elements)
            {
                Reference faceRef = null;
                XYZ measurePt = null;
                try
                {
                    var geo = el.get_Geometry(geoOpts);
                    if (geo != null)
                    {
                        foreach (GeometryObject obj in geo)
                        {
                            var solid = obj as Solid;
                            var inst = obj as GeometryInstance;
                            if (solid == null && inst != null)
                                solid = inst.GetInstanceGeometry().OfType<Solid>().FirstOrDefault(s => s.Faces.Size > 0);
                            if (solid == null || solid.Faces.Size == 0) continue;
                            foreach (Face f in solid.Faces)
                            {
                                if (f.Reference == null) continue;
                                var pf = f as PlanarFace;
                                if (pf == null) continue;
                                faceRef = f.Reference;
                                // measure at the face's centroid-ish point via its bounding UV middle
                                var bb = f.GetBoundingBox();
                                var mid = (bb.Min + bb.Max) / 2.0;
                                measurePt = f.Evaluate(mid);
                                break;
                            }
                            if (faceRef != null) break;
                        }
                    }
                }
                catch { }

                if (faceRef == null || measurePt == null)
                {
                    skipped++;
                    notes.Add($"Id {el.Id.IntegerValue}: no usable face reference");
                    continue;
                }

                try
                {
                    var bend = measurePt + new XYZ(mm(leaderOffsetXMm), mm(leaderOffsetYMm), 0);
                    var end = bend + new XYZ(mm(leaderOffsetXMm), 0, 0);
                    Document.Create.NewSpotElevation(spotView, faceRef, measurePt, bend, end, measurePt, hasLeader);
                    placed++;
                }
                catch (Exception exOne)
                {
                    skipped++;
                    notes.Add($"Id {el.Id.IntegerValue}: {exOne.Message}");
                }
            }
            t.Commit();
            sb.AppendLine($"Spot elevations: {placed} placed, {skipped} skipped, of {elements.Count} element(s) in view '{spotView.Name}'.");
            if (notes.Count > 0) sb.AppendLine("  " + string.Join("; ", notes.Take(20)) + (notes.Count > 20 ? $"; ... +{notes.Count - 20} more" : ""));
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED mid-placement — rolled back, nothing placed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
