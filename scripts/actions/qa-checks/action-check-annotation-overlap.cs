// ============================================================
// FRAGMENT (action) — action-check-annotation-overlap.cs
// PURPOSE: Find the annotation in a view that PRINTS ON TOP OF OTHER ANNOTATION — tags over tags, text
//          over dimensions, a symbol buried under a keynote. The drawing-issue check that catches what
//          nobody sees on screen at 200% zoom and everybody sees on the printed A1.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it sweeps one whole view, because
//          "which annotation overlaps" is only answerable against everything in the view, not a subset.
//          Read-only. The model never changes.
//
// ✱✱ IT MEASURES IN PAPER MILLIMETRES, WHICH IS THE ONLY UNIT THIS QUESTION HAS. Whether two tags clash
//    depends entirely on the scale the view prints at — the same two tags are clear at 1:50 and merged at
//    1:200. Every gap here is converted to paper mm using the view's own scale, so the answer is what
//    will actually come out of the plotter rather than what the model happens to look like.
//
// ✱✱ TOUCHING IS NOT OVERLAPPING, AND THE THRESHOLD IS AN INPUT. Two tags whose boxes share an edge are
//    legible; two that share half their area are not. `minOverlapMm` is how much shared paper area (in mm
//    on the short side of the intersection) counts as a real collision, so the report is a list of things
//    to fix rather than several hundred near-misses.
//
// ✱✱ A BOUNDING BOX IS BIGGER THAN THE INK. A tag's box is a rectangle around text that mostly is not
//    there — two boxes can overlap while the visible characters do not touch. This over-reports slightly
//    and says so, because the opposite error (missing a real collision) costs a reissued drawing and this
//    one costs thirty seconds of looking.
//
// GOTCHA: DIMENSIONS ARE INCLUDED BUT THEIR BOX IS THE WHOLE STRING, witness lines and all. A dimension
//         will therefore appear to overlap most things inside its own extent. `includeDimensions` is
//         separate for that reason — turn it on when chasing a specific problem, off for a routine sweep.
// GOTCHA: IT CANNOT SEE THE VIEWPORT'S OTHER CONTENTS. Annotation overlapping a MODEL line, a hatch or a
//         linked view's graphics is not found — this compares annotation against annotation.
// GOTCHA: A VIEW ON SEVERAL SHEETS prints at whatever each viewport's scale is. This uses the VIEW's
//         scale, which is the usual case; a view placed at a different scale needs a second look.
// RELATED: action-auto-arrange-tags.cs (fix overlapping TAGS by nudging them apart),
//          action-arrange-tags-to-view-edges.cs (the answer for a genuinely over-full view),
//          action-check-unannotated-elements.cs (the opposite problem — missing annotation).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Compare its top few findings against the
//   printed sheet before trusting the whole list.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId viewId = null;             // null = the active view
double minOverlapMm = 1.0;           // shared paper mm (short side) before it counts as a collision
bool includeTags = true;
bool includeTextNotes = true;
bool includeDimensions = false;      // a dimension's box is the whole string — noisy in a routine sweep
bool includeGenericAnnotations = true;
bool includeKeynotes = true;
int maxReportedRows = 60;
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;

var view = viewId != null ? Document.GetElement(viewId) as View : Document.ActiveView;
if (view == null) { sb.AppendLine("STOP: no view resolved."); return sb.ToString(); }
if (view.IsTemplate) { sb.AppendLine($"STOP: '{view.Name}' is a view template — it has no annotation of its own."); return sb.ToString(); }

int scale = view.Scale <= 0 ? 100 : view.Scale;
// model feet -> paper mm
Func<double, double> toPaperMm = ft => ToMm(ft) / scale;

// ---- collect the annotation in this view ----
var annotations = new List<(Element El, string Kind, string Label)>();

Action<IEnumerable<Element>, string> add = (list, kind) =>
{
    foreach (var e in list) annotations.Add((e, kind, ""));
};

if (includeTags)
    add(new FilteredElementCollector(Document, view.Id).OfClass(typeof(IndependentTag)).ToList(), "Tag");
if (includeTextNotes)
    add(new FilteredElementCollector(Document, view.Id).OfClass(typeof(TextNote)).ToList(), "Text");
if (includeDimensions)
    add(new FilteredElementCollector(Document, view.Id).OfClass(typeof(Dimension)).ToList(), "Dimension");
if (includeGenericAnnotations)
    add(new FilteredElementCollector(Document, view.Id)
        .OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsNotElementType().ToList(), "Symbol");
if (includeKeynotes)
    add(new FilteredElementCollector(Document, view.Id)
        .OfCategory(BuiltInCategory.OST_KeynoteTags).WhereElementIsNotElementType().ToList(), "Keynote");

// Room/space tags are IndependentTag subclasses in some versions and separate classes in others; pick
// them up by category so the sweep is the same on every Revit.
if (includeTags)
{
    foreach (var cat in new[] { BuiltInCategory.OST_RoomTags, BuiltInCategory.OST_MEPSpaceTags, BuiltInCategory.OST_AreaTags })
    {
        try
        {
            var extra = new FilteredElementCollector(Document, view.Id)
                .OfCategory(cat).WhereElementIsNotElementType().ToList();
            foreach (var e in extra)
                if (!annotations.Any(a => a.El.Id == e.Id)) annotations.Add((e, "Tag", ""));
        }
        catch { }
    }
}

