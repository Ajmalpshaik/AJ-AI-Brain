// ============================================================
// FRAGMENT (action) — action-audit-mep-openings.cs
// PURPOSE: Audit the openings/sleeves ALREADY IN THE MODEL against the MEP that is in it now and the
//          structure in the linked model. Answers the question a revision actually asks — "the ducts
//          moved; which of my openings are now wrong?" — with a status per opening: STALE (nothing
//          runs through it any more), UNDERSIZED (the service pokes out beyond the hole), COMBINED
//          (more than one service through one opening), OVERLAPPING (two openings intersect),
//          UNHOSTED (not inside any structure), SPLIT (spans two different structure categories),
//          BLOCKED (lands in a category where an opening is not allowed), or OK.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — the OPENING
//          elements. The MEP and the structure are collected by this fragment.
// NOT STANDALONE — see scripts/README.md for how to compose.
// READ-ONLY — opens no transaction, changes nothing, in this model or in the link.
// SOURCE: knowledge/live-model/mep-openings.md — cutting them. This one CHECKS them, and the two are
//         deliberately separate: creation happens once, checking happens at every revision.
//
// ✱✱ THE TWO KINDS OF "OPENING" IN THIS LIBRARY ARE NOT THE SAME KIND OF ELEMENT, AND THAT ALMOST BROKE
//    THIS FRAGMENT. Ajmal asked the right question on 2026-08-24 — "we have MEP opening already, will
//    the new one clash or get confused and not achieve what we need?" — and the answer was yes:
//      * `recipes/create-mep-openings.cs` calls `Document.Create.NewOpening(...)`, which makes a Revit
//        **`Opening` element**. An Opening is a VOID. It has NO SOLID — `get_Geometry()` returns
//        nothing usable, only `BoundaryRect` / `BoundaryCurves` and a `Host`.
//      * `recipes/place-sleeves-at-wall-penetrations.cs` places a **FamilyInstance**, which does have
//        solids.
//    `filters/by-relationship/filter-by-openings.cs` — the obvious filter to put in front of this —
//    returns mostly the FIRST kind. So the natural composition would have reported "NO GEOMETRY" for
//    every opening our own recipe creates: no crash, no error, and an audit that audited nothing.
//    **BOTH kinds are handled now.** A family instance uses its own solids; an `Opening` gets a solid
//    BUILT from its boundary and its host's thickness, and the row says which route was used.
//
// ✱✱ THE BUILT SOLID IS DELIBERATELY TOO DEEP, AND ONLY TOO DEEP. It is extruded well past both faces
//    of the host along the host's normal. That direction is free — it cannot hide a fault, because the
//    hole's SIZE is what the audit tests and that lives in the perpendicular plane. Making it wider
//    in-plane would hide an UNDERSIZED opening, which is the one answer that must never be optimistic.
// ✱✱ AND THE BUILT SOLID IS CHECKED AGAINST THE ELEMENT'S OWN BOUNDING BOX before it is trusted. If the
//    two disagree badly the row is reported as SUSPECT GEOMETRY rather than audited on a solid that may
//    be in the wrong place. `Opening.BoundaryRect` is documented as "the geometry information if the
//    opening boundary is a rect" and the documentation does not state its coordinate space — so the
//    construction is verified rather than assumed.
//
// ✱✱ WHY THIS EXISTS, AND WHY IT IS NOT THE CLASH REPORT.
//    `recipes/create-mep-openings.cs` cuts openings. `action-report-clashes.cs` finds services hitting
//    structure. **Neither can tell you an existing opening has gone wrong**, and that is the failure a
//    coordination job actually hits: the hole was right in revision B, the pipe moved 200 mm in
//    revision C, and nothing in the model complains. The opening is still there, the clash report is
//    still clean — because the pipe now passes through the hole and *past its edge* into the concrete.
//
// ✱✱ THE CENTRAL TRICK — SUBTRACT THE OPENING FROM THE SERVICE, THEN TEST WHAT IS LEFT.
//    An opening is correct when the service is entirely inside it where it crosses the structure. So:
//    take the union of the services running through the opening, subtract the opening's own solid, and
//    test THE REMAINDER against the structure. If the remainder still hits structure, the service is
//    outside the hole — undersized, or in the wrong place. Testing the service against the structure
//    directly cannot distinguish "goes through the provided hole" from "goes through the concrete".
//
// ✱✱ SOLID.GETBOUNDINGBOX() IS NOT IN MODEL COORDINATES — Autodesk's own words: "The bounding box
//    information is stored as bounds in LOCAL coordinates and a transform... This is different from
//    the bounding box returned by Element.BoundingBox." Building `new Outline(box.Min, box.Max)` from
//    a solid box therefore gives a box in the wrong place, and every quick filter built on it silently
//    finds the wrong candidates. All eight corners are transformed here before the outline is taken.
//
// ✱✱ A DEGENERATE SOLID MAKES THE BOOLEAN THROW, AND THE TEST FOR IT MUST RUN BEFORE THE TRANSFORM.
//    A solid with no faces or no edges cannot be intersected — and `SolidUtils.CreateTransformed` GIVES
//    such a solid faces and edges, so a transformed one looks healthy and then throws anyway. Emptiness
//    is checked on the original, before any transform, exactly once.
// ✱✱ AND A BOOLEAN THAT THROWS IS REPORTED, NEVER SWALLOWED. Revit's kernel raises
//    InvalidOperationException on geometry it dislikes. Silently treating that as "no intersection"
//    is how a coordination report gives a clean bill of health for something it never tested — the
//    exact defect `action-report-clashes.cs` was fixed for on 2026-08-23. Here it becomes GEOMETRY
//    UNCHECKED on its own line, and the count says how many.
//
// GOTCHA: ON AN MEP JOB THE STRUCTURE IS IN THE LINK, so `structureLinkIdInt` is the normal case, not
//         the exception. Leave it null only when the walls and slabs really are in this same file.
//         Solids are moved into the LINK's coordinates (`linkTransform.Inverse`) rather than the other
//         way round, so the collector can run inside the link document where the structure lives.
// GOTCHA: the quick filter must come first. `BoundingBoxIntersectsFilter` is a quick filter (it reads
//         only the element record); `ElementIntersectsSolidFilter` is slow (it expands geometry). Put
//         the slow one first and every candidate in the model gets its geometry built. Both are always
//         chained in that order below — see knowledge/live-model/query-cost.md.
// GOTCHA: "the host" is the structure with the LARGEST intersected volume, not the first one found. An
//         opening near a wall/slab junction clips both, and the first hit is whichever the collector
//         happened to return. Ties at half the opening's volume exit early — nothing can beat that.
// GOTCHA: `toleranceMm` is a PROJECT NUMBER — how far a service may sit from the opening face before
//         the opening counts as wrong. The value below is a placeholder so the fragment runs.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only.
//    Run it on ONE known-good opening and one you have deliberately broken before trusting a batch.
//
// ✱✱ TWO SLEEVE/OPENING CHECKS NOW, AND THEY ASK DIFFERENT QUESTIONS. Merged 2026-08-24 from two
//    sessions working in parallel:
//      action-check-sleeve-size.cs        IS THE HOLE THE RIGHT SIZE — service + insulation + the
//                                         annular clearance the spec asks for, and not so oversized
//                                         that fire-stopping becomes the problem. A specification check.
//      action-audit-mep-openings.cs       IS THE HOLE STILL VALID AT ALL — against the structure in the
//                                         LINK: nothing runs through it any more, the service leaves it
//                                         and re-enters concrete, it spans two structure types, it
//                                         landed in a column, two openings overlap. A coordination check.
//    Run the size check when the sleeves were just placed; run the audit after the MEP has moved.
//    They overlap on one answer only (undersized) and disagree on nothing.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? structureLinkIdInt = null;      // the RVT link holding the walls/slabs. null = structure is in THIS model
double toleranceMm = 2.0;            // ASK. volume/length noise below this is not a fault
int maxRowsListed = 120;
bool listOkRows = false;             // false = only the openings with something wrong
bool checkOverlaps = true;           // opening-vs-opening intersection (slower: openings x openings)
// MEP categories that count as "a service running through the opening"
BuiltInCategory[] mepCategories = {
    BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_CableTray,  BuiltInCategory.OST_Conduit,
    BuiltInCategory.OST_FlexDuctCurves, BuiltInCategory.OST_FlexPipeCurves
};
// structure an opening MAY be hosted in
BuiltInCategory[] hostCategories = {
    BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
    BuiltInCategory.OST_Roofs, BuiltInCategory.OST_Ceilings
};
// structure an opening MUST NOT land in — cutting these is a structural decision, not an MEP one
BuiltInCategory[] blockedCategories = {
    BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming
};
// ---- END INPUTS ----

