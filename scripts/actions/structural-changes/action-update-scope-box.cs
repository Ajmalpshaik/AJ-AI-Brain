// ============================================================
// FRAGMENT (action) — action-update-scope-box.cs
// PURPOSE: Resize an existing Scope Box (by name) to new mm box corners. Revit's public API has no
//          documented direct "resize this Scope Box's extents" setter — this works around that by
//          recording which views currently have it assigned as their Scope Box, DELETING the old element,
//          creating a new one at the new extents under the SAME name, then re-assigning it to every view
//          that had the old one. Functionally equivalent to a resize from the outside, but the element's
//          own Id changes — anything referencing the old Id directly (not by name) needs re-resolving
//          afterward.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — looks the scope box up by name directly (this
//          is inherently a single-named-object operation, same shape as action-rename-phase.cs).
// GOTCHA: same technique/honesty caveat as action-set-design-option.cs's copy-while-active workaround —
//         a genuine API limitation being worked around, not a guess.
// NOT YET LIVE-VERIFIED — this workaround technique has not been run against a real model this session.
// ============================================================
// MANDATORY before running this for real: confirm the scope box name and new extents with the user first
// — this deletes and recreates the element, it isn't a simple in-place edit.

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
    var dependentViewIds = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.AsElementId() == existing.Id)
        .Select(v => v.Id)
        .ToList();

    using (var t = new Transaction(Document, "AJ Tools - Update Scope Box"))
    {
        t.Start();
        try
        {
            Document.Delete(existing.Id);

            Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
            var p1 = new XYZ(mm(newMinXMm), mm(newMinYMm), mm(newMinZMm));
            var p2 = new XYZ(mm(newMaxXMm), mm(newMaxYMm), mm(newMaxZMm));
            var newBox = Document.Create.NewScopeBox(p1, p2);
            newBox.Name = scopeBoxName;

            int reattached = 0;
            foreach (var viewId in dependentViewIds)
            {
                var v = Document.GetElement(viewId) as View;
                try { v?.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.Set(newBox.Id); reattached++; }
                catch { }
            }

            t.Commit();
            sb.AppendLine($"Updated Scope Box '{scopeBoxName}' — recreated with new extents (new Id {newBox.Id.IntegerValue}), re-attached to {reattached}/{dependentViewIds.Count} view(s) that referenced it.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to update scope box — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
