// ============================================================
// FRAGMENT (creator) — create-dimension.cs
// PURPOSE: Create one dimension string across 2+ Grids and/or Levels, in a given view.
// PRODUCES: elements (List<Element>, the single newly created Dimension, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// SCOPE, READ BEFORE USING: this is DELIBERATELY LIMITED to Grids and Levels, not arbitrary elements
// (walls, ducts, etc.). Revit's NewDimension needs real geometric References — a whole-element Reference
// (new Reference(element)) is valid for Grid/Level/ReferencePlane, which is why those are the safe,
// reliably-correct case here. Dimensioning to a wall face, duct end, or other element geometry needs a
// Face/Edge Reference built from that element's own geometry, which this fragment does NOT attempt — that
// would need a separate, more fragile fragment built and verified against the specific geometry involved.
// GOTCHA: `line` below is NOT the thing being measured — it's the line ALONG which the dimension text and
// witness lines are drawn/placed in the view (Revit infers the measurement itself from the References'
// own positions). Give it two points roughly where you want the dimension string to visually sit.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
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
int viewIdInt = 0; // 0 = use Document.ActiveView
List<int> gridOrLevelIdInts = new List<int> { }; // 2+ Grid and/or Level element Ids, in the order to dimension
double lineP1XMm = 0, lineP1YMm = 0;   // where the dimension string starts (view placement, not measurement)
double lineP2XMm = 1000, lineP2YMm = 0; // where it ends
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else if (gridOrLevelIdInts.Count < 2)
{
    sb.AppendLine("Need at least 2 Grid/Level Ids in gridOrLevelIdInts to make a dimension string.");
}
else
{
    var refArray = new ReferenceArray();
    var missing = new List<int>();
    foreach (var idInt in gridOrLevelIdInts)
    {
        var el = Document.GetElement(new ElementId(idInt));
        if (el is Grid || el is Level) refArray.Append(new Reference(el));
        else missing.Add(idInt);
    }

    if (missing.Count > 0)
    {
        sb.AppendLine($"Id(s) not a Grid or Level, skipped: {string.Join(", ", missing)}.");
    }

    if (refArray.Size < 2)
    {
        sb.AppendLine("Fewer than 2 valid Grid/Level references remain — nothing to dimension.");
    }
    else
    {
        Func<double, double> mm = v => v / 304.8;
        var p1 = new XYZ(mm(lineP1XMm), mm(lineP1YMm), 0);
        var p2 = new XYZ(mm(lineP2XMm), mm(lineP2YMm), 0);

        if ((p2 - p1).GetLength() < 1e-6)
        {
            sb.AppendLine("Dimension line points are the same point — set two distinct points.");
        }
        else
        {
            var dimLine = Line.CreateBound(p1, p2);

            using (var t = new Transaction(Document, "AJ Tools - Create Dimension"))
            {
                t.Start();
                try
                {
                    var dim = Document.Create.NewDimension(view, dimLine, refArray);
                    elements.Add(dim);
                    t.Commit();
                    sb.AppendLine($"Created a dimension string across {refArray.Size} Grid/Level reference(s) in view '{view.Name}'.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to create dimension — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