const double MM = 304.8;
double tolFt = toleranceMm / MM;
double volTol = tolFt * tolFt * tolFt;

// ---------- resolve the structural document and its transform ----------
Document structDoc = Document;
Transform linkTf = Transform.Identity;
if (structureLinkIdInt.HasValue)
{
    var li = Document.GetElement(new ElementId(structureLinkIdInt.Value)) as RevitLinkInstance;
    if (li == null) { sb.AppendLine("structureLinkIdInt is not a Revit link instance in this model."); return sb.ToString(); }
    structDoc = li.GetLinkDocument();
    if (structDoc == null) { sb.AppendLine("That link is not loaded — load it before auditing against it."); return sb.ToString(); }
    linkTf = li.GetTotalTransform();
}

// ---------- geometry helpers ----------
var geomOpts = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };

// A solid with no faces or no edges cannot take part in a boolean. Checked on the ORIGINAL solid only —
// a transformed copy of an empty solid reports faces and edges and then throws anyway.
Func<Solid, bool> usable = s =>
{
    if (s == null) return false;
    try { return !s.Faces.IsEmpty && !s.Edges.IsEmpty && s.Volume > volTol; }
    catch { return false; }
};

Func<Element, List<Solid>> solidsOf = null;
solidsOf = el =>
{
    var found = new List<Solid>();
    if (el == null) return found;
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geomOpts); } catch { }
    if (ge == null) return found;
    var stack = new Stack<GeometryElement>();
    stack.Push(ge);
    while (stack.Count > 0)
    {
        foreach (var go in stack.Pop())
        {
            var s = go as Solid;
            if (s != null) { if (usable(s)) found.Add(s); continue; }
            var gi = go as GeometryInstance;
            if (gi != null) { try { stack.Push(gi.GetInstanceGeometry()); } catch { } }
        }
    }
    return found;
};

