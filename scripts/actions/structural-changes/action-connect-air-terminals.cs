// ============================================================
// FRAGMENT (action) — action-connect-air-terminals.cs
// PURPOSE: Connect the air terminals in `elements` to the duct running past them — Revit cuts the tap
//          into the duct and makes the connection itself. The step AFTER the terminals are laid out:
//          place-terminals-checkerboard.cs and the HVAC terminal-layout skill put diffusers in the right
//          places; nothing here joined them to the ductwork, so every layout ended as loose components.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — the AIR TERMINALS, e.g. from
//          filter-by-category.cs with OST_DuctTerminal.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ THE CALL DOES THE WHOLE JOB: MechanicalUtils.ConnectAirTerminalOnDuct(doc, terminalId, ductId).
//    It creates the tap fitting on the duct, positions it under the terminal's connector and connects
//    both ends. Doing this by hand means creating a fitting family instance, working out the duct's
//    parameter at that point and connecting three connectors — the reason nothing in this library did it.
//    It returns a BOOL, and a false is not an exception: it means Revit declined, and the model is
//    unchanged for that terminal. Every false is reported with the terminal's Id, never counted as done.
//
// ✱✱ THE TERMINAL MUST ALREADY BE IN THE RIGHT PLACE. This connects; it does not move anything. Revit
//    taps the duct at the point nearest the terminal's connector, so a terminal that is nowhere near the
//    duct produces either a refusal or a very long tap. `maxDistanceMm` below is the guard: a duct
//    further away than that is not considered a candidate at all.
//
// ✱✱ AUTOMATIC DUCT CHOICE IS A GUESS, AND IT IS SHOWN AS ONE. With ductIdInt left at 0 this picks, for
//    each terminal, the nearest duct whose centre line passes within maxDistanceMm — measured to the
//    terminal's own connector, not to its insertion point, because a ceiling diffuser's connector is on
//    top of it and that is the end that has to reach. The chosen duct and the distance are printed for
//    every terminal so a wrong pick is visible in the report rather than only in the model.
//
// GOTCHA: DRY RUN BY DEFAULT. Read the pairing table first, then set dryRun = false.
// GOTCHA: A TERMINAL THAT IS ALREADY CONNECTED IS SKIPPED, not reconnected — reconnecting would leave the
//         old tap behind as an orphan fitting. Disconnect it in Revit first if that is really wanted.
// GOTCHA: SYSTEM TYPE STILL HAS TO AGREE. A supply diffuser will not tap a return duct; Revit refuses and
//         this reports the refusal. That is a modelling problem, not a script problem.
// GOTCHA: connecting raises Revit warnings on a real model (sizes, system mismatch). The transaction sets
//         SetForcedModalHandling(false) so they do not stop the batch with a dialog — the same shape as
//         action-create-from-room-boundaries.cs. It does NOT resolve anything for you; see
//         ../../../knowledge/live-model/failure-handling-without-a-class.md for why blanket resolution
//         is dangerous.
// RELATED: verify_connectivity (native tool) and skills/ajtools-mep-connectivity-verify check the result;
//          run it afterwards rather than trusting the count here.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE terminal, look at the tap in a
//   section, and check the airflow reads through before doing a whole room.
//
// ✱✱ FOUR FRAGMENTS JOIN MEP TOGETHER AND THEY BUILD DIFFERENT AMOUNTS OF DUCTWORK. Pick by how
//    much already exists, because the wrong one either builds a system you did not ask for or fails
//    for want of one:
//      actions/structural-changes/action-connect-air-terminals.cs   The duct ALREADY RUNS PAST the
//                                                    terminals. Revit cuts the tap itself. Nothing new
//                                                    is drawn. "connect the terminals to the duct",
//                                                    "tap these diffusers into that main".
//      recipes/connect-terminal-branch.cs            ONE terminal, and the branch to the main DOES NOT
//                                                    EXIST yet. Draws the vertical riser, a real elbow
//                                                    at the turn, then the horizontal into a takeoff
//                                                    tee. Use when the tap alone will not reach.
//      recipes/connect-equipment-to-air-terminals.cs THE WHOLE ROOM AT ONCE, from equipment. Builds the
//                                                    main trunk out of the FCU, a tap per terminal, the
//                                                    branches, the drops, and caps the trunk past the
//                                                    last branch. Ajmal own connection method. This one
//                                                    CREATES A SYSTEM - do not reach for it when a duct
//                                                    is already there and only the taps are missing.
//      actions/structural-changes/action-connect-open-connectors.cs  CLEANUP, builds nothing. Joins
//                                                    pairs that already touch but that Revit does not
//                                                    think are connected - after a copy/paste, after a
//                                                    link is bound, after a run drawn leg by leg.
//    To CHECK rather than change, the connectivity fragments are separate again: verify-duct-connectivity.cs
//    (terminal to FCU chain), action-check-system-connectivity.cs (how many pieces is this really in),
//    action-check-open-pipe-ends.cs and action-find-dead-end-system.cs. Measured 2026-08-24: the plain
//    phrase "connect the air terminals to the duct" returned the WHOLE-SYSTEM recipe at #1 and the
//    tap-into-existing-duct fragment at #3, which is why this table is in all four files.
// ✘✘ MEASURED FAILURE 2026-08-25 — IT MOVES THE TERMINAL INSTEAD OF DROPPING DUCT TO IT.
//    Run on a corridor diffuser sitting DIRECTLY UNDER an overhead trunk (neck Z 2202, trunk Z 2823),
//    it reported "Connected 1 terminal(s). 0 refused" and Revit returned true — and what it actually did
//    was LIFT THE DIFFUSER 625 mm, from ceiling level 2100 up into the void at 2725, to meet the duct.
//    No drop duct and no tap fitting were created. The diffuser ends up above the ceiling, where it is
//    of no use to anybody, and every text-level check still passes.
//    THE RULE: this fragment is for a terminal whose connector ALREADY MEETS the duct, or is offset to
//    the side so Revit can cut a real tap. When the terminal sits under the duct with a vertical gap,
//    BUILD THE DROP YOURSELF and the terminal never moves:
//        var drop = Duct.Create(doc, ductTypeId, levelId, terminalConnector, topPointAtTrunkZ);
//        Document.Create.NewTakeoffFitting(freeTopConnectorOfDrop, trunk);
//    (The connector overload inherits size and system and joins the terminal itself; the takeoff then
//    shortens the drop to fit its own body — 422 mm came back from a 621 mm gap, which is correct.)
//    ALWAYS READ BACK THE TERMINAL'S Z AFTER RUNNING THIS. Its own note about verify_connectivity is
//    right but not enough: the air path can be perfect while the diffuser is in the wrong place.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;            // true = report the pairing only, change nothing
int ductIdInt = 0;             // 0 = pick the nearest duct per terminal; otherwise connect them all to this duct
double maxDistanceMm = 3000;   // ignore any duct further than this from the terminal's connector
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
double maxDistanceFt = maxDistanceMm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put filter-by-category.cs with OST_DuctTerminal above this.");
    return sb.ToString();
}

