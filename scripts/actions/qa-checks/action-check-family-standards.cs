// ============================================================
// FRAGMENT (action) — action-check-family-standards.cs
// PURPOSE: Audit the MEP FAMILIES a project is using — do they carry connectors, are they in the right
//          category, do they have the parameters the project needs, are they named to the convention,
//          and are any of them so heavy they are slowing the model down. The library check before a
//          family set goes onto a job, and the explanation for "why does nothing schedule properly".
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it audits the FAMILY TYPES in the
//          document, which is a different set from the instances a filter produces. Read-only.
//
// ✱✱ IT AUDITS TYPES, NOT INSTANCES, AND THAT IS THE POINT. One badly-built family type placed 200 times
//    is ONE thing to fix, and an instance-based report shows it as 200 findings and buries everything
//    else. Every row here is a type; the instance count beside it is what makes it urgent.
//
// ✱✱ NO CONNECTORS IS THE HEADLINE FINDING FOR MEP. A duct terminal, a piece of equipment or a valve with
//    no connector cannot join a system, cannot carry flow, and will never appear in a system browser —
//    but it looks perfectly normal in a plan and it clashes like anything else. It is the single most
//    common reason a model looks finished and behaves as if nothing is connected.
//
// ✱✱ CONNECTORS ARE CHECKED THROUGH A PLACED INSTANCE, because a FamilySymbol does not expose them.
//    There is no `symbol.Connectors`. So one instance per type is sampled and its connectors read; a type
//    with NO placed instance therefore cannot be checked this way and is reported as UNPLACED — NOT
//    CHECKED rather than passed or failed. That distinction is the difference between an audit and a
//    guess.
//
// ✱✱ CATEGORY MISMATCH IS TESTED AGAINST THE NAME, in both directions. A family called "VCD" sitting in
//    Generic Models, or one called "Louvre" filed as an Air Terminal, is exactly the defect
//    filter-by-wrong-category.cs was built for; the difference is that this one reports it per TYPE
//    across the whole library rather than per instance.
//
// GOTCHA: SYSTEM FAMILIES HAVE NO FAMILY FILE and cannot be audited this way — duct types, pipe types,
//         wall types. They are counted and excluded, not silently skipped.
// GOTCHA: THE NAMING PATTERN AND THE REQUIRED PARAMETERS ARE YOUR INPUTS. There is no universal family
//         naming standard; leave the pattern empty and that section reports NOT CHECKED rather than a
//         reassuring zero.
// GOTCHA: THE WEIGHT CHECK IS A ROUGH COUNT, not the proper measurement.
//         action-report-geometry-complexity.cs measures real triangle counts at each detail level and is
//         the tool to use when chasing model performance; this is a flag to send you there.
// RELATED: action-report-geometry-complexity.cs (real weight, per detail level),
//          filter-by-wrong-category.cs (per-instance category mismatches, as an actionable set),
//          action-check-bim-standards.cs (the project's naming and data conventions),
//          action-purge-unused-families.cs (types with zero instances),
//          action-report-connectors.cs (connectors on placed elements).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Open one flagged family and confirm the
//   finding before sending a list to whoever maintains the library.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Which categories to audit. These are the ones where a missing connector actually matters.
var categoriesToAudit = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctTerminal,
    BuiltInCategory.OST_MechanicalEquipment,
    BuiltInCategory.OST_DuctAccessory,
    BuiltInCategory.OST_PipeAccessory,
    BuiltInCategory.OST_PlumbingFixtures,
    BuiltInCategory.OST_Sprinklers,
    BuiltInCategory.OST_ElectricalEquipment,
    BuiltInCategory.OST_ElectricalFixtures,
    BuiltInCategory.OST_LightingFixtures,
};

string familyNamePattern = "";        // e.g. "M_*" — wildcard *, case-insensitive. "" = NOT CHECKED
var requiredTypeParameters = new List<string> { };   // e.g. "Manufacturer", "Model"

