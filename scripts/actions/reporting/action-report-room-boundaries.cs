// ============================================================
// FRAGMENT (action) — action-report-room-boundaries.cs
// PURPOSE: Report each Room/Space's boundary loops as mm segments — start/end coordinates + the wall (or
//          other element) that generates each segment. The geometry feed for "build something along this
//          room's edge" jobs (create-wall.cs, create-line.cs, terminal layouts). (Dynamo-package
//          equivalent: Genius Loci / Clockwork room boundary nodes.)
// ASSUMES: elements (List<Element>, Rooms and/or Spaces — filter-by-category.cs or
//          filter-by-unenclosed-spatial-elements.cs's opposite) and sb exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
// GOTCHA: loop 1 is the outer boundary; extra loops are holes (columns, shafts) — the loop index in the
//         output matters. Unenclosed rooms report zero loops (valid empty, not an error).
// GOTCHA: segments come back in Finish boundary location by default here — the wall FACE the room sees,
//         not the wall centerline; switch the SpatialElementBoundaryLocation input for centerlines.
// ✓ GRACEFUL PATH LIVE-VERIFIED 2026-07-26 on Project1 — 3 unplaced rooms correctly reported as
//   "0 boundary loops" rather than erroring. The positive path (a real enclosed room) is still untested.
// ============================================================


// Version-proof id VALUE, for the few places that need a number rather than an ElementId (testing for a
// built-in negative id, casting to BuiltInParameter). Revit 2024 renamed IntegerValue -> Value and made
// it 64-bit; naming either one directly fails to compile on the other version, so it is looked up by
// name instead. The lookup happens ONCE and is captured, so each call is a field read, not a reflection
// search - safe to use in a loop.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string boundaryLocation = "finish";  // "finish" (room-side face) | "center" (wall centerline)
int maxSegmentsPerRoom = 40;         // detail cap per room; loop/segment counts always complete
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
Func<XYZ, string> fmtPt = p => $"({toMm(p.X):F0}, {toMm(p.Y):F0})";

var opts = new SpatialElementBoundaryOptions
{
    SpatialElementBoundaryLocation = boundaryLocation == "center"
        ? SpatialElementBoundaryLocation.Center
        : SpatialElementBoundaryLocation.Finish
};

int reported = 0, notSpatial = 0;

foreach (var el in elements)
{
    var spatial = el as SpatialElement;
    if (spatial == null) { notSpatial++; continue; }

    var loops = spatial.GetBoundarySegments(opts);
    if (loops == null || loops.Count == 0)
    {
        sb.AppendLine($"- '{spatial.Name}' (Id {spatial.Id}) — 0 boundary loops (not enclosed/placed).");
        reported++;
        continue;
    }

    sb.AppendLine($"- '{spatial.Name}' (Id {spatial.Id}) — {loops.Count} loop(s) ({boundaryLocation} line):");
    int shown = 0;
    for (int li = 0; li < loops.Count; li++)
    {
        var segs = loops[li];
        sb.AppendLine($"    loop {li + 1}{(li == 0 ? " (outer)" : " (hole)")} — {segs.Count} segment(s):");
        foreach (var seg in segs)
        {
            if (shown >= maxSegmentsPerRoom) break;
            var curve = seg.GetCurve();
            var src = IdValue(seg.ElementId) >= 0 ? Document.GetElement(seg.ElementId) : null;
            sb.AppendLine($"      {fmtPt(curve.GetEndPoint(0))} -> {fmtPt(curve.GetEndPoint(1))}, {toMm(curve.Length):F0} mm, from: {(src != null ? $"'{src.Name}' (Id {src.Id})" : "(room separation/none)")}");
            shown++;
        }
        if (shown >= maxSegmentsPerRoom) { sb.AppendLine($"      ... segment detail capped at {maxSegmentsPerRoom}"); break; }
    }
    reported++;
}

sb.AppendLine($"Boundary report: {reported} room(s)/space(s) reported, {notSpatial} of {elements.Count} elements were not Rooms/Spaces.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
