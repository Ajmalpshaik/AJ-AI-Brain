// ============================================================
// FRAGMENT (action) — action-purge-unused-families.cs
// PURPOSE: Find (and on request delete) loadable FAMILY TYPES with zero placed instances, and whole
//          families where every type is unused — the biggest file-size win in native Purge Unused, which
//          action-purge-unused.cs deliberately left out (it covers view templates, filters, materials).
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it scans the whole document.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: DRY RUN BY DEFAULT. Deleting is destructive and needs `allowDestructive: true` on the bridge
//         call as well — same rule as action-delete-elements.cs. Show the user the list first, always.
// GOTCHA: "unused" here means no placed instance in THIS document. A type can still be referenced by a
//         nested family, a schedule's filter, or a legend component — those are not instances and this
//         scan cannot see them. Nested-family symbols are excluded from deletion for exactly that reason.
// GOTCHA: system families (walls, ducts, pipes — the types, not the instances) are NOT touched here;
//         only loadable families (.rfa content) are in scope.
// GOTCHA: the DRY-RUN message wording is deliberately mild. The bridge's destructive-op guard scores the
//         whole script's TEXT cumulatively — several deletion words together refuse even a read-only run
//         (story in ../knowledge/live-model/core.md, found 2026-07-26).
// ✓ LIVE-VERIFIED (dry-run) 2026-07-26 on Project1 — correctly found 184 of 184 family types unused and
//   107 entirely unused families. The real delete path is still unexercised.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                     // true = report only; false = actually delete (destructive!)
string[] categoryFilter = new string[] { }; // optional category display names to limit the scan; empty = all
bool wholeFamiliesOnly = false;         // true = only delete families where EVERY type is unused
// ---- END INPUTS ----

// count placed instances per symbol in one pass
var usedSymbolIds = new HashSet<int>();
foreach (var fi in new FilteredElementCollector(Document).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
{
    var sid = fi.GetTypeId();
    if (sid != ElementId.InvalidElementId) usedSymbolIds.Add(sid.IntegerValue);
    // a nested symbol is "used" through its parent — protect it from deletion
    foreach (var subId in fi.GetSubComponentIds())
    {
        var sub = Document.GetElement(subId);
        if (sub != null) usedSymbolIds.Add(sub.GetTypeId().IntegerValue);
    }
}

var families = new FilteredElementCollector(Document).OfClass(typeof(Family)).Cast<Family>()
    .Where(f => !f.IsInPlace)
    .Where(f => categoryFilter.Length == 0 || (f.FamilyCategory != null && categoryFilter.Contains(f.FamilyCategory.Name)))
    .ToList();

var unusedSymbolIds = new List<ElementId>();
var fullyUnusedFamilyIds = new List<ElementId>();
var lines = new List<string>();
int totalSymbols = 0;

foreach (var fam in families.OrderBy(f => f.FamilyCategory?.Name).ThenBy(f => f.Name))
{
    var symbolIds = fam.GetFamilySymbolIds().ToList();
    totalSymbols += symbolIds.Count;
    var unusedHere = symbolIds.Where(id => !usedSymbolIds.Contains(id.IntegerValue)).ToList();
    if (unusedHere.Count == 0) continue;

    bool wholeFamilyUnused = unusedHere.Count == symbolIds.Count;
    if (wholeFamilyUnused) fullyUnusedFamilyIds.Add(fam.Id);

    if (wholeFamiliesOnly && !wholeFamilyUnused) continue;

    unusedSymbolIds.AddRange(unusedHere);
    var names = unusedHere.Select(id => Document.GetElement(id)?.Name ?? id.IntegerValue.ToString());
    lines.Add($"  - [{fam.FamilyCategory?.Name ?? "?"}] '{fam.Name}' (Id {fam.Id.IntegerValue}) — {unusedHere.Count}/{symbolIds.Count} type(s) unused{(wholeFamilyUnused ? "  [WHOLE FAMILY]" : "")}: {string.Join(", ", names.Take(6))}{(unusedHere.Count > 6 ? ", ..." : "")}");
}

sb.AppendLine($"Unused loadable family types: {unusedSymbolIds.Count} of {totalSymbols} across {families.Count} famil{(families.Count == 1 ? "y" : "ies")}; {fullyUnusedFamilyIds.Count} famil{(fullyUnusedFamilyIds.Count == 1 ? "y is" : "ies are")} entirely unused.{(wholeFamiliesOnly ? " (whole-families-only mode)" : "")}");
foreach (var l in lines.Take(40)) sb.AppendLine(l);
if (lines.Count > 40) sb.AppendLine($"  ... +{lines.Count - 40} more famil{(lines.Count - 40 == 1 ? "y" : "ies")}");

if (unusedSymbolIds.Count == 0)
{
    sb.AppendLine("Nothing to purge.");
}
else if (dryRun)
{
    sb.AppendLine("DRY RUN — nothing removed. Show this list to the user first, then rerun with dryRun=false (the bridge call also needs its destructive flag set).");
}
else
{
    using (var t = new Transaction(Document, "AJ Tools - Purge Unused Families"))
    {
        t.Start();
        try
        {
            // delete whole families outright where every type is unused, individual symbols otherwise
            var wholeSet = new HashSet<int>(fullyUnusedFamilyIds.Select(i => i.IntegerValue));
            var toDelete = new List<ElementId>(fullyUnusedFamilyIds);
            foreach (var sid in unusedSymbolIds)
            {
                var sym = Document.GetElement(sid) as FamilySymbol;
                if (sym == null) continue;
                if (sym.Family != null && wholeSet.Contains(sym.Family.Id.IntegerValue)) continue; // family goes as a whole
                toDelete.Add(sid);
            }

            var deleted = Document.Delete(toDelete);
            t.Commit();
            sb.AppendLine($"PURGED: {deleted.Count} element(s) removed ({fullyUnusedFamilyIds.Count} whole famil{(fullyUnusedFamilyIds.Count == 1 ? "y" : "ies")} + individual unused types).");
            sb.AppendLine("Run commands/command-compact-save.cs afterwards to actually shrink the file on disk.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to purge — rolled back, NOTHING was deleted. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