// Family-name keyword -> the category it really belongs in. Both directions are tested.
var expectedCategoryByKeyword = new List<(string Keyword, BuiltInCategory Cat)>
{
    ("diffuser", BuiltInCategory.OST_DuctTerminal),
    ("grille",   BuiltInCategory.OST_DuctTerminal),
    ("louvre",   BuiltInCategory.OST_DuctTerminal),
    ("vcd",      BuiltInCategory.OST_DuctAccessory),
    ("damper",   BuiltInCategory.OST_DuctAccessory),
    ("valve",    BuiltInCategory.OST_PipeAccessory),
    ("fcu",      BuiltInCategory.OST_MechanicalEquipment),
    ("ahu",      BuiltInCategory.OST_MechanicalEquipment),
    ("sprinkler",BuiltInCategory.OST_Sprinklers),
};

int heavyFaceCount = 400;   // a type whose sampled instance carries more faces than this is flagged heavy
int maxReportedRows = 50;
// ---- END INPUTS ----

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- wildcard matcher: * only, case-insensitive ----
Func<string, string, bool> matches = (text, pattern) =>
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
            pos = part.Length; continue;
        }
        if (i == parts.Length - 1 && !pattern.EndsWith("*"))
            return text.EndsWith(part, StringComparison.OrdinalIgnoreCase) && text.Length - part.Length >= pos;
        int found = text.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
        if (found < 0) return false;
        pos = found + part.Length;
    }
    return true;
};

sb.AppendLine("# MEP FAMILY STANDARDS AUDIT");
sb.AppendLine($"Model: {(string.IsNullOrEmpty(Document.Title) ? "(unsaved)" : Document.Title)}");
sb.AppendLine();

// ---- gather types, and one placed instance per type ----
var auditCatIds = new HashSet<long>();
foreach (var c in categoriesToAudit) auditCatIds.Add((long)c);

var symbols = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
    .Where(s => s.Category != null && auditCatIds.Contains(IdValue(s.Category.Id)))
    .ToList();

// One instance per type, and the instance counts, in a single sweep.
var instanceByType = new Dictionary<long, FamilyInstance>();
var countByType = new Dictionary<long, int>();
foreach (var cat in categoriesToAudit)
{
    try
    {
        foreach (FamilyInstance fi in new FilteredElementCollector(Document).OfCategory(cat)
                     .WhereElementIsNotElementType().OfClass(typeof(FamilyInstance)))
        {
            long tid = IdValue(fi.GetTypeId());
            countByType[tid] = countByType.ContainsKey(tid) ? countByType[tid] + 1 : 1;
            if (!instanceByType.ContainsKey(tid)) instanceByType[tid] = fi;
        }
    }
    catch { }
}

int systemFamilyTypes = new FilteredElementCollector(Document).OfClass(typeof(ElementType))
    .Cast<ElementType>().Count(t => !(t is FamilySymbol));

sb.AppendLine($"Loadable family TYPES in the audited categories: **{symbols.Count}**");
sb.AppendLine($"Of those, placed at least once: **{symbols.Count(s => instanceByType.ContainsKey(IdValue(s.Id)))}**");
sb.AppendLine($"System-family types in the document (no family file — cannot be audited this way): {systemFamilyTypes}");
sb.AppendLine();

if (symbols.Count == 0)
{
    sb.AppendLine("**NOTHING TO AUDIT** — no loadable families in the audited categories. Widen categoriesToAudit, or the MEP families live in a link.");
    return sb.ToString();
}

// ---- audit each type ----
var noConnectors = new List<(FamilySymbol S, int Count)>();
var unplaced = new List<FamilySymbol>();
var wrongCategory = new List<(FamilySymbol S, string Should, int Count)>();
var badName = new List<(FamilySymbol S, int Count)>();
var missingParams = new List<(FamilySymbol S, string Param, int Count)>();
var heavy = new List<(FamilySymbol S, int Faces, int Count)>();

var geoOpts = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false, IncludeNonVisibleObjects = false };

