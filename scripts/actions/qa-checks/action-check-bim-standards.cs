// ============================================================
// FRAGMENT (action) — action-check-bim-standards.cs
// PURPOSE: Check the model against the project's own naming and data conventions — view names, sheet
//          numbers, level names, workset names, system names, and whether the parameters the project
//          requires are actually filled in. The sweep before a model is submitted, and the one that finds
//          the twelve views somebody called "Copy of Copy of Level 1".
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — a standards check is a
//          whole-document question. Read-only. The model never changes.
//
// ✱✱ THE RULES ARE PATTERNS YOU SUPPLY, NOT A STANDARD THIS FRAGMENT KNOWS. Naming conventions are
//    project-specific and change between employers; a fragment that hard-coded one would be wrong on
//    every job but one. Each rule is a plain-text pattern with `*` as a wildcard, which is the form
//    somebody who is not a programmer can actually write and check.
//
// ✱✱ IT REPORTS WHAT IT COULD NOT CHECK, and that is as important as what failed. A rule set that names
//    no pattern for sheets does not mean the sheets are fine — it means nobody looked. Every section
//    says NOT CHECKED with a reason rather than printing a reassuring zero.
//
// ✱✱ THE PARAMETER CHECK SEPARATES "BLANK" FROM "DOES NOT EXIST", for the same reason the handover
//    report does: one is data entry and the other is a project setup job, and they go to different
//    people. A single "empty" number sends the work to the wrong place.
//
// GOTCHA: WILDCARDS ONLY. The pattern language is deliberately just `*` (any run of characters) and
//         literal text, case-insensitive — no regular expressions. That is what makes it writable by the
//         person who owns the standard rather than by whoever is holding the keyboard.
// GOTCHA: VIEW TEMPLATES AND SYSTEM-OWNED VIEWS are excluded from the view naming check by default. A
//         project's templates rarely follow the sheet naming convention and flagging them buries the
//         real findings.
// GOTCHA: THIS CHECKS CONVENTIONS, NOT CORRECTNESS. A view named perfectly can still show the wrong
//         thing. This is the cheap half of a model audit; recipes/model-health-audit.cs is the health
//         half, and neither replaces looking at the drawings.
// RELATED: recipes/model-health-audit.cs (file, worksharing, warnings, imports — the other half),
//          action-check-family-standards.cs (the families themselves),
//          action-find-blank-parameter.cs (one parameter, as an actionable set),
//          action-create-mep-handover-report.cs (the asset-data view),
//          action-rename-element.cs and action-find-replace-element-name.cs (fixing what this finds).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Check one reported failure by eye before
//   sending the counts to anyone.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Patterns use * as a wildcard and are case-insensitive. Empty string = do not check that thing.
string viewNamePattern = "";          // e.g. "MEP-*" — leave "" to skip
string sheetNumberPattern = "";       // e.g. "M-*"
string levelNamePattern = "";         // e.g. "L*"
string worksetNamePattern = "";       // e.g. "M_*"
string systemNamePattern = "";        // e.g. "*-*" — MEP system names

// Parameters that must be filled in, per category.
var requiredParameters = new List<(BuiltInCategory Cat, string ParamName)>
{
    (BuiltInCategory.OST_MechanicalEquipment, "Mark"),
    (BuiltInCategory.OST_DuctCurves, "System Name"),
    (BuiltInCategory.OST_PipeCurves, "System Name"),
};

// Names that are almost always a mistake wherever they appear.
var suspectNameFragments = new List<string> { "copy of", "copy 1", "unnamed", "new construction", "test", "temp", "asdf", "xxx" };

bool skipViewTemplates = true;
int maxReportedRows = 40;
// ---- END INPUTS ----

