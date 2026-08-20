// ============================================================
// FRAGMENT (action) — action-create-view-filter.cs
// PURPOSE: Create a Revit VIEW FILTER (ParameterFilterElement) — Revit's own Visibility/Graphics >
//          Filters tab rule-based mechanism, NOT this repo's `filters/` folder. A View Filter is a
//          persistent document object: once created, it automatically colors/hides every matching
//          element — now AND anything added later — in whichever view(s) it's applied to, with no
//          script re-run needed. One condition per filter (parameter name + comparison + value); for a
//          compound AND/OR rule, build it directly in Revit's Filters dialog instead.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Pair with action-apply-view-filter.cs to actually put it on a view
//          with a color/visibility, and action-remove-view-filter.cs to take it back off.
// STATUS: not yet live-verified — ParameterFilterRuleFactory's exact overload set should be confirmed
//         against the real installed API before trusting a batch of filters built from this.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "MEP_System Type/CDP"; // View Filters can be folder-organized with "/" in the name
BuiltInCategory[] categoryScope = { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting };
string parameterName = "System Type"; // the parameter the rule tests — read off a sample element of categoryScope[0]
// Every ParameterFilterRuleFactory rule kind Revit actually offers in the Filters dialog's "matches"
// dropdown — text: "contains" | "notcontains" | "beginswith" | "notbeginswith" | "endswith" |
// "notendswith" | "equals" | "notequals" — numeric: "eq" | "noteq" | "gt" | "gte" | "lt" | "lte" —
// value presence (ignores matchTextValue/matchNumericValue entirely): "hasvalue" | "hasnovalue"
string matchMode = "contains";
string matchTextValue = "CDP"; // used for text matchModes
double matchNumericValue = 0; // used for numeric matchModes
bool caseSensitive = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var categoryIds = categoryScope.Select(c => new ElementId(c)).ToList();
var sampleCategory = categoryScope.FirstOrDefault();
var sampleElement = new FilteredElementCollector(Document).OfCategory(sampleCategory).WhereElementIsNotElementType().FirstOrDefault();

if (sampleElement == null)
{
    sb.AppendLine($"No element of category {sampleCategory} found to read the '{parameterName}' parameter's Id from — place at least one, or set categoryScope[0] to a category that has instances.");
}
else
{
    var sampleParam = sampleElement.LookupParameter(parameterName);
    if (sampleParam == null)
    {
        sb.AppendLine($"Parameter '{parameterName}' not found on a sample {sampleCategory} element — check the exact parameter name.");
    }
    else
    {
        ElementId paramId = sampleParam.Id;
        FilterRule rule = null;

        try
        {
            switch (matchMode)
            {
                // value presence — no comparison value needed at all
                case "hasvalue": rule = ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId); break;
                case "hasnovalue": rule = ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId); break;
                // numeric
                case "eq": rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, matchNumericValue, 1e-6); break;
                case "noteq": rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, matchNumericValue, 1e-6); break;
                case "gt": rule = ParameterFilterRuleFactory.CreateGreaterRule(paramId, matchNumericValue, 1e-6); break;
                case "gte": rule = ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, matchNumericValue, 1e-6); break;
                case "lt": rule = ParameterFilterRuleFactory.CreateLessRule(paramId, matchNumericValue, 1e-6); break;
                case "lte": rule = ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, matchNumericValue, 1e-6); break;
                // text
                case "equals": rule = ParameterFilterRuleFactory.CreateEqualsRule(paramId, matchTextValue, caseSensitive); break;
                case "notequals": rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, matchTextValue, caseSensitive); break;
                case "beginswith": rule = ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, matchTextValue, caseSensitive); break;
                case "notbeginswith": rule = ParameterFilterRuleFactory.CreateNotBeginsWithRule(paramId, matchTextValue, caseSensitive); break;
                case "endswith": rule = ParameterFilterRuleFactory.CreateEndsWithRule(paramId, matchTextValue, caseSensitive); break;
                case "notendswith": rule = ParameterFilterRuleFactory.CreateNotEndsWithRule(paramId, matchTextValue, caseSensitive); break;
                case "notcontains": rule = ParameterFilterRuleFactory.CreateNotContainsRule(paramId, matchTextValue, caseSensitive); break;
                default: rule = ParameterFilterRuleFactory.CreateContainsRule(paramId, matchTextValue, caseSensitive); break; // "contains"
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"FAILED to build the filter rule — check matchMode matches the parameter's real storage type (text vs number). Reason: {ex.Message}");
        }

        if (rule != null)
        {
            var elemFilter = new ElementParameterFilter(rule);

            using (var t = new Transaction(Document, "AJ Tools - Create View Filter"))
            {
                t.Start();
                try
                {
                    var existing = new FilteredElementCollector(Document).OfClass(typeof(ParameterFilterElement))
                        .Cast<ParameterFilterElement>()
                        .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

                    var numericModes = new HashSet<string> { "eq", "noteq", "gt", "gte", "lt", "lte" };
                    var noValueModes = new HashSet<string> { "hasvalue", "hasnovalue" };
                    string ruleLabel = noValueModes.Contains(matchMode) ? "(no value needed)"
                        : numericModes.Contains(matchMode) ? matchNumericValue.ToString()
                        : matchTextValue;

                    if (existing != null)
                    {
                        existing.SetCategories(categoryIds);
                        existing.SetElementFilter(elemFilter);
                        t.Commit();
                        sb.AppendLine($"Updated existing View Filter '{filterName}' (Id {existing.Id}) — rule: {parameterName} {matchMode} {ruleLabel}.");
                    }
                    else
                    {
                        var pfe = ParameterFilterElement.Create(Document, filterName, categoryIds, elemFilter);
                        t.Commit();
                        sb.AppendLine($"Created View Filter '{filterName}' (Id {pfe.Id}) — rule: {parameterName} {matchMode} {ruleLabel}. Not yet applied to any view — run action-apply-view-filter.cs next.");
                    }
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to create/update the View Filter — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
return sb.ToString();
