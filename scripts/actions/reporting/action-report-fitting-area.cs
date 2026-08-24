// ============================================================
// FRAGMENT (action) — action-report-fitting-area.cs
// PURPOSE: MEASURE the sheet-metal area of duct FITTINGS — bends, tees, transitions, reducers, caps —
//          instead of estimating them. Sums every face of every solid and subtracts the open ends at
//          the connectors, which is what turns a fitting's geometry into its developed metal area.
//          Also names what each fitting IS (`PartType`) and its size, so a takeoff can be read by
//          type. Read-only.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above — duct fittings
//          (OST_DuctFitting), and it works the same for pipe/conduit/cable-tray fittings.
// NOT STANDALONE — see scripts/README.md for how to compose.
// RELATED: action-report-duct-weight.cs — the STRAIGHT ducts, with gauge and weight. Its own header
//          says "FITTINGS AT 10% IS AN ALLOWANCE, NOT A MEASUREMENT... set fittingsPercent = 0 if the
//          model's fittings ARE in `elements` too." Until now nothing in this library could produce
//          the measurement that sentence points at. **This is it.** Run this over the fittings, set
//          `fittingsPercent = 0` there, and the two together are a measured takeoff instead of a
//          measured trunk plus a guess.
//
// ✱✱ THE SUBTRACTION IS THE WHOLE TECHNIQUE, AND WITHOUT IT THE NUMBER IS TOO BIG.
//    A fitting's solid is closed: Revit caps it at each connector. Summing `face.Area` therefore
//    includes those end disks, which are holes in the real thing — the duct continues through them
//    and there is no metal there. Each connector's own opening is subtracted:
//        round        -> pi * r^2      (Connector.Radius)
//        rectangular  -> H * W         (Connector.Height * Connector.Width)
//    On a small elbow the caps are a large share of the total, so leaving them in overstates a bend
//    badly and a long transition hardly at all — which is worse than a uniform error, because it
//    biases the mix rather than the total.
//
// ✱✱ IT REPORTS THE GROSS AND THE NET, NOT JUST THE ANSWER. If the subtraction ever exceeds the face
//    total — a fitting whose solid did not come through cleanly, or an oval connector, which has no
//    simple opening area — the row is flagged SUSPECT and keeps the gross figure rather than a
//    negative one. A takeoff that quietly returns a nonsense area is worse than one that says it
//    could not measure that fitting.
//
// ✱✱ PARTTYPE IS HOW YOU KNOW WHAT A FITTING IS, NOT ITS NAME. `(instance.MEPModel as MechanicalFitting)
//    .PartType` returns Elbow / Tee / Cross / Transition / TapAdjustable / Union / Cap / MultiPort.
//    Family names are whatever somebody typed; PartType is what Revit routes on. A Union is a coupling
//    with no metal of its own and is normally EXCLUDED from a sheet takeoff — `excludeUnions` does
//    that and says how many it dropped. Pipe and cable-tray fittings expose the same idea through
//    their own MEPModel types, so PartType simply comes back blank for them and the area still works.
//
// GOTCHA: this measures the OUTER SURFACE, which for sheet metal is the developed area. It is NOT the
//         same as a flat-pattern nesting area — a fabricator adds laps, seams and offcut. Those
//         allowances live in action-report-duct-weight.cs and are deliberately not repeated here.
// GOTCHA: insulation and lining are SEPARATE ELEMENTS and are not included. If the filter above caught
//         them too they are reported on their own rows, not folded into the fitting.
// GOTCHA: a fitting inside a nested family may be reported twice if the filter caught both — see
//         action-report-nested-families.cs before trusting a total over an assembled unit.
//
// ✱✱ NOT YET RUN ON A REAL MODEL (written 2026-08-24, compile-checked on 2020/2024/2027). Read-only.
//    Check ONE elbow by hand first: its net area should be near the duct perimeter times the centreline
//    arc length. If it is roughly double, the connector subtraction did not happen.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool excludeUnions = true;    // a Union/coupling carries no sheet metal of its own
int maxRowsListed = 120;
bool listEveryFitting = true; // false = summary by PartType only
// ---- END INPUTS ----

const double SQFT_TO_SQM = 0.09290304;
const double MM = 304.8;

var geomOpts = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, DetailLevel = ViewDetailLevel.Fine };

Func<Element, List<Solid>> solidsOf = el =>
{
    var found = new List<Solid>();
    GeometryElement ge = null;
    try { ge = el.get_Geometry(geomOpts); } catch { }
    if (ge == null) return found;
    var stack = new Stack<GeometryElement>();
    stack.Push(ge);
    while (stack.Count > 0)
        foreach (var go in stack.Pop())
        {
            var s = go as Solid;
            if (s != null)
            {
                bool ok = false;
                try { ok = !s.Faces.IsEmpty && s.Volume > 0; } catch { }
                if (ok) found.Add(s);
                continue;
            }
            var gi = go as GeometryInstance;
            if (gi != null) { try { stack.Push(gi.GetInstanceGeometry()); } catch { } }
        }
    return found;
};

