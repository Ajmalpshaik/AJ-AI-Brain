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
                // A BoundingBoxXYZ starts with an IDENTITY Transform, and CreateSection reads the look
                // direction from that Transform's BasisZ. Left as identity the section looks straight
                // DOWN — measured live 2026-08-07, the resulting view had ViewDirection (0,0,-1), the
                // same axis as a floor plan, so it cut a plan-like slice rather than a section and still
                // reported "Created section view". ../../knowledge/live-model/views.md already spelled
                // out the basis this needs; it just was not being built.
                //
                // BasisX = the section's left-right (in-view) axis
                // BasisY = up, which for a normal vertical section is global Z
                // BasisZ = the direction the camera looks, and it MUST equal BasisX cross BasisY
                //          or Revit throws with an EMPTY exception Message (views.md again).
                Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

                double minX = mm(sectionMinXMm), maxX = mm(sectionMaxXMm);
                double minY = mm(sectionMinYMm), maxY = mm(sectionMaxYMm);
                double minZ = mm(sectionMinZMm), maxZ = mm(sectionMaxZMm);

                // Run the section along the box's LONGER plan side, which is what the header promises.
                bool alongX = (maxX - minX) >= (maxY - minY);
                XYZ basisX = alongX ? XYZ.BasisX : XYZ.BasisY;      // in-view left-right
                XYZ basisY = XYZ.BasisZ;                             // in-view up
                XYZ basisZ = basisX.CrossProduct(basisY);            // look direction, right-handed by construction

                var origin = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
                var sectionTransform = Transform.Identity;
                sectionTransform.Origin = origin;
                sectionTransform.BasisX = basisX;
                sectionTransform.BasisY = basisY;
                sectionTransform.BasisZ = basisZ;

                // Box extents are now expressed IN THE SECTION'S OWN coordinates: X across the view,
                // Y up the view, Z = how deep the cut looks.
                double halfAcross = alongX ? (maxX - minX) / 2 : (maxY - minY) / 2;
                double halfDepth  = alongX ? (maxY - minY) / 2 : (maxX - minX) / 2;
                var box = new BoundingBoxXYZ
                {
                    Transform = sectionTransform,
                    Min = new XYZ(-halfAcross, -(maxZ - minZ) / 2, -halfDepth),
                    Max = new XYZ(halfAcross, (maxZ - minZ) / 2, halfDepth)
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
            // Report the direction the view actually looks, read back off the view. For a section that
            // is the whole question — a section looking down the Z axis is a plan-shaped slice, not a
            // section, and the old message could not tell the two apart.
            var vd = newView.ViewDirection;
            sb.AppendLine($"Created {viewKind} view '{newView.Name}' (Id {newView.Id.IntegerValue}), looking ({vd.X:0.##}, {vd.Y:0.##}, {vd.Z:0.##}).");
            if (viewKind == "section" && Math.Abs(vd.Z) > 0.9)
                sb.AppendLine("  WARNING: this 'section' is looking almost straight up/down — that is a plan-shaped cut, not a section. Check the section box extents.");
        }
    }
    catch (Exception ex)
    {
        try { t.RollBack(); } catch { }
        // views.md: CreateSection's exception Message comes back as an EMPTY string when the Transform
        // is not right-handed, so print the type name too or the failure reads as "no reason given".
        sb.AppendLine($"FAILED to create view — rolled back, nothing changed. Reason: {ex.GetType().Name}: {(string.IsNullOrEmpty(ex.Message) ? "(Revit gave no message — for a section this usually means the section box Transform is not a right-handed basis; see knowledge/live-model/views.md)" : ex.Message)}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