// Union a list of solids. A union that throws banks what has accumulated and starts a new one, so one
// bad pairing costs that join and not the whole element.
Func<List<Solid>, List<Solid>> unite = list =>
{
    var outp = new List<Solid>();
    Solid acc = null;
    foreach (var s in list)
    {
        if (acc == null) { acc = s; continue; }
        try { acc = BooleanOperationsUtils.ExecuteBooleanOperation(acc, s, BooleanOperationsType.Union); }
        catch { outp.Add(acc); acc = s; }
    }
    if (acc != null) outp.Add(acc);
    return outp;
};

Func<Element, Solid> oneSolidOf = el =>
{
    var parts = unite(solidsOf(el));
    Solid best = null; double bestV = 0;
    foreach (var s in parts) { double v = 0; try { v = s.Volume; } catch { } if (v > bestV) { bestV = v; best = s; } }
    return best;
};

// ---------- a Revit `Opening` is a VOID and has no solid: build one ----------
// See the header. The extrusion depth is deliberately generous along the host's normal and exact in the
// plane that matters. Returns null when it cannot be built honestly.
Func<Element, XYZ> hostNormalOf = host =>
{
    var w = host as Wall;
    if (w != null) { try { return w.Orientation; } catch { } }
    return XYZ.BasisZ;   // slabs, roofs, ceilings, and shafts all run vertically
};

