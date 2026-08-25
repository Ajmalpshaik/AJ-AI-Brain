// ============================================================
// RECIPE — sprinkler-place-heads.cs
// PURPOSE: Place real sprinkler family instances at a list of computed centres, at a stated height, and
//          then READ THE PLACED HEADS BACK OUT OF THE MODEL and report what is actually there. The last
//          step of the layout chain — everything before it produces numbers, this produces elements.
// STANDALONE — has its own sb and returns. WRITES to the model when confirmPlacement is true.
// SOURCE: ../../knowledge/fire-sprinkler/revit-modelling.md    (hosted vs unhosted, the origin trap, the schedule)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md      (step 7, and why the read-back is not optional)
//
// *** THE READ-BACK IS THE POINT. *** This Brain has caught FOUR separate "silent success" bugs — scripts
//   that reported "N placed, 0 skipped" while the model held something else. A script's own report of what
//   it did is not evidence that it did it. So this fragment re-collects the sprinklers from the document
//   after committing and reports the count, the positions and the heights it can actually see. If those
//   two halves disagree, believe the read-back. Better still: run a THIRD check from a separate bridge
//   call (recipes/sprinkler-compliance-audit.cs).
//
// HOSTED OR NOT — and the wrong choice fails in the case you most need it:
//   unhosted / level-based  -> NewFamilyInstance(point, symbol, level, NonStructural). Always works.
//                              The height is then a parameter you set, and it does not follow the ceiling.
//   face-based / ceiling    -> needs a real Reference to a face. Better coordination — the head moves with
//                              the ceiling — but it fails outright where there is NO ceiling, which is
//                              exactly the exposed-slab car park case. Do not pick hosted for a car park.
//   This fragment does the UNHOSTED path, because it is the one that always works and the one this
//   library has already proven live. For ceiling-hosted placement, use the face-based overload and expect
//   to resolve a Reference per point — that is a different job, not a flag on this one.
//
// GOTCHA: **an unactivated FamilySymbol places nothing and does not error.** `if (!symbol.IsActive)
//         symbol.Activate();` inside the transaction, before the first placement. Silent no-op otherwise.
// GOTCHA: **the height parameter differs by family.** 'Offset from Host', 'Elevation from Level',
//         'Offset' — read it off one head before writing a batch. A blank height reads as zero and puts
//         every head on the floor.
// GOTCHA: **the family origin is not the deflector.** The Z here places the family's insertion point.
//         If the code dimension you are working to is to the deflector plate, offset it — see
//         originToDeflectorMm in recipes/sprinkler-deflector-height.cs.
// GOTCHA: placing 40 heads is a bulk change. START-HERE rule 5: state what will happen and how many, and
//         wait for a clear go-ahead. confirmPlacement exists so that cannot be skipped by accident.
// GOTCHA: never Document.Regenerate() after Commit() — illegal, and it surfaces as a HANG, not an error.
//
// GOTCHA — PROVEN 2026-08-20, AND IT IS THE ONE THAT BITES: **the Z of the placement point is NOT
//         honoured on a OneLevelBased family.** Asked for Z = 2,400 mm via
//         `NewFamilyInstance(new XYZ(x, y, mm(2400)), sym, level, NonStructural)` and the head landed at
//         **2,500 mm** — the family's own default elevation won, silently, with no error and no warning.
//         Every head would have been 100 mm out and the script would have reported success.
//         THE FIX, and it is proven to work: after creating each instance, write the height explicitly —
//           var ep = fi.LookupParameter("Elevation from Level");
//           if (ep != null && !ep.IsReadOnly) ep.Set(mm(targetZmm));
//         then READ IT BACK. On the 38-head batch this gave min 2,244 / max 2,244 mm, 0 heads adrift.
//         'Offset from Host' mirrors 'Elevation from Level' on an unhosted family — either can be read,
//         but write the one you then verify.
//
// GOTCHA — PROVEN 2026-08-20: **find the deflector by MEASURING the family, never by assuming an end.**
//         See knowledge/fire-sprinkler/revit-modelling.md for the method (slice the solid, find the
//         widest disc) and for the RASCO F156 case where a type named "CONVENTIONAL" turned out to be
//         modelled as an UPRIGHT with the deflector 56 mm ABOVE the origin. Placing it as if the origin
//         were the deflector puts the head 56 mm wrong, and the sign of the error depends on the family.
//
// STATUS: LIVE-VERIFIED 2026-08-20 on Revit 2020 (model 'Project1'). 38 heads placed across 4 rooms in
//   one transaction, 0 failed, using RASCO F156 (OneLevelBased, so the UNHOSTED path this fragment
//   takes). The read-back is what earned its keep: it caught the elevation bug above on the pilot head
//   before the batch ran. Read-back confirmed 38 in the model, all at Z 2,244 mm, and
//   Room.IsPointInRoom put 6 / 9 / 9 / 14 in the four rooms exactly as planned. A third check from a
//   SEPARATE bridge call (recipes/sprinkler-compliance-audit.cs) then agreed — 0 failures.
//   Place ONE head first and measure it. On this run the pilot found two separate faults.
//
// ✱✱ FIXED 2026-08-24 — LEVEL HEIGHTS HERE NOW USE `ProjectElevation`, NOT `Elevation`.
//    A level has two heights. `Elevation` is measured from whatever the level type's "Elevation Base"
//    parameter says (Project OR Shared); `ProjectElevation` is always from the project origin, which is
//    the space every XYZ in the model lives in. This fragment mixes a level height with real
//    coordinates, so on a model with a survey offset the old code was wrong by exactly that offset —
//    silently, with a plausible number and no error. See
//    knowledge/live-model/level-elevation-vs-project-elevation.md, and run
//    action-report-level-elevations.cs to see whether a given model is affected.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool confirmPlacement = false;        // MUST be set true deliberately. False = report the plan and stop.
int roomIdInt = 0;                    // for the read-back check (which heads landed in this room)
string familyNameContains = "";       // e.g. "Sprinkler - Pendent" — leave blank to match any family
string typeNameContains = "";         // narrow to one type within the family
int levelIdInt = 0;                   // the level to place on — REQUIRED
double placementZmm = 0;              // the insertion-point Z, project coordinates. From
                                      // recipes/sprinkler-deflector-height.cs, not from memory.
