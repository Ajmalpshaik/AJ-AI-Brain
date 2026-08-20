// ============================================================
// SCRIPT: create-equipment-family-from-datasheet.cs
// PURPOSE: Build a manufacturer equipment family (.rfa) from a product datasheet, in one pass:
//          a parametric box cabinet, any number of round connector stubs carrying PIPE or
//          ELECTRICAL connectors (top or bottom face), and an optional toggleable maintenance
//          clearance zone on its own subcategory. The datasheet's own data (capacities, currents,
//          weights, model numbers) goes on as typed family parameters.
//          This is the "I have a PDF datasheet, make me the family" recipe — the duct-connector
//          sibling of create-parametric-box-family-with-duct-connector.cs, which stays the right
//          choice for an air terminal / duct-served box.
// REQUIRES: an already-open, active, EMPTY family document with its default reference planes intact
//           ("Center (Left/Right)", "Center (Front/Back)", "Reference Plane") and a plan view named
//           "Ref. Level". Any category template works; the category is set from INPUTS below.
//           Stops rather than overwriting if the family already has types or extrusions.
// SOURCE:  ../../knowledge/live-model/families.md - "Fourth build (2026-08-10, Condair EL 20 steam
//          humidifier from a manufacturer datasheet)" for every gotcha this encodes: the connector
//          size NOT inheriting the face (associate CONNECTOR_DIAMETER to an OD parameter), type
//          names rejecting ~ and friends, NewExtrusion taking a negative end to go downward,
//          negative formula results being legal and drivable, unconstrained stubs auto-tracking the
//          nearest face on resize, and SaveAs being blocked through the bridge.
// STATUS:  EVERY API CALL HERE RAN LIVE 2026-08-10 - but this composed file has NOT been executed
//          end-to-end as one script. It was generalized from a step-by-step live build of the Condair
//          EL 20-400V/3~ steam humidifier (530x406x780, 5 connectors, 63 parameters, clearance zone,
//          resize-tested at 700x500x1000 and reset clean). So the API surface is proven; the
//          INPUTS-driven loops, the backAtOrigin=false branch, and the per-stub "<name> Top" formula
//          are NOT - they were written for reuse, not run in that form. Run it once on a throwaway
//          empty family and confirm the bounding boxes before trusting it on a deliverable.
//          Living document - refine in place, do not fork a v2.
// NOTE:    the family CANNOT be saved from here. Document.SaveAs throws "Operation is not permitted
//          when there is any open transaction phase started by API client" through the bridge. Hand
//          the user the exact folder + filename and ask them to do File -> Save As.
// TODO:    this recipe does NOT yet do two things the 2026-08-11 rebuild added by hand, and a family
//          is not finished without them - see families.md § Fourth build for the exact calls:
//            (a) a "Unit Top" HORIZONTAL reference plane (cut vector XYZ.BasisY, created in an
//                ELEVATION view) with the top face aligned to it and a Height dimension labelled there,
//                instead of associating EXTRUSION_END_PARAM. Gives the modeller a plane to snap to.
//            (b) a subcategory per functional part (Cabinet / Steam Outlet / Condensate Ports / Water
//                Supply / Drain Water / Power Glands / Clearance) rather than only the clearance one.
//          Add both when this is next run in anger.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
Autodesk.Revit.DB.BuiltInCategory targetCategory = Autodesk.Revit.DB.BuiltInCategory.OST_MechanicalEquipment;
string familyTypeName = "EL 20-400V 3ph - 20 kg per h";  // NO \ : { } [ ] | ; < > ? ` ~  -- "/" is fine
double widthMm  = 530;    // X extent, always EQ-centred on Center (Left/Right)
double depthMm  = 406;    // Y extent
double heightMm = 780;    // Z extent, up from Z=0
bool   backAtOrigin = false;  // false = body EQ-centred on the origin in Y as well as X. DEFAULT, and
                              //         what a modeller expects - the insertion point is the centre of
                              //         the box. Ajmal asked for this explicitly on 2026-08-11.
                              // true  = back face pinned to Center (Front/Back), body runs into -Y.
                              //         Only for wall-mounted kit where snapping the back to a wall
                              //         matters more than a centred origin; it is silently asymmetric.
bool   lockToProductSize = false;  // true = give Width/Depth/Height a constant formula ("530 mm") so
                                   // Revit greys them out. Do this once the family IS a specific
                                   // purchased product, so nobody can resize it off-product. A locked
                                   // parameter still drives its dimension and still works as an
                                   // AssociateElementParameterToFamilyParameter target. Leave false
                                   // while you are still proving the geometry with resize tests.
