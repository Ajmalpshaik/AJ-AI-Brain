// ============================================================
// SCRIPT: connect-equipment-to-air-terminals.cs
// PURPOSE: Connect ONE mechanical equipment's supply-air connector to ALL free air terminals as a
//          proper branched system: main trunk out of the equipment connector -> tap per terminal ->
//          300x300 (or terminal-size) branch -> elbow -> vertical drop into each terminal's up-facing
//          connector -> extend main past the LAST branch -> end cap. Ajmal's connection method,
//          proven live 2026-07-26 (8-FCU exercise, then full 6-terminal system, 0 failures).
// SOURCE:  ../knowledge/live-model/hvac-ducts.md ("Connecting equipment to terminals" section)
// STATUS:  living document - refine in place, don't fork a v2 file.
// ============================================================
// THE METHOD (never draw blind - element -> connectors -> domain/size -> direction -> then draw):
//   1. Check the equipment HAS connectors (MEPModel.ConnectorManager may be null).
//   2. Check WHAT KIND - Domain, DuctSystemType, shape, size. Prefer the free SupplyAir connector
//      with empty Description (some families also expose a "Fresh Air" SupplyAir intake - skip it).
//   3. Check the REAL direction - Connector.CoordinateSystem.BasisZ + Origin. Never assume an axis:
//      mirrored/rotated equipment WILL face a different way (20 of 40 curves would have missed in
//      the 2026-07-23 exercise if an axis had been assumed).
//   4. Then draw. The CONNECTOR overload Duct.Create(doc, ductTypeId, levelId, connector, endXYZ)
//      inherits size AND system from the connector and auto-connects (the XYZ+XYZ overload does NOT
//      - that one needs explicit sizing, see hvac-ducts.md).
//   5. Extend the main ~500mm PAST the last branch centerline (a tap landing on the cut end of the
//      main cannot seat properly - Ajmal caught this live with a screenshot), then close the open
//      end with an end cap (duct fitting family with PartType=Cap).
//
// GOTCHA: terminals sitting exactly ON the main's centerline axis are skipped (a lateral branch has
// zero length there - they'd need an inline tee/straight drop instead; handle those by hand).

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
int equipmentId = 0;                 // 0 = auto (works only if EXACTLY one Mechanical Equipment in model)
int mainDuctTypeId = 0;              // 0 = auto: first rectangular duct type whose name contains "Taps"
int levelIdInput = 0;                // 0 = auto: the equipment's own LevelId
double branchOffsetMm = 600;         // branch start distance from main centerline (must clear main half-width)
double extendPastLastBranchMm = 500; // extra main length past the last branch centerline
bool placeEndCap = true;             // close the main's open end with a PartType=Cap fitting
// ---- END INPUTS ----

Func<double, double> toFt = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
var sb = new System.Text.StringBuilder();

// ---- STEP 1+2: the equipment and its supply connector (check first, draw later) ----
FamilyInstance equip = null;
if (equipmentId != 0)
    equip = Document.GetElement(new ElementId(equipmentId)) as FamilyInstance;
else
{
    var all = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
        .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
    if (all.Count != 1) return $"equipmentId=0 needs exactly 1 Mechanical Equipment in the model, found {all.Count} - set equipmentId.";
    equip = all[0];
}
if (equip == null || equip.MEPModel == null || equip.MEPModel.ConnectorManager == null)
    return "Equipment not found or has no connectors - stopped before drawing anything.";

Connector sup = null;
foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
    if (c.Domain == Domain.DomainHvac && !c.IsConnected
        && c.DuctSystemType == Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir
        && string.IsNullOrEmpty(c.Description))            // skip "Fresh Air" style intakes
        sup = c;
if (sup == null)                                            // fallback: any free SupplyAir connector
    foreach (Connector c in equip.MEPModel.ConnectorManager.Connectors)
        if (c.Domain == Domain.DomainHvac && !c.IsConnected
            && c.DuctSystemType == Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir) sup = c;
