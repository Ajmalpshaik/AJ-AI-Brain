// ============================================================
// FRAGMENT (creator) — create-view.cs
// PURPOSE: Create a new View — Floor Plan (at a given Level), 3D (isometric), or Section (through a given
//          mm box). The three genuinely simple, reliable ViewFamily cases; a Callout/Elevation/Drafting
//          view needs a different, more specific creation call not attempted here.
// PRODUCES: elements (List<Element>, the single newly created View, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so action-apply-view-template.cs or action-rename-element.cs can
//          chain onto it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string viewKind = "floor_plan"; // "floor_plan" | "three_d" | "section"
int levelIdInt = 0;             // required for mode="floor_plan"
string newName = null;          // null = keep Revit's default generated name
// mode="section" only — an axis-aligned mm box the section cuts through; minZ/maxZ set the vertical extent,
// the section plane itself runs along the box's own long side (Revit infers orientation from the box).
double sectionMinXMm = 0, sectionMinYMm = 0, sectionMinZMm = 0;
double sectionMaxXMm = 1000, sectionMaxYMm = 5000, sectionMaxZMm = 3000;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Create View"))
{
    t.Start();
    try
    {
        View newView = null;

        if (viewKind == "floor_plan")
        {
            var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
            if (level == null) { t.RollBack(); sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level."); }
            else
            {
                var vft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);
                if (vft == null) { t.RollBack(); sb.AppendLine("No Floor Plan ViewFamilyType found in the project."); }
                else
                {
                    newView = ViewPlan.Create(Document, vft.Id, level.Id);
                }
            }
        }
        else if (viewKind == "three_d")
        {
            var vft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            if (vft == null) { t.RollBack(); sb.AppendLine("No 3D ViewFamilyType found in the project."); }
            else
            {
                newView = View3D.CreateIsometric(Document, vft.Id);
            }
        }
        else if (viewKind == "section")
        {
            var vft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
            if (vft == null) { t.RollBack(); sb.AppendLine("No Section ViewFamilyType found in the project."); }
            else
            {
                Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
                var box = new BoundingBoxXYZ
                {
                    Min = new XYZ(mm(sectionMinXMm), mm(sectionMinYMm), mm(sectionMinZMm)),
                    Max = new XYZ(mm(sectionMaxXMm), mm(sectionMaxYMm), mm(sectionMaxZMm))
                };
                newView = ViewSection.CreateSection(Document, vft.Id, box);
            }
        }
        else
        {
            t.RollBack();
            sb.AppendLine($"Unknown viewKind '{viewKind}' — use \"floor_plan\", \"three_d\", or \"section\".");
        }

        if (newView != null)
        {
            if (!string.IsNullOrEmpty(newName))
            {
                try { newView.Name = newName; } catch { } // name collision — view still created, keeps default name
            }
            elements.Add(newView);
            t.Commit();
            sb.AppendLine($"Created {viewKind} view '{newView.Name}' (Id {newView.Id.IntegerValue}).");
        }
    }
    catch (Exception ex)
    {
        try { t.RollBack(); } catch { }
        sb.AppendLine($"FAILED to create view — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
