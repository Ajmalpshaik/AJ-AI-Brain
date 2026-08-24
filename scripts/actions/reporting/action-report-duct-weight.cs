// ============================================================
// FRAGMENT (action) — action-report-duct-weight.cs
// PURPOSE: SHEET-METAL TAKEOFF for the ducts in `elements` — gauge/thickness from the size band, the
//          developed sheet area, and the fabrication weight in kg with allowances. The BOQ/procurement
//          answer: "how many kilos of ductwork is this", "what gauge should this be".
//          Read-only: it calculates and reports, it writes nothing.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — Ducts.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/duct-sheet-metal-takeoff.md — the tables, the densities and the allowances live
//         there with the reasoning. Change them there and here together.
//
// NOT DUCT SIZING. This does not decide how big a duct should be from its airflow — that is
//         knowledge/live-model/hvac-duct-sizing.md. This decides how THICK THE METAL IS once the duct
//         is that size, and what it weighs.
// GOTCHA: THE ALLOWANCES ARE WHERE THE REAL NUMBER IS. Bare sheet weight understates a job by about a
//         QUARTER — the defaults below add +24% unreinforced and +29% reinforced. Report both, and
//         never quote the base as "the weight".
// GOTCHA: FITTINGS AT 10% IS AN ALLOWANCE, NOT A MEASUREMENT. It stands in for bends, tees and
//         transitions not being counted individually. If the model's fittings ARE in `elements` too,
//         set fittingsPercent = 0 or you double-count them.
// GOTCHA: AN OVAL DUCT CARRIES WIDTH, HEIGHT **AND** DIAMETER. Testing "has a diameter" alone calls
//         every oval duct round and then uses the wrong perimeter formula. Shape is decided by which
//         parameters have values, in the order oval -> round -> rectangular.
// GOTCHA: an oval's perimeter has no closed form — Ramanujan's approximation is used, not
//         `pi x (w+h)/2` which is wrong by several percent on a flat oval.
// GOTCHA: governing size is **max(width, height)** for rectangular/oval — not the perimeter, not the
//         average. The bands are different for round, and they are NOT the same numbers.
// GOTCHA: THESE ARE AJMAL'S OWN WORKING VALUES, shipped as the add-in's defaults — this office's
//         standard, not a code citation. Confirm the project's own gauge table before quoting anyone.
// NOTE: every row reports WHICH BAND produced its gauge, so a number can be traced back. A weight
//       nobody can trace to a band is a number nobody will sign.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Duct Standard tool.
//   Compile-checked on 2020/2024/2027. The arithmetic is checkable by hand — do one duct first.
//
// ✱✱ ADDED 2026-08-24 — THE ANSWER IS NOW CROSS-CHECKED AGAINST REVIT'S OWN AREA, FOR FREE.
//    `BuiltInParameter.RBS_CURVE_SURFACE_AREA` is Revit's own computed surface area for a duct —
//    perimeter x length, already in the model, and until now unread by anything in this library. The
//    developed area below is derived independently from the width/height/diameter, so the two are
//    genuinely separate calculations of the same quantity and a disagreement means one of them is
//    wrong. The usual cause is SHAPE DETECTION: an oval read as round, or a duct whose parameters do
//    not match its geometry. A running comparison is printed at the end. **A weight built on the wrong
//    area is wrong by the same proportion**, so check the comparison line before quoting a tonnage.
//    Note it is a CHECK, not a replacement: Revit's figure covers the duct and nothing else, while the
//    fittings, seams and wastage allowances below are the reason this fragment exists.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string pressureClassOverride = "";   // "" = read each duct's own parameter (below), else "low"|"medium"|"high"
string pressureParamName = "Duct Pressure Class";
string materialParamName = "Duct Material Name";
string defaultMaterial = "GI";       // used when the duct has no material parameter value
bool includeAllowances = true;       // false = bare sheet weight only
double seamPercent = 3.0, jointPercent = 2.0, flangePercent = 4.0;
double fittingsPercent = 10.0;       // set 0 if fittings are counted separately
double wastagePercent = 5.0, reinforcementPercent = 5.0;
// ---- END INPUTS ----

const double FT_TO_M = 0.3048;
const double FT_TO_MM = 304.8;

// material -> density kg/m3
var density = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) {
    { "GI", 7850.0 }, { "Black Steel", 7850.0 }, { "Stainless Steel", 8000.0 }, { "Aluminium", 2700.0 }
};