if (sup == null) return "No free SupplyAir connector on the equipment - stopped.";

// ---- STEP 2 (terminals): every free air-terminal duct connector ----
var terms = new List<Tuple<int, Connector>>();
foreach (var at in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctTerminal)
         .WhereElementIsNotElementType().Cast<FamilyInstance>())
{
    if (at.MEPModel == null || at.MEPModel.ConnectorManager == null) continue;
    foreach (Connector c in at.MEPModel.ConnectorManager.Connectors)
        if (c.Domain == Domain.DomainHvac && !c.IsConnected)
            terms.Add(Tuple.Create(at.Id.IntegerValue, c));
}
if (terms.Count == 0) return "No free air-terminal connectors found - nothing to connect.";

// ---- resolve types / level ----
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
       : new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First().Id);
ElementId sysSupply = new FilteredElementCollector(Document)
    .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
    .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>()
    .First(x => x.SystemClassification == MEPSystemClassification.SupplyAir).Id;

// ---- STEP 3: real direction + route geometry (all from the connector, nothing assumed) ----
XYZ s = sup.Origin;
XYZ d = sup.CoordinateSystem.BasisZ;                        // real outward direction of the supply connector
double mainZ = s.Z;
double maxAlong = 0;
var routes = new List<Tuple<int, Connector, XYZ, XYZ>>();   // id, termConn, branchStart, branchEnd
foreach (var t in terms)
{
    XYZ tp = t.Item2.Origin;
    XYZ v = tp - s;
    double along = v.DotProduct(d);
    if (along <= 0) { sb.AppendLine($"AT {t.Item1}: behind the supply connector - skipped."); continue; }
    XYZ proj = new XYZ(s.X + d.X * along, s.Y + d.Y * along, mainZ);
    XYZ lat = new XYZ(tp.X - proj.X, tp.Y - proj.Y, 0);
    if (lat.GetLength() < toFt(50)) { sb.AppendLine($"AT {t.Item1}: on the main centerline - needs inline tee, skipped."); continue; }
    XYZ latDir = lat.Normalize();
    routes.Add(Tuple.Create(t.Item1, t.Item2, proj + latDir * toFt(branchOffsetMm), new XYZ(tp.X, tp.Y, mainZ)));
    if (along > maxAlong) maxAlong = along;
}
if (routes.Count == 0) return "No routable terminals.\r\n" + sb.ToString();
XYZ mainEnd = s + d * (maxAlong + toFt(extendPastLastBranchMm));   // METHOD RULE 5: past the last branch

Func<MEPCurve, XYZ, Connector> connAt = (dc, p) => {
    Connector best = null; double bd = double.MaxValue;
    foreach (Connector cc in dc.ConnectorManager.Connectors)
    { double dd = cc.Origin.DistanceTo(p); if (dd < bd) { bd = dd; best = cc; } }
    return best;
};

