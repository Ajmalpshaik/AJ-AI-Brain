// ============================================================
// SCRIPT (context) — context-session-start.cs
// PURPOSE: Report the session's starting state — which Revit version, which API generation, which
//          document is open, what units the project displays, how big the model is, what is linked,
//          what is broken. THE opening call: one read-only bridge round-trip, zero input, safe to
//          call anytime, run before touching the model.
//
// ✓ LIVE-VERIFIED 2026-08-22 on `school.rvt` in Revit 2020.2.9 via the `session_start` native tool.
//   All eleven sections returned, none hit its guard: version + build, the API-generation line (correctly
//   read 32-bit ElementId / DisplayUnitType / single Dimension class for 2020), document title and path,
//   worksharing = no, project fields, the display-unit probe (Level 1 -> 0 mm), 3384 elements / 2 levels /
//   6 grids / 56 views / 10 sheets, links none, phases Existing -> New Construction, 0 warnings, and the
//   active view. Element total cross-checked against a direct collector count in the same session.
//
//          Ajmal's rule, 2026-08-20: "everytime while pinging or connection to revit check the all
//          things like what is the version of revit what is the model like that all kind of thing that
//          need to check in the beginning". This replaces guessing, and it replaces firing ten separate
//          context-*.cs fragments to assemble the same picture.
//
// WHY THE API-GENERATION LINE MATTERS: the fragment library was migrated on 2026-08-20 to run on Revit
//          2020 through 2027 from one source (knowledge/revit-version-compatibility.md). That migration
//          assumed nothing about which Revit is running — so this probe is how a session CONFIRMS which
//          API surface it actually got, instead of inferring it from the version number.
//
// EVERY SECTION IS INDEPENDENTLY GUARDED. A locked workset, a missing project parameter or a corrupt
//          link must not take down the whole opening report — a partial picture beats an exception, and
//          each line says plainly when it could not read something.
//
// VERSION-PROOF BY CONSTRUCTION: names no unit API (a level's own AsValueString reports the display
//          unit, which is what you actually want to know) and never calls ElementId.IntegerValue.
// ============================================================

var sb = new System.Text.StringBuilder();
Func<string, Action, string> safe = (label, body) =>
{ try { body(); return null; } catch (Exception ex) { return $"  {label}: could not read — {ex.Message}"; } };
var problems = new List<string>();
Action<string, Action> section = (label, body) =>
{ var p = safe(label, body); if (p != null) problems.Add(p); };

// ---- 1. Revit itself, and WHICH API GENERATION -----------------------------------------------------
section("Revit", () =>
{
    sb.AppendLine($"REVIT      {Application.VersionNumber} — {Application.VersionName}  (build {Application.VersionBuild})");
    var asm = typeof(ElementId).Assembly;
    bool id64  = typeof(ElementId).GetProperty("Value") != null;          // Revit 2024+
    bool forge = asm.GetType("Autodesk.Revit.DB.UnitTypeId") != null;     // Revit 2021+
    bool dims  = asm.GetType("Autodesk.Revit.DB.LinearDimension") != null;// Revit 2025+
    sb.AppendLine($"API        ElementId {(id64 ? "64-bit (.Value, 2024+)" : "32-bit (.IntegerValue, 2020-2023)")}"
                + $" | units {(forge ? "ForgeTypeId (2021+)" : "DisplayUnitType (2020)")}"
                + $" | dimensions {(dims ? "split classes (2025+)" : "single Dimension class")}");
});

// ---- 2. The document -------------------------------------------------------------------------------
section("Document", () =>
{
    sb.AppendLine($"MODEL      '{Document.Title}'  ({(Document.IsFamilyDocument ? "FAMILY document" : "project document")})");
    sb.AppendLine($"PATH       {(string.IsNullOrEmpty(Document.PathName) ? "(never saved)" : Document.PathName)}");
    if (Document.IsWorkshared)
    {
        string central = "(unknown)";
        try { central = ModelPathUtils.ConvertModelPathToUserVisiblePath(Document.GetWorksharingCentralModelPath()); } catch { }
        sb.AppendLine($"WORKSHARED yes — central: {central}");
    }
    else sb.AppendLine("WORKSHARED no");
    var others = new List<string>();
    foreach (Document d in Application.Documents) if (!d.IsLinked && d.Title != Document.Title) others.Add(d.Title);
    if (others.Count > 0) sb.AppendLine($"ALSO OPEN  {others.Count}: {string.Join(", ", others)}  <- check you are working on the right one");
});

