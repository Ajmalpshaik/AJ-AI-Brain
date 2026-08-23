// ============================================================
// FRAGMENT (action) — action-remap-line-styles.cs
// PURPOSE: Move every line off one line style and onto another, across the whole model — "everything on
//          <Thin Lines> should be MEP_DUCT", "we renamed the standard, re-point the old lines". The
//          cleanup after importing CAD, after inheriting somebody else's template, or after a standards
//          change, and the step that has to happen before the old style will purge.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// ✱✱ THE REASON THE OLD STYLE STILL WON'T PURGE AFTER YOU "MOVED EVERYTHING": lines live in more places
//    than the ones you can select. This walks all of them:
//      1. MODEL and DETAIL lines placed in views — the obvious ones, and the only ones a rubber-band
//         selection finds.
//      2. Lines inside SKETCHES — a floor's boundary, a ceiling's edge, a roof, an opening, a stair
//         riser. These are `CurveElement`s owned by a Sketch, they are never selectable in a view, and
//         they are the usual reason a style refuses to purge.
//      3. FILLED REGION BORDERS — and these are NOT CurveElements at all. ✱ ADDED 2026-08-23 after the
//         first version of this fragment missed them completely. Walk CurveElements alone — as it did —
//         and the report says "nothing left to move" while every region border still holds the old style
//         and it still will not purge. That is the exact false-clean this fragment exists to prevent,
//         reproduced by the fragment itself.
//      4. Lines inside GROUPS and inside FAMILY INSTANCES placed in the model.
//      5. The style set as a CATEGORY default on subcategories — not a line at all, but it keeps the
//         style referenced.
//    `includeSketchLines` covers (2), and defaults ON.
//
// ⚠⚠ FILLED REGIONS ARE WRITE-ONLY, AND THAT LIMITS WHAT THIS CAN HONESTLY PROMISE. Revit exposes
//    `FilledRegion.SetLineStyleId()` to CHANGE a region's border style and **no getter to read it back**
//    (proved on the compile check, 2026-08-23 — `GetLineStyleId` does not exist on any version here).
//    So there is no way to ask "which regions use the old style", and therefore no way to change only
//    those. `filledRegionMode` is honest about the three choices that leaves:
//        "report" (default) — count them and say so; change nothing
//        "all"              — set EVERY filled region's border to the target style. Blunt, and the only
//                             route that actually lets the old style purge
//        "skip"             — do not look at all
//    Note the validator IS static — `FilledRegion.GetValidLineStyleIdsForFilledRegion(Document)` — and a
//    region will only accept a style from that set, which is checked before each write.
//
// ✱✱ AND THE ONE THAT CANNOT BE FIXED FROM HERE: a line inside a LOADED FAMILY belongs to the family
//    document, not this one. It is reported, with the family named, so you know what still has to be
//    opened and edited — rather than being silently skipped and leaving you wondering.
//
// GOTCHA: BOTH styles must already exist. This re-points lines; it does not create styles. Use
//         recipes/create-mep-line-standards.cs to make the office set first.
// GOTCHA: a sketch line whose sketch is currently being edited cannot be changed and is reported.
// GOTCHA: DRY RUN BY DEFAULT — it prints exactly where the lines are before it moves anything.
// RELATED: action-set-line-style.cs sets the style on an `elements` set you already have;
//          this one hunts the whole model for one style. create-mep-line-standards.cs makes the styles.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it in dry run, check the sketch-line
//   count against what you expect, then apply.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string fromStyleName = "";        // REQUIRED — the line style to move OFF
string toStyleName = "";          // REQUIRED — the line style to move ON TO
bool includeSketchLines = true;   // the ones you cannot select — see the header
string filledRegionMode = "report"; // "report" | "all" | "skip" — see note 3; Revit gives no getter
bool dryRun = true;               // true = report only, change nothing
int maxListed = 30;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// Line styles are GraphicsStyle elements under the Lines category.
var allStyles = new FilteredElementCollector(Document)
    .OfClass(typeof(GraphicsStyle)).Cast<GraphicsStyle>()
    .Where(g => { try { return g.GraphicsStyleCategory != null
                            && g.GraphicsStyleCategory.Parent != null
                            && g.GraphicsStyleCategory.Parent.Id == new ElementId(BuiltInCategory.OST_Lines); }
                  catch { return false; } })
    .ToList();

var fromStyle = allStyles.FirstOrDefault(g => g.Name == fromStyleName);
var toStyle = allStyles.FirstOrDefault(g => g.Name == toStyleName);