// ---- the terminals, and the connector on each that has to reach the duct ----
Func<Element, Connector> firstFreeConnector = el =>
{
    var fi = el as FamilyInstance;
    var cm = fi != null ? fi.MEPModel != null ? fi.MEPModel.ConnectorManager : null : null;
    if (cm == null) return null;
    foreach (Connector c in cm.Connectors)
    {
        if (c.Domain != Domain.DomainHvac) continue;
        if (!c.IsConnected) return c;
    }
    return null;
};

// ---- candidate ducts ----
var ducts = new List<Autodesk.Revit.DB.Mechanical.Duct>();
if (ductIdInt != 0)
{
    var d = Document.GetElement(new ElementId(ductIdInt)) as Autodesk.Revit.DB.Mechanical.Duct;
    if (d == null)
    {
        sb.AppendLine($"ductIdInt {ductIdInt} is not a Duct — set it to 0 to pick automatically, or give a real duct Id.");
        return sb.ToString();
    }
    ducts.Add(d);
}
else
{
    ducts = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.Duct))
        .Cast<Autodesk.Revit.DB.Mechanical.Duct>()
        .ToList();
    if (ducts.Count == 0)
    {
        sb.AppendLine("No ducts in the model to connect to.");
        return sb.ToString();
    }
}

// ---- pair each terminal with a duct ----
var pairs = new List<(Element terminal, Autodesk.Revit.DB.Mechanical.Duct duct, double distFt, string note)>();

