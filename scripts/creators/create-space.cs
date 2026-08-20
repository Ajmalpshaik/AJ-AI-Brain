// ============================================================
// FRAGMENT (creator) — create-space.cs
// PURPOSE: Place an MEP Space at one or more points on a level — the Space-category equivalent of
//          create-room.cs (Space is a separate element type from Room even when it covers the same area;
//          several HVAC recipes — set-space-airflow.cs, place-terminals-checkerboard.cs — need a real
//          Space, not a Room).
// PRODUCES: elements (List<Element>, the newly created Space(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so any action fragment can be appended after it.
// ============================================================
// IMPORTANT: same caveat as create-room.cs — Document.Create.NewSpace(level, UV) only produces a properly
// bounded Space if the point sits inside an already-enclosed area (walls or room/space-separation lines
// forming a closed loop) on that level. Otherwise the Space element is still created but comes out
// unbounded (zero area) — always check `space.Area > 0` after creating, don't assume success just because
// no exception was thrown.
// LIVE-VERIFIED 2026-07-22: creation + Name/Number setters work exactly as written. GOTCHA found in the
// process (not a bug in THIS file, but affects anything reading the space back afterward): Revit
// auto-assigns a default Number to every new Space immediately on creation, and Element.Name always comes
// back as "{name} {number}" once a Number exists — which is always, since one gets auto-assigned even if
// you never set spacesToCreate's number field. If you need the PLAIN name back later (not "Name Number"),
// read `space.get_Parameter(BuiltInParameter.ROOM_NAME).AsString()` instead of `space.Name` — see
// filter-by-space.cs's file header for the full investigation.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId levelId = ElementId.InvalidElementId;
List<(double xMm, double yMm, string name, string number)> spacesToCreate = new List<(double, double, string, string)>
{
    (0, 0, "", "")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>();

if (levelId == ElementId.InvalidElementId)
{
    sb.AppendLine("No levelId set — nothing created.");
}
else
{
    var level = Document.GetElement(levelId) as Level;
    int unbounded = 0;

    using (var t = new Transaction(Document, "AJ Tools - Create Spaces"))
    {
        t.Start();
        try
        {
            foreach (var (xMm, yMm, name, number) in spacesToCreate)
            {
                var uv = new UV(
                    xMm / 304.8,
                    yMm / 304.8);
                var space = Document.Create.NewSpace(level, uv);
                if (!string.IsNullOrEmpty(name)) space.Name = name;
                if (!string.IsNullOrEmpty(number)) space.Number = number;
                elements.Add(space);
                if (space.Area <= 0) unbounded++;
            }
            t.Commit();
            sb.AppendLine($"Created {elements.Count} space(s) on level '{level?.Name}'.");
            if (unbounded > 0)
            {
                sb.AppendLine($"WARNING: {unbounded} space(s) came out unbounded (zero area) — no enclosing walls/separation lines found at that point. Check before relying on their Area.");
            }
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create spaces — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