Func<Element, double> hostThicknessOf = host =>
{
    if (host == null) return 0;
    var w = host as Wall;
    if (w != null) { try { if (w.Width > 0) return w.Width; } catch { } }
    try
    {
        var ha = Document.GetElement(host.GetTypeId()) as HostObjAttributes;
        if (ha != null)
        {
            var cs = ha.GetCompoundStructure();
            if (cs != null) { double gw = cs.GetWidth(); if (gw > 0) return gw; }
        }
    }
    catch { }
    return 0;
};

int builtFromBoundary = 0, suspectBuilds = 0;

Func<Opening, Solid> solidFromOpening = op =>
{
    Element host = null;
    try { host = op.Host; } catch { }
    var normal = hostNormalOf(host);
    double thick = hostThicknessOf(host);
    // A generous depth: at least the host's thickness, never less than 1 m, and doubled so the extrusion
    // clears both faces however the boundary plane sits inside the host.
    double depth = Math.Max(thick, 1000.0 / MM) * 2.0;

    CurveLoop loop = null;
    try
    {
        if (op.IsRectBoundary)
        {
            var r = op.BoundaryRect;
            if (r == null || r.Count < 2) return null;
            var p0 = r[0]; var p1 = r[1];
            // The rectangle lies in the plane perpendicular to the host's normal. Build its two in-plane
            // axes from that normal rather than assuming world X/Y, so a wall at any angle is handled.
            var up = Math.Abs(normal.DotProduct(XYZ.BasisZ)) > 0.9 ? XYZ.BasisY : XYZ.BasisZ;
            var along = normal.CrossProduct(up).Normalize();
            up = along.CrossProduct(normal).Normalize();
            var d = p1 - p0;
            double du = d.DotProduct(along), dv = d.DotProduct(up);
            if (Math.Abs(du) < tolFt || Math.Abs(dv) < tolFt) return null;   // degenerate
            var c0 = p0;
            var c1 = p0 + along * du;
            var c2 = p0 + along * du + up * dv;
            var c3 = p0 + up * dv;
            loop = CurveLoop.Create(new List<Curve> {
                Line.CreateBound(c0, c1), Line.CreateBound(c1, c2),
                Line.CreateBound(c2, c3), Line.CreateBound(c3, c0) });
        }
        else
        {
            var ca = op.BoundaryCurves;
            if (ca == null || ca.Size < 3) return null;
            var curves = new List<Curve>();
            foreach (Curve c in ca) if (c != null) curves.Add(c);
            if (curves.Count < 3) return null;
            loop = CurveLoop.Create(curves);
        }
    }
    catch { return null; }
    if (loop == null) return null;

    Solid made = null;
    try
    {
        made = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, normal, depth);
        // Centre it on the boundary plane so it clears the host both ways.
        made = SolidUtils.CreateTransformed(made, Transform.CreateTranslation(normal * (-depth / 2.0)));
    }
    catch { return null; }
    if (!usable(made)) return null;

    // ---- verify before trusting it ----
    // The API does not document BoundaryRect's coordinate space, so the construction is checked against
    // the element's own bounding box. A solid built in the wrong place fails this immediately.
    try
    {
        var bb = op.get_BoundingBox(null);
        if (bb != null)
        {
            var c = (bb.Min + bb.Max) * 0.5;
            var mc = made.ComputeCentroid();
            double slip = mc.DistanceTo(c);
            double diag = bb.Max.DistanceTo(bb.Min);
            if (diag > 0 && slip > diag) { suspectBuilds++; return null; }
        }
    }
    catch { }

    builtFromBoundary++;
    return made;
};

// One entry point: family instances and anything else with real geometry take the solid path; a Revit
// `Opening` element takes the constructed path.
Func<Element, Solid> auditSolidOf = el =>
{
    var direct = oneSolidOf(el);
    if (usable(direct)) return direct;
    var op = el as Opening;
    if (op != null) return solidFromOpening(op);
    return null;
};

