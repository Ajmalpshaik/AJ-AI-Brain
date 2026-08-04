// ============================================================
// FRAGMENT (action) — action-set-crop-box-settings.cs
// PURPOSE: Turn Crop Region on/off, its boundary line visibility on/off, and/or Annotation Crop on/off
//          across every View in `elements` — independent flag toggles, NOT resizing (for resizing/fitting
//          the crop to a set of elements, use action-set-view-crop.cs instead; the two compose fine
//          together — e.g. this to turn crop on first, that to size it).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above
//          (e.g. filter-by-views.cs).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: annotationCropActive only has a visible effect when cropBoxActive is also true — Revit ignores
//         it otherwise; this still sets it if asked, it just won't do anything until crop is on too.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool? cropBoxActive = true;         // turn cropping on/off; null = don't change
bool? cropBoxVisible = true;        // show/hide the crop boundary LINE (only matters while active); null = don't change
bool? annotationCropActive = null;  // also crop tags/dimensions/annotation, not just model geometry; null = don't change
// ---- END INPUTS ----

int activeSet = 0, visibleSet = 0, annoSet = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Crop Box Settings"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            var view = el as View;
            if (view == null) { skipped++; continue; }

            if (cropBoxActive.HasValue)
            {
                try { view.CropBoxActive = cropBoxActive.Value; activeSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' CropBoxActive: {ex.Message}"); }
            }
            if (cropBoxVisible.HasValue)
            {
                try { view.CropBoxVisible = cropBoxVisible.Value; visibleSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' CropBoxVisible: {ex.Message}"); }
            }
            if (annotationCropActive.HasValue)
            {
                // View.AnnotationCropActive does NOT exist on Revit 2020 — the property was added later.
                // The try/catch that used to wrap it was worthless: a missing member is a COMPILE error
                // (CS1061), not a runtime exception, so this fragment never built at all. The 2020 route
                // is the built-in parameter, which is what the UI checkbox drives anyway.
                // Caught by tools\verify-fragments-compile.ps1 on 2026-08-04.
                try
                {
                    var annoParam = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                    if (annoParam == null || annoParam.IsReadOnly)
                        failures.Add($"'{view.Name}' AnnotationCrop: parameter missing or read-only on this view type");
                    else { annoParam.Set(annotationCropActive.Value ? 1 : 0); annoSet++; }
                }
                catch (Exception ex) { failures.Add($"'{view.Name}' AnnotationCrop: {ex.Message}"); }
            }
        }
        t.Commit();
        sb.AppendLine($"CropBoxActive set on {activeSet}, CropBoxVisible set on {visibleSet}, AnnotationCropActive set on {annoSet} view(s), {skipped} non-View element(s) skipped.");
        if (failures.Count > 0)
            sb.AppendLine("Failures: " + string.Join("; ", failures.Take(10)) + (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set crop box settings — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
