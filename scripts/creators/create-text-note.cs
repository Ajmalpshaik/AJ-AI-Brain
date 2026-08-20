// ============================================================
// FRAGMENT (creator) — create-text-note.cs
// PURPOSE: Place one or more Text Notes at given points in a view.
// PRODUCES: elements (List<Element>, the newly created TextNote(s)), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so any action fragment can be appended after it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0; // 0 = use Document.ActiveView
string textNoteTypeName = null; // null = use the first available Text Note type in the project
List<(double xMm, double yMm, string text)> notesToCreate = new List<(double, double, string)>
{
    (0, 0, "Note text")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var view = viewIdInt != 0 ? Document.GetElement(new ElementId(viewIdInt)) as View : Document.ActiveView;
if (view == null)
{
    sb.AppendLine($"View not found for viewIdInt {viewIdInt}.");
}
else
{
    ElementId textTypeId;
    if (string.IsNullOrEmpty(textNoteTypeName))
    {
        textTypeId = new FilteredElementCollector(Document).OfClass(typeof(TextNoteType)).FirstElementId();
    }
    else
    {
        textTypeId = new FilteredElementCollector(Document).OfClass(typeof(TextNoteType))
            .FirstOrDefault(tt => tt.Name.Equals(textNoteTypeName, StringComparison.OrdinalIgnoreCase))?.Id
            ?? ElementId.InvalidElementId;
    }

    if (textTypeId == null || textTypeId == ElementId.InvalidElementId)
    {
        sb.AppendLine("No Text Note type found" + (string.IsNullOrEmpty(textNoteTypeName) ? " in the project." : $" named '{textNoteTypeName}'."));
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Create Text Notes"))
        {
            t.Start();
            try
            {
                foreach (var (xMm, yMm, text) in notesToCreate)
                {
                    var origin = new XYZ(
                        xMm / 304.8,
                        yMm / 304.8,
                        0);
                    var note = TextNote.Create(Document, view.Id, origin, text, textTypeId);
                    elements.Add(note);
                }
                t.Commit();
                sb.AppendLine($"Created {elements.Count} text note(s) in view '{view.Name}'.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to create text notes — rolled back, nothing changed. Reason: {ex.Message}");
                elements = new List<Element>();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
