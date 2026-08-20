// ============================================================
// FRAGMENT (action) — action-report-view-template-status.cs
// PURPOSE: Report whether one or more views currently have a View Template applied — which template,
//          and which parameters (if any) are excluded from its control on that view. Read-only, no
//          transaction needed. The missing "check before you act" piece for the View Template group —
//          confirm status before applying/removing/adjusting one.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on VIEWS, not model elements.
// ============================================================


// Version-proof id VALUE, for the few places that need a number rather than an ElementId (testing for a
// built-in negative id, casting to BuiltInParameter). Revit 2024 renamed IntegerValue -> Value and made
// it 64-bit; naming either one directly fails to compile on the other version, so it is looked up by
// name instead. The lookup happens ONCE and is captured, so each call is a field read, not a reflection
// search - safe to use in a loop.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

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
        if (IdValue(id) < 0) return LabelUtils.GetLabelFor((BuiltInParameter)IdValue(id));
    }
    catch { }
    var pe = Document.GetElement(id) as ParameterElement;
    return pe?.Name ?? $"(Id {id})";
};

int withTemplate = 0, withoutTemplate = 0;

foreach (var v in targetViews)
{
    if (v.IsTemplate)
    {
        sb.AppendLine($"'{v.Name}' (Id {v.Id}): is itself a View Template, not a real view.");
        continue;
    }

    if (v.ViewTemplateId == ElementId.InvalidElementId)
    {
        sb.AppendLine($"'{v.Name}' (Id {v.Id}): NO View Template applied.");
        withoutTemplate++;
    }
    else
    {
        var template = Document.GetElement(v.ViewTemplateId) as View;
        string templateName = template?.Name ?? $"(Id {v.ViewTemplateId}, unresolved)";

        var excludedIds = v.GetNonControlledTemplateParameterIds();
        string excludedLabel = "none — fully controlled by the template";
        if (excludedIds != null && excludedIds.Count > 0)
        {
            excludedLabel = string.Join(", ", excludedIds.Select(resolveParamName));
        }

        sb.AppendLine($"'{v.Name}' (Id {v.Id}): View Template = '{templateName}' (Id {v.ViewTemplateId}). Excluded from control: {excludedLabel}.");
        withTemplate++;
    }
}

sb.AppendLine($"Summary: {withTemplate} view(s) with a template applied, {withoutTemplate} without, out of {targetViews.Count} checked.");
return sb.ToString();
