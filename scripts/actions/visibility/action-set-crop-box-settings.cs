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
                try { view.AnnotationCropActive = annotationCropActive.Value; annoSet++; }
                catch (Exception ex) { failures.Add($"'{view.Name}' AnnotationCropActive: {ex.Message}"); }
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
