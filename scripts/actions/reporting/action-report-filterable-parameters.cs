// ============================================================
// FRAGMENT (action) — action-report-filterable-parameters.cs
// PURPOSE: Before you build a View Filter — which of these categories can Revit filter at all, and
//          which parameters are legal to filter on across ALL of them at once. Answers "why won't my
//          filter take that parameter", and gives the exact list to choose from. Read-only.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`,
//          ends with its own `return`). It works entirely off the CATEGORIES, with no elements needed.
// READ-ONLY — opens no transaction, changes nothing.
// RELATED: action-create-view-filter.cs (build one), action-apply-view-filter.cs (put it on a view),
//          action-audit-view-filters.cs (what is already there).
//
// ✱✱ WHY THIS EXISTS — TWO REAL WAYS `ParameterFilterElement.Create` GOES WRONG, AND BOTH ARE ASKABLE
//    IN ADVANCE. `ParameterFilterUtilities` was used by NO fragment here.
//      1. A CATEGORY THAT CANNOT BE FILTERED. `Create` throws if any category in the list is not
//         filterable — links, some annotation and view-specific categories. One bad category and the
//         whole call fails, with an exception that names none of them.
//      2. A PARAMETER THAT IS NOT FILTERABLE FOR THAT CATEGORY SET. A parameter can be present on the
//         element and still not be available to a filter, and the legal set SHRINKS as you add
//         categories — it is the parameters they have IN COMMON. Two categories that each allow a
//         parameter may allow nothing together.
//
// ✱✱ AND IT REMOVES A DEPENDENCE ON THERE BEING AN ELEMENT PLACED. `action-create-view-filter.cs`
//    resolves its parameter's Id by reading a SAMPLE ELEMENT of the first category — its own header
//    says so, and it gives up when the category is empty. `GetFilterableParametersInCommon` answers
//    from the categories alone, so a filter can be set up for a category before anything of that kind
//    exists in the model. That is the normal case at the start of a job.
//
// GOTCHA: "filterable" is about the FILTER MECHANISM, not about whether a value is set. A parameter
//         can be listed here and be empty on every element — the filter is still legal, it just
//         matches nothing.
// GOTCHA: the answer depends on the exact category SET. Adding one more category can remove parameters
//         from the list. Both the per-category and the in-common lists are shown so it is obvious
//         which category is doing the narrowing.
// GOTCHA: a parameter listed here may still be rejected for a particular RULE KIND — a text rule on a
//         numeric parameter, for instance. This answers "may I filter on it", not "with which
//         comparison". `action-create-view-filter.cs` handles the rule kinds.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only,
//    and the fastest check is to compare its list against Revit's own Filters dialog for the same
//    categories — they should match exactly.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory[] categoryScope = { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting };
string lookingFor = "";        // "" = list everything; else only parameters whose name contains this
int maxParametersListed = 120;
bool showPerCategory = true;   // also list each category on its own, to see which one narrows the set
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (categoryScope.Length == 0)
{
    sb.AppendLine("categoryScope is empty — name at least one category.");
    return sb.ToString();
}

// ---------- which of these categories can be filtered at all ----------
ICollection<ElementId> allFilterable = null;
try { allFilterable = ParameterFilterUtilities.GetAllFilterableCategories(); } catch { }

var wanted = new List<ElementId>();
var rejected = new List<string>();
foreach (var bic in categoryScope)
{
    var id = new ElementId(bic);
    bool ok = allFilterable == null || allFilterable.Contains(id);
    if (ok) wanted.Add(id);
    else rejected.Add(bic.ToString());
}

// RemoveUnfilterableCategories is Revit's own answer to the same question, and it is the one that
// matches what Create will accept. It is asked as well as the membership test above, because the two
// can disagree on edge cases and the stricter answer is the safe one.
ICollection<ElementId> accepted = wanted;
try
{
    var copy = new List<ElementId>(wanted);
    accepted = ParameterFilterUtilities.RemoveUnfilterableCategories(copy);
}
catch { }

Func<ElementId, string> catName = id =>
{
    try
    {
        foreach (Category c in Document.Settings.Categories)
            if (c.Id == id) return c.Name;
    }
    catch { }
    return id.ToString();
};

