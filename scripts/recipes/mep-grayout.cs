// ============================================================
// RECIPE — mep-grayout.cs
// PURPOSE: The whole "grayout for MEP" job in one pass — Ajmal's own drawing standard for making a
//          coordination view read: the architectural/structural background drops back to flat grey,
//          the services come forward in black, and insulation reads as a quiet dashed wrapper.
//          Taught step by step on 2026-08-10 and captured verbatim; the values below ARE the standard.
// TRIGGER: he says "do the grayout" or "do the grayout for MEP" — both phrasings, nothing more.
//          Full workflow + the reasoning behind each value: ../../skills/ajtools-mep-grayout/SKILL.md
//
// THE SCHEME, in one table (every value is an INPUT below, none are hard-coded):
//   background   line 150,150,150  fill 200,200,200 solid  weight 1
//   walls        line 150,150,150  fill 200,200,200 solid  weight 2   <- the one heavier background line
//   floors       line 240,240,240  fill 240,240,240 solid  weight 1   <- lightest, sits behind everything
//   doors        line 200,200,200  fill 200,200,200        weight 1   transparency 100%
//   windows      line 150,150,150  fill 200,200,200        weight 1   transparency 100%
//   services     line 0,0,0        (fill discarded)        weight 3   transparency 80%  solid pattern
//   insulation   line 80,80,80     (fill discarded)        weight 1   transparency 100% MEP_Hidden_Short_Dash
//   mech equip   line 0,0,0        fill 128,128,128 solid  weight 3   transparency 0%
//   OFF          Structural Rebar, Structural Rebar Couplers
//
// WHY WINDOWS ARE 150 AND DOORS ARE 200 — the one value that looks like a typo and is not.
//   A window sits INSIDE a wall whose fill is already 200, so a 200 window line vanishes into it.
//   A door breaks the wall with its opening and swing, so a light line still reads. He caught this
//   himself. Anything that sits inside a greyed host must not take the host's fill colour as its line.
//
// WHAT REVIT WILL SILENTLY THROW AWAY — expect it, do not report it as success:
//   * Non-cuttable categories (all of Ducts/Pipes/Air Terminals/Sprinklers/Mechanical Equipment, and
//     the electrical families) DISCARD the cut line, cut weight and cut fill. Ducts and pipes also
//     discard the surface fill, so the services end up as coloured LINES with no fill, whatever is set.
//   * Rooms, Areas, Spaces, Raster Images and Point Clouds take NO category override at all.
//   * Sub-categories hold only line colour and line weight — never fill, never transparency.
//   Measured in full: ../../knowledge/live-model/graphic-override-precedence.md
//
// SCOPE: one view, per view. Nothing here is project-wide — the project Line Weights table is NOT
//        touched (and is not reachable from the API anyway). To reuse the result across many views,
//        apply this to one view and then save that view as a View Template by hand.
// READ + WRITE — one Transaction, explicit RollBack on failure, then a read-back report of what
//        actually stuck. It reports per-slot losses rather than claiming the whole override applied.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null;          // null = active view; or an Element Id to target any view

// the greys
byte bgLineR = 150, bgLineG = 150, bgLineB = 150;      // background lines
byte bgFillR = 200, bgFillG = 200, bgFillB = 200;      // background fill
byte floorR  = 240, floorG  = 240, floorB  = 240;      // floors, line AND fill
byte doorR   = 200, doorG   = 200, doorB   = 200;      // door lines only
byte insR    =  80, insG    =  80, insB    =  80;      // insulation lines
byte equipFillR = 128, equipFillG = 128, equipFillB = 128;  // mechanical equipment fill

// weights (Revit pens 1-16; on a default metric table at 1:100, 1 = 0.10mm, 2 = 0.18, 3 = 0.25)
int weightBackground = 1;
int weightWalls      = 2;
int weightServices   = 3;
int weightInsulation = 1;

// transparencies (0-100). NOTE: only visible in Shaded / Consistent Colours / Realistic —
// a Hidden Line view stores these correctly and shows none of them.
int transpDoorsWindows = 100;
int transpServices     = 80;
int transpInsulation   = 100;
int transpEquipment    = 0;

string insulationPatternName = "MEP_Hidden_Short_Dash";  // the PATTERN behind line style MEP_Hidden_Short_Black
bool subCategoriesFollowFinalParent = false;  // false = reproduce the state as taught (service sub-categories
                                              // stay background grey). true = they follow their parent to black.
                                              // See "KNOWN INCONSISTENCY" in the skill before flipping this.

BuiltInCategory[] turnOff = { BuiltInCategory.OST_Rebar, BuiltInCategory.OST_Coupler };

