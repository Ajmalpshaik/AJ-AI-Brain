// ============================================================
// FRAGMENT (recipe) — create-mep-openings.cs
// PURPOSE: Cut real OPENINGS (sleeves) in walls, floors and beams wherever the MEP runs in `elements`
//          pass through them — "put the sleeves in", "cut the holes for the ducts". Finds each crossing
//          by REAL SOLID INTERSECTION, not overlapping bounding boxes, and picks the right one of
//          `NewOpening`'s three completely different overloads per host type.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — the MEP runs
//          (ducts, pipes, cable trays, conduits). The HOSTS are collected by this fragment.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/mep-openings.md — read it before running; it carries the merge and
//         rerun techniques this fragment deliberately does NOT implement.
//
// ⚠⚠ THIS CUTS REAL HOLES IN REAL ELEMENTS. Destructive and bulk. DRY RUN BY DEFAULT — show the count
//    and the host list, get an explicit go-ahead, then rerun with dryRun=false. Rule 5 of START-HERE.md.
// GOTCHA: IT DOES NOT MERGE, AND IT DOES NOT DE-DUPLICATE ON RERUN. Four pipes through one wall give
//         FOUR openings here, not one builder's opening; and running it twice gives EIGHT. The add-in's
//         version does both and the method is written up in the knowledge note — build it the day a job
//         needs it. Until then: run once, check, and delete by the reported ids if it is wrong.
// GOTCHA: THREE HOST TYPES, THREE DIFFERENT CALLS, and using the wrong one throws or makes the wrong
//         shape. Wall = two opposite corner POINTS (no profile). Floor/roof/ceiling = a CurveArray
//         profile + `true`. Beam = a profile + an `eRefFace`, and WHICH reference face works is not
//         predictable — CenterY, then CenterZ, then CenterX, taking the first that does not throw.
// GOTCHA: a bounding-box overlap is NOT a crossing. A duct running past a wall in the next room shares
//         a box corner with it. Every crossing here is a real `BooleanOperationsType.Intersect` with a
//         non-zero volume.
// GOTCHA: one element can have SEVERAL solids (a duct with insulation, a wall with layers) — every
//         pairing is intersected and the boxes unioned. Some Revit solids THROW on a boolean, so each
//         pair is caught individually; one throw must not abandon the element.
// GOTCHA: a wall opening is sized along the WALL'S OWN DIRECTION, not world X/Y — a wall at 30 degrees
//         would otherwise get an opening far too wide.
// GOTCHA: `clearanceMm` and `minOpeningMm` are PROJECT NUMBERS. Ask for them. The values below are
//         placeholders so the fragment runs, not recommendations.
// NOTE: linked MEP is supported by setting `linkInstanceIdInt` — the linked solids are brought into
//       host coordinates with SolidUtils.CreateTransformed. The LINK is never modified; only your own
//       hosts are cut. A linked HOST cannot be cut at all — that needs a face-based opening family.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's MEP Openings tool
//   (~136 KB, its largest service). Compile-checked on 2020/2024/2027. Run it dry, then on ONE crossing.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report the crossings only; false = actually cut openings
double clearanceMm = 50;             // ASK. how much bigger than the service the opening is, each side
double minOpeningMm = 100;           // ASK. smallest opening to bother cutting
bool hostWalls = true;               // which host categories to cut
bool hostFloors = true;
bool hostBeams = false;              // structural framing — the beam path tries 3 reference faces
int? linkInstanceIdInt = null;       // null = `elements` are in THIS model; else the RVT link they live in
int maxOpenings = 200;               // hard stop, so a bad filter cannot cut a thousand holes
// ---- END INPUTS ----

const double MM = 304.8;
const double VOL_TOL = 1e-7;

double clearFt = clearanceMm / MM;
double minFt = minOpeningMm / MM;

// ---------- link transform, if the services are linked ----------
Transform linkTf = Transform.Identity;
if (linkInstanceIdInt.HasValue)
{
    var li = Document.GetElement(new ElementId(linkInstanceIdInt.Value)) as RevitLinkInstance;
    if (li == null) { sb.AppendLine("linkInstanceIdInt is not a Revit link instance."); return sb.ToString(); }
    linkTf = li.GetTotalTransform();
}