// Solid.GetBoundingBox() is in the SOLID's own coordinates plus a transform — see the header. All eight
// corners go through that transform before the outline is taken, or the quick filter looks in the
// wrong place and quietly finds nothing.
Func<Solid, Outline> outlineOf = s =>
{
    BoundingBoxXYZ bb = null;
    try { bb = s.GetBoundingBox(); } catch { }
    if (bb == null) return null;
    var t = bb.Transform ?? Transform.Identity;
    var mn = bb.Min; var mx = bb.Max;
    double lo0 = 0, lo1 = 0, lo2 = 0, hi0 = 0, hi1 = 0, hi2 = 0;
    bool first = true;
    for (int i = 0; i < 8; i++)
    {
        var corner = t.OfPoint(new XYZ((i & 1) == 0 ? mn.X : mx.X,
                                       (i & 2) == 0 ? mn.Y : mx.Y,
                                       (i & 4) == 0 ? mn.Z : mx.Z));
        if (first) { lo0 = hi0 = corner.X; lo1 = hi1 = corner.Y; lo2 = hi2 = corner.Z; first = false; }
        else
        {
            lo0 = Math.Min(lo0, corner.X); hi0 = Math.Max(hi0, corner.X);
            lo1 = Math.Min(lo1, corner.Y); hi1 = Math.Max(hi1, corner.Y);
            lo2 = Math.Min(lo2, corner.Z); hi2 = Math.Max(hi2, corner.Z);
        }
    }
    return new Outline(new XYZ(lo0, lo1, lo2), new XYZ(hi0, hi1, hi2));
};

int geometryUnchecked = 0;
Func<Solid, Solid, BooleanOperationsType, Solid> boolOp = (a, b, op) =>
{
    try { return BooleanOperationsUtils.ExecuteBooleanOperation(a, b, op); }
    catch { geometryUnchecked++; return null; }
};

Func<Solid, double> volOf = s => { if (s == null) return 0; try { return s.Volume; } catch { return 0; } };

// ---------- candidate sets, collected once ----------
var mepIds = new List<ElementId>();
foreach (var bic in mepCategories)
    mepIds.AddRange(new FilteredElementCollector(Document).OfCategory(bic).WhereElementIsNotElementType().ToElementIds());

var openingIds = elements.Where(e => e != null).Select(e => e.Id).ToList();

// ---------- the audit ----------
var report = new List<string>();
var tally = new Dictionary<string, int>();
Action<string> count = k => { if (!tally.ContainsKey(k)) tally[k] = 0; tally[k]++; };

int examined = 0, noSolid = 0;

