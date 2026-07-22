// ============================================================
// FRAGMENT (action) — action-fillet-elements.cs
// PURPOSE: Round the corner between exactly TWO linear elements in `elements` — the "AutoCAD Fillet"
//          operation. Two modes because "fillet" means something different for MEP runs vs. drafting
//          lines:
//          - mode="elbow" -> both elements are MEP curves (Duct/Pipe/CableTray/Conduit) that already meet
//            (or nearly meet) at a corner. Inserts a REAL elbow fitting via Document.Create.NewElbowFitting
//            — this is how a duct/pipe corner actually gets "rounded" in Revit; there's no radius input
//            because the elbow's shape comes from the family's own routing preferences, not a value picked
//            here. Run action-trim-extend-elements.cs on the pair FIRST so their near ends share the exact
//            same point — NewElbowFitting needs two connectors close enough to bridge.
//          - mode="arc" -> both elements are Model Lines or Detail Lines (drafting, not MEP). Computes the
//            true tangent-arc fillet at the given radius: trims both lines back to their tangent points and
//            inserts a new arc-shaped line joining them.
// ASSUMES: elements (List<Element>) is EXACTLY the two elements meeting at one corner, and sb
//          (StringBuilder) already exist from a filter fragment above — fillet is inherently pairwise.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA (mode="arc"): assumes both lines are coplanar in a horizontal plane (same Z) — same limitation as
//         action-trim-extend-elements.cs. Only Model Lines and Detail Lines are supported for the new arc
//         segment (not walls — a curved wall segment is a different, heavier operation).
// NOT YET LIVE-VERIFIED — test once on a real pair before trusting it further.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "elbow";  // "elbow" (MEP curves — real fitting, no radius) | "arc" (Model/Detail lines — geometric fillet)
double radiusMm = 300;  // only used when mode == "arc"
// ---- END INPUTS ----

