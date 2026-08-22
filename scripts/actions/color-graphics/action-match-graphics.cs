// ============================================================
// FRAGMENT (action) — action-match-graphics.cs
// PURPOSE: Copy the graphic overrides OFF one source element and onto every element in `elements` —
//          Revit's "match this one's look" job, done in bulk. Two modes:
//            "element"  — copy the SOURCE ELEMENT's own per-element override onto each target element
//            "category" — copy the SOURCE's CATEGORY override onto each target's CATEGORY
//          Different from action-set-color-uniform.cs (you state the colour; here the MODEL states it,
//          read off something already looking right) and from action-reset-graphic-overrides.cs (that
//          clears, this copies).
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/graphic-override-precedence.md — read it before deciding which mode.
//
// GOTCHA: `new OverrideGraphicSettings(existing)` IS A COPY CONSTRUCTOR and it is the right way to do
//         this. Do NOT read the source's properties one at a time and re-set them — every property you
//         forget silently becomes "no override" on the target, so the target ends up a partial copy
//         that looks nearly right. Copy the whole object.
// GOTCHA: WHICH MODE YOU PICK CHANGES WHO ELSE IS AFFECTED. "element" touches only the elements you
//         filtered. "category" repaints EVERY element of that category in the view, including ones
//         that were never in `elements` — because a category override is not a per-element thing. Read
//         the target-category list in the report before running it for real.
// GOTCHA: an ELEMENT override beats a CATEGORY override (rows 2 and 7 of the precedence table). So if
//         a target already carries its own per-element override, copying a CATEGORY override onto it
//         changes nothing visible. If "match" appears to do nothing, that is the first thing to check —
//         `action-report-graphic-overrides.cs` will show it.
// GOTCHA: a category that Revit will not let this view override is SKIPPED and named, not failed.
//         `view.IsCategoryOverridable(categoryId)` is the real test — checking `CategoryType` alone is
//         not enough.
// GOTCHA: copying an EMPTY override is a legitimate result, not a bug — it means the source has no
//         override, so matching it CLEARS the targets. The report says so explicitly rather than
//         reporting a silent no-op.
// NOTE: dry-run by default. It reads the source, lists what would change, and touches nothing.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Match Element Graphics
//   and Match Category Graphics tools, which use exactly this clone-and-apply on real jobs. Run it on
//   one target first and look at it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int sourceElementIdInt = 0;      // REQUIRED — the element whose look is being copied
string mode = "element";         // "element" = per-element override | "category" = category-level override
bool dryRun = true;              // true = report only; false = actually apply
int? targetViewIdInt = null;     // null = active view; set an Element Id to target any view
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (sourceElementIdInt <= 0)
{
    sb.AppendLine("Set sourceElementIdInt to the element whose graphics should be copied.");
}
else
{
    Element source = Document.GetElement(new ElementId(sourceElementIdInt));
    if (source == null)
    {
        sb.AppendLine($"Source element {sourceElementIdInt} not found.");
    }
    else if (elements.Count == 0)
    {
        sb.AppendLine("No target elements — nothing to do.");
    }
    else
    {
        bool byCategory = mode.Equals("category", StringComparison.OrdinalIgnoreCase);

        // ---- read the source override, and COPY it whole ----
        OverrideGraphicSettings sourceOgs = null;
        string sourceLabel;

        if (byCategory)
        {
            Category sourceCat = source.Category;
            if (sourceCat == null || sourceCat.Id == ElementId.InvalidElementId)
            {
                sb.AppendLine($"Source element {sourceElementIdInt} has no category — cannot match category graphics.");
                return sb.ToString();
            }
            sourceOgs = new OverrideGraphicSettings(view.GetCategoryOverrides(sourceCat.Id));
            sourceLabel = $"category '{sourceCat.Name}'";
        }
        else
        {
            sourceOgs = new OverrideGraphicSettings(view.GetElementOverrides(source.Id));
            sourceLabel = $"element {source.Id} ({source.Name})";
        }

        // Is the source override actually empty? Copying an empty one CLEARS the targets — say so.
        bool sourceIsEmpty =
            sourceOgs.ProjectionLineColor != null && !sourceOgs.ProjectionLineColor.IsValid &&
            sourceOgs.SurfaceForegroundPatternId == ElementId.InvalidElementId &&
            sourceOgs.CutForegroundPatternId == ElementId.InvalidElementId &&
            sourceOgs.ProjectionLineWeight == OverrideGraphicSettings.InvalidPenNumber &&
            sourceOgs.Transparency == 0 && !sourceOgs.Halftone;

        sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
                      $"Matching graphics from {sourceLabel} in view '{view.Name}'" +
                      (byCategory ? " — CATEGORY mode" : " — element mode") + ".");
        if (sourceIsEmpty)
            sb.AppendLine("NOTE: the source carries NO override, so matching it will CLEAR the targets.");

        int applied = 0;
        var skipped = new List<string>();

        if (byCategory)
        {
            // One override per distinct TARGET CATEGORY — and every element of that category in the
            // view is repainted, not just the ones in `elements`.
            var targetCats = new Dictionary<string, Category>();
            foreach (var e in elements)
            {
                Category c = e == null ? null : e.Category;
                if (c == null || c.Id == ElementId.InvalidElementId) continue;
                if (!targetCats.ContainsKey(c.Name)) targetCats[c.Name] = c;
            }

            sb.AppendLine($"Target categories ({targetCats.Count}): " + string.Join(", ", targetCats.Keys.OrderBy(k => k)));

            using (var t = new Transaction(Document, "AJ Tools - Match Category Graphics"))
            {
                t.Start();
                try
                {
                    foreach (var kv in targetCats)
                    {
                        bool overridable = false;
                        try { overridable = view.IsCategoryOverridable(kv.Value.Id); } catch { }
                        if (!overridable) { skipped.Add($"{kv.Key} (not overridable in this view)"); continue; }

                        if (!dryRun) view.SetCategoryOverrides(kv.Value.Id, sourceOgs);
                        applied++;
                    }
                    if (dryRun) t.RollBack(); else t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
                    return sb.ToString();
                }
            }
            sb.AppendLine((dryRun ? "Would apply to " : "Applied to ") + applied + " categor(y/ies).");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Match Element Graphics"))
            {
                t.Start();
                try
                {
                    foreach (var e in elements)
                    {
                        if (e == null || e.Id == source.Id) continue;   // never match the source to itself
                        try
                        {
                            if (!dryRun) view.SetElementOverrides(e.Id, sourceOgs);
                            applied++;
                        }
                        catch
                        {
                            if (skipped.Count < 20) skipped.Add(e.Id.ToString());
                        }
                    }
                    if (dryRun) t.RollBack(); else t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
                    return sb.ToString();
                }
            }
            sb.AppendLine((dryRun ? "Would apply to " : "Applied to ") + applied + " element(s).");
        }

        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
