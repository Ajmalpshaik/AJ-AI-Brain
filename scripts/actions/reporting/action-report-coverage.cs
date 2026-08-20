// ============================================================
// FRAGMENT (action) — action-report-coverage.cs
// PURPOSE: How much floor does each element actually serve? Reports the coverage RADIUS and AREA per
//          element plus min / max / average / total, optionally draws the coverage circles so gaps and
//          overlaps are visible, and optionally writes the value back to a parameter ("set this coverage").
//          Built for air terminals but works on anything point-based — lights, sprinklers, detectors,
//          sockets. (The user's ask, 2026-07-26: "if we draw a circle, that is coverage".)
// ASSUMES: elements (List<Element>, point-based) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// **THERE IS USUALLY NO "COVERAGE" PARAMETER TO READ — IT HAS TO BE DERIVED.** Checked on the standard
//   M_Supply Diffuser (2026-07-26): it carries Flow, Pressure Drop, Diffuser Width/Height, Duct
//   Width/Height and Max/Min Flow — and nothing about throw or coverage at all. So pick a source
//   deliberately, because THE THREE DISAGREE ENORMOUSLY. Measured on the same 17 terminals:
//     "spacing" -> 344 m2 total      "flow" @2 L/s/m2 -> 1,998 m2 total    (~6x apart)
//
//   source="spacing"  radius = HALF the distance to the nearest other element. Pure geometry, needs no
//                     parameter and no design figure — it is each element's territory, the honest answer
//                     to "how far apart did we actually put these". Best default.
//   source="flow"     area = Flow / designFlowPerM2, then an equivalent circular radius. **The design
//                     figure is YOURS and the answer is only as good as it** — 2 L/s/m2 vs 10 L/s/m2
//                     changes the result fivefold. Never let this default silently.
//   source="fixed"    you state the radius (manufacturer throw data, a spec, a rule of thumb). Use this
//                     for "set the coverage to X and show me what that looks like".
//
// GOTCHA: **circles covering only ~45% of a floor is NORMAL, not a fault.** Circles cannot tile a plane —
//         even perfectly packed touching circles on a square grid cover only ~78%, and corners are always
//         missed. Read the percentage as a comparison between rooms, not as "half the room is unserved".
// GOTCHA: coverage here is a flat circle on plan. It ignores ceiling height, throw direction, obstructions
//         and whether the diffuser actually blows that way. It answers "how much floor is this responsible
//         for", not "will the air reach". Say that when reporting it.
// A CIRCLE IS ONE ELEMENT — DO NOT DRAW TWO HALF ARCS. `Arc.Create(centre, r, 0, 2*PI, X, Y)` gives a
//         closed, unbound arc that `NewDetailCurve` accepts directly: one element, one click, one circle.
//         (`Ellipse.CreateCurve` with equal radii also works.) An earlier version of THIS FILE claimed
//         full circles were "rejected by Revit" and drew two half arcs instead — that claim was never
//         tested and is FALSE, corrected 2026-07-26 after the user pointed out the halves were selectable
//         separately. Lesson kept deliberately: an untested assumption written down as a gotcha is worse
//         than no gotcha, because everyone after you obeys it.
// GOTCHA: drawing into a PLAN view uses DETAIL arcs (view-specific); model geometry at ceiling height
//         would sit above the cut plane and be invisible. Same trap as action-plan-shortest-route.cs.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1, 17 air terminals in 3 rooms:
//     spacing radii 1883 / 2492 / 3520 mm (min / avg / max), areas 11.1 / 20.2 / 38.9 m2, total 344.1 m2
//     drawn as 17 circles — 17 elements, not 34 — grouped as 'MEP_Terminal_Coverage'
//     flow method at an invented 2 L/s/m2 gave 117.5 m2 EACH — 262% of the floor, which is exactly why
//     that figure must come from the user rather than from this file.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string source = "spacing";          // "spacing" | "flow" | "fixed"  — see header, they disagree wildly
double fixedRadiusMm = 3000;        // source="fixed" only
string flowParameterName = "Flow";  // source="flow" only
double designFlowPerM2 = 0;         // source="flow" only — L/s per m2. MUST be set by you; 0 refuses to guess
bool drawCircles = false;           // draw the coverage circles so gaps/overlaps are visible
int drawInViewIdInt = 0;            // the PLAN view to draw into (detail arcs); 0 = don't draw
bool groupDrawn = true;             // bundle the drawn circles into one selectable Group
string drawnGroupName = "";         // e.g. "MEP_Terminal_Coverage"
string writeRadiusToParameter = ""; // optional — write the radius (mm) into this text/number parameter
int maxRowsListed = 30;             // per-element detail cap; the statistics always cover everything
// ---- END INPUTS ----

