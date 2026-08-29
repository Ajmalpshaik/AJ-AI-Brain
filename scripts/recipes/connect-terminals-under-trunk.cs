// ============================================================
// SCRIPT: connect-terminals-under-trunk.cs
// PURPOSE: Join air terminals that sit DIRECTLY UNDER an overhead trunk to it — a vertical drop out of
//          each terminal's neck and a real takeoff into the trunk above. The corridor case: trunk down
//          the centreline, diffusers on the same centreline below it, so there is no sideways offset for
//          a normal branch to use.
// SOURCE:  ../../knowledge/live-model/hvac-ducts.md § Terminals directly under the trunk
// STATUS:  living document - refine in place, don't fork a v2 file.
//
// ✱✱ THIS EXISTS BECAUSE THE OBVIOUS FRAGMENT MOVES YOUR MODEL. `action-connect-air-terminals.cs` reads
//    like the right tool — "the duct already runs past the terminals, Revit cuts the tap itself" — and on
//    2026-08-25 it reported *"Connected 1 terminal(s). 0 refused"* with Revit returning true, while what
//    it had actually done was LIFT THE DIFFUSER 625 mm out of the ceiling (Z 2100) up into the void
//    (Z 2725) to meet the duct. No drop, no fitting. Ajmal found it in a 3D view; every text check passed.
//    Revit will satisfy a connection by relocating the terminal when there is a vertical gap and nothing
//    else to move. This file builds the drop instead, so the terminal never moves.
//
// ✱✱ THREE TERMINAL GEOMETRIES, THREE DIFFERENT TOOLS. Pick by where the terminal sits relative to the
//    trunk, because the wrong one either refuses or damages the model:
//      OFFSET TO THE SIDE of the trunk   -> recipes/hvac-room-supply-ducting.cs (branch + elbow + drop,
//                                           whole room in one call), or hvac-floor-supply-ducting.cs
//      DIRECTLY UNDER the trunk          -> THIS FILE (vertical drop + takeoff)
//      ALREADY TOUCHING the duct         -> actions/structural-changes/action-connect-air-terminals.cs
//                                           (nothing new drawn — and read the warning in its header)
//    The room recipe REFUSES the under-the-trunk case rather than fudging it, printing "sits on the trunk
//    centreline - needs an inline tee, skipped". That refusal is what sends you here.
//
// GOTCHA: THE TERMINAL MUST NOT MOVE, and that is the check that catches the failure above. This file
//         records each terminal's Z before building and re-reads it after; any terminal that shifted is
//         reported and its drop is rolled back. A connected air path is not proof of a correct model.
// GOTCHA: THE DROP COMES BACK SHORTER THAN THE GAP and that is correct. The takeoff shortens it to fit
//         its own body — 422 mm came back from a 621 mm gap on the proving run. Never verify a drop by
//         comparing its length to the gap you asked for.
// GOTCHA: use the CONNECTOR overload of Duct.Create (doc, ductTypeId, levelId, terminalConnector, endXYZ).
//         It inherits size and system from the terminal and joins it in one step. The XYZ+XYZ overload
//         does not, and then the neck size has to be set by hand.
// GOTCHA: the trunk must genuinely pass OVER the terminal. A terminal whose neck is not under the trunk's
//         line within `lateralToleranceMm` is reported and skipped, not dragged sideways.
// RELATED: recipes/draw-main-duct-with-cap.cs (draw the trunk first, capped properly);
//          recipes/hvac-room-supply-ducting.cs (the side-offset case, whole room);
//          recipes/set-reducer-offset-from-takeoff.cs (tidy the reducers after sizing).
// ⚠ THE TECHNIQUE IS LIVE-PROVEN, THIS FILE AS WRITTEN IS NOT — 5 corridor diffusers were connected this
//   way on 2026-08-25 with the same logic run inline, all staying at Z 2100, 0 open ends. Dry-run first.
// ============================================================

