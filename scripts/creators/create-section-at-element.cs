// ============================================================
// FRAGMENT (creator) — create-section-at-element.cs
// PURPOSE: Cut a SECTION VIEW through each element in `elements`, aimed at it and sized around it —
//          "give me a section at every FCU", "section through each of these walls", "I need a cut at
//          every riser". One section per element, named after it.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs.
// PRODUCES: appends the new ViewSections to `elements`; sb reports each one.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ WHY THIS EXISTS ALONGSIDE creators/create-view.cs: that fragment's "section" mode takes a typed
//    mm box and is AXIS-ALIGNED ONLY. It cannot look at a wall running at 30 degrees, because a section
//    is defined by a TRANSFORM (an origin plus three axes), not by a box. This one builds that transform
//    from the element itself, so the cut faces the element square-on however it is rotated.
//
// ✱✱ HOW THE DIRECTION IS DECIDED, in this order — the part that is easy to get wrong:
//    1. The element has a LOCATION CURVE (wall, duct, pipe, beam): the curve's own direction is the
//       section's left-to-right axis, so the cut looks at the element side-on down its length.
//    2. The element has a LOCATION POINT with a rotation (most family instances): the bounding box's
//       X direction, TURNED BY THAT ROTATION. Skipping the rotation is the classic bug — the section
//       lands square to the world and slices the equipment at an angle.
//    3. Neither: fall back to world X. Reported per element so you can see which ones guessed.
//
// GOTCHA: THE SECTION IS SIZED FROM THE BOUNDING BOX IN MODEL SPACE, then padded by marginMm on every
//         side. A family whose box is much bigger than the visible geometry (a hosted family carrying an
//         invisible host reference) gives a section wider than expected — raise or lower marginMm rather
//         than assuming it failed.
// GOTCHA: `farClipMm` is the depth BEHIND the element that stays visible. Too small and the element
//         itself is clipped away; too large and every service behind it crowds the drawing.
// GOTCHA: Revit needs a section ViewFamilyType to exist in the project. If none does, this says so.
// GOTCHA: makes a view per element — 200 elements means 200 views. maxCreate is there for that reason.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. ViewSection.CreateSection is the same call
//   proven in create-view.cs. Run it on ONE element, look at the section, then do the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double marginMm = 500;         // padding added around the element on every side
double farClipMm = 1000;       // how deep behind the element the section still sees
string namePrefix = "Section"; // view name becomes "<prefix> - <element name or Id>"
bool dryRun = true;            // true = report what WOULD be cut, create nothing
int maxCreate = 10;            // hard cap — a section per element adds up fast; 0 = no cap
// ---- END INPUTS ----

Func<double, double> mm = v => v / 304.8;
var madeViews = new List<Element>();
int failed = 0, guessedDirection = 0;
var notes = new List<string>();

var sectionVft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);

