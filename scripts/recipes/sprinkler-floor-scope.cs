// ============================================================
// RECIPE — sprinkler-floor-scope.cs
// PURPOSE: The FIRST pass over a whole architectural plan. Walks every placed Room on a level (or the
//          whole model), sorts each into needs-heads / special-treatment / ASK, applies the area rule to
//          get an indicative head count per room, and totals it. Answers "do the whole plan" instead of
//          one room at a time. READ-ONLY — it decides nothing and places nothing.
// STANDALONE — has its own sb and returns.
// SOURCE: ../../knowledge/fire-sprinkler/where-sprinklers-are-required.md  (the three buckets, and why)
// SOURCE: ../../knowledge/fire-sprinkler/layout-method.md                  (what happens to each room after)
//
// *** IT NEVER OMITS A ROOM. *** Every space it cannot confidently call goes in the ASK bucket with the
//   reason. A room silently dropped from a fire layout is the worst failure this Brain could produce, so
//   the classifier is built to OVER-report: it flags candidates for omission and hands them to a person.
//   "Likely exempt" in this output means "go and check this one", never "leave it out".
//
// HOW IT CLASSIFIES, and the honest limits of it:
//   By ROOM NAME and AREA only. It cannot see construction materials, combustible loading, wall ratings
//   or what the space is really used for — and every real omission rule turns on exactly those. So the
//   name match is a PROMPT to look, not a finding. A room called "STORE" that is 2 m2 might qualify as a
//   small closet; the fragment says "check the small-closet rule on this one", not "omitted".
//   Room naming is also per-project. Feed the name lists that match THIS project's naming; the defaults
//   below are ordinary English and will miss a project that names rooms in codes or in Arabic.
//
// GOTCHA: **an unplaced Room (Area 0) encloses nothing** and is skipped with a count, because a plan full
//         of unplaced rooms would otherwise report a clean sweep over nothing at all.
// GOTCHA: **the head counts here are the AREA-RULE FLOOR, not a layout.** Room area / max area per head,
//         rounded up. The real count comes from recipes/sprinkler-nfpa-grid.cs, is never lower, and is
//         often higher once the spacing and wall limits bind. Use this total for an order-of-magnitude
//         and a fee, never for a material take-off.
// GOTCHA: **one hazard class across a whole floor is usually wrong.** A plant room, a store and an office
//         on the same level are different classes. The fragment applies the ONE class you give it and
//         says so on every line, so the assumption is visible rather than buried.
// GOTCHA: rooms with no ceiling, sloped ceilings or a deep void each change the answer for that room.
//         This pass does not look up — recipes/sprinkler-obstruction-survey.cs does, per room.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. The collector and Room reads are
//   the same shapes proven live elsewhere in this library. Run it on one known level and check the room
//   list against the plan by eye — in particular that nothing you can see is missing from the output.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int levelIdInt = 0;                   // 0 = every placed Room in the model; otherwise just this level
string standardLabel = "";            // REQUIRED — "NFPA 13 (2022)" / "BS EN 12845"
string hazardLabel = "";              // REQUIRED — and state on the reply that ONE class was applied to all
double maxAreaPerHeadM2 = 0;          // REQUIRED — the area limit for that class

// --- name fragments that mark a room for a closer look. Lowercase, matched as substrings.
//     Tune these to THIS project's room naming before trusting the sort.
string[] askNames = new[] { "closet", "cupboard", "store", "storage", "wc", "toilet", "bath", "shower",
                            "lift", "elevator", "hoist", "shaft", "duct", "riser", "electrical", "switch",
                            "substation", "transformer", "panel", "server", "comms", "it room", "battery" };
string[] specialNames = new[] { "stair", "escape", "lobby", "atrium", "void", "canopy", "balcony",
                                "terrace", "car park", "carpark", "parking", "plant", "kitchen" };
double smallRoomAreaM2 = 2.5;         // at or under this, flag for the small-closet rule regardless of name
int maxListedPerBucket = 60;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toM2 = v => UnitUtils.ConvertFromInternalUnits(v, DisplayUnitType.DUT_SQUARE_METERS);

var missing = new List<string>();
if (maxAreaPerHeadM2 <= 0) missing.Add("maxAreaPerHeadM2");
if (string.IsNullOrWhiteSpace(standardLabel)) missing.Add("standardLabel");
if (string.IsNullOrWhiteSpace(hazardLabel)) missing.Add("hazardLabel");

