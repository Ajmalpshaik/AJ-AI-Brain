// ============================================================
// SCRIPT: hvac-room-supply-ducting.cs
// PURPOSE: Build ONE room's air ductwork from its FCU in a single call — main trunk out of the
//          equipment connector, a tap + branch + drop per air terminal, the trunk extended past the
//          last tap and closed with a properly rotated end cap. Ajmal's connection method.
//          Unlike the recipe it replaces, a terminal standing BEHIND or LEVEL WITH the equipment is
//          NOT skipped: it is routed around the unit — rise, run forward clear of the casing, cross
//          to the trunk — which is Ajmal's own words for the job, 2026-08-24: "for the air terminal
//          [that] is back side of the FCU take a branch to [the] side and [move] to the front side,
//          that time you can connect to the main duct".
// SOURCE:  ../../knowledge/live-model/hvac-ducts.md § Connecting equipment to terminals
// STATUS:  living document - refine in place, don't fork a v2 file.
//
// ✱✱ WHY THIS EXISTS, AND WHAT IT REPLACES (decided with Ajmal 2026-08-25 after a three-room bake-off).
//    He asked whether to fold everything into one fragment. The answer was TWO, split by what each
//    one knows:
//      actions/structural-changes/action-auto-route-mep-run.cs  THE ENGINE. Point A to point B with
//               real elbows, for duct OR pipe OR anything else. Knows nothing about equipment,
//               terminals, systems, taps or caps — which is exactly why it serves chilled water and
//               drainage as happily as supply air. Keep it general; do not teach it HVAC.
//      recipes/hvac-room-supply-ducting.cs (this file)          THE TRADE TOOL. Knows which connector
//               on the FCU is supply, that a diffuser neck points up, that branches tap a trunk, and
//               that the trunk gets extended and capped. One room, one call.
//    It supersedes recipes/connect-terminal-branch.cs entirely — that one built a single branch and
//    REPORTED SUCCESS ON BROKEN GEOMETRY (see its header). Its job is a slice of this file's job.
//
// ✱✱ THE FRAGMENT'S OWN REPORT IS NOT EVIDENCE — that is the lesson this file is built around.
//    On 2026-08-25 a sibling fragment printed a clean success line for four terminals while the model
//    actually held two unjoined duct ends and a leg running diagonally through the FCU body. Every
//    text check passed; Ajmal found it in a 3D view. So this file VERIFIES ITSELF BEFORE REPORTING:
//      (a) no open connector on anything it created, other than the trunk end when placeEndCap=false
//      (b) no created duct's bounding box overlaps the equipment, except the trunk at its own connector
//      (c) every created duct leg is axis-aligned (a diagonal leg is the signature of the old bug)
//    If any check fails the whole thing ROLLS BACK and says so. A partial system is worse than none.
//
// GOTCHA: SCOPED TO ONE ROOM ON PURPOSE. The recipe this replaces swept terminals model-wide, so on a
//         14-room floor one FCU would have taken all 56 free supply diffusers. roomId is required.
// GOTCHA: SYSTEM TYPE IS MATCHED ON BOTH SIDES. Supply and return diffusers are checkerboarded on a
//         real ceiling; an unfiltered sweep wires RETURN terminals into the SUPPLY trunk. Set
//         systemKind to pick which system this run builds, and only terminals of that type are taken.
// GOTCHA: A CAP IS NOT CAPPED BY ConnectTo. Placing the endcap at the open connector and calling
//         ConnectTo leaves it 90 degrees out — a plate lying sideways across the opening — while Revit
//         still reports the end as connected and the model as having zero open ends. It must be sized,
//         moved, ROTATED, moved again, then connected, RE-FETCHING the cap connector after every step
//         because it goes stale on each change. The cap family comes from the duct type's Routing
//         Preferences, never a .First() on a name search — that picks M_Oval Endcap for a rectangular
//         duct.
// GOTCHA: a terminal sitting ON the trunk centreline needs an inline tee, not a takeoff. It is
//         reported and skipped rather than fudged.
// RELATED: actions/structural-changes/action-auto-route-mep-run.cs (the generic A-to-B engine);
//          actions/structural-changes/action-connect-air-terminals.cs (duct ALREADY runs past the
//          terminals — Revit cuts the tap, nothing new is drawn);
//          actions/structural-changes/action-connect-open-connectors.cs (cleanup, builds nothing);
//          recipes/verify-duct-connectivity.cs (check a built system, changes nothing).
// ✓ LIVE-VERIFIED 2026-08-25 — Room 4 of a 14-room floor, ONE call, against three rooms built the
//   long way that morning for comparison. It measured the casing itself (reached 275 mm past the
//   connector, so it crossed at 675 mm), took 3 terminals by straight tap and routed the 4th around the
//   unit, and capped square. Read back INDEPENDENTLY of its own report — which is the point — it
//   matched the hand-built rooms element for element: 10 ducts, 10 fittings, 4/4 terminals connected,
//   0 open ends, 0 clashes, 0 diagonal legs, cap facing dot -1.00.
//   Cost: 1 call. The same result via action-auto-route-mep-run.cs took 8, and via
//   connect-terminal-branch.cs it silently produced broken geometry.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
int equipmentId = 0;                 // the FCU / AHU. Required.
int roomId = 0;                      // Room whose terminals this run serves. Required.
string systemKind = "supply";        // "supply" or "return" - picks the equipment connector AND the
                                     // terminals, so both sides always match
