// ============================================================
// FRAGMENT (filter) — filter-by-connection-status.cs
// PURPOSE: Category elements that HAVE at least one open (unconnected) Connector, or are FULLY
//          connected — MEP QA sweep ("find loose pipe/duct ends"). Different job from
//          recipes/verify-duct-connectivity.cs, which traces a full chain out to equipment; this is a
//          cheap, local, single-element check with no chain-walking.
// ✱✱ FOUR FRAGMENTS ANSWER "OPEN ENDS". Pick by the sentence — only the last two can change the model:
//      filters/by-relationship/filter-by-connection-status.cs        FILTER: narrow any category to the
//                                                                    elements with an open connector.
//      actions/qa-checks/action-find-dead-end-system.cs              REPORT: sorts DELIBERATE ends from
//                                                                    accidents, lists only the accidents.
//      actions/qa-checks/action-check-open-pipe-ends.cs              PIPES: report open ends, and cap
//                                                                    them only if you say so.
//      actions/structural-changes/action-connect-open-connectors.cs  JOIN: connects open pairs that
//                                                                    already touch — WRITES, dry-run first.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: ConnectorManager access pattern confirmed live in recipes/verify-duct-connectivity.cs
//         (MEPCurve.ConnectorManager, falling back to FamilyInstance.MEPModel.ConnectorManager).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
bool wantOpenEnds = true; // true = at least one connector NOT connected; false = every connector IS connected
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

Func<Element, IEnumerable<Connector>> getConnectors = e =>
{
    var mgr = (e as MEPCurve)?.ConnectorManager
        ?? (e is FamilyInstance fi ? fi.MEPModel?.ConnectorManager : null);
    return mgr == null ? Enumerable.Empty<Connector>() : mgr.Connectors.Cast<Connector>();
};

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var conns = getConnectors(e).ToList();
        if (conns.Count == 0) return false; // no connectors at all — not a meaningful match either way
        bool hasOpenEnd = conns.Any(c => !c.IsConnected);
        return wantOpenEnds ? hasOpenEnd : !hasOpenEnd;
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}, {(wantOpenEnds ? "with at least one open connector end" : "fully connected")}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
