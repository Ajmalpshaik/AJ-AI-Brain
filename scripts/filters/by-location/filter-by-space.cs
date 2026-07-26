// ============================================================
// FRAGMENT (filter) — filter-by-space.cs
// PURPOSE: One category, narrowed to instances physically inside a given MEP Space. Same job as
//          filter-by-room.cs but for Spaces (mechanical/electrical zoning) instead of architectural
//          Rooms — the two are separate element types in Revit even when they cover the same area. The
//          space can be given by Id, Name, and/or Number — same lookup convention as
//          filter-by-assembly.cs/filter-by-group.cs, so you don't need the space's Id already in hand.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: mirrors filter-by-room.cs's Z-mismatch handling (../../../knowledge/live-model/core.md §
//         Room.IsPointInRoom...) — Space.IsPointInSpace is the Space-class equivalent of
//         Room.IsPointInRoom.
// LIVE-VERIFIED 2026-07-22 — FOUND AND FIXED A REAL BUG in the name-matching logic (IsPointInSpace itself
// was already correct). The original code matched on `s.Name`, Element.Name's C# property — but for
// Space (and, near-certainly, Room, same underlying mechanism), Element.Name is NOT the plain name you
// set. Revit auto-assigns a default Number to every new Space at creation, and ALWAYS derives Element.Name
// as "{name} {number}" whenever a Number is present (confirmed: creating a space with only Name="AJ TEST
// Space" set, never touching Number at all, still produced Element.Name == "AJ TEST Space T03" because
// Revit had already auto-assigned Number "T03" on creation — there is no way to get a plain, un-suffixed
// Element.Name on a Space with any Number set, and Number is essentially never truly absent). This broke
// exact-name lookups like this fragment's spaceName match. The PURE name (what you actually typed into
// Name) lives in a separate parameter — confirmed via BuiltInParameter.ROOM_NAME (yes, ROOM_NAME — Space
// reuses the Room parameter set), which reads back "AJ TEST Space" correctly regardless of Number. Fixed
// spaceName matching to read that parameter instead of Element.Name. spaceNumber matching (Space.Number)
// was already correct and unaffected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;
ElementId spaceId = ElementId.InvalidElementId; // set this OR spaceName/spaceNumber below — Id is checked first if set
string spaceName = ""; // "" = not checked; e.g. "Office"
string spaceNumber = ""; // "" = not checked; e.g. "101" — combine with spaceName for an exact match when numbers repeat across levels
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

Autodesk.Revit.DB.Mechanical.Space space = null;
if (spaceId != ElementId.InvalidElementId)
{
    space = Document.GetElement(spaceId) as Autodesk.Revit.DB.Mechanical.Space;
}
if (space == null && (!string.IsNullOrEmpty(spaceName) || !string.IsNullOrEmpty(spaceNumber)))
{
    space = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_MEPSpaces)
        .WhereElementIsNotElementType()
        .Cast<Autodesk.Revit.DB.Mechanical.Space>()
        .FirstOrDefault(s =>
            (string.IsNullOrEmpty(spaceName) || (s.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "").Equals(spaceName, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(spaceNumber) || (s.Number ?? "").Equals(spaceNumber, StringComparison.OrdinalIgnoreCase)));
}

if (space == null)
{
    sb.AppendLine("Space not found — set spaceId, spaceName, and/or spaceNumber to a real MEP Space.");
}
else
{
    double spaceZ = (space.Location as LocationPoint)?.Point.Z ?? 0;

    elements = new FilteredElementCollector(Document)
        .OfCategory(targetCategory)
        .WhereElementIsNotElementType()
        .Where(e =>
        {
            var loc = (e.Location as LocationPoint)?.Point;
            if (loc == null) return false;
            var testPt = new XYZ(loc.X, loc.Y, spaceZ); // space's own Z, not the element's real Z
            return space.IsPointInSpace(testPt);
        })
        .ToList();

    sb.AppendLine($"Filtered {elements.Count} element(s) in space {space.Name} {space.Number}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
