// ============================================================
// FRAGMENT (action) — action-check-room-mep-completeness.cs
// PURPOSE: Check that every room has the MEP it is supposed to have — a diffuser and an extract in each
//          toilet, a sprinkler in every enclosed space, a detector per room, lighting everywhere. Reports
//          per room what is missing against a rules table, so the gap is a list of rooms to go and fix
//          rather than a feeling that something was forgotten.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the ROOMS
//          or SPACES, e.g. filter-by-category.cs with OST_Rooms, usually scoped to one level with
//          filter-by-elements-on-level.cs. Read-only.
//
// ✱✱ THE RULES ARE THE INPUT AND THERE IS NO SENSIBLE DEFAULT SET. What a room needs depends on the
//    project, the discipline and the authority — a toilet needs extract in every specification and a
//    store room needs it in some. `rules` matches on the ROOM NAME (a substring, case-insensitive) and
//    says what must be there and how many. Fill it in for the job; the placeholders below are a shape,
//    not a standard.
//
// ✱✱ A DEVICE IS "IN" A ROOM BY REVIT'S OWN TEST, not by a bounding box. `Room.IsPointInRoom` uses the
//    room's real boundary, so an L-shaped room does not claim the device in the notch outside it. Devices
//    are placed by their insertion point; a device whose point sits in the ceiling void above the room
//    still counts, which is what you want for a diffuser and is stated here so it is not a surprise.
//
// ✱✱ ROOMS WITH NO AREA ARE EXCLUDED AND COUNTED. An unplaced or unenclosed room has no boundary, so
//    every device test against it returns false and it would report as missing everything — dozens of
//    false failures that bury the real ones. Those rooms are a different defect
//    (filter-by-unenclosed-spatial-elements.cs) and are reported as a count, not as failures.
//
// GOTCHA: IT MATCHES ON ROOM NAME. A room called "WC 1" and one called "Toilet 3" both need a rule that
//         catches them — put both words in the rules, or rename the rooms. Rooms matching NO rule are
//         counted and listed separately so a naming mismatch does not read as a clean result.
// GOTCHA: DEVICES IN LINKED MODELS ARE NOT COUNTED. If the sprinklers are in a separate fire model, this
//         will report every room as missing them. The per-category totals found are printed first for
//         exactly that reason — a category with zero found across the whole model is a link problem.
// GOTCHA: A ROOM CAN MATCH SEVERAL RULES and all of them apply. That is deliberate: "every room needs a
//         detector" and "every toilet needs extract" are both true of a toilet.
// RELATED: action-count-by-spatial-container.cs (how many of X per room, with no rule attached),
//          filter-by-room.cs (the elements in one room as an actionable set),
//          filter-by-unenclosed-spatial-elements.cs (the rooms that cannot be checked at all),
//          action-check-firefighting via the sprinkler recipes for coverage rather than presence.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Check one room's counts by eye before
//   trusting a whole floor's table.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// roomNameContains -> what must be in it. "" matches EVERY room.
// Each entry: room-name substring, the category, a friendly label, and the minimum count required.
var rules = new List<(string RoomNameContains, BuiltInCategory Cat, string Label, int MinCount)>
{
    ("",       BuiltInCategory.OST_Sprinklers,    "sprinkler head",  1),
    ("",       BuiltInCategory.OST_LightingFixtures, "light fitting", 1),
    ("toilet", BuiltInCategory.OST_DuctTerminal,  "air terminal",    1),
    ("wc",     BuiltInCategory.OST_DuctTerminal,  "air terminal",    1),
    ("office", BuiltInCategory.OST_DuctTerminal,  "air terminal",    1),
};

bool includeCeilingVoid = true;   // count a device whose point is above the room's ceiling as being in it
double voidHeightMm = 1500;       // how far above the room's top to still count a device
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the Rooms or Spaces).");
    return sb.ToString();
}

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

// ---- the rooms ----
var rooms = new List<Autodesk.Revit.DB.Architecture.Room>();
var spaces = new List<Autodesk.Revit.DB.Mechanical.Space>();
int notSpatial = 0, noArea = 0;

foreach (var el in elements)
{
    var r = el as Autodesk.Revit.DB.Architecture.Room;
    if (r != null) { if (r.Area <= 0) noArea++; else rooms.Add(r); continue; }
    var sp = el as Autodesk.Revit.DB.Mechanical.Space;
    if (sp != null) { if (sp.Area <= 0) noArea++; else spaces.Add(sp); continue; }
    notSpatial++;
}

int totalRooms = rooms.Count + spaces.Count;
if (totalRooms == 0)
{
    sb.AppendLine($"No placed, enclosed Rooms or Spaces in the set ({noArea} had zero area, {notSpatial} were not spatial elements at all).");
    sb.AppendLine("An unplaced or unenclosed room has no boundary to test against — filter-by-unenclosed-spatial-elements.cs is the check for those.");
    return sb.ToString();
}

// ---- the devices, once per category in the rules ----
var neededCats = rules.Select(r => r.Cat).Distinct().ToList();
var devicesByCat = new Dictionary<BuiltInCategory, List<(Element El, XYZ Pt)>>();

