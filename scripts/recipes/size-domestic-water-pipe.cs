// ============================================================
// FRAGMENT (recipe) — size-domestic-water-pipe.cs
// PURPOSE: Size a COLD or HOT WATER SUPPLY run the standard way — water supply fixture units (WSFU) ->
//          probable demand in GPM off Hunter's curve -> smallest pipe that holds the velocity limit ->
//          Hazen-Williams friction loss. Answers "what size does this run need to be" for a branch, a
//          riser, or the incoming main.
//          Two ways in: give it a WSFU total directly, OR let it count Plumbing Fixtures out of the
//          live model and add up their fixture units by family/type name.
// STANDALONE — needs no filter above. Read-only: it calculates and reports, it changes nothing.
// SOURCE: knowledge/plumbing-pipe-sizing.md — the tables, the limits and the sanity checks live there.
//
// NOT FOR DRAINAGE OR VENT SIZING. Different fixture-unit tables entirely (DFU, not WSFU) and different
//         rules. Using this for a waste stack gives a confidently wrong answer.
// GOTCHA: `velocityLimitMs` IS A PROJECT NUMBER, NOT A DEFAULT. Ask for it. The 2.0 below is a
//         placeholder so the fragment runs, not a recommendation — see START-HERE.md rule 3.
// GOTCHA: COLD vs HOT vs TOTAL. Sizing a hot run off the total column oversizes it badly; water closets
//         and urinals contribute ZERO to hot. Set `demandColumn` to match the run being sized.
// GOTCHA: the flush-VALVE curve does not begin until 5 WSFU. Below that this falls back to the
//         flush-tank figure, because there is no flush-valve figure to use. That is correct, not a bug.
// GOTCHA: the curve STOPS at 1000 WSFU / 239 GPM and CLAMPS. Feed it 5000 fixture units and it still
//         says 239 GPM. The report says so explicitly when the total is off the end of the table —
//         read that line rather than quoting the flow.
// GOTCHA: sizes are chosen against REAL INTERNAL DIAMETER, never nominal. A "1 inch" pipe is 26.6 mm
//         bore in uPVC Sch 40 and 22.9 mm in CPVC SDR 11 — 14% on bore is 30% on area.
// NOTE:   model mode matches fixtures by looking for each table name inside the fixture's FAMILY and
//         TYPE name, longest name first. Anything it cannot match is listed by name in the report so
//         nothing is silently counted as zero. Check that list before trusting the total.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Pipe Sizing tool, whose
//   tables and math are Ajmal's own working method. The arithmetic is checkable by hand: run it on one
//   known case first (e.g. 10 WSFU flush tank -> 14.6 GPM) and confirm the number before a real job.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double fixtureUnitsTotal = 20.0;     // WSFU downstream of this run; ignored when countFromModel = true
bool   countFromModel    = false;    // true = count Plumbing Fixtures in the model instead
bool   activeViewOnly    = false;    // model mode: limit the count to the active view
string demandColumn      = "total";  // "cold" | "hot" | "total" — which run is being sized
bool   isFlushValve      = true;     // true = flush VALVE system, false = flush TANK
int    materialIndex     = 0;        // 0 uPVC Sch 40 | 1 CPVC SDR 11 | 2 PPR SDR 7.4 | 3 Copper Type L
double velocityLimitMs   = 2.0;      // ASK FOR THIS. m/s. Placeholder only.
double developedLengthM  = 0;        // >0 also reports total head loss over this length (0 = skip)
// ---- END INPUTS ----

const double GPM_TO_M3S = 0.0000630901964;

string[] materialNames = { "uPVC (Sch 40)", "CPVC (SDR 11)", "PPR (SDR 7.4)", "Copper (Type L)" };
double[] cFactors      = { 150, 150, 150, 130 };
if (materialIndex < 0 || materialIndex > 3) materialIndex = 0;