foreach (var s in symbols)
{
    long tid = IdValue(s.Id);
    int count = countByType.ContainsKey(tid) ? countByType[tid] : 0;
    string famName = s.Family != null ? s.Family.Name : "";
    string full = $"{famName} : {s.Name}";

    // ---- naming ----
    if (!string.IsNullOrEmpty(familyNamePattern) && !matches(famName, familyNamePattern))
        badName.Add((s, count));

    // ---- required type parameters ----
    foreach (var pname in requiredTypeParameters)
    {
        var p = s.LookupParameter(pname);
        bool ok = p != null && p.HasValue;
        if (ok && p.StorageType == StorageType.String) ok = !string.IsNullOrWhiteSpace(p.AsString());
        if (!ok) missingParams.Add((s, pname, count));
    }

    // ---- category vs name ----
    string hay = (famName + " " + s.Name).ToLower();
    foreach (var rule in expectedCategoryByKeyword)
    {
        if (hay.IndexOf(rule.Keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
        if (s.Category != null && IdValue(s.Category.Id) != (long)rule.Cat)
        {
            wrongCategory.Add((s, rule.Cat.ToString(), count));
        }
        break;
    }

    // ---- connectors and weight: need a placed instance ----
    if (!instanceByType.ContainsKey(tid)) { unplaced.Add(s); continue; }

    var inst = instanceByType[tid];
    int connectorCount = 0;
    try
    {
        if (inst.MEPModel != null && inst.MEPModel.ConnectorManager != null)
            foreach (Connector c in inst.MEPModel.ConnectorManager.Connectors) connectorCount++;
    }
    catch { }
    if (connectorCount == 0) noConnectors.Add((s, count));

    // Rough weight: face count on the placed instance's solids.
    int faces = 0;
    try
    {
        var ge = inst.get_Geometry(geoOpts);
        if (ge != null)
        {
            Action<GeometryElement> walk = null;
            walk = g =>
            {
                foreach (GeometryObject go in g)
                {
                    var sol = go as Solid;
                    if (sol != null) { faces += sol.Faces.Size; continue; }
                    var gi = go as GeometryInstance;
                    if (gi != null) { var inner = gi.GetInstanceGeometry(); if (inner != null) walk(inner); }
                }
            };
            walk(ge);
        }
    }
    catch { }
    if (faces > heavyFaceCount) heavy.Add((s, faces, count));
}

// ---- report ----
sb.AppendLine("## Findings");
sb.AppendLine();
sb.AppendLine("| Finding | Types | Instances affected |");
sb.AppendLine("|---|---|---|");
sb.AppendLine($"| NO CONNECTORS (cannot join a system) | {noConnectors.Count} | {noConnectors.Sum(x => x.Count)} |");
sb.AppendLine($"| Wrong category for its name | {wrongCategory.Count} | {wrongCategory.Sum(x => x.Count)} |");
sb.AppendLine($"| Family name off the convention | {(string.IsNullOrEmpty(familyNamePattern) ? "NOT CHECKED" : badName.Count.ToString())} | {(string.IsNullOrEmpty(familyNamePattern) ? "-" : badName.Sum(x => x.Count).ToString())} |");
sb.AppendLine($"| Missing a required type parameter | {(requiredTypeParameters.Count == 0 ? "NOT CHECKED" : missingParams.Count.ToString())} | {(requiredTypeParameters.Count == 0 ? "-" : missingParams.Sum(x => x.Count).ToString())} |");
sb.AppendLine($"| Heavy geometry (over {heavyFaceCount} faces) | {heavy.Count} | {heavy.Sum(x => x.Count)} |");
sb.AppendLine($"| UNPLACED — could not be checked for connectors | {unplaced.Count} | 0 |");
sb.AppendLine();

if (noConnectors.Count > 0)
{
    sb.AppendLine("### No connectors");
    sb.AppendLine();
    sb.AppendLine("These cannot join a system, carry flow, or appear in the system browser. They still look right in plan.");
    sb.AppendLine();
    sb.AppendLine("| Family : Type | Category | Instances |");
    sb.AppendLine("|---|---|---|");
    foreach (var n in noConnectors.OrderByDescending(n => n.Count).Take(maxReportedRows))
        sb.AppendLine($"| {(n.S.Family != null ? n.S.Family.Name : "")} : {n.S.Name} | {n.S.Category?.Name} | {n.Count} |");
    if (noConnectors.Count > maxReportedRows) sb.AppendLine($"\n... and {noConnectors.Count - maxReportedRows} more");
    sb.AppendLine();
}

if (wrongCategory.Count > 0)
{
    sb.AppendLine("### Wrong category for its name");
    sb.AppendLine();
    sb.AppendLine("| Family : Type | Filed as | Name suggests | Instances |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var w in wrongCategory.OrderByDescending(w => w.Count).Take(maxReportedRows))
        sb.AppendLine($"| {(w.S.Family != null ? w.S.Family.Name : "")} : {w.S.Name} | {w.S.Category?.Name} | {w.Should} | {w.Count} |");
    sb.AppendLine();
    sb.AppendLine("`filter-by-wrong-category.cs` turns this into an actionable per-instance set.");
    sb.AppendLine();
}

if (badName.Count > 0)
{
    sb.AppendLine($"### Family names off the convention (`{familyNamePattern}`)");
    sb.AppendLine();
    foreach (var b in badName.OrderByDescending(b => b.Count).Take(maxReportedRows))
        sb.AppendLine($"  - {(b.S.Family != null ? b.S.Family.Name : "")} : {b.S.Name}  ({b.Count} instance(s))");
    if (badName.Count > maxReportedRows) sb.AppendLine($"  ... and {badName.Count - maxReportedRows} more");
    sb.AppendLine();
}

if (missingParams.Count > 0)
{
    sb.AppendLine("### Missing required type parameters");
    sb.AppendLine();
    foreach (var g in missingParams.GroupBy(m => m.Param))
    {
        sb.AppendLine($"**{g.Key}** — missing on {g.Count()} type(s):");
        foreach (var m in g.Take(15))
            sb.AppendLine($"  - {(m.S.Family != null ? m.S.Family.Name : "")} : {m.S.Name} ({m.Count} instance(s))");
        if (g.Count() > 15) sb.AppendLine($"  ... and {g.Count() - 15} more");
    }
    sb.AppendLine();
}

if (heavy.Count > 0)
{
    sb.AppendLine($"### Heavy geometry (over {heavyFaceCount} faces on the sampled instance)");
    sb.AppendLine();
    sb.AppendLine("| Family : Type | Faces | Instances | Faces x instances |");
    sb.AppendLine("|---|---|---|---|");
    foreach (var h in heavy.OrderByDescending(h => h.Faces * Math.Max(h.Count, 1)).Take(maxReportedRows))
        sb.AppendLine($"| {(h.S.Family != null ? h.S.Family.Name : "")} : {h.S.Name} | {h.Faces} | {h.Count} | {h.Faces * Math.Max(h.Count, 1)} |");
    sb.AppendLine();
    sb.AppendLine("This is a rough face count. `action-report-geometry-complexity.cs` measures real triangle counts per detail level and is the tool for chasing performance properly.");
    sb.AppendLine();
}

if (unplaced.Count > 0)
{
    sb.AppendLine($"### Unplaced types — NOT CHECKED for connectors ({unplaced.Count})");
    sb.AppendLine();
    sb.AppendLine("A FamilySymbol does not expose its connectors, so a type with no placed instance cannot be checked this way. These are unknown, not passes.");
    sb.AppendLine();
    foreach (var u in unplaced.Take(25))
        sb.AppendLine($"  - {(u.Family != null ? u.Family.Name : "")} : {u.Name}  ({u.Category?.Name})");
    if (unplaced.Count > 25) sb.AppendLine($"  ... and {unplaced.Count - 25} more");
    sb.AppendLine();
    sb.AppendLine("`action-purge-unused-families.cs` lists types with zero instances if the intent is to tidy the library rather than audit it.");
}

return sb.ToString();
