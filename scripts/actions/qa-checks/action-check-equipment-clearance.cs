// ============================================================
// FRAGMENT (action) — action-check-equipment-clearance.cs
// PURPOSE: Check the MAINTENANCE AND ACCESS ZONE around each piece of equipment — the space in front for
//          pulling a coil or a filter, at the sides for the panels, above for the valves — and report
//          everything that has been modelled inside it. "Can this thing actually be serviced once the
//          building is built", answered from the model instead of on site.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          equipment, e.g. filter-by-category.cs with OST_MechanicalEquipment. Read-only.
//
// ✱✱ THE ZONE IS DIRECTIONAL, AND THAT IS THE WHOLE POINT. A plain "keep 1000 mm clear all round" sphere
//    is not how equipment is serviced: an AHU needs a metre and a half in FRONT for the coil withdrawal
//    and 200 mm behind it, and a check that cannot tell those apart either passes real obstructions or
//    fails on the wall the unit is meant to be against. Each face gets its own figure, and the zone is
//    built in the equipment's OWN frame, so it rotates with the unit.
//
// ✱✱ "FRONT" IS THE FAMILY'S FACING DIRECTION, and that is worth checking once per family. The zone is
//    built on the instance transform: +Y is facing, X is hand (left/right), Z is up. If a family was
//    built facing the wrong way, its front zone points into the wall and the check is confidently wrong.
//    The report prints each unit's facing vector so that is visible on the first run rather than never.
//
// ✱✱ THE ZONE IS A REAL SOLID AND REVIT DOES THE INTERSECTION. `ElementIntersectsSolidFilter` against an
//    extruded clearance box is exact geometry, not a bounding-box guess — so a duct clipping the corner
//    of the access zone is caught, and a duct passing near but outside it is not reported.
//
// ✱✱ THE EQUIPMENT'S OWN SERVICES ARE NOT OBSTRUCTIONS. The duct plugged into the unit necessarily
//    occupies the space right at its connector. Anything CONNECTED to the unit is excluded by default,
//    along with its own sub-components — otherwise every unit reports its own pipework as blocking it and
//    the check gets ignored.
//
// GOTCHA: LOCAL EXTENTS ARE MEASURED FROM REAL GEOMETRY, not from the world bounding box. For a unit
//         rotated off the project axes the world box is much larger than the unit, and a zone built on it
//         would start outside the equipment and miss close obstructions. Elements with no readable solid
//         are reported, not silently passed.
// GOTCHA: LINKED MODELS ARE NOT SCANNED. Structure and architecture usually live in links and are exactly
//         what blocks access — a clean result against a linked model has checked nothing. The count of
//         host-model candidates is printed for that reason.
// GOTCHA: THE FIGURES ARE YOURS (START-HERE.md rule 3). Manufacturer clearances differ per unit; the
//         defaults here are placeholders, not a standard. Set them per equipment type and run it per type.
// RELATED: action-check-minimum-clearance.cs (a plain gap in any direction),
//          filter-by-solid-intersection.cs (a hand-built clearance zone as an actionable set),
//          action-check-valve-accessibility.cs (the same question for valves and dampers).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check the reported facing vector on one unit
//   first; if the front zone points the wrong way, the family is the problem, not the check.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double frontMm = 1000;      // access space in the FAMILY'S facing direction — coil/filter withdrawal
double backMm = 200;
double leftMm = 500;
double rightMm = 500;
double topMm = 300;
double bottomMm = 0;        // usually 0 — the unit sits on something

bool excludeConnected = true;       // the unit's own ducts/pipes are not obstructions
bool excludeSubComponents = true;   // its own nested parts are not obstructions

// What can count as an obstruction. Keep it to things that really would block a person.
var obstructionCategories = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_CableTray,
    BuiltInCategory.OST_Conduit,
    BuiltInCategory.OST_Walls,
    BuiltInCategory.OST_StructuralFraming,
    BuiltInCategory.OST_StructuralColumns,
    BuiltInCategory.OST_MechanicalEquipment,
};
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the equipment).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

var geoOpts = new Options { DetailLevel = ViewDetailLevel.Medium, ComputeReferences = false, IncludeNonVisibleObjects = false };