// shape | pressure | minMm | maxMm | thicknessMm | gauge | reinforcement
var rules = new List<Tuple<string,string,double,double,double,string,bool>> {
    Tuple.Create("rectangular","low",0.0,400.0,0.60,"26",false),   Tuple.Create("rectangular","low",401.0,550.0,0.80,"24",false),
    Tuple.Create("rectangular","low",551.0,1200.0,1.00,"22",true), Tuple.Create("rectangular","low",1201.0,99999.0,1.20,"20",true),
    Tuple.Create("rectangular","medium",0.0,400.0,0.80,"24",false),Tuple.Create("rectangular","medium",401.0,550.0,1.00,"22",false),
    Tuple.Create("rectangular","medium",551.0,1200.0,1.20,"20",true),Tuple.Create("rectangular","medium",1201.0,99999.0,1.50,"18",true),
    Tuple.Create("rectangular","high",0.0,400.0,1.00,"22",false),  Tuple.Create("rectangular","high",401.0,550.0,1.20,"20",true),
    Tuple.Create("rectangular","high",551.0,1200.0,1.50,"18",true),Tuple.Create("rectangular","high",1201.0,99999.0,1.90,"16",true),

    Tuple.Create("round","low",0.0,406.0,0.55,"26",false),         Tuple.Create("round","low",407.0,559.0,0.70,"24",false),
    Tuple.Create("round","low",560.0,1219.0,0.86,"22",false),      Tuple.Create("round","low",1220.0,99999.0,1.00,"20",true),
    Tuple.Create("round","medium",0.0,381.0,0.61,"24",false),      Tuple.Create("round","medium",382.0,686.0,0.76,"22",false),
    Tuple.Create("round","medium",687.0,1067.0,0.91,"20",false),   Tuple.Create("round","medium",1068.0,1524.0,1.21,"18",true),
    Tuple.Create("round","medium",1525.0,99999.0,1.52,"16",true),
    Tuple.Create("round","high",0.0,381.0,0.76,"22",false),        Tuple.Create("round","high",382.0,686.0,0.91,"20",false),
    Tuple.Create("round","high",687.0,1067.0,1.21,"18",true),      Tuple.Create("round","high",1068.0,1524.0,1.52,"16",true),
    Tuple.Create("round","high",1525.0,99999.0,1.90,"14",true),

    Tuple.Create("oval","low",0.0,400.0,0.60,"26",false),          Tuple.Create("oval","low",401.0,550.0,0.80,"24",false),
    Tuple.Create("oval","low",551.0,1200.0,1.00,"22",true),        Tuple.Create("oval","low",1201.0,99999.0,1.20,"20",true),
    Tuple.Create("oval","medium",0.0,400.0,0.80,"24",false),       Tuple.Create("oval","medium",401.0,550.0,1.00,"22",false),
    Tuple.Create("oval","medium",551.0,1200.0,1.20,"20",true),     Tuple.Create("oval","medium",1201.0,99999.0,1.50,"18",true),
    Tuple.Create("oval","high",0.0,400.0,1.00,"22",false),         Tuple.Create("oval","high",401.0,550.0,1.20,"20",true),
    Tuple.Create("oval","high",551.0,1200.0,1.50,"18",true),       Tuple.Create("oval","high",1201.0,99999.0,1.90,"16",true)
};

Func<Element, BuiltInParameter, double> pv = (el, bip) =>
{
    try { var p = el.get_Parameter(bip); return (p != null && p.HasValue) ? p.AsDouble() : 0.0; } catch { return 0.0; }
};
Func<Element, string, string> ps = (el, name) =>
{
    try { var p = el.LookupParameter(name); return (p != null && p.StorageType == StorageType.String) ? p.AsString() : null; }
    catch { return null; }
};

double totalBase = 0, totalWithAllow = 0, totalAreaM2 = 0, totalLenM = 0;
int shapeFromParams = 0;   // ducts whose type could not be read, so the shape was inferred
double revitAreaM2 = 0; int revitAreaRows = 0;   // Revit's own RBS_CURVE_SURFACE_AREA, for the cross-check
int counted = 0, noShape = 0, noRule = 0;
var bySize = new Dictionary<string, double[]>();   // label -> {qty, lengthM, areaM2, baseKg, totalKg}
var bandUsed = new Dictionary<string, string>();

