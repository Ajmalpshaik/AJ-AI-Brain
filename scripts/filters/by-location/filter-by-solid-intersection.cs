// ============================================================
// FRAGMENT (filter) — filter-by-solid-intersection.cs
// PURPOSE: Elements whose real geometry (not just its bounding box) intersects a custom 3D solid — a
//          clearance zone, an equipment-access envelope, a maintenance zone. More accurate than
//          filter-by-region.cs, which only tests bounding-box overlap and can false-positive on a
//          rotated/sloped element that overlaps the box's bounding volume without actually touching it.
//          The default solid here is a simple axis-aligned box built from mm coordinates — swap the
//          box-build block for a different profile if the clearance zone isn't a plain box.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: live-verified 2026-07-22 (ElementIntersectsSolidFilter path). CAVEAT: the box-solid
//         construction via GeometryCreationUtilities has still not been run against a real model —
//         verify that on one element before trusting it for a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double minXmm = 0, minYmm = 0, minZmm = 0;
double maxXmm = 1000, maxYmm = 1000, maxZmm = 2000;
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<double, double> mm = v => v / 304.8;

XYZ p0 = new XYZ(mm(minXmm), mm(minYmm), mm(minZmm));
double dx = mm(maxXmm) - mm(minXmm);
double dy = mm(maxYmm) - mm(minYmm);
double dz = mm(maxZmm) - mm(minZmm);

var profile = new List<Curve>
{
    Line.CreateBound(p0, p0 + new XYZ(dx, 0, 0)),
    Line.CreateBound(p0 + new XYZ(dx, 0, 0), p0 + new XYZ(dx, dy, 0)),
    Line.CreateBound(p0 + new XYZ(dx, dy, 0), p0 + new XYZ(0, dy, 0)),
    Line.CreateBound(p0 + new XYZ(0, dy, 0), p0),
};
var loop = CurveLoop.Create(profile);
Solid box = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, dz);

var intersectFilter = new ElementIntersectsSolidFilter(box);
var collector = new FilteredElementCollector(Document).WherePasses(intersectFilter).WhereElementIsNotElementType();
if (useCategoryFilter) collector = collector.OfCategory(targetCategory);

List<Element> elements = collector.ToList();
string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
sb.AppendLine($"Filtered {elements.Count} element(s) whose real geometry intersects the given box solid, candidates: {categoryLabel}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
