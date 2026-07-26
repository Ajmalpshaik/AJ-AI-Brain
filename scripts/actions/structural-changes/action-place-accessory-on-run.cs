// ============================================================
// FRAGMENT (action) — action-place-accessory-on-run.cs
// PURPOSE: Insert a duct/pipe ACCESSORY (VCD, fire damper, valve, strainer...) INTO each existing run in
//          `elements` — Revit breaks the duct/pipe at the insertion point and connects both cut ends to
//          the accessory automatically. The everyday MEP job that had no fragment until now.
// ASSUMES: elements (List<Element>, Ducts and/or Pipes — from any filter) and sb exist from a filter above.
// PRODUCES: newAccessoryIds (List<ElementId>) for chaining (tag them, set parameters, colour them).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: the accessory family must be the matching category and domain — Duct Accessories for ducts,
//         Pipe Accessories for pipes — and must have TWO connectors of that domain, or Revit places it as
//         a loose instance instead of breaking into the run. Wrong-domain families are skipped+reported.
// GOTCHA: position is a fraction along each run's own length (0.5 = middle) or a fixed mm from its start.
//         Very close to an end there may be no room to break — that run is skipped and reported.
// GOTCHA: this MODIFIES existing runs (each becomes two segments joined by the accessory) — explorer-first
//         applies: run the filter alone, confirm the count, then attach this.
// FLAGGED 2026-07-26 (static review): the break-into-run behaviour comes from the
//         NewFamilyInstance(XYZ, FamilySymbol, Element host, StructuralType) overload with the MEPCurve as
//         host. If the first live run places loose instances instead of breaking in, that's this overload
//         or a family with the wrong connector count — not the transaction logic.
// NOT YET LIVE-VERIFIED — created 2026-07-26, round 4.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string accessoryFamilyName = "";   // e.g. the office VCD family — must already be loaded (load-family.cs)
string accessoryTypeName = null;   // null = first type of that family
string positionMode = "fraction";  // "fraction" (along each run) | "mm_from_start"
double positionValue = 0.5;        // 0.5 = midpoint; or mm when positionMode = "mm_from_start"
// ---- END INPUTS ----

Func<double, double> mm = v => UnitUtils.ConvertToInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> toMm = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_MILLIMETERS);

var newAccessoryIds = new List<ElementId>();

var symbol = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
    .Where(s => s.FamilyName == accessoryFamilyName)
    .Where(s => accessoryTypeName == null || s.Name == accessoryTypeName)
    .FirstOrDefault();

var runs = elements.OfType<MEPCurve>().ToList();

if (symbol == null)
{
    sb.AppendLine($"Accessory family '{accessoryFamilyName}'{(accessoryTypeName != null ? $" type '{accessoryTypeName}'" : "")} not found — load it first (creators/load-family.cs).");
}
else if (runs.Count == 0)
{
    sb.AppendLine("No Ducts/Pipes in `elements` — nothing to insert into.");
}
else
{
    // domain sanity: duct accessory into duct, pipe accessory into pipe
    var catId = symbol.Category != null ? (BuiltInCategory)symbol.Category.Id.IntegerValue : BuiltInCategory.INVALID;
    bool isDuctAcc = catId == BuiltInCategory.OST_DuctAccessory || catId == BuiltInCategory.OST_DuctFitting;
    bool isPipeAcc = catId == BuiltInCategory.OST_PipeAccessory || catId == BuiltInCategory.OST_PipeFitting;
    if (!isDuctAcc && !isPipeAcc)
        sb.AppendLine($"NOTE: '{symbol.FamilyName}' is category '{symbol.Category?.Name ?? "?"}' — not a Duct/Pipe Accessory. It may place loose instead of breaking into the run (see header).");

    using (var t = new Transaction(Document, "AJ Tools - Place Accessory On Run"))
    {
        t.Start();
        try
        {
            if (!symbol.IsActive) { symbol.Activate(); Document.Regenerate(); }

            int placed = 0, skipped = 0;
            var notes = new List<string>();

            foreach (var run in runs)
            {
                bool runIsDuct = run is Autodesk.Revit.DB.Mechanical.Duct;
                if ((runIsDuct && isPipeAcc) || (!runIsDuct && isDuctAcc))
                {
                    skipped++;
                    notes.Add($"Id {run.Id.IntegerValue}: domain mismatch ({(runIsDuct ? "duct" : "pipe")} run vs {(isDuctAcc ? "duct" : "pipe")} accessory)");
                    continue;
                }

                var lc = run.Location as LocationCurve;
                var line = lc?.Curve as Line;
                if (line == null) { skipped++; notes.Add($"Id {run.Id.IntegerValue}: run is not a straight line"); continue; }

                double length = line.Length;
                double along = positionMode == "mm_from_start" ? mm(positionValue) : length * positionValue;
                // keep clear of both ends — nothing to break into right at a connector
                double margin = Math.Min(mm(50), length * 0.1);
                if (along < margin || along > length - margin)
                {
                    skipped++;
                    notes.Add($"Id {run.Id.IntegerValue}: position {toMm(along):F0} mm too close to an end (run is {toMm(length):F0} mm)");
                    continue;
                }

                var point = line.GetEndPoint(0) + line.Direction.Normalize() * along;

                try
                {
                    var inst = Document.Create.NewFamilyInstance(
                        point, symbol, run, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    newAccessoryIds.Add(inst.Id);
                    placed++;
                }
                catch (Exception exOne)
                {
                    skipped++;
                    notes.Add($"Id {run.Id.IntegerValue}: {exOne.Message}");
                }
            }

            t.Commit();
            sb.AppendLine($"Placed {placed} '{symbol.FamilyName} : {symbol.Name}' accessor{(placed == 1 ? "y" : "ies")} into {runs.Count} run(s), {skipped} skipped.");
            if (newAccessoryIds.Count > 0)
                sb.AppendLine($"  New accessory Ids: {string.Join(", ", newAccessoryIds.Take(30).Select(i => i.IntegerValue.ToString()))}{(newAccessoryIds.Count > 30 ? ", ..." : "")}");
            if (notes.Count > 0)
                sb.AppendLine("  Skipped: " + string.Join("; ", notes.Take(20)) + (notes.Count > 20 ? $"; ... +{notes.Count - 20} more" : ""));
            sb.AppendLine("  Verify each run really broke in two around its accessory — action-report-connectors.cs on the new Ids.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED mid-placement — rolled back, NO accessories placed and no runs modified. Reason: {ex.Message}");
            newAccessoryIds.Clear();
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