int mainDuctTypeId = 0;              // 0 = auto: first rectangular duct type whose name contains "Taps"
int levelIdInput = 0;                // 0 = auto: the equipment's own LevelId
double branchOffsetMm = 600;         // branch start distance from trunk centreline (must clear trunk half-width)
double extendPastLastBranchMm = 500; // extra trunk length past the last branch centreline
double equipmentClearanceMm = 400;   // how far past the equipment casing a re-routed branch crosses over
bool placeEndCap = true;             // close the trunk's open end with a PartType=Cap fitting
bool dryRun = false;                 // true = report the plan, build nothing
// ---- END INPUTS ----

Func<double, double> toFt = v => v / 304.8;
Func<double, double> toMm = v => v * 304.8;
var sb = new System.Text.StringBuilder();

if (equipmentId == 0) return "equipmentId is required - give the FCU's Element Id.";
if (roomId == 0) return "roomId is required - this fragment is scoped to one room on purpose (see header).";

bool wantSupply = systemKind.Trim().ToLowerInvariant().StartsWith("s");
var wantSystem = wantSupply
    ? Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir
    : Autodesk.Revit.DB.Mechanical.DuctSystemType.ReturnAir;
var wantClass = wantSupply ? MEPSystemClassification.SupplyAir : MEPSystemClassification.ReturnAir;

// ---- STEP 1: the equipment and its free connector of the wanted system ----
var equip = Document.GetElement(new ElementId(equipmentId)) as FamilyInstance;
if (equip == null) return $"equipmentId {equipmentId} is not a family instance.";
if (equip.MEPModel == null || equip.MEPModel.ConnectorManager == null)
    return $"equipment {equipmentId} exposes no connectors.";

Connector sup = null;
foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
    if (c.Domain == Domain.DomainHvac && !c.IsConnected && c.DuctSystemType == wantSystem
        && (string.IsNullOrEmpty(c.Description)
            || c.Description.IndexOf("fresh", StringComparison.OrdinalIgnoreCase) < 0))
        sup = c;
if (sup == null)
    foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
        if (c.Domain == Domain.DomainHvac && !c.IsConnected && c.DuctSystemType == wantSystem) sup = c;
if (sup == null) return $"No free {wantSystem} connector on equipment {equipmentId} - stopped.";

// ---- STEP 2: the room, and every free terminal connector of the same system inside it ----
var room = Document.GetElement(new ElementId(roomId));
if (room == null) return $"roomId {roomId} is not an element in this model.";
var roomBox = room.get_BoundingBox(null);
if (roomBox == null) return $"roomId {roomId} has no bounding box - is it a placed Room?";