// Every tessellated vertex of an element's solids, in world coordinates.
Func<Element, List<XYZ>> verticesOf = el =>
{
    var pts = new List<XYZ>();
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geoOpts); } catch { return pts; }
    if (ge == null) return pts;

    Action<GeometryElement> walk = null;
    walk = g =>
    {
        foreach (GeometryObject go in g)
        {
            var s = go as Solid;
            if (s != null && s.Volume > 1e-9)
            {
                foreach (Face f in s.Faces)
                {
                    Mesh m = null;
                    try { m = f.Triangulate(0.3); } catch { }
                    if (m == null) continue;
                    for (int i = 0; i < m.Vertices.Count; i++) pts.Add(m.Vertices[i]);
                }
                continue;
            }
            var gi = go as GeometryInstance;
            if (gi != null)
            {
                var inner = gi.GetInstanceGeometry();
                if (inner != null) walk(inner);
            }
        }
    };
    walk(ge);
    return pts;
};

// ---- candidate obstructions, once ----
var candidates = new List<Element>();
foreach (var cat in obstructionCategories)
{
    try { candidates.AddRange(new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType().ToList()); }
    catch { }
}

sb.AppendLine("EQUIPMENT ACCESS / MAINTENANCE ZONE");
sb.AppendLine($"Zone: front {frontMm:F0}, back {backMm:F0}, left {leftMm:F0}, right {rightMm:F0}, top {topMm:F0}, bottom {bottomMm:F0} mm");
sb.AppendLine($"Equipment: {elements.Count}   candidate obstructions in the HOST model: {candidates.Count} (links are NOT scanned)");
sb.AppendLine();

if (candidates.Count == 0)
{
    sb.AppendLine("STOP: nothing in the obstruction categories exists in the host model, so a clean result would mean nothing. Are the walls and structure in a LINK?");
    return sb.ToString();
}

// ---- per equipment ----
var findings = new List<(Element Eq, Element Blocker, string Face)>();
var noGeometry = new List<Element>();
var facingNotes = new List<string>();
int zonesBuilt = 0;

foreach (var eq in elements)
{
    var fi = eq as FamilyInstance;
    Transform xf = null;
    try { xf = fi != null ? fi.GetTransform() : Transform.Identity; }
    catch { xf = Transform.Identity; }
    if (xf == null) xf = Transform.Identity;

    var verts = verticesOf(eq);
    if (verts.Count == 0) { noGeometry.Add(eq); continue; }

    // True extents in the equipment's OWN frame — not the world bounding box, which is too big for
    // anything rotated off the project axes.
    var inv = xf.Inverse;
    double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
    double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
    foreach (var p in verts)
    {
        var lp = inv.OfPoint(p);
        if (lp.X < minX) minX = lp.X; if (lp.X > maxX) maxX = lp.X;
        if (lp.Y < minY) minY = lp.Y; if (lp.Y > maxY) maxY = lp.Y;
        if (lp.Z < minZ) minZ = lp.Z; if (lp.Z > maxZ) maxZ = lp.Z;
    }

    // Grow each face by its own figure. +Y is the family's facing direction.
    double gMinX = minX - ToFeet(leftMm),  gMaxX = maxX + ToFeet(rightMm);
    double gMinY = minY - ToFeet(backMm),  gMaxY = maxY + ToFeet(frontMm);
    double gMinZ = minZ - ToFeet(bottomMm), gMaxZ = maxZ + ToFeet(topMm);
    double height = gMaxZ - gMinZ;
    if (height <= 1e-6) { noGeometry.Add(eq); continue; }

    // The zone as a real solid, built in world coordinates from the local rectangle.
    Solid zone = null;
    try
    {
        var p0 = xf.OfPoint(new XYZ(gMinX, gMinY, gMinZ));
        var p1 = xf.OfPoint(new XYZ(gMaxX, gMinY, gMinZ));
        var p2 = xf.OfPoint(new XYZ(gMaxX, gMaxY, gMinZ));
        var p3 = xf.OfPoint(new XYZ(gMinX, gMaxY, gMinZ));

        var loop = new CurveLoop();
        loop.Append(Line.CreateBound(p0, p1));
        loop.Append(Line.CreateBound(p1, p2));
        loop.Append(Line.CreateBound(p2, p3));
        loop.Append(Line.CreateBound(p3, p0));

        zone = GeometryCreationUtilities.CreateExtrusionGeometry(
            new List<CurveLoop> { loop }, xf.BasisZ, height);
    }
    catch (Exception ex)
    {
        facingNotes.Add($"{eq.Id}: could not build the zone solid — {ex.Message}");
        continue;
    }
    if (zone == null) { noGeometry.Add(eq); continue; }
    zonesBuilt++;

    facingNotes.Add($"{eq.Id} ({eq.Name}): facing ({xf.BasisY.X:F2}, {xf.BasisY.Y:F2}, {xf.BasisY.Z:F2}) — the FRONT zone extends that way");

    // ---- what to ignore for this unit ----
    var ignore = new HashSet<long> { IdValue(eq.Id) };
    if (excludeConnected && fi != null && fi.MEPModel != null && fi.MEPModel.ConnectorManager != null)
    {
        try
        {
            foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
                foreach (Connector r in c.AllRefs)
                    if (r.Owner != null) ignore.Add(IdValue(r.Owner.Id));
        }
        catch { }
    }
    if (excludeSubComponents && fi != null)
    {
        try { foreach (var sid in fi.GetSubComponentIds()) ignore.Add(IdValue(sid)); }
        catch { }
        try { if (fi.SuperComponent != null) ignore.Add(IdValue(fi.SuperComponent.Id)); }
        catch { }
    }

    // ---- Revit does the intersection ----
    List<Element> hits;
    try
    {
        var solidFilter = new ElementIntersectsSolidFilter(zone);
        hits = new FilteredElementCollector(Document, candidates.Select(c => c.Id).ToList())
            .WherePasses(solidFilter).ToList();
    }
    catch (Exception ex)
    {
        facingNotes.Add($"{eq.Id}: intersection failed — {ex.Message}");
        continue;
    }

    foreach (var h in hits)
    {
        if (ignore.Contains(IdValue(h.Id))) continue;

        // Which face of the zone it sits against — the actionable half of the finding.
        string face = "inside the zone";
        try
        {
            BoundingBoxXYZ hb = h.get_BoundingBox(null);
            if (hb != null)
            {
                var c = inv.OfPoint((hb.Min + hb.Max) * 0.5);
                if (c.Y > maxY) face = "FRONT";
                else if (c.Y < minY) face = "back";
                else if (c.X > maxX) face = "right";
                else if (c.X < minX) face = "left";
                else if (c.Z > maxZ) face = "above";
                else if (c.Z < minZ) face = "below";
            }
        }
        catch { }

        findings.Add((eq, h, face));
    }
}

