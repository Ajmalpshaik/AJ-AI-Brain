// ============================================================
// FRAGMENT (action) — action-report-routing-preferences.cs
// PURPOSE: Read the ROUTING PREFERENCES table off a Pipe Type or Duct Type — which elbow, tee, cross,
//          transition, union and cap Revit will insert, and at which sizes. The answer to "why did it
//          put THAT fitting in", and the check before drawing anything with a type you did not build.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only: opens no Transaction and changes nothing.
//
// WHY THIS EXISTS: routing preferences are what turn "draw a pipe" into "and put the right fittings on
//   it", and they are invisible until something goes wrong. When a run comes out with the wrong elbow,
//   or a size silently gets no fitting at all, this table IS the explanation — and nothing in this
//   library could read it. `recipes/draw-main-duct-with-cap.cs` reaches one rule (`GetRule(Caps, 0)`)
//   to find a cap and goes no further.
//
// ✱✱ THE THING THAT MAKES A TYPE FAIL SILENTLY: A RULE WITH NO SIZE RANGE STILL MATCHES NOTHING.
//    Each rule carries a MinimumSize/MaximumSize window. Draw at a size outside every window in the
//    group and Revit inserts NO fitting — the run just stays unconnected at that junction, with no
//    error and no warning. That gap is what this fragment is really for, so it is reported explicitly
//    rather than left to be inferred from a table of numbers.
//
// ✱✱ AND THE ONE THAT LOOKS LIKE A BUG AND IS NOT: a rule whose MEPPartId points at a family that is
//    NOT LOADED reports as missing. The rule is fine; the family is gone. Same visible symptom as an
//    empty group, completely different fix, so the two are separated below.
//
// ✱✱ `GetMEPPartId` ANSWERS THE REAL QUESTION. `GetRule(group, i)` walks the table; `GetMEPPartId`
//    asks Revit "for THIS size, in THIS group, what would you actually use?" — which is the question a
//    person has. Both are reported: the table for the whole picture, the lookup for a size you name.
//
// RELATED: recipes/draw-main-duct-with-cap.cs (uses one cap rule; this explains the rest of the table),
//          knowledge/live-model/hvac-ducts.md, skills/ajtools-hvac-duct-routing/SKILL.md.
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only, so the worst case is a wrong reading rather than a wrong
//    model — but check one row against Revit's own Routing Preferences dialog before trusting the rest.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string domain = "pipe";            // "pipe" = Pipe Types, "duct" = Duct Types, "both"
string typeNameContains = "";      // "" = every type of that domain; else match on the type name

double checkSizeMm = 0;            // >0 = also ask Revit which fitting it would pick AT THIS SIZE
                                   //      (the GetMEPPartId question). 0 = table only.
bool onlyProblems = false;         // true = list only types with an empty group or a missing family
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// The groups a routing preference table is divided into. Named here rather than enumerated so the
// report reads in the order the Revit dialog shows them.
var groups = new[]
{
    RoutingPreferenceRuleGroupType.Elbows,
    RoutingPreferenceRuleGroupType.Junctions,
    RoutingPreferenceRuleGroupType.Crosses,
    RoutingPreferenceRuleGroupType.Transitions,
    RoutingPreferenceRuleGroupType.Unions,
    RoutingPreferenceRuleGroupType.MechanicalJoints,
    RoutingPreferenceRuleGroupType.Caps,
    RoutingPreferenceRuleGroupType.Segments
};

var types = new List<ElementType>();
if (domain == "pipe" || domain == "both")
{
    types.AddRange(new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeType)).Cast<ElementType>());
}
if (domain == "duct" || domain == "both")
{
    types.AddRange(new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType)).Cast<ElementType>());
}