if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this.");
}
else if (sectionVft == null)
{
    sb.AppendLine("This project has no Section view type, so no section can be created. "
                + "Add one in Revit (any section, then its type) and run again.");
}
else
{
    sb.AppendLine($"{elements.Count} element(s) in. Margin {marginMm} mm, far clip {farClipMm} mm.");
    if (maxCreate > 0 && elements.Count > maxCreate)
        sb.AppendLine($"CAP: only the first {maxCreate} will be cut (maxCreate). Raise it or set 0 for no cap.");
    sb.AppendLine();

    using (var t = new Transaction(Document, "AJ Tools - Section at element"))
    {
        if (!dryRun) t.Start();

        foreach (var el in elements.ToList())
        {
            if (maxCreate > 0 && madeViews.Count >= maxCreate) break;
            if (el is View) continue; // a filter that returned views would otherwise section a view

            string label = string.IsNullOrWhiteSpace(el.Name) ? $"Id {el.Id.IntegerValue}" : el.Name;

            var bb = el.get_BoundingBox(null);
            if (bb == null)
            {
                failed++; notes.Add($"  no geometry (nothing to aim at): {label}");
                continue;
            }

            // ---- 1/2/3: work out the left-to-right axis of the cut ----
            XYZ dir = null;
            string how;
            var loc = el.Location;

            if (loc is LocationCurve && ((LocationCurve)loc).Curve != null)
            {
                var c = ((LocationCurve)loc).Curve;
                var a = c.GetEndPoint(0); var b = c.GetEndPoint(1);
                var flat = new XYZ(b.X - a.X, b.Y - a.Y, 0);   // flatten: a section stands upright
                if (flat.GetLength() > 1e-9) { dir = flat.Normalize(); how = "along its length"; }
                else { dir = XYZ.BasisX; how = "curve too short — world X"; guessedDirection++; }
            }
            else if (loc is LocationPoint)
            {
                double rot = 0;
                try { rot = ((LocationPoint)loc).Rotation; } catch { }
                // World X turned by the instance's own rotation — see note 2.
                dir = new XYZ(Math.Cos(rot), Math.Sin(rot), 0).Normalize();
                how = rot == 0 ? "facing world X (no rotation on it)" : $"turned {(rot * 180.0 / Math.PI):F1}° with the element";
            }
            else
            {
                dir = XYZ.BasisX; how = "no location — world X"; guessedDirection++;
            }

            var up = XYZ.BasisZ;
            var viewDir = dir.CrossProduct(up).Normalize();  // the way the section looks

            var centre = (bb.Min + bb.Max) / 2.0;

            // Size the crop in the section's OWN axes, not the world's.
            var corners = new List<XYZ>();
            foreach (var x in new[] { bb.Min.X, bb.Max.X })
                foreach (var y in new[] { bb.Min.Y, bb.Max.Y })
                    foreach (var z in new[] { bb.Min.Z, bb.Max.Z })
                        corners.Add(new XYZ(x, y, z));

            double halfW = 0, halfH = 0, halfD = 0;
            foreach (var c in corners)
            {
                var v = c - centre;
                halfW = Math.Max(halfW, Math.Abs(v.DotProduct(dir)));
                halfH = Math.Max(halfH, Math.Abs(v.DotProduct(up)));
                halfD = Math.Max(halfD, Math.Abs(v.DotProduct(viewDir)));
            }

            double m = mm(marginMm);
            var tf = Transform.Identity;
            tf.Origin = centre;
            tf.BasisX = dir;
            tf.BasisY = up;
            tf.BasisZ = viewDir;

            var box = new BoundingBoxXYZ();
            box.Transform = tf;
            box.Min = new XYZ(-(halfW + m), -(halfH + m), -(halfD + m));
            box.Max = new XYZ(  halfW + m,    halfH + m,   halfD + mm(farClipMm));

            if (dryRun)
            {
                sb.AppendLine($"  would cut at '{label}' — {how}, "
                            + $"{(halfW * 2 * 304.8):F0} x {(halfH * 2 * 304.8):F0} mm + margin");
                madeViews.Add(el); // counted only
                continue;
            }

            try
            {
                var vs = ViewSection.CreateSection(Document, sectionVft.Id, box);
                if (vs == null) { failed++; notes.Add($"  Revit returned no section: {label}"); continue; }

                var wanted = $"{namePrefix} - {label}";
                for (int i = 0; i < 20; i++)
                {
                    try { vs.Name = i == 0 ? wanted : $"{wanted} ({i})"; break; }
                    catch { /* name taken — try the next suffix */ }
                }

                madeViews.Add(vs);
                sb.AppendLine($"  '{label}' -> section '{vs.Name}' ({how})");
            }
            catch (Exception ex)
            {
                failed++;
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                notes.Add($"  FAILED: {label} — {msg}");
            }
        }

        if (!dryRun) t.Commit();
    }

    sb.AppendLine();
    if (dryRun)
    {
        sb.AppendLine($"DRY RUN — no views created. {madeViews.Count} section(s) would be cut. Set dryRun = false.");
        madeViews.Clear();
    }
    else
    {
        sb.AppendLine($"Created {madeViews.Count} section view(s). Failed: {failed}.");
        elements.AddRange(madeViews);
    }
    if (guessedDirection > 0)
        sb.AppendLine($"{guessedDirection} element(s) had no usable direction and were cut facing world X — check those.");
    foreach (var n in notes) sb.AppendLine(n);
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
