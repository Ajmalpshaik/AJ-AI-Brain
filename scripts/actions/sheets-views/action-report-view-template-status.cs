// ============================================================
// FRAGMENT (action) — action-report-view-template-status.cs
// PURPOSE: Report whether one or more views currently have a View Template applied — which template,
//          and which parameters (if any) are excluded from its control on that view. Read-only, no
//          transaction needed. The missing "check before you act" piece for the View Template group —
//          confirm status before applying/removing/adjusting one.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on VIEWS, not model elements.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int[] targetViewIdInts = { }; // empty = active view only (unless checkAllViews is true)
bool checkAllViews = false; // true = report every real view in the project instead of just targetViewIdInts/active view
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

List<View> targetViews;
if (checkAllViews)
{
    targetViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => !v.IsTemplate && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.DrawingSheet)
        .OrderBy(v => v.Name)
        .ToList();
}
else
{
    targetViews = targetViewIdInts.Length > 0
        ? targetViewIdInts.Select(id => Document.GetElement(new ElementId(id)) as View).Where(v => v != null).ToList()
        : new List<View> { Document.ActiveView };
}

Func<ElementId, string> resolveParamName = id =>
{
    try
    {
        if (id.IntegerValue < 0) return LabelUtils.GetLabelFor((BuiltInParameter)id.IntegerValue);
    }
    catch { }
    var pe = Document.GetElement(id) as ParameterElement;
    return pe?.Name ?? $"(Id {id.IntegerValue})";
};

int withTemplate = 0, withoutTemplate = 0;

foreach (var v in targetViews)
{
    if (v.IsTemplate)
    {
        sb.AppendLine($"'{v.Name}' (Id {v.Id.IntegerValue}): is itself a View Template, not a real view.");
        continue;
    }

    if (v.ViewTemplateId == ElementId.InvalidElementId)
    {
        sb.AppendLine($"'{v.Name}' (Id {v.Id.IntegerValue}): NO View Template applied.");
        withoutTemplate++;
    }
    else
    {
        var template = Document.GetElement(v.ViewTemplateId) as View;
        string templateName = template?.Name ?? $"(Id {v.ViewTemplateId.IntegerValue}, unresolved)";

        var excludedIds = v.GetNonControlledTemplateParameterIds();
        string excludedLabel = "none — fully controlled by the template";
        if (excludedIds != null && excludedIds.Count > 0)
        {
            excludedLabel = string.Join(", ", excludedIds.Select(resolveParamName));
        }

        sb.AppendLine($"'{v.Name}' (Id {v.Id.IntegerValue}): View Template = '{templateName}' (Id {v.ViewTemplateId.IntegerValue}). Excluded from control: {excludedLabel}.");
        withTemplate++;
    }
}

sb.AppendLine($"Summary: {withTemplate} view(s) with a template applied, {withoutTemplate} without, out of {targetViews.Count} checked.");
return sb.ToString();
