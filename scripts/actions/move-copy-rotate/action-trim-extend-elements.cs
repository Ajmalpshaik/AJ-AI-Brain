// ============================================================
// FRAGMENT (action) — action-trim-extend-elements.cs
// PURPOSE: Trim or extend exactly TWO linear elements in `elements` so they meet cleanly at one corner —
//          the "AutoCAD Trim/Extend to a corner" operation. Works on anything with a straight Line
//          LocationCurve (duct, pipe, cable tray, conduit, wall, model line, detail line): computes where
//          the two lines' own directions cross, then moves each line's NEARER endpoint to that corner
//          point while leaving its farther endpoint untouched — trims if the lines already overlapped past
//          the corner, extends if they fell short of it.
// ASSUMES: elements (List<Element>) is EXACTLY the two elements to square off (elements[0], elements[1]),
//          and sb (StringBuilder) already exist from a filter fragment above (e.g. filter-by-id-list.cs
//          with two Ids — trim/extend is inherently pairwise, not a batch operation like most actions).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: assumes both lines are coplanar in a horizontal plane (same Z, e.g. two duct runs on the same
//         level) — the corner is solved in the XY-plane only. Not meant for lines that are skew in 3D or a
//         genuine vertical riser corner.
// GOTCHA: if the two lines are parallel (no real corner), this fails cleanly with a message instead of
//         guessing.
// GOTCHA: breaks any existing end-to-end MEP connections at the moved endpoint(s) — reconnect manually
//         afterward (same caveat as action-move-elements.cs), or follow with action-fillet-elements.cs
//         (mode="elbow") to insert a real fitting at the new corner instead of a sharp joint.
// LIVE-VERIFIED 2026-07-22 on Ducts: created 2 disconnected test ducts with a 2ft gap at 90 degrees,
// ran unmodified, both extended to meet exactly at the computed corner (exact numeric match), fresh
// re-fetch confirmed. Also LIVE-VERIFIED on Model Lines in the same "gap, needs extending" shape — exact
// match, persisted correctly.
// CAUTION (found while fixing the closely-related action-fillet-elements.cs mode="arc", which shares this
// exact in-place `lcA.Curve = ...` technique): for two Model/Detail Lines that ALREADY share a coincident
// endpoint before this runs, reassigning LocationCurve.Curve in place was found to silently no-op — no
// exception, transaction commits clean, but a fresh re-fetch shows the original untrimmed geometry. This
// was reproduced consistently for that scenario (see action-fillet-elements.cs's file header for the full
// investigation) and fixed there via delete+recreate instead of in-place reassignment. This fragment's own
// tests only covered the "gap, needs extending" shape (which works correctly) — the coincident-endpoint
// case specifically was NOT re-tested here. If this fragment appears to report success on a Model/Detail
// Line pair that already touched before running but the line geometry doesn't actually change, apply the
// same delete+recreate fix action-fillet-elements.cs now uses. Ducts were not affected by this issue in
// either shape tested.
// ============================================================

if (elements.Count != 2)
{
    sb.AppendLine($"Trim/extend needs EXACTLY 2 elements in `elements` — got {elements.Count}. Nothing changed.");
}
else
{
    var lcA = elements[0].Location as LocationCurve;
    var lcB = elements[1].Location as LocationCurve;
    var lineA = lcA?.Curve as Line;
    var lineB = lcB?.Curve as Line;

    if (lineA == null || lineB == null)
    {
        sb.AppendLine("Both elements need a straight Line LocationCurve (not an Arc, not a point-based element).");
    }
    else
    {
        XYZ p1 = lineA.GetEndPoint(0), d1 = lineA.Direction;
        XYZ p2 = lineB.GetEndPoint(0), d2 = lineB.Direction;
        double denom = d1.X * d2.Y - d1.Y * d2.X;

        if (Math.Abs(denom) < 1e-9)
        {
            sb.AppendLine("The two lines are parallel (in plan) — no single corner to trim/extend to.");
        }
        else
        {
            double t = ((p2.X - p1.X) * d2.Y - (p2.Y - p1.Y) * d2.X) / denom;
            XYZ corner = p1 + d1 * t;

            using (var tr = new Transaction(Document, "AJ Tools - Trim/Extend Elements"))
            {
                tr.Start();
                try
                {
                    Func<Line, XYZ, Line> rebuild = (line, cornerPt) =>
                    {
                        XYZ e0 = line.GetEndPoint(0), e1 = line.GetEndPoint(1);
                        bool nearIsStart = e0.DistanceTo(cornerPt) <= e1.DistanceTo(cornerPt);
                        return nearIsStart ? Line.CreateBound(cornerPt, e1) : Line.CreateBound(e0, cornerPt);
                    };

                    lcA.Curve = rebuild(lineA, corner);
                    lcB.Curve = rebuild(lineB, corner);
                    tr.Commit();
                    sb.AppendLine($"Trimmed/extended both elements to meet at ({corner.X:F3}, {corner.Y:F3}, {corner.Z:F3}).");
                }
                catch (Exception ex)
                {
                    tr.RollBack();
                    sb.AppendLine($"FAILED to trim/extend — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with another action fragment below (e.g. action-fillet-elements.cs mode="elbow" to
//      insert a real fitting at the new corner), or add return sb.ToString(); to finish ----
