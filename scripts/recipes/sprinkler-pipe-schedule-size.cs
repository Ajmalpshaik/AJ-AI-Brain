// ============================================================
// RECIPE — sprinkler-pipe-schedule-size.cs
// PURPOSE: Size sprinkler pipe by the PIPE SCHEDULE method — walk the modelled pipe network, count how
//          many sprinklers each segment feeds, look the required size up in the schedule table, and
//          report it against the size actually modelled. READ-ONLY by default; it can write the size
//          back only when told to explicitly.
// STANDALONE — has its own sb and returns.
// SOURCE: ../../knowledge/fire-sprinkler/pipe-sizing.md   (both methods, the gate, and the tables)
// VERSION-PROOF (2026-08-20, brought in line with the library-wide migration): unit conversion is
//         plain arithmetic (1 ft = exactly 304.8 mm) and every id collection is keyed by ElementId,
//         so this runs on Revit 2020 through 2027 from one source. The id INPUT is still declared
//         int, matching the rest of the library — see knowledge/revit-version-compatibility.md.
//
// *** IT CHECKS THE GATE BEFORE IT CHECKS ANYTHING ELSE, AND THAT IS THE POINT. ***
//   The pipe schedule method is heavily restricted. Commonly: new installations of 5,000 ft2 (465 m2) or
//   less; larger only where the required flow is available at 50 psi residual at the HIGHEST sprinkler;
//   light and ordinary hazard only, never extra hazard; and the 2025 edition removed the old allowance
//   for additions to existing pipe-schedule systems. On a Qatar project of any normal size the honest
//   answer is usually "this does not qualify — it needs hydraulic calculation by the fire engineer",
//   and saying that is worth more than a table of sizes nobody can use. The fragment says it, loudly,
//   and still shows the numbers below it marked as INDICATIVE ONLY so they can be used for coordination.
//
// *** THE TABLES BELOW ARE UNCONFIRMED. *** Same wall as the beam obstruction table: the sources gave the
//   method's LIMITS clearly and its NUMBERS not at all. The values seeded are the ones commonly published
//   for NFPA 13. Type your adopted edition's tables over them and set tablesConfirmed = true; until then
//   every run prints the warning.
//
// GOTCHA: **sprinklers above AND below a ceiling both count on the same branch.** If the ceiling void is
//         protected too (knowledge/fire-sprinkler/concealed-spaces.md), every branch below serves twice
//         what the ceiling plan shows. This fragment counts what is really connected, so it catches that
//         automatically — but only if the void heads are actually modelled and connected.
// GOTCHA: **a looped or gridded system has no "downstream".** A count-based schedule cannot be applied to
//         one at all; those must be hydraulically calculated. The walk detects a loop and reports it as a
//         finding rather than silently picking one path.
// GOTCHA: **Connector.IsConnected describes intent, not physical reality** — this Brain's own hard-won
//         rule. The network is built by clustering connector ORIGINS within a tolerance, the approach
//         live-proven in recipes/trace-mep-circuits.cs, not by trusting the flag.
// GOTCHA: the walk needs a ROOT — the segment nearest the water supply. Without it there is no "toward
//         the source" and therefore no downstream count. Give rootPipeIdInt; the fragment will not guess.
// GOTCHA: "size" here is the NOMINAL diameter on the pipe. Whether the project's nominal series matches
//         the one the table assumes is a project fact to confirm once, not per run.
// GOTCHA: the 8-sprinklers-per-branch-line limit is a SEPARATE rule from the size table. A segment can
//         pass the table and break that. It is reported separately, not folded into the size verdict.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. The connector-clustering walk is
//   the shape live-proven in recipes/trace-mep-circuits.cs (2026-07-23) and the BFS is plain C#. Run it on
//   ONE known branch first and check the head count by eye before trusting a whole system.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int rootPipeIdInt = 0;                // the pipe segment nearest the water supply — REQUIRED, defines "downstream"
string standardLabel = "";            // REQUIRED — "NFPA 13 (2022)" etc.
string hazardLabel = "";              // REQUIRED — "Light Hazard" / "Ordinary Hazard Group I"
string hazardTable = "ordinary";      // which table to use: "light" or "ordinary". Extra hazard is NOT allowed.

// --- THE GATE. Answer these honestly; the fragment reports the result and does not decide for you.
double systemAreaM2 = 0;              // floor area this system protects. 0 = unknown, and it will say so.
double gateAreaLimitM2 = 465;         // 5,000 ft2
bool fiftyPsiAtHighestHeadProven = false;  // only true if somebody has produced a real supply curve
bool isExistingPipeScheduleSystem = false; // an addition to an existing schedule system (pre-2025 editions only)
bool editionIs2025OrLater = false;    // 2025 removed the addition/modification allowance