Func<double,double> toMm = v => v * 304.8;
Func<double,double> mmToFt = v => v / 304.8;
Func<double,double> toM2 = v => v / 10.763910416709722;

Func<Element,XYZ> centreOf = el =>
{
    var lp = el.Location as LocationPoint;
    if (lp != null) return lp.Point;
    var b = el.get_BoundingBox(null);
    return b == null ? null : (b.Min + b.Max) / 2.0;
};
var pts = elements.Where(e => centreOf(e) != null).ToList();

if (pts.Count == 0) { sb.AppendLine("No elements with a usable location."); }
else if (source == "flow" && designFlowPerM2 <= 0)
{
    sb.AppendLine("source=\"flow\" needs designFlowPerM2 (L/s per m2) — this fragment will NOT invent one.");
    sb.AppendLine("  It changes the answer several-fold. Ask for the project's supply rate and set it.");
}
else if (source == "spacing" && pts.Count < 2)
{
    sb.AppendLine("source=\"spacing\" needs at least 2 elements — with one there is no neighbour to halve the gap to.");
}
else
{
    // ---- radius per element ----
    var radius = new Dictionary<int,double>();
    foreach (var e in pts)
    {
        double r;
        if (source == "fixed") r = mmToFt(fixedRadiusMm);
        else if (source == "flow")
        {
            var p = e.LookupParameter(flowParameterName);
            if (p == null) { var te = Document.GetElement(e.GetTypeId()); p = te?.LookupParameter(flowParameterName); }
            double lps = (p != null && p.HasValue)
                ? p.AsDouble() * 28.316846592 : 0;
            double m2 = lps / designFlowPerM2;
            r = mmToFt(Math.Sqrt(m2 / Math.PI) * 1000.0);   // equivalent circular radius
        }
        else r = pts.Where(o => o.Id != e.Id).Min(o => centreOf(e).DistanceTo(centreOf(o))) / 2.0;
        radius[e.Id.IntegerValue] = r;
    }

    var rs = radius.Values.ToList();
    var areas = rs.Select(r => Math.PI * r * r).ToList();
    sb.AppendLine($"Coverage — source '{source}', {pts.Count} element(s)"
        + (source == "flow" ? $" at {designFlowPerM2} L/s per m2 (YOUR figure)" : "")
        + (source == "fixed" ? $" at a stated {fixedRadiusMm} mm radius" : "") + ":");
    sb.AppendLine($"  RADIUS  min {toMm(rs.Min()):N0} mm | max {toMm(rs.Max()):N0} mm | average {toMm(rs.Average()):N0} mm");
    sb.AppendLine($"  AREA    min {toM2(areas.Min()):F1} m2 | max {toM2(areas.Max()):F1} m2 | average {toM2(areas.Average()):F1} m2 | TOTAL {toM2(areas.Sum()):F1} m2");

    // biggest circles = the elements carrying the most floor on their own
    sb.AppendLine("  Widest coverage (these carry the most floor alone):");
    foreach (var kv in radius.OrderByDescending(k => k.Value).Take(3))
        sb.AppendLine($"    Id {kv.Key}: {toMm(kv.Value):N0} mm radius, {toM2(Math.PI*kv.Value*kv.Value):F1} m2");
    sb.AppendLine("  Tightest coverage:");
    foreach (var kv in radius.OrderBy(k => k.Value).Take(3))
        sb.AppendLine($"    Id {kv.Key}: {toMm(kv.Value):N0} mm radius, {toM2(Math.PI*kv.Value*kv.Value):F1} m2");

    // ---- compare against real rooms, if any are placed ----
    var rooms = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms)
        .WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Architecture.Room>()
        .Where(r => r.Area > 0).ToList();
    if (rooms.Count > 0)
    {
        sb.AppendLine("  Against actual room floor area (see the ~45%-is-normal gotcha in the header):");
        foreach (var rm in rooms)
        {
            var mine = pts.Where(e => {
                var c = centreOf(e);
                var probe = new XYZ(c.X, c.Y, rm.Level.Elevation + mmToFt(1000));
                bool inside=false; try { inside = rm.IsPointInRoom(probe); } catch {}
                return inside; }).ToList();
            if (mine.Count == 0) continue;
            double a = mine.Sum(e => { double r = radius[e.Id.IntegerValue]; return Math.PI*r*r; });
            sb.AppendLine($"    {rm.Name}: floor {toM2(rm.Area):F0} m2, {mine.Count} element(s), coverage {toM2(a):F0} m2 ({toM2(a)/toM2(rm.Area)*100:F0}%)");
        }
    }

    // ---- optionally write the radius back ----
    if (!string.IsNullOrEmpty(writeRadiusToParameter))
    {
        using (var t = new Transaction(Document, "AJ Tools - Write Coverage Radius"))
        {
            t.Start();
            try
            {
                int wrote = 0, skipped = 0;
                foreach (var e in pts)
                {
                    var p = e.LookupParameter(writeRadiusToParameter);
                    if (p == null || p.IsReadOnly) { skipped++; continue; }
                    double mmVal = toMm(radius[e.Id.IntegerValue]);
                    bool ok = p.StorageType == StorageType.String
                        ? p.Set($"{mmVal:F0}")
                        : p.StorageType == StorageType.Double ? p.Set(radius[e.Id.IntegerValue])
                        : p.StorageType == StorageType.Integer ? p.Set((int)Math.Round(mmVal)) : false;
                    if (ok) wrote++; else skipped++;
                }
                t.Commit();
                sb.AppendLine($"  Wrote the radius into '{writeRadiusToParameter}' on {wrote} element(s), {skipped} skipped (missing or read-only).");
            }
            catch (Exception ex) { try { t.RollBack(); } catch { } sb.AppendLine($"  FAILED writing '{writeRadiusToParameter}' — rolled back. {ex.Message}"); }
        }
    }

    // ---- optionally draw the circles ----
    if (drawCircles && drawInViewIdInt != 0)
    {
        var dv = Document.GetElement(new ElementId(drawInViewIdInt)) as View;
        if (dv == null || dv.IsTemplate) sb.AppendLine($"  drawInViewIdInt {drawInViewIdInt} is not a usable view — nothing drawn.");
        else
        {
            double flatZ = (dv is ViewPlan && (dv as ViewPlan).GenLevel != null)
                ? (dv as ViewPlan).GenLevel.Elevation : dv.Origin.Z;
            var made = new List<ElementId>();
            using (var t = new Transaction(Document, "AJ Tools - Draw Coverage Circles"))
            {
                t.Start();
                try
                {
                    foreach (var e in pts)
                    {
                        var c = centreOf(e); double r = radius[e.Id.IntegerValue];
                        if (r <= 0) continue;
                        var ctr = new XYZ(c.X, c.Y, flatZ);
                        // ONE closed circle, one element — verified 2026-07-26
                        var circle = Arc.Create(ctr, r, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
                        made.Add(Document.Create.NewDetailCurve(dv, circle).Id);
                    }
                    if (groupDrawn && made.Count > 1)
                    {
                        try {
                            var g = Document.Create.NewGroup(made);
                            if (!string.IsNullOrEmpty(drawnGroupName)) { try { g.GroupType.Name = drawnGroupName; } catch { } }
                            sb.AppendLine($"  Grouped the circles as '{g.GroupType.Name}' (Id {g.Id}).");
                        } catch (Exception exG) { sb.AppendLine($"  Grouping failed ({exG.Message}) — circles are still drawn."); }
                    }
                    t.Commit();
                    sb.AppendLine($"  Drew {pts.Count} coverage circle(s) ({made.Count} arc segments) in '{dv.Name}'.");
                }
                catch (Exception ex) { try { t.RollBack(); } catch { } sb.AppendLine($"  FAILED to draw — rolled back, nothing created. {ex.Message}"); }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