var terms = new List<Tuple<ElementId, Connector>>();
int skippedWrongSystem = 0, skippedOutOfRoom = 0;
foreach (var at in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctTerminal)
         .WhereElementIsNotElementType().Cast<FamilyInstance>())
{
    if (at.MEPModel == null || at.MEPModel.ConnectorManager == null) continue;
    // the terminal's INSERTION POINT, never its bounding box: a diffuser's box is several times the
    // face size and laps into the next room, which would drag in the neighbour's terminals
    var lp = at.Location as LocationPoint;
    if (lp == null) { skippedOutOfRoom++; continue; }
    var q = lp.Point;
    if (q.X <= roomBox.Min.X || q.X >= roomBox.Max.X ||
        q.Y <= roomBox.Min.Y || q.Y >= roomBox.Max.Y) { skippedOutOfRoom++; continue; }

    foreach (Connector c in at.MEPModel.ConnectorManager.Connectors)
    {
        if (c.Domain != Domain.DomainHvac || c.IsConnected) continue;
        if (c.DuctSystemType != wantSystem) { skippedWrongSystem++; continue; }
        terms.Add(Tuple.Create(at.Id, c));
    }
}
if (terms.Count == 0)
    return $"No free {wantSystem} terminal connectors inside room {roomId} - nothing to connect."
         + (skippedWrongSystem > 0 ? $" ({skippedWrongSystem} skipped as a different system type.)" : "")
         + (skippedOutOfRoom > 0 ? $" ({skippedOutOfRoom} terminal(s) skipped as outside the room.)" : "");

// ---- types / level / system ----
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
ElementId levelId = levelIdInput != 0 ? new ElementId(levelIdInput)
    : (equip.LevelId != ElementId.InvalidElementId ? equip.LevelId
       : new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
             .OrderBy(l => l.Elevation).First().Id);
var sysTypeEl = new FilteredElementCollector(Document)
    .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
    .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>()
    .FirstOrDefault(x => x.SystemClassification == wantClass);
if (sysTypeEl == null) return $"No {wantClass} MechanicalSystemType in this project.";
ElementId sysId = sysTypeEl.Id;

// ---- STEP 3: real geometry from the connector, nothing assumed about axes ----
XYZ s = sup.Origin;
XYZ d = sup.CoordinateSystem.BasisZ;               // true outward direction of the equipment connector
double mainZ = s.Z;

// how far along d the equipment casing reaches, so a re-routed branch knows where it is safe to cross
var equipBox = equip.get_BoundingBox(null);
double equipAlong = 0;
if (equipBox != null)
    foreach (var cx in new[] { equipBox.Min.X, equipBox.Max.X })
    foreach (var cy in new[] { equipBox.Min.Y, equipBox.Max.Y })
    {
        double a = (new XYZ(cx, cy, mainZ) - s).DotProduct(d);
        if (a > equipAlong) equipAlong = a;
    }
double crossAlong = equipAlong + toFt(equipmentClearanceMm);

// straight = normal tap/branch/drop | around = rise, run forward past the unit, cross to the trunk
var straight = new List<Tuple<ElementId, Connector, XYZ, XYZ>>();          // id, conn, branchStart, branchEnd
var around   = new List<Tuple<ElementId, Connector, XYZ, XYZ, XYZ>>();     // id, conn, riseTop, forwardEnd, tapPoint
double maxAlong = 0;

