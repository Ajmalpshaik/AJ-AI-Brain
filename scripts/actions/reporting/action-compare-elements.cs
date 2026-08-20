// ============================================================
// FRAGMENT (action) — action-compare-elements.cs
// PURPOSE: Side-by-side parameter comparison of the elements in `elements` (2 or more) — reports which
//          parameters DIFFER between them (or every parameter with showAll). The "why is this duct
//          different from that one" QA answer.
// ASSUMES: elements (List<Element>, 2+) and sb (StringBuilder) exist from a filter above —
//          filter-by-id-list.cs is the natural feeder ("compare 123456 and 123789").
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
// GOTCHA: comparison uses each parameter's display string (AsValueString, mm-side) so 2500.0000001 vs
//         2500 internal-feet noise doesn't produce false differences; truly unset vs "" both read "(none)".
// GOTCHA: with many elements the table gets wide — happiest at 2-5 elements; hard cap of 8 columns,
//         extras are reported skipped, not silently dropped.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 3 walls compared, surfaced exactly the 3 differing parameters
//   (Area, Length, Volume) and suppressed the ~50 identical ones.
// ============================================================


// Version-proof id VALUE, for the few places that need a number rather than an ElementId (testing for a
// built-in negative id, casting to BuiltInParameter). Revit 2024 renamed IntegerValue -> Value and made
// it 64-bit; naming either one directly fails to compile on the other version, so it is looked up by
// name instead. The lookup happens ONCE and is captured, so each call is a field read, not a reflection
// search - safe to use in a loop.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool showAll = false;       // false = only parameters whose values differ; true = every parameter
bool includeType = true;    // also compare the elements' TYPE parameters (marked [T])
// ---- END INPUTS ----

if (elements.Count < 2)
{
    sb.AppendLine($"Need at least 2 elements to compare — got {elements.Count}.");
}
else
{
    var compare = elements.Take(8).ToList();
    if (elements.Count > 8) sb.AppendLine($"NOTE: comparing the first 8 of {elements.Count} elements — rerun with a tighter filter for the rest.");

    Func<Parameter, string> readVal = p =>
    {
        if (p == null || !p.HasValue) return "(none)";
        string v = null;
        try { v = p.AsValueString(); } catch { }
        if (string.IsNullOrEmpty(v))
        {
            switch (p.StorageType)
            {
                case StorageType.String: v = p.AsString(); break;
                case StorageType.Integer: v = p.AsInteger().ToString(); break;
                case StorageType.Double: v = p.AsDouble().ToString("F4"); break;
                case StorageType.ElementId:
                    var id = p.AsElementId();
                    v = IdValue(id) < 0 ? "(none)" : (Document.GetElement(id)?.Name ?? id.ToString());
                    break;
            }
        }
        return string.IsNullOrEmpty(v) ? "(none)" : v;
    };

    // parameter name -> per-element display value (instance first, [T]-prefixed name for type params)
    var table = new SortedDictionary<string, string[]>();
    Action<string, int, string> put = (name, col, val) =>
    {
        if (!table.ContainsKey(name))
        {
            table[name] = Enumerable.Repeat("(n/a)", compare.Count).ToArray();
        }
        table[name][col] = val;
    };

    for (int c = 0; c < compare.Count; c++)
    {
        foreach (Parameter p in compare[c].Parameters)
        {
            if (p.Definition == null) continue;
            put(p.Definition.Name, c, readVal(p));
        }
        if (includeType)
        {
            var typeEl = Document.GetElement(compare[c].GetTypeId());
            if (typeEl != null)
                foreach (Parameter p in typeEl.Parameters)
                {
                    if (p.Definition == null) continue;
                    put("[T] " + p.Definition.Name, c, readVal(p));
                }
        }
    }

    var header = "Parameter | " + string.Join(" | ", compare.Select(e => $"Id {e.Id}"));
    sb.AppendLine($"Comparing {compare.Count} elements: {string.Join(", ", compare.Select(e => $"'{e.Name}' (Id {e.Id})"))}");
    sb.AppendLine(header);

    int shown = 0;
    foreach (var kv in table)
    {
        bool differs = kv.Value.Distinct().Count() > 1;
        if (differs || showAll)
        {
            sb.AppendLine($"{(differs ? "≠ " : "  ")}{kv.Key} | {string.Join(" | ", kv.Value)}");
            shown++;
        }
    }
    sb.AppendLine(shown == 0
        ? "No differing parameters — the elements are identical on everything compared."
        : $"{shown} parameter(s) listed ({(showAll ? "all" : "differences only")}); ≠ marks a difference.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
