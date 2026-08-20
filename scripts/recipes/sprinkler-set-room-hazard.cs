// ============================================================
// RECIPE — sprinkler-set-room-hazard.cs
// PURPOSE: Record the decided hazard class ON EACH ROOM, in the model, and read it back. Turns the single
//          most important sprinkler input from something re-asked every session into a project fact that
//          travels with the file — and lets a MIXED floor be a mixed floor instead of being flattened to
//          one class. Also reports which rooms still have no class, which is the list that matters.
// STANDALONE — has its own sb and returns. Writes only when told to explicitly.
// SOURCE: ../../knowledge/fire-sprinkler/hazard-classification.md  (the classes, and how the call is made)
// SOURCE: ../../knowledge/fire-sprinkler/where-sprinklers-are-required.md (the sweep this pairs with)
// VERSION-PROOF (2026-08-20, brought in line with the library-wide migration): unit conversion is
//         plain arithmetic (1 ft = exactly 304.8 mm) and every id collection is keyed by ElementId,
//         so this runs on Revit 2020 through 2027 from one source. The id INPUT is still declared
//         int, matching the rest of the library — see knowledge/revit-version-compatibility.md.
//
// WHY THIS EXISTS: every other fragment in the chain takes ONE hazardLabel and applies it to everything it
//   touches. On a real floor — office, plant room, store, car park ramp — that is three or four classes,
//   and flattening them is a silent error: the area-per-head figure is IDENTICAL between Ordinary Hazard
//   Group 1 and Group 2, so a spacing check cannot catch it. Only the label can. Recording the class per
//   room is what makes the rest of the chain honest on anything bigger than a single room.
//
// *** IT DOES NOT DECIDE THE CLASS. *** That is a licensed judgement about contents, process and fire
//   growth, and no model geometry substitutes for it. This fragment RECORDS a decision somebody else made
//   and shows you where no decision exists yet. The name-matching below is a SUGGESTION engine for the
//   report only — it never writes a class it guessed, and every suggestion is labelled as one.
//
// GOTCHA: **the parameter has to exist first.** A project parameter cannot be created from a script here
//         (that needs a shared-parameter file bound through the UI), so this fragment writes into a
//         parameter you already have and reports plainly when it is missing rather than failing halfway.
//         Ask Ajmal to add it once: Manage > Project Parameters > Add, Text, applied to Rooms.
// GOTCHA: **a class written into a model outlives the session and the person.** That is the point, and it
//         is also the risk — a wrong class recorded looks exactly as authoritative as a right one. Write
//         only what the fire engineer or the specification actually says, and put the SOURCE of the
//         decision in the second parameter (sourceParameterName) so a future reader can trace it.
// GOTCHA: **Ordinary Hazard Group 1 and Group 2 have the same max area per head** (130 ft2 / 12.1 m2), so
//         changing between them changes the hydraulic demand by a third and the layout not at all. Never
//         infer that a layout is fine because "the spacing did not change".
// GOTCHA: an UNPLACED room (Area 0) is skipped and counted — it encloses nothing.
//
// STATUS: NOT LIVE-VERIFIED — written 2026-08-20 with no Revit session. The Room collector, LookupParameter
//   and the write-then-read-back pattern are all shapes live-proven elsewhere in this library (see
//   recipes/fill-mm-document-register.cs, live 2026-08-19 across 160 elements). Run it in report mode
//   first — that mode changes nothing and tells you whether the parameter is even bound.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "report";               // "report"  = read what is there now, suggest nothing binding (SAFE, start here)
                                      // "write"   = write hazardClassToWrite onto the rooms selected below
int levelIdInt = 0;                   // 0 = every placed Room in the model; otherwise just this level

// --- where the decision is stored. These must already exist as project parameters on Rooms.
string hazardParameterName = "FF_Hazard_Class";     // e.g. "Light Hazard" / "Ordinary Hazard Group 2"
string sourceParameterName = "FF_Hazard_Source";    // WHO decided it — "FPE letter 2026-08-20", "Spec 21 13 13 cl.2.4"
string standardParameterName = "FF_Standard";       // "NFPA 13 (2022)" / "BS EN 12845" — the class name means
                                                    // nothing without it; the two systems do not translate

// --- what to write, in "write" mode
string hazardClassToWrite = "";       // the decided class, verbatim as the engineer/spec states it
string hazardSourceToWrite = "";      // REQUIRED in write mode — where the decision came from
string standardToWrite = "";          // REQUIRED in write mode
int[] roomIdsToWrite = new int[] { }; // the rooms to write. EMPTY = every placed room in scope — say so out loud first.
bool overwriteExisting = false;       // false = only fill blanks, and report any different existing value

// --- report-mode help only. Never written, ever. Purely to speed up the human decision.
bool suggestFromRoomNames = true;
int maxListed = 80;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> toM2 = v => v / 10.763910416709722;   // version-proof: no unit API to deprecate

Level level = levelIdInt == 0 ? null : Document.GetElement(new ElementId(levelIdInt)) as Level;

