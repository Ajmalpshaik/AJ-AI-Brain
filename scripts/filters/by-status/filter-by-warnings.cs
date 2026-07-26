// ============================================================
// FRAGMENT (filter) — filter-by-warnings.cs
// PURPOSE: Elements flagged by a current model warning, as an actionable `elements` set — for "select/
//          highlight/isolate everything with a warning". Different job from
//          context/context-all-warnings.cs, which only REPORTS warnings read-only and doesn't produce a
//          chainable element set.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: Document.GetWarnings() usage confirmed in context/context-all-warnings.cs.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool errorsOnly = false; // true = only Error-severity warnings, skip Warning-severity
string descriptionContains = ""; // "" = every warning; else substring match on the warning's description text
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var warnings = Document.GetWarnings().AsEnumerable();
if (errorsOnly) warnings = warnings.Where(w => w.GetSeverity() == FailureSeverity.Error);
if (!string.IsNullOrEmpty(descriptionContains))
    warnings = warnings.Where(w => (w.GetDescriptionText() ?? "").IndexOf(descriptionContains, StringComparison.OrdinalIgnoreCase) >= 0);

var failingIds = warnings.SelectMany(w => w.GetFailingElements()).Distinct().ToList();

List<Element> elements = failingIds
    .Select(id => Document.GetElement(id))
    .Where(e => e != null)
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) flagged by a current warning" +
    (errorsOnly ? " (Error severity only)" : "") +
    (string.IsNullOrEmpty(descriptionContains) ? "" : $", description contains \"{descriptionContains}\"") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
