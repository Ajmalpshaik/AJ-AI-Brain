// ============================================================
// FRAGMENT (action) — action-check-open-pipe-ends.cs
// PURPOSE: Find the pipes in `elements` that still have an OPEN END — a connector joined to nothing —
//          and optionally cap them. The QA sweep before a pipework model is issued, and the fix for the
//          run that was drawn but never terminated.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs
//          with OST_PipeCurves.
// ✱✱ FOUR FRAGMENTS ANSWER "OPEN ENDS". Pick by the sentence — only the last two can change the model:
//      filters/by-relationship/filter-by-connection-status.cs        FILTER: narrow any category to the
//                                                                    elements with an open connector.
//      actions/qa-checks/action-find-dead-end-system.cs              REPORT: sorts DELIBERATE ends from
//                                                                    accidents, lists only the accidents.
//      actions/qa-checks/action-check-open-pipe-ends.cs              PIPES: report open ends, and cap
//                                                                    them only if you say so.
//      actions/structural-changes/action-connect-open-connectors.cs  JOIN: connects open pairs that
//                                                                    already touch — WRITES, dry-run first.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ REVIT ANSWERS THIS DIRECTLY AND WE WERE WALKING CONNECTORS BY HAND:
//        PlumbingUtils.HasOpenConnector(doc, pipeId)   -> bool
//        PlumbingUtils.PlaceCapOnOpenEnds(doc, pipeId, capTypeId)
//    The first is one call against Revit's own connectivity state, which is the same state the system
//    browser reads. The second creates and connects the cap fittings itself — sizing them to the pipe
//    and joining them — which by hand means finding the right fitting family, working out the connector
//    direction and placing an instance per end.
//
// ✱✱ AN OPEN END IS NOT ALWAYS A FAULT, and this reports before it fixes. A riser waiting for the next
//    level, a run ending at a future connection, a pipe deliberately left for the contractor — all have
//    open ends on purpose. Capping them all makes the model look finished and hides real gaps, so
//    dryRun is on and the list comes first.
//
// ✱✱ THIS IS PIPES ONLY. There is no MechanicalUtils equivalent for ducts — PlaceCapOnOpenEnds lives in
//    PlumbingUtils and takes a pipe. Ducts are covered by drawing the cap as part of the run; see
//    recipes/draw-main-duct-with-cap.cs. Saying so here saves the ten minutes it takes to discover that
//    the symmetrical call does not exist.
//
// GOTCHA: DRY RUN BY DEFAULT.
// GOTCHA: capTypeIdInt = 0 lets Revit choose the cap from the pipe's routing preferences. If the routing
//         preferences name no cap, the call fails for that pipe and the failure is reported per pipe.
// GOTCHA: capping is one transaction for the whole batch — a failure part-way rolls back everything, so
//         nothing half-caps. Run it on a small set first.
// GOTCHA: a pipe with BOTH ends open gets both capped; the count below is pipes, not ends.
// RELATED: the native verify_connectivity tool and skills/ajtools-mep-connectivity-verify check whether a
//          system is joined end to end. This one asks a narrower question — is this pipe terminated.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run the report first and check two of the
//   pipes it names really do end in nothing.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;
bool capThem = false;      // false = report only, even when dryRun is off
int capTypeIdInt = 0;      // 0 = let Revit pick the cap from the pipe's routing preferences
int maxReported = 50;
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put filter-by-category.cs with OST_PipeCurves above this.");
    return sb.ToString();
}

var pipes = elements.OfType<Autodesk.Revit.DB.Plumbing.Pipe>().ToList();
int notPipes = elements.Count - pipes.Count;

if (pipes.Count == 0)
{
    sb.AppendLine($"None of the {elements.Count} element(s) is a Pipe. This fragment is pipes only — ducts have no PlaceCapOnOpenEnds equivalent (see recipes/draw-main-duct-with-cap.cs).");
    return sb.ToString();
}

var open = new List<Autodesk.Revit.DB.Plumbing.Pipe>();
int unreadable = 0;

foreach (var p in pipes)
{
    try
    {
        if (Autodesk.Revit.DB.Plumbing.PlumbingUtils.HasOpenConnector(Document, p.Id)) open.Add(p);
    }
    catch { unreadable++; }
}

sb.AppendLine($"{pipes.Count} pipe(s) checked{(notPipes > 0 ? $" ({notPipes} non-pipe element(s) ignored)" : "")}: {open.Count} with an open end{(unreadable > 0 ? $", {unreadable} could not be read" : "")}.");

if (open.Count == 0)
{
    sb.AppendLine("Every pipe in the set is connected at both ends.");
    return sb.ToString();
}

sb.AppendLine();
sb.AppendLine("Pipe Id | Size | System | Level");
sb.AppendLine("--- | --- | --- | ---");
foreach (var p in open.Take(maxReported))
{
    string size = "", system = "", level = "";
    try { size = p.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? ""; } catch { }
    try { system = p.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString() ?? ""; } catch { }
    try { level = (Document.GetElement(p.LevelId) as Level)?.Name ?? ""; } catch { }
    sb.AppendLine($"{p.Id} | {(string.IsNullOrEmpty(size) ? "-" : size)} | {(string.IsNullOrEmpty(system) ? "-" : system)} | {(string.IsNullOrEmpty(level) ? "-" : level)}");
}
if (open.Count > maxReported) sb.AppendLine($"... {open.Count - maxReported} more not shown.");

if (!capThem)
{
    sb.AppendLine();
    sb.AppendLine("Report only. An open end is often deliberate — check the list before capping. Set capThem = true (and dryRun = false) to cap them.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine($"DRY RUN — would cap the open ends on {open.Count} pipe(s). Nothing changed. Set dryRun = false to apply.");
    return sb.ToString();
}

var capTypeId = capTypeIdInt == 0 ? ElementId.InvalidElementId : new ElementId(capTypeIdInt);
int capped = 0, failed = 0;

using (var t = new Transaction(Document, "AJ Tools - Cap Open Pipe Ends"))
{
    t.Start();
    try
    {
        var fo = t.GetFailureHandlingOptions();
        fo.SetForcedModalHandling(false);
        fo.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(fo);

        foreach (var p in open)
        {
            try
            {
                Autodesk.Revit.DB.Plumbing.PlumbingUtils.PlaceCapOnOpenEnds(Document, p.Id, capTypeId);
                capped++;
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"  FAILED on pipe {p.Id}: {ex.Message}");
            }
        }

        t.Commit();
        sb.AppendLine();
        sb.AppendLine($"Capped {capped} pipe(s). {failed} failed.");
        sb.AppendLine("Re-run this fragment to confirm — HasOpenConnector should now be false for the ones that succeeded.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
