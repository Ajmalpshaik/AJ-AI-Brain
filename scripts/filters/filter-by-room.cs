// ============================================================
// FRAGMENT (filter) — filter-by-room.cs
// PURPOSE: One category, narrowed to instances physically inside a given room. Handles the
//          Room.IsPointInRoom Z-mismatch gotcha (fails for elements above/below the room's own
//          vertical range, e.g. an FCU in the ceiling void) by testing at the room's own Z. The room can
//          be given by Id, Name, and/or Number — same lookup convention as filter-by-assembly.cs/
//          filter-by-group.cs, so you don't need the room's Id already in hand.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ../knowledge/live-model/core.md § Room.IsPointInRoom silently returns false...
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;
ElementId roomId = ElementId.InvalidElementId; // set this OR roomName/roomNumber below — Id is checked first if set
string roomName = ""; // "" = not checked; e.g. "Office"
string roomNumber = ""; // "" = not checked; e.g. "101" — combine with roomName for an exact match when numbers repeat across levels
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

Autodesk.Revit.DB.Architecture.Room room = null;
if (roomId != ElementId.InvalidElementId)
{
    room = Document.GetElement(roomId) as Autodesk.Revit.DB.Architecture.Room;
}
if (room == null && (!string.IsNullOrEmpty(roomName) || !string.IsNullOrEmpty(roomNumber)))
{
    room = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_Rooms)
        .WhereElementIsNotElementType()
        .Cast<Autodesk.Revit.DB.Architecture.Room>()
        .FirstOrDefault(r =>
            (string.IsNullOrEmpty(roomName) || (r.Name ?? "").Equals(roomName, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(roomNumber) || (r.Number ?? "").Equals(roomNumber, StringComparison.OrdinalIgnoreCase)));
}

if (room == null)
{
    sb.AppendLine("Room not found — set roomId, roomName, and/or roomNumber to a real Room.");
}
else
{
    double roomZ = (room.Location as LocationPoint)?.Point.Z ?? 0;

    elements = new FilteredElementCollector(Document)
        .OfCategory(targetCategory)
        .WhereElementIsNotElementType()
        .Where(e =>
        {
            var loc = (e.Location as LocationPoint)?.Point;
            if (loc == null) return false;
            var testPt = new XYZ(loc.X, loc.Y, roomZ); // room's own Z, not the element's real Z
            return room.IsPointInRoom(testPt);
        })
        .ToList();

    sb.AppendLine($"Filtered {elements.Count} element(s) in room {room.Name} {room.Number}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