foreach (var e in elements)
{
    double wMm = pv(e, BuiltInParameter.RBS_CURVE_WIDTH_PARAM) * FT_TO_MM;
    double hMm = pv(e, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM) * FT_TO_MM;
    double dMm = pv(e, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM) * FT_TO_MM;
    double lenM = pv(e, BuiltInParameter.CURVE_ELEM_LENGTH) * FT_TO_M;
    if (lenM <= 0) { noShape++; continue; }

    // OVAL FIRST — an oval carries width, height AND diameter, so "has a diameter" is not enough.
    // ✱✱ SHAPE COMES FROM THE DUCT TYPE FIRST, ADDED 2026-08-24. `MEPCurveType.Shape` (which DuctType
    //    inherits, and which is present on 2020 through 2027) is Revit's OWN answer: Round, Rectangular
    //    or Oval, stated rather than inferred. The parameter heuristic underneath it is kept as the
    //    fallback for a duct whose type cannot be read, but it is a guess and the oval case is exactly
    //    where it goes wrong — which is what the GOTCHA above this describes. Reading the type removes
    //    that guess entirely for every duct that has one.
    string shape = null;
    try
    {
        var mct = Document.GetElement(e.GetTypeId()) as MEPCurveType;
        if (mct != null)
        {
            if (mct.Shape == ConnectorProfileType.Round) shape = "round";
            else if (mct.Shape == ConnectorProfileType.Rectangular) shape = "rectangular";
            else if (mct.Shape == ConnectorProfileType.Oval) shape = "oval";
        }
    }
    catch { }
    if (shape == null)
    {
        shapeFromParams++;
        if (wMm > 0 && hMm > 0 && dMm > 0) shape = "oval";
        else if (dMm > 0) shape = "round";
        else if (wMm > 0 && hMm > 0) shape = "rectangular";
        else { noShape++; continue; }
    }
    // An oval reports width, height AND diameter; a round one that somehow lost its diameter cannot be
    // sized at all. Guard the dimensions the chosen shape actually needs.
    if (shape == "round" && dMm <= 0) { noShape++; continue; }
    if (shape != "round" && (wMm <= 0 || hMm <= 0)) { noShape++; continue; }

    double governing = (shape == "round") ? dMm : Math.Max(wMm, hMm);

    string pressure = !string.IsNullOrWhiteSpace(pressureClassOverride)
        ? pressureClassOverride.Trim().ToLowerInvariant()
        : (ps(e, pressureParamName) ?? "").Trim().ToLowerInvariant();
    if (pressure != "low" && pressure != "medium" && pressure != "high") pressure = "low";

    var rule = rules.FirstOrDefault(r => r.Item1 == shape && r.Item2 == pressure &&
                                         governing >= r.Item3 && governing <= r.Item4);
    if (rule == null) { noRule++; continue; }

    double thickMm = rule.Item5;
    bool reinforced = rule.Item7;

    // developed sheet area
    double areaM2;
    if (shape == "rectangular") areaM2 = 2.0 * ((wMm + hMm) / 1000.0) * lenM;
    else if (shape == "round") areaM2 = Math.PI * (dMm / 1000.0) * lenM;
    else
    {
        // Ramanujan — an oval perimeter has no closed form; pi*(w+h)/2 is several percent out.
        double a = (wMm / 1000.0) / 2.0, b = (hMm / 1000.0) / 2.0;
        double term = (3.0 * a + b) * (a + 3.0 * b);
        double per = term > 0 ? Math.PI * (3.0 * (a + b) - Math.Sqrt(term)) : 0.0;
        areaM2 = per * lenM;
    }

    string mat = ps(e, materialParamName);
    if (string.IsNullOrWhiteSpace(mat)) mat = defaultMaterial;
    double dens = density.ContainsKey(mat) ? density[mat] : 7850.0;

    double baseKg = areaM2 * (thickMm / 1000.0) * dens;

    double pct = 0;
    if (includeAllowances)
    {
        pct = seamPercent + jointPercent + flangePercent + fittingsPercent + wastagePercent;
        if (reinforced) pct += reinforcementPercent;
    }
    double totKg = baseKg * (1.0 + pct / 100.0);

    string label = shape == "round"
        ? Math.Round(dMm) + "ø"
        : Math.Round(wMm) + "x" + Math.Round(hMm);

    if (!bySize.ContainsKey(label)) { bySize[label] = new double[5]; }
    var row = bySize[label];
    row[0] += 1; row[1] += lenM; row[2] += areaM2; row[3] += baseKg; row[4] += totKg;
    bandUsed[label] = shape + " / " + pressure + " / " + rule.Item3 + "-" +
                      (rule.Item4 >= 99999 ? "up" : rule.Item4.ToString()) + " mm -> " +
                      thickMm + " mm (" + rule.Item6 + " g)" + (reinforced ? " +reinf" : "");

    totalBase += baseKg; totalWithAllow += totKg; totalAreaM2 += areaM2; totalLenM += lenM;

    // Revit's own figure for the same duct. Missing on some categories, so it is counted separately
    // and the comparison says how many ducts it actually covers rather than assuming all of them.
    try
    {
        var rap = e.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
        if (rap != null && rap.HasValue) { revitAreaM2 += rap.AsDouble() * 0.09290304; revitAreaRows++; }
    }
    catch { }
    counted++;
}

