// ============================================================
// FRAGMENT (action) — action-report-curtain-elements.cs
// PURPOSE: Break a curtain wall down into what it is actually made of — panels, mullions and grid
//          lines — with type, size and area/length per piece, and a total per type. The façade
//          take-off, and the answer to "how many panels of each type are in this elevation".
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only: opens no Transaction and changes nothing.
//
// WHY THIS EXISTS: nothing in this library had ever touched a curtain wall. `CurtainGrid`, `Mullion`
//   and `Panel` appeared in no fragment, so a façade question had to start from raw C# every time.
//
// FOUR THINGS THAT MAKE A NAIVE VERSION WRONG:
//
//   1. A "panel" is not always a Panel. Revit lets a WALL type be used as curtain infill, and those
//      come back from GetPanelIds() as Wall elements, not Panel. Cast to Panel and you silently drop
//      every solid infill in the façade — usually the spandrels, which is the part someone is costing.
//
//   2. "All" and "unlocked" are two different questions. GetPanelIds()/GetMullionIds() give you
//      everything; GetUnlockedPanelIds()/GetUnlockedMullionIds() give you the ones Revit will let you
//      change individually. The difference IS the answer to "why won't this panel swap" — a locked
//      piece takes the call and does not change. Both counts are reported, and the gap is named.
//
//   3. A curtain wall is not the only thing with a grid. A CurtainSystem (and a sloped glazed roof)
//      carries `CurtainGrids` — PLURAL, one per face. Reading `.CurtainGrid` off a wall finds none of
//      them, so a project whose atrium is a curtain system reports zero and looks clean.
//
//   4. Mullion length is not the type's length. `mullion.LocationCurve.Length` is the real cut length
//      of that piece; the type only carries the profile. For a take-off you want the sum of the
//      instances, which is what this does.
//
// RELATED: actions/reporting/action-report-material-takeoff.cs (materials, once you know the pieces),
//          actions/structural-changes/action-change-element-type.cs (swap a panel type — and note it
//          preserves instance parameters, which a bare ChangeTypeId does not).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only, so the worst case is a wrong number rather than a wrong
//    model — but check one wall's panel count against Revit's own schedule before trusting a total.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string scope = "all";               // "all"      = every curtain wall / curtain system in the document
                                    // "view"     = only those visible in the target view
                                    // "selected" = only the ones currently selected in Revit

string wallTypeNameContains = "";   // "" = every curtain wall; else match on the host's TYPE name
int? targetViewIdInt = null;        // used by scope "view"; null = active view

bool listEachPiece = false;         // false = totals per type only (what you usually want)
                                    // true  = one line per panel and per mullion — long
int maxRowsListed = 200;            // cap on the per-piece list so a big façade does not flood the reply
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;
if (scope == "view" && view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
    return sb.ToString();
}

// ---- Collect the hosts: curtain WALLS and curtain SYSTEMS, kept apart because they are read
// differently (one grid vs many).
var hostWalls = new List<Wall>();
var hostSystems = new List<Element>();

if (scope == "selected")
{
    foreach (var id in UIDocument.Selection.GetElementIds())
    {
        var e = Document.GetElement(id);
        var w = e as Wall;
        if (w != null) { hostWalls.Add(w); continue; }
        if (e != null && e.Category != null && e.Category.Id == new ElementId(BuiltInCategory.OST_Curtain_Systems))
            hostSystems.Add(e);
    }
}
else
{
    var wallCollector = scope == "view"
        ? new FilteredElementCollector(Document, view.Id)
        : new FilteredElementCollector(Document);
    hostWalls.AddRange(wallCollector.OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType().Cast<Wall>());

    var sysCollector = scope == "view"
        ? new FilteredElementCollector(Document, view.Id)
        : new FilteredElementCollector(Document);
    hostSystems.AddRange(sysCollector.OfCategory(BuiltInCategory.OST_Curtain_Systems)
        .WhereElementIsNotElementType().ToElements());
}

