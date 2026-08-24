// ============================================================
// FRAGMENT (action) — action-report-ceiling-heights.cs
// PURPOSE: The CLEAR HEIGHT under the ceiling in each room — which ceilings are over it, how high the
//          underside of each one sits above the finished floor, and which is the main one. The number
//          every service above the ceiling depends on: duct routing depth, sprinkler drop length, FCU
//          space, the light fitting recess.
// ASSUMES: elements (List<Element>, Rooms) and sb (StringBuilder) exist from a filter above, e.g.
//          filter-by-category.cs with OST_Rooms.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ A ROOM'S SOLID STOPS AT ITS UPPER LIMIT, AND THAT IS WHY THIS LOOKS EMPTY WHEN IT ISN'T.
//    `room.ClosedShell` is bounded by the room's Upper Limit + Limit Offset. On a huge number of real
//    models the upper limit is left at the room's own level with a small offset, so the room solid stops
//    BELOW the ceiling and intersects nothing — and a naive report says "no ceiling" for a room with a
//    perfectly good ceiling in it. This is the single reason a ceiling-height script appears not to work.
//    Two passes here, in order, and the report always says which one produced the answer:
//      1. The room as it stands. Free, read-only, and correct whenever the upper limit is sensible.
//      2. Only if that found nothing and `allowTemporaryUpperLimit` is on: raise the room's upper limit
//         to a level above, measure, and **roll the whole thing back** so the room is exactly as it was.
//    Pass 2 opens a transaction and rolls it back, so this fragment is NOT purely read-only when it runs.
//    That is stated in the output every time it happens rather than hidden.
//
// ✱✱ A ROOM USUALLY HAS MORE THAN ONE CEILING, and "the ceiling height" means the main one. A bulkhead,
//    a soffit over the joinery and the main grid are three ceilings over one room, at three heights.
//    The MAIN ceiling is the one whose intersection with the room has the greatest VOLUME — not the
//    first found, not the lowest, not the largest by area. Taking the first is how a report ends up
//    quoting the height of a 400 mm bulkhead as the room's ceiling height. Every ceiling found is listed
//    with its own height so the drop and the bulkhead are both visible.
//
// ✱✱ FILTER THE CEILINGS TO THE ROOM'S OWN LEVEL. A tall room, or a raised upper limit from pass 2, will
//    happily intersect the ceiling of the floor ABOVE. Without the level check the answer is the storey
//    height, which looks plausible and is wrong.
//
// GOTCHA: an UNPLACED room (Area = 0) encloses nothing and has no solid — reported separately, not as a
//         failure. Same trap as everywhere else room work is done.
// GOTCHA: heights are measured to the ceiling's UNDERSIDE from the room's finished floor level, because
//         that is the clear dimension a service has to fit under. `CEILING_HEIGHTABOVELEVEL_PARAM` (the
//         ceiling's own Height Offset From Level) is reported alongside as a cross-check — the two agree
//         only when the ceiling's level is the room's level and the room has no base offset.
// GOTCHA: this reads CEILINGS. A room with no ceiling element — an exposed-services warehouse, a
//         double-height space — correctly reports none, and the clear height there is to the slab, which
//         is recipes/ray-trace-to-ceiling.cs's job.
// RELATED: ray-trace-to-ceiling.cs casts a ray up from ONE point and hits whatever is above it (slab,
//          duct, ceiling). This reports the CEILINGS over a whole room by real geometry. Use the ray for
//          "what is directly above this sprinkler", this for "what is the clear height in this room".
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Run it on ONE room whose ceiling height you
//   know and check the number before trusting a floor's worth.
//
// ✱✱ FIXED 2026-08-24 — LEVEL HEIGHTS HERE NOW USE `ProjectElevation`, NOT `Elevation`.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says (Project OR Shared); `ProjectElevation` is always from the project origin, which is
//    the space every XYZ in the model lives in. This fragment mixes a level height with real
//    coordinates, so on a model with a survey offset the old code was wrong by exactly that offset —
//    silently, with a plausible number and no error. See
//    knowledge/live-model/level-elevation-vs-project-elevation.md, and run
//    action-report-level-elevations.cs to see whether a given model is affected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool allowTemporaryUpperLimit = true;  // pass 2: raise the room's upper limit, measure, roll back
int levelsToRaise = 3;                 // how many levels up to raise to in pass 2
int maxRoomsReported = 40;
bool listEveryCeiling = true;          // false = main ceiling only, one row per room
// ---- END INPUTS ----