// internal diameters in mm — REAL bore, not nominal
double[][] idTables = new double[][] {
    new double[] { 15.80, 20.93, 26.64, 35.05, 40.89, 52.50, 62.71, 77.93, 102.26, 154.05 },
    new double[] { 12.42, 18.16, 22.89, 28.58, 33.88, 44.17, 55.00, 65.00,  88.00, 131.00 },
    new double[] { 14.40, 18.00, 23.20, 29.00, 36.20, 45.80, 54.40, 65.40,  79.80, 116.00 },
    new double[] { 13.84, 19.94, 26.04, 32.13, 38.23, 50.42, 62.61, 74.80,  99.19, 148.00 }
};
string[][] idLabels = new string[][] {
    new string[] { "1/2\"","3/4\"","1\"","1 1/4\"","1 1/2\"","2\"","2 1/2\"","3\"","4\"","6\"" },
    new string[] { "1/2\"","3/4\"","1\"","1 1/4\"","1 1/2\"","2\"","2 1/2\"","3\"","4\"","6\"" },
    new string[] { "20mm","25mm","32mm","40mm","50mm","63mm","75mm","90mm","110mm","160mm" },
    new string[] { "1/2\"","3/4\"","1\"","1 1/4\"","1 1/2\"","2\"","2 1/2\"","3\"","4\"","6\"" }
};

// Hunter's curve: WSFU | flush tank GPM | flush valve GPM (0 = no flush-valve figure at that WSFU)
double[][] curve = new double[][] {
    new double[]{1,3.0,0},      new double[]{2,5.0,0},      new double[]{3,6.5,0},      new double[]{4,8.0,0},
    new double[]{5,9.4,15.0},   new double[]{6,10.7,17.4},  new double[]{7,11.8,19.8},  new double[]{8,12.8,22.2},
    new double[]{9,13.7,24.6},  new double[]{10,14.6,27.0}, new double[]{11,15.4,27.8}, new double[]{12,16.0,28.6},
    new double[]{13,16.5,29.4}, new double[]{14,17.0,30.2}, new double[]{15,17.5,31.0}, new double[]{16,18.0,31.8},
    new double[]{17,18.4,32.6}, new double[]{18,18.8,33.4}, new double[]{19,19.2,34.2}, new double[]{20,19.6,35.0},
    new double[]{25,21.5,38.0}, new double[]{30,23.3,41.0}, new double[]{35,24.9,43.8}, new double[]{40,26.3,46.5},
    new double[]{45,27.7,49.2}, new double[]{50,29.1,51.5}, new double[]{60,32.0,54.0}, new double[]{70,35.0,58.0},
    new double[]{80,38.0,62.0}, new double[]{90,41.0,66.0}, new double[]{100,43.5,71.0},new double[]{120,48.0,77.0},
    new double[]{140,52.5,83.0},new double[]{160,57.0,89.0},new double[]{180,61.0,95.0},new double[]{200,65.0,101.0},
    new double[]{225,70.0,107.0},new double[]{250,75.0,113.0},new double[]{275,80.0,118.0},new double[]{300,85.0,124.0},
    new double[]{400,105.0,148.0},new double[]{500,124.0,170.0},new double[]{750,170.0,208.0},new double[]{1000,208.0,239.0}
};

// fixture name -> cold, hot, total WSFU
var fixtureTable = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase) {
    {"Bathroom Group Private Flush Tank", new double[]{2.7,1.5,3.6}},
    {"Bathroom Group Private Flush Valve", new double[]{6.0,3.0,8.0}},
    {"Bathtub Private", new double[]{1.0,1.0,1.4}},          {"Bathtub Public", new double[]{3.0,3.0,4.0}},
    {"Bidet", new double[]{1.5,1.5,2.0}},                    {"Combination Fixture", new double[]{2.25,2.25,3.0}},
    {"Dishwashing Machine Private", new double[]{0.0,1.4,1.4}},
    {"Dishwashing Machine Commercial", new double[]{0.0,2.0,2.0}},
    {"Drinking Fountain", new double[]{0.25,0.0,0.25}},      {"Kitchen Sink Private", new double[]{1.0,1.0,1.4}},
    {"Kitchen Sink Restaurant", new double[]{3.0,3.0,4.0}},  {"Bar Sink", new double[]{1.0,1.0,1.4}},
    {"Laundry Trays", new double[]{1.0,1.0,1.4}},            {"Lavatory Private", new double[]{0.5,0.5,0.7}},
    {"Lavatory Public", new double[]{1.5,1.5,2.0}},          {"Service Sink", new double[]{2.25,2.25,3.0}},
    {"Mop Basin", new double[]{2.25,2.25,3.0}},              {"Janitor Sink", new double[]{2.25,2.25,3.0}},
    {"Wash Sink", new double[]{1.5,1.5,2.0}},                {"Shower Head Private", new double[]{1.0,1.0,1.4}},
    {"Shower Head Public", new double[]{3.0,3.0,4.0}},       {"Urinal 1\" Flush Valve", new double[]{10.0,0.0,10.0}},
    {"Urinal 3/4\" Flush Valve", new double[]{5.0,0.0,5.0}}, {"Urinal Flush Tank", new double[]{3.0,0.0,3.0}},
    {"Water Closet Private Flush Tank", new double[]{2.2,0.0,2.2}},
    {"Water Closet Private Flush Valve", new double[]{6.0,0.0,6.0}},
    {"Water Closet Public Flush Tank", new double[]{5.0,0.0,5.0}},
    {"Water Closet Public Flush Valve", new double[]{10.0,0.0,10.0}},
    {"Clinic Sink", new double[]{8.0,0.0,8.0}},
    {"Washing Machine Private", new double[]{1.0,1.0,1.4}},  {"Washing Machine Public (15 Lb)", new double[]{3.0,3.0,4.0}},
    {"Washing Machine Public", new double[]{2.25,2.25,3.0}},
    {"Hose Bibb (1/2\" connection)", new double[]{2.5,0.0,2.5}},
    {"Hose Bibb (3/4\" connection)", new double[]{3.0,0.0,3.0}}
};

