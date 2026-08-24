// ============================================================
// FRAGMENT (creator) — create-point-based-element.cs
// PURPOSE: Place a family instance (door, window, piece of equipment, furniture, anything
//          point-placed) at one or more points on a level. Matches the user's own past request shape
//          ("add N of X on level Y").
//          THIS IS THE GENERAL PLACER — reach for it whenever something has to be PUT somewhere and no
//          specialised recipe covers it: "place an FCU", "add a diffuser here", "put a sleeve at that
//          point", "add 4 sprinklers on level 2", "insert this family", "drop equipment in this room".
//
// ✱✱ IF A RECIPE ALREADY COVERS THE JOB, IT IS BETTER THAN THIS, because it also works out WHERE.
//    This fragment places what you tell it where you tell it; the recipes below compute the positions:
//      recipes/place-fcu.cs                          an FCU in a room, at ceiling-void height, its
//                                                    supply connector turned to face the room.
//      recipes/place-terminals-checkerboard.cs       supply/return terminals in a matched grid.
//      recipes/sprinkler-place-heads.cs              heads at computed centres, then read back.
//      recipes/generate-room-coverage-layout.cs      any fixed-radius device so a room has no gap.
//      recipes/place-sleeves-at-wall-penetrations.cs sleeves where services cross walls.
//    The family must be LOADED first — creators/load-family.cs. Measured 2026-08-24: searching this
//    fragment's own description did not return it in the top five, which is why the phrasings above are
//    written in.
// PRODUCES: elements (List<Element>, the newly placed instance(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role
//          as a filter — it produces `elements` — so any action fragment can be appended after it.
// ============================================================
// Every point and level is a per-request input — never a default. `StructuralType` must be fully
// qualified in this script context (bare `StructuralType` fails to compile — see ../../knowledge/live-model/core.md).

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string familyNameContains = "";      // e.g. "STI_ME_FCU" — leave blank to match any family
string typeNameContains = "";        // narrow to a specific type within the family, or leave blank
ElementId levelId = ElementId.InvalidElementId;
List<(double xMm, double yMm, double zMm)> pointsMm = new List<(double, double, double)>
{
    (0, 0, 0)
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (levelId == ElementId.InvalidElementId)
{
    sb.AppendLine("No levelId set — nothing placed.");
}
else
{
    var symbol = new FilteredElementCollector(Document)
        .OfClass(typeof(FamilySymbol))
        .Cast<FamilySymbol>()
        .FirstOrDefault(fs =>
            (string.IsNullOrEmpty(familyNameContains) || fs.Family.Name.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
            && (string.IsNullOrEmpty(typeNameContains) || fs.Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0));

    if (symbol == null)
    {
        sb.AppendLine($"No FamilySymbol found matching family '{familyNameContains}' / type '{typeNameContains}' — nothing placed.");
    }
    else
    {
        var level = Document.GetElement(levelId) as Level;

        using (var t = new Transaction(Document, "AJ Tools - Place Elements"))
        {
            t.Start();
            try
            {
                if (!symbol.IsActive) symbol.Activate();
                foreach (var (xMm, yMm, zMm) in pointsMm)
                {
                    var pt = new XYZ(
                        xMm / 304.8,
                        yMm / 304.8,
                        zMm / 304.8);
                    var fi = Document.Create.NewFamilyInstance(pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    elements.Add(fi);
                }
                t.Commit();
                sb.AppendLine($"Placed {elements.Count} instance(s) of '{symbol.Family.Name} : {symbol.Name}' on level '{level?.Name}'.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to place elements — rolled back, nothing changed. Reason: {ex.Message}");
                elements = new List<Element>();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