foreach (var t in terms)
{
    XYZ tp = t.Item2.Origin;
    XYZ v = tp - s;
    double along = v.DotProduct(d);
    XYZ proj = new XYZ(s.X + d.X * along, s.Y + d.Y * along, mainZ);
    XYZ lat = new XYZ(tp.X - proj.X, tp.Y - proj.Y, 0);
    double latLen = lat.GetLength();

    if (latLen < toFt(50))
    {
        sb.AppendLine($"  AT {t.Item1}: sits on the trunk centreline - needs an inline tee, skipped.");
        continue;
    }
    XYZ latDir = lat.Normalize();

    if (along > toFt(1))
    {
        // straight in front of the connector: the trunk reaches it, a plain tap will do
        straight.Add(Tuple.Create(t.Item1, t.Item2,
            proj + latDir * toFt(branchOffsetMm),
            new XYZ(tp.X, tp.Y, mainZ)));
        if (along > maxAlong) maxAlong = along;
    }
    else
    {
        // behind or level with the connector - route it around the unit rather than skipping it
        XYZ riseTop    = new XYZ(tp.X, tp.Y, mainZ);
        XYZ crossBase  = s + d * crossAlong;
        XYZ forwardEnd = new XYZ(crossBase.X + latDir.X * latLen, crossBase.Y + latDir.Y * latLen, mainZ);
        XYZ tapPoint   = new XYZ(crossBase.X + latDir.X * toFt(branchOffsetMm),
                                 crossBase.Y + latDir.Y * toFt(branchOffsetMm), mainZ);
        if (latLen <= toFt(branchOffsetMm) + toFt(25))
        {
            sb.AppendLine($"  AT {t.Item1}: behind the unit but only {toMm(latLen):F0} mm off the trunk - " +
                          $"less than branchOffsetMm ({branchOffsetMm:F0}) + clearance, so the cross leg would " +
                          $"double back. Skipped; move the terminal or lower branchOffsetMm.");
            continue;
        }
        around.Add(Tuple.Create(t.Item1, t.Item2, riseTop, forwardEnd, tapPoint));
        if (crossAlong > maxAlong) maxAlong = crossAlong;
    }
}

if (straight.Count + around.Count == 0) return "No routable terminals.\r\n" + sb.ToString();
XYZ mainEnd = s + d * (maxAlong + toFt(extendPastLastBranchMm));

sb.AppendLine($"PLAN for room {roomId} from equipment {equipmentId} ({wantSystem})");
sb.AppendLine($"  trunk {toMm(sup.Width):F0} x {toMm(sup.Height):F0} mm, " +
              $"{toMm((mainEnd - s).GetLength()):F0} mm long along ({d.X:F2},{d.Y:F2},{d.Z:F2})");
sb.AppendLine($"  {straight.Count} terminal(s) by straight tap, {around.Count} routed around the unit " +
              $"(casing reaches {toMm(equipAlong):F0} mm, crossing at {toMm(crossAlong):F0} mm)");
if (dryRun) { sb.AppendLine(); sb.AppendLine("DRY RUN - nothing was created."); return sb.ToString(); }

Func<MEPCurve, XYZ, Connector> connAt = (dc, p) => {
    Connector best = null; double bd = double.MaxValue;
    foreach (Connector cc in dc.ConnectorManager.Connectors)
    { double dd = cc.Origin.DistanceTo(p); if (dd < bd) { bd = dd; best = cc; } }
    return best;
};

// ---- STEP 4: build. One TransactionGroup: any failure, or any failed self-check, rolls back the lot ----
var createdDucts = new List<ElementId>();
var createdOther = new List<ElementId>();
ElementId mainId = null;

