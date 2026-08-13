// ============================================================
// FRAGMENT (action) — action-add-spot-elevations.cs
// PURPOSE: Place a Spot Elevation annotation on each element in `elements` in one view — the "annotate
//          the levels of these" job, same annotation family as action-tag-elements.cs but reporting
//          height instead of identity.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: NewSpotElevation needs a geometry REFERENCE, not just a point — this fragment digs one out of
//         each element's solid geometry (ComputeReferences=true). Elements whose geometry yields no usable
//         reference are skipped and REPORTED, not errors.
// GOTCHA: WHICH face you pick IS the answer. Revit hands back the BOTTOM face first on a floor, so
//         "first planar face with a reference" silently annotates the soffit. `facePreference` picks
//         deliberately, and the report prints the measured level per element so a wrong value cannot hide.
// LIVE-VERIFIED 2026-08-14 — on floor 918904 (Generic 300mm, top at 0 mm, soffit at -300 mm) in a
//         FloorPlan view. Placed 1, reported the correct 0.0 mm top-of-slab.
// BUG FOUND AND FIXED 2026-08-14 (this is why the fix above exists): the original took the first planar
//         face with a Reference and annotated -300.0 mm — the soffit — while reporting "1 placed, 0
//         skipped". A silent success: plausible number, wrong level, straight onto a drawing sheet.
//         Measured face order on that floor was normalZ -1.00 (bottom) FIRST, then +1.00 (top), then the
//         four verticals. NOTE the earlier proposed guard `Math.Abs(pf.FaceNormal.Z) > 0.9` would NOT have
//         caught it — Math.Abs(-1.00) passes. The test must be POSITIVE: pf.FaceNormal.Z > 0.9.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;              // the view the spot elevations are placed in
double leaderOffsetXMm = 300;   // leader bend/end offset from the measured point
double leaderOffsetYMm = 300;
bool hasLeader = true;
string facePreference = "top";  // "top" = highest upward face (FFL — the usual job) | "bottom" = soffit | "any"
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
                string faceNote = "";
                try
                {
                    // Collect EVERY candidate planar face first — never "first one wins". Revit returns the
                    // bottom face before the top on a floor, so first-wins annotates the soffit.
                    var cands = new List<(Reference r, XYZ pt, double nz)>();
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
                                // measure at the face's centroid-ish point via its bounding UV middle
                                var bb = f.GetBoundingBox();
                                var mid = (bb.Min + bb.Max) / 2.0;
                                cands.Add((f.Reference, f.Evaluate(mid), pf.FaceNormal.Z));
                            }
                        }
                    }

                    // POSITIVE normal test — Math.Abs() would let the soffit (nz = -1) through.
                    var pick = facePreference == "bottom"
                        ? cands.Where(c => c.nz < -0.9).OrderBy(c => c.pt.Z).FirstOrDefault()
                        : facePreference == "any"
                            ? cands.FirstOrDefault()
                            : cands.Where(c => c.nz > 0.9).OrderByDescending(c => c.pt.Z).FirstOrDefault();

                    if (pick.r == null && cands.Count > 0 && facePreference != "any")
                    {
                        pick = cands.First();
                        faceNote = $" [no {facePreference} face found — fell back to first planar face, normalZ {pick.nz:F2}]";
                    }
                    faceRef = pick.r;
                    measurePt = pick.pt;
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
                    // print the level actually annotated — this is what makes a wrong face visible
                    notes.Add($"Id {el.Id.IntegerValue}: annotated {UnitUtils.ConvertFromInternalUnits(measurePt.Z, DisplayUnitType.DUT_MILLIMETERS):F1} mm{faceNote}");
                }
                catch (Exception exOne)
                {
                    skipped++;
                    notes.Add($"Id {el.Id.IntegerValue}: {exOne.Message}");
                }
            }
            t.Commit();
            sb.AppendLine($"Spot elevations: {placed} placed, {skipped} skipped, of {elements.Count} element(s) in view '{spotView.Name}' (facePreference '{facePreference}').");
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
