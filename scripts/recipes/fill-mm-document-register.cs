// ============================================================
// RECIPE — fill-mm-document-register.cs
// PURPOSE: Fill the MM_ document/handover register on one category in one pass — the fixed-value columns
//          (CWA, MM_NP System Type, MM_Discipline Code, MM_Main Document Definition, MM_Main Drawing
//          Statement, MM_Main Drawing Revision) plus two that are DERIVED, never typed: CWA and
//          Sub-Discipline come out of the model file name, and MM_Main Drawing Number comes from **which
//          sheet actually shows the element**.
//          Built and proven 2026-08-19 on 4355-BHVD-3D-60P00-BL006A across air terminals (45), duct
//          accessories (88), flex ducts (6) and mechanical equipment (21) — 160 elements, 0 failures.
// STANDALONE — has its own sb and returns; not filter+action shaped.
//
// THE IDEA THAT MAKES THE DRAWING NUMBER RIGHT: do not derive it from the element's LEVEL. Ask Revit what
// is VISIBLE in each candidate sheet's plan views — `new FilteredElementCollector(doc, viewId)` — because
// that respects view range, crop region, hidden elements and view filters. The register then says where
// the item is genuinely *drawn*, which is what a fitter on site needs, rather than where it happens to sit.
//
// ***THE RULE THAT IS NOT OBVIOUS AND COST A CORRECTION (2026-08-19):*** the candidate sheets are NOT the
// same for every category. Ducting items (terminals, accessories, flex duct) belong to the **duct layout**
// sheets; equipment belongs to the separate **equipment layout** sheet. Ajmal's own words: *"for the
// mechanical equipment do not check in the ducting layout and sheets, there is separate drawings for the
// equipments"*. Run the equipment category against the equipment sheets or every value is wrong — and
// wrong silently, because every element still gets a plausible sheet number.
//   EXCEPTION found the same day: a few items sit in the equipment CATEGORY but are drawn on the duct
//   sheets (exhaust louvres, categorised as Mechanical Equipment). Ajmal's ruling: *"louvres you need to
//   refer ducting layout"*. So an element that is not on the category's own sheets is NOT an error — check
//   where it really appears before blanking or forcing it.
//
// SAFETY BUILT IN, and each one earned its place:
//   - Document pinned by TITLE, never `Document`. With two projects open the bridge follows whatever Revit
//     has in front and it changes between calls — see knowledge/live-model/core.md.
//   - A fixed value is written only into a BLANK. An element already holding a DIFFERENT value is left
//     alone and reported, never overwritten.
//   - If any element is visible on more than one candidate sheet, the drawing-number pass writes NOTHING
//     and reports the conflict — an arbitrary pick would be indistinguishable from a correct answer.
//   - One transaction per column, so Revit's Undo steps back one column at a time.
//   - Reads every value back OUT of the model afterwards; the write's own return value is not the evidence.
//
// SOURCE: the sheet-visibility technique is the same `FilteredElementCollector(doc, viewId)` walk used by
//          actions/sheets-views/action-report-schedule-definition.cs to resolve what a schedule collects.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetDocTitle = "4355-BHVD-3D-60P00-BL006A";
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;

Document doc = null;
foreach (Document d in Application.Documents)
    if (!d.IsLinked && d.Title == targetDocTitle) doc = d;
if (doc == null) return "ABORTED: '" + targetDocTitle + "' is not open. Nothing changed.";

// ***CWA IS NOT A FIXED VALUE — IT IS READ OUT OF THE MODEL NAME.*** Ajmal's rule, 2026-08-19, in his own
// words: *"CWA it will not be always 60P00, this is as per the model name... after 3D what is that 60P00,
// so that's why it came"*. The document title is `<project>-<disc><sub>-3D-<CWA>-<block>`, e.g.
// `4355-BHVD-3D-60P00-BL006A` -> CWA `60P00`; `4355-BHVD-3D-60A10-BL002A` -> CWA `60A10`. Derive it, never
// carry it over from the last model. The token after `3D` is the Construction Work Area.
// SUB-DISCIPLINE comes out of the SAME name — Ajmal confirmed 2026-08-19: *"yes Sub-Discipline also from
// model name BHVD"*. Token 2 is the discipline pair `BHVD` = sub-discipline `BH` + `VD`, and the sheets
// prove the split by writing it as `4355-BH-VD-VL224880700`. So Sub-Discipline is the FIRST TWO characters
// of the token before `3D`. Derive it; never carry it from the last model.
string cwa = "", subDiscipline = "";
{
    var parts = doc.Title.Split('-');
    for (int i = 0; i < parts.Length - 1; i++)
        if (parts[i].Equals("3D", StringComparison.OrdinalIgnoreCase))
        {
            cwa = parts[i + 1];
            if (i >= 1 && parts[i - 1].Length >= 2) subDiscipline = parts[i - 1].Substring(0, 2);
            break;
        }
}

