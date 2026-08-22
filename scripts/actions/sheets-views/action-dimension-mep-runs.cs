// ============================================================
// FRAGMENT (action) — action-dimension-mep-runs.cs
// PURPOSE: Dimension between DUCTS, PIPES, CONDUIT and CABLE TRAYS in one view — the spacing string
//          across a corridor of services. Gets its references by WALKING THE GEOMETRY, which is the
//          only route that works for MEP, and fills the gap the Brain's other two dimension fragments
//          could not: `create-dimension.cs` is grids/levels only, and `action-add-aligned-dimensions.cs`
//          uses `FamilyInstance.GetReferences`, which MEP runs and fittings expose NONE of (measured
//          in this Brain 2026-08-14).
// ASSUMES: elements (List<Element>, 2+ MEP curves) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/dimensioning.md — read it; it carries six defects this avoids.
//
// GOTCHA: `IncludeNonVisibleObjects = true` IS WHAT MAKES THIS WORK AT ALL. A duct's CENTRELINE is a
//         NON-VISIBLE geometry object. Leave that option out and a round pipe yields nothing and it
//         looks like the API cannot do it. `ComputeReferences = true` is equally required — without it
//         every `.Reference` comes back null.
// GOTCHA: `new Reference(element)` IS NOT A SAFE FALLBACK. `NewDimension` accepts a whole-element
//         reference for a DATUM, but for a pipe or duct it throws "the references are not geometric
//         references" — and in a chained dimension THAT ONE BAD REFERENCE TAKES DOWN EVERY GOOD ONE
//         sharing the array. An element with no usable reference is dropped and named instead.
// GOTCHA: `LocationCurve.Curve.Reference` is tried first but is USUALLY NULL on MEP curves. The
//         geometry walk is what actually finds one — do not conclude the element has no reference from
//         the location curve alone.
// GOTCHA: geometry must be recursed into every `GeometryInstance` via `GetInstanceGeometry()`, or a
//         family-hosted run contributes nothing.
// GOTCHA: only `DimensionStyleType.Linear` / `LinearFixed` are legal for a linear dimension. Handing
//         `NewDimension` an angular or radial type is a guaranteed failure. `dimensionTypeName = ""`
//         uses the document default, which is the safe choice.
// GOTCHA: reading it back, `Dimension.Curve` THROWS "The input curve is not bound" — this reports
//         `NumberOfSegments` and `References` instead. Never call `.Curve` on a Dimension.
// GOTCHA: a dimension whose references are all at the same coordinate is degenerate and Revit may
//         delete it at commit. The count is read back after `Commit()` for exactly that reason.
// NOTE: `axis` is the direction the dimension string RUNS. Elements are sorted along it first so the
//       chain reads in drawing order.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Auto MEP Dimension
//   (~240 KB of dimension services, the largest area in that add-in). Compile-checked on
//   2020/2024/2027. Run it on TWO ducts first and look at the segment value.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;                   // REQUIRED — the view the dimension is drawn in
string axis = "x";                   // "x" | "y" — the direction the dimension string runs
double offsetMm = 1000;              // how far off the runs the dimension line sits
string dimensionTypeName = "";       // "" = document default (safest); else a LINEAR dimension type name
bool preferCentreline = true;        // true = centreline of each run; false = try a face first
// ---- END INPUTS ----

const double MM = 304.8;

var dimView = Document.GetElement(new ElementId(viewIdInt)) as View;