// ---- INPUTS (edit every time - never treat these as fixed defaults) ----
int trunkDuctId = 0;                // the duct running over the terminals. Required.
int roomIdScope = 0;                // 0 = every free terminal under that trunk; else only inside this room
string systemKind = "supply";       // "supply" or "return" - which terminals to take
double lateralToleranceMm = 300;    // how far off the trunk's line a neck may sit and still count
double minGapMm = 100;              // below this there is no room for a drop - reported, skipped
bool dryRun = true;                 // true = list the pairing, build nothing
// ---- END INPUTS ----

const double MM = 304.8;
var sb = new System.Text.StringBuilder();

if (trunkDuctId == 0) return "trunkDuctId is required - give the Id of the duct running over the terminals.";
var trunk = Document.GetElement(new ElementId(trunkDuctId)) as MEPCurve;
if (trunk == null) return $"trunkDuctId {trunkDuctId} is not a duct.";

var trunkCurve = (trunk.Location as LocationCurve).Curve;
var tA = trunkCurve.GetEndPoint(0);
var tB = trunkCurve.GetEndPoint(1);
var trunkDir = (tB - tA).Normalize();
double trunkZ = tA.Z;

bool wantSupply = systemKind.Trim().ToLowerInvariant().StartsWith("s");
var wantSystem = wantSupply
    ? Autodesk.Revit.DB.Mechanical.DuctSystemType.SupplyAir
    : Autodesk.Revit.DB.Mechanical.DuctSystemType.ReturnAir;

BoundingBoxXYZ scope = null;
if (roomIdScope != 0) {
    var rm = Document.GetElement(new ElementId(roomIdScope));
    if (rm == null) return $"roomIdScope {roomIdScope} is not an element in this model.";
    scope = rm.get_BoundingBox(null);
    if (scope == null) return $"roomIdScope {roomIdScope} has no bounding box - is it a placed Room?";
}

sb.AppendLine($"Trunk {trunkDuctId}: ({tA.X*MM:F0},{tA.Y*MM:F0},{tA.Z*MM:F0}) -> ({tB.X*MM:F0},{tB.Y*MM:F0},{tB.Z*MM:F0})");
sb.AppendLine();

// ---- pair terminals to the trunk by perpendicular distance from its LINE, along its own length ----
var jobs = new List<Tuple<FamilyInstance, Connector, XYZ, double>>();   // terminal, neck, topPoint, gapMm
int wrongSystem = 0, outOfScope = 0, notUnder = 0, tooClose = 0, alreadyJoined = 0;

foreach (var at in new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctTerminal)
         .WhereElementIsNotElementType().Cast<FamilyInstance>())
{
    if (at.MEPModel == null || at.MEPModel.ConnectorManager == null) continue;
    var lp = at.Location as LocationPoint; if (lp == null) continue;
    if (scope != null) {
        var q = lp.Point;
        if (q.X <= scope.Min.X || q.X >= scope.Max.X || q.Y <= scope.Min.Y || q.Y >= scope.Max.Y) { outOfScope++; continue; }
    }

    Connector neck = null;
    foreach (Connector c in at.MEPModel.ConnectorManager.Connectors)
    {
        if (c.Domain != Domain.DomainHvac) continue;
        if (c.DuctSystemType != wantSystem) { wrongSystem++; continue; }
        if (c.IsConnected) { alreadyJoined++; continue; }
        neck = c;
    }
    if (neck == null) continue;

    // is the neck under the trunk's line, and between its two ends?
    var v = neck.Origin - tA;
    double along = v.DotProduct(trunkDir);
    if (along < 0 || along > (tB - tA).GetLength()) { notUnder++; continue; }
    var onLine = tA + trunkDir * along;
    double lateral = new XYZ(neck.Origin.X - onLine.X, neck.Origin.Y - onLine.Y, 0).GetLength() * MM;
    if (lateral > lateralToleranceMm) { notUnder++; continue; }

    double gap = (trunkZ - neck.Origin.Z) * MM;
    if (gap < minGapMm) {
        tooClose++;
        sb.AppendLine($"  SKIP {at.Id}: only {gap:F0} mm below the trunk - no room for a drop.");
        continue;
    }
    jobs.Add(Tuple.Create(at, neck, new XYZ(neck.Origin.X, neck.Origin.Y, trunkZ), gap));
}

