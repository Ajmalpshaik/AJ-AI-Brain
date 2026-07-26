// ============================================================
// FRAGMENT (action) — action-add-aligned-dimensions.cs
// PURPOSE: Dimension between the elements in `elements` — one aligned dimension string through all of
//          them along a chosen axis. Extends dimensioning beyond create-dimension.cs, which is
//          deliberately Grid/Level only.
// ASSUMES: elements (List<Element>, 2+ FamilyInstances — terminals, fixtures, equipment) and sb exist
//          from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: dimensioning needs a geometry REFERENCE, not a point. This uses the family instance's own
//         built-in centre references (FamilyInstanceReferenceType.StrongReference / CenterLeftRight /
//         CenterFrontBack), which is what makes a dimension actually hold when the element moves.
//         Families whose author never marked reference planes as references yield nothing — those
//         elements are skipped and REPORTED, and if fewer than 2 survive no dimension is created.
// GOTCHA: axis "x" dimensions horizontal spacing, "y" vertical-in-plan; elements are sorted along that
//         axis first so the chain reads left-to-right / bottom-to-top like a drafter would place it.
// FLAGGED 2026-07-26 (static review): which reference types a given family exposes varies by how the
//         family was authored. If whole categories come back "no usable reference", that's family
//         authoring, not this fragment — dimension to grids instead (create-dimension.cs).
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;              // the view the dimension is drawn in
string axis = "x";              // "x" | "y" — direction the dimension string runs
double offsetMm = 1000;         // how far off the elements the dimension line sits (perpendicular)
// ---- END INPUTS ----

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

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
    // sort along the dimension axis so the chain reads in drawing order
    Func<Element, double> keyOf = el =>
    {
        var lp = el.Location as LocationPoint;
        if (lp != null) return axis == "y" ? lp.Point.Y : lp.Point.X;
        var bb = el.get_BoundingBox(null);
        if (bb != null) { var c = (bb.Min + bb.Max) / 2.0; return axis == "y" ? c.Y : c.X; }
        return 0;
    };
    var ordered = elements.OrderBy(keyOf).ToList();

    var refArray = new ReferenceArray();
    var used = new List<Element>();
    var notes = new List<string>();

    var wantTypes = axis == "y"
        ? new[] { FamilyInstanceReferenceType.CenterFrontBack, FamilyInstanceReferenceType.StrongReference, FamilyInstanceReferenceType.WeakReference }
        : new[] { FamilyInstanceReferenceType.CenterLeftRight, FamilyInstanceReferenceType.StrongReference, FamilyInstanceReferenceType.WeakReference };

    foreach (var el in ordered)
    {
        var fi = el as FamilyInstance;
        Reference chosen = null;
        if (fi != null)
        {
            foreach (var rt in wantTypes)
            {
                var refs = fi.GetReferences(rt);
                if (refs != null && refs.Count > 0) { chosen = refs.First(); break; }
            }
        }
        if (chosen == null) { notes.Add($"Id {el.Id.IntegerValue}: no usable reference"); continue; }
        refArray.Append(chosen);
        used.Add(el);
    }

    if (refArray.Size < 2)
    {
        sb.AppendLine($"Only {refArray.Size} element(s) exposed a usable reference — need 2. Nothing created.");
        if (notes.Count > 0) sb.AppendLine("  " + string.Join("; ", notes.Take(15)));
        sb.AppendLine("  See the header: this is family authoring, not a script bug — dimension to grids instead (create-dimension.cs).");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Add Aligned Dimension"))
        {
            t.Start();
            try
            {
                double first = keyOf(used.First()), last = keyOf(used.Last());
                var anchor = used.First().get_BoundingBox(null);
                double perp = anchor != null
                    ? (axis == "y" ? anchor.Min.X : anchor.Min.Y) - mm(offsetMm)
                    : -mm(offsetMm);
                double z = anchor != null ? anchor.Min.Z : 0;

                Line dimLine = axis == "y"
                    ? Line.CreateBound(new XYZ(perp, first, z), new XYZ(perp, last, z))
                    : Line.CreateBound(new XYZ(first, perp, z), new XYZ(last, perp, z));

                var dim = Document.Create.NewDimension(dimView, dimLine, refArray);
                t.Commit();
                sb.AppendLine($"Created aligned dimension (Id {dim.Id.IntegerValue}) across {refArray.Size} element(s) along {axis.ToUpper()} in view '{dimView.Name}', offset {offsetMm} mm.");
                sb.AppendLine($"  Dimensioned: {string.Join(", ", used.Take(20).Select(e => $"Id {e.Id.IntegerValue}"))}{(used.Count > 20 ? ", ..." : "")}");
                if (notes.Count > 0) sb.AppendLine("  Skipped: " + string.Join("; ", notes.Take(15)));
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to create dimension — rolled back, nothing created. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
