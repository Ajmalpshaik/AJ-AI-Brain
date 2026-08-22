// ============================================================
// FRAGMENT (action) — action-set-view-crop.cs
// PURPOSE: Set a view's crop region to fit `elements`' combined extent plus a margin, turning crop on
//          if it's off. Different from action-section-box-and-zoom.cs (that one is 3D-view-only); crop
//          applies to plan/section/elevation views too.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱ UPGRADED 2026-08-22 (harvested from the add-in's View Crop tool). Two real defects were fixed:
//
//   1. IT WORKED IN THE VIEW'S OWN FRAME, NOT THE WORLD'S — and now it does.
//      `view.CropBox` Min/Max are read in the box's OWN Transform, not in world coordinates. The old
//      version merged world-aligned bounding boxes and assigned them with no Transform at all, so on
//      any view whose crop is rotated (a rotated plan, a section, an elevation, a scope-box-derived
//      view) the crop landed somewhere else entirely. It only looked right on a plain north-up plan,
//      where the transform is near-identity — which is why it passed its own live test.
//      Now: project all EIGHT corners of every element box through `CropBox.Transform.Inverse`, take
//      min/max in the view's XY, and write back keeping the view's existing Transform and Z.
//
//   2. IT COULD SILENTLY DO NOTHING. Four things stop a crop being set, and three of them do not
//      throw — so the old catch block never fired and it reported success:
//        - the view has no crop box at all
//        - A SCOPE BOX IS ASSIGNED (`VIEWER_VOLUME_OF_INTEREST_CROP`) — the scope box owns the crop,
//          so your value is accepted and then overwritten. THE COMMONEST SILENT FAILURE.
//        - A VIEW TEMPLATE controls the crop (`VIEWER_CROP_REGION` reads back read-only)
//        - Crop is switched off — that one is fixable, so it just gets switched on
//      Each is now checked first and named in the report instead of being reported as a success.
//
// GOTCHA: an element with no bounding box is skipped, and the count of skipped ones is reported. If
//         EVERY element is skipped, nothing changes.
// GOTCHA: the Z extent is left exactly as the view had it. A crop is a plan-boundary, not a depth
//         setting — for depth use the view range or a section box, not this.
// NOTE: `targetViewIdInt` lets this crop a view that isn't open on screen.
// ✓ LIVE-VERIFIED 2026-07-22 in its pre-upgrade form (plain plan view, non-rotated crop).
// ⚠ THE UPGRADE ITSELF IS NOT YET LIVE-RUN — compile-checked on Revit 2020, 2024 and 2027. The
//   rotated/section-view case is exactly the one the old version got wrong, so check one such view by
//   eye before trusting a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double marginMm = 500;
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