sb.AppendLine($"FILTERABLE PARAMETERS — {categoryScope.Length} category(ies) asked for");
sb.AppendLine();
sb.AppendLine("CATEGORIES");
foreach (var bic in categoryScope)
{
    var id = new ElementId(bic);
    bool ok = accepted.Contains(id);
    sb.AppendLine($"  {(ok ? "OK      " : "REJECTED")}  {bic} ({catName(id)})");
}
if (rejected.Count > 0 || accepted.Count != categoryScope.Length)
{
    sb.AppendLine();
    sb.AppendLine("*** AT LEAST ONE CATEGORY CANNOT BE FILTERED. `ParameterFilterElement.Create` throws if any");
    sb.AppendLine("    category in the list is unfilterable — remove the rejected ones above before calling it.");
}
if (accepted.Count == 0)
{
    sb.AppendLine();
    sb.AppendLine("None of these categories is filterable, so no filter can be built for this set at all.");
    return sb.ToString();
}

// ---------- the parameters legal across all of them ----------
ICollection<ElementId> common = null;
try { common = ParameterFilterUtilities.GetFilterableParametersInCommon(Document, accepted); }
catch (Exception ex)
{
    sb.AppendLine();
    sb.AppendLine("Could not read the common parameter set: " + ex.Message);
    return sb.ToString();
}

// A parameter Id is either a BuiltInParameter (a negative, "system" id) or a project/shared parameter
// element. Both are named the same way here: the element's name when there is one, the enum's name
// when there is not. Never read the raw number — see knowledge/live-model/element-identity.md.
// The BuiltInParameter enum has over three THOUSAND values, so a linear scan per parameter is
// millions of comparisons for a list of a few hundred. The reverse map is built once, lazily, and only
// if a system parameter actually turns up.
Dictionary<ElementId, string> systemNames = null;
Action buildSystemNames = () =>
{
    if (systemNames != null) return;
    systemNames = new Dictionary<ElementId, string>();
    try
    {
        foreach (BuiltInParameter bp in Enum.GetValues(typeof(BuiltInParameter)))
        {
            ElementId id;
            try { id = new ElementId(bp); } catch { continue; }
            if (systemNames.ContainsKey(id)) continue;
            string label = null;
            try { label = LabelUtils.GetLabelFor(bp); } catch { }
            systemNames[id] = string.IsNullOrEmpty(label) ? bp.ToString() : $"{label}  [{bp}]";
        }
    }
    catch { }
};

Func<ElementId, string> paramName = pid =>
{
    try
    {
        var pe = Document.GetElement(pid);
        if (pe != null && !string.IsNullOrEmpty(pe.Name)) return pe.Name;
    }
    catch { }
    buildSystemNames();
    string found;
    if (systemNames.TryGetValue(pid, out found)) return found;
    return pid.ToString();
};

var names = new List<string>();
foreach (var pid in common)
{
    string n = paramName(pid);
    if (lookingFor.Length > 0 && n.IndexOf(lookingFor, StringComparison.OrdinalIgnoreCase) < 0) continue;
    names.Add(n);
}
names.Sort(StringComparer.OrdinalIgnoreCase);

sb.AppendLine();
sb.AppendLine($"PARAMETERS FILTERABLE ACROSS ALL {accepted.Count} ACCEPTED CATEGORY(IES): {common.Count} in total"
    + (lookingFor.Length > 0 ? $", {names.Count} matching \"{lookingFor}\"" : ""));
foreach (var n in names.Take(maxParametersListed)) sb.AppendLine("  " + n);
if (names.Count > maxParametersListed)
    sb.AppendLine($"  ... {names.Count - maxParametersListed} more (raise maxParametersListed, or set lookingFor).");
if (names.Count == 0 && lookingFor.Length > 0)
    sb.AppendLine($"  nothing matches \"{lookingFor}\" — the parameter exists on the elements but is NOT filterable for this category set, or is spelt differently.");

// ---------- where the narrowing happens ----------
if (showPerCategory && accepted.Count > 1)
{
    sb.AppendLine();
    sb.AppendLine("PER CATEGORY — how many each one allows on its own. A category with a much smaller number");
    sb.AppendLine("is the one narrowing the set above; drop it into its own filter if you need its parameters.");
    sb.AppendLine("Category | Filterable parameters alone");
    sb.AppendLine("--- | ---:");
    foreach (var id in accepted)
    {
        int n = -1;
        try { n = ParameterFilterUtilities.GetFilterableParametersInCommon(Document, new List<ElementId> { id }).Count; }
        catch { }
        sb.AppendLine($"{catName(id)} | {(n < 0 ? "(unreadable)" : n.ToString())}");
    }
}

sb.AppendLine();
sb.AppendLine("Take a name from the list above straight into action-create-view-filter.cs. A parameter that is");
sb.AppendLine("NOT on this list will be rejected by Revit however it is spelt — that is the check this saves.");
return sb.ToString();
