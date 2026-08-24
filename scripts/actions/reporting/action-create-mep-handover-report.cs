// ============================================================
// FRAGMENT (action) — action-create-mep-handover-report.cs
// PURPOSE: The MEP ASSET REGISTER for handover — every piece of equipment with the data the facilities
//          team is actually going to be given, and a blunt count of what is still blank. The report that
//          decides whether a model is ready to hand over, rather than whether it looks finished.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the
//          assets, e.g. filter-by-multiple-categories.cs over mechanical/electrical/plumbing equipment.
//          Read-only. The model never changes.
//
// ✱✱ THE POINT IS THE GAP, NOT THE LIST. Anyone can export a schedule. What a schedule does not tell you
//    is how much of it is empty, and a handover model is refused for exactly that. The completeness
//    percentage per parameter is the headline; the asset table underneath it is the evidence.
//
// ✱✱ MISSING AND BLANK ARE DIFFERENT AND THE DIFFERENCE IS THE FIRST THING TO FIX. A parameter that is
//    BLANK on 40 units is 40 pieces of data entry. A parameter that DOES NOT EXIST on those units is a
//    shared-parameter or family problem, and no amount of typing will fix it — the parameter has to be
//    added first (action-add-project-parameter.cs). The two are counted separately for that reason; a
//    plain "empty" count conflates them and sends the work to the wrong person.
//
// ✱✱ AN ASSET WITH NO MARK IS UNTRACEABLE, so it is called out on its own. Every other field can be
//    filled in later from a schedule; without a unique identifier there is nothing to hang the data on
//    and no way to match the model to what was installed. Duplicate Marks are flagged for the same
//    reason — two assets sharing an identifier is worse than one having none.
//
// GOTCHA: THE PARAMETER NAMES ARE THE INPUT AND THEY VARY BY EMPLOYER. `assetParameters` holds what a
//         typical handover asks for; a real employer's information requirements will name them
//         differently (COBie-style names, an asset-tag convention, a client's own set). Put the real
//         names in before running it, or every row will read "no such parameter" and say nothing.
// GOTCHA: IT READS INSTANCE FIRST, THEN THE TYPE. Manufacturer and Model normally live on the TYPE, and
//         a check that only looked at the instance would report them all missing. Where a value came
//         from the type, the report says so — because one type-level value covering 40 instances is
//         correct for Model and wrong for Serial Number.
// GOTCHA: LINKED MODELS ARE NOT INCLUDED. Hand over each model's own assets, or bring them across.
// RELATED: action-report-parameters.cs (a plain parameter table, no judgement),
//          action-find-blank-parameter.cs (one parameter, as an actionable set),
//          action-export-parameters-to-csv.cs (get it out to a spreadsheet),
//          action-add-project-parameter.cs (add a parameter that does not exist yet),
//          action-create-coordination-report.cs (the coordination view of the same model).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Check one asset's row against the element in
//   Revit before sending the percentages to anyone.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// The data the handover actually asks for. Replace with the employer's real parameter names.
var assetParameters = new List<string>
{
    "Mark",
    "Manufacturer",
    "Model",
    "Serial Number",
    "Asset Tag",
    "Installation Date",
    "Warranty Period",
    "Maintenance Interval",
};

string identifierParameter = "Mark";   // the unique identifier — blanks and duplicates here are called out
bool groupByCategory = true;
bool listCompleteAssets = false;       // false = only show assets with something missing
int maxReportedRows = 80;
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the equipment/assets).");
    return sb.ToString();
}