bool   addClearanceZone = true;
double clrFrontMm = 600, clrLeftMm = 250, clrRightMm = 400, clrCeilingMm = 400, clrFloorMm = 600;

// Connector stubs. One row per connection.
//  0 name | 1 xMm | 2 yMm (negative = toward the front) | 3 "top"/"bottom" | 4 odMm | 5 stubLenMm
//  6 "pipe"/"elec" | 7 system type name | 8 flow: "in"/"out"/"bi" | 9 description
// Pipe system names: OtherPipe (use this for STEAM - Revit has no steam type), DomesticColdWater,
//   DomesticHotWater, Sanitary, Vent, SupplyHydronic, ReturnHydronic, FireProtectWet, ...
// Electrical system names: PowerBalanced (3-phase), PowerUnBalanced (1-phase), Data, Controls, ...
//
// *** GET THESE FROM THE SHOP DRAWING, NEVER FROM THE DATA SHEET PAGE. ***
// The data sheet gives connection SIZES only. It does not say where any of them are, and it does not
// always say how many there are. The shop drawing ("Unit dimensions unit X") carries the dimensioned
// top and bottom views. Building from the data sheet alone on 2026-08-10 put all four small
// connections on the wrong face at invented coordinates, and missed a second port entirely - see
// families.md § Fifth build. If you only have a data sheet, say the positions are unknown and ask.
//
// Real, dimensioned values below: Condair EL 20-400V/3~, housing "M", drawing A3.
// x is measured from the CENTRE (drawing dims are from the LEFT edge: x = fromLeft - Width/2).
// y is measured from the BACK  (drawing bottom-view dims are from the FRONT: y = -(Depth - fromFront)).
object[][] stubs = new object[][] {
    new object[]{"Steam Outlet",      -83.2, -232.0, "top",    45.0, 60.0, "pipe", "OtherPipe",        "out", "Steam outlet 45mm OD - 181.8 from left, 232 from back"},
    new object[]{"Condensate Return",-101.5,  -44.7, "top",     8.0, 40.0, "pipe", "Sanitary",         "in",  "Condensate return 8mm - 163.5 from left, 44.7 from back"},
    new object[]{"Condensate Spare", -203.2,  -82.4, "top",     8.0, 40.0, "none", "",                 "",    "Second 8mm port, geometry only - 61.8 from left, 82.4 from back"},
    new object[]{"Supply Water",     -214.0, -309.0, "bottom", 20.0, 60.0, "pipe", "DomesticColdWater","in",  "Supply water G 3/4in - 51 from left, 97 from front"},
    new object[]{"Drain Water",        56.5, -362.0, "bottom", 30.0, 60.0, "pipe", "Sanitary",         "out", "Drain water 30mm OD - 321.5 from left, 44 from front"},
    new object[]{"Power Heating",     177.0,  -40.0, "top",    30.0, 25.0, "elec", "PowerBalanced",    "bi",  "Heating voltage 400/3/50-60 V/Ph/Hz, 15 kW, 21.7 A"},
    new object[]{"Power Control",     222.0,  -40.0, "top",    22.0, 25.0, "elec", "PowerUnBalanced",  "bi",  "Control voltage 230/1/50 V/Ph/Hz"},
};

// Electrical. Plant often has TWO supplies - a heating/power feed and a separate control feed - and
// the control one is usually in the PROJECT schedule, not the manufacturer data sheet. Rows using
// PowerBalanced get the heating figures; rows using PowerUnBalanced get the control figures.
// Set apparentVA from the datasheet's own CURRENT, not its kW: sqrt(3) x V x A reproduces the quoted
// MCA exactly, a rounded kW does not.
double elecVolts   = 400;     // heating supply
double elecAmps    = 21.7;    // heating MCA
int    elecPoles   = 3;
bool   elecThreePhase = true;
double elecPF      = 1.0;     // electrode humidifiers / heaters are resistive -> 1.0
double ctrlVolts   = 230;     // control supply
int    ctrlPoles   = 1;
double ctrlVA      = 0;       // 0 = not stated in the submittal. Leave 0 rather than inventing a load.
// ---- END INPUTS ----

if (!Document.IsFamilyDocument) return "Active document is not a family document - open the family and make it active first.";
var doc = Document;
var fm  = doc.FamilyManager;

// ---- VERSION-PROOF UnitUtils.ConvertToInternalUnits ------------------------------------------------
// Electrical units are the one conversion this library cannot do as arithmetic (volts and volt-amperes
// are not a fixed factor off an internal foot the way mm are), so the API call has to stay - and
// DisplayUnitType went inaccessible at 2024. Ask the real method which unit type IT takes.
var _cvtM = typeof(UnitUtils).GetMethods()
    .Where(m => m.Name == "ConvertToInternalUnits" && m.GetParameters().Length == 2
             && m.GetParameters()[0].ParameterType == typeof(double))
    .OrderBy(m => m.GetParameters()[1].ParameterType.Name == "ForgeTypeId" ? 0 : 1)
    .FirstOrDefault();