int column = demandColumn.Equals("cold", StringComparison.OrdinalIgnoreCase) ? 0
           : demandColumn.Equals("hot",  StringComparison.OrdinalIgnoreCase) ? 1 : 2;

// ---------- optionally count fixtures out of the live model ----------
var matchedCounts = new Dictionary<string, int>();
var unmatched = new List<string>();

if (countFromModel)
{
    var collector = activeViewOnly
        ? new FilteredElementCollector(Document, Document.ActiveView.Id)
        : new FilteredElementCollector(Document);

    var fixtures = collector.OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                            .WhereElementIsNotElementType().ToElements();

    // longest table name first, so "Urinal 1" Flush Valve" is tried before "Urinal Flush Tank"
    var keysByLength = fixtureTable.Keys.OrderByDescending(k => k.Length).ToList();

    fixtureUnitsTotal = 0;
    foreach (Element f in fixtures)
    {
        var fi = f as FamilyInstance;
        string famName = (fi != null && fi.Symbol != null && fi.Symbol.Family != null) ? fi.Symbol.Family.Name : "";
        string typeName = (fi != null && fi.Symbol != null) ? fi.Symbol.Name : "";
        string haystack = (famName + " " + typeName);

        string hit = keysByLength.FirstOrDefault(k => haystack.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        if (hit == null)
        {
            string label = famName + " : " + typeName;
            if (!unmatched.Contains(label)) unmatched.Add(label);
            continue;
        }

        if (!matchedCounts.ContainsKey(hit)) matchedCounts[hit] = 0;
        matchedCounts[hit]++;
        fixtureUnitsTotal += fixtureTable[hit][column];
    }
}

// ---------- Hunter's curve, with linear interpolation ----------
double gpm = 0;
bool offTheTable = false;

if (fixtureUnitsTotal > 0)
{
    if (fixtureUnitsTotal >= curve[curve.Length - 1][0])
    {
        offTheTable = fixtureUnitsTotal > curve[curve.Length - 1][0];
        double[] last = curve[curve.Length - 1];
        gpm = (isFlushValve && last[2] > 0) ? last[2] : last[1];
    }
    else
    {
        int upper = 0;
        for (int i = 0; i < curve.Length; i++) { if (fixtureUnitsTotal <= curve[i][0]) { upper = i; break; } }
        int lower = upper > 0 ? upper - 1 : 0;

        double lowVal  = (isFlushValve && curve[lower][2] > 0) ? curve[lower][2] : curve[lower][1];
        double highVal = (isFlushValve && curve[upper][2] > 0) ? curve[upper][2] : curve[upper][1];

        gpm = Math.Abs(curve[upper][0] - curve[lower][0]) < 1e-9
            ? lowVal
            : lowVal + (highVal - lowVal) * ((fixtureUnitsTotal - curve[lower][0]) / (curve[upper][0] - curve[lower][0]));
    }
    gpm = Math.Round(gpm, 2);
}

// ---------- velocity sizing, then friction loss ----------
sb.AppendLine("DOMESTIC WATER PIPE SIZING — " + (column == 0 ? "COLD" : column == 1 ? "HOT" : "TOTAL") +
              " run, " + (isFlushValve ? "flush valve" : "flush tank") + " system");
sb.AppendLine("Material: " + materialNames[materialIndex] + " (C = " + cFactors[materialIndex] +
              ") | velocity limit: " + velocityLimitMs + " m/s");
sb.AppendLine("");

if (countFromModel)
{
    sb.AppendLine("Counted from the model" + (activeViewOnly ? " (active view only)" : "") + ":");
    sb.AppendLine("Fixture | Qty | WSFU each | WSFU");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var kv in matchedCounts.OrderBy(k => k.Key))
        sb.AppendLine(kv.Key + " | " + kv.Value + " | " + fixtureTable[kv.Key][column] + " | " +
                      Math.Round(kv.Value * fixtureTable[kv.Key][column], 2));
    sb.AppendLine("");
    if (unmatched.Count > 0)
    {
        sb.AppendLine("NOT MATCHED to the fixture-unit table — counted as ZERO, check these:");
        foreach (string u in unmatched) sb.AppendLine("  - " + u);
        sb.AppendLine("");
    }
}