// the remaining fixed columns. Values are per PROJECT, not universal — confirm them fresh every time.
var fixedVals = new List<KeyValuePair<string,string>> {
    new KeyValuePair<string,string>("CWA", cwa),
    new KeyValuePair<string,string>("MM_NP System Type","NPD"),
    new KeyValuePair<string,string>("MM_Discipline Code","H"),
    new KeyValuePair<string,string>("MM_Main Document Definition","Drawing"),
    new KeyValuePair<string,string>("MM_Main Drawing Statement","NA"),
    new KeyValuePair<string,string>("MM_Main Drawing Revision","DD"),
    new KeyValuePair<string,string>("Sub-Discipline", subDiscipline)
};

// the sheets that DOCUMENT this category — duct layouts for ducting, equipment layouts for equipment
string[] candidateSheetNumbers = {
    "4355-BH-VD-VL224880700",   // HVAC Air Ducts Layouts - Ground Floor
    "4355-BH-VD-VL224880709",   // HVAC Air Ducts Layouts - First Floor
    "4355-BH-VD-VL224880701"    // HVAC Air Ducts Layouts - Roof Plan
};
string drawingNumberParam = "MM_Main Drawing Number";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
if (string.IsNullOrWhiteSpace(cwa) || string.IsNullOrWhiteSpace(subDiscipline))
    return "ABORTED: could not read CWA and Sub-Discipline out of the document title '" + targetDocTitle
         + "'. Expected <project>-<disc><sub>-3D-<CWA>-<block>, e.g. 4355-BHVD-3D-60P00-BL006A. Nothing changed."
         + " Guessing either one is worse than stopping — they end up on a handover register.";
sb.AppendLine("From the model name -> CWA \"" + cwa + "\" | Sub-Discipline \"" + subDiscipline + "\"");

var els = new FilteredElementCollector(doc).OfCategory(targetCategory)
    .WhereElementIsNotElementType().ToElements();
sb.AppendLine("Document: " + doc.Title + " | " + targetCategory + ": " + els.Count);
sb.AppendLine();

// ---- 1. the fixed columns ----
foreach (var j in fixedVals)
{
    int set = 0, already = 0, differs = 0, missing = 0;
    var differsVals = new List<string>();
    using (var t = new Transaction(doc, "AJ Tools - Set " + j.Key))
    {
        t.Start();
        try
        {
            foreach (var e in els)
            {
                var p = e.LookupParameter(j.Key);
                if (p == null || p.IsReadOnly) { missing++; continue; }
                var v = p.AsString();
                if (v == j.Value) { already++; continue; }
                if (!string.IsNullOrWhiteSpace(v)) { differs++; differsVals.Add(v); continue; }
                if (p.Set(j.Value)) set++;
            }
            t.Commit();
        }
        catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED " + j.Key + " - rolled back. " + ex.Message); continue; }
    }
    sb.AppendLine("  " + j.Key.PadRight(30) + (missing == els.Count
        ? "-- PARAMETER NOT BOUND TO THIS CATEGORY -- fix in Manage > Project Parameters"
        : "= \"" + j.Value + "\"  set " + set + " | already " + already
          + (differs > 0 ? " | LEFT ALONE " + differs + " (" + string.Join(", ", differsVals.Distinct().Take(5).Select(x => "\"" + x + "\"")) + ")" : "")
          + (missing > 0 ? " | param missing on " + missing : "")));
}