string heightParameterName = "";      // optional: also write the height into this parameter
double heightParameterValueMm = 0;    // what to write into it

List<(double xMm, double yMm)> pointsMm = new List<(double, double)>
{
    // paste the HEAD CENTRES block from recipes/sprinkler-nfpa-grid.cs (after the obstruction check has passed)
};

// --- the head schedule data. A placed head with no data is a dot on a drawing.
string hazardLabel = "";              // the hazard class this layout was computed for — record it somewhere
string constructionLabel = "";        // and the construction type
int maxListed = 100;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;
Func<double, double> mm = v => v / 304.8;

var level = levelIdInt == 0 ? null : Document.GetElement(new ElementId(levelIdInt)) as Level;
var room = roomIdInt == 0 ? null : Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;

var symbol = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
    .FirstOrDefault(fs =>
        (string.IsNullOrEmpty(familyNameContains) || fs.Family.Name.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
        && (string.IsNullOrEmpty(typeNameContains) || fs.Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0));

if (level == null) { sb.AppendLine("NOT RUN — levelIdInt is not set, or is not a Level."); }
else if (pointsMm.Count == 0) { sb.AppendLine("NOT RUN — pointsMm is empty. Paste the centres from recipes/sprinkler-nfpa-grid.cs."); }
else if (symbol == null)
{
    sb.AppendLine($"NOT RUN — no FamilySymbol matches family '{familyNameContains}' / type '{typeNameContains}'.");
    sb.AppendLine("  List what is loaded: context/context-used-families.cs. A sprinkler family lives in the");
    sb.AppendLine("  Sprinklers category — if nothing matches, it may not be loaded at all (creators/load-family.cs).");
}
else
{
    string catName = symbol.Category != null ? symbol.Category.Name : "(no category)";
    sb.AppendLine($"PLACE SPRINKLER HEADS — {pointsMm.Count:N0} position(s)");
    sb.AppendLine($"  family '{symbol.Family.Name}' type '{symbol.Name}', category {catName}");
    sb.AppendLine($"  level '{level.Name}' ({toMm(level.ProjectElevation):N0} mm), insertion Z {placementZmm:N0} mm");
    if (!string.IsNullOrWhiteSpace(hazardLabel) || !string.IsNullOrWhiteSpace(constructionLabel))
        sb.AppendLine($"  computed for: {hazardLabel} | {constructionLabel}");
    else
    {
        sb.AppendLine("  *** hazardLabel and constructionLabel are blank. A head count with no hazard class is meaningless,");
        sb.AppendLine("      and it is the first thing a reviewer asks for. Record them somewhere on this layout.");
    }
    if (catName != "Sprinklers")
        sb.AppendLine($"  *** WARNING: this family is in '{catName}', not Sprinklers. A fire schedule filtered by category will miss it.");

    if (!confirmPlacement)
    {
        sb.AppendLine();
        sb.AppendLine($"  NOT PLACED — confirmPlacement is false. This would create {pointsMm.Count:N0} element(s).");
        sb.AppendLine("  Show Ajmal that count, get a clear go-ahead, then set confirmPlacement = true.");
        int shown = 0;
        foreach (var p in pointsMm)
        {
            if (shown++ >= maxListed) break;
            sb.AppendLine($"    {shown,3}. {p.xMm:N0}, {p.yMm:N0} mm");
        }
    }
    else
    {
        var placedIds = new List<ElementId>();
        int failed = 0;
        using (var t = new Transaction(Document, "AJ Tools - Place Sprinkler Heads"))
        {
            t.Start();
            try
            {
                if (!symbol.IsActive) symbol.Activate();     // silent no-op placement without this
                foreach (var p in pointsMm)
                {
                    try
                    {
                        var pt = new XYZ(mm(p.xMm), mm(p.yMm), mm(placementZmm));
                        var fi = Document.Create.NewFamilyInstance(
                            pt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        if (fi != null)
                        {
                            placedIds.Add(fi.Id);
                            if (!string.IsNullOrWhiteSpace(heightParameterName))
                            {
                                var prm = fi.LookupParameter(heightParameterName);
                                if (prm != null && !prm.IsReadOnly) prm.Set(mm(heightParameterValueMm));
                            }
                        }
                        else failed++;
                    }
                    catch { failed++; }
                }
                t.Commit();
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED (place) — rolled back, nothing changed. Reason: {ex.Message}");
                placedIds.Clear();
            }
        }

        if (placedIds.Count > 0 || failed > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  REPORTED: {placedIds.Count:N0} placed, {failed:N0} failed.");

            // ---- THE READ-BACK. Do not trust the line above. ----
            double zProbe = room != null ? room.Level.ProjectElevation + mm(1000) : 0;
            Func<XYZ, bool> insideRoom = p =>
            {
                if (room == null) return true;
                bool i = false; try { i = room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe)); } catch { }
                return i;
            };

            int seen = 0, inRoom = 0, offTarget = 0, offHeight = 0;
            double worstOffMm = 0, worstOffZmm = 0;
            foreach (var id in placedIds)
            {
                var el = Document.GetElement(id);
                if (el == null) continue;
                seen++;
                var lp = el.Location as LocationPoint;
                if (lp == null) continue;
                if (insideRoom(lp.Point)) inRoom++;
                // did it land where it was asked to?
                double best = double.MaxValue;
                foreach (var p in pointsMm)
                {
                    double dx = toMm(lp.Point.X) - p.xMm, dy = toMm(lp.Point.Y) - p.yMm;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < best) best = d;
                }
                if (best > 1.0) { offTarget++; if (best > worstOffMm) worstOffMm = best; }
                // and did it land at the HEIGHT asked for? This is the check the header's own GOTCHA
                // exists for (the family default Z winning silently) and until 2026-08-25 the read-back
                // compared X and Y only — the one proven failure mode was the one it could not see.
                // When heightParameterName is set, that parameter legitimately overrides the placement
                // Z, so the expected height is the level plus the written value, not placementZmm.
                double expectZmm = string.IsNullOrWhiteSpace(heightParameterName)
                    ? placementZmm
                    : toMm(level.ProjectElevation) + heightParameterValueMm;
                double dz = Math.Abs(toMm(lp.Point.Z) - expectZmm);
                if (dz > 1.0) { offHeight++; if (dz > worstOffZmm) worstOffZmm = dz; }
            }

            sb.AppendLine($"  READ BACK from the document: {seen:N0} of those Ids still exist"
                + (room != null ? $", {inRoom:N0} inside '{room.Name}'" : ""));
            if (seen != placedIds.Count)
                sb.AppendLine($"  *** {placedIds.Count - seen:N0} placed Id(s) are NOT in the document. The report above was wrong —"
                    + " believe this line, and find out why before placing more.");
            if (offTarget > 0)
                sb.AppendLine($"  *** {offTarget:N0} head(s) are not at the position asked for (worst {worstOffMm:N0} mm out)."
                    + " Something moved them — a host, a snap, or a workplane. Look before continuing.");
            if (offHeight > 0)
                sb.AppendLine($"  *** {offHeight:N0} head(s) are not at the HEIGHT asked for (worst {worstOffZmm:N0} mm out)."
                    + " This is the family-default-elevation trap from the header — set heightParameterName"
                    + " to the family's own height parameter and re-run.");
            if (string.IsNullOrWhiteSpace(heightParameterName))
                sb.AppendLine("  *** heightParameterName is blank, so the placement height is NOT being enforced through the"
                    + " family's own parameter — only the Z read-back above stands between you and the family default.");
            if (seen == placedIds.Count && offTarget == 0 && offHeight == 0 && failed == 0)
                sb.AppendLine("  Placed count, surviving Ids, positions and heights all agree.");

            sb.AppendLine();
            sb.AppendLine("  NEXT, from a SEPARATE bridge call: recipes/sprinkler-compliance-audit.cs on this room.");
            sb.AppendLine("  That reads the heads out of the model with fresh eyes and re-tests every spacing limit —");
            sb.AppendLine("  which is the only evidence that what was placed is what was designed.");
        }
    }
}

return sb.ToString();
