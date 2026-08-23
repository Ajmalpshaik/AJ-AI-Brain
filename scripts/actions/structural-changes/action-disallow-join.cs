// ============================================================
// FRAGMENT (action) — action-disallow-join.cs
// PURPOSE: Turn OFF automatic end-joining on the walls and structural framing in `elements` — the fix for
//          walls that clean up into each other where they should read as separate, and for beams whose
//          ends get eaten by the column they run into. Revit's right-click "Disallow Join", in bulk.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs.
//
// ✱✱ THE STEP EVERYONE MISSES: A PINNED ELEMENT SILENTLY REFUSES. The join calls do not throw on a pinned
//    wall — they return normally and change nothing, so the script reports success and the model is
//    untouched. This unpins first (and pins back afterwards if it was pinned, unless you say otherwise),
//    and counts how many were pinned so the number is visible rather than mysterious.
//
// ✱✱ WALLS AND BEAMS USE DIFFERENT CALLS. Walls: WallUtils.DisallowWallJoinAtEnd(wall, 0|1). Structural
//    framing: StructuralFramingUtils.DisallowJoinAtEnd(beam, 0|1). There is no shared method, so anything
//    that is neither is reported as unsupported rather than quietly skipped.
//
// GOTCHA: "end 0" is the start of the element's location curve and "end 1" the finish. Which one is which
//         on the screen depends how the wall was drawn — that is why `whichEnd = "both"` is the default.
// GOTCHA: THIS IS REVERSIBLE — set allow = true to put automatic joining back on (AllowWallJoinAtEnd /
//         AllowJoinAtEnd). Turning it back on lets Revit re-clean the corner, which can visibly move
//         where the wall ends.
// GOTCHA: DRY RUN BY DEFAULT.
// RELATED: action-change-wall-constraints.cs re-hosts walls without moving them.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on one corner and look at the plan.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool allow = false;          // false = DISALLOW joining (the usual job); true = allow it again
string whichEnd = "both";    // "both" | "start" | "end"
bool restorePinned = true;   // pin an element back afterwards if it was pinned before
bool dryRun = true;          // true = report only, change nothing
// ---- END INPUTS ----

int done = 0, wasPinned = 0, unsupported = 0, failed = 0;
var notes = new List<string>();

var ends = new List<int>();
if (whichEnd == "both") { ends.Add(0); ends.Add(1); }
else if (whichEnd == "start") ends.Add(0);
else if (whichEnd == "end") ends.Add(1);

if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this.");
}
else if (ends.Count == 0)
{
    sb.AppendLine($"whichEnd '{whichEnd}' is not one of: both, start, end.");
}
else
{
    sb.AppendLine($"{elements.Count} element(s) in. {(allow ? "ALLOWING" : "DISALLOWING")} join at: {whichEnd}.");
    sb.AppendLine();

    using (var t = new Transaction(Document, "AJ Tools - " + (allow ? "Allow join" : "Disallow join")))
    {
        if (!dryRun) t.Start();

        foreach (var el in elements)
        {
            string label = $"{el.Category?.Name ?? "element"} {el.Id}";
            bool isWall = el is Wall;
            // Compared as ElementId, not as an int: ElementId.IntegerValue was REMOVED in Revit 2027.
            bool isFraming = el.Category != null
                && el.Category.Id == new ElementId(BuiltInCategory.OST_StructuralFraming);

            if (!isWall && !isFraming)
            {
                unsupported++;
                notes.Add($"  {label}: only Walls and Structural Framing can have joining turned off — skipped.");
                continue;
            }

            if (dryRun)
            {
                sb.AppendLine($"  would {(allow ? "allow" : "disallow")} join on {label}"
                            + (el.Pinned ? " (pinned — will be unpinned first)" : ""));
                done++;
                continue;
            }

            bool pinnedBefore = el.Pinned;
            if (pinnedBefore) { wasPinned++; try { el.Pinned = false; } catch { } }

            try
            {
                foreach (int end in ends)
                {
                    if (isWall)
                    {
                        if (allow) WallUtils.AllowWallJoinAtEnd((Wall)el, end);
                        else       WallUtils.DisallowWallJoinAtEnd((Wall)el, end);
                    }
                    else
                    {
                        var fi = el as FamilyInstance;
                        if (fi == null) throw new Exception("structural framing element is not a FamilyInstance");
                        if (allow) StructuralFramingUtils.AllowJoinAtEnd(fi, end);
                        else       StructuralFramingUtils.DisallowJoinAtEnd(fi, end);
                    }
                }
                done++;
                sb.AppendLine($"  {label}: done{(pinnedBefore ? " (was pinned)" : "")}");
            }
            catch (Exception ex)
            {
                failed++;
                notes.Add($"  {label}: FAILED — {ex.Message}");
            }
            finally
            {
                if (pinnedBefore && restorePinned) { try { el.Pinned = true; } catch { } }
            }
        }

        if (!dryRun) t.Commit();
    }

    sb.AppendLine();
    sb.AppendLine(dryRun
        ? $"DRY RUN — nothing changed. {done} element(s) would be updated. Set dryRun = false to apply."
        : $"Updated {done} element(s). Pinned first: {wasPinned}{(restorePinned ? " (pinned back)" : " (left unpinned)")}.");
    if (unsupported > 0) sb.AppendLine($"Not a wall or beam, skipped: {unsupported}.");
    if (failed > 0)      sb.AppendLine($"Failed: {failed}.");
    foreach (var n in notes) sb.AppendLine(n);
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