const double MM = 304.8;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put filter-by-category.cs with OST_Rooms above this.");
    return sb.ToString();
}

var rooms = elements.OfType<Autodesk.Revit.DB.Architecture.Room>().ToList();
int notRooms = elements.Count - rooms.Count;
if (rooms.Count == 0)
{
    sb.AppendLine($"None of the {elements.Count} element(s) is a Room.");
    return sb.ToString();
}

// levels in elevation order, for the temporary raise
var levels = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
    .OrderBy(l => l.Elevation).ToList();

// ---- the room's own solid, largest positive-volume piece of its closed shell ----
Func<Autodesk.Revit.DB.Architecture.Room, Solid> roomSolidOf = rm =>
{
    Solid best = null;
    try
    {
        var shell = rm.ClosedShell;
        if (shell == null) return null;
        foreach (var go in shell)
        {
            var s = go as Solid;
            if (s != null && s.Volume > 0 && (best == null || s.Volume > best.Volume)) best = s;
        }
    }
    catch { }
    return best;
};

// ---- a ceiling's own solid ----
Func<Element, Solid> ceilingSolidOf = el =>
{
    Solid best = null;
    try
    {
        var opt = new Options { ComputeReferences = false, IncludeNonVisibleObjects = true };
        var geo = el.get_Geometry(opt);
        if (geo == null) return null;
        foreach (var go in geo)
        {
            var s = go as Solid;
            if (s != null && s.Volume > 0 && (best == null || s.Volume > best.Volume)) best = s;
        }
    }
    catch { }
    return best;
};

// ---- ceilings intersecting a given room solid, restricted to the room's own level ----
Func<Solid, ElementId, List<Element>> ceilingsOver = (roomSolid, roomLevelId) =>
{
    var found = new List<Element>();
    if (roomSolid == null) return found;
    try
    {
        var col = new FilteredElementCollector(Document)
            .OfClass(typeof(Autodesk.Revit.DB.Ceiling))
            .WhereElementIsNotElementType()
            .WherePasses(new ElementIntersectsSolidFilter(roomSolid));
        foreach (var c in col)
        {
            // Without this the storey above gets caught and the answer becomes the floor-to-floor height.
            if (c.LevelId == roomLevelId) found.Add(c);
        }
    }
    catch { }
    return found;
};

// ---- report ----
var lines = new List<string>();
int withCeiling = 0, withoutCeiling = 0, unplaced = 0, usedPass2 = 0;

Func<Autodesk.Revit.DB.Architecture.Room, double> roomFloorZ = rm =>
{
    double z = 0;
    try
    {
        var lvl = Document.GetElement(rm.LevelId) as Level;
        if (lvl != null) z = lvl.ProjectElevation;
        var pBase = rm.get_Parameter(BuiltInParameter.ROOM_LOWER_OFFSET);
        if (pBase != null) z += pBase.AsDouble();
    }
    catch { }
    return z;
};