if (_cvtM == null) throw new InvalidOperationException("This Revit has no UnitUtils.ConvertToInternalUnits(double, unit).");
var _unitT = _cvtM.GetParameters()[1].ParameterType;

// forgeName is the UnitTypeId property ("Volts"); dutName is the old enum member ("DUT_VOLTS").
Func<double, string, string, double> ToInternal = (v, forgeName, dutName) =>
{
    object unit;
    if (_unitT.Name == "ForgeTypeId")
    {
        var ty = typeof(Document).Assembly.GetType("Autodesk.Revit.DB.UnitTypeId");
        var pr = ty == null ? null : ty.GetProperty(forgeName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (pr == null) throw new InvalidOperationException("No UnitTypeId." + forgeName + " on this Revit.");
        unit = pr.GetValue(null);
    }
    else unit = Enum.Parse(_unitT, dutName);
    return (double)_cvtM.Invoke(null, new object[] { v, unit });
};
// ---------------------------------------------------------------------------------------------------


// ---- VERSION-PROOF FamilyManager.AddParameter -----------------------------------------------------
// Revit 2022 replaced the ParameterType enum with ForgeTypeId (SpecTypeId.Length, SpecTypeId.Boolean.YesNo)
// and 2024 made the old enum inaccessible, so naming it in code stops this fragment compiling there.
// The overload is chosen from the REAL method's own third parameter type, not by testing whether
// ForgeTypeId exists - Revit 2021 ships ForgeTypeId but this method there still takes the old enum.
var _addParamM = typeof(FamilyManager).GetMethods()
    .Where(m => m.Name == "AddParameter" && m.GetParameters().Length == 4
             && m.GetParameters()[0].ParameterType == typeof(string)
             && m.GetParameters()[3].ParameterType == typeof(bool)
             && (m.GetParameters()[1].ParameterType.Name == "BuiltInParameterGroup"
              || m.GetParameters()[1].ParameterType.Name == "ForgeTypeId"))
    .OrderBy(m => (m.GetParameters()[1].ParameterType.Name == "ForgeTypeId" ? 0 : 1)
                + (m.GetParameters()[2].ParameterType.Name == "ForgeTypeId" ? 0 : 1))
    .FirstOrDefault();
if (_addParamM == null) throw new InvalidOperationException("This Revit has no 4-argument FamilyManager.AddParameter(name, group, spec, isInstance).");
var _groupT = _addParamM.GetParameters()[1].ParameterType;   // enum before 2027, ForgeTypeId after
var _specT  = _addParamM.GetParameters()[2].ParameterType;

// The leaf name is the same in both worlds ("Length", "YesNo"); SpecTypeId files YesNo under Boolean.
Func<string, object> _specFor = nm =>
{
    if (_specT.Name != "ForgeTypeId") return Enum.Parse(_specT, nm);
    var _asm = typeof(Document).Assembly;
    var _fl = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
    foreach (var tn in new[] { "Autodesk.Revit.DB.SpecTypeId", "Autodesk.Revit.DB.SpecTypeId+Boolean", "Autodesk.Revit.DB.SpecTypeId+String" })
    {
        var ty = _asm.GetType(tn);
        var pr = ty == null ? null : ty.GetProperty(nm, _fl);
        if (pr != null) return pr.GetValue(null);
    }
    throw new InvalidOperationException("No SpecTypeId named '" + nm + "' on this Revit.");
};

// "PG_GEOMETRY" -> the old enum member, or GroupTypeId.Geometry on 2027 where the enum is REMOVED.
// The leaf name is derivable: strip PG_, then Title-case each underscore-separated word.
Func<System.Type, string, object> _groupAs = (gt, nm) =>
{
    if (gt.Name != "ForgeTypeId") return Enum.Parse(gt, nm);
    var core = nm.StartsWith("PG_") ? nm.Substring(3) : nm;
    var camel = string.Join("", core.Split('_')
        .Select(w => w.Length == 0 ? w : w.Substring(0, 1).ToUpper() + w.Substring(1).ToLower()));
    var gidT = typeof(Document).Assembly.GetType("Autodesk.Revit.DB.GroupTypeId");
    var pr = gidT == null ? null : gidT.GetProperty(camel,
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    if (pr == null) throw new InvalidOperationException("No GroupTypeId." + camel + " on this Revit (from '" + nm + "').");
    return pr.GetValue(null);
};
Func<string, string, string, bool, FamilyParameter> AddParam =
    (nm, grp, spec, inst) => (FamilyParameter)_addParamM.Invoke(fm,
        new object[] { nm, _groupAs(_groupT, grp), _specFor(spec), inst });
// ---------------------------------------------------------------------------------------------------

if (fm.Types.Size > 0) return "Family already has " + fm.Types.Size + " type(s) - stopping rather than overwriting.";
if (new FilteredElementCollector(doc).OfClass(typeof(Extrusion)).GetElementCount() > 0) return "Family already has extrusions - stopping.";

double LFt(double mm) => mm / 304.8;
double MM(double ft) => ft * 304.8;
var sb = new System.Text.StringBuilder();

var view = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
    .FirstOrDefault(v => v.Name == "Ref. Level" && v.ViewType == ViewType.FloorPlan);
if (view == null) return "No 'Ref. Level' floor plan view found - this recipe expects a default family template.";
ReferencePlane GetRP(string n) => new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().First(p => p.Name == n);
FamilyParameter FP(string n) { foreach (FamilyParameter p in fm.Parameters) if (p.Definition.Name == n) return p; return null; }

var cLR = GetRP("Center (Left/Right)");
var cFB = GetRP("Center (Front/Back)");
var horiz = GetRP("Reference Plane");
double halfW = LFt(widthMm) / 2.0, dep = LFt(depthMm), hgt = LFt(heightMm);
double yBack  = backAtOrigin ? 0 : dep / 2.0;
double yFront = backAtOrigin ? -dep : -dep / 2.0;

// ---- 1. category ----
using (var t = new Transaction(doc, "Set family category")) {
    t.Start();
    try { doc.OwnerFamily.FamilyCategory = doc.Settings.Categories.get_Item(targetCategory); t.Commit(); }
    catch (Exception ex) { t.RollBack(); return "FAILED category: " + ex.Message; }
}
sb.AppendLine("Category: " + doc.OwnerFamily.FamilyCategory.Name);

// ---- 2. parameters + default type (type MUST exist before any SetFormula) ----
using (var t = new Transaction(doc, "Parameters + type")) {
    t.Start();
    try {
        var G = "PG_GEOMETRY";
        var C = "PG_CONSTRAINTS";
        AddParam("Width",  G, "Length", false);
        AddParam("Depth",  G, "Length", false);
        AddParam("Height", G, "Length", false);
        AddParam("Top Stub Base", G, "Length", false);   // 5mm inside the body
        foreach (var row in stubs)
            if ((string)row[6] != "none")
                AddParam((string)row[0] + " OD", G, "Length", false);
        if (addClearanceZone) {
            foreach (var n in new[]{"Front Clearance","Left Clearance","Right Clearance","Ceiling Clearance","Floor Clearance",
                                    "Clearance Left Extent","Clearance Right Extent","Clearance Total Depth","Clearance Top","Clearance Bottom"})
                AddParam(n, C, "Length", false);
            AddParam("Show Clearance Zone", "PG_GRAPHICS", "YesNo", true);
        }
        foreach (var row in stubs)
            if ((string)row[3] == "top")
                AddParam((string)row[0] + " Top", G, "Length", false);

        var ft = fm.NewType(familyTypeName);
        fm.CurrentType = ft;

        fm.Set(FP("Width"), LFt(widthMm)); fm.Set(FP("Depth"), LFt(depthMm)); fm.Set(FP("Height"), LFt(heightMm));
        fm.Set(FP("Top Stub Base"), LFt(heightMm - 5));
        fm.SetFormula(FP("Top Stub Base"), "Height - 5 mm");   // unit suffixes ARE legal in formulas
        if (lockToProductSize) {                               // set AFTER the values, before the geometry
            fm.SetFormula(FP("Width"),  widthMm  + " mm");
            fm.SetFormula(FP("Depth"),  depthMm  + " mm");
            fm.SetFormula(FP("Height"), heightMm + " mm");
        }
        foreach (var row in stubs)
            if ((string)row[6] != "none") fm.Set(FP((string)row[0] + " OD"), LFt((double)row[4]));
        if (addClearanceZone) {
            fm.Set(FP("Front Clearance"), LFt(clrFrontMm));   fm.Set(FP("Left Clearance"), LFt(clrLeftMm));
            fm.Set(FP("Right Clearance"), LFt(clrRightMm));   fm.Set(FP("Ceiling Clearance"), LFt(clrCeilingMm));
            fm.Set(FP("Floor Clearance"), LFt(clrFloorMm));   fm.Set(FP("Show Clearance Zone"), 1);
            // spaced names are NOT quoted in formulas; a NEGATIVE result is legal and drivable
            fm.SetFormula(FP("Clearance Left Extent"),  "Width / 2 + Left Clearance");
            fm.SetFormula(FP("Clearance Right Extent"), "Width / 2 + Right Clearance");
            fm.SetFormula(FP("Clearance Total Depth"),  "Depth + Front Clearance");
            fm.SetFormula(FP("Clearance Top"),          "Height + Ceiling Clearance");
            fm.SetFormula(FP("Clearance Bottom"),       "-Floor Clearance");
        }
        foreach (var row in stubs)
            if ((string)row[3] == "top") {
                var p = FP((string)row[0] + " Top");
                fm.Set(p, LFt(heightMm + (double)row[5]));
                fm.SetFormula(p, "Height + " + (double)row[5] + " mm");
            }
        // NOTE: bottom stubs need no Z parameter - they hang off Z=0, which does not move with Height.
        t.Commit();
    } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED parameters: " + ex.Message).ToString(); }
}
sb.AppendLine("Type '" + fm.CurrentType.Name + "' created with " + fm.Parameters.Size + " parameters.");

// ---- 3. body ----
ReferencePlane uL, uR, uF; Extrusion body;
using (var t = new Transaction(doc, "Unit planes")) {
    t.Start();
    try {
        uL = doc.FamilyCreate.NewReferencePlane(new XYZ(-halfW,-3,0), new XYZ(-halfW,3,0), XYZ.BasisZ, view); uL.Name = "Unit Left";
        uR = doc.FamilyCreate.NewReferencePlane(new XYZ( halfW,-3,0), new XYZ( halfW,3,0), XYZ.BasisZ, view); uR.Name = "Unit Right";
        uF = doc.FamilyCreate.NewReferencePlane(new XYZ(-3,yFront,0), new XYZ(3,yFront,0), XYZ.BasisZ, view); uF.Name = "Unit Front";
        t.Commit();
    } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED unit planes: " + ex.Message).ToString(); }
}
foreach (var rp in new[]{uL,uR,uF})                       // 3rd arg is a DIRECTION - a tilted plane is silent
    if (Math.Abs(rp.GetPlane().Normal.Z) > 1e-6) return sb.AppendLine("ABORT: plane '" + rp.Name + "' is tilted.").ToString();

using (var t = new Transaction(doc, "Body extrusion")) {
    t.Start();
    try {
        var sp = SketchPlane.Create(doc, horiz.GetPlane());
        var ca = new CurveArray();
        var a = new XYZ(-halfW,yFront,0); var b = new XYZ(halfW,yFront,0);
        var c = new XYZ(halfW,yBack,0);   var d = new XYZ(-halfW,yBack,0);
        ca.Append(Line.CreateBound(a,b)); ca.Append(Line.CreateBound(b,c));
        ca.Append(Line.CreateBound(c,d)); ca.Append(Line.CreateBound(d,a));
        var arr = new CurveArrArray(); arr.Append(ca);
        body = doc.FamilyCreate.NewExtrusion(true, arr, sp, hgt);
        t.Commit();
    } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED body: " + ex.Message).ToString(); }
}

var opt = new Options { ComputeReferences = true };
Func<Extrusion, XYZ, Reference> FaceRef = (e, normal) => {
    foreach (GeometryObject go in e.get_Geometry(opt))
        if (go is Solid s) foreach (Face f in s.Faces)
            if (f is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(normal)) return pf.Reference;
    return null;
};
var bL = FaceRef(body, new XYZ(-1,0,0)); var bR = FaceRef(body, new XYZ(1,0,0));
var bF = FaceRef(body, new XYZ(0,-1,0)); var bB = FaceRef(body, new XYZ(0,1,0));
if (bL==null||bR==null||bF==null||bB==null) return sb.AppendLine("ABORT: missing a body face reference (sketch plane must be horizontal).").ToString();

double d1 = LFt(220), d2 = LFt(430);
using (var t = new Transaction(doc, "Lock body")) {
    t.Start();
    try {
        doc.FamilyCreate.NewAlignment(view, uL.GetReference(), bL);
        doc.FamilyCreate.NewAlignment(view, uR.GetReference(), bR);
        doc.FamilyCreate.NewAlignment(view, uF.GetReference(), bF);

        var eqL = Line.CreateBound(new XYZ(-halfW,yFront-d1,0), new XYZ(halfW,yFront-d1,0));
        var eqR = new ReferenceArray(); eqR.Append(uL.GetReference()); eqR.Append(cLR.GetReference()); eqR.Append(uR.GetReference());
        doc.FamilyCreate.NewDimension(view, eqL, eqR).AreSegmentsEqual = true;

        var wLn = Line.CreateBound(new XYZ(-halfW,yFront-d2,0), new XYZ(halfW,yFront-d2,0));
        var wRf = new ReferenceArray(); wRf.Append(uL.GetReference()); wRf.Append(uR.GetReference());
        doc.FamilyCreate.NewDimension(view, wLn, wRf).FamilyLabel = FP("Width");

        if (backAtOrigin) {
            doc.FamilyCreate.NewAlignment(view, cFB.GetReference(), bB);      // back pinned to origin
            var dLn = Line.CreateBound(new XYZ(-halfW-d1,yFront,0), new XYZ(-halfW-d1,yBack,0));
            var dRf = new ReferenceArray(); dRf.Append(cFB.GetReference()); dRf.Append(uF.GetReference());
            doc.FamilyCreate.NewDimension(view, dLn, dRf).FamilyLabel = FP("Depth");
        } else {
            var uB = doc.FamilyCreate.NewReferencePlane(new XYZ(-3,yBack,0), new XYZ(3,yBack,0), XYZ.BasisZ, view); uB.Name = "Unit Back";
            doc.FamilyCreate.NewAlignment(view, uB.GetReference(), bB);
            var eq2 = Line.CreateBound(new XYZ(-halfW-d1,yFront,0), new XYZ(-halfW-d1,yBack,0));
            var er2 = new ReferenceArray(); er2.Append(uF.GetReference()); er2.Append(cFB.GetReference()); er2.Append(uB.GetReference());
            doc.FamilyCreate.NewDimension(view, eq2, er2).AreSegmentsEqual = true;
            var dLn = Line.CreateBound(new XYZ(-halfW-d2,yFront,0), new XYZ(-halfW-d2,yBack,0));
            var dRf = new ReferenceArray(); dRf.Append(uF.GetReference()); dRf.Append(uB.GetReference());
            doc.FamilyCreate.NewDimension(view, dLn, dRf).FamilyLabel = FP("Depth");
        }
        fm.AssociateElementParameterToFamilyParameter(body.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), FP("Height"));
        t.Commit();
    } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED lock body: " + ex.Message).ToString(); }
}
var bb = body.get_BoundingBox(null);
sb.AppendLine("Body: X " + Math.Round(MM(bb.Min.X),1) + ".." + Math.Round(MM(bb.Max.X),1)
    + " Y " + Math.Round(MM(bb.Min.Y),1) + ".." + Math.Round(MM(bb.Max.Y),1)
    + " Z " + Math.Round(MM(bb.Min.Z),1) + ".." + Math.Round(MM(bb.Max.Z),1));

// ---- 4. connector stubs ----
foreach (var row in stubs)
{
    string nm = (string)row[0]; double sx = LFt((double)row[1]), sy = LFt((double)row[2]);
    bool onTop = (string)row[3] == "top"; double rad = LFt((double)row[4] / 2.0); double sl = LFt((double)row[5]);
    string domain = (string)row[6], sysName = (string)row[7], flowS = (string)row[8], desc = (string)row[9];

    Extrusion st;
    using (var t = new Transaction(doc, "Stub " + nm)) {
        t.Start();
        try {
            var sp = SketchPlane.Create(doc, horiz.GetPlane());
            var ctr = new XYZ(sx, sy, 0); var ca = new CurveArray();
            ca.Append(Arc.Create(ctr, rad, 0, Math.PI, XYZ.BasisX, XYZ.BasisY));
            ca.Append(Arc.Create(ctr, rad, Math.PI, 2*Math.PI, XYZ.BasisX, XYZ.BasisY));
            var arr = new CurveArrArray(); arr.Append(ca);
            // Both faces overlap the body by 5mm so the union is unambiguous (no coincident faces).
            // top:    start = Height-5, end = Height+len   (both Height-driven, so the stub follows a resize)
            // bottom: start = -len,     end = +5           (fixed - the base sits at Z=0 and does not move)
            st = doc.FamilyCreate.NewExtrusion(true, arr, sp, onTop ? hgt + sl : LFt(5));
            st.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(onTop ? hgt - LFt(5) : -sl);
            if (onTop) {
                fm.AssociateElementParameterToFamilyParameter(st.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM),   FP(nm + " Top"));
                fm.AssociateElementParameterToFamilyParameter(st.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM), FP("Top Stub Base"));
            }
            t.Commit();
        } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED stub " + nm + ": " + ex.Message).ToString(); }
    }

    if (domain == "none") { sb.AppendLine("  " + nm.PadRight(20) + " geometry only (spare port, no connector)"); continue; }

    var faceRef = FaceRef(st, onTop ? new XYZ(0,0,1) : new XYZ(0,0,-1));
    if (faceRef == null) return sb.AppendLine("ABORT: no end face reference on stub " + nm).ToString();

    using (var t = new Transaction(doc, "Connector " + nm)) {
        t.Start();
        try {
            ConnectorElement ce;
            if (domain == "pipe") {
                var sys = (Autodesk.Revit.DB.Plumbing.PipeSystemType)Enum.Parse(typeof(Autodesk.Revit.DB.Plumbing.PipeSystemType), sysName);
                ce = ConnectorElement.CreatePipeConnector(doc, sys, faceRef);
                // size is NOT inherited from the face (defaults to 1ft radius) - drive it from the OD parameter
                fm.AssociateElementParameterToFamilyParameter(ce.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER), FP(nm + " OD"));
                int flow = flowS == "in" ? (int)FlowDirectionType.In : flowS == "out" ? (int)FlowDirectionType.Out : (int)FlowDirectionType.Bidirectional;
                ce.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_DIRECTION_PARAM).Set(flow);
            } else {
                var sys = (Autodesk.Revit.DB.Electrical.ElectricalSystemType)Enum.Parse(typeof(Autodesk.Revit.DB.Electrical.ElectricalSystemType), sysName);
                ce = ConnectorElement.CreateElectricalConnector(doc, sys, faceRef);
                bool heating = sysName == "PowerBalanced";
                double volts = heating ? elecVolts : ctrlVolts;
                int    poles = heating ? elecPoles : ctrlPoles;
                double va    = heating ? (elecThreePhase ? Math.Sqrt(3.0) : 1.0) * elecVolts * elecAmps : ctrlVA;
                ce.get_Parameter(BuiltInParameter.RBS_ELEC_NUMBER_OF_POLES).Set(poles);
                ce.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE).Set(ToInternal(volts, "Volts", "DUT_VOLTS"));
                if (va > 0) ce.get_Parameter(BuiltInParameter.RBS_ELEC_APPARENT_LOAD).Set(ToInternal(va, "VoltAmperes", "DUT_VOLT_AMPERES"));
                var pf = ce.LookupParameter("Power Factor"); if (pf != null && !pf.IsReadOnly) pf.Set(elecPF);
            }
            // there is NO BuiltInParameter.CONNECTOR_DESCRIPTION - reach it by name
            var dp = ce.LookupParameter("Connector Description"); if (dp != null && !dp.IsReadOnly) dp.Set(desc);
            t.Commit();
            sb.AppendLine("  " + nm.PadRight(20) + " " + domain + "/" + sysName + " at ("
                + Math.Round(MM(ce.Origin.X),0) + "," + Math.Round(MM(ce.Origin.Y),0) + "," + Math.Round(MM(ce.Origin.Z),0) + ")");
        } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED connector " + nm + ": " + ex.Message).ToString(); }
    }
}

