// ============================================================
// FRAGMENT (action) — action-set-view-template-controlled-params.cs
// PURPOSE: Set which parameters a View Template controls vs. leaves free per view — the Include/Exclude
//          checkbox column in Revit's "Apply View Template" dialog / a view's V/G Overrides properties
//          row. Works on the VIEW the template is applied to (not the template itself) — each view
//          sharing a template can independently exclude different parameters from its control.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on a VIEW, not model elements.
// VERIFIED LIVE 2026-07-22 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "STATUS: not yet live-verified.", which the README had already
//          superseded. The verification was recorded there and never here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int? targetViewIdInt = null; // null = active view — must already have a View Template applied
string[] parameterNamesToExclude = { "V/G Overrides Filters" }; // e.g. "V/G Overrides Filters", "V/G Overrides Model", "View Scale", "Detail Level", "Phase", "Phase Filter" — must match the parameter's real display name
bool replaceExisting = true; // true = this list REPLACES the current excluded set; false = ADDS to whatever's already excluded
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (view.ViewTemplateId == ElementId.InvalidElementId)
{
    sb.AppendLine($"View '{view.Name}' has no View Template applied — nothing to include/exclude.");
}
else
{
    var excludeIds = new HashSet<ElementId>();
    var notFound = new List<string>();

    foreach (var name in parameterNamesToExclude)
    {
        var p = view.LookupParameter(name);
        if (p == null) { notFound.Add(name); continue; }
        excludeIds.Add(p.Id);
    }

    if (!replaceExisting)
    {
        foreach (var id in view.GetNonControlledTemplateParameterIds()) excludeIds.Add(id);
    }

    using (var t = new Transaction(Document, "AJ Tools - Set View Template Controlled Parameters"))
    {
        t.Start();
        try
        {
            view.SetNonControlledTemplateParameterIds(excludeIds.ToList());
            t.Commit();
            sb.AppendLine($"View '{view.Name}': {excludeIds.Count} parameter(s) now EXCLUDED from template control (free to differ from the template).");
            if (notFound.Count > 0) sb.AppendLine("Not found (skipped): " + string.Join(", ", notFound));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set controlled parameters — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
