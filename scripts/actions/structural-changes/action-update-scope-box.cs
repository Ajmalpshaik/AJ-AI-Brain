// ============================================================
// FRAGMENT (action) — action-update-scope-box.cs
// PURPOSE: Resize an existing Scope Box (by name) to new mm box corners.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — looks the scope box up by name directly (this
//          is inherently a single-named-object operation, same shape as action-rename-phase.cs).
// CONFIRMED IMPOSSIBLE (as originally written) on Revit 2020 — LIVE-CHECKED 2026-07-22: the original
// delete-and-recreate workaround depended on `Document.Create.NewScopeBox`, which does not exist anywhere
// in the public API on this Revit version (see create-scope-box.cs's file header for the full reflection
// evidence — no Scope Box creation method exists at all). That makes the delete+recreate technique
// impossible to complete: deleting the old box would succeed, but nothing could recreate it, so a mid-way
// failure would permanently lose the Scope Box with no way back — an unacceptable risk for an already-
// uncertain fragment. This fragment NO LONGER deletes anything. It now only looks up the named box and
// reports its dependent views (still useful — same as before), then reports the resize as not achievable
// via public API on this Revit version, instead of attempting the destructive workaround.
// UNVERIFIED, NOT RULED OUT: whether an EXISTING Scope Box exposes any direct, non-destructive way to
// change its extents (e.g. a settable bounding box / location API) could not be checked this session —
// this model has 0 Scope Boxes and none can be created via API to test against (see create-scope-box.cs).
// If a Scope Box exists in a live model, inspect its Element.Location and any BuiltInParameter around its
// 6 faces/corners before assuming a direct resize is impossible — it may just be undiscovered, not absent.
// Until verified, treat true resize as a manual-only operation (drag the box's control handles, or its
// corner grips, in a plan view).
// ============================================================
// MANDATORY before running this for real: confirm the scope box name with the user first — even the
// report-only behavior below should not be assumed silently correct without checking actual dependent
// views against what the user expects.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string scopeBoxName = "Scope Box 1";
double newMinXMm = 0, newMinYMm = 0, newMinZMm = 0;
double newMaxXMm = 15000, newMaxYMm = 15000, newMaxZMm = 3000;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var existing = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
    .WhereElementIsNotElementType()
    .FirstOrDefault(e => e.Name.Equals(scopeBoxName, StringComparison.OrdinalIgnoreCase));

if (existing == null)
{
    var available = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_VolumeOfInterest).WhereElementIsNotElementType();
    sb.AppendLine($"Scope Box '{scopeBoxName}' not found. Available: {string.Join(", ", available.Select(e => e.Name))}");
}
else
{
    var dependentViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.AsElementId() == existing.Id)
        .ToList();

    sb.AppendLine($"Found Scope Box '{scopeBoxName}' (Id {existing.Id}), referenced by {dependentViews.Count} view(s): " +
        string.Join(", ", dependentViews.Select(v => v.Name)));
    sb.AppendLine($"Requested new extents ({newMinXMm},{newMinYMm},{newMinZMm}) to ({newMaxXMm},{newMaxYMm},{newMaxZMm}) mm NOT applied — Revit's public API has no Scope Box creation method on this Revit version, so the delete+recreate resize workaround this fragment used to attempt is impossible to complete safely (see CONFIRMED IMPOSSIBLE note above). Resize manually by selecting the Scope Box in a plan view and dragging its corner/edge grips, or editing its Properties.");
}
return sb.ToString();