if (!string.IsNullOrEmpty(typeNameContains))
    types = types.Where(t => (t.Name ?? "").IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

if (types.Count == 0)
{
    sb.AppendLine($"No {domain} types found"
        + (string.IsNullOrEmpty(typeNameContains) ? "" : $" with a name containing \"{typeNameContains}\"") + ".");
    return sb.ToString();
}

double checkSizeFt = checkSizeMm / 304.8;
int typesRead = 0, emptyGroups = 0, missingFamilies = 0;

sb.AppendLine($"ROUTING PREFERENCES — {types.Count} type(s).");
sb.AppendLine("Each group lists: size window -> the fitting family/type Revit will insert.");
sb.AppendLine();

foreach (var et in types.OrderBy(t => t.Name))
{
    RoutingPreferenceManager rpm = null;
    try
    {
        var mct = et as MEPCurveType;
        if (mct != null) rpm = mct.RoutingPreferenceManager;
    }
    catch { }

    if (rpm == null)
    {
        sb.AppendLine($"=== {et.Name}   (no routing preference manager — nothing to read)");
        sb.AppendLine();
        continue;
    }

    typesRead++;
    var lines = new List<string>();
    var faults = new List<string>();

    foreach (var grp in groups)
    {
        int n = 0;
        try { n = rpm.GetNumberOfRules(grp); } catch { continue; }

        if (n == 0)
        {
            // Segments legitimately can be empty on some types; a missing Elbows group cannot.
            if (grp == RoutingPreferenceRuleGroupType.Elbows || grp == RoutingPreferenceRuleGroupType.Junctions)
            {
                faults.Add($"! {grp} is EMPTY — Revit has nothing to insert there, so a run using this type will not fit up at that junction.");
                emptyGroups++;
            }
            continue;
        }

        lines.Add($"  {grp} ({n} rule{(n == 1 ? "" : "s")})");
        for (int i = 0; i < n; i++)
        {
            try
            {
                var rule = rpm.GetRule(grp, i);
                if (rule == null) continue;

                string partName = "(no family set)";
                var pid = rule.MEPPartId;
                if (pid != null && pid != ElementId.InvalidElementId)
                {
                    var part = Document.GetElement(pid);
                    if (part == null)
                    {
                        partName = "MISSING — the rule points at a family that is not loaded";
                        faults.Add($"! {grp} rule {i + 1}: family not loaded (the rule is fine; the family is gone).");
                        missingFamilies++;
                    }
                    else
                    {
                        var ps = part as FamilySymbol;
                        partName = ps != null && ps.Family != null
                            ? ps.Family.Name + " : " + ps.Name
                            : (part.Name ?? "(unnamed)");
                    }
                }

                // ✱✱ THE SIZE WINDOW IS NOT ON THE RULE. A rule holds a LIST OF CRITERIA
                //    (`NumberOfCriteria` / `GetCriterion(i)`), and the size range is one of them — a
                //    `PrimarySizeCriterion` carrying MinimumSize/MaximumSize. Reaching for
                //    rule.MinimumSize looks obvious and does not compile. A rule with no size
                //    criterion applies to ALL sizes, which is a real and common configuration.
                string window = "all sizes";
                try
                {
                    int nc = rule.NumberOfCriteria;
                    for (int c = 0; c < nc; c++)
                    {
                        var crit = rule.GetCriterion(c) as PrimarySizeCriterion;
                        if (crit == null) continue;
                        double mn = crit.MinimumSize, mx = crit.MaximumSize;
                        bool hasMin = mn > 0, hasMax = mx > 0 && mx < 1e6;
                        if (hasMin || hasMax)
                            window = (hasMin ? Math.Round(mn * 304.8, 1) + "mm" : "any") + " to " +
                                     (hasMax ? Math.Round(mx * 304.8, 1) + "mm" : "any");
                        break;
                    }
                }
                catch { window = "(size window unreadable)"; }

                lines.Add($"      {window,-26} {partName}");
            }
            catch (Exception exRule)
            {
                lines.Add($"      rule {i + 1}: unreadable — {exRule.Message}");
            }
        }
    }

    // The direct question, if a size was named.
    var lookup = new List<string>();
    if (checkSizeMm > 0)
    {
        foreach (var grp in new[] { RoutingPreferenceRuleGroupType.Elbows,
                                    RoutingPreferenceRuleGroupType.Junctions,
                                    RoutingPreferenceRuleGroupType.Transitions })
        {
            try
            {
                var cond = new RoutingConditions(RoutingPreferenceErrorLevel.None);
                cond.AppendCondition(new RoutingCondition(checkSizeFt));
                var partId = rpm.GetMEPPartId(grp, cond);
                if (partId == null || partId == ElementId.InvalidElementId)
                {
                    lookup.Add($"      {grp,-16} NOTHING — no rule covers {checkSizeMm}mm, so Revit would insert no fitting here.");
                }
                else
                {
                    var part = Document.GetElement(partId) as FamilySymbol;
                    lookup.Add($"      {grp,-16} {(part != null && part.Family != null ? part.Family.Name + " : " + part.Name : partId.ToString())}");
                }
            }
            catch (Exception exLk)
            {
                lookup.Add($"      {grp,-16} lookup failed — {exLk.Message}");
            }
        }
    }

    bool hasFault = faults.Count > 0;
    if (onlyProblems && !hasFault) continue;

    sb.AppendLine($"=== {et.Name}");
    foreach (var f in faults) sb.AppendLine("  " + f);
    if (!onlyProblems) foreach (var l in lines) sb.AppendLine(l);
    if (lookup.Count > 0)
    {
        sb.AppendLine($"  AT {checkSizeMm}mm Revit would use:");
        foreach (var l in lookup) sb.AppendLine(l);
    }
    sb.AppendLine();
}

sb.AppendLine($"Read {typesRead} type(s) with a routing table.");
if (emptyGroups > 0)
    sb.AppendLine($"! {emptyGroups} empty Elbows/Junctions group(s) — those types cannot fit up a corner or a tee at all.");
if (missingFamilies > 0)
    sb.AppendLine($"! {missingFamilies} rule(s) point at a family that is not loaded — load the family, do not rebuild the rule.");
if (emptyGroups == 0 && missingFamilies == 0)
    sb.AppendLine("No empty groups and no missing families.");
if (checkSizeMm <= 0)
    sb.AppendLine("Set checkSizeMm to a real size to ask Revit which fitting it would actually pick there — that is the question a size gap answers.");

return sb.ToString();
