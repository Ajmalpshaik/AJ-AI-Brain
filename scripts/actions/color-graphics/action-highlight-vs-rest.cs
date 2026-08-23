// ============================================================
// FRAGMENT (action) — action-highlight-vs-rest.cs
// PURPOSE: Color every element ALREADY IN THE ACTIVE VIEW gray, except `elements` (the filtered
//          highlight subset, from a filter fragment above), which gets its own highlight color instead.
//          Different from action-set-color-uniform.cs (colors only `elements`, leaves everything else
//          untouched) and action-color-by-group.cs (splits `elements` itself into many colored
//          sub-groups) — this one is specifically "make ONE subset stand out against a dimmed rest of
//          the whole model."
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
//          `elements` should be a SUBSET of the active view's content (e.g. one family within a
//          category) — if it's the whole model already there's nothing left to gray out.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: verified live 2026-07-15 — "color VCD red, everything else in the model gray" (3761 grayed,
//         35 red, 0 skipped, 3796 total elements in the active view).
//
// ✱ UPGRADED 2026-08-22 (harvested from the add-in's Highlight Selection tool): INSULATION AND LINING
//   NOW TRAVEL WITH THEIR HOST, BOTH DIRECTIONS. Without this, highlighting an insulated duct leaves
//   the insulation sleeve grey — so on screen the duct you asked to see is still wrapped in dimmed
//   material and reads as half-highlighted. Both directions matter:
//     - picking the WRAP pulls in the duct/pipe underneath it (`wrap.HostElementId`)
//     - picking the HOST pulls in its insulation AND its lining
//       (`InsulationLiningBase.GetInsulationIds` / `GetLiningIds`)
//   `DuctInsulation`, `DuctLining` and `PipeInsulation` all derive from `InsulationLiningBase`; there
//   is no `PipeLining` class in any Revit version, so those three are the whole set.
// GOTCHA: `GetInsulationIds`/`GetLiningIds` THROW `ArgumentException` for an id that cannot host a
//         wrap — so catching per element IS the category filter. Do not pre-filter by category and do
//         not let one throw kill the loop.
// GOTCHA: only wraps ALREADY IN THE VIEW are pulled in. A wrap outside the view has nothing to
//         override and adding it would inflate the reported count with elements nobody can see.
// SOURCE: insulation expansion verified live 2026-08-23 on Revit 2020 — "red only VCD, remaining all
//         gray" on a 2,134-element view: 41 VCDs matched, 40 duct-insulation sleeves followed their
//         hosts, 81 red / 2,053 gray / 0 skipped. One VCD carried no insulation, which is why the
//         wrap count is 40 and not 41 — an unequal count is normal, not a miss.
// ⚠ LEAVE `expandInsulationAndLining = true`. Ajmal's standing rule, 2026-08-23: *"if you are coloring
//   the ducts or duct accessorys or any thing it means if the items there is linsilation you have to
//   color the isulation also other vise we ccant see."* Turning it off is what makes a highlight look
//   half-applied on screen — see ../../../knowledge/live-model/insulation-follows-host.md.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
byte highlightR = 255, highlightG = 0, highlightB = 0;   // e.g. red
byte restR = 128, restG = 128, restB = 128;              // e.g. gray
bool expandInsulationAndLining = true;                   // insulation/lining travels WITH its host — see the header
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

var view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    var solidFillPattern = new FilteredElementCollector(Document)
        .OfClass(typeof(FillPatternElement))
        .Cast<FillPatternElement>()
        .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

    OverrideGraphicSettings MakeOgs(Autodesk.Revit.DB.Color c)
    {
        var ogs = new OverrideGraphicSettings();
        ogs.SetProjectionLineColor(c);
        ogs.SetCutLineColor(c);
        if (solidFillPattern != null)
        {
            ogs.SetSurfaceForegroundPatternColor(c);
            ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
            ogs.SetSurfaceForegroundPatternVisible(true);
            ogs.SetCutForegroundPatternColor(c);
            ogs.SetCutForegroundPatternId(solidFillPattern.Id);
            ogs.SetCutForegroundPatternVisible(true);
        }
        return ogs;
    }

    var highlightOgs = MakeOgs(new Autodesk.Revit.DB.Color(highlightR, highlightG, highlightB));
    var restOgs = MakeOgs(new Autodesk.Revit.DB.Color(restR, restG, restB));
    var highlightIds = new HashSet<ElementId>(elements.Select(e => e.Id));

    var allInView = new FilteredElementCollector(Document, view.Id)
        .WhereElementIsNotElementType()
        .ToElements();

    // ---- insulation and lining travel WITH their host, both directions (see the header) ----
    int wrapsPulledIn = 0;
    if (expandInsulationAndLining)
    {
        var inView = new HashSet<ElementId>(allInView.Select(e => e.Id));
        var pending = new List<ElementId>(highlightIds);

        // Direction 1 — a picked WRAP pulls in the duct/pipe underneath it.
        foreach (var id in pending.ToList())
        {
            var wrap = Document.GetElement(id) as Autodesk.Revit.DB.InsulationLiningBase;
            if (wrap == null) continue;
            var hostId = wrap.HostElementId;
            if (hostId == null || hostId == ElementId.InvalidElementId) continue;
            if (inView.Contains(hostId) && highlightIds.Add(hostId)) { pending.Add(hostId); wrapsPulledIn++; }
        }

        // Direction 2 — every highlighted HOST (including hosts just added) pulls in its wraps.
        // Index-capped: a wrap appended here can't host anything itself, so it needs no pass of its own.
        int hostCandidates = pending.Count;
        for (int i = 0; i < hostCandidates; i++)
        {
            foreach (var getIds in new Func<Document, ElementId, ICollection<ElementId>>[] {
                         Autodesk.Revit.DB.InsulationLiningBase.GetInsulationIds,
                         Autodesk.Revit.DB.InsulationLiningBase.GetLiningIds })
            {
                ICollection<ElementId> wrapIds = null;
                // Revit THROWS ArgumentException for an id that can't host insulation/lining, so this
                // catch IS the category filter — no need to pre-check the category.
                try { wrapIds = getIds(Document, pending[i]); } catch { continue; }
                if (wrapIds == null) continue;
                foreach (var wid in wrapIds)
                    if (inView.Contains(wid) && highlightIds.Add(wid)) wrapsPulledIn++;
            }
        }
    }

    int highlightCount = 0, restCount = 0, skipCount = 0;

    using (var t = new Transaction(Document, "AJ Tools - Highlight Vs Rest"))
    {
        t.Start();
        try
        {
            foreach (var e in allInView)
            {
                bool isHighlight = highlightIds.Contains(e.Id);
                try
                {
                    view.SetElementOverrides(e.Id, isHighlight ? highlightOgs : restOgs);
                    if (isHighlight) highlightCount++; else restCount++;
                }
                catch
                {
                    skipCount++; // element/category doesn't support graphic overrides in this view — skip, don't fail the whole batch
                }
            }
            t.Commit();
            sb.AppendLine($"Highlighted {highlightCount} element(s) RGB({highlightR},{highlightG},{highlightB}), grayed {restCount} RGB({restR},{restG},{restB}) in view '{view.Name}' ({skipCount} skipped)." + (wrapsPulledIn > 0 ? $" {wrapsPulledIn} insulation/lining element(s) followed their host into the highlight." : ""));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to highlight vs rest — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