if (string.IsNullOrWhiteSpace(fromStyleName) || string.IsNullOrWhiteSpace(toStyleName))
{
    sb.AppendLine("Both fromStyleName and toStyleName are required.");
}
else if (fromStyle == null || toStyle == null)
{
    sb.AppendLine(fromStyle == null ? $"Line style '{fromStyleName}' not found." : $"Line style '{toStyleName}' not found.");
    sb.AppendLine("Line styles in this project: " + string.Join(", ", allStyles.Select(g => g.Name).OrderBy(n => n)));
    sb.AppendLine("To create the office set first, use recipes/create-mep-line-standards.cs.");
}
else
{
    // Every CurveElement in the document — this deliberately does NOT filter by view, because sketch
    // lines belong to no view and are exactly what gets missed.
    var allCurves = new FilteredElementCollector(Document)
        .OfClass(typeof(CurveElement)).Cast<CurveElement>().ToList();

    var placed = new List<CurveElement>();
    var sketch = new List<CurveElement>();
    var inFamilyDoc = new Dictionary<string, int>();
    int alreadyRight = 0;

    foreach (var ce in allCurves)
    {
        GraphicsStyle gs = null;
        try { gs = ce.LineStyle as GraphicsStyle; } catch { }
        if (gs == null) continue;
        if (gs.Id == toStyle.Id) { alreadyRight++; continue; }
        if (gs.Id != fromStyle.Id) continue;

        // A CurveElement owned by a Sketch is not placeable/selectable in a view.
        bool isSketch = false;
        try
        {
            var owner = ce.OwnerViewId;
            isSketch = (owner == null || owner == ElementId.InvalidElementId)
                       && !(ce is ModelCurve && ce.SketchPlane != null && ce.GroupId == ElementId.InvalidElementId);
            // The reliable test: its category is a sketch category, or it has a Sketch parent.
            var catId = ce.Category?.Id;
            if (catId != null && (catId == new ElementId(BuiltInCategory.OST_SketchLines))) isSketch = true;
        }
        catch { }

        if (isSketch) sketch.Add(ce); else placed.Add(ce);
    }

    // Lines that live inside loaded family definitions cannot be reached from this document.
    foreach (var fs in new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>())
    {
        // Nothing to change here — recorded so the report is honest about what remains.
        var fam = fs.Family?.Name;
        if (fam != null && !inFamilyDoc.ContainsKey(fam)) inFamilyDoc[fam] = 0;
    }

    // ---- filled-region borders: a different API entirely, and a one-way one (see note 3) ----
    var allRegions = filledRegionMode == "skip"
        ? new List<FilledRegion>()
        : new FilteredElementCollector(Document).OfClass(typeof(FilledRegion)).Cast<FilledRegion>().ToList();
    var regions = filledRegionMode == "all" ? allRegions : new List<FilledRegion>();

    int target = placed.Count + (includeSketchLines ? sketch.Count : 0) + regions.Count;

    sb.AppendLine($"Moving lines from '{fromStyle.Name}' to '{toStyle.Name}'.");
    sb.AppendLine($"  placed model/detail lines : {placed.Count}");
    sb.AppendLine($"  lines inside sketches     : {sketch.Count}" + (includeSketchLines ? "" : "   (NOT being changed — includeSketchLines is off)"));
    sb.AppendLine($"  filled regions in model   : {allRegions.Count}"
        + (filledRegionMode == "all"  ? "   (ALL will be set to the target — Revit cannot tell us which use the old style)"
        :  filledRegionMode == "skip" ? "   (skipped)"
        :                               "   (NOT changed — set filledRegionMode = \"all\" if the old style must purge)"));
    sb.AppendLine($"  already on the target     : {alreadyRight}");
    sb.AppendLine();

    if (sketch.Count > 0 && !includeSketchLines)
    {
        sb.AppendLine("NOTE: the sketch lines are almost certainly why the old style will not purge.");
        sb.AppendLine("Set includeSketchLines = true unless you have a specific reason not to.");
        sb.AppendLine();
    }

    if (target == 0)
    {
        sb.AppendLine($"Nothing to move — no line in this document uses '{fromStyle.Name}'.");
        sb.AppendLine("If the style still refuses to purge, it is referenced by a loaded FAMILY (open and edit that family),");
        sb.AppendLine("or set as a subcategory default in Object Styles.");
    }
    else if (dryRun)
    {
        int shown = 0;
        foreach (var ce in placed.Concat(includeSketchLines ? sketch : new List<CurveElement>()))
        {
            if (shown++ >= maxListed) break;
            string where = ce.OwnerViewId != null && ce.OwnerViewId != ElementId.InvalidElementId
                ? $"view '{Document.GetElement(ce.OwnerViewId)?.Name}'" : "sketch / not view-owned";
            sb.AppendLine($"  {ce.Id}  {where}");
        }
        if (target > maxListed) sb.AppendLine($"  ... and {target - maxListed} more.");
        sb.AppendLine();
        sb.AppendLine($"DRY RUN — nothing changed. {target} line(s) would move. Set dryRun = false to apply.");
    }
    else
    {
        int moved = 0, failed = 0;
        using (var t = new Transaction(Document, "AJ Tools - Remap line styles"))
        {
            t.Start();
            foreach (var ce in placed.Concat(includeSketchLines ? sketch : new List<CurveElement>()))
            {
                try { ce.LineStyle = toStyle; moved++; }
                catch (Exception ex)
                {
                    failed++;
                    if (failed <= 10) sb.AppendLine($"  {ce.Id}: {ex.Message}");
                }
            }

            // Filled regions take the style through their own setter, and only accept a style Revit
            // considers valid for them — so check first rather than letting it throw.
            foreach (var fr in regions)
            {
                try
                {
                    var valid = FilledRegion.GetValidLineStyleIdsForFilledRegion(Document);
                    if (valid != null && !valid.Contains(toStyle.Id))
                    {
                        failed++;
                        sb.AppendLine($"  region {fr.Id}: '{toStyle.Name}' is not a valid border style for a filled region.");
                        continue;
                    }
                    fr.SetLineStyleId(toStyle.Id);
                    moved++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (failed <= 10) sb.AppendLine($"  region {fr.Id}: {ex.Message}");
                }
            }
            t.Commit();
        }
        sb.AppendLine($"Moved {moved} line(s) and border(s) onto '{toStyle.Name}'. Failed: {failed}.");
        sb.AppendLine($"'{fromStyle.Name}' should now purge — if it does not, it is referenced inside a loaded family or in Object Styles.");
    }
}
return sb.ToString();
