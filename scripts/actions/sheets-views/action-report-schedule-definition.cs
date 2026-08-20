// ============================================================
// FRAGMENT (action) — action-report-schedule-definition.cs
// PURPOSE: Read back HOW a schedule is built, not what is in it — the category it collects (including
//          the -1 "<Multi-Category>" case), its filter rules with the category/element IDs resolved to
//          real names, its sort/group levels in order, the itemised/headers/grand-total/linked-files
//          switches, and for every column WHERE its value comes from: a Revit built-in parameter (named),
//          a SHARED parameter (with its GUID), a project parameter, or a formula calculated in the
//          schedule itself. Read-only. This is the "study the schedule" companion to
//          action-report-schedule-fields.cs, which lists the columns but not the rules around them —
//          run that one for the column order/headings, this one for the recipe.
//          The GUID column is the useful half when a schedule has to be rebuilt in another model: a
//          shared parameter is only the same parameter if the GUID matches, never because the name does.
// ASSUMES: elements (List<Element>, each really a ViewSchedule — e.g. from filters/by-view-and-sheet/filter-by-schedules.cs)
//          and sb (StringBuilder) already exist from a filter fragment above.
// SOURCE: composed from actions/sheets-views/action-report-schedule-fields.cs (the GetFieldOrder/GetField
//          walk) plus the read-back halves of action-set-schedule-filters.cs and
//          action-set-schedule-sort-group.cs — those two SET the rules this one reports.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// Version-proof id VALUE, for the few places that need a number rather than an ElementId (testing for a
// built-in negative id, casting to BuiltInParameter). Revit 2024 renamed IntegerValue -> Value and made
// it 64-bit; naming either one directly fails to compile on the other version, so it is looked up by
// name instead. The lookup happens ONCE and is captured, so each call is a field read, not a reflection
// search - safe to use in a loop.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

// API LIMIT, measured on Revit 2020 (2026-08-19): the TEXT of a calculated-value formula cannot be read.
// ScheduleField has no formula member at all in this version (ScheduleField.GetFormula() arrived later),
// so a Formula column is reported as such and its expression has to be read in the Revit UI —
// Schedule Properties > Fields > select the field > Edit. The check below is by reflection on purpose:
// calling GetFormula() directly is a COMPILE error on 2020, which no try/catch can rescue.

int skipped = 0;

foreach (var el in elements)
{
    var schedule = el as ViewSchedule;
    if (schedule == null) { skipped++; continue; }

    var def = schedule.Definition;

    // ---- what it collects ----
    Category cat = null;
    try { cat = Category.GetCategory(Document, def.CategoryId); } catch { }
    string catName = cat != null ? cat.Name
        : (IdValue(def.CategoryId) == -1 ? "<Multi-Category>" : def.CategoryId.ToString());

    sb.AppendLine($"Schedule '{schedule.Name}' (Id {schedule.Id})");
    sb.AppendLine($"  Category     : {catName}");
    sb.AppendLine($"  Key schedule : {def.IsKeySchedule}");
    sb.AppendLine($"  Itemized     : {def.IsItemized}   (false = one row per group, totals only)");
    sb.AppendLine($"  Show headers : {def.ShowHeaders}");
    sb.AppendLine($"  Grand total  : {def.ShowGrandTotal}");
    sb.AppendLine($"  Incl. links  : {def.IncludeLinkedFiles}");

    // ---- filters, with ids resolved ----
    var filters = def.GetFilters();
    sb.AppendLine($"  Filters ({filters.Count}):");
    if (filters.Count == 0) sb.AppendLine("    (none — every element of the category is listed)");
    foreach (var f in filters)
    {
        string fieldName = "?";
        try { fieldName = def.GetField(f.FieldId).GetName(); } catch { }

        string val;
        try { val = "\"" + f.GetStringValue() + "\""; }
        catch
        {
            try { val = f.GetDoubleValue().ToString(); }
            catch
            {
                try { val = f.GetIntegerValue().ToString(); }
                catch
                {
                    try
                    {
                        var id = f.GetElementIdValue();
                        Category fc = null;
                        try { fc = Category.GetCategory(Document, id); } catch { }
                        if (fc != null) val = $"{fc.Name} [{(BuiltInCategory)id}]";
                        else { var e2 = Document.GetElement(id); val = e2 != null ? e2.Name : id.ToString(); }
                    }
                    catch { val = "(no value — e.g. HasValue / HasNoValue)"; }
                }
            }
        }
        sb.AppendLine($"    {fieldName}  {f.FilterType}  {val}");
    }

    // ---- sorting / grouping, in order ----
    var sorts = def.GetSortGroupFields();
    sb.AppendLine($"  Sort/group ({sorts.Count}), in order:");
    if (sorts.Count == 0) sb.AppendLine("    (none)");
    int n = 1;
    foreach (var s in sorts)
    {
        string fieldName = "?";
        try { fieldName = def.GetField(s.FieldId).GetName(); } catch { }
        sb.AppendLine($"    {n}. {fieldName}  {s.SortOrder}   header={s.ShowHeader} footer={s.ShowFooter} blankLine={s.ShowBlankLine}");
        n++;
    }

    // ---- where every column's value comes from ----
    var formulaMethod = typeof(ScheduleField).GetMethod("GetFormula");
    var order = def.GetFieldOrder();
    sb.AppendLine($"  Columns ({order.Count}) — # | field | type | source | formula/GUID:");
    for (int i = 0; i < order.Count; i++)
    {
        var field = def.GetField(order[i]);
        string source, extra = "";
        var pid = field.ParameterId;

        if (field.FieldType.ToString() == "Formula")
        {
            source = "calculated in the schedule";
            if (formulaMethod != null) { try { extra = (string)formulaMethod.Invoke(field, null); } catch { extra = "(formula unreadable)"; } }
            else extra = "(formula text not exposed by this Revit version — read it in the UI)";
        }
        else if (IdValue(pid) < 0)
        {
            source = "Revit built-in";
            extra = ((BuiltInParameter)IdValue(pid)).ToString();
        }
        else
        {
            var pe = Document.GetElement(pid) as ParameterElement;
            var spe = pe as SharedParameterElement;
            if (spe != null) { source = "SHARED parameter"; extra = spe.GuidValue.ToString(); }
            else if (pe != null) { source = "project parameter"; extra = "(not shared)"; }
            else { source = "id " + pid; }
        }
        sb.AppendLine($"    {i + 1} | {field.GetName()} | {field.FieldType} | {source} | {extra}");
    }
    sb.AppendLine();
}
if (skipped > 0) sb.AppendLine($"{skipped} non-ViewSchedule element(s) skipped.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
