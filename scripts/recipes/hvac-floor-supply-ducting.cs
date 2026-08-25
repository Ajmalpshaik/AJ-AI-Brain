// ============================================================
// SCRIPT: hvac-floor-supply-ducting.cs
// PURPOSE: Build the air ductwork for EVERY room on a floor in ONE call — pairs each room with the
//          equipment standing inside it, then for each pair draws the trunk out of the equipment
//          connector, a tap + branch + drop per terminal, extends the trunk past the last tap and caps
//          it. The bulk counterpart to recipes/hvac-room-supply-ducting.cs, which does exactly one room.
// SOURCE:  ../../knowledge/live-model/hvac-ducts.md § Connecting equipment to terminals
// STATUS:  living document - refine in place, don't fork a v2 file.
//
// ✱✱ WHY THERE ARE TWO FILES, AND WHY THIS ONE DOES NOT REPLACE THE OTHER. Ajmal's instruction,
//    2026-08-25: *"do not change the code because maybe I need to do room by room, maybe one time,
//    that's depending on the work"*. Both ways are real work, so both stay:
//      recipes/hvac-room-supply-ducting.cs   ONE room. Use when rooms differ, when you are checking as
//                                            you go, or when only part of a floor is ready.
//      recipes/hvac-floor-supply-ducting.cs  EVERY room at once. Use when the floor is uniform and
//                                            already checked on one room.
//    ⚠ THE PER-ROOM BUILD LOGIC IS DUPLICATED BETWEEN THESE TWO FILES ON PURPOSE, because a fragment
//    cannot call another fragment. That is a real maintenance cost and it is written here so nobody
//    discovers it by surprise: FIX A BUG IN ONE AND YOU MUST FIX IT IN THE OTHER. If they ever drift,
//    the per-room file is the reference - it is the one proven first.
//
// ✱✱ ONE ROOM'S FAILURE DOES NOT COST YOU THE FLOOR. Each room is its own TransactionGroup, so a room
//    whose geometry defeats the fragment rolls back ALONE and is reported, while every other room
//    stands. That is deliberate: the alternative - one group for the whole floor - would throw away
//    thirteen good rooms because the fourteenth had an odd diffuser.
//
// ✱✱ EVERY ROOM IS VERIFIED BEFORE IT IS COUNTED, and the fragment's own report is not the evidence -
//    the same three checks the per-room file runs: no open connector on anything created, no created
//    duct overlapping the equipment body, every leg axis-aligned. A room failing any of them rolls back
//    and is listed as failed. (A sibling fragment once printed a clean success line over two unjoined
//    duct ends and a leg driven diagonally through an FCU; Ajmal found it in a 3D view, not in the text.)
//
// GOTCHA: ROOMS WITH NO EQUIPMENT INSIDE THEM ARE SKIPPED AND REPORTED, not treated as an error - a
//         corridor or a store has no FCU and that is normal.
// GOTCHA: A ROOM THAT ALREADY HAS DUCTWORK IS SKIPPED by default (skipRoomsThatHaveDuct), so re-running
//         after a partial failure only builds what is missing instead of doubling up what worked.
// GOTCHA: ElementId.IntegerValue IS GONE IN REVIT 2027 (renamed to .Value, a long, in 2024). This file
//         failed tools/check-scripts.cmd on 2027 while passing 2020 and 2024, for exactly two uses of
//         it - a level filter and a printed message - and the single-room sibling passed only because
//         it happens never to convert an ElementId to an int. Two ways out, both already in this
//         library, and the FIRST is better whenever it applies:
//           1. DON'T CONVERT AT ALL. Compare ElementIds to each other (`id == new ElementId(n)`) and
//              print the ElementId itself - `$"{el.Id}"` formats fine on every version. That is what
//              this file does now, and it needs no reflection.
//           2. When you genuinely need the number, use the library's established reflection helper,
//              e.g. actions/reporting/action-compare-elements.cs:
//                  var idValueProp = typeof(ElementId).GetProperty("Value")
//                                 ?? typeof(ElementId).GetProperty("IntegerValue");
//              About twenty QA fragments carry that line; copy it rather than inventing a third way.
// GOTCHA: equipment is matched to a room by ITS INSERTION POINT INSIDE THE ROOM'S BOUNDING BOX, never
//         by nearest-distance. On a corridor layout the neighbouring room's FCU can be closer to a
//         terminal than its own.
// RELATED: recipes/hvac-room-supply-ducting.cs (one room - the reference implementation);
//          actions/structural-changes/action-auto-route-mep-run.cs (generic A-to-B engine, any service);
//          recipes/verify-duct-connectivity.cs (check a built system, changes nothing).
// ✓ LIVE-VERIFIED 2026-08-25 — a 14-room floor in ONE call: 14 of 14 rooms built, 56 of 56 terminals
//   connected, 14 of them routed around their unit, 0 open duct ends model-wide. Read back INDEPENDENTLY
//   of its own report, per room: terminals connected, open connectors, duct/equipment bounding-box
//   overlap, axis-alignment, and cap orientation by connector dot product — 14 of 14 clean.
//   IT MATCHES THE SAME FLOOR BUILT ROOM BY ROOM AN HOUR EARLIER: 140 ducts, 140 fittings, 174.3 m
//   against 174.2 m (the 100 mm is two rooms hand-repaired that morning at a slightly different crossing
//   point; this run is the consistent one). Both ways are kept on purpose — see the header above.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
string systemKind = "supply";          // "supply" or "return" - picks the equipment connector AND the terminals
int levelIdFilter = 0;                 // 0 = every level; else only rooms on this Level Id
string roomNameContains = "";          // "" = every room; else only rooms whose name contains this
string excludeRoomNameContains = "";   // "" = exclude nothing; e.g. "CORRIDOOR" to leave corridors out
bool skipRoomsThatHaveDuct = true;     // true = leave rooms that already contain ductwork alone
int mainDuctTypeId = 0;                // 0 = auto: first rectangular duct type whose name contains "Taps"
double branchOffsetMm = 600;           // branch start distance from trunk centreline
double extendPastLastBranchMm = 500;   // extra trunk length past the last branch centreline
double equipmentClearanceMm = 400;     // how far past the equipment casing a re-routed branch crosses
bool placeEndCap = true;               // close each trunk's open end with a PartType=Cap fitting
bool dryRun = false;                   // true = list the room/equipment pairs and build nothing
// ---- END INPUTS ----

