// ============================================================
// FRAGMENT (filter) — filter-by-view-templates.cs
// PURPOSE: View Templates themselves (IsTemplate == true) — the gap every other View Template fragment
//          had to work around with its own by-name lookup instead of composing through the normal
//          filter+action system. Produces `elements` (each really a View with IsTemplate == true) so ANY
//          existing action (action-rename-element.cs, action-report-parameters.cs,
//          action-delete-elements.cs, ...) can operate on templates directly, the same way every other
//          category does. Optional name-contains AND usage mode:
//            "all"    — every View Template in the document
//            "used"   — applied to at least one real (non-template) view
//            "unused" — applied to none — the standard purge-candidate set; compose with
//                       actions/structural-changes/action-delete-elements.cs to actually purge them (see
//                       scripts/examples/purge-unused-view-templates.cs for the fully-assembled version).
// PRODUCES: elements (List<Element>, each one really a View — cast back with `as View` in the action),
//           sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string nameContains = ""; // "" = every template; else substring match on the template's Name, case-insensitive
string usage = "all"; // "all" | "used" | "unused"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(View))
    .Cast<View>()
    .Where(v => v.IsTemplate)
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(v => v.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

if (usage == "used" || usage == "unused")
{
    var usedTemplateIds = new HashSet<ElementId>(
        new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
            .Select(v => v.ViewTemplateId));

    query = usage == "used"
        ? query.Where(v => usedTemplateIds.Contains(v.Id))
        : query.Where(v => !usedTemplateIds.Contains(v.Id));
}

List<Element> elements = query.OrderBy(v => v.Name).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} View Template(s), usage: {usage}" + (string.IsNullOrEmpty(nameContains) ? "" : $", name contains \"{nameContains}\"") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