using (var group = new TransactionGroup(Document, "AJ Tools - room supply ducting"))
{
    group.Start();
    try
    {
        using (var tr = new Transaction(Document, "AJ Tools - draw room ducting"))
        {
            tr.Start();

            var main = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, sup, mainEnd);
            mainId = main.Id; createdDucts.Add(main.Id);
            Document.Regenerate();
            sb.AppendLine($"  trunk {main.Id} drawn (size and system inherited from the connector).");

            foreach (var r in straight)
            {
                Connector ac = r.Item2;
                double bw = ac.Shape == ConnectorProfileType.Rectangular ? ac.Width : ac.Radius * 2;
                double bh = ac.Shape == ConnectorProfileType.Rectangular ? ac.Height : ac.Radius * 2;

                var branch = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item3, r.Item4);
                branch.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);   // XYZ overload does NOT inherit size
                branch.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                var drop = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, ac, r.Item4);
                Document.Regenerate();
                var elbow = Document.Create.NewElbowFitting(connAt(branch, r.Item4), connAt(drop, r.Item4));
                var tap = Document.Create.NewTakeoffFitting(connAt(branch, r.Item3), main);
                Document.Regenerate();
                createdDucts.Add(branch.Id); createdDucts.Add(drop.Id);
                createdOther.Add(elbow.Id); createdOther.Add(tap.Id);
                sb.AppendLine($"  AT {r.Item1}: straight tap - branch {branch.Id}, drop {drop.Id}, elbow {elbow.Id}, tap {tap.Id}");
            }

            foreach (var r in around)
            {
                Connector ac = r.Item2;
                double bw = ac.Shape == ConnectorProfileType.Rectangular ? ac.Width : ac.Radius * 2;
                double bh = ac.Shape == ConnectorProfileType.Rectangular ? ac.Height : ac.Radius * 2;

                // rise out of the terminal (connector overload: inherits size and system, auto-connects)
                var riser = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, ac, r.Item3);
                // forward, clear of the casing, holding the terminal's lateral offset
                var fwd = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item3, r.Item4);
                fwd.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);
                fwd.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                // across to the trunk
                var cross = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysId, ductTypeId, levelId, r.Item4, r.Item5);
                cross.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);
                cross.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
                Document.Regenerate();

                var e1 = Document.Create.NewElbowFitting(connAt(riser, r.Item3), connAt(fwd, r.Item3));
                var e2 = Document.Create.NewElbowFitting(connAt(fwd, r.Item4), connAt(cross, r.Item4));
                var tap = Document.Create.NewTakeoffFitting(connAt(cross, r.Item5), main);
                Document.Regenerate();
                createdDucts.Add(riser.Id); createdDucts.Add(fwd.Id); createdDucts.Add(cross.Id);
                createdOther.Add(e1.Id); createdOther.Add(e2.Id); createdOther.Add(tap.Id);
                sb.AppendLine($"  AT {r.Item1}: ROUTED AROUND THE UNIT - riser {riser.Id}, forward {fwd.Id}, " +
                              $"cross {cross.Id}, elbows {e1.Id}/{e2.Id}, tap {tap.Id}");
            }

            tr.Commit();
        }

        // ---- the cap: sized, positioned, ROTATED, then connected (a bare ConnectTo proves nothing) ----
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

            if (openEnd == null) sb.AppendLine("  cap skipped: the trunk has no open end.");
            else if (capBase == null) sb.AppendLine("  cap skipped: this duct type has no Caps rule in its Routing Preferences.");
            else
            {
                using (var tc = new Transaction(Document, "AJ Tools - cap the trunk"))
                {
                    tc.Start();
                    if (!capBase.IsActive) { capBase.Activate(); Document.Regenerate(); }
                    var cap = Document.Create.NewFamilyInstance(openEnd.Origin, capBase,
                                  Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    Document.Regenerate();

                    // RE-FETCH after every change - the connector goes stale on each one
                    Func<Connector> capConn = () => cap.MEPModel.ConnectorManager.Connectors.Cast<Connector>().First();
                    var k = capConn(); k.Width = openEnd.Width; k.Height = openEnd.Height;
                    Document.Regenerate();

                    ElementTransformUtils.MoveElement(Document, cap.Id, openEnd.Origin - capConn().Origin);
                    Document.Regenerate();

                    XYZ target = openEnd.CoordinateSystem.BasisZ.Negate();
                    double ang = capConn().CoordinateSystem.BasisZ.AngleTo(target);
                    if (ang > 0.001)
                    {
                        XYZ axis = capConn().CoordinateSystem.BasisZ.CrossProduct(target);
                        if (axis.IsZeroLength()) axis = capConn().CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisZ);
                        if (axis.IsZeroLength()) axis = capConn().CoordinateSystem.BasisZ.CrossProduct(XYZ.BasisX);
                        ElementTransformUtils.RotateElement(Document, cap.Id,
                            Line.CreateBound(openEnd.Origin, openEnd.Origin + axis.Normalize()), ang);
                        Document.Regenerate();
                    }
                    ElementTransformUtils.MoveElement(Document, cap.Id, openEnd.Origin - capConn().Origin);
                    Document.Regenerate();
                    capConn().ConnectTo(openEnd);
                    Document.Regenerate();

                    createdOther.Add(cap.Id);
                    double dot = capConn().CoordinateSystem.BasisZ.DotProduct(openEnd.CoordinateSystem.BasisZ);
                    sb.AppendLine($"  cap {cap.Id} placed - sized, positioned and rotated " +
                                  $"(facing dot {dot:F2}, -1.00 is square to the trunk).");
                    tc.Commit();
                }
            }
        }

        // ---- STEP 5: SELF-CHECK. Never report success on unverified geometry (see header) ----
        var faults = new List<string>();

        // (a) open connectors on anything created
        foreach (var id in createdDucts.Concat(createdOther))
        {
            var el = Document.GetElement(id);
            var mgr = (el as MEPCurve)?.ConnectorManager ?? (el as FamilyInstance)?.MEPModel?.ConnectorManager;
            if (mgr == null) continue;
            foreach (Connector c in mgr.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End || c.IsConnected) continue;
                bool trunkEndWithoutCap = !placeEndCap && id == mainId;
                if (!trunkEndWithoutCap)
                    faults.Add($"open end on {el.Category?.Name} {id} at " +
                               $"({toMm(c.Origin.X):F0},{toMm(c.Origin.Y):F0},{toMm(c.Origin.Z):F0})");
            }
        }

        // (b) nothing driven through the equipment, except the trunk meeting its own connector
        if (equipBox != null)
            foreach (var id in createdDucts)
            {
                if (id == mainId) continue;
                var db = Document.GetElement(id).get_BoundingBox(null);
                if (db == null) continue;
                if (db.Min.X < equipBox.Max.X && db.Max.X > equipBox.Min.X &&
                    db.Min.Y < equipBox.Max.Y && db.Max.Y > equipBox.Min.Y &&
                    db.Min.Z < equipBox.Max.Z && db.Max.Z > equipBox.Min.Z)
                    faults.Add($"duct {id} passes through the equipment body");
            }

        // (c) every leg axis-aligned - a diagonal leg is the signature of the bug this replaces
        foreach (var id in createdDucts)
        {
            var cv = (Document.GetElement(id) as MEPCurve)?.Location as LocationCurve;
            if (cv == null) continue;
            var p0 = cv.Curve.GetEndPoint(0); var p1 = cv.Curve.GetEndPoint(1);
            int moving = 0;
            if (Math.Abs(p1.X - p0.X) > toFt(1)) moving++;
            if (Math.Abs(p1.Y - p0.Y) > toFt(1)) moving++;
            if (Math.Abs(p1.Z - p0.Z) > toFt(1)) moving++;
            if (moving > 1)
                faults.Add($"duct {id} is DIAGONAL - ({toMm(p0.X):F0},{toMm(p0.Y):F0},{toMm(p0.Z):F0}) -> " +
                           $"({toMm(p1.X):F0},{toMm(p1.Y):F0},{toMm(p1.Z):F0})");
        }

        if (faults.Count > 0)
        {
            group.RollBack();
            var f = new System.Text.StringBuilder();
            f.AppendLine("ROLLED BACK - the geometry failed this fragment's own checks, nothing was left behind.");
            foreach (var x in faults) f.AppendLine("  " + x);
            f.AppendLine();
            f.Append(sb.ToString());
            return f.ToString();
        }

        group.Assimilate();
    }
    catch (Exception ex)
    {
        group.RollBack();
        return "ROLLED BACK - nothing was built. " + ex.Message + "\r\n" + sb.ToString();
    }
}

// ---- report (only reached once the checks above passed) ----
int connectedTerms = 0;
foreach (var t in terms)
{
    var at = Document.GetElement(t.Item1) as FamilyInstance;
    foreach (Connector c in at.MEPModel.ConnectorManager.Connectors)
        if (c.Domain == Domain.DomainHvac && c.DuctSystemType == wantSystem && c.IsConnected) { connectedTerms++; break; }
}
sb.AppendLine();
sb.AppendLine($"VERIFIED: {connectedTerms}/{terms.Count} terminal(s) connected, " +
              $"{straight.Count} by straight tap and {around.Count} routed around the unit. " +
              $"Equipment connector joined={sup.IsConnected}. " +
              $"No open ends, no duct through the equipment, no diagonal legs."
            + (skippedWrongSystem > 0 ? $" {skippedWrongSystem} connector(s) skipped as a different system type." : ""));
return sb.ToString();
