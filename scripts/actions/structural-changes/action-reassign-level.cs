// ============================================================
// FRAGMENT (action) — action-reassign-level.cs
// PURPOSE: Re-point every element in `elements` to a DIFFERENT reference level WITHOUT MOVING IT — the
//          fix for "these ducts are modelled on Level 1 but they belong to Level 2", where the schedule
//          and the level filter are wrong but the geometry is right.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱ THE WHOLE POINT IS THE OFFSET COMPENSATION. Changing an element's level parameter alone MOVES IT
//   VERTICALLY, because its height is stored as an offset FROM that level. So every write is paired:
//
//       newOffset = oldOffset + oldLevel.Elevation - newLevel.Elevation
//
//   Change the level and the offset together and the element stays exactly where it is on screen while
//   its level parameter becomes correct. Change only the level and 400 ducts jump a storey — silently,
//   because nothing throws.
//
// GOTCHA: THE LEVEL PARAMETER IS A DIFFERENT ONE PER KIND, and there is no single "level" parameter:
//           MEP curves (duct/pipe/tray/conduit) -> RBS_START_LEVEL_PARAM
//           family instances                    -> FAMILY_LEVEL_PARAM, else INSTANCE_REFERENCE_LEVEL_PARAM
//         The offset is INSTANCE_FREE_HOST_OFFSET_PARAM. Anything with none of these is reported as
//         unsupported rather than skipped silently.
// GOTCHA: DRY RUN BY DEFAULT. This is a bulk parameter change whose failure mode (everything jumps a
//         storey) is invisible in a plan view — you only see it in section.
// GOTCHA: A READ-ONLY LEVEL PARAMETER MEANS THE ELEMENT IS CONSTRAINED, not that the fragment failed.
//         A wall-hosted or workplane-hosted element gets its level from its host. Those are counted and
//         named; re-host them or move the host instead.
// GOTCHA: THIS DOES NOT CHANGE WHICH LEVEL AN ELEMENT IS PHYSICALLY NEAREST. It changes the parameter.
//         If the geometry really is on the wrong storey, this is the wrong fragment — move it.
// GOTCHA: read the offset back after writing. Some elements accept the level change and reject the
//         offset, which leaves the element moved — that combination is reported as a FAILURE with the
//         id named, not counted as a success.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Reassign Reference Level.
//   Compile-checked on 2020/2024/2027. Dry-run it, then do ONE element and check it in a section.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report what would change; false = actually reassign
string toLevelName = "";             // REQUIRED — the level to re-point to, by name
bool compensateOffset = true;        // true = keep the element physically put (leave this true)
// ---- END INPUTS ----

const double MM = 304.8;

if (string.IsNullOrWhiteSpace(toLevelName))
{
    sb.AppendLine("Set toLevelName to the level these elements should belong to.");
}
else
{
    var toLevel = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
        .FirstOrDefault(l => string.Equals(l.Name, toLevelName, StringComparison.OrdinalIgnoreCase));

    if (toLevel == null)
    {
        var names = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => l.Elevation).Select(l => l.Name);
        sb.AppendLine($"Level '{toLevelName}' not found. Available: {string.Join(", ", names)}");
    }
    else
    {
        // the element's level parameter, whichever kind it is
        Func<Element, Parameter> levelParamOf = el =>
        {
            var p = el.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            if (p != null) return p;
            p = el.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (p != null) return p;
            return el.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
        };

        int done = 0, alreadyThere = 0, readOnly = 0, unsupported = 0, failed = 0;
        var rows = new List<string>();
        var problems = new List<string>();

        using (var t = new Transaction(Document, "AJ Tools - Reassign Reference Level"))
        {
            t.Start();
            try
            {
                foreach (var e in elements)
                {
                    if (e == null) { unsupported++; continue; }

                    var lp = levelParamOf(e);
                    if (lp == null || lp.StorageType != StorageType.ElementId) { unsupported++; continue; }

                    var oldLevelId = lp.AsElementId();
                    if (oldLevelId == toLevel.Id) { alreadyThere++; continue; }
                    if (lp.IsReadOnly)
                    {
                        readOnly++;
                        if (problems.Count < 15) problems.Add(e.Id + " (level is read-only — hosted or constrained)");
                        continue;
                    }

                    var oldLevel = Document.GetElement(oldLevelId) as Level;
                    double deltaFt = oldLevel != null ? (oldLevel.Elevation - toLevel.Elevation) : 0.0;

                    var offP = e.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                    bool canOffset = compensateOffset && offP != null && !offP.IsReadOnly &&
                                     offP.StorageType == StorageType.Double;

                    double oldOffset = canOffset ? offP.AsDouble() : 0.0;

                    if (dryRun)
                    {
                        done++;
                        if (rows.Count < 25)
                            rows.Add(e.Id + " | " + (e.Category != null ? e.Category.Name : "?") + " | " +
                                     (oldLevel != null ? oldLevel.Name : "?") + " | " + toLevel.Name + " | " +
                                     (canOffset ? Math.Round((oldOffset + deltaFt) * MM, 1).ToString() : "n/a"));
                        continue;
                    }

                    try
                    {
                        lp.Set(toLevel.Id);

                        if (canOffset && Math.Abs(deltaFt) > 1e-9)
                        {
                            // THE compensation — keeps the element physically where it was.
                            offP.Set(oldOffset + deltaFt);

                            // Read it back. Accepting the level and rejecting the offset leaves the
                            // element MOVED, which is worse than doing nothing — call that a failure.
                            double check = offP.AsDouble();
                            if (Math.Abs(check - (oldOffset + deltaFt)) > 1e-6)
                            {
                                failed++;
                                if (problems.Count < 15)
                                    problems.Add(e.Id + " (level changed but offset refused — THIS ELEMENT HAS MOVED)");
                                continue;
                            }
                        }

                        done++;
                        if (rows.Count < 25)
                            rows.Add(e.Id + " | " + (e.Category != null ? e.Category.Name : "?") + " | " +
                                     (oldLevel != null ? oldLevel.Name : "?") + " | " + toLevel.Name + " | " +
                                     (canOffset ? Math.Round((oldOffset + deltaFt) * MM, 1).ToString() : "n/a"));
                    }
                    catch (Exception ex2)
                    {
                        failed++;
                        if (problems.Count < 15) problems.Add(e.Id + " (" + ex2.Message + ")");
                    }
                }

                if (dryRun) t.RollBack(); else t.Commit();
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
                return sb.ToString();
            }
        }

        sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
                      $"Re-point to level '{toLevel.Name}' ({Math.Round(toLevel.Elevation * MM)} mm)" +
                      (compensateOffset ? ", offsets compensated so nothing moves" : ", OFFSETS NOT COMPENSATED — elements WILL move") + ".");
        sb.AppendLine((dryRun ? "Would change: " : "Changed: ") + done +
                      " | already on that level: " + alreadyThere +
                      (readOnly > 0 ? " | level read-only (hosted/constrained): " + readOnly : "") +
                      (unsupported > 0 ? " | no level parameter: " + unsupported : "") +
                      (failed > 0 ? " | FAILED: " + failed : ""));
        if (problems.Count > 0)
        {
            sb.AppendLine("Problems:");
            foreach (var p in problems) sb.AppendLine("  - " + p);
        }

        if (rows.Count > 0)
        {
            sb.AppendLine("");
            sb.AppendLine("Id | Category | From level | To level | New offset (mm)");
            sb.AppendLine("--- | --- | --- | --- | ---");
            foreach (var r in rows) sb.AppendLine(r);
            if (done > rows.Count) sb.AppendLine("... and " + (done - rows.Count) + " more");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
