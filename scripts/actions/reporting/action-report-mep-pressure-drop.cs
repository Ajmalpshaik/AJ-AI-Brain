// ============================================================
// FRAGMENT (action) — action-report-mep-pressure-drop.cs
// PURPOSE: Revit's OWN calculated pressure loss through each duct or pipe system, section by section —
//          flow, velocity, pressure drop, friction, and WHICH SECTIONS ARE ON THE CRITICAL PATH. The
//          index run that sizes the fan or the pump, read straight out of the model instead of
//          recalculated by hand.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only, opens no transaction.
//
// ✱✱ REVIT ALREADY COMPUTES THIS AND NOTHING HERE WAS READING IT. Once a system is connected and its
//    elements are sized, Revit divides it into SECTIONS and calculates the hydraulics for each one.
//    `MechanicalSystem` / `PipingSystem` expose it:
//        system.SectionsCount                      how many sections
//        system.GetSectionByIndex(i)               -> MEPSection, i from 0 to SectionsCount-1
//        system.GetSectionByNumber(n)              -> MEPSection, by the section's own NUMBER
//        system.GetCriticalPathSectionNumbers()    the index run, as section NUMBERS
//
// ✱✱ INDEX AND NUMBER ARE NOT THE SAME THING, and this fragment had it wrong for two hours after it was
//    written (fixed 2026-08-24). The first version looped `for (n = 1; n <= SectionsCount; n++)` calling
//    GetSectionByNumber(n) — which silently assumes the section NUMBERS are exactly 1..N with no gaps.
//    They are not guaranteed to be: `SectionsCount` bounds the INDEX, and the number is a property of
//    the section. On any system whose numbering is sparse that loop reads some sections twice and
//    misses others, and being a report, it would look plausible either way.
//    **Iterate by INDEX and read `.Number` off each section.** GetSectionByNumber stays for looking one
//    up when you already have its number — which is what the critical-path list gives you.
//    and each MEPSection carries Number, Flow, Velocity, TotalPressureLoss, Friction,
//    VelocityPressure and ReynoldsNumber, plus per-element `GetPressureDrop(id)`,
//    `GetCoefficient(id)` and `GetSegmentLength(id)`. None of it appeared anywhere in this library, so
//    every pressure question here was being answered from first principles when the model already knew.
//
// ✱✱ THE CRITICAL PATH IS THE POINT OF THE REPORT. Total pressure loss along the critical path is what
//    the fan or pump has to overcome — the rest of the system is not the constraint. The sections on it
//    are marked, summed separately, and reported first. Sizing off the whole-system total instead of the
//    critical path over-specifies the plant.
//
// ✱✱ A SYSTEM THAT IS NOT FULLY CONNECTED REPORTS ZERO, NOT AN ERROR, and that is the trap. Revit only
//    computes hydraulics for a complete, sized, connected system. A run with an open end, an unconnected
//    terminal or an unsized segment simply gives 0 or a section count of 0 — which reads as "no pressure
//    loss" when it means "not calculable". Systems with 0 sections are listed separately and named as
//    UNCALCULATED rather than folded into the table as zeros.
//
// ✱✱ PRESSURE VALUES ARE LEFT IN REVIT'S INTERNAL UNITS ON PURPOSE. Length and flow have definitional
//    conversions (1 ft is exactly 304.8 mm), so those are converted. Pressure does not — and this Brain
//    deliberately names no unit API (see ../../../knowledge/revit-version-compatibility.md, "Units —
//    delete the API call, don't guard it"). Quoting a converted Pa figure from a guessed constant would
//    be worse than useless on a submittal. **The ranking is exact and that is what this report is for**:
//    which section is worst, and what the critical path totals RELATIVE to the rest. Read the absolute
//    number off the system's own schedule in Revit.
//
// GOTCHA: sections are numbered from 1, and section numbers are not element ids.
// GOTCHA: a section spans several elements; `GetElementIds()` lists them and the per-element calls take
//         one of those ids. Passing any other id throws.
// GOTCHA: on a big model with many systems this walks every section of every system — use
//         systemNameContains to narrow it.
// RELATED: action-report-length-by-size.cs and the duct-sizing skill work forward from airflow; this
//          works backward from what Revit calculated. knowledge/live-model/hvac-duct-sizing.md holds the
//          hand method.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Run it on ONE known system and compare the
//   critical-path section list against Revit's own System Inspector before trusting it.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemNameContains = "";   // "" = every system; otherwise only systems whose name contains this
bool includeDuct = true;
bool includePipe = true;
int maxSystemsReported = 20;
int maxSectionsPerSystem = 40;
bool listWorstElementsPerSystem = true;  // the individual elements with the biggest loss
int worstElementsCount = 5;
// ---- END INPUTS ----