// the services that come forward in black — carrier + its fittings + its accessories, per service
BuiltInCategory[] blackRun = {
    BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_DuctAccessory,
    BuiltInCategory.OST_FlexDuctCurves,
    BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
    BuiltInCategory.OST_FlexPipeCurves,
    BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting
};
BuiltInCategory[] insulation = { BuiltInCategory.OST_DuctInsulations, BuiltInCategory.OST_PipeInsulations };
BuiltInCategory[] equipmentSolid = { BuiltInCategory.OST_MechanicalEquipment };
BuiltInCategory[] keepGreyAt80  = { BuiltInCategory.OST_DuctLinings };  // wrapper, left grey pending his call
// ---- END INPUTS ----

var doc = Document;
var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? doc.GetElement(new ElementId(targetViewIdInt.Value)) as View : doc.ActiveView;
if (view == null) return $"Target view (Id {targetViewIdInt}) not found or is not a view.";
if (view.ViewTemplateId != null && view.ViewTemplateId != ElementId.InvalidElementId)
{
    var tpl = doc.GetElement(view.ViewTemplateId) as View;
    sb.AppendLine($"WARNING: view '{view.Name}' has View Template '{(tpl == null ? "?" : tpl.Name)}' applied.");
    sb.AppendLine("  If that template controls V/G model categories, these overrides will be locked out or");
    sb.AppendLine("  reverted. Check before trusting the result.");
}

var solidFill = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement))
    .Cast<FillPatternElement>().FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
if (solidFill == null) return "ABORTED - no Solid fill pattern in this document, nothing changed.";
var solidLineId = LinePatternElement.GetSolidPatternId();

var dashPattern = new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement))
    .Cast<LinePatternElement>().FirstOrDefault(p => p.Name == insulationPatternName);
if (dashPattern == null)
    sb.AppendLine($"NOTE: line pattern '{insulationPatternName}' not in this project — insulation keeps a solid line. " +
                  "Run recipes/create-mep-line-standards.cs first to install the MEP_ pattern set.");

var cLine   = new Autodesk.Revit.DB.Color(bgLineR, bgLineG, bgLineB);
var cFill   = new Autodesk.Revit.DB.Color(bgFillR, bgFillG, bgFillB);
var cFloor  = new Autodesk.Revit.DB.Color(floorR, floorG, floorB);
var cDoor   = new Autodesk.Revit.DB.Color(doorR, doorG, doorB);
var cIns    = new Autodesk.Revit.DB.Color(insR, insG, insB);
var cEquip  = new Autodesk.Revit.DB.Color(equipFillR, equipFillG, equipFillB);
var cBlack  = new Autodesk.Revit.DB.Color(0, 0, 0);

int IdOf(BuiltInCategory b) { var c = Category.GetCategory(doc, b); return c == null ? -1 : c.Id.IntegerValue; }
var blackIds = new HashSet<int>(blackRun.Select(IdOf).Where(i => i != -1));
var insIds   = new HashSet<int>(insulation.Select(IdOf).Where(i => i != -1));
var equipIds = new HashSet<int>(equipmentSolid.Select(IdOf).Where(i => i != -1));
var grey80Ids = new HashSet<int>(keepGreyAt80.Select(IdOf).Where(i => i != -1));
int wallId = IdOf(BuiltInCategory.OST_Walls), floorId = IdOf(BuiltInCategory.OST_Floors);
int doorId = IdOf(BuiltInCategory.OST_Doors), windowId = IdOf(BuiltInCategory.OST_Windows);

var cats = doc.Settings.Categories.Cast<Category>()
    .Where(c => c.CategoryType == CategoryType.Model).OrderBy(c => c.Name).ToList();

int shown = 0, hidden = 0, written = 0, notControllable = 0, subsWritten = 0;
var lostSlots = new List<string>();