foreach (var opening in elements)
{
    if (opening == null) continue;
    var openSolid = auditSolidOf(opening);
    if (!usable(openSolid))
    {
        noSolid++;
        count("NO GEOMETRY");
        report.Add($"{opening.Id} | {opening.Name} | NO GEOMETRY | — | no usable solid, and no boundary this could be built from, so NOTHING about it was checked");
        continue;
    }
    examined++;

    var openOutline = outlineOf(openSolid);
    double openVol = volOf(openSolid);

    // --- services through it (this document) ---
    var throughIds = new List<ElementId>();
    if (mepIds.Count > 0 && openOutline != null)
    {
        try
        {
            throughIds = new FilteredElementCollector(Document, mepIds)
                .WherePasses(new BoundingBoxIntersectsFilter(openOutline))   // quick filter FIRST
                .WherePasses(new ElementIntersectsSolidFilter(openSolid))    // slow filter second
                .ToElementIds().ToList();
        }
        catch { geometryUnchecked++; }
    }

    // --- the same solid, in the structural document's coordinates ---
    Solid openSolidInStruct = openSolid;
    if (structureLinkIdInt.HasValue)
    {
        try { openSolidInStruct = SolidUtils.CreateTransformed(openSolid, linkTf.Inverse); }
        catch { geometryUnchecked++; openSolidInStruct = null; }
    }
    var structOutline = openSolidInStruct == null ? null : outlineOf(openSolidInStruct);

    Func<BuiltInCategory[], List<ElementId>> structureHits = cats =>
    {
        var hits = new List<ElementId>();
        if (openSolidInStruct == null || structOutline == null) return hits;
        try
        {
            hits = new FilteredElementCollector(structDoc)
                .WherePasses(new ElementMulticategoryFilter(cats.ToList()))
                .WhereElementIsNotElementType()
                .WherePasses(new BoundingBoxIntersectsFilter(structOutline))
                .WherePasses(new ElementIntersectsSolidFilter(openSolidInStruct))
                .ToElementIds().ToList();
        }
        catch { geometryUnchecked++; }
        return hits;
    };

    var hostHits = structureHits(hostCategories);
    var blockedHits = structureHits(blockedCategories);

    // --- host = the structure with the largest intersected volume, not the first one found ---
    Element host = null;
    if (hostHits.Count > 0)
    {
        double bestVol = 0;
        double half = openVol / 2.0;
        foreach (var hid in hostHits)
        {
            var cand = structDoc.GetElement(hid);
            var cs = oneSolidOf(cand);
            if (!usable(cs)) continue;
            double v = volOf(boolOp(openSolidInStruct, cs, BooleanOperationsType.Intersect));
            if (v > bestVol) { bestVol = v; host = cand; }
            if (v >= half) break;   // nothing can beat half the opening's own volume
        }
        if (host == null) host = structDoc.GetElement(hostHits[0]);
    }

    // --- the categories the opening spans ---
    var spannedCats = new HashSet<string>();
    foreach (var hid in hostHits)
    {
        var el = structDoc.GetElement(hid);
        if (el != null && el.Category != null) spannedCats.Add(el.Category.Name);
    }

    // --- does the service stick out past the hole? ---
    bool sticksOut = false;
    if (throughIds.Count > 0 && hostHits.Count > 0 && openSolidInStruct != null)
    {
        var mepSolids = new List<Solid>();
        foreach (var mid in throughIds) mepSolids.AddRange(solidsOf(Document.GetElement(mid)));
        foreach (var united in unite(mepSolids))
        {
            // Emptiness is checked on the ORIGINAL solid whether or not a transform follows — a
            // transformed empty solid reports faces and edges and then throws anyway, and counting that
            // as an unchecked geometry operation would overstate how much this run could not test.
            if (!usable(united)) continue;
            Solid inStruct = united;
            if (structureLinkIdInt.HasValue)
            {
                try { inStruct = SolidUtils.CreateTransformed(united, linkTf.Inverse); }
                catch { geometryUnchecked++; continue; }
            }
            // the service geometry lying OUTSIDE the opening
            var remainder = boolOp(inStruct, openSolidInStruct, BooleanOperationsType.Difference);
            if (remainder == null || volOf(remainder) <= volTol) continue;
            var remOutline = outlineOf(remainder);
            if (remOutline == null) continue;
            try
            {
                bool hit = new FilteredElementCollector(structDoc, hostHits)
                    .WherePasses(new BoundingBoxIntersectsFilter(remOutline))
                    .WherePasses(new ElementIntersectsSolidFilter(remainder))
                    .Any();
                if (hit) { sticksOut = true; break; }
            }
            catch { geometryUnchecked++; }
        }
    }

    // --- opening against opening ---
    int overlaps = 0;
    if (checkOverlaps && openingIds.Count > 1 && openOutline != null)
    {
        try
        {
            overlaps = new FilteredElementCollector(Document, openingIds)
                .Excluding(new List<ElementId> { opening.Id })
                .WherePasses(new BoundingBoxIntersectsFilter(openOutline))
                .WherePasses(new ElementIntersectsSolidFilter(openSolid))
                .GetElementCount();
        }
        catch { geometryUnchecked++; }
    }

    // --- verdict, most serious first ---
    string status, why;
    if (blockedHits.Count > 0)
    { status = "BLOCKED"; why = $"lands in {blockedHits.Count} structural column/framing element(s) — cutting those is a structural decision"; }
    else if (hostHits.Count == 0)
    { status = "UNHOSTED"; why = "not inside any wall, floor, roof or ceiling — it is hanging in air"; }
    else if (throughIds.Count == 0)
    { status = "STALE"; why = "no service runs through it any more — the duct or pipe it was cut for has moved or gone"; }
    else if (sticksOut)
    { status = "UNDERSIZED"; why = "the service leaves the opening and re-enters the structure — too small, or in the wrong place"; }
    else if (spannedCats.Count > 1)
    { status = "SPLIT"; why = "spans " + string.Join(" + ", spannedCats) + " — one opening across two structure types"; }
    else if (throughIds.Count > 1)
    { status = "COMBINED"; why = $"{throughIds.Count} services share this opening — intended as a builder's opening, or two that should be separate"; }
    else if (overlaps > 0)
    { status = "OVERLAPPING"; why = $"intersects {overlaps} other opening(s) — they should probably be merged"; }
    else
    { status = "OK"; why = "one service, inside one host, fully within the hole"; }

    count(status);
    if (status != "OK" || listOkRows)
    {
        string hostTxt = host == null ? "(none)" : $"{host.Category?.Name} {host.Id}";
        report.Add($"{opening.Id} | {opening.Name} | {status} | host {hostTxt}, {throughIds.Count} service(s) | {why}");
    }
}