// ---- STEP 4: draw (one transaction, rollback on any failure) ----
var created = new List<ElementId>();
using (var tr = new Transaction(Document, "Connect equipment to air terminals"))
{
    tr.Start();
    try
    {
        var main = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, sup, mainEnd);
        created.Add(main.Id);
        Document.Regenerate();
        sb.AppendLine($"Main duct {main.Id} (size+system inherited from connector).");

        foreach (var r in routes)
        {
            Connector ac = r.Item2;
            double bw = ac.Shape == ConnectorProfileType.Rectangular ? ac.Width : ac.Radius * 2;
            double bh = ac.Shape == ConnectorProfileType.Rectangular ? ac.Height : ac.Radius * 2;

            var branch = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, sysSupply, ductTypeId, levelId, r.Item3, r.Item4);
            branch.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).Set(bw);   // XYZ overload does NOT inherit size
            branch.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).Set(bh);
            var drop = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, ac, r.Item4);
            Document.Regenerate();
            var elbow = Document.Create.NewElbowFitting(connAt(branch, r.Item4), connAt(drop, r.Item4));
            var tap = Document.Create.NewTakeoffFitting(connAt(branch, r.Item3), main);
            created.Add(branch.Id); created.Add(drop.Id); created.Add(elbow.Id); created.Add(tap.Id);
            sb.AppendLine($"AT {r.Item1}: branch {branch.Id}, drop {drop.Id}, elbow {elbow.Id}, tap {tap.Id}");
        }

        if (placeEndCap)
        {
            Connector openEnd = null;
            foreach (Connector c in main.ConnectorManager.Connectors)
                if (c.ConnectorType == ConnectorType.End && !c.IsConnected) openEnd = c;
            var capSym = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_DuctFitting).Cast<FamilySymbol>()
                .FirstOrDefault(x => {
                    var pt = x.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
                    return pt != null && (PartType)pt.AsInteger() == PartType.Cap && x.Family.Name.Contains("Rectangular");
                });
            if (openEnd != null && capSym != null)
            {
                if (!capSym.IsActive) { capSym.Activate(); Document.Regenerate(); }
                var cap = Document.Create.NewFamilyInstance(openEnd.Origin, capSym, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                Document.Regenerate();
                Func<Connector> capConn = () => { foreach (Connector c in cap.MEPModel.ConnectorManager.Connectors) return c; return null; };
                XYZ v0 = capConn().CoordinateSystem.BasisZ, target = openEnd.CoordinateSystem.BasisZ.Negate();
                double ang = v0.AngleTo(target);
                if (ang > 0.001)
                {
                    XYZ axis = v0.CrossProduct(target);
                    if (axis.GetLength() < 0.001) axis = Math.Abs(v0.Z) < 0.9 ? v0.CrossProduct(XYZ.BasisZ) : v0.CrossProduct(XYZ.BasisX);
                    ElementTransformUtils.RotateElement(Document, cap.Id, Line.CreateUnbound(capConn().Origin, axis.Normalize()), ang);
                    Document.Regenerate();
                }
                var pw = cap.LookupParameter("Duct Width"); var ph = cap.LookupParameter("Duct Height");
                if (pw != null && !pw.IsReadOnly && main.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM) != null)
                    pw.Set(main.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).AsDouble());
                if (ph != null && !ph.IsReadOnly && main.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) != null)
                    ph.Set(main.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).AsDouble());
                Document.Regenerate();
                XYZ shift = openEnd.Origin - capConn().Origin;
                if (shift.GetLength() > 0.0001) { ElementTransformUtils.MoveElement(Document, cap.Id, shift); Document.Regenerate(); }
                openEnd.ConnectTo(capConn());
                Document.Regenerate();
                created.Add(cap.Id);
                sb.AppendLine($"End cap {cap.Id} placed on main's open end.");
            }
            else sb.AppendLine("End cap skipped: " + (openEnd == null ? "no open end" : "no PartType=Cap rectangular family loaded"));
        }

        tr.Commit();
    }
    catch (Exception ex)
    {
        if (tr.GetStatus() == TransactionStatus.Started) tr.RollBack();
        return "ROLLED BACK - nothing was drawn. " + ex.Message + "\r\n" + sb.ToString();
    }
}

// ---- verify (never report success without checking) ----
int open = 0;
foreach (var id in created)
{
    var el = Document.GetElement(id);
    var mgr = (el as MEPCurve)?.ConnectorManager ?? (el as FamilyInstance)?.MEPModel?.ConnectorManager;
    if (mgr == null) continue;
    foreach (Connector c in mgr.Connectors)
        if (c.ConnectorType == ConnectorType.End && !c.IsConnected) open++;
}
sb.AppendLine($"VERIFY: equipment supply connected={sup.IsConnected}, routed terminals={routes.Count}/{terms.Count}, open ends in new system={open} (expect 0 with cap, 1 without).");
return sb.ToString();
