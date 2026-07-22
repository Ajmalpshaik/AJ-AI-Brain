// ============================================================
// FRAGMENT (action) — action-count-by-spatial-container.cs
// PURPOSE: Count `elements`, broken down by which Room, MEP Space, or HVAC Zone physically contains each
//          one. action-count-by-group.cs's plain parameter lookup CANNOT do this — most MEP elements
//          have no "Room"/"Space" parameter at all, so grouping by Room/Space/Zone needs the same
//          spatial IsPointInRoom/IsPointInSpace test filter-by-room.cs/filter-by-space.cs already use,
//          not a LookupParameter call. Zone is one hop further: find the containing Space, then read
//          that Space's own assigned Zone. (Phase is different from all three of these — it IS a normal
//          ElementId-storage parameter, so "count by Phase Created" already works fine with the plain
//          action-count-by-group.cs, no spatial test needed.)
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: spatial test technique reused from filter-by-room.cs/filter-by-space.cs (Room.IsPointInRoom /
//         Space.IsPointInSpace, tested at the container's own Z per the documented Z-mismatch gotcha).
// STATUS: the "space"/"zone" modes carry filter-by-space.cs's existing not-yet-live-verified caveat on
//         Space.IsPointInSpace; "room" mode reuses the already-verified Room.IsPointInRoom path.
// ============================================================
// PERFORMANCE NOTE: this is an O(elements x containers) spatial scan (same technique as filter-by-room.cs/
// filter-by-space.cs, just run once per element here instead of once total) — fine for a normal room/space
// count, but can get slow on a very large model with many rooms/spaces and a large `elements` set.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string containerType = "room"; // "room" | "space" | "zone"
// ---- END INPUTS ----

var rooms = containerType == "room"
    ? new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Architecture.Room>().ToList()
    : new List<Autodesk.Revit.DB.Architecture.Room>();

var spaces = (containerType == "space" || containerType == "zone")
    ? new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_MEPSpaces).WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Mechanical.Space>().ToList()
    : new List<Autodesk.Revit.DB.Mechanical.Space>();

Func<Element, string> groupKey = e =>
{
    var loc = (e.Location as LocationPoint)?.Point;
    if (loc == null)
    {
        var bb = e.get_BoundingBox(null);
        if (bb == null) return "(no location)";
        loc = (bb.Min + bb.Max) * 0.5;
    }

    if (containerType == "room")
    {
        foreach (var room in rooms)
        {
            double roomZ = (room.Location as LocationPoint)?.Point.Z ?? 0;
            var testPt = new XYZ(loc.X, loc.Y, roomZ);
            if (room.IsPointInRoom(testPt)) return $"{room.Name} {room.Number}";
        }
        return "(no room)";
    }

    // "space" and "zone" both need the containing Space first
    Autodesk.Revit.DB.Mechanical.Space foundSpace = null;
    foreach (var space in spaces)
    {
        double spaceZ = (space.Location as LocationPoint)?.Point.Z ?? 0;
        var testPt = new XYZ(loc.X, loc.Y, spaceZ);
        if (space.IsPointInSpace(testPt)) { foundSpace = space; break; }
    }

    if (containerType == "space")
        return foundSpace != null ? $"{foundSpace.Name} {foundSpace.Number}" : "(no space)";

    // "zone"
    if (foundSpace == null) return "(no space -> no zone)";
    var zone = foundSpace.Zone;
    return zone != null ? zone.Name : "(space has no zone)";
};

var groups = elements.GroupBy(groupKey).OrderByDescending(g => g.Count()).ToList();

sb.AppendLine($"Count by {containerType}, {elements.Count} element(s) total, {groups.Count} group(s):");
sb.AppendLine($"{containerType} | Qty");
sb.AppendLine("--- | ---");
foreach (var g in groups) sb.AppendLine($"{g.Key} | {g.Count()}");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