if (elements.Count != 2)
{
    sb.AppendLine($"Fillet needs EXACTLY 2 elements in `elements` — got {elements.Count}. Nothing changed.");
}
else if (mode == "elbow")
{
    var a = elements[0] as MEPCurve;
    var b = elements[1] as MEPCurve;
    if (a == null || b == null)
    {
        sb.AppendLine("mode=\"elbow\" needs both elements to be MEP curves (Duct/Pipe/CableTray/Conduit).");
    }
    else
    {
        Connector connA = null, connB = null;
        double best = double.MaxValue;
        foreach (Connector ca in a.ConnectorManager.Connectors)
        {
            if (ca.ConnectorType != ConnectorType.End) continue;
            foreach (Connector cb in b.ConnectorManager.Connectors)
            {
                if (cb.ConnectorType != ConnectorType.End) continue;
                double dist = ca.Origin.DistanceTo(cb.Origin);
                if (dist < best) { best = dist; connA = ca; connB = cb; }
            }
        }

        if (connA == null || connB == null)
        {
            sb.AppendLine("Could not find End connectors on both elements.");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Fillet (Elbow)"))
            {
                t.Start();
                try
                {
                    var elbow = Document.Create.NewElbowFitting(connA, connB);
                    t.Commit();
                    sb.AppendLine($"Inserted elbow fitting Id {elbow.Id.IntegerValue} between Id {elements[0].Id.IntegerValue} and Id {elements[1].Id.IntegerValue} (connectors were {UnitUtils.ConvertFromInternalUnits(best, DisplayUnitType.DUT_MILLIMETERS):F1}mm apart).");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to insert elbow — rolled back, nothing changed. Reason: {ex.Message}. Try running action-trim-extend-elements.cs on this pair first so the connectors coincide.");
                }
            }
        }
    }
}
else if (mode == "arc")
{
    var lcA = elements[0].Location as LocationCurve;
    var lcB = elements[1].Location as LocationCurve;
    var lineA = lcA?.Curve as Line;
    var lineB = lcB?.Curve as Line;

    if (lineA == null || lineB == null)
    {
        sb.AppendLine("mode=\"arc\" needs both elements to have a straight Line LocationCurve.");
    }
    else if (!(elements[0] is ModelLine || elements[0] is DetailLine) || !(elements[1] is ModelLine || elements[1] is DetailLine))
    {
        sb.AppendLine("mode=\"arc\" only supports Model Lines and Detail Lines (not walls/MEP curves) for the new arc segment.");
    }
    else
    {
        XYZ p1 = lineA.GetEndPoint(0), d1 = lineA.Direction;
        XYZ p2 = lineB.GetEndPoint(0), d2 = lineB.Direction;
        double denom = d1.X * d2.Y - d1.Y * d2.X;

        if (Math.Abs(denom) < 1e-9)
        {
            sb.AppendLine("The two lines are parallel (in plan) — no corner to fillet.");
        }
        else
        {
            double t = ((p2.X - p1.X) * d2.Y - (p2.Y - p1.Y) * d2.X) / denom;
            XYZ corner = p1 + d1 * t;

            XYZ farA = lineA.GetEndPoint(0).DistanceTo(corner) >= lineA.GetEndPoint(1).DistanceTo(corner) ? lineA.GetEndPoint(0) : lineA.GetEndPoint(1);
            XYZ farB = lineB.GetEndPoint(0).DistanceTo(corner) >= lineB.GetEndPoint(1).DistanceTo(corner) ? lineB.GetEndPoint(0) : lineB.GetEndPoint(1);
            XYZ dirA = (farA - corner).Normalize();
            XYZ dirB = (farB - corner).Normalize();

            double theta = dirA.AngleTo(dirB);
            double radiusFt = UnitUtils.ConvertToInternalUnits(radiusMm, DisplayUnitType.DUT_MILLIMETERS);

            if (theta < 1e-6 || Math.PI - theta < 1e-6)
            {
                sb.AppendLine("Corner angle is ~0 or ~180 degrees — can't fillet a straight or fully folded-back corner.");
            }
            else
            {
                double tangentLen = radiusFt / Math.Tan(theta / 2.0);
                if (tangentLen > lineA.Length || tangentLen > lineB.Length)
                {
                    sb.AppendLine($"Radius {radiusMm}mm needs a {UnitUtils.ConvertFromInternalUnits(tangentLen, DisplayUnitType.DUT_MILLIMETERS):F0}mm tangent length, longer than one of the lines — reduce radiusMm.");
                }
                else
                {
                    XYZ tangentA = corner + dirA * tangentLen;
                    XYZ tangentB = corner + dirB * tangentLen;
                    double centerDist = radiusFt / Math.Sin(theta / 2.0);
                    XYZ bisector = (dirA + dirB).Normalize();
                    XYZ center = corner + bisector * centerDist;
                    XYZ midUnit = ((tangentA - center).Normalize() + (tangentB - center).Normalize()).Normalize();
                    XYZ midPt = center + midUnit * radiusFt;

                    using (var t = new Transaction(Document, "AJ Tools - Fillet (Arc)"))
                    {
                        t.Start();
                        try
                        {
                            var arc = Arc.Create(tangentA, tangentB, midPt);

                            lcA.Curve = (lineA.GetEndPoint(0).DistanceTo(corner) <= lineA.GetEndPoint(1).DistanceTo(corner))
                                ? Line.CreateBound(tangentA, farA) : Line.CreateBound(farA, tangentA);
                            lcB.Curve = (lineB.GetEndPoint(0).DistanceTo(corner) <= lineB.GetEndPoint(1).DistanceTo(corner))
                                ? Line.CreateBound(tangentB, farB) : Line.CreateBound(farB, tangentB);

                            Element newArcElement;
                            if (elements[0] is DetailLine)
                            {
                                var view = Document.ActiveView;
                                newArcElement = Document.Create.NewDetailCurve(view, arc);
                            }
                            else
                            {
                                var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, corner);
                                var sketchPlane = SketchPlane.Create(Document, plane);
                                newArcElement = Document.Create.NewModelCurve(arc, sketchPlane);
                            }

                            t.Commit();
                            sb.AppendLine($"Filleted corner with a {radiusMm}mm-radius arc, Id {newArcElement.Id.IntegerValue}. Both source lines trimmed back to their tangent points.");
                        }
                        catch (Exception ex)
                        {
                            t.RollBack();
                            sb.AppendLine($"FAILED to fillet — rolled back, nothing changed. Reason: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"elbow\" or \"arc\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
