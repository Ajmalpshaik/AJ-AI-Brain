// ============================================================
// FRAGMENT (action) — action-purge-unused.cs
// PURPOSE: Delete unused View Templates, unused View/Selection Filters, or unused Materials — the subset of
//          Revit's native "Purge Unused" that's actually provably correct from the PUBLIC API (each mode
//          below computes "unused" from a real reverse-lookup, not a heuristic guess).
// DOES NOT consume `elements` — this always scans/acts on the WHOLE document for the chosen mode.
// NOT STANDALONE — see scripts/README.md for how to compose (though this fragment is usually run alone).
// HONESTY NOTE: this does NOT replace Revit's native Purge Unused command. Families, Line Patterns, Fill
// Patterns, and several other categories in that dialog rely on internal Revit heuristics with no public
// API equivalent — this fragment only covers what can be checked correctly: View Templates (is it assigned
// to any view), Filters (is it applied to any view), Materials (does any element in the whole model
// actually reference it, via the same GetMaterialIds() scan action-report-material-takeoff.cs uses). Run the
// native Purge Unused command too for families/patterns — this is a supplement, not a substitute.
// GOTCHA: Materials mode does a WHOLE-MODEL element scan (every non-type element's GetMaterialIds()) — can
//         be slow on a large model, same caution as any unbounded scan in this repo.
// MANDATORY per README's explorer-first rule: run with dryRun = true first, read the actual list of what
// would be deleted, confirm with the user, THEN set dryRun = false.
// Dry-run live-verified 2026-07-22 for all 3 modes. Real non-dry-run delete not yet exercised (no
// pre-existing unused content in this model that was safe to actually remove).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "view_templates"; // "view_templates" | "filters" | "materials"
bool dryRun = true; // true = report what WOULD be deleted, delete nothing; false = actually delete
// ---- END INPUTS ----

List<Element> targets = null;

if (mode == "view_templates")
{
    var usedTemplateIds = new HashSet<ElementId>(
        new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
            .Select(v => v.ViewTemplateId));

    targets = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => v.IsTemplate && !usedTemplateIds.Contains(v.Id))
        .Cast<Element>().ToList();
}
else if (mode == "filters")
{
    var appliedFilterIds = new HashSet<ElementId>();
    foreach (View v in new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>())
    {
        ICollection<ElementId> ids;
        try { ids = v.GetFilters(); } catch { continue; } // some view types don't support filters
        foreach (var id in ids) appliedFilterIds.Add(id);
    }

    targets = new FilteredElementCollector(Document).OfClass(typeof(FilterElement))
        .Where(f => !appliedFilterIds.Contains(f.Id))
        .Cast<Element>().ToList();
}
else if (mode == "materials")
{
    var usedMaterialIds = new HashSet<ElementId>();
    foreach (var e in new FilteredElementCollector(Document).WhereElementIsNotElementType())
    {
        ICollection<ElementId> matIds;
        try { matIds = e.GetMaterialIds(false); } catch { continue; }
        if (matIds == null) continue;
        foreach (var id in matIds) usedMaterialIds.Add(id);
    }

    targets = new FilteredElementCollector(Document).OfClass(typeof(Material))
        .Where(m => !usedMaterialIds.Contains(m.Id))
        .Cast<Element>().ToList();
}

if (targets == null)
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"view_templates\", \"filters\", or \"materials\". Nothing changed.");
}
else
{
    sb.AppendLine($"Mode '{mode}': {targets.Count} unused element(s) found" + (dryRun ? " (DRY RUN — nothing deleted)." : "."));
    foreach (var el in targets.Take(50))
        sb.AppendLine($"  '{el.Name}' (Id {el.Id})");
    if (targets.Count > 50) sb.AppendLine($"  ... and {targets.Count - 50} more.");

    if (!dryRun && targets.Count > 0)
    {
        int deleted = 0, skipped = 0;
        using (var t = new Transaction(Document, "AJ Tools - Purge Unused"))
        {
            t.Start();
            try
            {
                foreach (var el in targets)
                {
                    try
                    {
                        if (!Document.GetElement(el.Id).IsValidObject) { skipped++; continue; }
                        Document.Delete(el.Id);
                        deleted++;
                    }
                    catch { skipped++; }
                }
                t.Commit();
                sb.AppendLine($"Deleted {deleted}, skipped {skipped}.");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to purge — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
// ---- add return sb.ToString(); to finish (this fragment is usually run standalone) ----