Func<double, double> toFt = v => v / 304.8;
Func<double, double> toMm = v => v * 304.8;
var sb = new System.Text.StringBuilder();

bool wantSupply = systemKind.Trim().ToLowerInvariant().StartsWith("s");
var wantSystem = wantSupply
    ? Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir
    : Autodesk.Revit.DB.Mechanical.DuctSystemType.ReturnAir;
var wantClass = wantSupply ? MEPSystemClassification.SupplyAir : MEPSystemClassification.ReturnAir;

// ---- types / system, resolved once for the whole floor ----
ElementId ductTypeId;
if (mainDuctTypeId != 0) ductTypeId = new ElementId(mainDuctTypeId);
else
{
    var dt = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))
        .Cast<Autodesk.Revit.DB.Mechanical.DuctType>()
        .FirstOrDefault(x => x.FamilyName.Contains("Rectangular") && x.Name.Contains("Taps"));
    if (dt == null) return "No rectangular 'Taps' duct type found - set mainDuctTypeId.";
    ductTypeId = dt.Id;
}
var sysTypeEl = new FilteredElementCollector(Document)
    .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
    .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>()
    .FirstOrDefault(x => x.SystemClassification == wantClass);
if (sysTypeEl == null) return $"No {wantClass} MechanicalSystemType in this project.";
ElementId sysId = sysTypeEl.Id;

// ---- the whole model, read once: equipment, terminals, existing duct ----
var allEquip = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
    .WhereElementIsNotElementType().Cast<FamilyInstance>()
    .Where(f => f.Location is LocationPoint).ToList();