foreach (var rm in rooms.Take(maxRoomsReported))
{
    double area = 0;
    try { area = rm.Area; } catch { }
    if (area <= 0) { unplaced++; lines.Add($"{rm.Number} | {rm.Name} | UNPLACED (Area 0) — no solid to measure against"); continue; }

    var solid = roomSolidOf(rm);
    var found = ceilingsOver(solid, rm.LevelId);
    string howFound = "room as it stands";

    // ---- pass 2: temporarily raise the upper limit, measure, roll back ----
    if (found.Count == 0 && allowTemporaryUpperLimit && levels.Count > 0)
    {
        var roomLevel = Document.GetElement(rm.LevelId) as Level;
        int idx = roomLevel == null ? -1 : levels.FindIndex(l => l.Id == roomLevel.Id);
        if (idx >= 0)
        {
            int targetIdx = Math.Min(levels.Count - 1, idx + Math.Max(1, levelsToRaise));
            if (targetIdx > idx)
            {
                using (var tg = new TransactionGroup(Document, "AJ Tools - Measure ceiling height (temporary)"))
                {
                    tg.Start();
                    try
                    {
                        using (var t = new Transaction(Document, "AJ Tools - Raise room upper limit"))
                        {
                            t.Start();
                            var fo = t.GetFailureHandlingOptions();
                            fo.SetForcedModalHandling(false);
                            fo.SetClearAfterRollback(true);
                            t.SetFailureHandlingOptions(fo);
                            rm.UpperLimit = levels[targetIdx];
                            t.Commit();   // the shell only regrows on commit
                        }
                        var solid2 = roomSolidOf(rm);
                        found = ceilingsOver(solid2, rm.LevelId);
                        solid = solid2;
                        if (found.Count > 0) { howFound = $"upper limit temporarily raised to '{levels[targetIdx].Name}'"; usedPass2++; }
                    }
                    catch { }
                    finally { if (tg.HasStarted()) tg.RollBack(); }   // ALWAYS put the room back
                }
            }
        }
    }

    if (found.Count == 0)
    {
        withoutCeiling++;
        lines.Add($"{rm.Number} | {rm.Name} | no ceiling found{(allowTemporaryUpperLimit ? " (both passes)" : " — try allowTemporaryUpperLimit = true")}");
        continue;
    }

    withCeiling++;
    double floorZ = roomFloorZ(rm);

    // main ceiling = greatest intersection VOLUME with the room, not the first or the lowest
    Element main = null; double maxVol = 0;
    var perCeiling = new List<Tuple<Element, double, double>>(); // ceiling, underside height above floor, intersect volume
    foreach (var c in found)
    {
        double undersideFt = double.NaN;
        try
        {
            var bb = c.get_BoundingBox(null);
            if (bb != null) undersideFt = bb.Min.Z - floorZ;
        }
        catch { }

        double vol = 0;
        try
        {
            var cs = ceilingSolidOf(c);
            if (cs != null && solid != null)
            {
                var inter = BooleanOperationsUtils.ExecuteBooleanOperation(solid, cs, BooleanOperationsType.Intersect);
                if (inter != null) vol = inter.Volume;
            }
        }
        catch { }

        if (vol > maxVol) { maxVol = vol; main = c; }
        perCeiling.Add(Tuple.Create(c, undersideFt, vol));
    }
    if (main == null) main = found[0];

    var mainRow = perCeiling.FirstOrDefault(x => x.Item1.Id == main.Id);
    string mainH = mainRow != null && !double.IsNaN(mainRow.Item2) ? Math.Round(mainRow.Item2 * MM).ToString() + " mm" : "?";

    double offsetParam = double.NaN;
    try { var p = main.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM); if (p != null) offsetParam = p.AsDouble(); } catch { }

    lines.Add($"{rm.Number} | {rm.Name} | **{mainH}** | {found.Count} ceiling(s) | main Id {main.Id}"
        + (double.IsNaN(offsetParam) ? "" : $" | its Height Offset From Level {Math.Round(offsetParam * MM)} mm")
        + $" | {howFound}");

    if (listEveryCeiling && perCeiling.Count > 1)
    {
        foreach (var pc in perCeiling.OrderBy(x => x.Item2))
        {
            string h = double.IsNaN(pc.Item2) ? "?" : Math.Round(pc.Item2 * MM) + " mm";
            string tag = pc.Item1.Id == main.Id ? " (main)" : "";
            lines.Add($"    - Id {pc.Item1.Id} | underside {h} | intersect volume {Math.Round(pc.Item3 * 0.0283168 * 1000) / 1000.0} m3{tag}");
        }
    }
}

sb.AppendLine($"Ceiling heights across {rooms.Count} room(s){(notRooms > 0 ? $" ({notRooms} non-room element(s) ignored)" : "")}:");
sb.AppendLine($"{withCeiling} with a ceiling, {withoutCeiling} without, {unplaced} unplaced.");
if (usedPass2 > 0)
    sb.AppendLine($"⚠ {usedPass2} room(s) only answered after their upper limit was TEMPORARILY raised and rolled back — the model is unchanged, but a transaction was opened. Those rooms' Upper Limit is set too low to reach their own ceiling, which is worth fixing in the model.");
sb.AppendLine();
sb.AppendLine("Room | Name | Clear height to main ceiling | Ceilings | Detail");
sb.AppendLine("--- | --- | --- | --- | ---");
foreach (var l in lines) sb.AppendLine(l);
if (rooms.Count > maxRoomsReported) sb.AppendLine($"... {rooms.Count - maxRoomsReported} more room(s) not reported (raise maxRoomsReported).");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