// ---------- collect candidate hosts from THIS model ----------
var hostCats = new List<BuiltInCategory>();
if (hostWalls) hostCats.Add(BuiltInCategory.OST_Walls);
if (hostFloors) hostCats.Add(BuiltInCategory.OST_Floors);
if (hostBeams) hostCats.Add(BuiltInCategory.OST_StructuralFraming);
if (hostCats.Count == 0) { sb.AppendLine("No host categories selected — nothing to cut."); return sb.ToString(); }

var hosts = new List<Element>();
foreach (var bic in hostCats)
    hosts.AddRange(new FilteredElementCollector(Document).OfCategory(bic).WhereElementIsNotElementType().ToElements());

if (hosts.Count == 0) { sb.AppendLine("No walls/floors/beams found in this model."); return sb.ToString(); }

// ---------- helper: every solid of an element, in host coordinates ----------
Func<Element, Transform, List<Solid>> solidsOf = (el, tf) =>
{
    var outList = new List<Solid>();
    if (el == null) return outList;
    try
    {
        var opts = new Options { DetailLevel = ViewDetailLevel.Fine, IncludeNonVisibleObjects = false, ComputeReferences = false };
        var geo = el.get_Geometry(opts);
        if (geo == null) return outList;

        Action<GeometryElement> collect = null;
        collect = g =>
        {
            foreach (GeometryObject go in g)
            {
                var s = go as Solid;
                if (s != null && s.Volume > VOL_TOL) { outList.Add(s); continue; }
                var inst = go as GeometryInstance;
                if (inst != null) { var ig = inst.GetInstanceGeometry(); if (ig != null) collect(ig); }
            }
        };
        collect(geo);
    }
    catch { return outList; }

    if (tf == null || tf.IsIdentity) return outList;
    var moved = new List<Solid>();
    foreach (var s in outList)
    {
        try { moved.Add(SolidUtils.CreateTransformed(s, tf)); } catch { moved.Add(s); }
    }
    return moved;
};

// ---------- find every real crossing ----------
var crossings = new List<Tuple<Element, Element, XYZ, XYZ>>();   // host, mep, boxMin, boxMax  (world)
int booleanFailures = 0;