const double MM = 304.8;
const double LPS_PER_CFS = 28.316846592;   // ft3/s -> litres/second, definitional
const double MPS_PER_FPS = 0.3048;         // ft/s  -> m/s, definitional

var sb = new System.Text.StringBuilder();

// Collect the concrete system classes. MEPSystem (the base) does NOT carry the section members —
// they are declared separately on MechanicalSystem and PipingSystem, so both are handled explicitly.
var systems = new List<Element>();
if (includeDuct)
    systems.AddRange(new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystem))
        .WhereElementIsNotElementType().ToElements());
if (includePipe)
    systems.AddRange(new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipingSystem))
        .WhereElementIsNotElementType().ToElements());

if (!string.IsNullOrEmpty(systemNameContains))
    systems = systems.Where(s => (s.Name ?? "").IndexOf(systemNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

if (systems.Count == 0)
{
    sb.AppendLine("No duct or pipe systems found" + (string.IsNullOrEmpty(systemNameContains) ? "." : $" whose name contains '{systemNameContains}'."));
    sb.AppendLine("A system only exists once elements are connected — see the connectivity skill if you expected some.");
    return sb.ToString();
}

sb.AppendLine($"=== MEP PRESSURE LOSS — {systems.Count} system(s) ===");
sb.AppendLine("Read-only. Pressure values are Revit's INTERNAL units — exact for ranking, not for quoting.");
sb.AppendLine();

var uncalculated = new List<string>();
int reported = 0;

foreach (var sysEl in systems)
{
    if (reported >= maxSystemsReported) break;

    var mech = sysEl as Autodesk.Revit.DB.Mechanical.MechanicalSystem;
    var pipe = sysEl as Autodesk.Revit.DB.Plumbing.PipingSystem;

    int sectionCount = 0;
    try { sectionCount = mech != null ? mech.SectionsCount : (pipe != null ? pipe.SectionsCount : 0); }
    catch { sectionCount = 0; }

    if (sectionCount == 0)
    {
        // Not "no pressure loss" — not calculable. Almost always an open end or an unsized segment.
        uncalculated.Add($"{sysEl.Name} (Id {sysEl.Id}) — {(mech != null ? "duct" : "pipe")}");
        continue;
    }

    reported++;

    // the index run
    var criticalNumbers = new HashSet<int>();
    try
    {
        var crit = mech != null ? mech.GetCriticalPathSectionNumbers() : pipe.GetCriticalPathSectionNumbers();
        if (crit != null) foreach (var n in crit) criticalNumbers.Add(n);
    }
    catch { }

    sb.AppendLine($"### {sysEl.Name} (Id {sysEl.Id}) — {(mech != null ? "duct" : "pipe")} system, {sectionCount} section(s), {criticalNumbers.Count} on the critical path");

    double totalDrop = 0, criticalDrop = 0;
    var rows = new List<string>();
    var elementLosses = new List<Tuple<ElementId, double, int>>(); // element, its drop, section number

    for (int i = 0; i < sectionCount && i < maxSectionsPerSystem; i++)
    {
        // BY INDEX, not by number - SectionsCount bounds the index, and the number is a property.
        Autodesk.Revit.DB.Mechanical.MEPSection sec = null;
        try { sec = mech != null ? mech.GetSectionByIndex(i) : pipe.GetSectionByIndex(i); }
        catch { }
        if (sec == null) continue;

        int n = i;                                  // fallback if the section will not report its own
        try { n = sec.Number; } catch { }

        double flow = 0, vel = 0, drop = 0, friction = 0, velPressure = 0, reynolds = 0;
        // MEPSection's REAL member set, settled by compiling rather than guessing (2026-08-24):
        //   HAS:      Number, Flow, Velocity, TotalPressureLoss, Friction, VelocityPressure,
        //             ReynoldsNumber, GetElementIds(), GetPressureDrop(id), GetCoefficient(id),
        //             GetSegmentLength(id), IsMain(id)
        //   HAS NOT:  PressureDrop, PressureLoss, HydraulicDiameter, Size, Diameter, Length, Area
        // The section total is TotalPressureLoss; `PressureDrop` exists ONLY as the per-element method.
        // Size and length come from the elements themselves, not from the section.
        try { flow = sec.Flow; } catch { }
        try { vel = sec.Velocity; } catch { }
        try { drop = sec.TotalPressureLoss; } catch { }
        try { friction = sec.Friction; } catch { }
        try { velPressure = sec.VelocityPressure; } catch { }
        try { reynolds = sec.ReynoldsNumber; } catch { }

        bool onCritical = criticalNumbers.Contains(n);
        totalDrop += drop;
        if (onCritical) criticalDrop += drop;

        rows.Add($"{n}{(onCritical ? " ★" : "")} | {flow * LPS_PER_CFS:F1} | {vel * MPS_PER_FPS:F2} | {drop:F5} | {friction:F5} | {velPressure:F5} | {reynolds:F0}");

        if (listWorstElementsPerSystem)
        {
            try
            {
                var ids = sec.GetElementIds();
                if (ids != null)
                    foreach (var id in ids)
                    {
                        double d = 0;
                        try { d = sec.GetPressureDrop(id); } catch { continue; }
                        elementLosses.Add(Tuple.Create(id, d, n));
                    }
            }
            catch { }
        }
    }

    sb.AppendLine("Section | Flow (L/s) | Velocity (m/s) | Total pressure loss | Friction | Velocity pressure | Reynolds");
    sb.AppendLine("--- | --- | --- | --- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (sectionCount > maxSectionsPerSystem) sb.AppendLine($"... {sectionCount - maxSectionsPerSystem} more section(s) not shown.");
    sb.AppendLine($"★ = on the critical path. Critical-path drop {criticalDrop:F5} of {totalDrop:F5} total across the sections read ({(totalDrop > 0 ? (criticalDrop / totalDrop * 100).ToString("F0") : "0")}%).");

    if (listWorstElementsPerSystem && elementLosses.Count > 0)
    {
        sb.AppendLine($"Worst {Math.Min(worstElementsCount, elementLosses.Count)} element(s) by pressure drop:");
        foreach (var e in elementLosses.OrderByDescending(x => x.Item2).Take(worstElementsCount))
        {
            var el = Document.GetElement(e.Item1);
            string what = el == null ? "(gone)" : $"{el.Category?.Name ?? el.GetType().Name} '{el.Name}'";
            double segLen = 0; bool isMain = false;
            // by NUMBER here on purpose - e.Item3 is the section's own number, recorded above.
            try { var s2 = mech != null ? mech.GetSectionByNumber(e.Item3) : pipe.GetSectionByNumber(e.Item3); if (s2 != null) { segLen = s2.GetSegmentLength(e.Item1); isMain = s2.IsMain(e.Item1); } } catch { }
            sb.AppendLine($"  Id {e.Item1} | {what} | drop {e.Item2:F5} | section {e.Item3}{(isMain ? " (main run)" : "")}{(segLen > 0 ? $" | {Math.Round(segLen * MM)} mm long" : "")}");
        }
    }
    sb.AppendLine();
}

if (uncalculated.Count > 0)
{
    sb.AppendLine($"⚠ {uncalculated.Count} system(s) reported ZERO SECTIONS — Revit could not calculate them. That is NOT 'no pressure loss':");
    foreach (var u in uncalculated.Take(30)) sb.AppendLine("  " + u);
    if (uncalculated.Count > 30) sb.AppendLine($"  ... {uncalculated.Count - 30} more.");
    sb.AppendLine("  Usual causes: an open end, an unconnected terminal, an unsized segment, or the system not being fully joined.");
    sb.AppendLine("  Check with the verify_connectivity tool and actions/qa-checks/action-check-open-pipe-ends.cs.");
}
if (systems.Count > maxSystemsReported)
    sb.AppendLine($"... {systems.Count - maxSystemsReported} more system(s) not reported (raise maxSystemsReported).");

return sb.ToString();