// ---------- output ----------
sb.AppendLine($"MEP OPENING AUDIT — {elements.Count} opening element(s) given, {examined} with usable geometry"
    + (structureLinkIdInt.HasValue ? $", structure read from link '{structDoc.Title}'" : ", structure read from THIS model"));
if (!structureLinkIdInt.HasValue)
    sb.AppendLine("NOTE: structureLinkIdInt is null, so only structure in THIS file was checked. On a normal MEP job the walls and slabs are in a LINK — set it, or this reports UNHOSTED for everything.");
sb.AppendLine();

sb.AppendLine("SUMMARY");
foreach (var kv in tally.OrderByDescending(k => k.Value)) sb.AppendLine($"  {kv.Key}: {kv.Value}");
if (noSolid > 0) sb.AppendLine($"  (of which {noSolid} had no usable solid at all)");
if (builtFromBoundary > 0)
    sb.AppendLine($"  {builtFromBoundary} of these are Revit `Opening` elements — a VOID, with no solid of its own — so their"
        + " geometry was BUILT from the boundary and the host's thickness. That is the normal path for anything"
        + " recipes/create-mep-openings.cs produced.");
if (suspectBuilds > 0)
{
    sb.AppendLine($"⚠ {suspectBuilds} opening(s) had a boundary that could NOT be turned into a trustworthy solid — the");
    sb.AppendLine("  built shape landed away from the element's own bounding box, so it was rejected rather than audited");
    sb.AppendLine("  on geometry in the wrong place. Those are counted as NO GEOMETRY above, not as passes.");
}
if (geometryUnchecked > 0)
{
    sb.AppendLine();
    sb.AppendLine($"⚠ {geometryUnchecked} GEOMETRY OPERATION(S) COULD NOT BE COMPLETED — Revit's kernel refused the solid.");
    sb.AppendLine("  Those checks did NOT run. This is not a clean result for them, it is an absent one.");
}

sb.AppendLine();
sb.AppendLine("Opening Id | Name | Status | Context | Why");
sb.AppendLine("--- | --- | --- | --- | ---");
foreach (var r in report.Take(maxRowsListed)) sb.AppendLine(r);
if (report.Count > maxRowsListed)
    sb.AppendLine($"... {report.Count - maxRowsListed} more not listed (raise maxRowsListed).");
if (report.Count == 0)
    sb.AppendLine("(nothing to report — set listOkRows = true to see the openings that passed)");

sb.AppendLine();
sb.AppendLine("STALE and UNDERSIZED are the two that a clash report cannot find: the service passes through the");
sb.AppendLine("hole, so nothing registers as a clash, and the opening is wrong anyway.");
sb.AppendLine();
sb.AppendLine("Feed this with filters/by-relationship/filter-by-openings.cs (Revit's own Opening elements AND");
sb.AppendLine("cutting families), or with a filter for your sleeve family. Both kinds are handled.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