sb.AppendLine("Total fixture units : " + Math.Round(fixtureUnitsTotal, 2) + " WSFU");

if (fixtureUnitsTotal <= 0) { sb.AppendLine("No fixture units — nothing to size."); return sb.ToString(); }

if (offTheTable)
    sb.AppendLine("*** OFF THE TABLE: Hunter's curve stops at 1000 WSFU. The flow below is CLAMPED at the " +
                  "table maximum and is NOT the real demand for this load. Size this by another method. ***");
if (isFlushValve && fixtureUnitsTotal < 5)
    sb.AppendLine("(Below 5 WSFU the flush-valve curve does not exist — the flush-tank figure is used.)");

sb.AppendLine("Probable demand     : " + gpm + " GPM");

double q = GPM_TO_M3S * gpm;
double requiredIdMm = Math.Round(Math.Sqrt((q / velocityLimitMs) * (4.0 / Math.PI)) * 1000.0, 2);
sb.AppendLine("Required bore       : " + requiredIdMm + " mm (minimum, to hold " + velocityLimitMs + " m/s)");

double[] ids = idTables[materialIndex];
string[] labels = idLabels[materialIndex];
int pick = -1;
for (int i = 0; i < ids.Length; i++) { if (ids[i] >= requiredIdMm) { pick = i; break; } }
bool oversized = pick < 0;
if (oversized) pick = ids.Length - 1;

double chosenIdMm = ids[pick];
double actualVelocity = q / (Math.PI * Math.Pow((chosenIdMm / 1000.0) / 2.0, 2));
double frictionPsiPer100Ft = 4.52 * Math.Pow(gpm, 1.852) /
                             (Math.Pow(cFactors[materialIndex], 1.852) * Math.Pow(chosenIdMm / 25.4, 4.87));

sb.AppendLine("");
sb.AppendLine("SELECTED SIZE       : " + labels[pick] + "  (" + chosenIdMm + " mm bore)");
sb.AppendLine("Actual velocity     : " + Math.Round(actualVelocity, 2) + " m/s  (limit " + velocityLimitMs + ")");
sb.AppendLine("Friction loss       : " + Math.Round(frictionPsiPer100Ft, 2) + " psi per 100 ft");

if (developedLengthM > 0)
{
    double lengthFt = developedLengthM * 3.28084;
    sb.AppendLine("Over " + developedLengthM + " m developed length: " +
                  Math.Round(frictionPsiPer100Ft * lengthFt / 100.0, 2) + " psi (" +
                  Math.Round(frictionPsiPer100Ft * lengthFt / 100.0 * 0.703, 2) + " m head) — " +
                  "ADD A FITTINGS ALLOWANCE, this is straight pipe only");
}

if (oversized)
    sb.AppendLine("*** NOTHING IN THE " + materialNames[materialIndex].ToUpper() + " TABLE IS BIG ENOUGH — " +
                  "the largest size (" + labels[pick] + ") is shown and it EXCEEDS the velocity limit " +
                  "at " + Math.Round(actualVelocity, 2) + " m/s. Split the load or use a larger range. ***");

return sb.ToString();