// Instance first, then the type — Manufacturer and Model normally live on the type, and an
// instance-only read would report every one of them missing.
Func<Element, string, (bool Exists, bool HasValue, string Value, bool FromType)> readParam = (el, name) =>
{
    var p = el.LookupParameter(name);
    if (p != null)
    {
        if (!p.HasValue) return (true, false, "", false);
        string v = p.StorageType == StorageType.String ? (p.AsString() ?? "") : (p.AsValueString() ?? "");
        return (true, !string.IsNullOrWhiteSpace(v), v.Trim(), false);
    }
    var te = Document.GetElement(el.GetTypeId());
    if (te != null)
    {
        var tp = te.LookupParameter(name);
        if (tp != null)
        {
            if (!tp.HasValue) return (true, false, "", true);
            string v = tp.StorageType == StorageType.String ? (tp.AsString() ?? "") : (tp.AsValueString() ?? "");
            return (true, !string.IsNullOrWhiteSpace(v), v.Trim(), true);
        }
    }
    return (false, false, "", false);
};

// ---- gather ----
var assets = new List<(Element El, string Cat, string TypeName, string Level, Dictionary<string, (bool Exists, bool HasValue, string Value, bool FromType)> Data, int Missing)>();
var perParamExists = new Dictionary<string, int>();
var perParamFilled = new Dictionary<string, int>();
foreach (var p in assetParameters) { perParamExists[p] = 0; perParamFilled[p] = 0; }

var idProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");

foreach (var el in elements)
{
    var data = new Dictionary<string, (bool, bool, string, bool)>();
    int missing = 0;
    foreach (var pname in assetParameters)
    {
        var r = readParam(el, pname);
        data[pname] = r;
        if (r.Exists) perParamExists[pname]++;
        if (r.HasValue) perParamFilled[pname]++; else missing++;
    }

    string typeName = "";
    var te = Document.GetElement(el.GetTypeId());
    var fi = el as FamilyInstance;
    if (fi != null && fi.Symbol != null && fi.Symbol.Family != null) typeName = $"{fi.Symbol.Family.Name} : {fi.Symbol.Name}";
    else if (te != null) typeName = te.Name;

    string levelName = "";
    var lvl = Document.GetElement(el.LevelId) as Level;
    if (lvl != null) levelName = lvl.Name;
    else
    {
        var lp = el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
              ?? el.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
              ?? el.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
        if (lp != null && lp.HasValue) { var l2 = Document.GetElement(lp.AsElementId()) as Level; if (l2 != null) levelName = l2.Name; }
    }

    assets.Add((el, el.Category != null ? el.Category.Name : "-", typeName, levelName, data, missing));
}

// ---- headline ----
int total = assets.Count;
int complete = assets.Count(a => a.Missing == 0);
double overallPct = total > 0 ? complete * 100.0 / total : 0;

sb.AppendLine("# MEP HANDOVER / ASSET REGISTER");
sb.AppendLine($"Model: {(string.IsNullOrEmpty(Document.Title) ? "(unsaved)" : Document.Title)}");
sb.AppendLine();
sb.AppendLine($"**Assets: {total}   fully populated: {complete} ({overallPct:F0}%)   with something missing: {total - complete}**");
sb.AppendLine();
sb.AppendLine("Linked models are NOT included — this covers the host model's own assets only.");
sb.AppendLine();

// ---- per-parameter completeness ----
sb.AppendLine("## Completeness by parameter");
sb.AppendLine();
sb.AppendLine("| Parameter | Exists on | Filled in | Filled % | Blank | Parameter absent |");
sb.AppendLine("|---|---|---|---|---|---|");
foreach (var pname in assetParameters)
{
    int exists = perParamExists[pname];
    int filled = perParamFilled[pname];
    int blank = exists - filled;
    int absent = total - exists;
    double pct = total > 0 ? filled * 100.0 / total : 0;
    sb.AppendLine($"| {pname} | {exists} | {filled} | {pct:F0}% | {blank} | {absent} |");
}
sb.AppendLine();
sb.AppendLine("**Blank** is data entry. **Parameter absent** is a family or shared-parameter job — typing will not fix it; the parameter has to be added first (`action-add-project-parameter.cs`).");
sb.AppendLine();

var absentEverywhere = assetParameters.Where(p => perParamExists[p] == 0).ToList();
if (absentEverywhere.Count > 0)
{
    sb.AppendLine($"**{absentEverywhere.Count} parameter(s) do not exist anywhere in this set** — {string.Join(", ", absentEverywhere)}. Until these are added, no amount of data entry produces a handover.");
    sb.AppendLine();
}