// ---- wildcard matcher: * only, case-insensitive. No regex on purpose. ----
Func<string, string, bool> matches = null;
matches = (text, pattern) =>
{
    text = text ?? "";
    if (string.IsNullOrEmpty(pattern)) return true;

    var parts = pattern.Split('*');
    int pos = 0;
    for (int i = 0; i < parts.Length; i++)
    {
        var part = parts[i];
        if (part.Length == 0) continue;

        if (i == 0)
        {
            if (!text.StartsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
            pos = part.Length;
            continue;
        }
        if (i == parts.Length - 1 && !pattern.EndsWith("*"))
        {
            if (!text.EndsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
            return text.Length - part.Length >= pos;
        }
        int found = text.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
        if (found < 0) return false;
        pos = found + part.Length;
    }
    return true;
};

sb.AppendLine("# BIM STANDARDS CHECK");
sb.AppendLine($"Model: {(string.IsNullOrEmpty(Document.Title) ? "(unsaved)" : Document.Title)}");
sb.AppendLine();

int totalFailures = 0;
int sectionsChecked = 0, sectionsSkipped = 0;

Action<string, string, List<(string Name, ElementId Id)>, int> reportSection = (title, pattern, failures, examined) =>
{
    sb.AppendLine($"## {title}");
    sb.AppendLine();
    if (string.IsNullOrEmpty(pattern))
    {
        sb.AppendLine("**NOT CHECKED** — no pattern given. This is not a pass; nobody looked.");
        sb.AppendLine();
        sectionsSkipped++;
        return;
    }
    sectionsChecked++;
    sb.AppendLine($"Pattern: `{pattern}`   examined: {examined}   **failures: {failures.Count}**");
    if (failures.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("| Name | Id |");
        sb.AppendLine("|---|---|");
        foreach (var f in failures.Take(maxReportedRows)) sb.AppendLine($"| {f.Name} | {f.Id} |");
        if (failures.Count > maxReportedRows) sb.AppendLine($"\n... and {failures.Count - maxReportedRows} more");
    }
    sb.AppendLine();
    totalFailures += failures.Count;
};

// ---- 1. VIEWS ----
var views = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => !(skipViewTemplates && v.IsTemplate)).ToList();
var viewFails = views.Where(v => !matches(v.Name, viewNamePattern))
    .Select(v => (v.Name, v.Id)).ToList();
reportSection("View names", viewNamePattern, viewFails, views.Count);

// ---- 2. SHEETS ----
var sheets = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
var sheetFails = sheets.Where(s => !matches(s.SheetNumber, sheetNumberPattern))
    .Select(s => ($"{s.SheetNumber} — {s.Name}", s.Id)).ToList();
reportSection("Sheet numbers", sheetNumberPattern, sheetFails, sheets.Count);

// ---- 3. LEVELS ----
var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().ToList();
var levelFails = levels.Where(l => !matches(l.Name, levelNamePattern))
    .Select(l => (l.Name, l.Id)).ToList();
reportSection("Level names", levelNamePattern, levelFails, levels.Count);

// ---- 4. WORKSETS ----
sb.AppendLine("## Workset names");
sb.AppendLine();
if (!Document.IsWorkshared)
{
    sb.AppendLine("**NOT CHECKED** — this model is not workshared, so it has no user worksets.");
    sb.AppendLine();
}
else if (string.IsNullOrEmpty(worksetNamePattern))
{
    sb.AppendLine("**NOT CHECKED** — no pattern given.");
    sb.AppendLine();
    sectionsSkipped++;
}
else
{
    sectionsChecked++;
    var worksets = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).ToWorksets().ToList();
    var wsFails = worksets.Where(w => !matches(w.Name, worksetNamePattern)).ToList();
    sb.AppendLine($"Pattern: `{worksetNamePattern}`   examined: {worksets.Count}   **failures: {wsFails.Count}**");
    if (wsFails.Count > 0)
    {
        sb.AppendLine();
        foreach (var w in wsFails.Take(maxReportedRows)) sb.AppendLine($"  - {w.Name}");
    }
    sb.AppendLine();
    totalFailures += wsFails.Count;
}

// ---- 5. MEP SYSTEM NAMES ----
sb.AppendLine("## MEP system names");
sb.AppendLine();
if (string.IsNullOrEmpty(systemNamePattern))
{
    sb.AppendLine("**NOT CHECKED** — no pattern given.");
    sb.AppendLine();
    sectionsSkipped++;
}
else
{
    sectionsChecked++;
    var systems = new List<(string Name, ElementId Id)>();
    foreach (var t in new[] { typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystem), typeof(Autodesk.Revit.DB.Plumbing.PipingSystem) })
    {
        try
        {
            foreach (var e in new FilteredElementCollector(Document).OfClass(t).WhereElementIsNotElementType())
                systems.Add((e.Name ?? "", e.Id));
        }
        catch { }
    }
    var sysFails = systems.Where(s => !matches(s.Name, systemNamePattern)).ToList();
    sb.AppendLine($"Pattern: `{systemNamePattern}`   examined: {systems.Count}   **failures: {sysFails.Count}**");
    if (systems.Count == 0) sb.AppendLine("(No MEP systems in this model — the systems may be in a link, in which case this has told you nothing.)");
    if (sysFails.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("| System | Id |");
        sb.AppendLine("|---|---|");
        foreach (var s in sysFails.Take(maxReportedRows)) sb.AppendLine($"| {s.Name} | {s.Id} |");
        if (sysFails.Count > maxReportedRows) sb.AppendLine($"\n... and {sysFails.Count - maxReportedRows} more");
    }
    sb.AppendLine();
    totalFailures += sysFails.Count;
}

