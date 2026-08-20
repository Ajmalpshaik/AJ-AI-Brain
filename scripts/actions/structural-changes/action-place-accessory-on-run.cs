// ============================================================
// FRAGMENT (action) — action-place-accessory-on-run.cs
// PURPOSE: Insert a duct/pipe ACCESSORY (VCD, fire damper, valve, strainer...) INTO each existing run in
//          `elements` — the run is cut in two and both cut ends are connected to the accessory, so the
//          system stays continuous. The everyday MEP job.
// ASSUMES: elements (List<Element>, Ducts and/or Pipes — from any filter) and sb exist from a filter above.
// PRODUCES: newAccessoryIds (List<ElementId>) for chaining (tag them, set parameters, colour them).
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// REWRITTEN 2026-08-14 after the first live run DISPROVED the original approach.
// The original called NewFamilyInstance(point, symbol, run, StructuralType) and assumed Revit would
// break the run automatically, the way the UI command does. IT DOES NOT. Measured live: the duct count
// did not change, `fi.Host` came back null, and the accessory sat LOOSE on top of the duct with 0 of its
// 2 connectors joined. Nothing errored — it reported success. The 2026-07-26 static review had flagged
// exactly this risk; the live run confirmed it, and confirmed it is the OVERLOAD, not the family.
//
// THE WORKING SEQUENCE, each step verified live:
//   1. MechanicalUtils.BreakCurve (ducts) / PlumbingUtils.BreakCurve (pipes) at the insertion point.
//   2. Place the accessory at that point.
//   3. MATCH THE ACCESSORY'S SIZE TO THE RUN  <- the step whose absence caused everything else.
//   4. ConnectTo each half's free end, pairing by DISTANCE.
//
// GOTCHA — SIZE MISMATCH IS THE WHOLE BUG. A freshly placed accessory takes its FAMILY DEFAULT size, not
//          the run's. On a 300x300 duct that default happened to be 300x300 and looked like Revit had
//          matched it — a coincidence that hid the problem. On a 200x200 duct the same family still came
//          in at 300x300, and connecting a 300 accessory to a 200 duct is what raises a Revit dialog. The
//          bridge has no way to answer a dialog, so the call HANGS and needs Stop pressed in the AJ AI
//          pane. Set `Duct Width`/`Duct Height` (writable; every other size parameter on this family is
//          read-only and derived) BEFORE connecting.
// GOTCHA — BreakCurve swaps which half keeps the id: the ORIGINAL id kept the SECOND half and the
//          returned new id got the first. Never assume the original is the "first" piece; re-read both.
// GOTCHA — BreakCurve does NOT connect the two halves to each other. That is deliberate here: the
//          accessory goes between them.
// GOTCHA — NEVER call Document.Regenerate() after Commit(). Commit already regenerates and a later call
//          throws "Modification of the document is forbidden", which in a bridge script surfaces as a
//          hang, not an error. See knowledge/live-model/core.md.
// GOTCHA — the accessory family must be the matching category and domain (Duct Accessories for ducts,
//          Pipe Accessories for pipes) with TWO connectors of that domain.
// GOTCHA — this MODIFIES existing runs: explorer-first, run the filter alone and confirm the count first.
//
// STATUS 2026-08-14 — READ THIS BEFORE TRUSTING IT. Be precise about what was proven:
//   ✓ THE METHOD IS PROVEN, executed step by step against a live model. A 200x200 duct broke into two
//     1500 mm halves with an M_Fire Damper between them: accessory 2/2 connectors joined, each half 1/1,
//     run still on its MEP system (element 918743 in the scratch model).
//   ✓ EACH FAILURE MODE IS PROVEN TOO, which is why the gotchas above are facts and not guesses: the
//     original overload gave 0/2 and a null host; correct size but both joins in one transaction gave
//     0/2 and the modal network dialog.
//   ✗ THIS FILE AS ONE UNINTERRUPTED RUN IS NOT YET VERIFIED. A re-test on a fresh 350x350 duct threw
//     "Object reference not set to an instance of an object" and rolled back cleanly, leaving nothing
//     behind. The null was not isolated before the session ended. Most likely suspect: an element handle
//     used across a transaction boundary after BreakCurve — re-fetch by ID inside each transaction rather
//     than reusing a variable captured before it.
//   SO: run this on ONE run first and read the reported joined count. Do not turn it loose on a set.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string accessoryFamilyName = "";   // e.g. the office VCD family — must already be loaded (load-family.cs)
string accessoryTypeName = null;   // null = first type of that family
string positionMode = "fraction";  // "fraction" (along each run) | "mm_from_start"
double positionValue = 0.5;        // 0.5 = midpoint; or mm when positionMode = "mm_from_start"
bool matchAccessorySizeToRun = true; // leave true — see the SIZE MISMATCH gotcha above
// ---- END INPUTS ----