// ---- 5. clearance zone ----
if (addClearanceZone)
{
    double xL = LFt(-(widthMm/2 + clrLeftMm)), xR = LFt(widthMm/2 + clrRightMm);
    double yF = LFt(-(depthMm + clrFrontMm)) + LFt(backAtOrigin ? 0 : depthMm/2);
    Category sub = null;
    using (var t = new Transaction(doc, "Clearance subcategory")) {
        t.Start();
        try {
            var parent = doc.Settings.Categories.get_Item(targetCategory);
            foreach (Category c in parent.SubCategories) if (c.Name == "Clearance") sub = c;
            if (sub == null) sub = doc.Settings.Categories.NewSubcategory(parent, "Clearance");
            sub.LineColor = new Color(255, 140, 0);
            var m = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x => x.Name == "Clearance Zone");
            if (m == null) m = doc.GetElement(Material.Create(doc, "Clearance Zone")) as Material;
            m.Color = new Color(255, 140, 0); m.Transparency = 80; sub.Material = m;
            t.Commit();
        } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED clearance subcategory: " + ex.Message).ToString(); }
    }

    ReferencePlane rL, rR, rF; Extrusion clr;
    using (var t = new Transaction(doc, "Clearance planes + solid")) {
        t.Start();
        try {
            rL = doc.FamilyCreate.NewReferencePlane(new XYZ(xL,-6,0), new XYZ(xL,6,0), XYZ.BasisZ, view); rL.Name = "Clearance Left";
            rR = doc.FamilyCreate.NewReferencePlane(new XYZ(xR,-6,0), new XYZ(xR,6,0), XYZ.BasisZ, view); rR.Name = "Clearance Right";
            rF = doc.FamilyCreate.NewReferencePlane(new XYZ(-6,yF,0), new XYZ(6,yF,0), XYZ.BasisZ, view); rF.Name = "Clearance Front";
            var sp = SketchPlane.Create(doc, horiz.GetPlane());
            var ca = new CurveArray();
            var a = new XYZ(xL,yF,0); var b = new XYZ(xR,yF,0); var c = new XYZ(xR,yBack,0); var d = new XYZ(xL,yBack,0);
            ca.Append(Line.CreateBound(a,b)); ca.Append(Line.CreateBound(b,c));
            ca.Append(Line.CreateBound(c,d)); ca.Append(Line.CreateBound(d,a));
            var arr = new CurveArrArray(); arr.Append(ca);
            clr = doc.FamilyCreate.NewExtrusion(true, arr, sp, LFt(heightMm + clrCeilingMm));
            clr.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(LFt(-clrFloorMm));
            clr.Subcategory = sub;
            t.Commit();
        } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED clearance solid: " + ex.Message).ToString(); }
    }

    var gL = FaceRef(clr, new XYZ(-1,0,0)); var gR = FaceRef(clr, new XYZ(1,0,0));
    var gF = FaceRef(clr, new XYZ(0,-1,0)); var gB = FaceRef(clr, new XYZ(0,1,0));
    if (gL==null||gR==null||gF==null||gB==null) return sb.AppendLine("ABORT: missing a clearance face reference.").ToString();

    using (var t = new Transaction(doc, "Lock clearance")) {
        t.Start();
        try {
            doc.FamilyCreate.NewAlignment(view, rL.GetReference(), gL);
            doc.FamilyCreate.NewAlignment(view, rR.GetReference(), gR);
            doc.FamilyCreate.NewAlignment(view, rF.GetReference(), gF);
            if (backAtOrigin) doc.FamilyCreate.NewAlignment(view, cFB.GetReference(), gB);

            double o1 = LFt(depthMm + clrFrontMm + 300), o2 = LFt(depthMm + clrFrontMm + 550);
            var lLn = Line.CreateBound(new XYZ(xL,-o1,0), new XYZ(0,-o1,0));
            var lRf = new ReferenceArray(); lRf.Append(rL.GetReference()); lRf.Append(cLR.GetReference());
            doc.FamilyCreate.NewDimension(view, lLn, lRf).FamilyLabel = FP("Clearance Left Extent");

            var rLn = Line.CreateBound(new XYZ(0,-o2,0), new XYZ(xR,-o2,0));
            var rRf = new ReferenceArray(); rRf.Append(cLR.GetReference()); rRf.Append(rR.GetReference());
            doc.FamilyCreate.NewDimension(view, rLn, rRf).FamilyLabel = FP("Clearance Right Extent");

            var fLn = Line.CreateBound(new XYZ(xL-LFt(300),yF,0), new XYZ(xL-LFt(300),yBack,0));
            var fRf = new ReferenceArray(); fRf.Append(cFB.GetReference()); fRf.Append(rF.GetReference());
            doc.FamilyCreate.NewDimension(view, fLn, fRf).FamilyLabel = FP("Clearance Total Depth");

            fm.AssociateElementParameterToFamilyParameter(clr.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM),   FP("Clearance Top"));
            fm.AssociateElementParameterToFamilyParameter(clr.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM), FP("Clearance Bottom"));
            fm.AssociateElementParameterToFamilyParameter(clr.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM),      FP("Show Clearance Zone"));
            t.Commit();
        } catch (Exception ex) { t.RollBack(); return sb.AppendLine("FAILED lock clearance: " + ex.Message).ToString(); }
    }
    var cbb = clr.get_BoundingBox(null);
    sb.AppendLine("Clearance: X " + Math.Round(MM(cbb.Min.X),0) + ".." + Math.Round(MM(cbb.Max.X),0)
        + " Y " + Math.Round(MM(cbb.Min.Y),0) + ".." + Math.Round(MM(cbb.Max.Y),0)
        + " Z " + Math.Round(MM(cbb.Min.Z),0) + ".." + Math.Round(MM(cbb.Max.Z),0) + "  (toggle: 'Show Clearance Zone')");
}

sb.AppendLine("\nDone. Extrusions=" + new FilteredElementCollector(doc).OfClass(typeof(Extrusion)).GetElementCount()
    + " Connectors=" + new FilteredElementCollector(doc).OfClass(typeof(ConnectorElement)).GetElementCount()
    + " Parameters=" + fm.Parameters.Size);
sb.AppendLine("NEXT: run a real multi-value resize test before trusting this, then ask the user to File -> Save As");
sb.AppendLine("      (SaveAs cannot be scripted through the bridge - see the header note).");
return sb.ToString();