// ---- 3. Project information ------------------------------------------------------------------------
section("Project info", () =>
{
    var pi = Document.ProjectInformation;
    if (pi == null) return;
    Func<BuiltInParameter, string> v = bip =>
    { var p = pi.get_Parameter(bip); var s = p == null ? null : p.AsString(); return string.IsNullOrWhiteSpace(s) ? "(blank)" : s; };
    sb.AppendLine($"PROJECT    {v(BuiltInParameter.PROJECT_NAME)}  |  number {v(BuiltInParameter.PROJECT_NUMBER)}"
                + $"  |  client {v(BuiltInParameter.CLIENT_NAME)}  |  status {v(BuiltInParameter.PROJECT_STATUS)}");
});

// ---- 4. Units — the one that silently ruins numbers -------------------------------------------------
// Read from a real level's own elevation rather than the units API: it names no version-specific type,
// and it answers the question that actually matters — what this project DISPLAYS, mm or m or feet.
section("Units", () =>
{
    var lvl = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
    if (lvl == null) { sb.AppendLine("UNITS      no Level to sample — check manually before any mm figure"); return; }
    var ep = lvl.get_Parameter(BuiltInParameter.LEVEL_ELEV);
    string shown = ep == null ? "(unreadable)" : (ep.AsValueString() ?? "(unreadable)");
    sb.AppendLine($"UNITS      level '{lvl.Name}' elevation displays as: {shown}   (internal {lvl.Elevation:F4} ft = {lvl.Elevation * 304.8:F1} mm)");
    sb.AppendLine("           ^ if that is NOT millimetres, restate every number with the user before calculating (START-HERE rule 3)");
});

// ---- 5. Size and framework -------------------------------------------------------------------------
section("Model size", () =>
{
    int elems  = new FilteredElementCollector(Document).WhereElementIsNotElementType().GetElementCount();
    int levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).GetElementCount();
    int grids  = new FilteredElementCollector(Document).OfClass(typeof(Grid)).GetElementCount();
    int views  = new FilteredElementCollector(Document).OfClass(typeof(View)).GetElementCount();
    int sheets = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).GetElementCount();
    sb.AppendLine($"SIZE       {elems} elements | {levels} levels | {grids} grids | {views} views | {sheets} sheets");
});

// ---- 6. Links --------------------------------------------------------------------------------------
section("Links", () =>
{
    var types = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();
    if (types.Count == 0) { sb.AppendLine("LINKS      none"); return; }
    var bad = new List<string>();
    foreach (var lt in types)
    {
        var st = lt.GetLinkedFileStatus();
        if (st != LinkedFileStatus.Loaded) bad.Add($"{lt.Name} [{st}]");
    }
    sb.AppendLine($"LINKS      {types.Count} link type(s), {types.Count - bad.Count} loaded"
                + (bad.Count > 0 ? $"  |  NOT LOADED: {string.Join(", ", bad)}  <- anything read through these will be MISSING" : ""));
});

// ---- 7. Worksets, design options, phases -----------------------------------------------------------
section("Worksets / options / phases", () =>
{
    if (Document.IsWorkshared)
    {
        var ws = new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).ToWorksets();
        int closed = ws.Count(w => !w.IsOpen);
        sb.AppendLine($"WORKSETS   {ws.Count} user workset(s)"
                    + (closed > 0 ? $", {closed} CLOSED  <- elements on a closed workset are invisible to every query" : ", all open"));
    }
    int opts = new FilteredElementCollector(Document).OfClass(typeof(DesignOption)).GetElementCount();
    if (opts > 0) sb.AppendLine($"OPTIONS    {opts} design option(s)  <- a query may be seeing only the primary option");
    try { sb.AppendLine($"PHASES     {string.Join(" -> ", Document.Phases.Cast<Phase>().Select(p => p.Name))}"); } catch { }
});

// ---- 8. Health -------------------------------------------------------------------------------------
section("Warnings", () =>
{
    int w = Document.GetWarnings().Count;
    sb.AppendLine($"WARNINGS   {w}" + (w > 0 ? "  (context/context-all-warnings.cs lists them)" : ""));
});

// ---- 9. Where the user is right now ----------------------------------------------------------------
section("Active view", () =>
{
    View view = UIDocument.ActiveView;
    sb.AppendLine($"VIEW       '{view.Name}' (Id {view.Id}, {view.ViewType}, 1:{view.Scale})"
                + ((view as ViewPlan)?.GenLevel != null ? $" on level {((ViewPlan)view).GenLevel.Name}" : ""));
    int sel = UIDocument.Selection.GetElementIds().Count;
    if (sel > 0) sb.AppendLine($"SELECTED   {sel} element(s) selected right now");
});

if (problems.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine($"Could not read {problems.Count} section(s) — the rest above is still good:");
    foreach (var p in problems) sb.AppendLine(p);
}

return sb.ToString();
