// ============================================================
// FRAGMENT (action) — action-snap-to-ceiling-grid.cs
// PURPOSE: Move each point-based element in `elements` sideways onto the nearest CEILING TILE CENTRE —
//          diffusers, sprinklers, light fittings, smoke detectors sitting neatly on the grid instead of
//          "somewhere in the room". PLAN MOVE ONLY: each element keeps its own Z.
//          The tile size is READ FROM THE CEILING, never assumed 600x600 — see the header notes.
//          Pairs with `action-move-to-ray-hit.cs`, which fixes HEIGHT (snap up to the ceiling). A full
//          job usually wants both: grid-snap in plan here, ray-snap in Z there.
// ASSUMES: elements (List<Element>, point-based) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/ceiling-grid.md — read it before changing the detection order.
//
// GOTCHA: DRY RUN BY DEFAULT. This moves real geometry in bulk. Show the user the distances, get a
//         go-ahead, then rerun with dryRun=false. Same explorer-first rule as every bulk fragment here.
// GOTCHA: THE ANCHOR IS THE WEAK POINT, AND IT FAILS SILENTLY. Tile SIZE is in the model; where the grid
//         actually STARTS is not, on any Revit before 2025.3 — the interactive tool asks the user to
//         click a grid intersection for exactly this reason. With no anchor supplied this uses a corner
//         of the ceiling's own face, which gives evenly spaced centres PARALLEL to the real grid but
//         possibly half a tile out from what is drawn on screen. Nothing crashes; it just lands on a
//         neat WRONG grid. Check one element by eye before running the batch, and read the reported
//         anchor source.
// GOTCHA: the surface pattern must be a MODEL pattern (`FillPatternTarget.Model`). A drafting pattern's
//         Offset is a PAPER distance that changes with view scale — taking it as a tile size gives a
//         number that moves when the view scale does. This fragment skips drafting patterns.
// GOTCHA: "which elements are over this ceiling" is tested against the ceiling's real largest horizontal
//         FACE, not its bounding box. On an L-shaped ceiling a bounding box covers the missing corner
//         and sweeps up elements from the next room. `Face.Project` returns null outside the trimmed
//         face — that is an exact contains test, and it is cheap.
// GOTCHA: pinned elements are skipped. `MoveElement` returns normally on a pinned element while moving
//         NOTHING — see knowledge/live-model/geometry-and-transforms.md.
// GOTCHA: a tile size under 15 mm or over 10 m is rejected as a bad pattern read rather than used. A
//         misread pattern gives an absurd number, not a plausible one, so this catches it before 200
//         diffusers move.
// NOTE:   Revit 2025.3+ can read the ceiling's REAL grid lines (`Ceiling.GetCeilingGridLines`), which
//         also yields a true anchor for free. That call does not exist on older versions and would stop
//         this fragment compiling there, so it is deliberately NOT used — the type-pattern path below
//         works on every version. The clustering method is written up in the knowledge note if the day
//         comes to add it.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Ceiling Magnet tool, whose
//   detection order, face test and snap math have been used on real jobs. Run it on ONE element first,
//   look at where it landed, then trust it for the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report the moves only; false = actually move
int ceilingIdInt = 0;                // the Ceiling to read the grid from; 0 = use the one under the FIRST element
bool onlyElementsOverCeiling = true; // true = ignore elements that don't sit over this ceiling (face test)
double anchorXMm = double.NaN;       // a known TILE INTERSECTION in mm; NaN = guess from the ceiling's face corner
double anchorYMm = double.NaN;       // (both must be supplied to be used)
double fallbackTileMm = 600.0;       // only used when the ceiling type has no usable model pattern
// ---- END INPUTS ----

const double MM = 304.8;
const double TOL = 1e-9;