if (missing.Count > 0)
{
    sb.AppendLine("NOT RUN — required inputs missing:");
    foreach (var m in missing) sb.AppendLine("  - " + m);
    sb.AppendLine("A floor total with no standard and no hazard class named is a number nobody can use.");
}
else
{
    Level level = levelIdInt == 0 ? null : Document.GetElement(new ElementId(levelIdInt)) as Level;
    if (levelIdInt != 0 && level == null) { sb.AppendLine($"levelIdInt {levelIdInt} is not a Level."); }
    else
    {
        var allRooms = new FilteredElementCollector(Document)
            .OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
            .Cast<Autodesk.Revit.DB.Architecture.Room>().ToList();

        var rooms = allRooms.Where(r =>
        {
            if (level == null) return true;
            try { return r.Level != null && r.Level.Id == level.Id; } catch { return false; }
        }).ToList();

        int unplaced = rooms.Count(r => r.Area <= 0);
        var placed = rooms.Where(r => r.Area > 0).ToList();

        sb.AppendLine($"SPRINKLER FLOOR SCOPE — {(level == null ? "whole model" : "level '" + level.Name + "'")}");
        sb.AppendLine($"STANDARD: {standardLabel}  |  HAZARD APPLIED TO EVERY ROOM: {hazardLabel} ({maxAreaPerHeadM2:F2} m2 per head)");
        sb.AppendLine("*** ONE hazard class has been applied to every room below. On a mixed floor that is usually wrong —");
        sb.AppendLine("    a plant room, a store and an office are not the same class. Treat the total as indicative.");
        sb.AppendLine($"ROOMS: {rooms.Count:N0} found, {placed.Count:N0} placed"
            + (unplaced > 0 ? $", {unplaced:N0} UNPLACED and skipped (Area 0 — they enclose nothing)" : ""));
        if (placed.Count == 0)
        {
            sb.AppendLine("  Nothing to scope. If the plan clearly has rooms, they are not placed as Revit Rooms —");
            sb.AppendLine("  creators/create-rooms-in-enclosed-regions.cs places them first.");
        }
        else
        {
            var needs = new List<string>();
            var special = new List<string>();
            var ask = new List<string>();
            int totalHeads = 0;
            double totalArea = 0;

            foreach (var r in placed.OrderBy(r => { try { return r.Number; } catch { return ""; } }))
            {
                string nm = "";
                try { nm = r.Name ?? ""; } catch { }
                string num = "";
                try { num = r.Number ?? ""; } catch { }
                double aM2 = toM2(r.Area);
                totalArea += aM2;
                int heads = (int)Math.Ceiling(aM2 / maxAreaPerHeadM2);
                string lower = nm.ToLowerInvariant();
                string tag = $"{num} '{nm}' (Id {r.Id.IntegerValue}) {aM2:F1} m2";

                string askHit = askNames.FirstOrDefault(k => lower.Contains(k));
                string specialHit = specialNames.FirstOrDefault(k => lower.Contains(k));

                if (aM2 <= smallRoomAreaM2)
                    ask.Add($"{tag} — only {aM2:F1} m2. CHECK the small-closet rule (needs smallest dimension <= 914 mm,"
                        + " area <= 2.2 m2, AND noncombustible surfaces — all three).");
                else if (askHit != null)
                    ask.Add($"{tag} — name contains '{askHit}'. CHECK whether an omission rule applies. It probably does NOT:"
                        + " a room is not exempt merely for being damp, fire-rated, or full of electrical equipment.");
                else if (specialHit != null)
                {
                    special.Add($"{tag} — name contains '{specialHit}'. Special treatment, NOT omission:"
                        + " stairs get heads at the top and under the first landing; car parks and plant rooms are exposed"
                        + " structure (upright, obstructed construction); kitchens change the temperature rating.");
                    totalHeads += heads;
                }
                else { needs.Add($"{tag} — at least {heads:N0} head(s) by the area rule"); totalHeads += heads; }
            }

            sb.AppendLine();
            sb.AppendLine($"1. NEEDS HEADS — the default, {needs.Count:N0} room(s)");
            foreach (var l in needs.Take(maxListedPerBucket)) sb.AppendLine("   " + l);
            if (needs.Count > maxListedPerBucket) sb.AppendLine($"   ... and {needs.Count - maxListedPerBucket:N0} more.");

            sb.AppendLine();
            sb.AppendLine($"2. SPECIAL TREATMENT — {special.Count:N0} room(s). These still get heads; the RULES differ.");
            foreach (var l in special.Take(maxListedPerBucket)) sb.AppendLine("   " + l);
            if (special.Count > maxListedPerBucket) sb.AppendLine($"   ... and {special.Count - maxListedPerBucket:N0} more.");

            sb.AppendLine();
            sb.AppendLine($"3. ASK — {ask.Count:N0} room(s) matched something worth checking. NONE has been omitted.");
            foreach (var l in ask.Take(maxListedPerBucket)) sb.AppendLine("   " + l);
            if (ask.Count > maxListedPerBucket) sb.AppendLine($"   ... and {ask.Count - maxListedPerBucket:N0} more.");
            if (ask.Count > 0)
            {
                sb.AppendLine("   Every one of these is counted as NEEDING heads until a competent person rules otherwise.");
                sb.AppendLine("   The omission rules all turn on construction and combustible loading, which the model does not hold.");
            }

            sb.AppendLine();
            sb.AppendLine("TOTALS (indicative — area rule only, before spacing and wall limits bind)");
            sb.AppendLine($"  rooms scoped        {placed.Count,6:N0}");
            sb.AppendLine($"  floor area          {totalArea,6:F0} m2");
            sb.AppendLine($"  minimum heads       {totalHeads,6:N0}   at {maxAreaPerHeadM2:F2} m2 per head");
            sb.AppendLine($"  ask-bucket rooms    {ask.Count,6:N0}   (their heads are INCLUDED above only where they also");
            sb.AppendLine("                              fell into needs/special — see each line)");
            sb.AppendLine("  The real number is HIGHER: the area rule is a floor, and spacing, wall distance and obstructions");
            sb.AppendLine("  all push it up. Never quote this as a take-off.");

            sb.AppendLine();
            sb.AppendLine("NEXT, per room, in this order:");
            sb.AppendLine("  1. recipes/sprinkler-obstruction-survey.cs   — ceiling or not, void depth, beams, columns, bay module");
            sb.AppendLine("  2. recipes/sprinkler-layout-options.cs       — several compliant layouts to choose from");
            sb.AppendLine("  3. recipes/sprinkler-obstruction-check.cs    — the chosen option against the real structure");
            sb.AppendLine("  4. recipes/sprinkler-deflector-height.cs     — the Z, read from what is actually above");
            sb.AppendLine("  5. recipes/sprinkler-place-heads.cs          — after the count is agreed");
            sb.AppendLine("  6. recipes/sprinkler-compliance-audit.cs     — from a separate call, on what really got placed");
        }
    }
}

return sb.ToString();