if (levelIdInt != 0 && level == null) { sb.AppendLine($"levelIdInt {levelIdInt} is not a Level."); }
else if (mode == "write" && (string.IsNullOrWhiteSpace(hazardClassToWrite)
        || string.IsNullOrWhiteSpace(hazardSourceToWrite) || string.IsNullOrWhiteSpace(standardToWrite)))
{
    sb.AppendLine("NOT RUN — write mode needs all three of hazardClassToWrite, hazardSourceToWrite and standardToWrite.");
    sb.AppendLine("  A class with no source is a number nobody can defend, and a class with no standard named is");
    sb.AppendLine("  ambiguous — 'Ordinary Hazard' means different things under NFPA 13 and BS EN 12845.");
    sb.AppendLine("  See knowledge/fire-sprinkler/hazard-classification.md.");
}
else
{
    var rooms = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
        .Cast<Autodesk.Revit.DB.Architecture.Room>()
        .Where(r => { if (level == null) return true; try { return r.Level != null && r.Level.Id == level.Id; } catch { return false; } })
        .ToList();

    int unplaced = rooms.Count(r => r.Area <= 0);
    var placed = rooms.Where(r => r.Area > 0).ToList();

    sb.AppendLine($"ROOM HAZARD CLASS — {(level == null ? "whole model" : "level '" + level.Name + "'")}, mode \"{mode}\"");
    sb.AppendLine($"  parameters: '{hazardParameterName}' | '{sourceParameterName}' | '{standardParameterName}'");
    sb.AppendLine($"  {placed.Count:N0} placed room(s)" + (unplaced > 0 ? $", {unplaced:N0} unplaced and skipped" : ""));

    if (placed.Count == 0) { sb.AppendLine("  Nothing to do."); }
    else
    {
        // is the parameter even bound? check on the first room rather than failing per-room
        var probe = placed[0];
        bool hasHazard = probe.LookupParameter(hazardParameterName) != null;
        bool hasSource = probe.LookupParameter(sourceParameterName) != null;
        bool hasStandard = probe.LookupParameter(standardParameterName) != null;

        if (!hasHazard || !hasSource || !hasStandard)
        {
            sb.AppendLine();
            sb.AppendLine("  *** PARAMETER(S) NOT BOUND TO ROOMS:");
            if (!hasHazard) sb.AppendLine($"      '{hazardParameterName}' — missing");
            if (!hasSource) sb.AppendLine($"      '{sourceParameterName}' — missing");
            if (!hasStandard) sb.AppendLine($"      '{standardParameterName}' — missing");
            sb.AppendLine("      A script cannot create a project parameter here — that needs a shared-parameter file");
            sb.AppendLine("      bound through the UI. Ajmal adds them once: Manage > Project Parameters > Add,");
            sb.AppendLine("      type Text, category Rooms. Then this works for the life of the project.");
            sb.AppendLine("      Reporting continues below for whatever IS bound.");
        }

        // suggestion engine — report only, never written
        Func<string, double, string> suggest = (nm, areaM2) =>
        {
            if (!suggestFromRoomNames) return "";
            string l = (nm ?? "").ToLowerInvariant();
            if (l.Contains("car park") || l.Contains("carpark") || l.Contains("parking") || l.Contains("garage"))
                return "car park — recent NFPA editions moved these from OH1 to ORDINARY HAZARD GROUP 2. An old template still says OH1.";
            if (l.Contains("plant") || l.Contains("mechanical") || l.Contains("chiller") || l.Contains("pump"))
                return "plant room — usually ordinary hazard, and the temperature rating usually changes too.";
            if (l.Contains("kitchen") || l.Contains("canteen") || l.Contains("pantry"))
                return "kitchen — ordinary hazard territory, and check the temperature rating near cooking equipment.";
            if (l.Contains("store") || l.Contains("storage") || l.Contains("warehouse"))
                return "STORE — ask the 8 ft (2.4 m) stockpile question. Above that it is OH2 or it leaves occupancy classification for the STORAGE chapters entirely.";
            if (l.Contains("workshop") || l.Contains("repair") || l.Contains("joinery") || l.Contains("carpentry"))
                return "workshop — commonly ordinary hazard group 2.";
            if (l.Contains("office") || l.Contains("meeting") || l.Contains("class") || l.Contains("ward")
                || l.Contains("bedroom") || l.Contains("lobby") || l.Contains("corridor"))
                return "commonly light hazard — confirm the contents, not the name.";
            return "";
        };

        var missingClass = new List<string>();
        var conflicts = new List<string>();
        var byClass = new Dictionary<string, int>();
        int listed = 0;

        foreach (var r in placed.OrderBy(r => { try { return r.Number; } catch { return ""; } }))
        {
            string nm = ""; try { nm = r.Name ?? ""; } catch { }
            string num = ""; try { num = r.Number ?? ""; } catch { }
            double aM2 = toM2(r.Area);

            var hp = r.LookupParameter(hazardParameterName);
            string current = (hp != null && hp.HasValue) ? (hp.AsString() ?? "") : "";
            var sp = r.LookupParameter(standardParameterName);
            string curStd = (sp != null && sp.HasValue) ? (sp.AsString() ?? "") : "";

            string keyed = string.IsNullOrWhiteSpace(current) ? "(none)" : current;
            byClass[keyed] = byClass.ContainsKey(keyed) ? byClass[keyed] + 1 : 1;

            if (string.IsNullOrWhiteSpace(current))
            {
                string hint = suggest(nm, aM2);
                missingClass.Add($"{num} '{nm}' (Id {r.Id}) {aM2:F1} m2"
                    + (string.IsNullOrEmpty(hint) ? "" : $"  [suggestion only, NOT written: {hint}]"));
            }
            else if (!string.IsNullOrWhiteSpace(current) && string.IsNullOrWhiteSpace(curStd))
                conflicts.Add($"{num} '{nm}' (Id {r.Id}) has class '{current}' but NO standard recorded —"
                    + " 'Ordinary Hazard' is ambiguous between NFPA 13 and BS EN 12845.");

            if (listed++ < maxListed && !string.IsNullOrWhiteSpace(current))
                sb.AppendLine($"   {num} '{nm}' {aM2,7:F1} m2 | {current}" + (string.IsNullOrWhiteSpace(curStd) ? "" : $" | {curStd}"));
        }

        sb.AppendLine();
        sb.AppendLine("CLASSES RECORDED");
        foreach (var kv in byClass.OrderByDescending(k => k.Value))
            sb.AppendLine($"  {kv.Value,5:N0}  {kv.Key}");
        if (byClass.Count(k => k.Key != "(none)") > 1)
            sb.AppendLine("  This floor is MIXED. Good — that is usually the truth. Every fragment that takes one hazardLabel");

        if (byClass.Count(k => k.Key != "(none)") > 1)
            sb.AppendLine("  must be run per class, not once for the floor.");

        if (missingClass.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"NO CLASS RECORDED — {missingClass.Count:N0} room(s). This is the list that matters:");
            foreach (var m in missingClass.Take(maxListed)) sb.AppendLine("   " + m);
            if (missingClass.Count > maxListed) sb.AppendLine($"   ... and {missingClass.Count - maxListed:N0} more.");
            sb.AppendLine("   Suggestions above are to speed up a HUMAN decision. Nothing has been written from them.");
        }
        if (conflicts.Count > 0)
        {
            sb.AppendLine();
            foreach (var c in conflicts.Take(maxListed)) sb.AppendLine("  *** " + c);
        }

        // ---------- write ----------
        if (mode == "write" && hasHazard)
        {
            // compare by ElementId, never by int — a 2024+ id does not fit an int (revit-version-compatibility.md)
            var writeIdSet = new HashSet<ElementId>(roomIdsToWrite.Select(i => new ElementId(i)));
            var targets = roomIdsToWrite.Length > 0
                ? placed.Where(r => writeIdSet.Contains(r.Id)).ToList()
                : placed;

            sb.AppendLine();
            sb.AppendLine($"WRITING '{hazardClassToWrite}' to {targets.Count:N0} room(s)"
                + (roomIdsToWrite.Length == 0 ? " — EVERY placed room in scope, because roomIdsToWrite was empty." : "."));
            if (roomIdsToWrite.Length == 0 && targets.Count > 1)
                sb.AppendLine("  *** That is a bulk change across a whole floor. If this floor is mixed, it is the WRONG thing"
                    + " to do — list the room Ids instead. Check the class summary above before accepting this.");

            using (var t = new Transaction(Document, "AJ Tools - Set Room Hazard Class"))
            {
                t.Start();
                try
                {
                    int wrote = 0, skipped = 0, differed = 0;
                    foreach (var r in targets)
                    {
                        var hp = r.LookupParameter(hazardParameterName);
                        if (hp == null || hp.IsReadOnly) { skipped++; continue; }
                        string existing = hp.HasValue ? (hp.AsString() ?? "") : "";
                        if (!string.IsNullOrWhiteSpace(existing) && !overwriteExisting)
                        {
                            if (existing != hazardClassToWrite) differed++;
                            skipped++; continue;
                        }
                        hp.Set(hazardClassToWrite);
                        var sp2 = r.LookupParameter(sourceParameterName);
                        if (sp2 != null && !sp2.IsReadOnly) sp2.Set(hazardSourceToWrite);
                        var stp = r.LookupParameter(standardParameterName);
                        if (stp != null && !stp.IsReadOnly) stp.Set(standardToWrite);
                        wrote++;
                    }
                    t.Commit();
                    sb.AppendLine($"  wrote {wrote:N0}, skipped {skipped:N0}"
                        + (differed > 0 ? $", of which {differed:N0} already held a DIFFERENT class and were left alone" : ""));
                    if (differed > 0)
                        sb.AppendLine("  *** Those differing rooms are worth looking at before overwriting anything —"
                            + " somebody recorded a different decision.");
                    sb.AppendLine("  *** READ IT BACK in a SEPARATE call (run this again in \"report\" mode).");
                    sb.AppendLine("      This report is not evidence the model changed.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED (write) — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
        else if (mode == "write")
            sb.AppendLine("\n  Cannot write — the hazard parameter is not bound to Rooms. See the note above.");
    }
}

return sb.ToString();