// --- the schedule tables. Ascending. sizeMm and maxSprinklers must be the same length.
bool tablesConfirmed = false;
double[] sizeMm            = new[] { 25.0, 32, 40, 50, 65, 80, 90, 100, 125, 150, 200 };
int[] lightMaxSprinklers   = new[] {    2,  3,  5, 10, 30, 60, 100, 999, 999, 999, 999 };
int[] ordinaryMaxSprinklers= new[] {    2,  3,  5, 10, 20, 40,  65, 100, 160, 275, 400 };

// --- the other rule that travels with the table
int maxSprinklersPerBranchSide = 8;   // per side of a cross main, light and ordinary

// --- walk settings
double clusterToleranceMm = 25;       // how close two connector origins must be to count as joined
int maxSegmentsListed = 120;
bool writeSizeBack = false;           // DEFAULT OFF. True = set the pipe diameter. Bulk change — get a go-ahead.
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toMm = v => v * 304.8;   // version-proof: 1 ft is exactly 304.8 mm, no unit API
Func<double, double> mm = v => v / 304.8;

var missing = new List<string>();
if (rootPipeIdInt == 0) missing.Add("rootPipeIdInt (the segment nearest the supply — there is no downstream without it)");
if (string.IsNullOrWhiteSpace(standardLabel)) missing.Add("standardLabel");
if (string.IsNullOrWhiteSpace(hazardLabel)) missing.Add("hazardLabel");

int[] table = hazardTable == "light" ? lightMaxSprinklers : ordinaryMaxSprinklers;
if (sizeMm.Length != table.Length) missing.Add("sizeMm and the chosen table must be the same length");

