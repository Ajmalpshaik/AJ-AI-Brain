// ============================================================
// FRAGMENT (creator) — create-callout-view.cs
// PURPOSE: Create a Callout view inside a parent view over an mm rectangle — the last of the common view
//          types create-view.cs excludes (elevations are create-room-elevations.cs).
// PRODUCES: elements (List<Element>, the new callout view), sb
// NOT STANDALONE — see scripts/README.md for how to compose. Chain action-rename-element.cs,
//          action-apply-view-template.cs or action-place-viewport-on-sheet.cs onto the result.
// GOTCHA: the callout inherits its parent's type — a callout of a floor plan is a floor plan, of a
//         section is a section. The viewFamilyTypeName input must therefore match a type valid for the
//         parent; leave it null to let Revit pick the parent's own type, which is what you want most days.
// GOTCHA: the rectangle is in MODEL coordinates (mm), not screen or paper — take the numbers from a real
//         element's bounding box (action-report-bounding-box.cs) rather than estimating them.
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int parentViewIdInt = 0;        // the view the callout bubble is drawn in
string viewFamilyTypeName = null;  // null = use the parent view's own type (usual choice)
double minXMm = 0, minYMm = 0;  // callout rectangle in MODEL coordinates, mm
double maxXMm = 5000, maxYMm = 4000;
string newName = null;          // null = keep Revit's generated name
int scaleDenominator = 0;       // 0 = inherit the parent's scale; else e.g. 20 for 1:20
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

var parent = Document.GetElement(new ElementId(parentViewIdInt)) as View;

if (parent == null || parent.IsTemplate)
{
    sb.AppendLine($"parentViewIdInt {parentViewIdInt} is not a usable view.");
}
else if (maxXMm <= minXMm || maxYMm <= minYMm)
{
    sb.AppendLine("Callout rectangle is inverted or zero-sized — max must exceed min on both axes.");
}
else
{
    ElementId vftId = parent.GetTypeId();
    if (!string.IsNullOrEmpty(viewFamilyTypeName))
    {
        var named = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.Name == viewFamilyTypeName);
        if (named == null)
            sb.AppendLine($"NOTE: ViewFamilyType '{viewFamilyTypeName}' not found — using the parent view's own type instead.");
        else vftId = named.Id;
    }

    using (var t = new Transaction(Document, "AJ Tools - Create Callout View"))
    {
        t.Start();
        try
        {
            // Z comes from the parent so the callout sits in its plane
            double z = parent.Origin != null ? parent.Origin.Z : 0;
            var p1 = new XYZ(mm(minXMm), mm(minYMm), z);
            var p2 = new XYZ(mm(maxXMm), mm(maxYMm), z);

            var callout = ViewSection.CreateCallout(Document, parent.Id, vftId, p1, p2);

            if (!string.IsNullOrEmpty(newName))
            {
                try { callout.Name = newName; } catch { } // name collision — keeps the generated name
            }
            // SCALE: on Revit 2020 a fresh callout's VIEW_SCALE parameter is READ-ONLY (measured live
            // 2026-08-07 — so is VIEW_SCALE_PULLDOWN_METRIC), because the callout inherits its parent's
            // scale. The old code guarded with `!pScale.IsReadOnly`, so the Set was SKIPPED ENTIRELY,
            // and the summary then printed "scale 1:20" from the INPUT — a claim with no API call
            // behind it at all. Report the scale the view actually has, and say plainly when the
            // requested one could not be applied.
            string scaleNote = null;
            if (scaleDenominator > 0)
            {
                var pScale = callout.get_Parameter(BuiltInParameter.VIEW_SCALE);
                if (pScale == null || pScale.IsReadOnly)
                    scaleNote = $"scale 1:{scaleDenominator} NOT applied — a callout's scale parameter is read-only on this Revit version; it follows the parent view. Change it in the Revit UI, or change the parent's scale.";
                else if (!pScale.Set(scaleDenominator))
                    scaleNote = $"scale 1:{scaleDenominator} REFUSED by Revit — left at 1:{callout.Scale}.";
            }

            elements.Add(callout);
            t.Commit();

            // Read the scale back off the view rather than echoing the request.
            sb.AppendLine($"Created callout '{callout.Name}' (Id {callout.Id}) in parent view '{parent.Name}', covering {maxXMm - minXMm}x{maxYMm - minYMm} mm, ACTUAL scale 1:{callout.Scale}{(scaleDenominator > 0 ? "" : " (inherited)")}.");
            if (scaleNote != null) sb.AppendLine("  " + scaleNote);
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create callout — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
