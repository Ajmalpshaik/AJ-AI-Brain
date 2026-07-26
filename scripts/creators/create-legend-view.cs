// ============================================================
// FRAGMENT (creator) — create-legend-view.cs
// PURPOSE: Create a new Legend view by DUPLICATING an existing one — the only route the Revit API offers.
//          There is no ViewLegend.Create on any Revit version, so a project with zero legends cannot get
//          its first one from a script; this fragment says so plainly instead of failing obscurely.
// PRODUCES: elements (List<Element>, the new legend view), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: PARTIAL API LIMIT — duplicating copies the source legend's CONTENT too (its legend components,
//         text, lines). There is no API to place a NEW legend component, so building a legend's contents
//         from scratch stays a manual job. Practical pattern: keep one office template legend in the
//         project, duplicate it here, then edit its text via action-find-replace-text-notes.cs.
// GOTCHA: legends are the one view type that can sit on MANY sheets at once — no need to duplicate per
//         sheet, just place the same one repeatedly (action-place-viewport-on-sheet.cs).
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceLegendName = null; // existing legend to duplicate; null = the first legend found
string newName = null;          // name for the new legend; null = Revit's generated name
bool withContents = true;       // true = copy the source's contents too; false = an empty legend frame
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var legends = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
    .ToList();

if (legends.Count == 0)
{
    sb.AppendLine("This project has NO legend views, and Revit's API cannot create the first one — there is no ViewLegend.Create on any version.");
    sb.AppendLine("Ask the user to make one legend manually (View tab > Legends > Legend). After that this fragment can duplicate it any number of times.");
}
else
{
    var source = sourceLegendName == null
        ? legends.First()
        : legends.FirstOrDefault(v => v.Name == sourceLegendName);

    if (source == null)
    {
        sb.AppendLine($"Legend '{sourceLegendName}' not found. Available: {string.Join(", ", legends.Select(v => $"'{v.Name}'"))}");
    }
    else
    {
        var mode = withContents ? ViewDuplicateOption.WithDetailing : ViewDuplicateOption.Duplicate;
        if (!source.CanViewBeDuplicated(mode))
        {
            sb.AppendLine($"Legend '{source.Name}' reports it cannot be duplicated {(withContents ? "with its contents" : "empty")} — try the other withContents setting.");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Create Legend View"))
            {
                t.Start();
                try
                {
                    var newId = source.Duplicate(mode);
                    var newLegend = Document.GetElement(newId) as View;
                    if (!string.IsNullOrEmpty(newName))
                    {
                        try { newLegend.Name = newName; } catch { } // name collision — keeps generated name
                    }
                    elements.Add(newLegend);
                    t.Commit();
                    sb.AppendLine($"Created legend '{newLegend.Name}' (Id {newLegend.Id.IntegerValue}) by duplicating '{source.Name}'{(withContents ? " with its contents" : " as an empty frame")}.");
                    sb.AppendLine("  Edit its text via action-find-replace-text-notes.cs — placing NEW legend components has no API (see header).");
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"FAILED to duplicate legend — rolled back, nothing created. Reason: {ex.Message}");
                    elements = new List<Element>();
                }
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