Func<double, double> mm = v => v / 304.8;
Func<double, double> toMm = v => v * 304.8;

// Version-proof id value: ElementId.IntegerValue (pre-2024) vs .Value (2024+). Looked up once.
// A BuiltInCategory is an enum over that NUMBER, so the cast needs the number, not the ElementId.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

var newAccessoryIds = new List<ElementId>();

var symbol = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
    .Where(s => s.FamilyName == accessoryFamilyName)
    .Where(s => accessoryTypeName == null || s.Name == accessoryTypeName)
    .FirstOrDefault();

var runs = elements.OfType<MEPCurve>().ToList();

if (symbol == null)
{
    sb.AppendLine($"Accessory family '{accessoryFamilyName}' not found — load it first (creators/load-family.cs).");
}
else if (runs.Count == 0)
{
    sb.AppendLine("No Ducts/Pipes in `elements` — nothing to insert into.");
}
else
{
    var catId = symbol.Category != null ? (BuiltInCategory)IdValue(symbol.Category.Id) : BuiltInCategory.INVALID;
    bool isDuctAcc = catId == BuiltInCategory.OST_DuctAccessory || catId == BuiltInCategory.OST_DuctFitting;
    bool isPipeAcc = catId == BuiltInCategory.OST_PipeAccessory || catId == BuiltInCategory.OST_PipeFitting;
    if (!isDuctAcc && !isPipeAcc)
        sb.AppendLine($"NOTE: '{symbol.FamilyName}' is category '{symbol.Category?.Name ?? "?"}' — not a Duct/Pipe Accessory.");

    int placed = 0, skipped = 0;
    var notes = new List<string>();

    // One TransactionGroup around the lot: a run that breaks but fails to connect would otherwise be
    // left cut open, which is worse than not having started.
    using (var tg = new TransactionGroup(Document, "AJ Tools - Place Accessory On Run"))
    {
        tg.Start();
        try
        {
            foreach (var run in runs)
            {
                bool runIsDuct = run is Autodesk.Revit.DB.Mechanical.Duct;
                if ((runIsDuct && isPipeAcc) || (!runIsDuct && isDuctAcc))
                { skipped++; notes.Add($"Id {run.Id}: domain mismatch"); continue; }

                var line = (run.Location as LocationCurve)?.Curve as Line;
                if (line == null) { skipped++; notes.Add($"Id {run.Id}: not a straight line"); continue; }

                double length = line.Length;
                double along = positionMode == "mm_from_start" ? mm(positionValue) : length * positionValue;
                double margin = Math.Min(mm(50), length * 0.1);
                if (along < margin || along > length - margin)
                { skipped++; notes.Add($"Id {run.Id}: {toMm(along):F0} mm too close to an end"); continue; }

                var point = line.GetEndPoint(0) + line.Direction.Normalize() * along;

                // Read the run's size BEFORE breaking it, to size the accessory with.
                double runW = run.LookupParameter("Width")?.AsDouble() ?? 0;
                double runH = run.LookupParameter("Height")?.AsDouble() ?? 0;
                var runId = run.Id;

                try
                {
                    // STEP 1 — break
                    ElementId otherHalfId;
                    using (var t = new Transaction(Document, "break run"))
                    {
                        t.Start();
                        otherHalfId = runIsDuct
                            ? Autodesk.Revit.DB.Mechanical.MechanicalUtils.BreakCurve(Document, runId, point)
                            : Autodesk.Revit.DB.Plumbing.PlumbingUtils.BreakCurve(Document, runId, point);
                        t.Commit();
                    }

                    // STEPS 2-3 — place, then size. Committed together and SEPARATELY from the
                    // connections: the accessory's connector origins only move to the new size when
                    // the change is committed, and pairing by distance against stale origins picks
                    // the wrong connector.
                    ElementId instId;
                    using (var t = new Transaction(Document, "place accessory"))
                    {
                        t.Start();
                        if (!symbol.IsActive) symbol.Activate();
                        var host = Document.GetElement(runId) as MEPCurve;
                        var inst = Document.Create.NewFamilyInstance(
                            point, symbol, host, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        instId = inst.Id;

                        // STEP 3 — without this the accessory keeps its FAMILY DEFAULT size.
                        if (matchAccessorySizeToRun && runW > 0 && runH > 0)
                        {
                            inst.LookupParameter("Duct Width")?.Set(runW);
                            inst.LookupParameter("Duct Height")?.Set(runH);
                        }
                        t.Commit();
                    }

                    // STEP 4 — ONE CONNECTION PER TRANSACTION. This is not style, it is the fix.
                    // Connecting both halves inside a single transaction makes Revit raise, at COMMIT,
                    // a modal "Error - cannot be ignored: The family is connected in a network and can
                    // no longer keep the connectivity. Disconnect the family from the network?". A
                    // bridge script cannot answer a modal dialog, so the call does not fail - it HANGS,
                    // and Stop has to be pressed in the AJ AI pane. Committing each join separately
                    // lets Revit settle the first connection before the second is offered. Verified
                    // live 2026-08-14: same code, both-in-one gives 0/2 and a dialog; one-per-commit
                    // gives 2/2 silently.
                    foreach (var halfId in new[] { runId, otherHalfId })
                    {
                        using (var t = new Transaction(Document, "connect one side"))
                        {
                            t.Start();
                            var half = Document.GetElement(halfId) as MEPCurve;
                            var inst = Document.GetElement(instId) as FamilyInstance;
                            if (half != null && inst != null)
                            {
                                // Pair by DISTANCE, never by index — connector order is not guaranteed
                                // and pairing wrongly crosses the two halves.
                                var freeEnd = half.ConnectorManager.Connectors.Cast<Connector>()
                                    .Where(c => !c.IsConnected).OrderBy(c => c.Origin.DistanceTo(point)).FirstOrDefault();
                                var accCons = inst.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                                    .Where(c => !c.IsConnected).ToList();
                                if (freeEnd != null && accCons.Count > 0)
                                {
                                    var mate = accCons.OrderBy(c => c.Origin.DistanceTo(freeEnd.Origin)).First();
                                    try { freeEnd.ConnectTo(mate); }
                                    catch (Exception exJoin) { notes.Add($"Id {halfId}: join failed — {exJoin.Message}"); }
                                }
                            }
                            t.Commit();
                        }
                    }

                    // Report what actually happened, not what was attempted.
                    int joined = (Document.GetElement(instId) as FamilyInstance)
                        .MEPModel.ConnectorManager.Connectors.Cast<Connector>().Count(c => c.IsConnected);
                    if (joined < 2)
                        notes.Add($"Id {runId}: accessory {instId} joined only {joined}/2");
                    newAccessoryIds.Add(instId);
                    placed++;
                }
                catch (Exception exOne) { skipped++; notes.Add($"Id {runId}: {exOne.Message}"); }
            }

            tg.Assimilate();
            sb.AppendLine($"Placed {placed} accessor{(placed == 1 ? "y" : "ies")} into {runs.Count} run(s), {skipped} skipped.");
            if (newAccessoryIds.Count > 0)
                sb.AppendLine($"  New accessory Ids: {string.Join(", ", newAccessoryIds.Take(30).Select(i => i.ToString()))}");
            if (notes.Count > 0)
                sb.AppendLine("  Notes: " + string.Join("; ", notes.Take(20)));
            sb.AppendLine("  Each run is now TWO segments joined by its accessory — the ids changed, re-filter before chaining.");
        }
        catch (Exception ex)
        {
            try { tg.RollBack(); } catch { }
            sb.AppendLine($"FAILED — rolled back, no runs modified. Reason: {ex.Message}");
            newAccessoryIds.Clear();
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