var allTerms = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctTerminal)
    .WhereElementIsNotElementType().Cast<FamilyInstance>()
    .Where(f => f.Location is LocationPoint && f.MEPModel != null && f.MEPModel.ConnectorManager != null).ToList();
var allDuct = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves)
    .WhereElementIsNotElementType().Cast<MEPCurve>()
    .Where(d => d.Location is LocationCurve).ToList();

var rooms = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms)
    .WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Architecture.Room>()
    .Where(r => r.Area > 0)
    .Where(r => levelIdFilter == 0 || r.LevelId == new ElementId(levelIdFilter))
    .Where(r => roomNameContains == "" || (r.Name ?? "").IndexOf(roomNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
    .Where(r => excludeRoomNameContains == "" || (r.Name ?? "").IndexOf(excludeRoomNameContains, StringComparison.OrdinalIgnoreCase) < 0)
    .OrderBy(r => r.Name).ToList();
if (rooms.Count == 0) return "No rooms matched the filters - nothing to do.";

// ---- pair each room with the equipment standing INSIDE it ----
var pairs = new List<Tuple<Autodesk.Revit.DB.Architecture.Room, FamilyInstance, BoundingBoxXYZ>>();
int noEquip = 0, alreadyDucted = 0;
foreach (var r in rooms)
{
    var rb = r.get_BoundingBox(null);
    if (rb == null) { noEquip++; sb.AppendLine($"  SKIP {r.Name}: no bounding box."); continue; }
    Func<XYZ, bool> inR = p => p.X > rb.Min.X && p.X < rb.Max.X && p.Y > rb.Min.Y && p.Y < rb.Max.Y;

    if (skipRoomsThatHaveDuct && allDuct.Any(d => {
            var cv = (d.Location as LocationCurve).Curve;
            return inR((cv.GetEndPoint(0) + cv.GetEndPoint(1)) / 2); }))
    { alreadyDucted++; sb.AppendLine($"  SKIP {r.Name}: already has ductwork."); continue; }

    // insertion point inside the room, never nearest-distance (see header)
    var eq = allEquip.FirstOrDefault(f => inR((f.Location as LocationPoint).Point)
                                       && f.MEPModel != null && f.MEPModel.ConnectorManager != null);
    if (eq == null) { noEquip++; sb.AppendLine($"  SKIP {r.Name}: no equipment inside it."); continue; }
    pairs.Add(Tuple.Create(r, eq, rb));
}

sb.AppendLine();
sb.AppendLine($"{pairs.Count} room(s) to build ({wantSystem}), {noEquip} without equipment, {alreadyDucted} already ducted.");
if (pairs.Count == 0) return sb.ToString();

if (dryRun)
{
    sb.AppendLine();
    foreach (var p in pairs) sb.AppendLine($"  {p.Item1.Name}: equipment {p.Item2.Id}");
    sb.AppendLine();
    sb.AppendLine("DRY RUN - nothing was created.");
    return sb.ToString();
}

Func<MEPCurve, XYZ, Connector> connAt = (dc, p) => {
    Connector best = null; double bd = double.MaxValue;
    foreach (Connector cc in dc.ConnectorManager.Connectors)
    { double dd = cc.Origin.DistanceTo(p); if (dd < bd) { bd = dd; best = cc; } }
    return best;
};

// ---- build, ROOM BY ROOM, each in its own group so one failure costs only that room ----
int okRooms = 0, okTerms = 0, totalTerms = 0, aroundTotal = 0;
var failed = new List<string>();
sb.AppendLine();

foreach (var pair in pairs)
{
    var room = pair.Item1; var equip = pair.Item2; var rb = pair.Item3;
    Func<XYZ, bool> inR = p => p.X > rb.Min.X && p.X < rb.Max.X && p.Y > rb.Min.Y && p.Y < rb.Max.Y;

    Connector sup = null;
    foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
        if (c.Domain == Domain.DomainHvac && !c.IsConnected && c.DuctSystemType == wantSystem
            && (string.IsNullOrEmpty(c.Description)
                || c.Description.IndexOf("fresh", StringComparison.OrdinalIgnoreCase) < 0))
            sup = c;
    if (sup == null)
        foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
            if (c.Domain == Domain.DomainHvac && !c.IsConnected && c.DuctSystemType == wantSystem) sup = c;
    if (sup == null) { failed.Add($"{room.Name}: no free {wantSystem} connector on equipment {equip.Id}"); continue; }

    var terms = new List<Tuple<ElementId, Connector>>();
    foreach (var at in allTerms)
    {
        if (!inR((at.Location as LocationPoint).Point)) continue;
        foreach (Connector c in at.MEPModel.ConnectorManager.Connectors)
        {
            if (c.Domain != Domain.DomainHvac || c.IsConnected) continue;
            if (c.DuctSystemType != wantSystem) continue;
            terms.Add(Tuple.Create(at.Id, c));
        }
    }
    if (terms.Count == 0) { failed.Add($"{room.Name}: no free {wantSystem} terminals inside it"); continue; }
    totalTerms += terms.Count;

    XYZ s = sup.Origin;
    XYZ d = sup.CoordinateSystem.BasisZ;          // real direction; never an assumed axis
    double mainZ = s.Z;

    var eqBox = equip.get_BoundingBox(null);
    double eqAlong = 0;
    if (eqBox != null)
        foreach (var cx in new[] { eqBox.Min.X, eqBox.Max.X })
        foreach (var cy in new[] { eqBox.Min.Y, eqBox.Max.Y })
        {
            double a = (new XYZ(cx, cy, mainZ) - s).DotProduct(d);
            if (a > eqAlong) eqAlong = a;
        }
    double crossAlong = eqAlong + toFt(equipmentClearanceMm);

    var straight = new List<Tuple<ElementId, Connector, XYZ, XYZ>>();
    var around   = new List<Tuple<ElementId, Connector, XYZ, XYZ, XYZ>>();
    double maxAlong = 0;
    var notes = new List<string>();

    foreach (var t in terms)
    {
        XYZ tp = t.Item2.Origin;
        double along = (tp - s).DotProduct(d);
        XYZ proj = new XYZ(s.X + d.X * along, s.Y + d.Y * along, mainZ);
        XYZ lat = new XYZ(tp.X - proj.X, tp.Y - proj.Y, 0);
        double latLen = lat.GetLength();
        if (latLen < toFt(50)) { notes.Add($"AT {t.Item1} on the trunk centreline - needs an inline tee, skipped"); continue; }
        XYZ latDir = lat.Normalize();

        if (along > toFt(1))
        {
            straight.Add(Tuple.Create(t.Item1, t.Item2, proj + latDir * toFt(branchOffsetMm),
                                      new XYZ(tp.X, tp.Y, mainZ)));
            if (along > maxAlong) maxAlong = along;
        }
        else
        {
            if (latLen <= toFt(branchOffsetMm) + toFt(25))
            { notes.Add($"AT {t.Item1} behind the unit and only {toMm(latLen):F0} mm off the trunk - skipped"); continue; }
            XYZ cb = s + d * crossAlong;
            around.Add(Tuple.Create(t.Item1, t.Item2,
                new XYZ(tp.X, tp.Y, mainZ),
                new XYZ(cb.X + latDir.X * latLen, cb.Y + latDir.Y * latLen, mainZ),
                new XYZ(cb.X + latDir.X * toFt(branchOffsetMm), cb.Y + latDir.Y * toFt(branchOffsetMm), mainZ)));
            if (crossAlong > maxAlong) maxAlong = crossAlong;
        }
    }
    if (straight.Count + around.Count == 0) { failed.Add($"{room.Name}: no routable terminals"); continue; }
    XYZ mainEnd = s + d * (maxAlong + toFt(extendPastLastBranchMm));

    var madeDucts = new List<ElementId>(); var madeOther = new List<ElementId>();
    ElementId mainId = null;

    using (var group = new TransactionGroup(Document, $"AJ Tools - ducting {room.Name}"))
    {
        group.Start();
        try
        {
            using (var tr = new Transaction(Document, "draw"))
            {
                tr.Start();
                var main = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, ElementId.InvalidElementId == equip.LevelId
                    ? new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First().Id
                    : equip.LevelId, sup, mainEnd);
                mainId = main.Id; madeDucts.Add(main.Id);
                var levelId = main.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM).AsElementId();
                Document.Regenerate();

                foreach (var r in straight)
                {
                    Connector ac = r.Item2;
                    double bw = ac.Shape == ConnectorProfileType.Rectangular ? ac.Width : ac.Radius * 2;
                    double bh = ac.Shape == ConnectorProfileType.Rectangular ? ac.Height : ac.Radius * 2;
                    var branch = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item3, r.Item4);
                    branch.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);
                    branch.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                    var drop = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, ac, r.Item4);
                    Document.Regenerate();
                    var elbow = Document.Create.NewElbowFitting(connAt(branch, r.Item4), connAt(drop, r.Item4));
                    var tap = Document.Create.NewTakeoffFitting(connAt(branch, r.Item3), main);
                    Document.Regenerate();
                    madeDucts.Add(branch.Id); madeDucts.Add(drop.Id);
                    madeOther.Add(elbow.Id); madeOther.Add(tap.Id);
                }

                foreach (var r in around)
                {
                    Connector ac = r.Item2;
                    double bw = ac.Shape == ConnectorProfileType.Rectangular ? ac.Width : ac.Radius * 2;
                    double bh = ac.Shape == ConnectorProfileType.Rectangular ? ac.Height : ac.Radius * 2;
                    var riser = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, ac, r.Item3);
                    var fwd = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item3, r.Item4);
                    fwd.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);
                    fwd.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                    var cross = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item4, r.Item5);
                    cross.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);
                    cross.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                    Document.Regenerate();
                    var e1 = Document.Create.NewElbowFitting(connAt(riser, r.Item3), connAt(fwd, r.Item3));
                    var e2 = Document.Create.NewElbowFitting(connAt(fwd, r.Item4), connAt(cross, r.Item4));
                    var tap = Document.Create.NewTakeoffFitting(connAt(cross, r.Item5), main);
                    Document.Regenerate();
                    madeDucts.Add(riser.Id); madeDucts.Add(fwd.Id); madeDucts.Add(cross.Id);
                    madeOther.Add(e1.Id); madeOther.Add(e2.Id); madeOther.Add(tap.Id);
                }
                tr.Commit();
            }

            if (placeEndCap)
            {
                var mainCurve = Document.GetElement(mainId) as MEPCurve;
                Connector openEnd = null;
                foreach (Connector c in mainCurve.ConnectorManager.Connectors)
                    if (c.ConnectorType == ConnectorType.End && !c.IsConnected) openEnd = c;
                var ductType = Document.GetElement(ductTypeId) as Autodesk.Revit.DB.Mechanical.DuctType;
                RoutingPreferenceRule capRule = null;
                try { capRule = ductType.RoutingPreferenceManager.GetRule(RoutingPreferenceRuleGroupType.Caps, 0); }
                catch { capRule = null; }
                var capBase = capRule != null ? Document.GetElement(capRule.MEPPartId) as FamilySymbol : null;

                if (openEnd != null && capBase != null)
                {
                    using (var tc = new Transaction(Document, "cap"))
                    {
                        tc.Start();
                        if (!capBase.IsActive) { capBase.Activate(); Document.Regenerate(); }
                        var cap = Document.Create.NewFamilyInstance(openEnd.Origin, capBase,
                                      Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        Document.Regenerate();
                        Func<Connector> cc2 = () => cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>().First();
                        var k = cc2(); k.Width = openEnd.Width; k.Height = openEnd.Height;
                        Document.Regenerate();
                        ElementTransformUtils.MoveElement(Document, cap.Id, openEnd.Origin - cc2().Origin);
                        Document.Regenerate();
                        XYZ target = openEnd.CoordinateSystem.BasisZ.Negate();
                        double ang = cc2().CoordinateSystem.BasisZ.AngleTo(target);
                        if (ang > 0.001)
                        {
                            XYZ axis = cc2().CoordinateSystem.BasisZ.CrossProduct(target);
                            if (axis.IsZeroLength()) axis = cc2().CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisZ);
                            if (axis.IsZeroLength()) axis = cc2().CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisX);
                            ElementTransformUtils.RotateElement(Document, cap.Id,
                                Line.CreateBound(openEnd.Origin, openEnd.Origin + axis.Normalize()), ang);
                            Document.Regenerate();
                        }
                        ElementTransformUtils.MoveElement(Document, cap.Id, openEnd.Origin - cc2().Origin);
                        Document.Regenerate();
                        cc2().ConnectTo(openEnd);
                        Document.Regenerate();
                        madeOther.Add(cap.Id);
                        tc.Commit();
                    }
                }
            }

            // ---- the three checks, per room ----
            var faults = new List<string>();
            foreach (var id in madeDucts.Concat(madeOther))
            {
                var el = Document.GetElement(id);
                var mgr = (el as MEPCurve)?.ConnectorManager ?? (el as FamilyInstance)?.MEPModel?.ConnectorManager;
                if (mgr == null) continue;
                foreach (Connector c in mgr.Connectors)
                    if (c.ConnectorType == ConnectorType.End && !c.IsConnected
                        && !(!placeEndCap && id == mainId)) faults.Add($"open end on {id}");
            }
            if (eqBox != null)
                foreach (var id in madeDucts)
                {
                    if (id == mainId) continue;
                    var db = Document.GetElement(id).get_BoundingBox(null);
                    if (db == null) continue;
                    if (db.Min.X < eqBox.Max.X && db.Max.X > eqBox.Min.X &&
                        db.Min.Y < eqBox.Max.Y && db.Max.Y > eqBox.Min.Y &&
                        db.Min.Z < eqBox.Max.Z && db.Max.Z > eqBox.Min.Z) faults.Add($"duct {id} through the equipment");
                }
            foreach (var id in madeDucts)
            {
                var lc = (Document.GetElement(id) as MEPCurve)?.Location as LocationCurve;
                if (lc == null) continue;
                var p0 = lc.Curve.GetEndPoint(0); var p1 = lc.Curve.GetEndPoint(1);
                int moving = 0;
                if (Math.Abs(p1.X - p0.X) > toFt(1)) moving++;
                if (Math.Abs(p1.Y - p0.Y) > toFt(1)) moving++;
                if (Math.Abs(p1.Z - p0.Z) > toFt(1)) moving++;
                if (moving > 1) faults.Add($"duct {id} is diagonal");
            }

            if (faults.Count > 0)
            {
                group.RollBack();
                failed.Add($"{room.Name}: ROLLED BACK - {string.Join("; ", faults.Take(3))}"
                           + (faults.Count > 3 ? $" (+{faults.Count - 3} more)" : ""));
                continue;
            }

            group.Assimilate();
            okRooms++; okTerms += straight.Count + around.Count; aroundTotal += around.Count;
            sb.AppendLine($"  {room.Name}: {straight.Count + around.Count}/{terms.Count} terminal(s) - "
                        + $"{straight.Count} straight, {around.Count} routed around the unit, trunk {mainId}"
                        + (notes.Count > 0 ? "  [" + string.Join("; ", notes) + "]" : ""));
        }
        catch (Exception ex)
        {
            group.RollBack();
            failed.Add($"{room.Name}: ROLLED BACK - {ex.Message}");
        }
    }
}

sb.AppendLine();
sb.AppendLine($"BUILT {okRooms} of {pairs.Count} room(s). {okTerms} of {totalTerms} terminal(s) connected, "
            + $"{aroundTotal} routed around their unit. Every built room passed the open-end, "
            + $"equipment-clash and axis-alignment checks.");
if (failed.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"{failed.Count} room(s) NOT built (each rolled back on its own, the rest are untouched):");
    foreach (var f in failed) sb.AppendLine("  " + f);
}
return sb.ToString();