// A plain wall has no CurtainGrid — reading the property is how you tell, and it is cheap.
var curtainWalls = new List<Wall>();
foreach (var w in hostWalls)
{
    try
    {
        if (w.CurtainGrid == null) continue;
        if (!string.IsNullOrEmpty(wallTypeNameContains))
        {
            var wt = Document.GetElement(w.GetTypeId()) as ElementType;
            string tn = wt != null ? (wt.Name ?? "") : "";
            if (tn.IndexOf(wallTypeNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
        }
        curtainWalls.Add(w);
    }
    catch { }   // some wall kinds throw rather than return null — same meaning: not a curtain wall
}

if (curtainWalls.Count == 0 && hostSystems.Count == 0)
{
    sb.AppendLine("No curtain walls or curtain systems found for this scope"
        + (string.IsNullOrEmpty(wallTypeNameContains) ? "" : $" with a type name containing \"{wallTypeNameContains}\"") + ".");
    return sb.ToString();
}

// ---- Walk every grid on every host and gather the pieces.
var panelAreaByType = new Dictionary<string, double>();     // sq ft
var panelCountByType = new Dictionary<string, int>();
var mullionLenByType = new Dictionary<string, double>();    // ft
var mullionCountByType = new Dictionary<string, int>();
var rows = new List<string>();

int gridsRead = 0, panelsRead = 0, mullionsRead = 0, gridLines = 0;
int panelsLocked = 0, mullionsLocked = 0, wallInfillPanels = 0;
int hostsSkipped = 0;

Func<Element, string> TypeNameOf = e =>
{
    try
    {
        var t = Document.GetElement(e.GetTypeId()) as ElementType;
        if (t == null) return "(no type)";
        string fam = "";
        try { fam = t.FamilyName ?? ""; } catch { }
        return string.IsNullOrEmpty(fam) ? (t.Name ?? "(unnamed)") : fam + " : " + (t.Name ?? "");
    }
    catch { return "(type unreadable)"; }
};

Action<CurtainGrid, string> ReadGrid = (grid, hostLabel) =>
{
    if (grid == null) return;
    gridsRead++;

    // "unlocked" is a SUBSET of the full list, not a separate one — it is the set Revit will let you
    // change piece by piece. Held as a set so the locked count below is a real subtraction.
    var unlockedPanels = new HashSet<ElementId>();
    var unlockedMullions = new HashSet<ElementId>();
    try { foreach (var id in grid.GetUnlockedPanelIds()) unlockedPanels.Add(id); } catch { }
    try { foreach (var id in grid.GetUnlockedMullionIds()) unlockedMullions.Add(id); } catch { }

    try { gridLines += grid.GetUGridLineIds().Count + grid.GetVGridLineIds().Count; } catch { }

    try
    {
        foreach (var pid in grid.GetPanelIds())
        {
            var p = Document.GetElement(pid);
            if (p == null) continue;
            panelsRead++;
            if (!unlockedPanels.Contains(pid)) panelsLocked++;

            // The trap: a Wall used as curtain infill arrives here too. Counting only `Panel` drops it.
            bool isWallInfill = p is Wall;
            if (isWallInfill) wallInfillPanels++;

            string tn = TypeNameOf(p) + (isWallInfill ? "  [wall infill]" : "");

            double area = 0.0;
            try
            {
                var ap = p.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if (ap != null && ap.HasValue) area = ap.AsDouble();
            }
            catch { }

            if (!panelAreaByType.ContainsKey(tn)) { panelAreaByType[tn] = 0.0; panelCountByType[tn] = 0; }
            panelAreaByType[tn] += area;
            panelCountByType[tn] += 1;

            if (listEachPiece && rows.Count < maxRowsListed)
                rows.Add($"  panel    {hostLabel}  {tn}  {Math.Round(area * 0.09290304, 3)} m2"
                         + (unlockedPanels.Contains(pid) ? "" : "  LOCKED") + $"  (Id {p.Id})");
        }
    }
    catch (Exception ex) { sb.AppendLine($"  (panels unreadable on {hostLabel}: {ex.Message})"); }

    try
    {
        foreach (var mid in grid.GetMullionIds())
        {
            var m = Document.GetElement(mid);
            if (m == null) continue;
            mullionsRead++;
            if (!unlockedMullions.Contains(mid)) mullionsLocked++;

            string tn = TypeNameOf(m);

            // The real cut length of THIS piece — the type only carries the profile.
            double len = 0.0;
            try
            {
                var lc = m.Location as LocationCurve;
                if (lc != null && lc.Curve != null) len = lc.Curve.Length;
            }
            catch { }

            if (!mullionLenByType.ContainsKey(tn)) { mullionLenByType[tn] = 0.0; mullionCountByType[tn] = 0; }
            mullionLenByType[tn] += len;
            mullionCountByType[tn] += 1;

            if (listEachPiece && rows.Count < maxRowsListed)
                rows.Add($"  mullion  {hostLabel}  {tn}  {Math.Round(len * 304.8, 0)} mm"
                         + (unlockedMullions.Contains(mid) ? "" : "  LOCKED") + $"  (Id {m.Id})");
        }
    }
    catch (Exception ex) { sb.AppendLine($"  (mullions unreadable on {hostLabel}: {ex.Message})"); }
};

foreach (var w in curtainWalls)
{
    string label = $"{TypeNameOf(w)} (Id {w.Id})";
    try { ReadGrid(w.CurtainGrid, label); }
    catch { hostsSkipped++; }
}

// Curtain systems and glazed roofs carry MANY grids — one per face. `.CurtainGrids` is a different
// property from a wall's `.CurtainGrid`, and reached by name so this fragment does not need the
// concrete type to compile on every Revit version.
foreach (var cs in hostSystems)
{
    string label = $"{TypeNameOf(cs)} (Id {cs.Id})";
    try
    {
        var prop = cs.GetType().GetProperty("CurtainGrids");
        if (prop == null) { hostsSkipped++; continue; }
        var set = prop.GetValue(cs) as System.Collections.IEnumerable;
        if (set == null) { hostsSkipped++; continue; }
        foreach (var g in set) ReadGrid(g as CurtainGrid, label);
    }
    catch { hostsSkipped++; }
}

// ---- Report
sb.AppendLine($"CURTAIN TAKE-OFF — {curtainWalls.Count} curtain wall(s), {hostSystems.Count} curtain system(s), {gridsRead} grid(s) read.");
sb.AppendLine($"  {panelsRead} panels ({wallInfillPanels} of them WALL infill, not Panel elements), {mullionsRead} mullions, {gridLines} grid lines.");
if (panelsLocked > 0 || mullionsLocked > 0)
    sb.AppendLine($"  LOCKED by the grid: {panelsLocked} panel(s), {mullionsLocked} mullion(s) — these will not change type or move individually. Unpin/unlock them first.");
if (hostsSkipped > 0)
    sb.AppendLine($"  {hostsSkipped} host(s) could not be read.");

if (panelCountByType.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("PANELS BY TYPE");
    sb.AppendLine("  Count |      Area m2 | Type");
    foreach (var kv in panelAreaByType.OrderByDescending(k => k.Value))
        sb.AppendLine($"  {panelCountByType[kv.Key],5} | {Math.Round(kv.Value * 0.09290304, 2),12} | {kv.Key}");
    sb.AppendLine($"  {panelCountByType.Values.Sum(),5} | {Math.Round(panelAreaByType.Values.Sum() * 0.09290304, 2),12} | TOTAL");
}

if (mullionCountByType.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("MULLIONS BY TYPE");
    sb.AppendLine("  Count |   Length m | Type");
    foreach (var kv in mullionLenByType.OrderByDescending(k => k.Value))
        sb.AppendLine($"  {mullionCountByType[kv.Key],5} | {Math.Round(kv.Value * 304.8 / 1000.0, 2),10} | {kv.Key}");
    sb.AppendLine($"  {mullionCountByType.Values.Sum(),5} | {Math.Round(mullionLenByType.Values.Sum() * 304.8 / 1000.0, 2),10} | TOTAL");
}

if (listEachPiece)
{
    sb.AppendLine();
    sb.AppendLine($"EACH PIECE (first {Math.Min(rows.Count, maxRowsListed)})");
    foreach (var r in rows) sb.AppendLine(r);
    if (panelsRead + mullionsRead > rows.Count)
        sb.AppendLine($"  ... {panelsRead + mullionsRead - rows.Count} more not listed (raise maxRowsListed).");
}

return sb.ToString();
