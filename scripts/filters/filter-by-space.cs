// ============================================================
// FRAGMENT (filter) — filter-by-space.cs
// PURPOSE: One category, narrowed to instances physically inside a given MEP Space. Same job as
//          filter-by-room.cs but for Spaces (mechanical/electrical zoning) instead of architectural
//          Rooms — the two are separate element types in Revit even when they cover the same area.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: mirrors filter-by-room.cs's Z-mismatch handling (../../knowledge/live-model/core.md §
//         Room.IsPointInRoom...) — Space.IsPointInSpace is the Space-class equivalent of
//         Room.IsPointInRoom.
// STATUS: not yet live-verified — confirm Space.IsPointInSpace resolves in this Revit version before
//         trusting bulk results.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctTerminal;
ElementId spaceId = ElementId.InvalidElementId; // set explicitly — Id of the MEP Space, not a Room
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

if (spaceId == ElementId.InvalidElementId)
{
    sb.AppendLine("No spaceId set — filter produced 0 elements.");
}
else
{
    var space = Document.GetElement(spaceId) as Autodesk.Revit.DB.Mechanical.Space;
    if (space == null)
    {
        sb.AppendLine($"spaceId {spaceId} is not a valid MEP Space — filter produced 0 elements.");
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
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
