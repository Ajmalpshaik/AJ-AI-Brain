// ============================================================
// FRAGMENT (creator) — create-revision-cloud.cs
// PURPOSE: Draw a rectangular Revision Cloud in a view (or on a sheet) tied to an existing project
//          Revision — the annotation half the Revision lifecycle (create/edit/delete/assign) was missing.
// PRODUCES: elements (List<Element>, the single new RevisionCloud), sb
// NOT STANDALONE — see scripts/README.md for how to compose. Revisions themselves: creators/create-revision.cs;
//          the list to pick from: action-report-revisions.cs.
// GOTCHA: the mm rectangle is in the VIEW's own plane (view coordinates from its Origin along
//         RightDirection/UpDirection) — so the same numbers work in a plan, a section, or on a sheet.
// GOTCHA: placing a cloud in a view auto-adds that view's sheet to the Revision's sheet list (Revit
//         behavior) — same interaction noted in ../../knowledge/live-model/revisions.md.
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2
//          suggestions.", which the README had already superseded. The verification was recorded there and never
//          here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;              // the view or sheet the cloud is drawn in
int revisionSequenceNumber = 1; // which project Revision the cloud belongs to (see action-report-revisions.cs)
double minXMm = 0, minYMm = 0;  // rectangle corners in the view's plane, mm
double maxXMm = 2000, maxYMm = 1000;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

var view = Document.GetElement(new ElementId(viewIdInt)) as View;
var revision = new FilteredElementCollector(Document).OfClass(typeof(Revision)).Cast<Revision>()
    .FirstOrDefault(r => r.SequenceNumber == revisionSequenceNumber);

if (view == null || view.IsTemplate) sb.AppendLine($"viewIdInt {viewIdInt} is not a usable view.");
else if (revision == null) sb.AppendLine($"No Revision with SequenceNumber {revisionSequenceNumber} — list them via action-report-revisions.cs.");
else if (maxXMm <= minXMm || maxYMm <= minYMm) sb.AppendLine("Rectangle is inverted or zero-sized — max must be greater than min on both axes.");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create Revision Cloud"))
    {
        t.Start();
        try
        {
            var o = view.Origin; var right = view.RightDirection; var up = view.UpDirection;
            Func<double, double, XYZ> pt = (x, y) => o + right * mm(x) + up * mm(y);
            var p1 = pt(minXMm, minYMm); var p2 = pt(maxXMm, minYMm);
            var p3 = pt(maxXMm, maxYMm); var p4 = pt(minXMm, maxYMm);
            var curves = new List<Curve>
            {
                Line.CreateBound(p1, p2), Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4), Line.CreateBound(p4, p1)
            };

            var cloud = RevisionCloud.Create(Document, view, revision.Id, curves);
            elements.Add(cloud);
            t.Commit();
            sb.AppendLine($"Created revision cloud (Id {cloud.Id}) in view '{view.Name}' for Revision seq {revisionSequenceNumber} ('{revision.Description}'), {maxXMm - minXMm}x{maxYMm - minYMm} mm.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create revision cloud — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