foreach (var cat in neededCats)
{
    var list = new List<(Element El, XYZ Pt)>();
    try
    {
        foreach (var e in new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType())
        {
            XYZ pt = null;
            var lp = e.Location as LocationPoint;
            if (lp != null) pt = lp.Point;
            else
            {
                var lc = e.Location as LocationCurve;
                if (lc != null && lc.Curve != null) pt = lc.Curve.Evaluate(0.5, true);
            }
            if (pt != null) list.Add((e, pt));
        }
    }
    catch { }
    devicesByCat[cat] = list;
}

sb.AppendLine($"ROOM MEP COMPLETENESS — {totalRooms} placed room/space(s) against {rules.Count} rule(s)");
if (noArea > 0) sb.AppendLine($"NOTE: {noArea} room(s) have no area (unplaced or unenclosed) and CANNOT be checked — not a pass, a different defect.");
if (notSpatial > 0) sb.AppendLine($"NOTE: {notSpatial} element(s) in the set were not Rooms or Spaces and were ignored.");
sb.AppendLine();

sb.AppendLine("DEVICES FOUND IN THE HOST MODEL (a zero here means a LINK problem, not a room problem):");
foreach (var cat in neededCats)
    sb.AppendLine($"  {cat}: {devicesByCat[cat].Count}");
sb.AppendLine();

// ---- test each room ----
Func<object, string> nameOf = o =>
{
    var r = o as Autodesk.Revit.DB.Architecture.Room;
    if (r != null) return r.Name ?? "";
    var s = o as Autodesk.Revit.DB.Mechanical.Space;
    return s != null ? (s.Name ?? "") : "";
};
Func<object, ElementId> idOf = o =>
{
    var r = o as Autodesk.Revit.DB.Architecture.Room;
    if (r != null) return r.Id;
    var s = o as Autodesk.Revit.DB.Mechanical.Space;
    return s != null ? s.Id : ElementId.InvalidElementId;
};
Func<object, bool, XYZ, bool> pointInRoom = (o, allowVoid, pt) =>
{
    var r = o as Autodesk.Revit.DB.Architecture.Room;
    var s = o as Autodesk.Revit.DB.Mechanical.Space;
    try
    {
        if (r != null && r.IsPointInRoom(pt)) return true;
        if (s != null && s.IsPointInSpace(pt)) return true;
    }
    catch { }

    if (!allowVoid) return false;

    // A ceiling diffuser's insertion point often sits above the room's upper boundary, so the plain test
    // says no. Drop the point back into the room's own height band and re-test in plan.
    try
    {
        Element re = (Element)o;
        var bb = re.get_BoundingBox(null);
        if (bb == null) return false;
        if (pt.Z < bb.Min.Z || pt.Z > bb.Max.Z + ToFeet(voidHeightMm)) return false;
        var dropped = new XYZ(pt.X, pt.Y, (bb.Min.Z + bb.Max.Z) / 2.0);
        if (r != null) return r.IsPointInRoom(dropped);
        if (s != null) return s.IsPointInSpace(dropped);
    }
    catch { }
    return false;
};

var allRooms = new List<object>();
foreach (var r in rooms) allRooms.Add(r);
foreach (var s in spaces) allRooms.Add(s);

var failures = new List<(string Room, ElementId Id, string Label, int Found, int Needed)>();
var noRuleMatched = new List<string>();
int roomsChecked = 0, roomsOk = 0;

foreach (var room in allRooms)
{
    string rname = nameOf(room);
    var applicable = rules.Where(r =>
        string.IsNullOrEmpty(r.RoomNameContains) ||
        rname.IndexOf(r.RoomNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

    if (applicable.Count == 0)
    {
        noRuleMatched.Add($"{rname} ({idOf(room)})");
        continue;
    }

    roomsChecked++;
    bool roomClean = true;

    foreach (var rule in applicable)
    {
        int found = 0;
        foreach (var d in devicesByCat[rule.Cat])
            if (pointInRoom(room, includeCeilingVoid, d.Pt)) found++;

        if (found < rule.MinCount)
        {
            failures.Add((rname, idOf(room), rule.Label, found, rule.MinCount));
            roomClean = false;
        }
    }
    if (roomClean) roomsOk++;
}

// ---- report ----
sb.AppendLine($"ROOMS CHECKED: {roomsChecked}   COMPLETE: {roomsOk}   WITH SOMETHING MISSING: {roomsChecked - roomsOk}");
if (noRuleMatched.Count > 0)
    sb.AppendLine($"NO RULE MATCHED: {noRuleMatched.Count} room(s) — these were NOT checked. Usually a room-naming mismatch: " +
                  string.Join(", ", noRuleMatched.Take(12)) + (noRuleMatched.Count > 12 ? " ..." : ""));
sb.AppendLine();

if (failures.Count == 0)
{
    sb.AppendLine("COMPLETE — every checked room has what its rules require.");
    return sb.ToString();
}

sb.AppendLine("| Room | Id | Missing | Found | Needed |");
sb.AppendLine("|---|---|---|---|---|");
foreach (var f in failures.OrderBy(f => f.Room).ThenBy(f => f.Label).Take(maxReportedRows))
    sb.AppendLine($"| {f.Room} | {f.Id} | {f.Label} | {f.Found} | {f.Needed} |");
if (failures.Count > maxReportedRows)
    sb.AppendLine($"\n... and {failures.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
sb.AppendLine("By what is missing:");
foreach (var g in failures.GroupBy(f => f.Label).OrderByDescending(g => g.Count()))
    sb.AppendLine($"  {g.Key}: missing from {g.Count()} room(s)");

return sb.ToString();