using (var t = new Transaction(doc, "AJ Tools - MEP grayout"))
{
    t.Start();
    try
    {
        var offIds = new HashSet<int>(turnOff.Select(IdOf).Where(i => i != -1));

        foreach (var c in cats)
        {
            if (!c.get_AllowsVisibilityControl(view)) { notControllable++; continue; }
            int id = c.Id.IntegerValue;

            // --- 1. visibility: everything on, the named few off ---
            bool wantHidden = offIds.Contains(id);
            if (view.GetCategoryHidden(c.Id) != wantHidden) view.SetCategoryHidden(c.Id, wantHidden);
            if (wantHidden) hidden++; else shown++;

            // --- 2. the override, background rule first then the exceptions ---
            var o = view.GetCategoryOverrides(c.Id);
            var lineColour = cLine;
            int weight = weightBackground;

            o.SetSurfaceForegroundPatternId(solidFill.Id);
            o.SetSurfaceForegroundPatternVisible(true);
            o.SetCutForegroundPatternId(solidFill.Id);
            o.SetCutForegroundPatternVisible(true);
            o.SetSurfaceForegroundPatternColor(cFill);
            o.SetCutForegroundPatternColor(cFill);
            o.SetSurfaceTransparency(0);
            o.SetProjectionLinePatternId(solidLineId);
            o.SetCutLinePatternId(solidLineId);

            if (id == wallId) { weight = weightWalls; }
            else if (id == floorId)
            {
                lineColour = cFloor;
                o.SetSurfaceForegroundPatternColor(cFloor);
                o.SetCutForegroundPatternColor(cFloor);
            }
            else if (id == doorId) { lineColour = cDoor; o.SetSurfaceTransparency(transpDoorsWindows); }
            else if (id == windowId) { o.SetSurfaceTransparency(transpDoorsWindows); }  // stays background grey ON PURPOSE
            else if (blackIds.Contains(id))
            {
                lineColour = cBlack; weight = weightServices;
                o.SetSurfaceTransparency(transpServices);
            }
            else if (insIds.Contains(id))
            {
                lineColour = cIns; weight = weightInsulation;
                o.SetSurfaceTransparency(transpInsulation);
                if (dashPattern != null)
                {
                    o.SetProjectionLinePatternId(dashPattern.Id);
                    o.SetCutLinePatternId(dashPattern.Id);
                }
            }
            else if (equipIds.Contains(id))
            {
                lineColour = cBlack; weight = weightServices;
                o.SetSurfaceTransparency(transpEquipment);
                o.SetSurfaceForegroundPatternColor(cEquip);
                o.SetCutForegroundPatternColor(cEquip);
            }
            else if (grey80Ids.Contains(id)) { o.SetSurfaceTransparency(transpServices); }

            o.SetProjectionLineColor(lineColour);
            o.SetCutLineColor(lineColour);
            o.SetProjectionLineWeight(weight);
            o.SetCutLineWeight(weight);
            view.SetCategoryOverrides(c.Id, o);
            written++;

            // read back THIS category and record what Revit refused
            var back = view.GetCategoryOverrides(c.Id);
            var lost = new List<string>();
            if (!back.ProjectionLineColor.IsValid) lost.Add("proj line");
            if (!back.CutLineColor.IsValid) lost.Add("cut line");
            if (!back.SurfaceForegroundPatternColor.IsValid) lost.Add("surface fill");
            if (!back.CutForegroundPatternColor.IsValid) lost.Add("cut fill");
            if (lost.Count > 0) lostSlots.Add($"{c.Name}: {string.Join("+", lost)}");

            // --- 3. sub-categories carry line colour + weight only ---
            if (c.SubCategories == null) continue;
            var subColour = subCategoriesFollowFinalParent ? lineColour : (id == floorId ? cFloor : (id == doorId ? cDoor : cLine));
            int subWeight = subCategoriesFollowFinalParent ? weight : (id == wallId ? weightWalls : weightBackground);
            foreach (Category sc in c.SubCategories)
            {
                bool ok; try { ok = sc.get_AllowsVisibilityControl(view); } catch { ok = false; }
                if (!ok) continue;
                var so = view.GetCategoryOverrides(sc.Id);
                so.SetProjectionLineColor(subColour);
                so.SetCutLineColor(subColour);
                try { so.SetProjectionLineWeight(subWeight); } catch { }
                try { so.SetCutLineWeight(subWeight); } catch { }
                view.SetCategoryOverrides(sc.Id, so);
                subsWritten++;
            }
        }
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        return sb.ToString() + $"\nFAILED - rolled back, nothing changed. Reason: {ex.Message}";
    }
}

sb.AppendLine($"MEP grayout applied to view '{view.Name}' (Id {view.Id.IntegerValue}, 1:{view.Scale}, {view.DisplayStyle}).");
sb.AppendLine($"  categories written {written} | shown {shown} | hidden {hidden} | not controllable here {notControllable}");
sb.AppendLine($"  sub-categories written {subsWritten} (line colour + weight only — they never hold fill or transparency)");
sb.AppendLine($"  categories where Revit discarded at least one slot: {lostSlots.Count} — expected, see the header");
foreach (var l in lostSlots.Take(40)) sb.AppendLine("    - " + l);
if (view.DisplayStyle == DisplayStyle.HLR || view.DisplayStyle == DisplayStyle.Wireframe)
    sb.AppendLine($"  NOTE: this view is {view.DisplayStyle} — every transparency above is stored but shows nothing. " +
                  "Switch to Shaded / Consistent Colours / Realistic to see them.");
return sb.ToString();
