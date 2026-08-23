// ============================================================
// FRAGMENT (action) — action-create-view-filters-by-value.cs
// PURPOSE: Read every distinct value of one parameter across `elements`, then build ONE PERSISTENT VIEW
//          FILTER PER VALUE, each in its own colour, and apply them to a view — "a filter per system",
//          "a colour per duct size", "one filter for each fire zone". The standards job: a colour scheme
//          that lives in the project, applies itself to elements added later, and can be copied to every
//          other view.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
//
// ✱✱ WHY THIS IS NOT action-color-by-group.cs. That fragment already colours by any parameter value, and
//    it is the right tool for LOOKING at something now. But it writes per-element graphic OVERRIDES:
//    they live on the elements in one view, they do not follow the parameter, and an element drawn
//    tomorrow gets nothing. This one writes `ParameterFilterElement`s — real Revit filters, in the
//    Visibility/Graphics dialog, that re-evaluate themselves forever. Use colour-by-group to investigate;
//    use this to establish a standard.
//
// ✱✱ THE FILTER'S CATEGORY SET IS TAKEN FROM THE ELEMENTS, NOT GUESSED. A ParameterFilterElement is only
//    valid for categories that actually share the parameter — hand it a category that does not have it
//    and Revit rejects the whole filter. This collects the distinct categories present in `elements` and
//    keeps only those where the parameter genuinely resolves, then says which ones it dropped and why.
//
// GOTCHA: A FILTER RULE NEEDS THE PARAMETER'S ElementId, and for a built-in parameter that Id is
//         negative and version-stable, while for a shared/project parameter it is the definition's own
//         element. Both are resolved from a real element that carries the parameter, which is why this
//         needs `elements` rather than just a parameter name.
// GOTCHA: text rules — `ParameterFilterRuleFactory.CreateEqualsRule(ElementId, string, bool)` lost its
//         case-sensitivity argument in later Revit versions. Reached by reflection, same as
//         action-create-view-filter.cs, so one source runs on 2020 through 2027.
// GOTCHA: a value that is BLANK becomes its own filter only if `includeBlank` is set — usually you want
//         to SEE the blanks rather than colour them, and `filter-by-parameter-exists.cs` finds them.
// GOTCHA: DRY RUN BY DEFAULT. This creates project-wide elements; the filters persist after undo of a
//         later step, so read the list first.
// RELATED: action-color-by-group.cs (temporary, for looking), action-create-view-filter.cs (one filter,
//          any rule kind), action-copy-view-filters.cs (push the finished set onto every other view).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on one small category and one view,
//   then open Visibility/Graphics and look at the Filters tab before doing it for real.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string parameterName = "System Type";  // the parameter whose distinct values become filters
string filterNamePrefix = "";          // "" = "<parameter> - <value>"; e.g. "MEP_" to match the office standard
bool applyToActiveView = true;         // add the new filters to the active view with their colours
bool colourLinesOnly = true;           // Ajmal's standing default: line colour only, no fill/pattern change
string colourMode = "palette";         // "palette" | "gradient"
bool includeBlank = false;             // give elements with no value their own filter too
int maxValues = 20;                    // refuse to make more filters than this — a runaway parameter is a mistake
bool dryRun = true;                    // true = list what WOULD be created, change nothing
// ---- END INPUTS ----

if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this.");
    return sb.ToString();
}

// ---- 1. read the parameter off every element, and find its ElementId ----
ElementId paramId = ElementId.InvalidElementId;
StorageType storage = StorageType.None;
var byValue = new Dictionary<string, List<Element>>();
var catIds = new HashSet<ElementId>();
int noParam = 0;

foreach (var el in elements)
{
    Parameter p = null;
    try { p = el.LookupParameter(parameterName); } catch { }
    if (p == null || p.Definition == null) { noParam++; continue; }

    if (paramId == ElementId.InvalidElementId)
    {
        paramId = p.Id;
        storage = p.StorageType;
    }

    string v;
    try
    {
        v = p.StorageType == StorageType.String ? (p.AsString() ?? "")
          : (p.AsValueString() ?? "");
    }
    catch { v = ""; }

    if (string.IsNullOrWhiteSpace(v) && !includeBlank) continue;
    if (string.IsNullOrWhiteSpace(v)) v = "(blank)";

    if (!byValue.ContainsKey(v)) byValue[v] = new List<Element>();
    byValue[v].Add(el);
    if (el.Category != null) catIds.Add(el.Category.Id);
}

if (paramId == ElementId.InvalidElementId)
{
    sb.AppendLine($"None of the {elements.Count} element(s) carries a parameter called '{parameterName}'.");
    sb.AppendLine("Check the spelling against action-report-parameter-inventory.cs, which lists what they actually have.");
    return sb.ToString();
}

var values = byValue.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

sb.AppendLine($"Parameter '{parameterName}' — {values.Count} distinct value(s) across {elements.Count} element(s).");
if (noParam > 0) sb.AppendLine($"{noParam} element(s) do not carry it at all and are ignored.");
sb.AppendLine($"Categories involved: {string.Join(", ", catIds.Select(c => Category.GetCategory(Document, c)?.Name).Where(n => n != null))}");
sb.AppendLine();