sb.AppendLine($"ANNOTATION OVERLAP — view '{view.Name}' at 1:{scale}");
sb.AppendLine($"Annotation elements examined: {annotations.Count}" +
              (includeDimensions ? "" : "   (dimensions excluded — set includeDimensions = true to include them)"));

if (annotations.Count < 2)
{
    sb.AppendLine("Fewer than two annotation elements — nothing can overlap.");
    return sb.ToString();
}

// ---- boxes ----
var boxed = new List<(Element El, string Kind, BoundingBoxXYZ Box, string Text)>();
int noBox = 0;

Func<Element, string> describe = el =>
{
    var tn = el as TextNote;
    if (tn != null)
    {
        var t = (tn.Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return t.Length > 40 ? t.Substring(0, 40) + "..." : t;
    }
    var te = Document.GetElement(el.GetTypeId());
    return te != null ? te.Name : (el.Name ?? "");
};

foreach (var a in annotations)
{
    BoundingBoxXYZ bb = null;
    try { bb = a.El.get_BoundingBox(view); } catch { }
    if (bb == null) { noBox++; continue; }
    boxed.Add((a.El, a.Kind, bb, describe(a.El)));
}
if (noBox > 0) sb.AppendLine($"NOTE: {noBox} annotation element(s) reported no bounding box in this view and were not checked.");
sb.AppendLine();

// ---- pairwise ----
var hits = new List<(Element A, string AK, string AT, Element B, string BK, string BT, double OverlapMm, double AreaPct)>();

for (int i = 0; i < boxed.Count; i++)
{
    for (int j = i + 1; j < boxed.Count; j++)
    {
        var A = boxed[i]; var B = boxed[j];

        double ox = Math.Min(A.Box.Max.X, B.Box.Max.X) - Math.Max(A.Box.Min.X, B.Box.Min.X);
        double oy = Math.Min(A.Box.Max.Y, B.Box.Max.Y) - Math.Max(A.Box.Min.Y, B.Box.Min.Y);
        if (ox <= 0 || oy <= 0) continue;

        double shortSideMm = Math.Min(toPaperMm(ox), toPaperMm(oy));
        if (shortSideMm < minOverlapMm) continue;

        // How much of the SMALLER annotation is buried — the number that says whether it is readable.
        double areaA = Math.Max((A.Box.Max.X - A.Box.Min.X) * (A.Box.Max.Y - A.Box.Min.Y), 1e-9);
        double areaB = Math.Max((B.Box.Max.X - B.Box.Min.X) * (B.Box.Max.Y - B.Box.Min.Y), 1e-9);
        double overlapArea = ox * oy;
        double pct = overlapArea / Math.Min(areaA, areaB) * 100.0;

        hits.Add((A.El, A.Kind, A.Text, B.El, B.Kind, B.Text, shortSideMm, Math.Min(pct, 100)));
    }
}

sb.AppendLine($"OVERLAPPING PAIRS: {hits.Count}   (threshold {minOverlapMm:F1} mm on paper)");
sb.AppendLine();

if (hits.Count == 0)
{
    sb.AppendLine("CLEAR — no annotation overlaps another by more than the threshold on this view.");
    return sb.ToString();
}

sb.AppendLine("| Buried % | Overlap mm | A | A kind | A text | B | B kind | B text |");
sb.AppendLine("|---|---|---|---|---|---|---|---|");
foreach (var h in hits.OrderByDescending(h => h.AreaPct).Take(maxReportedRows))
    sb.AppendLine($"| {h.AreaPct:F0} | {h.OverlapMm:F1} | {h.A.Id} | {h.AK} | {h.AT} | {h.B.Id} | {h.BK} | {h.BT} |");
if (hits.Count > maxReportedRows)
    sb.AppendLine($"\n... and {hits.Count - maxReportedRows} more (raise maxReportedRows to see them).");

sb.AppendLine();
int badlyBuried = hits.Count(h => h.AreaPct >= 50);
sb.AppendLine($"At least half hidden: {badlyBuried} pair(s) — these are the ones that will read as a smudge on the print.");

sb.AppendLine();
sb.AppendLine("Worst offenders — annotation involved in the most collisions:");
var counts = new Dictionary<ElementId, int>();
foreach (var h in hits)
{
    counts[h.A.Id] = counts.ContainsKey(h.A.Id) ? counts[h.A.Id] + 1 : 1;
    counts[h.B.Id] = counts.ContainsKey(h.B.Id) ? counts[h.B.Id] + 1 : 1;
}
foreach (var kv in counts.OrderByDescending(k => k.Value).Take(10))
    sb.AppendLine($"  {kv.Key}: in {kv.Value} collision(s)");

sb.AppendLine();
sb.AppendLine("If most of these are tag-on-tag, action-auto-arrange-tags.cs will push them apart. If the view is simply too full, action-arrange-tags-to-view-edges.cs is the honest fix.");

return sb.ToString();
