// ============================================================
// FRAGMENT (action) — action-report-connector-loads.cs
// PURPOSE: The ENGINEERING VALUES set on each connector — demand, assigned flow, K-factor, fixture
//          units, loss coefficient, pressure drop, flow direction and loss method. The INPUTS Revit's
//          system calculation runs on, and the check for the ones nobody filled in.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from
//          filter-by-category.cs or filter-by-id-list.cs.
// NOT STANDALONE — see scripts/README.md for how to compose.
// READ-ONLY — opens no transaction, changes nothing.
//
// ✱✱ WHY THIS EXISTS, AND IT IS A HOLE IN THE MOST-USED MEP FRAGMENT HERE.
//    `action-report-connectors.cs` is one of the most-run fragments in this library, and it reads the
//    GEOMETRY and TOPOLOGY half of a connector — Radius, Height, Width, Shape, Origin, Domain,
//    IsConnected, AllRefs, Owner, CoordinateSystem. Every one of those answers "where is it and what
//    is it joined to". **None of them answers "what is it carrying".** Demand, AssignedFlow,
//    AssignedKCoefficient, AssignedFixtureUnits, AssignedLossCoefficient, AssignedPressureDrop,
//    Coefficient and EngagementLength appeared in NO fragment at all.
//
// ✱✱ THE FAILURE THIS CATCHES, AND IT IS THE CLASSIC ONE.
//    Revit calculates system flow and pressure loss from what is SET ON THE CONNECTORS. A fixture with
//    no Demand, a sprinkler with no K-factor, an air terminal whose flow was never assigned — each
//    contributes ZERO, and the system report then comes back clean, fast and completely wrong. The
//    numbers look plausible because they are real numbers; they are just the wrong ones. This reports
//    the inputs and flags the zeros, so the calculation can be trusted or rejected on evidence.
//    Its counterpart reads the OUTPUT side (Revit's computed sections and critical path) — run both:
//    outputs are only as good as the inputs listed here.
//
// ✱✱ A ZERO IS NOT ALWAYS A FAULT, WHICH IS WHY THEY ARE SEPARATED. A pipe's own connectors carry no
//    Demand — the demand belongs to the FIXTURE at the end of the run. So a zero on a duct or pipe is
//    normal, and a zero on a terminal, fixture or equipment connector is the thing to look at. The
//    report splits them on exactly that line rather than counting every zero as a problem.
//
// ✱✱ AND A REAL TRAP: READING A PROPERTY THE DOMAIN DOES NOT HAVE THROWS. A duct connector has no
//    K-factor; an electrical connector has no flow. Revit does not return zero for these, it raises —
//    so every read here is guarded individually and a blank column means "not applicable to this
//    domain", never "set to nothing".
//
// RELATED: actions/reporting/action-report-connectors.cs (the geometry/topology half — run both),
//          actions/qa-checks/action-verify-connectivity.cs and the native `verify_connectivity` tool,
//          skills/ajtools-hvac-space-airflow/SKILL.md, skills/ajtools-fire-sprinkler-layout/SKILL.md
//          (the K-factor is what sizes a sprinkler run).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only, so the worst case is a wrong reading rather than a wrong
//    model — but check one connector's flow against Revit's own properties palette before trusting it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool onlyProblems = false;        // true = list only connectors whose engineering value is unset
bool includeUnconnected = true;   // include connectors that are not joined to anything
int maxRowsListed = 120;
// ---- END INPUTS ----

// Every read is individually guarded — see the domain trap in the header. A null return means
// "not applicable here", which is reported as blank and never as zero.
Func<Func<double>, double?> Num = read =>
{
    try { return read(); } catch { return null; }
};
Func<Func<object>, string> Txt = read =>
{
    try { var v = read(); return v == null ? "" : v.ToString(); } catch { return ""; }
};

// Which categories are EXPECTED to carry a load. A zero on one of these is worth looking at; a zero
// on a duct or pipe is normal, because the demand lives at the fixture, not in the run.
var loadBearing = new HashSet<int>
{
    (int)BuiltInCategory.OST_DuctTerminal,
    (int)BuiltInCategory.OST_MechanicalEquipment,
    (int)BuiltInCategory.OST_PlumbingFixtures,
    (int)BuiltInCategory.OST_Sprinklers,
    (int)BuiltInCategory.OST_ElectricalEquipment,
    (int)BuiltInCategory.OST_ElectricalFixtures,
    (int)BuiltInCategory.OST_LightingFixtures,
    (int)BuiltInCategory.OST_SpecialityEquipment
};

int elementsRead = 0, connectorsRead = 0, unconnected = 0;
int shouldCarryLoad = 0, carryingNothing = 0;
var rows = new List<string>();
var byDomain = new Dictionary<string, int>();
var problems = new List<string>();

