// ============================================================
// FRAGMENT (action) — action-purge-unused.cs
// PURPOSE: Delete unused View Templates, unused View/Selection Filters, or unused Materials — the subset of
//          Revit's native "Purge Unused" that's actually provably correct from the PUBLIC API (each mode
//          below computes "unused" from a real reverse-lookup, not a heuristic guess).
// DOES NOT consume `elements` — this always scans/acts on the WHOLE document for the chosen mode.
// NOT STANDALONE — see scripts/README.md for how to compose (though this fragment is usually run alone).
// HONESTY NOTE, CORRECTED 2026-08-24 — IT WAS TRUE ONLY OF REVIT 2020, AND IT DID NOT SAY SO.
// This note used to state flatly that families, line patterns and fill patterns "rely on internal Revit
// heuristics with no public API equivalent", so the four hand-computed modes below were all that was
// possible. **On Revit 2024 and 2027 that is wrong.** `Document.GetAllUnusedElements(ISet<ElementId>)`
// and `Document.GetUnusedElements(...)` are the public API behind the native Purge Unused dialog, and
// they cover what it covers — families, patterns, the lot. They do NOT exist on Revit 2020, which is
// where the original claim came from.
// So there is now a fifth mode, "revit_native", reached by reflection so one source still runs
// everywhere: on 2024+ it gives Revit's own full purge list, and on 2020 it says plainly that this
// version has no such API and points back at the four computed modes.
// **This is the same mistake create-ceiling.cs made** (fixed 2026-08-23): an "impossible" recorded
// against one version, stated without naming the version, becomes a lie the day another Revit is
// installed. This PC has had 2024 and 2027 on it the whole time.
// The four computed modes stay — they work on every version, and each is a provable reverse-lookup
// rather than a call into a black box.
// GOTCHA: Materials mode does a WHOLE-MODEL element scan (every non-type element's GetMaterialIds()) — can
//         be slow on a large model, same caution as any unbounded scan in this repo.
//
// ✱✱ FIXED 2026-08-23 — A MATERIAL USED ONLY AS PAINT WAS BEING REPORTED AS UNUSED. `GetMaterialIds(bool)`
//    returns TWO DIFFERENT SETS depending on the flag: `false` gives geometry and compound-structure
//    materials, `true` gives PAINT materials — the ones applied face-by-face with Modify > Paint. This
//    mode read only the `false` set, so a finish applied purely as paint (a wall face painted a different
//    colour, a floor with a painted screed) had no user and was offered for deletion. Deleting it strips
//    the paint off every face it was on. Both sets are now unioned. The same flaw was in
//    action-report-material-takeoff.cs and is fixed there too.
// MANDATORY per README's explorer-first rule: run with dryRun = true first, read the actual list of what
// would be deleted, confirm with the user, THEN set dryRun = false.
// * UPGRADED 2026-08-22 (harvested from the add-in's Purge Unused Groups): added a "groups" mode.
//   A GroupType whose `Groups` set is EMPTY has no placed instances anywhere — that is the provable
//   test, the same shape as the other three modes here. Model groups and detail groups are both
//   covered because both are GroupType.
// GOTCHA (groups): an ATTACHED DETAIL GROUP is owned by its parent model group. If the parent is
//   placed, the attached group is in use even when its own `Groups` set looks empty in some Revit
//   versions - so attached detail groups are reported SEPARATELY and never deleted by this fragment.
// GOTCHA (groups): deleting a GroupType is not the same as ungrouping. There is nothing placed to
//   ungroup - the definition itself goes. Nothing in the model changes visually.
// Dry-run live-verified 2026-07-22 for the first 3 modes; the groups mode is compile-checked on
// 2020/2024/2027 but not yet live-run. Real non-dry-run delete not yet exercised (no
// pre-existing unused content in this model that was safe to actually remove).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "view_templates"; // "view_templates" | "filters" | "materials" | "groups" | "revit_native"
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
else if (mode == "groups")
{
    // A GroupType with an EMPTY Groups set has no placed instances anywhere in the model.
    int attachedSkipped = 0;
    foreach (var gt in new FilteredElementCollector(Document).OfClass(typeof(GroupType)).Cast<GroupType>())
    {
        if (gt == null || !gt.IsValidObject) continue;

        int placed = 0;
        try { var gs = gt.Groups; placed = gs == null ? 0 : gs.Size; } catch { continue; }
        if (placed > 0) continue;

        // An attached detail group belongs to a parent model group - never delete one from here.
        bool isAttached = false;
        try
        {
            var cat = gt.Category;
            isAttached = cat != null && cat.Name != null &&
                         cat.Name.IndexOf("Attached", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { }
        if (isAttached) { attachedSkipped++; continue; }

        targets.Add(gt);
    }
    if (attachedSkipped > 0)
        sb.AppendLine($"({attachedSkipped} attached detail group type(s) skipped - they belong to a parent model group.)");
}
else if (mode == "revit_native")
{
    // Revit's OWN purge list - the same set the native Purge Unused dialog offers. Reached by name
    // because these members do not exist on Revit 2020: naming them directly is a COMPILE error there,
    // which no try/catch can rescue.
    var mGetAll = typeof(Document).GetMethod("GetAllUnusedElements");
    var mGetOne = typeof(Document).GetMethod("GetUnusedElements");

    if (mGetAll == null && mGetOne == null)
    {
        sb.AppendLine("This Revit has no GetUnusedElements/GetAllUnusedElements - that API arrives after Revit 2020.");
        sb.AppendLine("Use a computed mode instead: view_templates, filters, materials or groups. Those work on every version.");
        return sb.ToString();
    }

    // The argument is a set of ids to TREAT AS ALREADY DELETED. An empty set means "what is unused now".
    var emptySet = new HashSet<ElementId>();
    ICollection<ElementId> single = null, cascading = null;
    try { if (mGetOne != null) single = mGetOne.Invoke(Document, new object[] { emptySet }) as ICollection<ElementId>; } catch (Exception exU) { sb.AppendLine("GetUnusedElements failed: " + exU.Message); }
    try { if (mGetAll != null) cascading = mGetAll.Invoke(Document, new object[] { emptySet }) as ICollection<ElementId>; } catch (Exception exA) { sb.AppendLine("GetAllUnusedElements failed: " + exA.Message); }

    // The two differ in whether the purge CASCADES - removing one unused element can leave another
    // unused. Both counts are reported rather than assuming which is which: that is checkable on a real
    // model, and a guess here would be a confident wrong number.
    if (single != null)    sb.AppendLine($"GetUnusedElements: {single.Count} element(s).");
    if (cascading != null) sb.AppendLine($"GetAllUnusedElements: {cascading.Count} element(s).");
    if (single != null && cascading != null && single.Count != cascading.Count)
        sb.AppendLine($"The larger ({Math.Max(single.Count, cascading.Count)}) includes what becomes unused once the first pass is gone - a difference of {Math.Abs(cascading.Count - single.Count)}.");

    var chosen = cascading ?? single;
    if (chosen == null) { sb.AppendLine("Neither call returned a list."); return sb.ToString(); }

    targets = chosen.Select(id => Document.GetElement(id)).Where(e => e != null).ToList();

    // Grouped, because a flat list of 4000 ids says nothing about what you are about to lose.
    sb.AppendLine();
    sb.AppendLine("What Revit considers unused, by category:");
    sb.AppendLine("Category | Count | Examples");
    sb.AppendLine("--- | --- | ---");
    foreach (var g in targets.GroupBy(e => e.Category != null ? e.Category.Name : e.GetType().Name).OrderByDescending(g => g.Count()).Take(40))
        sb.AppendLine($"{g.Key} | {g.Count()} | {string.Join(", ", g.Take(3).Select(e => "'" + e.Name + "'"))}");
    sb.AppendLine();
    sb.AppendLine("This is Revit's own list and it is BROAD - every unloaded family type, every unused pattern.");
    sb.AppendLine("Read the categories above before setting dryRun = false. Removing all of it is what the native dialog does, and that is not always what you want.");
}
else if (mode == "materials")
{
    var usedMaterialIds = new HashSet<ElementId>();
    foreach (var e in new FilteredElementCollector(Document).WhereElementIsNotElementType())
    {
        // BOTH sets, always. false = geometry + compound structure, true = paint. A material that is
        // only ever painted onto faces appears in the second set alone; reading one set marks it unused.
        try
        {
            var geomIds = e.GetMaterialIds(false);
            if (geomIds != null) foreach (var id in geomIds) usedMaterialIds.Add(id);
        }
        catch { }
        try
        {
            var paintIds = e.GetMaterialIds(true);
            if (paintIds != null) foreach (var id in paintIds) usedMaterialIds.Add(id);
        }
        catch { }
    }

    targets = new FilteredElementCollector(Document).OfClass(typeof(Material))
        .Where(m => !usedMaterialIds.Contains(m.Id))
        .Cast<Element>().ToList();
}

if (targets == null)
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"view_templates\", \"filters\", \"materials\", or \"groups\". Nothing changed.");
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