Func<Element, List<Connector>> connectorsOf = el =>
{
    var list = new List<Connector>();
    ConnectorManager cm = null;
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) { try { cm = fi.MEPModel.ConnectorManager; } catch { } }
    var mc = el as MEPCurve;
    if (cm == null && mc != null) { try { cm = mc.ConnectorManager; } catch { } }
    if (cm == null) return list;
    try { foreach (Connector c in cm.Connectors) list.Add(c); } catch { }
    return list;
};

Func<Element, string> partTypeOf = el =>
{
    var fi = el as FamilyInstance;
    if (fi == null) return "";
    var mf = fi.MEPModel as MechanicalFitting;
    if (mf == null) return "";
    try { return mf.PartType.ToString(); } catch { return ""; }
};

Func<Element, string> sizeOf = el =>
{
    try
    {
        var p = el.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
        if (p != null && p.HasValue) return p.AsString() ?? "";
    }
    catch { }
    return "";
};

// ---------- measure ----------
var rows = new List<string>();
var byType = new Dictionary<string, double>();
var countByType = new Dictionary<string, int>();
double totalNet = 0, totalGross = 0;
int measured = 0, noGeometry = 0, suspect = 0, unionsDropped = 0;

foreach (var el in elements)
{
    if (el == null) continue;

    string pt = partTypeOf(el);
    if (excludeUnions && pt == "Union") { unionsDropped++; continue; }

    var solids = solidsOf(el);
    if (solids.Count == 0)
    {
        noGeometry++;
        rows.Add($"{el.Id} | {el.Name} | {(pt.Length > 0 ? pt : "-")} | {sizeOf(el)} | — | — | NO GEOMETRY");
        continue;
    }

    double gross = 0;
    foreach (var s in solids)
    {
        try { foreach (Face f in s.Faces) gross += f.Area; }
        catch { }
    }

    double openings = 0;
    int ovalEnds = 0;
    foreach (var c in connectorsOf(el))
    {
        try
        {
            if (c.Shape == ConnectorProfileType.Round) openings += Math.PI * c.Radius * c.Radius;
            else if (c.Shape == ConnectorProfileType.Rectangular) openings += c.Height * c.Width;
            else ovalEnds++;   // Oval has no simple opening area — counted, never guessed at
        }
        catch { }
    }

    double net = gross - openings;
    bool bad = net <= 0 || openings > gross;
    if (bad) { suspect++; net = gross; }

    measured++;
    totalGross += gross;
    totalNet += net;

    string key = pt.Length > 0 ? pt : (el.Category != null ? el.Category.Name : "(unknown)");
    if (!byType.ContainsKey(key)) { byType[key] = 0; countByType[key] = 0; }
    byType[key] += net * SQFT_TO_SQM;
    countByType[key]++;

    string note = bad ? "SUSPECT — ends exceeded the surface, gross kept" : (ovalEnds > 0 ? $"{ovalEnds} oval end(s) not subtracted" : "");
    rows.Add($"{el.Id} | {el.Name} | {(pt.Length > 0 ? pt : "-")} | {sizeOf(el)} | "
        + $"{Math.Round(gross * SQFT_TO_SQM, 3)} | {Math.Round(net * SQFT_TO_SQM, 3)} | {note}");
}

// ---------- output ----------
sb.AppendLine($"FITTING SHEET AREA — {measured} fitting(s) measured of {elements.Count} given"
    + (unionsDropped > 0 ? $", {unionsDropped} Union/coupling dropped" : "")
    + (noGeometry > 0 ? $", {noGeometry} with no geometry" : ""));
sb.AppendLine($"NET (metal) {Math.Round(totalNet * SQFT_TO_SQM, 2):N2} m²   |   GROSS (with the open ends counted) {Math.Round(totalGross * SQFT_TO_SQM, 2):N2} m²");
if (totalGross > 0)
    sb.AppendLine($"The open ends account for {Math.Round((1 - totalNet / totalGross) * 100, 1)}% of the raw surface — that is the part that is NOT metal.");
if (suspect > 0)
    sb.AppendLine($"⚠ {suspect} fitting(s) SUSPECT — the connector openings came out larger than the whole surface. Those rows kept the gross figure and are marked; do not quote them without looking.");

sb.AppendLine();
sb.AppendLine("BY TYPE");
sb.AppendLine("PartType | Count | Net area (m²)");
sb.AppendLine("--- | ---: | ---:");
foreach (var kv in byType.OrderByDescending(k => k.Value))
    sb.AppendLine($"{kv.Key} | {countByType[kv.Key]} | {Math.Round(kv.Value, 2):N2}");

if (listEveryFitting && rows.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("EVERY FITTING");
    sb.AppendLine("Id | Name | PartType | Size | Gross m² | Net m² | Note");
    sb.AppendLine("--- | --- | --- | --- | ---: | ---: | ---");
    foreach (var r in rows.Take(maxRowsListed)) sb.AppendLine(r);
    if (rows.Count > maxRowsListed)
        sb.AppendLine($"... {rows.Count - maxRowsListed} more not listed (raise maxRowsListed).");
}

sb.AppendLine();
sb.AppendLine("This is DEVELOPED SURFACE, not a nesting area: no laps, seams, offcut or wastage. Those");
sb.AppendLine("allowances belong to action-report-duct-weight.cs — and set its fittingsPercent to 0 when");
sb.AppendLine("you use this number, or the fittings are counted twice.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