foreach (var e in elements)
{
    ConnectorManager cm = null;
    try
    {
        var mep = e as MEPCurve;
        if (mep != null) cm = mep.ConnectorManager;
        else
        {
            var fi = e as FamilyInstance;
            if (fi != null && fi.MEPModel != null) cm = fi.MEPModel.ConnectorManager;
        }
    }
    catch { }
    if (cm == null) continue;

    elementsRead++;

    // Is this a category that SHOULD carry a load? Matched by comparing Category Ids rather than by
    // casting Category.Id back to a BuiltInCategory — that needs the id's NUMBER, and the property
    // holding it was renamed across versions (IntegerValue, gone in 2027; Value, absent before it).
    bool isLoadBearing = false;
    try
    {
        foreach (var bic in loadBearing)
        {
            var cat = Category.GetCategory(Document, (BuiltInCategory)bic);
            if (cat != null && e.Category != null && cat.Id == e.Category.Id) { isLoadBearing = true; break; }
        }
    }
    catch { }

    ConnectorSet set = null;
    try { set = cm.Connectors; } catch { }
    if (set == null) continue;

    foreach (Connector c in set)
    {
        connectorsRead++;

        bool connected = false;
        try { connected = c.IsConnected; } catch { }
        if (!connected) unconnected++;
        if (!connected && !includeUnconnected) continue;

        string domain = Txt(() => c.Domain);
        if (!byDomain.ContainsKey(domain)) byDomain[domain] = 0;
        byDomain[domain]++;

        var flow      = Num(() => c.Flow);
        var demand    = Num(() => c.Demand);
        var asgFlow   = Num(() => c.AssignedFlow);
        var kFactor   = Num(() => c.AssignedKCoefficient);
        var fixtureU  = Num(() => c.AssignedFixtureUnits);
        var lossCoef  = Num(() => c.AssignedLossCoefficient);
        var asgDrop   = Num(() => c.AssignedPressureDrop);
        var drop      = Num(() => c.PressureDrop);
        var coeff     = Num(() => c.Coefficient);
        var engage    = Num(() => c.EngagementLength);
        string dir    = Txt(() => c.Direction);
        string asgDir = Txt(() => c.AssignedFlowDirection);

        // "Carrying nothing" = every value that could express a load is zero or absent. On a
        // load-bearing category that is the finding; anywhere else it is simply how Revit works.
        bool anyLoad =
               (flow.HasValue     && Math.Abs(flow.Value) > 1e-9)
            || (demand.HasValue   && Math.Abs(demand.Value) > 1e-9)
            || (asgFlow.HasValue  && Math.Abs(asgFlow.Value) > 1e-9)
            || (kFactor.HasValue  && Math.Abs(kFactor.Value) > 1e-9)
            || (fixtureU.HasValue && Math.Abs(fixtureU.Value) > 1e-9);

        if (isLoadBearing)
        {
            shouldCarryLoad++;
            if (!anyLoad)
            {
                carryingNothing++;
                if (problems.Count < maxRowsListed)
                    problems.Add($"  {e.Category.Name} '{e.Name}' (Id {e.Id}) connector {Txt(() => c.Id)} [{domain}] — no flow, demand, assigned flow, K-factor or fixture units. Revit will calculate this run as carrying nothing.");
            }
        }

        if (!onlyProblems && rows.Count < maxRowsListed)
        {
            var bits = new List<string>();
            Action<string, double?> put = (label, v) =>
            {
                if (v.HasValue && Math.Abs(v.Value) > 1e-9) bits.Add($"{label} {Math.Round(v.Value, 4)}");
            };
            put("flow", flow); put("demand", demand); put("asgFlow", asgFlow);
            put("K", kFactor); put("FU", fixtureU); put("lossCoef", lossCoef);
            put("asgDrop", asgDrop); put("drop", drop); put("coeff", coeff); put("engage", engage);
            if (!string.IsNullOrEmpty(dir)) bits.Add("dir " + dir);
            if (!string.IsNullOrEmpty(asgDir) && asgDir != dir) bits.Add("asgDir " + asgDir);

            rows.Add($"  {e.Category?.Name} '{e.Name}' (Id {e.Id}) conn {Txt(() => c.Id)} [{domain}]"
                     + (connected ? "" : " NOT CONNECTED")
                     + (bits.Count > 0 ? " : " + string.Join(", ", bits) : " : (no engineering value set)"));
        }
    }
}

sb.AppendLine($"CONNECTOR LOADS — {connectorsRead} connector(s) on {elementsRead} element(s) that have any.");
if (connectorsRead == 0)
{
    sb.AppendLine("  None of the elements given carry connectors. Feed this MEP elements or equipment.");
}
else
{
    if (byDomain.Count > 0)
        sb.AppendLine("  by domain: " + string.Join(", ", byDomain.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
    if (unconnected > 0)
        sb.AppendLine($"  {unconnected} connector(s) are not joined to anything.");

    sb.AppendLine();
    if (shouldCarryLoad == 0)
    {
        sb.AppendLine("None of these elements is a category that normally carries a load (terminals, equipment, fixtures, sprinklers), so a blank engineering value here is expected, not a fault.");
    }
    else if (carryingNothing == 0)
    {
        sb.AppendLine($"All {shouldCarryLoad} connector(s) on load-bearing equipment carry a value. Revit's system calculation has real inputs to work from.");
    }
    else
    {
        sb.AppendLine($"! {carryingNothing} of {shouldCarryLoad} connector(s) on load-bearing equipment carry NO value at all:");
        foreach (var p in problems) sb.AppendLine(p);
        sb.AppendLine("  Each of these contributes ZERO to the system calculation. A pressure-drop or flow report over this system will come back clean and be wrong — the numbers are real, they are just the wrong ones. Fix these before quoting any system total.");
    }

    if (!onlyProblems && rows.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine($"EVERY CONNECTOR (first {rows.Count})");
        foreach (var r in rows) sb.AppendLine(r);
        if (connectorsRead > rows.Count)
            sb.AppendLine($"  ... {connectorsRead - rows.Count} more not listed (raise maxRowsListed, or set onlyProblems = true).");
    }

    sb.AppendLine();
    sb.AppendLine("A BLANK value means the property does not apply to that domain — a duct connector has no K-factor, an electrical one has no flow. Blank is never 'set to zero'.");
    sb.AppendLine("Values are in Revit's internal units. Convert before quoting them to anyone.");
}
