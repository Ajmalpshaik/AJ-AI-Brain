// ============================================================
// FRAGMENT (action) — action-add-remove-insulation.cs
// PURPOSE: Add or remove insulation (or duct lining) on the ducts/pipes in `elements` — the WRITE
//          counterpart to filter-by-insulation-status.cs / filter-by-insulation-type.cs, which can only
//          find it. Revit's Modify > Add Insulation / Add Lining, scripted.
// ASSUMES: elements (List<Element>, Ducts and/or Pipes — fittings/accessories also accepted where Revit
//          allows) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: an element can hold only ONE insulation — already-insulated elements are skipped and reported
//         on add (remove first to change type/thickness).
// GOTCHA: kind="lining" is DUCT-only (DuctLining); pipes are skipped and reported.
// LIVE-VERIFIED 2026-08-14, twice on the same day by two sessions — first on a scratch model (5 ducts:
//          add 5, re-add skip 5, remove 5), then again on the current test model, each result read back
//          from a SEPARATE bridge call. Second run: add 3 ducts -> 3 DuctInsulation, type "Rigid Fiber
//          Board", 25.0 mm; skip path over one insulated + one bare duct returned exactly "added 1,
//          skipped 1"; remove on 2 of the 4 left exactly the other 2 and all 17 ducts standing.
// WAS WRONGLY MARKED BLOCKED until 2026-08-14, reason recorded so the mistake is not repeated: the note
//          said "no insulation fixture in this model". But this fragment CREATES insulation — it never
//          needed one. All it needs is an insulation TYPE, and the stock template ships six
//          (2 DuctInsulationType, 2 PipeInsulationType, 2 DuctLiningType). It was runnable all along.
//          Same false blocker had stalled filter-by-insulation-status.cs and filter-by-insulation-type.cs,
//          which this fragment's output then cleared in the same session.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "add";            // "add" | "remove"
string kind = "insulation";     // "insulation" | "lining" (lining = ducts only)
string insulationTypeName = null; // add only — exact type name; null = first matching type in project
double thicknessMm = 25;        // add only
// ---- END INPUTS ----

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

if (mode == "add")
{
    // resolve the type per domain lazily — duct insulation, pipe insulation, and duct lining are 3 different type classes
    var ductInsTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctInsulationType)).Cast<ElementType>().ToList();
    var pipeInsTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeInsulationType)).Cast<ElementType>().ToList();
    var ductLinTypes = new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctLiningType)).Cast<ElementType>().ToList();

    Func<List<ElementType>, ElementType> pick = list =>
        insulationTypeName == null ? list.FirstOrDefault() : list.FirstOrDefault(x => x.Name == insulationTypeName);

    int added = 0, skipped = 0;
    var notes = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Add Insulation/Lining"))
    {
        t.Start();
        try
        {
            foreach (var el in elements)
            {
                bool isDuct = el is Autodesk.Revit.DB.Mechanical.Duct;
                bool isPipe = el is Autodesk.Revit.DB.Plumbing.Pipe;
                if (!isDuct && !isPipe) { skipped++; notes.Add($"Id {el.Id.IntegerValue}: not a Duct/Pipe"); continue; }

                // one insulation/lining per element — skip if it already has one of the requested kind
                bool hasAlready = new FilteredElementCollector(Document).OfClass(typeof(InsulationLiningBase))
                    .Cast<InsulationLiningBase>().Any(i => i.HostElementId == el.Id &&
                        (kind == "lining") == (i is Autodesk.Revit.DB.Mechanical.DuctLining));
                if (hasAlready) { skipped++; notes.Add($"Id {el.Id.IntegerValue}: already has {kind}"); continue; }

                try
                {
                    if (kind == "insulation" && isDuct)
                    {
                        var ty = pick(ductInsTypes);
                        if (ty == null) { skipped++; notes.Add("no DuctInsulationType in project"); continue; }
                        Autodesk.Revit.DB.Mechanical.DuctInsulation.Create(Document, el.Id, ty.Id, mm(thicknessMm));
                        added++;
                    }
                    else if (kind == "insulation" && isPipe)
                    {
                        var ty = pick(pipeInsTypes);
                        if (ty == null) { skipped++; notes.Add("no PipeInsulationType in project"); continue; }
                        Autodesk.Revit.DB.Plumbing.PipeInsulation.Create(Document, el.Id, ty.Id, mm(thicknessMm));
                        added++;
                    }
                    else if (kind == "lining" && isDuct)
                    {
                        var ty = pick(ductLinTypes);
                        if (ty == null) { skipped++; notes.Add("no DuctLiningType in project"); continue; }
                        Autodesk.Revit.DB.Mechanical.DuctLining.Create(Document, el.Id, ty.Id, mm(thicknessMm));
                        added++;
                    }
                    else { skipped++; notes.Add($"Id {el.Id.IntegerValue}: lining is duct-only"); }
                }
                catch (Exception exOne) { skipped++; notes.Add($"Id {el.Id.IntegerValue}: {exOne.Message}"); }
            }
            t.Commit();
            sb.AppendLine($"Added {kind} ({thicknessMm} mm) to {added} element(s), {skipped} skipped, of {elements.Count}.");
            if (notes.Count > 0) sb.AppendLine("  " + string.Join("; ", notes.Take(20)) + (notes.Count > 20 ? $"; ... +{notes.Count - 20} more" : ""));
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED mid-add — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
else if (mode == "remove")
{
    var hostIds = new HashSet<int>(elements.Select(e => e.Id.IntegerValue));
    var toDelete = new FilteredElementCollector(Document).OfClass(typeof(InsulationLiningBase))
        .Cast<InsulationLiningBase>()
        .Where(i => hostIds.Contains(i.HostElementId.IntegerValue))
        .Where(i => kind == "lining" ? i is Autodesk.Revit.DB.Mechanical.DuctLining : !(i is Autodesk.Revit.DB.Mechanical.DuctLining))
        .Select(i => i.Id)
        .ToList();

    if (toDelete.Count == 0) sb.AppendLine($"No {kind} found on the {elements.Count} element(s) — nothing to remove.");
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Remove Insulation/Lining"))
        {
            t.Start();
            try
            {
                Document.Delete(toDelete);
                t.Commit();
                sb.AppendLine($"Removed {toDelete.Count} {kind} element(s) from the set of {elements.Count}.");
            }
            catch (Exception ex)
            {
                try { t.RollBack(); } catch { }
                sb.AppendLine($"FAILED to remove {kind} — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"add\" or \"remove\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