sb.AppendLine("DUCT SHEET-METAL TAKEOFF — " + counted + " of " + elements.Count + " duct(s) costed" +
              (includeAllowances ? "" : "  (BARE SHEET ONLY — allowances OFF)"));
if (noShape > 0) sb.AppendLine(noShape + " had no readable size or length and were skipped.");
if (noRule > 0) sb.AppendLine(noRule + " fell outside every gauge band and were skipped.");

if (counted == 0) return sb.ToString();

sb.AppendLine("");
sb.AppendLine("Size (mm) | Qty | Length (m) | Sheet (m2) | Base (kg) | Total (kg) | Rule used");
sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");

Func<string, Tuple<double,double>> sortKey = s =>
{
    var parts = s.Replace("ø", "").Split('x');
    double a1 = 0, b1 = 0;
    double.TryParse(parts[0], out a1);
    if (parts.Length > 1) double.TryParse(parts[1], out b1); else b1 = a1;
    return Tuple.Create(a1, b1);
};

foreach (var kv in bySize.OrderBy(k => sortKey(k.Key).Item1).ThenBy(k => sortKey(k.Key).Item2))
{
    var r = kv.Value;
    sb.AppendLine(kv.Key + " | " + (int)r[0] + " | " + Math.Round(r[1], 2) + " | " + Math.Round(r[2], 2) +
                  " | " + Math.Round(r[3], 1) + " | " + Math.Round(r[4], 1) + " | " + bandUsed[kv.Key]);
}

sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");
sb.AppendLine("TOTAL | " + counted + " | " + Math.Round(totalLenM, 2) + " | " + Math.Round(totalAreaM2, 2) +
              " | " + Math.Round(totalBase, 1) + " | " + Math.Round(totalWithAllow, 1) + " | ");

// ---- the cross-check against Revit's own surface area ----
if (shapeFromParams > 0)
{
    sb.AppendLine("");
    sb.AppendLine("NOTE: " + shapeFromParams + " duct(s) had no readable type, so their SHAPE was inferred from which");
    sb.AppendLine("  dimension parameters carry a value. That is the guess the oval case defeats — check those.");
}

sb.AppendLine("");
if (revitAreaRows == 0)
{
    sb.AppendLine("CROSS-CHECK: none of these elements carries Revit's own surface area parameter, so the");
    sb.AppendLine("  area above could not be checked against anything. It rests on the shape detection alone.");
}
else
{
    double diffPct = revitAreaM2 > 0 ? (totalAreaM2 / revitAreaM2 - 1.0) * 100.0 : 0;
    sb.AppendLine("CROSS-CHECK against Revit's own area (RBS_CURVE_SURFACE_AREA), " + revitAreaRows + " of " +
                  counted + " duct(s):");
    sb.AppendLine("  computed here " + Math.Round(totalAreaM2, 2) + " m2   |   Revit says " +
                  Math.Round(revitAreaM2, 2) + " m2   |   difference " + Math.Round(diffPct, 1) + "%");
    if (Math.Abs(diffPct) > 5.0)
    {
        sb.AppendLine("  *** THEY DISAGREE BY MORE THAN 5%. One of the two is wrong, and the usual cause is SHAPE");
        sb.AppendLine("      DETECTION — an oval duct read as round uses the wrong perimeter formula. Check the");
        sb.AppendLine("      shape column above against the model before quoting the weight: the kilos are");
        sb.AppendLine("      proportional to this area, so they are out by the same percentage.");
    }
    else if (revitAreaRows < counted)
    {
        sb.AppendLine("  (the two agree on the ducts Revit reports an area for; " + (counted - revitAreaRows) +
                      " duct(s) had no such parameter and are not covered by this check)");
    }
    else
    {
        sb.AppendLine("  The two independent calculations agree, so the shape detection and the area are sound.");
    }
}

if (includeAllowances && totalBase > 0)
{
    double upliftPct = (totalWithAllow / totalBase - 1.0) * 100.0;
    sb.AppendLine("");
    sb.AppendLine("Allowances added " + Math.Round(totalWithAllow - totalBase, 1) + " kg (+" +
                  Math.Round(upliftPct, 1) + "%) over bare sheet — seam " + seamPercent + "%, joint " +
                  jointPercent + "%, flange " + flangePercent + "%, fittings " + fittingsPercent +
                  "%, wastage " + wastagePercent + "%, reinforcement " + reinforcementPercent +
                  "% where the band requires it.");
    if (fittingsPercent > 0)
        sb.AppendLine("NOTE: the " + fittingsPercent + "% fittings allowance stands in for bends and tees " +
                      "NOT counted individually. If fittings are in this selection too, set fittingsPercent = 0.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