foreach (var el in elements)
{
    if (el == null) continue;

    var conn = firstFreeConnector(el);
    if (conn == null)
    {
        pairs.Add((el, null, 0, "no free HVAC connector (already connected, or not an air terminal)"));
        continue;
    }
    var p = conn.Origin;

    Autodesk.Revit.DB.Mechanical.Duct best = null;
    double bestDist = double.MaxValue;
    foreach (var d in ducts)
    {
        var lc = d.Location as LocationCurve;
        if (lc == null || lc.Curve == null) continue;
        double dist;
        try { dist = lc.Curve.Distance(p); } catch { continue; }
        if (dist < bestDist) { bestDist = dist; best = d; }
    }

    if (best == null) pairs.Add((el, null, 0, "no duct with a location curve found"));
    else if (ductIdInt == 0 && bestDist > maxDistanceFt)
        pairs.Add((el, null, bestDist, $"nearest duct is {bestDist * MM_PER_FOOT:F0} mm away — beyond maxDistanceMm"));
    else pairs.Add((el, best, bestDist, null));
}

// ---- report the pairing BEFORE touching anything ----
sb.AppendLine($"{elements.Count} terminal(s) in. Pairing:");
sb.AppendLine("Terminal Id | Duct Id | Distance (mm) | Note");
sb.AppendLine("--- | --- | --- | ---");
foreach (var (terminal, duct, distFt, note) in pairs.Take(50))
    sb.AppendLine($"{terminal.Id} | {(duct == null ? "-" : duct.Id.ToString())} | {(duct == null && note != null && distFt == 0 ? "-" : (distFt * MM_PER_FOOT).ToString("F0"))} | {note ?? "ready"}");
if (pairs.Count > 50) sb.AppendLine($"... {pairs.Count - 50} more not shown.");

var ready = pairs.Where(x => x.duct != null).ToList();
int skipped = pairs.Count - ready.Count;

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine($"DRY RUN — {ready.Count} terminal(s) would be connected, {skipped} skipped. Nothing changed. Set dryRun = false to apply.");
    return sb.ToString();
}

if (ready.Count == 0)
{
    sb.AppendLine();
    sb.AppendLine("Nothing to connect — every terminal was skipped for the reason shown above.");
    return sb.ToString();
}

int connected = 0, refused = 0;
using (var t = new Transaction(Document, "AJ Tools - Connect Air Terminals"))
{
    t.Start();
    try
    {
        var fo = t.GetFailureHandlingOptions();
        fo.SetForcedModalHandling(false);   // do not stop the batch with a dialog
        fo.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(fo);

        foreach (var (terminal, duct, distFt, note) in ready)
        {
            bool ok = false;
            string why = null;
            try
            {
                ok = Autodesk.Revit.DB.Mechanical.MechanicalUtils.ConnectAirTerminalOnDuct(Document, terminal.Id, duct.Id);
            }
            catch (Exception ex) { why = ex.Message; }

            if (ok) connected++;
            else
            {
                refused++;
                sb.AppendLine($"  REFUSED: terminal {terminal.Id} -> duct {duct.Id}{(why == null ? " (Revit returned false — usually a system-type mismatch or the tap will not fit)" : " — " + why)}");
            }
        }

        t.Commit();
        sb.AppendLine();
        sb.AppendLine($"Connected {connected} terminal(s). {refused} refused, {skipped} skipped before the attempt.");
        sb.AppendLine("Check the result with the verify_connectivity tool — a returned true is Revit accepting the call, not proof the air path is right.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