// ---------- 1. resolve the ceiling ----------
Ceiling ceiling = null;
if (ceilingIdInt > 0)
{
    ceiling = Document.GetElement(new ElementId(ceilingIdInt)) as Ceiling;
    if (ceiling == null) { sb.AppendLine("Element " + ceilingIdInt + " is not a Ceiling."); return sb.ToString(); }
}
else
{
    // No ceiling named — find the one whose face contains the first element's plan point.
    Element probe = elements.FirstOrDefault(e => e.Location is LocationPoint);
    if (probe == null) { sb.AppendLine("No point-based element in `elements` to locate a ceiling from."); return sb.ToString(); }
    XYZ probePt = ((LocationPoint)probe.Location).Point;

    foreach (Ceiling c in new FilteredElementCollector(Document)
                              .OfCategory(BuiltInCategory.OST_Ceilings)
                              .WhereElementIsNotElementType()
                              .Cast<Ceiling>())
    {
        BoundingBoxXYZ bb = c.get_BoundingBox(null);
        if (bb == null) continue;
        if (probePt.X >= bb.Min.X && probePt.X <= bb.Max.X && probePt.Y >= bb.Min.Y && probePt.Y <= bb.Max.Y)
        { ceiling = c; break; }
    }
    if (ceiling == null) { sb.AppendLine("Could not find a ceiling over the first element — pass ceilingIdInt explicitly."); return sb.ToString(); }
}

// ---------- 2. read the tile size + angle from the ceiling TYPE's model surface pattern ----------
double tileU = 0, tileV = 0, gridAngle = 0;
string sizeSource = "600 mm fallback (GUESS — no usable model pattern on the ceiling type)";

CeilingType ceilingType = Document.GetElement(ceiling.GetTypeId()) as CeilingType;
CompoundStructure cs = ceilingType == null ? null : ceilingType.GetCompoundStructure();
if (cs != null)
{
    foreach (CompoundStructureLayer layer in cs.GetLayers())
    {
        Material material = Document.GetElement(layer.MaterialId) as Material;
        if (material == null) continue;

        ElementId patternId = material.SurfaceForegroundPatternId;
        if (patternId == null || patternId == ElementId.InvalidElementId) continue;

        FillPatternElement fpe = Document.GetElement(patternId) as FillPatternElement;
        FillPattern pattern = fpe == null ? null : fpe.GetFillPattern();
        if (pattern == null) continue;
        if (pattern.Target != FillPatternTarget.Model) continue;   // drafting pattern = paper distance, not a tile

        IList<FillGrid> fillGrids = pattern.GetFillGrids();
        if (fillGrids == null || fillGrids.Count < 2) continue;
        if (fillGrids[0].Offset <= 0 || fillGrids[1].Offset <= 0) continue;

        tileU = fillGrids[0].Offset;
        tileV = fillGrids[1].Offset;
        gridAngle = fillGrids[0].Angle;
        sizeSource = "ceiling type surface pattern \"" + fpe.Name + "\"";
        break;
    }
}

if (tileU <= 0 || tileV <= 0) { tileU = fallbackTileMm / MM; tileV = tileU; gridAngle = 0; }

// sanity — a misread pattern gives an absurd number, not a plausible one
double minTileFt = 15.0 / MM, maxTileFt = 10000.0 / MM;
if (tileU < minTileFt || tileU > maxTileFt || tileV < minTileFt || tileV > maxTileFt)
{
    sb.AppendLine("Rejected the tile size read from this ceiling: " + Math.Round(tileU * MM, 1) + " x " +
                  Math.Round(tileV * MM, 1) + " mm is not a plausible ceiling tile. Nothing moved.");
    return sb.ToString();
}

XYZ axisU = new XYZ(-Math.Sin(gridAngle), Math.Cos(gridAngle), 0);
XYZ axisV = new XYZ(Math.Cos(gridAngle), Math.Sin(gridAngle), 0);

// ---------- 3. the ceiling's largest horizontal face — used for BOTH the contains test and the anchor ----------
PlanarFace bestFace = null;
double bestArea = 0;
Options geomOptions = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
GeometryElement geom = ceiling.get_Geometry(geomOptions);
if (geom != null)
{
    foreach (GeometryObject go in geom)
    {
        Solid solid = go as Solid;
        if (solid == null) continue;
        foreach (Face face in solid.Faces)
        {
            PlanarFace pf = face as PlanarFace;
            if (pf == null) continue;
            if (Math.Abs(Math.Abs(pf.FaceNormal.Z) - 1.0) > 0.01) continue;   // horizontal only
            if (pf.Area > bestArea) { bestArea = pf.Area; bestFace = pf; }
        }
    }
}

