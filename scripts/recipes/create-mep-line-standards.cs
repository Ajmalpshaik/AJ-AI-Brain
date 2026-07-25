// ============================================================
// RECIPE — create-MEP-line-standards.cs
// PURPOSE: One-click setup of the full MEP drafting line standard in any project:
//          1) Line patterns (dash-dot section, hidden, centre, phantom, demo, match, existing)
//          2) Line styles (MEP_ prefix, max line weight 3 per office rule) — the COMMON drafting
//             set only. Ajmal explicitly does NOT want system/service styles (CHW, drainage,
//             fire fighting, air) — do not add them back.
//          3) Object Styles for Matchline, Callout Boundary, Scope Boxes, Reference Planes,
//             and Grid types (centre dash-dot)
//          4) Filled region types: MEP_Demolition Hatch (diagonal), MEP_Existing Grey (solid)
//          5) A drafting view "MEP_Line_Styles_Legend" listing every MEP line style
// BASIS:   Section/cutting-plane line = thick dash-dot per ISO 128 / ASME Y14.2; hidden = short
//          dash; centre = long dash-dot; phantom = dash-dot-dot. Weights capped at 3.
// NAMING:  MEP stays ALL-CAPS; every other word First-Letter-Capital; underscores between
//          words, NEVER spaces (Ajmal's rule) — e.g. MEP_Hidden_Short_Red, MEP_Line_Styles_Legend.
// IDEMPOTENT: safe to re-run — existing patterns/styles/regions are updated, not duplicated;
//          the legend only appends styles it hasn't listed yet.
// PITFALL: Category.SubCategories is a snapshot — re-fetch the Lines category AFTER creating
//          styles (and Regenerate) before building the legend, or new styles will be missed.
// READ + WRITE — wraps all creation in a Transaction with explicit RollBack on failure.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string prefix = "MEP_";          // office prefix for all patterns/styles
int weightThin = 1;              // thin lines
int weightMedium = 2;            // demolition / service lines
int weightThick = 3;             // section + match line (office max = 3)
bool applyObjectStyles = true;   // matchline / callout / scope box / ref planes / grids
bool createFilledRegions = true;
bool createLegendView = true;
// ---- END INPUTS ----

var doc = Document;
var sb = new System.Text.StringBuilder();
double mm = 1.0 / 304.8; // pattern lengths are paper-space feet