sb.AppendLine($"{jobs.Count} terminal(s) to drop"
            + (alreadyJoined > 0 ? $", {alreadyJoined} already connected" : "")
            + (notUnder > 0 ? $", {notUnder} not under this trunk" : "")
            + (wrongSystem > 0 ? $", {wrongSystem} a different system" : "")
            + (outOfScope > 0 ? $", {outOfScope} outside the room" : "")
            + (tooClose > 0 ? $", {tooClose} too close" : "") + ".");

if (jobs.Count == 0) return sb.ToString();

if (dryRun) {
    sb.AppendLine();
    foreach (var j in jobs)
        sb.AppendLine($"  terminal {j.Item1.Id} neck Z {j.Item2.Origin.Z*MM:F0} -> trunk Z {trunkZ*MM:F0}   drop {j.Item4:F0} mm");
    sb.AppendLine();
    sb.AppendLine("DRY RUN - nothing built. Set dryRun = false to apply.");
    return sb.ToString();
}

// ---- build, one group per terminal, and refuse any that moved ----
var ductTypeId = trunk.GetTypeId();
var levelId = trunk.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM).AsElementId();
int built = 0; var problems = new List<string>();

foreach (var j in jobs)
{
    var term = j.Item1;
    double zBefore = (term.Location as LocationPoint).Point.Z;

    using (var g = new TransactionGroup(Document, $"AJ Tools - drop to terminal {term.Id}"))
    {
        g.Start();
        try
        {
            ElementId dropId = null, tapId = null;
            using (var t = new Transaction(Document, "drop"))
            {
                t.Start();
                var neck = term.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                    .First(c => c.Domain == Domain.DomainHvac && c.DuctSystemType == wantSystem);
                // connector overload: inherits size + system and joins the terminal itself
                var drop = Autodesk.Revit.DB.Mechanical.Duct.Create(Document, ductTypeId, levelId, neck, j.Item3);
                dropId = drop.Id;
                Document.Regenerate();
                Connector top = null;
                foreach (Connector c in drop.ConnectorManager.Connectors) if (!c.IsConnected) top = c;
                if (top == null) throw new Exception("the drop has no free top end");
                var tap = Document.Create.NewTakeoffFitting(top, trunk);
                tapId = tap.Id;
                Document.Regenerate();
                t.Commit();
            }

            // THE CHECK THIS FILE EXISTS FOR: did the terminal stay put?
            double zAfter = (term.Location as LocationPoint).Point.Z;
            if (Math.Abs(zAfter - zBefore) * MM > 1.0)
            {
                g.RollBack();
                problems.Add($"terminal {term.Id} MOVED {(zAfter - zBefore)*MM:F0} mm - rolled back, nothing left behind");
                continue;
            }
            int open = 0;
            var dd = Document.GetElement(dropId) as MEPCurve;
            foreach (Connector c in dd.ConnectorManager.Connectors) if (!c.IsConnected) open++;
            if (open > 0)
            {
                g.RollBack();
                problems.Add($"terminal {term.Id}: the drop was left with {open} open end(s) - rolled back");
                continue;
            }

            g.Assimilate();
            built++;
            sb.AppendLine($"  {term.Id}: drop {dropId} ({((dd.Location as LocationCurve).Curve).Length*MM:F0} mm), takeoff {tapId}");
        }
        catch (Exception ex) { g.RollBack(); problems.Add($"terminal {term.Id}: {ex.Message}"); }
    }
}

sb.AppendLine();
int stillMoved = jobs.Count(j => Math.Abs((j.Item1.Location as LocationPoint).Point.Z * MM
                                        - (j.Item3.Z * MM - j.Item4)) > 1.0);
sb.AppendLine($"BUILT {built} of {jobs.Count}. Terminals that moved: {stillMoved} (must be 0).");
foreach (var p in problems) sb.AppendLine("  " + p);
return sb.ToString();
