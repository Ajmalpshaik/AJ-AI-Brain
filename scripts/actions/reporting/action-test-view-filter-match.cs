// ============================================================
// FRAGMENT (action) — action-test-view-filter-match.cs
// PURPOSE: Dry-run an existing View Filter against `elements` and report, per element, whether it
//          MATCHES, does not match the rule, or is N/A because the element's category was never in the
//          filter's scope — all WITHOUT applying the filter to any view. This is the "why is my filter
//          not catching these ducts?" tool.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// Model never changes — read-only, no transaction, safe to run anytime.
// GOTCHA: the N/A verdict is the whole point. A rule-based View Filter only ever applies to the
//         categories it was scoped to, so an element outside that list can NEVER match no matter how
//         well it satisfies the rule. A plain pass/fail test reports those as "fail" and sends you off
//         rewriting a rule that was already correct.
// SOURCE: ../../../knowledge/live-model/views.md § View visibility patterns
// STATUS: not yet live-verified — compile-checked only (2026-08-20). Run it on a handful of elements and
//         confirm the three verdicts read correctly before trusting it across a batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string filterName = "My View Filter";  // exact name as it appears in Visibility/Graphics > Filters
int? filterIdInt = null;               // optional — use an Element Id instead of the name; wins if set
int maxRows = 40;                      // cap the per-element listing so a big set stays readable
// ---- END INPUTS ----

Func<Element, string> SafeName = e =>
{
    try { return string.IsNullOrEmpty(e.Name) ? "(unnamed)" : e.Name; }
    catch { return "(name unavailable)"; }
};

Element filterEl = null;
if (filterIdInt.HasValue)
{
    filterEl = Document.GetElement(new ElementId(filterIdInt.Value));
}
else
{
    filterEl = new FilteredElementCollector(Document)
        .OfClass(typeof(FilterElement))
        .Cast<FilterElement>()
        .FirstOrDefault(f => string.Equals(f.Name, filterName, StringComparison.OrdinalIgnoreCase));
}

if (filterEl == null)
{
    sb.AppendLine($"Filter '{(filterIdInt.HasValue ? filterIdInt.Value.ToString() : filterName)}' not found in this model.");
    sb.AppendLine("List what actually exists with action-report-view-filters.cs, then re-run with the exact name.");
}
else if (filterEl is SelectionFilterElement)
{
    // An explicit hand-picked list, not a rule — membership is the only question there is.
    var sfe = (SelectionFilterElement)filterEl;
    var memberIds = new HashSet<ElementId>(sfe.GetElementIds());
    int nIn = 0, nOut = 0, nRows = 0;

    sb.AppendLine($"Filter '{sfe.Name}' (Id {sfe.Id}) is a SELECTION filter — an explicit list of {memberIds.Count} element(s), no rules.");
    foreach (var el in elements)
    {
        bool inSet = memberIds.Contains(el.Id);
        if (inSet) nIn++; else nOut++;
        if (nRows < maxRows)
        {
            sb.AppendLine($"  Id {el.Id} — {SafeName(el)} — {(inSet ? "IN the set" : "not in the set")}");
            nRows++;
        }
    }
    if (elements.Count > nRows) sb.AppendLine($"  ... and {elements.Count - nRows} more (raise maxRows to see them).");
    sb.AppendLine($"Result: {nIn} of {elements.Count} tested element(s) are in this selection filter, {nOut} are not.");
}
else if (filterEl is ParameterFilterElement)
{
    var pfe = (ParameterFilterElement)filterEl;
    var scopeCatIds = new HashSet<ElementId>(pfe.GetCategories());

    var scopeCatNames = scopeCatIds
        .Select(id => Document.Settings.Categories.Cast<Category>().FirstOrDefault(c => c.Id == id))
        .Where(c => c != null)
        .Select(c => c.Name)
        .OrderBy(n => n)
        .ToList();

    ElementFilter rule = null;
    string ruleNote = "";
    try
    {
        rule = pfe.GetElementFilter();
    }
    catch (Exception ex)
    {
        ruleNote = $" (rule could not be read: {ex.Message})";
    }

    sb.AppendLine($"Filter '{pfe.Name}' (Id {pfe.Id}) is a RULE-BASED View Filter{ruleNote}.");
    sb.AppendLine($"  Scoped to {scopeCatIds.Count} categor(y/ies): {(scopeCatNames.Count > 0 ? string.Join(", ", scopeCatNames) : "(none)")}");
    if (rule == null) sb.AppendLine("  It carries NO rules — every element inside those categories matches it.");

    int nMatch = 0, nNoMatch = 0, nOutOfScope = 0, nRows = 0;
    var outOfScopeCats = new Dictionary<string, int>();

    foreach (var el in elements)
    {
        string verdict;
        if (el.Category == null)
        {
            verdict = "N/A — element has no category, a View Filter can never reach it";
            nOutOfScope++;
        }
        else if (!scopeCatIds.Contains(el.Category.Id))
        {
            verdict = $"N/A — category '{el.Category.Name}' is NOT in this filter's scope";
            nOutOfScope++;
            int seen;
            outOfScopeCats.TryGetValue(el.Category.Name, out seen);
            outOfScopeCats[el.Category.Name] = seen + 1;
        }
        else if (rule == null)
        {
            verdict = "MATCH — in scope, and the filter has no rules to fail";
            nMatch++;
        }
        else if (rule.PassesFilter(el))
        {
            verdict = "MATCH";
            nMatch++;
        }
        else
        {
            verdict = "no match — in scope, but the rule is not satisfied";
            nNoMatch++;
        }

        if (nRows < maxRows)
        {
            sb.AppendLine($"  Id {el.Id} — {SafeName(el)} — {verdict}");
            nRows++;
        }
    }
    if (elements.Count > nRows) sb.AppendLine($"  ... and {elements.Count - nRows} more (raise maxRows to see them).");

    sb.AppendLine($"Result across {elements.Count} tested element(s): {nMatch} match, {nNoMatch} in scope but rule not satisfied, {nOutOfScope} out of scope.");

    if (nOutOfScope > 0)
    {
        sb.AppendLine("DIAGNOSIS: the out-of-scope ones can never be caught by this filter as written — the fix is to");
        sb.AppendLine("add their categor(y/ies) to the filter, NOT to change the rule. Categories missing from scope:");
        foreach (var kv in outOfScopeCats.OrderByDescending(k => k.Value))
            sb.AppendLine($"  - {kv.Key} ({kv.Value} element(s))");
    }
    else if (nNoMatch > 0 && nMatch == 0)
    {
        sb.AppendLine("DIAGNOSIS: every element is in scope but none satisfies the rule — the scope is right, the rule is wrong.");
        sb.AppendLine("Read the actual parameter values with report_parameters before editing the rule.");
    }
}
else
{
    sb.AppendLine($"'{filterEl.Name}' (Id {filterEl.Id}) is a {filterEl.GetType().Name}, which this fragment does not test.");
    sb.AppendLine("It handles ParameterFilterElement (rule-based) and SelectionFilterElement (explicit list) only.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