using (var t = new Transaction(doc, "MEP line standards setup"))
{
    t.Start();
    try
    {
        Categories cats = doc.Settings.Categories;
        Category linesCat = cats.get_Item(BuiltInCategory.OST_Lines);

        LinePatternElement EnsurePattern(string name, System.Collections.Generic.List<LinePatternSegment> segs)
        {
            var existing = LinePatternElement.GetLinePatternElementByName(doc, name);
            if (existing != null) { sb.AppendLine("Pattern existed: " + name); return existing; }
            var lp = new LinePattern(name);
            lp.SetSegments(segs);
            var created = LinePatternElement.Create(doc, lp);
            sb.AppendLine("Pattern created: " + name);
            return created;
        }
        LinePatternSegment Dash(double len) { return new LinePatternSegment(LinePatternSegmentType.Dash, len); }
        LinePatternSegment Space(double len) { return new LinePatternSegment(LinePatternSegmentType.Space, len); }
        LinePatternSegment Dot() { return new LinePatternSegment(LinePatternSegmentType.Dot, 0.0); }

        // Segment dimensions adopted from Ajmal's office MODEL PROJECT patterns (2026-07-25)
        var patSection = EnsurePattern(prefix + "Section_Dash-Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(7*mm), Space(1.5*mm), Dot(), Space(1.5*mm) });
        var patHidden = EnsurePattern(prefix + "Hidden_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(3*mm), Space(1.5*mm) });
        var patCentre = EnsurePattern(prefix + "Centre_Dash-Dot",   // office look: long dash + short dash
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(6*mm), Space(2*mm), Dash(2*mm), Space(2*mm) });
        var patPhantom = EnsurePattern(prefix + "Phantom_Dash-Dot-Dot",   // office look: long + 2 short dashes
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(12*mm), Space(2*mm), Dash(2*mm), Space(2*mm), Dash(2*mm), Space(2*mm) });
        var patDemo = EnsurePattern(prefix + "Demolition_Dash",   // office look: dash + short dash
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1*mm), Dash(1.5*mm), Space(1*mm) });
        var patMatch = EnsurePattern(prefix + "Match_Dash-Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(10*mm), Space(2*mm), Dot(), Space(2*mm) });
        var patExisting = EnsurePattern(prefix + "Existing_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(3*mm), Space(1*mm) });
        var patGridLevel = EnsurePattern(prefix + "Grid&Level_Dash-Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(8*mm), Space(2*mm), Dot(), Space(2*mm) });

        Category EnsureStyle(string name, ElementId patternId, int weight, Color color)
        {
            Category style = null;
            foreach (Category c in linesCat.SubCategories) if (c.Name == name) { style = c; break; }
            if (style == null) { style = cats.NewSubcategory(linesCat, name); sb.AppendLine("Style created: " + name); }
            else sb.AppendLine("Style existed (updated): " + name);
            style.SetLinePatternId(patternId, GraphicsStyleType.Projection);
            style.SetLineWeight(weight, GraphicsStyleType.Projection);
            style.LineColor = color;
            return style;
        }

        var black = new Color(0,0,0);
        ElementId solid = LinePatternElement.GetSolidPatternId();

        // Drafting standard set
        EnsureStyle(prefix + "Section_Line",    patSection.Id, weightThick,  black);
        EnsureStyle(prefix + "Hidden_Line",     patHidden.Id,  weightThin,   black);
        EnsureStyle(prefix + "Centre_Line",     patCentre.Id,  weightThin,   black);
        EnsureStyle(prefix + "Phantom_Line",    patPhantom.Id, weightThin,   black);
        EnsureStyle(prefix + "Demolition_Line", patDemo.Id,    weightMedium, black);
        EnsureStyle(prefix + "Match_Line",      patMatch.Id,   weightThick,  black);
        EnsureStyle(prefix + "Break_Line",      solid,         weightThin,   black);
        EnsureStyle(prefix + "Existing_Line",   patExisting.Id, weightThin,  new Color(128,128,128));
        EnsureStyle(prefix + "Grid&Level_Line", patGridLevel.Id, weightThin, black);  // grids + levels share one chain-line style
        EnsureStyle(prefix + "Schedule_Line",   solid,         weightThick,  new Color(127,63,63));  // brown solid w3 — copied from Ajmal's office MODEL PROJECT

        // Hidden-line variants: 3 dash sizes x 5 colours (green is the softer 0,166,0 — Ajmal's spec)
        var patHiddenShort  = EnsurePattern(prefix + "Hidden_Short_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(2*mm), Space(1*mm) });
        var patHiddenMedium = EnsurePattern(prefix + "Hidden_Medium_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1.5*mm) });
        var patHiddenLarge  = EnsurePattern(prefix + "Hidden_Large_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(8*mm), Space(2*mm) });
        var hiddenColours = new System.Collections.Generic.Dictionary<string, Color>{
            {"Black", black}, {"Red", new Color(255,0,0)}, {"Orange", new Color(255,128,0)},
            {"Green", new Color(0,166,0)}, {"Blue", new Color(0,0,255)} };
        foreach (var kv in hiddenColours)
        {
            EnsureStyle(prefix + "Hidden_Short_" + kv.Key,  patHiddenShort.Id,  weightThick, kv.Value);
            EnsureStyle(prefix + "Hidden_Medium_" + kv.Key, patHiddenMedium.Id, weightThick, kv.Value);
            EnsureStyle(prefix + "Hidden_Large_" + kv.Key,  patHiddenLarge.Id,  weightThick, kv.Value);
        }

        // Extra dash patterns: double/triple dash, dotted, long dash, long-short (boundary)
        var patDouble = EnsurePattern(prefix + "Double_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1*mm), Dash(4*mm), Space(1.5*mm) });
        var patTriple = EnsurePattern(prefix + "Triple_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(3*mm), Space(1*mm), Dash(3*mm), Space(1*mm), Dash(3*mm), Space(1.5*mm) });
        var patDotted = EnsurePattern(prefix + "Dotted",
            new System.Collections.Generic.List<LinePatternSegment>{ Dot(), Space(1.5*mm) });
        var patLong = EnsurePattern(prefix + "Long_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(8*mm), Space(2*mm) });
        var patLongShort = EnsurePattern(prefix + "Long-Short_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(12*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm) });
        EnsureStyle(prefix + "Double_Dash_Line",     patDouble.Id,    weightMedium, black);
        EnsureStyle(prefix + "Triple_Dash_Line",     patTriple.Id,    weightMedium, black);
        EnsureStyle(prefix + "Dotted_Line",          patDotted.Id,    weightThin,   black);
        EnsureStyle(prefix + "Long_Dash_Line",       patLong.Id,      weightMedium, black);
        EnsureStyle(prefix + "Long-Short_Dash_Line", patLongShort.Id, weightMedium, black);
        var patLong2 = EnsurePattern(prefix + "Long_Two_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(12*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm) });
        var patLong3 = EnsurePattern(prefix + "Long_Three_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(12*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm), Dash(3*mm), Space(1.5*mm) });
        EnsureStyle(prefix + "Long_Two_Dash_Line",   patLong2.Id, weightMedium, black);
        EnsureStyle(prefix + "Long_Three_Dash_Line", patLong3.Id, weightMedium, black);

        // Consolidated office kinds (one type each, patterns copied from MODEL PROJECT 2026-07-25,
        // weights per ISO 128-2 roles: thin=1, medium=2, thick=3)
        var patAligning = EnsurePattern(prefix + "Aligning_Dash-Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1.5*mm), Dot(), Space(1.5*mm) });
        var patBorder = EnsurePattern(prefix + "Border_Long-Short",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(12*mm), Space(2*mm), Dash(2*mm), Space(2*mm) });
        var patBoundary = EnsurePattern(prefix + "Boundary_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(5*mm), Space(1.5*mm) });
        var patChain = EnsurePattern(prefix + "Chain",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(8*mm), Space(1.5*mm), Dot(), Space(1.5*mm), Dash(2*mm), Space(1.5*mm) });
        var patDash = EnsurePattern(prefix + "Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(2*mm) });
        var patDashDot = EnsurePattern(prefix + "Dash_Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(5*mm), Space(1.5*mm), Dot(), Space(1.5*mm) });
        var patDashDotDot = EnsurePattern(prefix + "Dash_Dot_Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(5*mm), Space(1.5*mm), Dot(), Space(1.5*mm), Dot(), Space(1.5*mm) });
        var patDetail = EnsurePattern(prefix + "Detail_Dash-Dot",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(0.8*mm), Dot(), Space(0.8*mm) });
        var patFlow = EnsurePattern(prefix + "Flow_Direction",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1*mm), Dot(), Space(1*mm), Dash(2*mm), Space(1*mm) });
        var patLoose = EnsurePattern(prefix + "Loose_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(4*mm) });
        var patOutline = EnsurePattern(prefix + "Outline_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1*mm), Dash(1.5*mm), Space(1*mm) });
        var patOverhead = EnsurePattern(prefix + "Overhead_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(6*mm), Space(1.5*mm), Dash(2*mm), Space(1.5*mm) });
        var patProperty = EnsurePattern(prefix + "Property",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(6*mm), Space(1.5*mm), Dash(2*mm), Space(1.5*mm), Dot(), Space(1.5*mm) });
        var patProposed = EnsurePattern(prefix + "Proposed_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(5*mm), Space(1*mm) });
        var patReference = EnsurePattern(prefix + "Reference_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(6*mm), Space(3*mm) });
        var patStitch = EnsurePattern(prefix + "Stitch",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(2*mm), Space(1*mm), Dot(), Space(1*mm), Dot(), Space(1*mm) });
        var patTitle = EnsurePattern(prefix + "Title_Dash",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(10*mm), Space(2.5*mm) });
        var patTrack = EnsurePattern(prefix + "Track",
            new System.Collections.Generic.List<LinePatternSegment>{ Dash(4*mm), Space(1*mm), Dash(1.5*mm), Space(1*mm), Dash(1.5*mm), Space(1.5*mm) });

        EnsureStyle(prefix + "Aligning_Line",       patAligning.Id,   weightThin,   black);
        EnsureStyle(prefix + "Border_Line",         patBorder.Id,     weightThick,  black);
        EnsureStyle(prefix + "Boundary_Line",       patBoundary.Id,   weightMedium, black);
        EnsureStyle(prefix + "Chain_Line",          patChain.Id,      weightThin,   black);
        EnsureStyle(prefix + "Dash_Line",           patDash.Id,       weightThin,   black);
        EnsureStyle(prefix + "Dash_Dot_Line",       patDashDot.Id,    weightThin,   black);
        EnsureStyle(prefix + "Dash_Dot_Dot_Line",   patDashDotDot.Id, weightThin,   black);
        EnsureStyle(prefix + "Detail_Line",         patDetail.Id,     weightMedium, black);
        EnsureStyle(prefix + "Flow_Direction_Line", patFlow.Id,       weightMedium, black);
        EnsureStyle(prefix + "Loose_Dash_Line",     patLoose.Id,      weightThin,   black);
        EnsureStyle(prefix + "Outline_Line",        patOutline.Id,    weightThick,  black);
        EnsureStyle(prefix + "Overhead_Line",       patOverhead.Id,   weightThin,   black);
        EnsureStyle(prefix + "Property_Line",       patProperty.Id,   weightThick,  black);
        EnsureStyle(prefix + "Proposed_Line",       patProposed.Id,   weightMedium, new Color(0,92,184));
        EnsureStyle(prefix + "Rebar_Cover_Line",    patExisting.Id,   weightThin,   black);  // same 3/1 dash as Existing
        EnsureStyle(prefix + "Reference_Line",      patReference.Id,  weightThin,   black);
        EnsureStyle(prefix + "Stitch_Line",         patStitch.Id,     weightThin,   black);
        EnsureStyle(prefix + "Title_Line",          patTitle.Id,      weightThick,  black);
        EnsureStyle(prefix + "Track_Line",          patTrack.Id,      weightMedium, black);

        if (applyObjectStyles)
        {
            try
            {
                Category matchCat = cats.get_Item(BuiltInCategory.OST_Matchline);
                matchCat.SetLinePatternId(patMatch.Id, GraphicsStyleType.Projection);
                matchCat.SetLineWeight(weightThick, GraphicsStyleType.Projection);
                sb.AppendLine("Matchline category updated.");
            }
            catch (System.Exception ex) { sb.AppendLine("Matchline skipped: " + ex.Message); }

            try
            {
                Category cbCat = null;
                try { cbCat = cats.get_Item(BuiltInCategory.OST_CalloutBoundary); } catch { }
                if (cbCat == null)
                {
                    Category callouts = cats.get_Item(BuiltInCategory.OST_Callouts);
                    foreach (Category c in callouts.SubCategories)
                        if (c.Name.ToLower().Contains("boundary")) { cbCat = c; break; }
                }
                if (cbCat != null)
                {
                    cbCat.SetLinePatternId(patSection.Id, GraphicsStyleType.Projection);
                    cbCat.SetLineWeight(weightMedium, GraphicsStyleType.Projection);
                    sb.AppendLine("Callout Boundary updated.");
                }
            }
            catch (System.Exception ex) { sb.AppendLine("Callout Boundary skipped: " + ex.Message); }

            try
            {
                Category scopeCat = cats.get_Item(BuiltInCategory.OST_VolumeOfInterest);
                scopeCat.SetLinePatternId(patCentre.Id, GraphicsStyleType.Projection);
                scopeCat.SetLineWeight(weightThin, GraphicsStyleType.Projection);
                sb.AppendLine("Scope Boxes updated.");
            }
            catch (System.Exception ex) { sb.AppendLine("Scope Boxes skipped: " + ex.Message); }

            try
            {
                Category refPlanes = cats.get_Item(BuiltInCategory.OST_CLines);
                refPlanes.SetLinePatternId(patCentre.Id, GraphicsStyleType.Projection);
                sb.AppendLine("Reference Planes updated.");
            }
            catch (System.Exception ex) { sb.AppendLine("Reference Planes skipped: " + ex.Message); }

            try
            {
                var gridTypes = new FilteredElementCollector(doc).OfClass(typeof(ElementType)).OfCategory(BuiltInCategory.OST_Grids).ToElements();
                int gridsDone = 0;
                foreach (Element gt in gridTypes)
                {
                    bool touched = false;
                    var pEnd = gt.LookupParameter("End Segment Pattern");
                    if (pEnd != null && !pEnd.IsReadOnly) { pEnd.Set(patGridLevel.Id); touched = true; }
                    var pCen = gt.LookupParameter("Center Segment Pattern");
                    if (pCen != null && !pCen.IsReadOnly) { pCen.Set(patGridLevel.Id); touched = true; }
                    if (touched) gridsDone++;
                }
                sb.AppendLine("Grid types updated: " + gridsDone + " of " + gridTypes.Count);
            }
            catch (System.Exception ex) { sb.AppendLine("Grid types skipped: " + ex.Message); }

            try
            {
                int levelsDone = 0;
                foreach (Element lt in new FilteredElementCollector(doc).OfClass(typeof(ElementType)).OfCategory(BuiltInCategory.OST_Levels))
                {
                    var pl = lt.LookupParameter("Line Pattern");
                    if (pl != null && !pl.IsReadOnly) { pl.Set(patGridLevel.Id); levelsDone++; }
                }
                sb.AppendLine("Level types updated: " + levelsDone);
            }
            catch (System.Exception ex) { sb.AppendLine("Level types skipped: " + ex.Message); }
        }

        if (createFilledRegions)
        {
            try
            {
                FilledRegionType baseFrt = null;
                foreach (FilledRegionType f in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType))) { baseFrt = f; break; }
                if (baseFrt == null) sb.AppendLine("Filled regions skipped: no base type.");
                else
                {
                    FillPatternElement diag = null, solidFill = null;
                    foreach (FillPatternElement fp in new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)))
                    {
                        var pat = fp.GetFillPattern();
                        if (pat.Target != FillPatternTarget.Drafting) continue;
                        if (pat.IsSolidFill && solidFill == null) solidFill = fp;
                        if (!pat.IsSolidFill && diag == null && fp.Name.ToLower().Contains("diagonal")) diag = fp;
                    }
                    bool demoExists = false, existExists = false;
                    foreach (FilledRegionType f in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)))
                    { if (f.Name == prefix + "Demolition_Hatch") demoExists = true; if (f.Name == prefix + "Existing_Grey") existExists = true; }

                    if (!demoExists && diag != null)
                    {
                        var demo = baseFrt.Duplicate(prefix + "Demolition_Hatch") as FilledRegionType;
                        demo.ForegroundPatternId = diag.Id;
                        demo.ForegroundPatternColor = black;
                        demo.BackgroundPatternId = ElementId.InvalidElementId;
                        sb.AppendLine("Filled region created: " + prefix + "Demolition_Hatch");
                    }
                    if (!existExists && solidFill != null)
                    {
                        var exist = baseFrt.Duplicate(prefix + "Existing_Grey") as FilledRegionType;
                        exist.ForegroundPatternId = solidFill.Id;
                        exist.ForegroundPatternColor = new Color(192,192,192);
                        exist.BackgroundPatternId = ElementId.InvalidElementId;
                        sb.AppendLine("Filled region created: " + prefix + "Existing_Grey");
                    }
                }
            }
            catch (System.Exception ex) { sb.AppendLine("Filled regions skipped: " + ex.Message); }
        }

        doc.Regenerate();

        if (createLegendView)
        {
            try
            {
                string legendName = prefix + "Line_Styles_Legend";
                View legend = null;
                foreach (View v in new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)))
                    if (v.Name == legendName) { legend = v; break; }
                if (legend == null)
                {
                    ViewFamilyType draftVft = null;
                    foreach (ViewFamilyType vft in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)))
                        if (vft.ViewFamily == ViewFamily.Drafting) { draftVft = vft; break; }
                    var vd = ViewDrafting.Create(doc, draftVft.Id);
                    vd.Name = legendName;
                    vd.Scale = 1;
                    legend = vd;
                    sb.AppendLine("Legend view created: " + legendName);
                }

                var listed = new System.Collections.Generic.HashSet<string>();
                int rows = 0;
                foreach (TextNote tn in new FilteredElementCollector(doc, legend.Id).OfClass(typeof(TextNote)))
                { listed.Add(tn.Text.Trim()); rows++; }

                // Re-fetch Lines category AFTER style creation (snapshot pitfall — see header)
                Category linesFresh = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
                var toDraw = new System.Collections.Generic.List<Category>();
                foreach (Category c in linesFresh.SubCategories)
                    if (c.Name.StartsWith(prefix) && !listed.Contains(c.Name)) toDraw.Add(c);
                toDraw.Sort((a,b) => string.Compare(a.Name, b.Name));

                ElementId textTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                if (textTypeId == ElementId.InvalidElementId)
                    foreach (TextNoteType tnt in new FilteredElementCollector(doc).OfClass(typeof(TextNoteType))) { textTypeId = tnt.Id; break; }

                double y = -8.0 * mm * rows;
                foreach (Category styleCat in toDraw)
                {
                    Line geom = Line.CreateBound(new XYZ(0, y, 0), new XYZ(50.0*mm, y, 0));
                    DetailCurve dc = doc.Create.NewDetailCurve(legend, geom);
                    GraphicsStyle gs = styleCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                    if (gs != null) dc.LineStyle = gs;
                    if (textTypeId != ElementId.InvalidElementId)
                        TextNote.Create(doc, legend.Id, new XYZ(55.0*mm, y + 1.5*mm, 0), styleCat.Name, textTypeId);
                    y -= 8.0*mm;
                }
                sb.AppendLine("Legend rows added: " + toDraw.Count);
            }
            catch (System.Exception ex) { sb.AppendLine("Legend skipped: " + ex.Message); }
        }

        t.Commit();
    }
    catch (System.Exception ex)
    {
        t.RollBack();
        return "FAILED (rolled back, nothing changed): " + ex.Message;
    }
}
return sb.ToString();