// ---- report ----
sb.AppendLine($"ZONES BUILT: {zonesBuilt}   OBSTRUCTIONS FOUND: {findings.Count}");
if (noGeometry.Count > 0)
    sb.AppendLine($"NO USABLE GEOMETRY: {noGeometry.Count} item(s) — NOT checked and NOT a pass: " +
                  string.Join(", ", noGeometry.Take(15).Select(e => e.Id.ToString())) + (noGeometry.Count > 15 ? " ..." : ""));
sb.AppendLine();

sb.AppendLine("FACING CHECK — confirm these point the way you expect before trusting the front-zone results:");
foreach (var n in facingNotes.Take(10)) sb.AppendLine($"  {n}");
if (facingNotes.Count > 10) sb.AppendLine($"  ... and {facingNotes.Count - 10} more");
sb.AppendLine();

if (findings.Count == 0)
{
    sb.AppendLine("CLEAR — nothing in the host model sits inside any unit's access zone.");
    return sb.ToString();
}

sb.AppendLine("| Equipment | Name | Blocked at | Obstruction | Category |");
sb.AppendLine("|---|---|---|---|---|");
foreach (var f in findings.OrderBy(f => f.Face == "FRONT" ? 0 : 1).ThenBy(f => f.Eq.Id.ToString()).Take(maxReportedRows))
    sb.AppendLine($"| {f.Eq.Id} | {f.Eq.Name} | {f.Face} | {f.Blocker.Id} | {f.Blocker.Category?.Name ?? "-"} |");
if (findings.Count > maxReportedRows)
    sb.AppendLine($"\n... and {findings.Count - maxReportedRows} more (raise maxReportedRows to see them).");

int frontBlocked = findings.Count(f => f.Face == "FRONT");
sb.AppendLine();
sb.AppendLine($"BLOCKED AT THE FRONT: {frontBlocked} — these are the ones that stop the unit being serviced at all.");
sb.AppendLine("Worst equipment:");
foreach (var g in findings.GroupBy(f => f.Eq.Id).OrderByDescending(g => g.Count()).Take(10))
    sb.AppendLine($"  {g.Key}: {g.Count()} obstruction(s) — {string.Join(", ", g.Select(x => x.Face).Distinct())}");

return sb.ToString();