foreach (var mep in elements)
{
    var mepSolids = solidsOf(mep, linkTf);
    if (mepSolids.Count == 0) continue;

    // cheap reject first: only test hosts whose bbox is anywhere near
    BoundingBoxXYZ mepBb = null;
    foreach (var ms in mepSolids)
    {
        var b = ms.GetBoundingBox();
        if (b == null) continue;
        // solid bbox is in the solid's own frame
        var t2 = b.Transform ?? Transform.Identity;
        var lo = t2.OfPoint(b.Min); var hi = t2.OfPoint(b.Max);
        var mn = new XYZ(Math.Min(lo.X, hi.X), Math.Min(lo.Y, hi.Y), Math.Min(lo.Z, hi.Z));
        var mx = new XYZ(Math.Max(lo.X, hi.X), Math.Max(lo.Y, hi.Y), Math.Max(lo.Z, hi.Z));
        if (mepBb == null) mepBb = new BoundingBoxXYZ { Min = mn, Max = mx };
        else
        {
            mepBb.Min = new XYZ(Math.Min(mepBb.Min.X, mn.X), Math.Min(mepBb.Min.Y, mn.Y), Math.Min(mepBb.Min.Z, mn.Z));
            mepBb.Max = new XYZ(Math.Max(mepBb.Max.X, mx.X), Math.Max(mepBb.Max.Y, mx.Y), Math.Max(mepBb.Max.Z, mx.Z));
        }
    }
    if (mepBb == null) continue;

    foreach (var host in hosts)
    {
        var hb = host.get_BoundingBox(null);
        if (hb == null) continue;
        if (hb.Max.X < mepBb.Min.X || hb.Min.X > mepBb.Max.X ||
            hb.Max.Y < mepBb.Min.Y || hb.Min.Y > mepBb.Max.Y ||
            hb.Max.Z < mepBb.Min.Z || hb.Min.Z > mepBb.Max.Z) continue;

        var hostSolids = solidsOf(host, Transform.Identity);
        if (hostSolids.Count == 0) continue;

        XYZ iMin = null, iMax = null;
        foreach (var ms in mepSolids)
        {
            foreach (var hs in hostSolids)
            {
                Solid inter = null;
                try { inter = BooleanOperationsUtils.ExecuteBooleanOperation(ms, hs, BooleanOperationsType.Intersect); }
                catch { booleanFailures++; continue; }   // some solids refuse — try the next pair
                if (inter == null || inter.Volume <= VOL_TOL) continue;

                var ib = inter.GetBoundingBox();
                if (ib == null) continue;
                var it = ib.Transform ?? Transform.Identity;
                var c0 = it.OfPoint(ib.Min); var c1 = it.OfPoint(ib.Max);
                var lo = new XYZ(Math.Min(c0.X, c1.X), Math.Min(c0.Y, c1.Y), Math.Min(c0.Z, c1.Z));
                var hi = new XYZ(Math.Max(c0.X, c1.X), Math.Max(c0.Y, c1.Y), Math.Max(c0.Z, c1.Z));

                if (iMin == null) { iMin = lo; iMax = hi; }
                else
                {
                    iMin = new XYZ(Math.Min(iMin.X, lo.X), Math.Min(iMin.Y, lo.Y), Math.Min(iMin.Z, lo.Z));
                    iMax = new XYZ(Math.Max(iMax.X, hi.X), Math.Max(iMax.Y, hi.Y), Math.Max(iMax.Z, hi.Z));
                }
            }
        }

        if (iMin != null) crossings.Add(Tuple.Create(host, mep, iMin, iMax));
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing cut. " : "") +
              $"{crossings.Count} real crossing(s) found between {elements.Count} service(s) and {hosts.Count} host(s)" +
              (linkInstanceIdInt.HasValue ? " (services read from a link)" : "") + ".");
if (booleanFailures > 0) sb.AppendLine($"{booleanFailures} solid pair(s) refused a boolean and were skipped.");

if (crossings.Count == 0) return sb.ToString();
if (crossings.Count > maxOpenings)
{
    sb.AppendLine($"STOPPED: {crossings.Count} crossings exceeds maxOpenings ({maxOpenings}). Narrow the filter " +
                  "or raise the cap deliberately — this would cut a hole for every one of them.");
    return sb.ToString();
}

// ---------- cut ----------
int cut = 0, failed = 0;
var byHost = new Dictionary<string, int>();
var madeIds = new List<string>();
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Create MEP Openings"))
{
    t.Start();
    try
    {
        foreach (var c in crossings)
        {
            Element host = c.Item1, mep = c.Item2;
            XYZ lo = c.Item3, hi = c.Item4;

            // clearance + minimum size, applied about the centre
            XYZ ctr = (lo + hi) * 0.5;
            double dx = Math.Max(hi.X - lo.X + 2 * clearFt, minFt);
            double dy = Math.Max(hi.Y - lo.Y + 2 * clearFt, minFt);
            double dz = Math.Max(hi.Z - lo.Z + 2 * clearFt, minFt);

            string hostKind = host.Category != null ? host.Category.Name : "?";
            Opening op = null;

            try
            {
                var wall = host as Wall;
                if (wall != null)
                {
                    // WALL: two opposite corner points. Width along the WALL'S OWN direction, height in Z.
                    var lc = wall.Location as LocationCurve;
                    XYZ dir = XYZ.BasisX;
                    if (lc != null && lc.Curve is Line) dir = ((Line)lc.Curve).Direction.Normalize();
                    double along = Math.Abs(dir.X) * dx + Math.Abs(dir.Y) * dy;
                    along = Math.Max(along, minFt);

                    XYZ p1 = ctr - dir.Multiply(along / 2.0) - XYZ.BasisZ.Multiply(dz / 2.0);
                    XYZ p2 = ctr + dir.Multiply(along / 2.0) + XYZ.BasisZ.Multiply(dz / 2.0);
                    if (!dryRun) op = Document.Create.NewOpening(wall, p1, p2);
                }
                else if (host.Category != null &&
                         host.Category.Id == new ElementId(BuiltInCategory.OST_StructuralFraming))
                {
                    // BEAM: profile + a reference face, and which face works is NOT predictable.
                    if (!dryRun)
                    {
                        foreach (var face in new[] { Autodesk.Revit.Creation.eRefFace.CenterY,
                                                     Autodesk.Revit.Creation.eRefFace.CenterZ,
                                                     Autodesk.Revit.Creation.eRefFace.CenterX })
                        {
                            try
                            {
                                var prof = new CurveArray();
                                XYZ a = new XYZ(ctr.X - dx / 2, ctr.Y - dy / 2, ctr.Z - dz / 2);
                                XYZ b = new XYZ(ctr.X + dx / 2, ctr.Y - dy / 2, ctr.Z - dz / 2);
                                XYZ cc = new XYZ(ctr.X + dx / 2, ctr.Y + dy / 2, ctr.Z + dz / 2);
                                XYZ d = new XYZ(ctr.X - dx / 2, ctr.Y + dy / 2, ctr.Z + dz / 2);
                                prof.Append(Line.CreateBound(a, b)); prof.Append(Line.CreateBound(b, cc));
                                prof.Append(Line.CreateBound(cc, d)); prof.Append(Line.CreateBound(d, a));
                                op = Document.Create.NewOpening(host, prof, face);
                                if (op != null) break;
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    // FLOOR / ROOF / CEILING: a CurveArray profile, `true` = perpendicular to the face.
                    if (!dryRun)
                    {
                        double z = ctr.Z;
                        var prof = new CurveArray();
                        XYZ a = new XYZ(ctr.X - dx / 2, ctr.Y - dy / 2, z);
                        XYZ b = new XYZ(ctr.X + dx / 2, ctr.Y - dy / 2, z);
                        XYZ cc = new XYZ(ctr.X + dx / 2, ctr.Y + dy / 2, z);
                        XYZ d = new XYZ(ctr.X - dx / 2, ctr.Y + dy / 2, z);
                        prof.Append(Line.CreateBound(a, b)); prof.Append(Line.CreateBound(b, cc));
                        prof.Append(Line.CreateBound(cc, d)); prof.Append(Line.CreateBound(d, a));
                        op = Document.Create.NewOpening(host, prof, true);
                    }
                }

                if (dryRun || op != null)
                {
                    cut++;
                    if (!byHost.ContainsKey(hostKind)) byHost[hostKind] = 0;
                    byHost[hostKind]++;
                    if (op != null && madeIds.Count < 500) madeIds.Add(op.Id.ToString());
                    if (rows.Count < 25)
                        rows.Add(host.Id + " | " + hostKind + " | " + mep.Id + " | " +
                                 Math.Round(dx * MM) + " x " + Math.Round(dz * MM));
                }
                else failed++;
            }
            catch { failed++; }
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing cut. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "Would cut " : "Cut ") + cut + " opening(s)" +
              (failed > 0 ? $", {failed} failed" : "") +
              $" — clearance {clearanceMm} mm, minimum {minOpeningMm} mm.");
foreach (var kv in byHost.OrderBy(k => k.Key)) sb.AppendLine("  " + kv.Key + ": " + kv.Value);
sb.AppendLine("NOT merged and NOT de-duplicated: four services through one wall give four openings, and " +
              "running this twice gives eight. Check before rerunning.");
if (madeIds.Count > 0)
    sb.AppendLine("New Opening ids (delete these to undo): " + string.Join(", ", madeIds.Take(100)) +
                  (madeIds.Count > 100 ? $" ... and {madeIds.Count - 100} more" : ""));

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Host Id | Host | Service Id | Opening W x H (mm)");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (cut > rows.Count) sb.AppendLine("... and " + (cut - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
