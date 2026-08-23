// ============================================================
// FRAGMENT (action) — action-report-view-references.cs
// PURPOSE: For each section mark, callout bubble, elevation mark or reference view in `elements`, report
//          WHICH VIEW IT POINTS AT — and optionally re-point it at a different one. The fix for a
//          section mark whose bubble reads the wrong sheet/detail number because it is referencing a
//          view nobody uses any more.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs
//          with OST_Sections / OST_Callouts / OST_Elev, or filter-by-current-selection.cs.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ A MARKER AND THE VIEW IT REFERENCES ARE TWO SEPARATE ELEMENTS, and the link between them is not a
//    parameter. A section mark sitting in a plan is a marker element; the section view it opens is a
//    different element entirely, and the head/tail text on the mark is drawn from THAT view's Detail
//    Number and the sheet it sits on. So a mark showing the wrong numbers is almost never a text
//    problem — it is pointing at the wrong view. Revit exposes the link directly:
//        ReferenceableViewUtils.GetReferencedViewId(doc, markerId)
//        ReferenceableViewUtils.ChangeReferencedView(doc, markerId, desiredViewId)
//    Nothing in this library was reading either one, so "the section mark is wrong" had no answer here.
//
// ✱✱ RE-POINTING IS THE POINT OF THE SECOND HALF, and it is a real edit. `ChangeReferencedView` makes
//    this mark open, and annotate for, a different view. Typical use: a set was duplicated, the marks
//    still reference the ORIGINAL views, and every bubble on the new sheets shows the old sheet number.
//    Set newReferencedViewIdInt and the marks in `elements` all re-point to it.
//
// GOTCHA: DRY RUN BY DEFAULT.
// GOTCHA: NOT EVERY ELEMENT HAS A REFERENCED VIEW. A plain detail line or a dimension returns
//         InvalidElementId — reported as "does not reference a view", never as an error, so a mixed
//         selection is readable instead of noisy.
// GOTCHA: THE TARGET MUST BE A REFERENCEABLE VIEW OF THE RIGHT KIND. A section mark cannot be re-pointed
//         at a schedule, and Revit refuses. Every refusal is reported with its Id.
// GOTCHA: re-pointing every mark in a selection at ONE view is usually what is wanted only for a small,
//         deliberate set. Check the report first — this fragment will happily point forty marks at the
//         same section if that is what it is told to do.
// RELATED: action-report-views-showing-element.cs answers "which views show this element";
//          this answers "which view does this marker open".
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run the report on ONE section mark you can
//   see in a plan, and check the view it names is the one that opens when you double-click it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int newReferencedViewIdInt = 0; // 0 = report only; otherwise re-point every marker in `elements` at this view
bool dryRun = true;             // true = say what would change, change nothing
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this (filter-by-category.cs with OST_Sections, or filter-by-current-selection.cs).");
    return sb.ToString();
}

// ---- read the current reference on each ----
var rows = new List<(Element marker, ElementId viewId, string viewLabel)>();
int withReference = 0;

foreach (var el in elements)
{
    if (el == null) continue;
    ElementId refId = ElementId.InvalidElementId;
    try { refId = ReferenceableViewUtils.GetReferencedViewId(Document, el.Id); } catch { }

    if (refId == ElementId.InvalidElementId)
    {
        rows.Add((el, refId, "(does not reference a view)"));
        continue;
    }
    withReference++;

    var v = Document.GetElement(refId) as View;
    string label;
    if (v == null) label = $"Id {refId} — not a view any more (deleted?)";
    else
    {
        // The numbers on the bubble come from here, so they are what the report has to show.
        string detail = "", sheet = "";
        try { detail = v.get_Parameter(BuiltInParameter.VIEWER_DETAIL_NUMBER)?.AsString() ?? ""; } catch { }
        try { sheet = v.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER)?.AsString() ?? ""; } catch { }
        label = $"'{v.Name}' ({v.ViewType}, Id {refId})";
        if (!string.IsNullOrEmpty(detail) || !string.IsNullOrEmpty(sheet))
            label += $" — detail {(string.IsNullOrEmpty(detail) ? "-" : detail)} on sheet {(string.IsNullOrEmpty(sheet) ? "not placed" : sheet)}";
        else label += " — not placed on a sheet";
    }
    rows.Add((el, refId, label));
}

sb.AppendLine($"{elements.Count} element(s) in, {withReference} of them reference a view:");
sb.AppendLine("Marker Id | Marker category | References");
sb.AppendLine("--- | --- | ---");
foreach (var (marker, viewId, viewLabel) in rows.Take(60))
    sb.AppendLine($"{marker.Id} | {marker.Category?.Name ?? "(none)"} | {viewLabel}");
if (rows.Count > 60) sb.AppendLine($"... {rows.Count - 60} more not shown.");

if (newReferencedViewIdInt == 0)
{
    sb.AppendLine();
    sb.AppendLine("Report only. To re-point these marks, set newReferencedViewIdInt to the target view's Id.");
    return sb.ToString();
}

var targetView = Document.GetElement(new ElementId(newReferencedViewIdInt)) as View;
if (targetView == null)
{
    sb.AppendLine();
    sb.AppendLine($"Id {newReferencedViewIdInt} is not a view — nothing to re-point at.");
    return sb.ToString();
}

var repointable = rows.Where(r => r.viewId != ElementId.InvalidElementId).ToList();
sb.AppendLine();
sb.AppendLine($"Would re-point {repointable.Count} marker(s) at '{targetView.Name}' ({targetView.ViewType}, Id {targetView.Id}).");

if (dryRun)
{
    sb.AppendLine("DRY RUN — nothing changed. Set dryRun = false to apply.");
    return sb.ToString();
}

int changed = 0, refused = 0;
using (var t = new Transaction(Document, "AJ Tools - Change Referenced View"))
{
    t.Start();
    try
    {
        foreach (var (marker, viewId, viewLabel) in repointable)
        {
            try
            {
                ReferenceableViewUtils.ChangeReferencedView(Document, marker.Id, targetView.Id);
                changed++;
            }
            catch (Exception ex)
            {
                refused++;
                sb.AppendLine($"  REFUSED: marker {marker.Id} — {ex.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Re-pointed {changed} marker(s). {refused} refused.");
        sb.AppendLine("Look at a bubble — the detail and sheet numbers on it should now be the target view's.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