if (values.Count > maxValues)
{
    sb.AppendLine($"STOPPED — {values.Count} distinct values would mean {values.Count} filters, over the cap of {maxValues}.");
    sb.AppendLine("That usually means the wrong parameter (a length, a mark, a unique id). Raise maxValues if it really is intended.");
    foreach (var v in values.Take(15)) sb.AppendLine($"    {v}  ({byValue[v].Count})");
    return sb.ToString();
}

// ---- 2. colours ----
var palette = new List<Color>
{
    new Color(230,25,75),   new Color(60,180,75),   new Color(0,130,200),  new Color(245,130,48),
    new Color(145,30,180),  new Color(70,240,240),  new Color(240,50,230), new Color(210,245,60),
    new Color(250,190,190), new Color(0,128,128),   new Color(170,110,40), new Color(128,0,0),
    new Color(170,255,195), new Color(128,128,0),   new Color(0,0,128),    new Color(128,128,128),
    new Color(255,215,0),   new Color(75,0,130),    new Color(46,139,87),  new Color(220,20,60)
};
Func<int, Color> colourFor = (i) =>
{
    if (colourMode == "gradient" && values.Count > 1)
    {
        double t = (double)i / (values.Count - 1);
        return new Color((byte)(20 + 220 * t), (byte)(80 + 100 * (1 - Math.Abs(0.5 - t) * 2)), (byte)(220 - 200 * t));
    }
    return palette[i % palette.Count];
};

// ---- 3. the version-safe text rule (see the header) ----
Func<ElementId, string, FilterRule> textEquals = (pid, val) =>
{
    var cands = typeof(ParameterFilterRuleFactory).GetMethods()
        .Where(m => m.Name == "CreateEqualsRule").ToList();
    var three = cands.FirstOrDefault(m => m.GetParameters().Length == 3
                                       && m.GetParameters()[1].ParameterType == typeof(string));
    if (three != null) return (FilterRule)three.Invoke(null, new object[] { pid, val, false });
    var two = cands.FirstOrDefault(m => m.GetParameters().Length == 2
                                     && m.GetParameters()[1].ParameterType == typeof(string));
    if (two != null) return (FilterRule)two.Invoke(null, new object[] { pid, val });
    throw new InvalidOperationException("This Revit has no ParameterFilterRuleFactory.CreateEqualsRule(ElementId, string, ...).");
};

var view = Document.ActiveView;
if (applyToActiveView && (view == null || view.IsTemplate))
{
    sb.AppendLine("applyToActiveView is on but the active view is a template or missing — open a real view first.");
    return sb.ToString();
}

sb.AppendLine($"{"Value",-34}{"Count",7}  {"Colour",-14}Filter name");
var plan = new List<Tuple<string, string, Color, int>>();
for (int i = 0; i < values.Count; i++)
{
    var v = values[i];
    string name = (filterNamePrefix == "" ? $"{parameterName} - {v}" : $"{filterNamePrefix}{v}");
    var c = colourFor(i);
    plan.Add(Tuple.Create(v, name, c, byValue[v].Count));
    sb.AppendLine($"{v,-34}{byValue[v].Count,7}  {$"{c.Red},{c.Green},{c.Blue}",-14}{name}");
}
sb.AppendLine();

if (dryRun)
{
    sb.AppendLine($"DRY RUN — nothing created. {plan.Count} view filter(s) would be made"
                + (applyToActiveView ? $" and applied to '{view.Name}'" : "") + ".");
    sb.AppendLine(colourLinesOnly ? "Line colour only (fill and pattern untouched)." : "Line AND surface colour would be set.");
    sb.AppendLine("Set dryRun = false to create them.");
}
else
{
    int made = 0, reused = 0, failed = 0;
    var catList = new List<ElementId>(catIds);

    using (var t = new Transaction(Document, "AJ Tools - View filters by value"))
    {
        t.Start();
        var existing = new FilteredElementCollector(Document)
            .OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>()
            .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var item in plan)
        {
            try
            {
                var rule = textEquals(paramId, item.Item1 == "(blank)" ? "" : item.Item1);
                var elemFilter = new ElementParameterFilter(rule);

                ParameterFilterElement pfe;
                if (existing.ContainsKey(item.Item2))
                {
                    pfe = existing[item.Item2];
                    pfe.SetElementFilter(elemFilter);
                    reused++;
                }
                else
                {
                    pfe = ParameterFilterElement.Create(Document, item.Item2, catList, elemFilter);
                    made++;
                }

                if (applyToActiveView)
                {
                    var ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(item.Item3);
                    ogs.SetCutLineColor(item.Item3);
                    if (!colourLinesOnly)
                    {
                        ogs.SetSurfaceForegroundPatternColor(item.Item3);
                        ogs.SetCutForegroundPatternColor(item.Item3);
                    }

                    var already = view.GetFilters();
                    if (!already.Contains(pfe.Id)) view.AddFilter(pfe.Id);
                    view.SetFilterOverrides(pfe.Id, ogs);
                }
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"  '{item.Item2}': FAILED — {ex.Message}");
            }
        }
        t.Commit();
    }

    sb.AppendLine($"Created {made} filter(s), updated {reused} existing, failed {failed}.");
    if (applyToActiveView) sb.AppendLine($"Applied to '{view.Name}' — {(colourLinesOnly ? "line colour only" : "line and surface")}.");
    sb.AppendLine("Next: action-copy-view-filters.cs pushes this whole set onto every other view or a view template.");
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