// ---------- 4. the anchor ----------
XYZ anchor;
string anchorSource;
if (!double.IsNaN(anchorXMm) && !double.IsNaN(anchorYMm))
{
    anchor = new XYZ(anchorXMm / MM, anchorYMm / MM, 0);
    anchorSource = "supplied (" + anchorXMm + ", " + anchorYMm + " mm)";
}
else
{
    BoundingBoxXYZ cbb = ceiling.get_BoundingBox(null);
    anchor = cbb != null ? new XYZ(cbb.Min.X, cbb.Min.Y, 0) : XYZ.Zero;
    anchorSource = "GUESSED from the ceiling's corner — centres are parallel to the real grid but may sit half a tile out";
}

// ---------- 5. which elements are actually over this ceiling ----------
var candidates = new List<Element>();
foreach (Element e in elements)
{
    LocationPoint lp = e == null ? null : e.Location as LocationPoint;
    if (lp == null) continue;

    if (!onlyElementsOverCeiling) { candidates.Add(e); continue; }

    if (bestFace != null)
    {
        // Face.Project returns null OUTSIDE the trimmed face — an exact contains test, L-shapes included.
        XYZ probePoint = new XYZ(lp.Point.X, lp.Point.Y, bestFace.Origin.Z);
        if (bestFace.Project(probePoint) != null) candidates.Add(e);
    }
    else
    {
        BoundingBoxXYZ cbb = ceiling.get_BoundingBox(null);   // defensive fallback only
        if (cbb != null && lp.Point.X >= cbb.Min.X && lp.Point.X <= cbb.Max.X &&
            lp.Point.Y >= cbb.Min.Y && lp.Point.Y <= cbb.Max.Y) candidates.Add(e);
    }
}

// ---------- 6. snap each to the nearest tile CENTRE ----------
int moved = 0, aligned = 0, skippedPinned = 0;
double maxMoveMm = 0;
var rows = new List<string>();

foreach (Element e in candidates)
{
    LocationPoint lp = (LocationPoint)e.Location;
    if (e.Pinned) { skippedPinned++; continue; }

    XYZ current = lp.Point;
    XYZ rel = current - anchor;
    double u = rel.DotProduct(axisU);
    double v = rel.DotProduct(axisV);

    // nearest TILE CENTRE, not nearest grid line: centre = step/2 + n*step
    double uSnap = (tileU * 0.5) + (Math.Round((u - (tileU * 0.5)) / tileU) * tileU);
    double vSnap = (tileV * 0.5) + (Math.Round((v - (tileV * 0.5)) / tileV) * tileV);

    XYZ delta = axisU.Multiply(uSnap).Add(axisV.Multiply(vSnap));
    XYZ target = new XYZ(anchor.X + delta.X, anchor.Y + delta.Y, current.Z);   // Z untouched — plan move only
    XYZ move = target - current;
    double moveMm = move.GetLength() * MM;

    if (move.GetLength() <= TOL) { aligned++; continue; }

    if (moveMm > maxMoveMm) maxMoveMm = moveMm;
    if (rows.Count < 25)
        rows.Add(e.Id + " | " + (e.Name ?? "") + " | " + Math.Round(moveMm, 1));

    if (!dryRun) { ElementTransformUtils.MoveElement(Document, e.Id, move); }
    moved++;
}

// ---------- 7. report ----------
sb.AppendLine((dryRun ? "DRY RUN — nothing moved. " : "") + "Ceiling " + ceiling.Id +
              " (" + (ceilingType != null ? ceilingType.Name : "?") + ")");
sb.AppendLine("Tile size : " + Math.Round(tileU * MM, 1) + " x " + Math.Round(tileV * MM, 1) +
              " mm at " + Math.Round(gridAngle * 180.0 / Math.PI, 2) + " deg — from " + sizeSource);
sb.AppendLine("Anchor    : " + anchorSource);
sb.AppendLine("Over this ceiling: " + candidates.Count + " of " + elements.Count + " element(s)" +
              (bestFace == null ? "  (bounding-box fallback — no usable ceiling face found)" : "  (exact face test)"));
sb.AppendLine((dryRun ? "Would move" : "Moved") + ": " + moved + " | already on centre: " + aligned +
              " | skipped (pinned): " + skippedPinned);
if (moved > 0) sb.AppendLine("Largest move: " + Math.Round(maxMoveMm, 1) + " mm");

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Id | Name | Move (mm)");
    sb.AppendLine("--- | --- | ---");
    foreach (string r in rows) sb.AppendLine(r);
    if (moved > rows.Count) sb.AppendLine("... and " + (moved - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