// ---- 6. SUSPECT NAMES ANYWHERE ----
sb.AppendLine("## Suspect names");
sb.AppendLine();
var suspects = new List<(string What, string Name, ElementId Id)>();
foreach (var v in views)
{
    var low = (v.Name ?? "").ToLower();
    foreach (var frag in suspectNameFragments)
        if (low.Contains(frag)) { suspects.Add(("View", v.Name, v.Id)); break; }
}
foreach (var s in sheets)
{
    var low = (s.Name ?? "").ToLower();
    foreach (var frag in suspectNameFragments)
        if (low.Contains(frag)) { suspects.Add(("Sheet", $"{s.SheetNumber} — {s.Name}", s.Id)); break; }
}
sb.AppendLine($"Looking for: {string.Join(", ", suspectNameFragments.Select(f => $"\"{f}\""))}");
sb.AppendLine($"**Found: {suspects.Count}**");
if (suspects.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("| What | Name | Id |");
    sb.AppendLine("|---|---|---|");
    foreach (var s in suspects.Take(maxReportedRows)) sb.AppendLine($"| {s.What} | {s.Name} | {s.Id} |");
    if (suspects.Count > maxReportedRows) sb.AppendLine($"\n... and {suspects.Count - maxReportedRows} more");
}
sb.AppendLine();
totalFailures += suspects.Count;
sectionsChecked++;

// ---- 7. REQUIRED PARAMETERS ----
sb.AppendLine("## Required parameters");
sb.AppendLine();
if (requiredParameters.Count == 0)
{
    sb.AppendLine("**NOT CHECKED** — no required parameters listed.");
    sectionsSkipped++;
}
else
{
    sectionsChecked++;
    sb.AppendLine("| Category | Parameter | Elements | Filled | Blank | Parameter absent |");
    sb.AppendLine("|---|---|---|---|---|---|");
    foreach (var req in requiredParameters)
    {
        List<Element> els;
        try { els = new FilteredElementCollector(Document).OfCategory(req.Cat).WhereElementIsNotElementType().ToList(); }
        catch { els = new List<Element>(); }

        int filled = 0, blank = 0, absent = 0;
        foreach (var e in els)
        {
            var p = e.LookupParameter(req.ParamName);
            if (p == null)
            {
                var te = Document.GetElement(e.GetTypeId());
                if (te != null) p = te.LookupParameter(req.ParamName);
            }
            if (p == null) { absent++; continue; }
            if (!p.HasValue) { blank++; continue; }
            string v = p.StorageType == StorageType.String ? (p.AsString() ?? "") : (p.AsValueString() ?? "");
            if (string.IsNullOrWhiteSpace(v)) blank++; else filled++;
        }

        string catName = els.Count > 0 && els[0].Category != null ? els[0].Category.Name : req.Cat.ToString();
        sb.AppendLine($"| {catName} | {req.ParamName} | {els.Count} | {filled} | {blank} | {absent} |");
        totalFailures += blank + absent;
    }
    sb.AppendLine();
    sb.AppendLine("**Blank** is data entry. **Parameter absent** is a project-setup job — `action-add-project-parameter.cs`.");
}
sb.AppendLine();

// ---- summary ----
sb.AppendLine("## Summary");
sb.AppendLine();
sb.AppendLine($"- Sections checked: **{sectionsChecked}**");
sb.AppendLine($"- Sections NOT checked (no pattern given): **{sectionsSkipped}** — these are unknown, not passes");
sb.AppendLine($"- Total items failing a stated rule: **{totalFailures}**");
sb.AppendLine();
if (sectionsSkipped > 0)
    sb.AppendLine("Fill in the patterns for the skipped sections before treating this as a standards audit.");
if (totalFailures == 0 && sectionsSkipped == 0)
    sb.AppendLine("Everything checked matches the stated conventions.");
sb.AppendLine();
sb.AppendLine("This covers conventions and data. For file health, warnings, imports and worksharing, run `recipes/model-health-audit.cs`; for the families themselves, `action-check-family-standards.cs`.");

return sb.ToString();