// ---- 2. drawing number, from what each sheet actually SHOWS ----
var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
    .Where(s => candidateSheetNumbers.Contains(s.SheetNumber)).ToList();

var map = new Dictionary<int,string>();
var conflict = new Dictionary<int,List<string>>();
foreach (var sh in sheets.OrderBy(s => s.SheetNumber))
    foreach (var vid in sh.GetAllPlacedViews())
    {
        var v = doc.GetElement(vid) as View;
        if (v.ViewType != ViewType.FloorPlan && v.ViewType != ViewType.CeilingPlan
            && v.ViewType != ViewType.EngineeringPlan) continue;
        foreach (var id in new FilteredElementCollector(doc, vid).OfCategory(targetCategory)
            .WhereElementIsNotElementType().ToElementIds())
        {
            int k = id.IntegerValue;
            if (map.ContainsKey(k) && map[k] != sh.SheetNumber)
            { if (!conflict.ContainsKey(k)) conflict[k] = new List<string> { map[k] }; conflict[k].Add(sh.SheetNumber); continue; }
            map[k] = sh.SheetNumber;
        }
    }

sb.AppendLine();
if (conflict.Count > 0)
{
    sb.AppendLine("  " + drawingNumberParam + " NOT WRITTEN - " + conflict.Count
        + " element(s) appear on more than one candidate sheet. Decide the rule first:");
    foreach (var k in conflict.Take(10))
    {
        var fi = doc.GetElement(new ElementId(k.Key)) as FamilyInstance;
        sb.AppendLine("     Id " + k.Key + (fi != null ? "  " + fi.Symbol.FamilyName : "") + " -> " + string.Join(" + ", k.Value));
    }
}
else
{
    int set = 0, noSheet = 0; var orphans = new List<int>();
    using (var t = new Transaction(doc, "AJ Tools - Set " + drawingNumberParam))
    {
        t.Start();
        try
        {
            foreach (var e in els)
            {
                if (!map.ContainsKey(e.Id.IntegerValue)) { noSheet++; orphans.Add(e.Id.IntegerValue); continue; }
                var p = e.LookupParameter(drawingNumberParam);
                if (p == null || p.IsReadOnly) continue;
                if (p.Set(map[e.Id.IntegerValue])) set++;
            }
            t.Commit();
        }
        catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED drawing number - rolled back. " + ex.Message); }
    }
    sb.AppendLine("  " + drawingNumberParam.PadRight(30) + "set " + set + " | NOT on any candidate sheet " + noSheet);
    // an orphan is a QUESTION, not a failure — it is usually documented on a different discipline's sheet
    foreach (var o in orphans.Take(20))
    {
        var fi = doc.GetElement(new ElementId(o)) as FamilyInstance;
        sb.AppendLine("     orphan Id " + o + (fi != null ? "  " + fi.Symbol.FamilyName + " :: " + fi.Symbol.Name : ""));
    }
    if (orphans.Count > 0)
        sb.AppendLine("     ^ check which sheet DOES show these before blanking them — see the header note on louvres.");
}

// ---- 3. read every column back OUT of the model ----
sb.AppendLine();
sb.AppendLine("READ BACK - " + els.Count + " element(s):");
var fresh = new FilteredElementCollector(doc).OfCategory(targetCategory)
    .WhereElementIsNotElementType().ToElements();
var watch = fixedVals.Select(k => k.Key).Concat(new[] { drawingNumberParam }).ToList();
foreach (var w in watch)
{
    var counts = new Dictionary<string,int>(); int blank = 0, absent = 0;
    foreach (var e in fresh)
    {
        var p = e.LookupParameter(w);
        if (p == null) { absent++; continue; }
        var v = p.AsString();
        if (string.IsNullOrWhiteSpace(v)) blank++; else { if (!counts.ContainsKey(v)) counts[v] = 0; counts[v]++; }
    }
    sb.AppendLine("  " + w.PadRight(30) + (absent == fresh.Count ? "-- NOT BOUND --"
        : string.Join(", ", counts.OrderByDescending(k => k.Value).Select(k => "\"" + k.Key + "\" x" + k.Value))
          + (blank > 0 ? " | BLANK x" + blank : " | blank 0")));
}
return sb.ToString();
