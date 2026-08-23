// ============================================================
// FRAGMENT (action) — action-copy-view-filters.cs
// PURPOSE: Copy the view filters from ONE view (or view template) onto every view in `elements`, keeping
//          each filter's OVERRIDES and its visibility — "I set the colours up on this plan, now put the
//          same on the other twelve". The manual alternative is re-adding each filter on each view and
//          re-typing every override.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — Views or View Templates,
//          e.g. from filter-by-views.cs or filter-by-view-templates.cs. The source view is named
//          separately and may be outside that set.
//
// ✱✱ THE HALF EVERYONE FORGETS: ADDING THE FILTER IS NOT COPYING IT. `AddFilter` puts the filter on the
//    view with EMPTY overrides — no colour, no line weight, nothing. The look lives in a separate object
//    fetched with GetFilterOverrides(filterId) and applied with SetFilterOverrides. Copy one and not the
//    other and you get twelve views carrying the right filters and showing no change at all, which reads
//    as "the script did nothing".
//    The same is true of the on/off state: GetFilterVisibility / SetFilterVisibility is a THIRD thing.
//
// GOTCHA: A VIEW GOVERNED BY A TEMPLATE WILL NOT ACCEPT FILTERS. The template owns V/G, so AddFilter
//         throws or is ignored. Those views are named in the report — copy onto the TEMPLATE instead,
//         which changes every view using it at once, and is usually what was actually wanted.
//         action-report-view-template-status.cs shows which views are governed.
// GOTCHA: some view types (schedules, legends, sheets) cannot carry filters at all — reported, not failed.
// GOTCHA: an existing filter of the same name on the target is UPDATED, not duplicated.
// GOTCHA: DRY RUN BY DEFAULT.
// RELATED: action-create-view-filter.cs makes a filter in the first place;
//          action-report-graphic-overrides.cs shows what is currently set.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it onto ONE view and look at the plan.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceViewName = "";      // REQUIRED — the view or view template to copy FROM
string onlyFilterNamed = null;   // null = all filters on the source; otherwise just this one
bool copyOverrides = true;       // bring the colours and line weights across (see the note above)
bool copyVisibility = true;      // bring the filter's on/off state across
bool dryRun = true;              // true = report only, change nothing
// ---- END INPUTS ----

int viewsDone = 0, filtersApplied = 0, blockedByTemplate = 0, unsupported = 0, failed = 0;
var notes = new List<string>();

var allViews = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().ToList();
var source = allViews.FirstOrDefault(v => v.Name == sourceViewName);
var targets = elements.OfType<View>().ToList();

if (string.IsNullOrWhiteSpace(sourceViewName))
{
    sb.AppendLine("sourceViewName is required — name the view or view template whose filters you want copied.");
}
else if (source == null)
{
    var withFilters = allViews.Where(v => { try { return v.GetFilters().Count > 0; } catch { return false; } })
                              .Select(v => (v.IsTemplate ? "[template] " : "") + v.Name).Take(30);
    sb.AppendLine($"No view named '{sourceViewName}'. Views that DO carry filters: {string.Join(", ", withFilters)}");
}
else if (targets.Count == 0)
{
    sb.AppendLine("No Views in `elements` — put filter-by-views.cs or filter-by-view-templates.cs above this.");
}
else
{
    ICollection<ElementId> sourceFilterIds = null;
    try { sourceFilterIds = source.GetFilters(); } catch { }

    if (sourceFilterIds == null || sourceFilterIds.Count == 0)
    {
        sb.AppendLine($"'{sourceViewName}' has no view filters on it — nothing to copy.");
    }
    else
    {
        var toCopy = sourceFilterIds.ToList();
        if (onlyFilterNamed != null)
            toCopy = toCopy.Where(id => { var f = Document.GetElement(id); return f != null && f.Name == onlyFilterNamed; }).ToList();

        if (toCopy.Count == 0)
        {
            sb.AppendLine($"Filter '{onlyFilterNamed}' is not on '{sourceViewName}'. It carries: "
                + string.Join(", ", sourceFilterIds.Select(id => Document.GetElement(id)?.Name)));
        }
        else
        {
            sb.AppendLine($"Source: '{source.Name}'{(source.IsTemplate ? " [template]" : "")} — {toCopy.Count} filter(s) to copy:");
            foreach (var id in toCopy) sb.AppendLine($"    {Document.GetElement(id)?.Name}");
            sb.AppendLine($"{targets.Count} target view(s) in.");
            sb.AppendLine();

            using (var t = new Transaction(Document, "AJ Tools - Copy view filters"))
            {
                if (!dryRun) t.Start();

                foreach (var view in targets)
                {
                    if (view.Id == source.Id) continue;
                    string label = $"'{view.Name}'{(view.IsTemplate ? " [template]" : "")}";

                    // A view driven by a template cannot take its own filters.
                    if (!view.IsTemplate && view.ViewTemplateId != ElementId.InvalidElementId)
                    {
                        var tmpl = Document.GetElement(view.ViewTemplateId);
                        blockedByTemplate++;
                        notes.Add($"  {label}: governed by template '{tmpl?.Name}' — copy onto that template instead.");
                        continue;
                    }

                    ICollection<ElementId> existing = null;
                    try { existing = view.GetFilters(); }
                    catch { unsupported++; notes.Add($"  {label}: this view type cannot carry filters — skipped."); continue; }

                    int appliedHere = 0;
                    foreach (var fid in toCopy)
                    {
                        var fname = Document.GetElement(fid)?.Name ?? fid.ToString();
                        if (dryRun) { appliedHere++; continue; }

                        try
                        {
                            if (!existing.Contains(fid)) view.AddFilter(fid);

                            // The two halves that are NOT carried by AddFilter — see the header.
                            if (copyOverrides)
                                view.SetFilterOverrides(fid, source.GetFilterOverrides(fid));
                            if (copyVisibility)
                                view.SetFilterVisibility(fid, source.GetFilterVisibility(fid));

                            appliedHere++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            notes.Add($"  {label}: filter '{fname}' failed — {ex.Message}");
                        }
                    }

                    if (appliedHere > 0)
                    {
                        viewsDone++; filtersApplied += appliedHere;
                        sb.AppendLine(dryRun
                            ? $"  would put {appliedHere} filter(s) on {label}"
                            : $"  {label}: {appliedHere} filter(s) applied");
                    }
                }

                if (!dryRun) t.Commit();
            }

            sb.AppendLine();
            sb.AppendLine(dryRun
                ? $"DRY RUN — nothing changed. {filtersApplied} filter application(s) across {viewsDone} view(s). Set dryRun = false."
                : $"Applied {filtersApplied} filter(s) across {viewsDone} view(s)."
                  + (copyOverrides ? " Overrides copied." : " OVERRIDES NOT COPIED — the views will look unchanged."));
            if (blockedByTemplate > 0) sb.AppendLine($"Blocked by a view template: {blockedByTemplate} (copy onto the template instead).");
            if (unsupported > 0)       sb.AppendLine($"View type cannot carry filters: {unsupported}.");
            if (failed > 0)            sb.AppendLine($"Individual failures: {failed}.");
            foreach (var n in notes) sb.AppendLine(n);
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