if (dimView == null || dimView.IsTemplate)
{
    sb.AppendLine($"viewIdInt {viewIdInt} is not a usable view.");
}
else if (elements.Count < 2)
{
    sb.AppendLine($"Need at least 2 elements to dimension between — got {elements.Count}.");
}
else
{
    bool byY = axis.Equals("y", StringComparison.OrdinalIgnoreCase);

    // ---------- the reference hunt ----------
    Func<Element, Reference> referenceOf = el =>
    {
        // 1) the location curve's own reference — cheap, usually null on MEP, worth trying
        var lc = el.Location as LocationCurve;
        if (lc != null && lc.Curve is Line)
        {
            try { if (lc.Curve.Reference != null) return lc.Curve.Reference; } catch { }
        }

        // 2) walk the geometry. BOTH options matter: ComputeReferences to get any reference at all,
        //    IncludeNonVisibleObjects because the CENTRELINE is a non-visible object.
        GeometryElement geo;
        try
        {
            geo = el.get_Geometry(new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            });
        }
        catch { return null; }
        if (geo == null) return null;

        Reference foundCurve = null, foundFace = null;
        Action<GeometryElement> walk = null;
        walk = g =>
        {
            foreach (GeometryObject go in g)
            {
                var inst = go as GeometryInstance;
                if (inst != null)
                {
                    GeometryElement nested = null;
                    try { nested = inst.GetInstanceGeometry(); } catch { }
                    if (nested != null) walk(nested);
                    continue;
                }

                var c = go as Curve;
                if (c != null && foundCurve == null)
                {
                    try { if (c.Reference != null) foundCurve = c.Reference; } catch { }
                    continue;
                }

                var s = go as Solid;
                if (s != null && foundFace == null)
                {
                    foreach (Face f in s.Faces)
                    {
                        var pf = f as PlanarFace;
                        if (pf == null) continue;
                        // a side face — its normal lies in plan, not up/down
                        if (Math.Abs(pf.FaceNormal.Z) > 0.1) continue;
                        try { if (pf.Reference != null) { foundFace = pf.Reference; break; } } catch { }
                    }
                }
            }
        };
        walk(geo);

        // Round pipes/conduit have NO planar side faces, so the centreline is the only route there.
        return preferCentreline ? (foundCurve ?? foundFace) : (foundFace ?? foundCurve);
    };

    Func<Element, double> keyOf = el =>
    {
        var lc = el.Location as LocationCurve;
        if (lc != null && lc.Curve != null)
        {
            var m = lc.Curve.Evaluate(0.5, true);
            return byY ? m.Y : m.X;
        }
        var bb = el.get_BoundingBox(null);
        if (bb != null) { var c = (bb.Min + bb.Max) / 2.0; return byY ? c.Y : c.X; }
        return 0;
    };

    var ordered = elements.OrderBy(keyOf).ToList();
    var refArray = new ReferenceArray();
    var used = new List<Element>();
    var dropped = new List<string>();

    foreach (var el in ordered)
    {
        var r = referenceOf(el);
        if (r == null)
        {
            // NOT falling back to new Reference(element): it throws for MEP and would take the whole
            // array down with it. Drop and name instead.
            if (dropped.Count < 20) dropped.Add(el.Id.ToString());
            continue;
        }
        refArray.Append(r);
        used.Add(el);
    }

    if (refArray.Size < 2)
    {
        sb.AppendLine($"Only {refArray.Size} of {elements.Count} element(s) gave a usable geometry " +
                      "reference — need 2. Nothing created.");
        if (dropped.Count > 0) sb.AppendLine("No reference found on ids: " + string.Join(", ", dropped));
        sb.AppendLine("If this view is COARSE, Revit draws MEP as single lines — try a Medium or Fine view.");
    }
    else
    {
        // Only Linear / LinearFixed types are legal for a linear dimension.
        DimensionType dimType = null;
        if (!string.IsNullOrWhiteSpace(dimensionTypeName))
        {
            dimType = new FilteredElementCollector(Document).OfClass(typeof(DimensionType)).Cast<DimensionType>()
                .FirstOrDefault(dt =>
                {
                    if (dt == null || !string.Equals(dt.Name, dimensionTypeName.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                    try { return dt.StyleType == DimensionStyleType.Linear || dt.StyleType == DimensionStyleType.LinearFixed; }
                    catch { return false; }
                });
            if (dimType == null)
                sb.AppendLine($"No LINEAR dimension type named '{dimensionTypeName}' — using the document default. " +
                              "(An angular or radial type would guarantee a failure.)");
        }

        ElementId madeId = null; int segments = -1, refsHeld = -1;

        using (var t = new Transaction(Document, "AJ Tools - Dimension MEP Runs"))
        {
            t.Start();
            try
            {
                double first = keyOf(used.First()), last = keyOf(used.Last());
                var bb0 = used.First().get_BoundingBox(null);
                double perp = (bb0 != null ? (byY ? bb0.Min.X : bb0.Min.Y) : 0) - offsetMm / MM;
                double z = bb0 != null ? bb0.Min.Z : 0;

                Line dimLine = byY
                    ? Line.CreateBound(new XYZ(perp, first, z), new XYZ(perp, last, z))
                    : Line.CreateBound(new XYZ(first, perp, z), new XYZ(last, perp, z));

                var dim = dimType != null
                    ? Document.Create.NewDimension(dimView, dimLine, refArray, dimType)
                    : Document.Create.NewDimension(dimView, dimLine, refArray);

                t.Commit();

                // Read back AFTER commit — a degenerate dimension can be deleted at commit, and
                // `.Curve` throws "The input curve is not bound", so never use it to verify.
                if (dim != null && dim.IsValidObject)
                {
                    madeId = dim.Id;
                    try { segments = dim.NumberOfSegments; } catch { }
                    try { refsHeld = dim.References != null ? dim.References.Size : -1; } catch { }
                }
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to create the dimension — rolled back, nothing created. Reason: {ex.Message}");
                return sb.ToString();
            }
        }

        if (madeId == null)
        {
            sb.AppendLine("The dimension did not survive the commit — Revit deletes a degenerate one " +
                          "(all references at the same coordinate). Check the runs are actually spaced along " +
                          axis.ToUpperInvariant() + ".");
        }
        else
        {
            sb.AppendLine($"Created dimension {madeId} across {refArray.Size} MEP run(s) along " +
                          $"{axis.ToUpperInvariant()} in view '{dimView.Name}', offset {offsetMm} mm.");
            sb.AppendLine($"Read back: {segments} segment(s), {refsHeld} reference(s) held.");
            sb.AppendLine("Dimensioned ids: " + string.Join(", ", used.Take(20).Select(e => e.Id.ToString())) +
                          (used.Count > 20 ? ", ..." : ""));
        }
        if (dropped.Count > 0)
            sb.AppendLine("No usable reference (dropped, NOT bodged with new Reference(element)): " +
                          string.Join(", ", dropped));
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
