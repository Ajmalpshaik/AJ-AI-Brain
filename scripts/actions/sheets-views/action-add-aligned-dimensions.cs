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
// GOTCHA: this is FamilyInstance-only — `elements` that are Walls, Floors, Ducts or any system family
//         yield `fi == null` and are all skipped. To dimension walls, use create-dimension.cs (grids/levels)
//         or dimension to their faces; do not point this fragment at them expecting a result.
// LIVE-VERIFIED 2026-08-14 — 3x M_Supply Diffuser at X 21500/24000/26500 mm, axis "x", offset 1000 mm, in a
//         FloorPlan view. Created dimension id 918995 with 3 references and 2 segments reading 2500.0 mm
//         each — the correct spacing. Confirmed from a SEPARATE bridge call: the element survived commit,
//         so the degenerate-dimension-deleted-at-commit risk did not fire on this fixture.
// MEASURED 2026-08-14, which reference types real content actually exposes — the header guess below was
//         right, and the numbers are worth keeping:
//           M_Supply Diffuser (stock Revit content) -> CenterLeftRight 1, CenterFrontBack 1, Strong 0, Weak 0
//           M_* duct fittings, duct accessories     -> ALL FOUR ZERO. This fragment can never dimension them.
//         So "no usable reference" on MEP fittings is expected, not a bug. Air terminals / equipment work.
// ✱ CORRECTED 2026-08-22 — the measurement above is right, the CONCLUSION drawn from it was too broad.
//   "All four zero" is true of the `FamilyInstance.GetReferences` ROUTE, not of MEP as a subject: ducts,
//   pipes, conduit and trays CAN be dimensioned by walking their geometry for a `.Reference`, provided
//   `Options.IncludeNonVisibleObjects = true` (a run's CENTRELINE is a non-visible object) alongside
//   `ComputeReferences = true`. That is a different fragment —
//   `action-dimension-mep-runs.cs` — and the full method is in knowledge/live-model/dimensioning.md.
//   Keep using THIS one for air terminals and equipment; send MEP runs to that one.
// GOTCHA: reading a created dimension back, `Dimension.Curve` throws "The input curve is not bound" —
//         use `NumberOfSegments` / `Segments` / `References` to verify instead, never `.Curve`.
//
// ✱✱ SIX FRAGMENTS PUT A DIMENSION ON A DRAWING AND THEY ARE NOT INTERCHANGEABLE. Pick deliberately:
//      creators/create-dimension.cs                            GRIDS and LEVELS only.
//      actions/sheets-views/action-add-aligned-dimensions.cs   between FAMILY INSTANCES, via
//                                                              GetReferences. MEP runs expose none.
//      actions/sheets-views/action-dimension-mep-runs.cs       between DUCTS, PIPES, CONDUIT, TRAY.
//                                                              "dimension between the ducts", "put a
//                                                              dimension between the services", "how
//                                                              far apart are these on the drawing".
//                                                              Walks the geometry, the only route
//                                                              that works for MEP.
//      actions/sheets-views/action-dimension-rooms.cs          each ROOM width and depth, wall face to
//                                                              wall face. Project X/Y only.
//      actions/sheets-views/action-dimension-wall-openings.cs  along a WALL, picking up every door and
//                                                              window opening in it.
//      actions/reporting/action-report-room-dimensions.cs      READ-ONLY. Asks, never draws.
//
// ✱✱ MEASURING IS NOT DIMENSIONING, AND THE SAME SENTENCE ASKS FOR BOTH. "How much gap is between
//    those two ducts" is a CLEARANCE question, wants a number, must change nothing:
//    actions/qa-checks/action-report-mep-clearance.cs. "Put a dimension between those two ducts" is
//    this group, and it WRITES Dimension elements into the view. Measured 2026-08-24: the plain phrase
//    "dimension between the ducts" returned two clearance reports at the top and
//    action-dimension-mep-runs.cs nowhere at all, which is why the spoken phrasings are written above.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;              // the view the dimension is drawn in
string axis = "x";              // "x" | "y" — direction the dimension string runs
double offsetMm = 1000;         // how far off the elements the dimension line sits (perpendicular)
// ---- END INPUTS ----

Func<double, double> mm = v => v / 304.8;

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
        if (chosen == null) { notes.Add($"Id {el.Id}: no usable reference"); continue; }
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
                sb.AppendLine($"Created aligned dimension (Id {dim.Id}) across {refArray.Size} element(s) along {axis.ToUpper()} in view '{dimView.Name}', offset {offsetMm} mm.");
                sb.AppendLine($"  Dimensioned: {string.Join(", ", used.Take(20).Select(e => $"Id {e.Id}"))}{(used.Count > 20 ? ", ..." : "")}");
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
