// ============================================================
// RECIPE — create-mep-text-standards.cs
// PURPOSE: One-click setup of Ajmal's MEP text annotation standard in any project:
//          1) 120 text note types — the full matrix: 6 sizes x 10 colours x box/no-box,
//             named MEP_Anno_Arial_[Size]mm[_Colour][_Box]  (Black = NO colour suffix)
//          2) A drafting view "MEP_Text_Styles_Legend" (1:1) cataloguing every type,
//             one column per colour group with 95 mm distance between groups
// NAMING:  Prefix ALL-CAPS (MEP), other words First-Letter-Capital, underscores never
//          spaces. Font is fixed: Arial (Ajmal's decision — always available on any PC).
// COLOURS: Black(default) Red(255,0,0) Gray(128,128,128) Yellow(255,255,0)
//          Green(0,166,0 — the softer green, NEVER 0,255,0 or 0,128,0)
//          Blue(0,0,255) Brown(127,63,63) Orange(255,127,0) Mustard(165,124,0) Maroon(76,0,0)
// SETTINGS: Regular (no bold/italic), width factor 1.0, background Opaque, border via _Box.
// IDEMPOTENT: existing types are kept, only missing ones are created; the legend view is
//          reused if it already exists (samples are appended fresh each run — delete old
//          samples first if re-running on a populated legend).
// READ + WRITE — wraps all creation in a Transaction with explicit RollBack on failure.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string namePrefix = "MEP_Anno_Arial_";   // type name prefix (font is part of the name)
bool createLegendView = true;
string legendName = "MEP_Text_Styles_Legend";
// ---- END INPUTS ----

var doc = Document;
var sb = new System.Text.StringBuilder();
double mm = 1.0 / 304.8;

var sizes = new[] { "2", "2.5", "3", "3.5", "5", "6" };
var sizeVals = new[] { 2.0, 2.5, 3.0, 3.5, 5.0, 6.0 };
var colourNames = new[] { "", "Red", "Gray", "Yellow", "Green", "Blue", "Brown", "Orange", "Mustard", "Maroon" };
var headers = new[] { "BLACK", "RED", "GRAY", "YELLOW", "GREEN", "BLUE", "BROWN", "ORANGE", "MUSTARD", "MAROON" };
var colourInts = new[] {
    0,                          // black
    255,                        // red 255,0,0
    128 + (128<<8) + (128<<16), // gray
    255 + (255<<8),             // yellow
    (166<<8),                   // green 0,166,0 (Ajmal's green)
    (255<<16),                  // blue
    127 + (63<<8) + (63<<16),   // brown 127,63,63
    255 + (127<<8),             // orange 255,127,0
    165 + (124<<8),             // mustard 165,124,0
    76                          // maroon 76,0,0
};

using (var t = new Transaction(doc, "MEP text standards setup"))
{
    t.Start();
    try
    {
        TextNoteType baseType = null;
        foreach (TextNoteType x in new FilteredElementCollector(doc).OfClass(typeof(TextNoteType))) { baseType = x; break; }
        if (baseType == null) { t.RollBack(); return "No text type exists in project - cannot duplicate."; }

        var types = new System.Collections.Generic.Dictionary<string, TextNoteType>();
        foreach (TextNoteType x in new FilteredElementCollector(doc).OfClass(typeof(TextNoteType))) types[x.Name] = x;

        int created = 0;
        for (int c = 0; c < colourNames.Length; c++)
        {
            for (int s = 0; s < sizes.Length; s++)
            {
                for (int b = 0; b < 2; b++)
                {
                    string name = namePrefix + sizes[s] + "mm"
                        + (colourNames[c] == "" ? "" : "_" + colourNames[c])
                        + (b == 1 ? "_Box" : "");
                    if (types.ContainsKey(name)) continue;
                    var nt = baseType.Duplicate(name) as TextNoteType;
                    var p = nt.get_Parameter(BuiltInParameter.TEXT_FONT); if (p != null) p.Set("Arial");
                    p = nt.get_Parameter(BuiltInParameter.TEXT_SIZE); if (p != null) p.Set(sizeVals[s] / 304.8);
                    p = nt.get_Parameter(BuiltInParameter.LINE_COLOR); if (p != null) p.Set(colourInts[c]);
                    p = nt.get_Parameter(BuiltInParameter.TEXT_BOX_VISIBILITY); if (p != null) p.Set(b);
                    if (b == 1) { p = nt.get_Parameter(BuiltInParameter.LEADER_OFFSET_SHEET); if (p != null) p.Set(0.5 / 304.8); } // 0.5mm border offset on _Box types
                    p = nt.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD); if (p != null) p.Set(0);
                    p = nt.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC); if (p != null) p.Set(0);
                    p = nt.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE); if (p != null) p.Set(0);
                    p = nt.get_Parameter(BuiltInParameter.TEXT_BACKGROUND); if (p != null) p.Set(0);
                    p = nt.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE); if (p != null) p.Set(1.0);
                    types[name] = nt;
                    created++;
                }
            }
        }
        sb.AppendLine("Text types created: " + created);

        if (createLegendView)
        {
            View legend = null;
            foreach (View dv in new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)))
                if (dv.Name == legendName) { legend = dv; break; }
            if (legend == null)
            {
                ViewFamilyType draftVft = null;
                foreach (ViewFamilyType vft in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)))
                    if (vft.ViewFamily == ViewFamily.Drafting) { draftVft = vft; break; }
                var vd = ViewDrafting.Create(doc, draftVft.Id);
                vd.Name = legendName;
                vd.Scale = 1;
                legend = vd;
                sb.AppendLine("View created: " + legendName);
            }
            double sc = legend.Scale;
            double colW = 160.0 * mm * sc;  // distance between colour groups (long 6mm names need ~110mm)
            double subGap = 10.0 * mm * sc;
            int placed = 0;

            for (int c = 0; c < colourNames.Length; c++)
            {
                double x = c * colW;
                double y = 0;
                TextNoteType ht;
                if (types.TryGetValue(namePrefix + "3.5mm" + (colourNames[c] == "" ? "" : "_" + colourNames[c]) + "_Box", out ht))
                    TextNote.Create(doc, legend.Id, new XYZ(x, y, 0), headers[c], ht.Id);
                y -= 14.0 * mm * sc;

                for (int s = 0; s < sizes.Length; s++)
                {
                    TextNoteType tt2;
                    string name = namePrefix + sizes[s] + "mm" + (colourNames[c] == "" ? "" : "_" + colourNames[c]);
                    if (types.TryGetValue(name, out tt2))
                    { TextNote.Create(doc, legend.Id, new XYZ(x, y, 0), name, tt2.Id); placed++; }
                    y -= (sizeVals[s] * 2.0 + 4.0) * mm * sc;   // row height grows with text size
                }
                y -= subGap;

                for (int s = 0; s < sizes.Length; s++)
                {
                    TextNoteType tt2;
                    string name = namePrefix + sizes[s] + "mm" + (colourNames[c] == "" ? "" : "_" + colourNames[c]) + "_Box";
                    if (types.TryGetValue(name, out tt2))
                    { TextNote.Create(doc, legend.Id, new XYZ(x, y, 0), name, tt2.Id); placed++; }
                    y -= (sizeVals[s] * 2.0 + 5.0) * mm * sc;   // a bit taller for boxed rows
                }
            }
            sb.AppendLine("Samples placed: " + placed);
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