if (missing.Count > 0)
{
    sb.AppendLine("NOT RUN — required inputs missing:");
    foreach (var m in missing) sb.AppendLine("  - " + m);
}
else
{
    // ---------- THE GATE, FIRST ----------
    sb.AppendLine($"SPRINKLER PIPE SIZING — PIPE SCHEDULE METHOD");
    sb.AppendLine($"STANDARD: {standardLabel}  |  HAZARD: {hazardLabel}  |  TABLE: {hazardTable}");
    sb.AppendLine();
    sb.AppendLine("GATE — is the pipe schedule method permitted for this system?");

    var blockers = new List<string>();
    if (hazardTable != "light" && hazardTable != "ordinary")
        blockers.Add($"hazardTable is '{hazardTable}'. The schedule method covers LIGHT and ORDINARY hazard only — extra hazard cannot use it.");
    if (systemAreaM2 <= 0)
        blockers.Add("systemAreaM2 is not stated, so the 465 m2 (5,000 ft2) test cannot be applied. State it.");
    else if (systemAreaM2 > gateAreaLimitM2 && !fiftyPsiAtHighestHeadProven)
        blockers.Add($"this system protects {systemAreaM2:F0} m2, over the {gateAreaLimitM2:F0} m2 limit, and 50 psi residual at the"
            + " HIGHEST sprinkler has not been proven. That is a water-supply fact needing a real supply curve, not a drawing fact.");
    if (isExistingPipeScheduleSystem && editionIs2025OrLater)
        blockers.Add("this is an addition to an existing pipe-schedule system, and the 2025 edition removed that allowance.");

    if (blockers.Count == 0)
    {
        sb.AppendLine("  PERMITTED on the inputs given"
            + (systemAreaM2 > gateAreaLimitM2 ? " — via the 50 psi route, which rests on the supply curve you say exists." : "."));
    }
    else
    {
        sb.AppendLine("  *** NOT PERMITTED on the inputs given:");
        foreach (var b in blockers) sb.AppendLine("      - " + b);
        sb.AppendLine("  *** THE HONEST OUTPUT IS: this system needs HYDRAULIC CALCULATION by the fire protection engineer.");
        sb.AppendLine("      Everything below is INDICATIVE ONLY — usable for coordination, clash and take-off, and NOT");
        sb.AppendLine("      for issue. Say that in the reply; do not let a coordination size become a design size.");
    }
    if (!tablesConfirmed)
        sb.AppendLine("  *** The schedule table in this run is UNCONFIRMED — commonly published values, not verified against"
            + " the standard. Type your edition's table in and set tablesConfirmed = true.");
    sb.AppendLine();

    // ---------- build the network by connector proximity ----------
    var root = Document.GetElement(new ElementId(rootPipeIdInt));
    if (root == null) { sb.AppendLine($"rootPipeIdInt {rootPipeIdInt} does not exist."); }
    else
    {
        double tolFt = mm(clusterToleranceMm);

        var pipeLike = new List<Element>();
        pipeLike.AddRange(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeCurves)
            .WhereElementIsNotElementType().ToList());
        pipeLike.AddRange(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeFitting)
            .WhereElementIsNotElementType().ToList());
        var sprinklers = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Sprinklers)
            .WhereElementIsNotElementType().ToList();

        var nodes = new List<Element>();
        nodes.AddRange(pipeLike);
        nodes.AddRange(sprinklers);

        // connector origins per element — IsConnected is NOT trusted (trace-mep-circuits.cs lesson)
        // keyed by ElementId throughout: its GetHashCode returns the id, so it keys a Dictionary/HashSet
        // directly and stays correct on 2024+ where an id no longer fits an int.
        var rootId = new ElementId(rootPipeIdInt);
        var conns = new Dictionary<ElementId, List<XYZ>>();
        foreach (var e in nodes)
        {
            ConnectorManager mgr = null;
            var mc = e as MEPCurve;
            if (mc != null) mgr = mc.ConnectorManager;
            else { var fi = e as FamilyInstance; if (fi != null && fi.MEPModel != null) mgr = fi.MEPModel.ConnectorManager; }
            if (mgr == null) continue;
            var list = new List<XYZ>();
            foreach (Connector c in mgr.Connectors) { try { list.Add(c.Origin); } catch { } }
            if (list.Count > 0) conns[e.Id] = list;
        }

        if (!conns.ContainsKey(rootId))
        {
            sb.AppendLine($"The root element (Id {rootPipeIdInt}) has no readable connectors — it cannot anchor the walk.");
        }
        else
        {
            var ids = conns.Keys.ToList();
            var adj = ids.ToDictionary(i => i, i => new List<ElementId>());
            for (int a = 0; a < ids.Count; a++)
                for (int b = a + 1; b < ids.Count; b++)
                {
                    bool touch = conns[ids[a]].Any(p => conns[ids[b]].Any(q => p.DistanceTo(q) <= tolFt));
                    if (!touch) continue;
                    adj[ids[a]].Add(ids[b]);
                    adj[ids[b]].Add(ids[a]);
                }

            var sprinklerIds = new HashSet<ElementId>(sprinklers.Select(s => s.Id));

            // BFS out from the root: parents, order, and loop detection
            var parent = new Dictionary<ElementId, ElementId>();
            var order = new List<ElementId>();
            var seen = new HashSet<ElementId> { rootId };
            var q = new Queue<ElementId>(); q.Enqueue(rootId);
            parent[rootId] = ElementId.InvalidElementId;   // the root has no parent
            int loopEdges = 0;
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                order.Add(cur);
                foreach (var nb in adj[cur])
                {
                    if (!seen.Contains(nb)) { seen.Add(nb); parent[nb] = cur; q.Enqueue(nb); }
                    else if (parent.ContainsKey(cur) && !parent[cur].Equals(nb)) loopEdges++;
                }
            }

            int reached = seen.Count, unreached = ids.Count - reached;
            sb.AppendLine($"NETWORK — {ids.Count:N0} connectable element(s); {reached:N0} reached from the root, {unreached:N0} NOT reached.");
            if (unreached > 0)
                sb.AppendLine($"  *** {unreached:N0} element(s) are not connected to the root. Either they are a separate system, or"
                    + " there is a real break. Trace it (recipes/verify-duct-connectivity.cs is the duct twin of this) before sizing.");
            if (loopEdges > 0)
            {
                sb.AppendLine($"  *** {loopEdges:N0} loop edge(s) detected — this network is LOOPED or GRIDDED.");
                sb.AppendLine("      A count-based schedule has no meaning on a loop: there is no single downstream. Such systems must");
                sb.AppendLine("      be hydraulically calculated. The counts below follow one BFS path and are NOT a sizing basis.");
            }

            // downstream sprinkler count per node, computed leaf-first
            var downstream = ids.ToDictionary(i => i, i => 0);
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var id = order[i];
                if (sprinklerIds.Contains(id)) downstream[id] += 1;
                var p = parent[id];
                if (!p.Equals(ElementId.InvalidElementId)) downstream[p] += downstream[id];
            }

            // ---------- size each pipe ----------
            Func<int, double> requiredSize = count =>
            {
                for (int i = 0; i < sizeMm.Length; i++) if (count <= table[i]) return sizeMm[i];
                return sizeMm[sizeMm.Length - 1];
            };

            int listed = 0, undersized = 0, oversized = 0, ok = 0, noSize = 0, branchBreaches = 0;
            var writes = new List<Tuple<Element, double>>();

            foreach (var e in pipeLike)
            {
                var id = e.Id;
                if (!downstream.ContainsKey(id) || !seen.Contains(id)) continue;
                int heads = downstream[id];
                if (heads == 0) continue;                       // feeds nothing — nothing to size from

                double modelled = 0;
                try
                {
                    var p = e.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    if (p != null && p.HasValue) modelled = toMm(p.AsDouble());
                }
                catch { }

                double need = requiredSize(heads);
                string verdict;
                if (modelled <= 0) { verdict = "no diameter read"; noSize++; }
                else if (modelled < need - 0.5) { verdict = $"UNDERSIZED — needs {need:N0} mm"; undersized++; }
                else if (modelled > need + 0.5) { verdict = $"larger than the schedule requires ({need:N0} mm) — fine, but check why"; oversized++; }
                else { verdict = "matches the schedule"; ok++; }

                if (writeSizeBack && modelled > 0 && Math.Abs(modelled - need) > 0.5) writes.Add(Tuple.Create(e, need));

                if (heads <= maxSprinklersPerBranchSide * 2 && heads > maxSprinklersPerBranchSide)
                {
                    // a cross main legitimately carries more; this only flags what looks like a branch line
                    if (adj[id].Count <= 2) branchBreaches++;
                }

                if (listed++ < maxSegmentsListed)
                {
                    string nm = null; try { nm = e.Name; } catch { }
                    sb.AppendLine($"  Id {id} '{nm ?? "?"}' feeds {heads:N0} head(s) | modelled "
                        + (modelled > 0 ? $"{modelled:N0} mm" : "—") + $" | schedule {need:N0} mm | {verdict}");
                }
            }
            if (listed > maxSegmentsListed) sb.AppendLine($"  ... and {listed - maxSegmentsListed:N0} more segment(s).");

            sb.AppendLine();
            sb.AppendLine("RESULT");
            sb.AppendLine($"  segments sized      {listed,6:N0}");
            sb.AppendLine($"  match the schedule  {ok,6:N0}");
            sb.AppendLine($"  UNDERSIZED          {undersized,6:N0}   <- the ones that matter");
            sb.AppendLine($"  larger than needed  {oversized,6:N0}");
            sb.AppendLine($"  no diameter read    {noSize,6:N0}");
            if (branchBreaches > 0)
                sb.AppendLine($"  *** {branchBreaches:N0} apparent branch line(s) carry more than {maxSprinklersPerBranchSide} heads."
                    + " That is a SEPARATE rule from the size table and a segment can pass one and break the other.");
            sb.AppendLine($"  total sprinklers on this network: {downstream[rootId]:N0}");
            sb.AppendLine("  NOTE: heads above AND below a ceiling both count on the branch that feeds them — if the ceiling void");
            sb.AppendLine("  is protected, this count includes it, but ONLY if those heads are modelled and connected.");

            if (blockers.Count > 0)
                sb.AppendLine("  REMINDER: the gate above says the schedule method does not apply here. These sizes are for"
                    + " coordination only and must not be issued as design.");
            if (!tablesConfirmed)
                sb.AppendLine("  REMINDER: the table used was UNCONFIRMED.");

            // ---------- optional write-back ----------
            if (writeSizeBack && writes.Count > 0)
            {
                using (var t = new Transaction(Document, "AJ Tools - Size Sprinkler Pipe by Schedule"))
                {
                    t.Start();
                    try
                    {
                        int done = 0, failed = 0;
                        foreach (var w in writes)
                        {
                            try
                            {
                                var p = w.Item1.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                                if (p != null && !p.IsReadOnly) { p.Set(mm(w.Item2)); done++; } else failed++;
                            }
                            catch { failed++; }
                        }
                        t.Commit();
                        sb.AppendLine();
                        sb.AppendLine($"  WROTE {done:N0} diameter(s); {failed:N0} refused (read-only, or the size is not in the pipe type's list).");
                        sb.AppendLine("  *** READ THEM BACK IN A SEPARATE CALL. Revit snaps a diameter to the sizes the Pipe Type allows,");
                        sb.AppendLine("      so a written value can silently land on a DIFFERENT size than the one requested.");
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        sb.AppendLine($"FAILED (write) — rolled back, nothing changed. Reason: {ex.Message}");
                    }
                }
            }
            else if (writeSizeBack)
                sb.AppendLine("  writeSizeBack was on but nothing needed changing.");
        }
    }
}

return sb.ToString();