// ---- identifier check ----
sb.AppendLine($"## Identifier check ({identifierParameter})");
sb.AppendLine();
var noId = new List<Element>();
var byId = new Dictionary<string, List<Element>>();
foreach (var a in assets)
{
    var r = a.Data.ContainsKey(identifierParameter) ? a.Data[identifierParameter] : readParam(a.El, identifierParameter);
    if (!r.HasValue) { noId.Add(a.El); continue; }
    if (!byId.ContainsKey(r.Value)) byId[r.Value] = new List<Element>();
    byId[r.Value].Add(a.El);
}
var duplicates = byId.Where(kv => kv.Value.Count > 1).ToList();

sb.AppendLine($"- Assets with no {identifierParameter}: **{noId.Count}** — untraceable, nothing can be hung on them");
sb.AppendLine($"- Duplicate {identifierParameter} values: **{duplicates.Count}** covering {duplicates.Sum(d => d.Value.Count)} asset(s)");
if (noId.Count > 0)
    sb.AppendLine($"  - No {identifierParameter}: {string.Join(", ", noId.Take(20).Select(e => e.Id.ToString()))}{(noId.Count > 20 ? $" ... and {noId.Count - 20} more" : "")}");
foreach (var d in duplicates.Take(10))
    sb.AppendLine($"  - '{d.Key}' used by {d.Value.Count}: {string.Join(", ", d.Value.Take(6).Select(e => e.Id.ToString()))}");
sb.AppendLine();

// ---- breakdown ----
if (groupByCategory)
{
    sb.AppendLine("## By category");
    sb.AppendLine();
    sb.AppendLine("| Category | Assets | Complete | Complete % |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var g in assets.GroupBy(a => a.Cat).OrderByDescending(g => g.Count()))
    {
        int c = g.Count(a => a.Missing == 0);
        sb.AppendLine($"| {g.Key} | {g.Count()} | {c} | {(g.Count() > 0 ? c * 100.0 / g.Count() : 0):F0}% |");
    }
    sb.AppendLine();
}

// ---- the register ----
sb.AppendLine("## Asset register");
sb.AppendLine();
var show = listCompleteAssets ? assets : assets.Where(a => a.Missing > 0).ToList();
if (show.Count == 0)
{
    sb.AppendLine("Every asset is fully populated. Ready to hand over on the data side.");
    return sb.ToString();
}

sb.AppendLine(listCompleteAssets ? "Every asset:" : $"Assets with something missing ({show.Count} of {total}). Set listCompleteAssets = true for the full register.");
sb.AppendLine();

var header = new List<string> { "Id", "Category", "Type", "Level" };
header.AddRange(assetParameters);
header.Add("Missing");
sb.AppendLine("| " + string.Join(" | ", header) + " |");
sb.AppendLine("|" + string.Join("|", header.Select(h => "---")) + "|");

foreach (var a in show.OrderByDescending(a => a.Missing).ThenBy(a => a.Cat).Take(maxReportedRows))
{
    var cells = new List<string> { a.El.Id.ToString(), a.Cat, a.TypeName, a.Level };
    foreach (var pname in assetParameters)
    {
        var r = a.Data[pname];
        if (!r.Exists) cells.Add("(no such parameter)");
        else if (!r.HasValue) cells.Add("(blank)");
        else cells.Add(r.Value + (r.FromType ? " [T]" : ""));
    }
    cells.Add(a.Missing.ToString());
    sb.AppendLine("| " + string.Join(" | ", cells) + " |");
}
if (show.Count > maxReportedRows)
    sb.AppendLine($"\n... and {show.Count - maxReportedRows} more (raise maxReportedRows, or export with action-export-parameters-to-csv.cs).");

sb.AppendLine();
sb.AppendLine("`[T]` means the value came from the TYPE, not the instance — correct for Manufacturer and Model, wrong for anything that must be unique per unit such as a Serial Number.");

return sb.ToString();