var view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (elements.Count == 0)
{
    sb.AppendLine("No elements to crop to — nothing to do.");
}
else
{
    // ---- the four reasons a crop refuses, checked BEFORE doing any work ----
    string refusal = null;
    BoundingBoxXYZ currentCrop = null;

    try { currentCrop = view.CropBox; } catch { }
    if (currentCrop == null || currentCrop.Transform == null)
    {
        refusal = "this view does not expose an editable crop box";
    }
    else
    {
        var scopeBoxParam = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
        if (scopeBoxParam != null && scopeBoxParam.HasValue &&
            scopeBoxParam.AsElementId() != null && scopeBoxParam.AsElementId() != ElementId.InvalidElementId)
        {
            var sbx = Document.GetElement(scopeBoxParam.AsElementId());
            refusal = $"a SCOPE BOX ('{(sbx != null ? sbx.Name : "?")}') controls this view's crop — " +
                      "remove it from the view first, or resize the scope box instead. Setting the crop " +
                      "here would be accepted and then overwritten";
        }
        else
        {
            var cropActiveParam = view.get_Parameter(BuiltInParameter.VIEWER_CROP_REGION);
            if (view.ViewTemplateId != null && view.ViewTemplateId != ElementId.InvalidElementId &&
                cropActiveParam != null && cropActiveParam.IsReadOnly)
            {
                var tpl = Document.GetElement(view.ViewTemplateId);
                refusal = $"the VIEW TEMPLATE '{(tpl != null ? tpl.Name : "?")}' controls crop settings on " +
                          "this view — unlock Crop in the template, or detach the template";
            }
        }
    }

    if (refusal != null)
    {
        sb.AppendLine($"Cannot crop '{view.Name}': {refusal}. Nothing changed.");
    }
    else
    {
        // ---- gather the extent IN THE VIEW'S OWN FRAME ----
        Transform modelToView = currentCrop.Transform.Inverse;
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        int used = 0, skipped = 0;

        foreach (var e in elements)
        {
            var eb = e.get_BoundingBox(null);
            if (eb == null || eb.Min == null || eb.Max == null) { skipped++; continue; }

            // The element's own box may itself carry a Transform — go through it to reach world first.
            Transform boxToWorld = eb.Transform ?? Transform.Identity;
            double bx0 = Math.Min(eb.Min.X, eb.Max.X), bx1 = Math.Max(eb.Min.X, eb.Max.X);
            double by0 = Math.Min(eb.Min.Y, eb.Max.Y), by1 = Math.Max(eb.Min.Y, eb.Max.Y);
            double bz0 = Math.Min(eb.Min.Z, eb.Max.Z), bz1 = Math.Max(eb.Min.Z, eb.Max.Z);

            // ALL EIGHT corners — a rotated view sees the box's diagonal, so 2 corners is not enough.
            var corners = new XYZ[] {
                new XYZ(bx0, by0, bz0), new XYZ(bx1, by0, bz0), new XYZ(bx1, by1, bz0), new XYZ(bx0, by1, bz0),
                new XYZ(bx0, by0, bz1), new XYZ(bx1, by0, bz1), new XYZ(bx1, by1, bz1), new XYZ(bx0, by1, bz1)
            };

            bool any = false;
            foreach (var c in corners)
            {
                XYZ local = modelToView.OfPoint(boxToWorld.OfPoint(c));
                if (local == null || double.IsNaN(local.X) || double.IsNaN(local.Y) ||
                    double.IsInfinity(local.X) || double.IsInfinity(local.Y)) continue;

                if (local.X < minX) minX = local.X;
                if (local.Y < minY) minY = local.Y;
                if (local.X > maxX) maxX = local.X;
                if (local.Y > maxY) maxY = local.Y;
                any = true;
            }

            if (any) used++; else skipped++;
        }

        if (used == 0 || minX > maxX || minY > maxY)
        {
            sb.AppendLine($"None of the {elements.Count} element(s) gave a usable bounding box — nothing changed.");
        }
        else
        {
            double marginFt = marginMm / 304.8;
            minX -= marginFt; minY -= marginFt;
            maxX += marginFt; maxY += marginFt;

            using (var t = new Transaction(Document, "AJ Tools - Set View Crop"))
            {
                t.Start();
                try
                {
                    if (!view.CropBoxActive) view.CropBoxActive = true;
                    view.CropBoxVisible = true;

                    // Keep the view's OWN transform and Z — only the plan extent changes.
                    view.CropBox = new BoundingBoxXYZ
                    {
                        Transform = currentCrop.Transform,
                        Min = new XYZ(minX, minY, currentCrop.Min.Z),
                        Max = new XYZ(maxX, maxY, currentCrop.Max.Z)
                    };
                    t.Commit();

                    sb.AppendLine($"Cropped view '{view.Name}' to {used} element(s) + {marginMm}mm margin " +
                                  $"({Math.Round((maxX - minX) * 304.8)} x {Math.Round((maxY - minY) * 304.8)} mm in the view's own frame)" +
                                  (skipped > 0 ? $"; {skipped} element(s) had no usable bounding box and were ignored." : "."));
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to set view crop — rolled back, nothing changed. Reason: {ex.Message} (this view type may not support cropping).");
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
