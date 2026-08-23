// ============================================================
// FRAGMENT (action) — action-report-external-references.cs
// PURPOSE: Every FILE this model depends on, in one list — RVT links, CAD links AND imports, point
//          clouds, keynote tables, decal images, IFC links. The "what has to travel with this model"
//          and "what is missing on the new machine" answer, and the dependency list an issue/handover
//          record needs.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). Read-only, opens no transaction.
//
// ✱✱ WHY THIS EXISTS ALONGSIDE context-linked-models.cs: THAT ONE ONLY SEES RVT LINKS. It collects
//    RevitLinkInstance, so a model whose real risk is three unresolved DWGs and a point cloud reports as
//    clean. Revit keeps one list of ALL of them:
//        ExternalFileUtils.GetAllExternalFileReferences(doc)
//    and each entry answers what kind it is, where it points, whether the path is absolute or relative,
//    and whether Revit can currently find it. That last one is the whole point: a link that is Loaded
//    on this PC and NotFound on the next is the single most common "it opened fine for me" problem.
//
// ✱✱ THERE ARE TWO PATHS PER REFERENCE AND THEY ARE NOT THE SAME. GetPath() is what is SAVED in the file
//    — which may be relative. GetAbsolutePath() is what it currently RESOLVES TO on this machine. When a
//    link goes missing after a move, the saved path is usually still right and the absolute one is the
//    one that has gone stale, so both are printed. A ModelPath is not a string; it has to go through
//    ModelPathUtils.ConvertModelPathToUserVisiblePath before anyone can read it.
//
// GOTCHA: an IMPORTED CAD file (Insert > Import, not Link) is embedded — it appears here as a reference
//         but there is no external file to lose. That is why the type column matters: imports bloat the
//         file, links break. model-health-audit.cs counts imports for the bloat question.
// GOTCHA: a reference whose status is Unloaded is a deliberate choice, not a fault. NotFound and
//         Invalid are the ones to act on.
// GOTCHA: cloud-hosted references (BIM 360 / ACC) have no local path and report the cloud identity
//         instead — that is correct, not a failure to resolve.
// RELATED: context-linked-models.cs for RVT links in detail (pinned, workset, per-instance);
//          action-reload-links.cs to act on what this finds.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on a model you know has a DWG and
//   check the DWG appears with the right status before trusting it as a handover checklist.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool onlyProblems = false;   // true = list only references that are NOT loaded/found
int maxReported = 200;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

ICollection<ElementId> refIds = null;
try { refIds = ExternalFileUtils.GetAllExternalFileReferences(Document); }
catch (Exception ex)
{
    sb.AppendLine($"Could not read the external reference list: {ex.Message}");
    return sb.ToString();
}

if (refIds == null || refIds.Count == 0)
{
    sb.AppendLine("This model has NO external file references — nothing linked, imported or attached.");
    return sb.ToString();
}

Func<ModelPath, string> readable = mp =>
{
    if (mp == null) return "(none)";
    try
    {
        var s = ModelPathUtils.ConvertModelPathToUserVisiblePath(mp);
        return string.IsNullOrEmpty(s) ? "(cloud or unnamed path)" : s;
    }
    catch { return "(unreadable path)"; }
};

var rows = new List<(string kind, string name, string status, string savedPath, string resolvedPath, string pathType, ElementId id)>();
int problems = 0;

foreach (var id in refIds)
{
    var el = Document.GetElement(id);
    string name = el != null ? (string.IsNullOrEmpty(el.Name) ? "(unnamed)" : el.Name) : "(element not resolved)";

    ExternalFileReference xf = null;
    try { xf = ExternalFileUtils.GetExternalFileReference(Document, id); }
    catch { }

    if (xf == null)
    {
        rows.Add(("(unreadable)", name, "reference could not be read", "-", "-", "-", id));
        problems++;
        continue;
    }

    string kind, status, pathType;
    try { kind = xf.ExternalFileReferenceType.ToString(); } catch { kind = "?"; }
    try { status = xf.GetLinkedFileStatus().ToString(); } catch { status = "?"; }
    try { pathType = xf.PathType.ToString(); } catch { pathType = "?"; }

    string saved = "-", resolved = "-";
    try { saved = readable(xf.GetPath()); } catch { }
    try { resolved = readable(xf.GetAbsolutePath()); } catch { }

    bool bad = status != "Loaded" && status != "Unloaded";
    if (bad) problems++;
    if (onlyProblems && !bad) continue;

    rows.Add((kind, name, status, saved, resolved, pathType, id));
}

sb.AppendLine($"=== EXTERNAL FILE REFERENCES — {refIds.Count} total, {problems} needing attention ===");
if (onlyProblems) sb.AppendLine("(showing problems only — set onlyProblems = false for the full list)");
sb.AppendLine();

foreach (var g in rows.GroupBy(r => r.kind).OrderBy(g => g.Key))
{
    sb.AppendLine($"--- {g.Key}: {g.Count()} ---");
    sb.AppendLine("Name | Status | Path type | Saved path | Resolves to now | Id");
    sb.AppendLine("--- | --- | --- | --- | --- | ---");
    foreach (var r in g.Take(maxReported))
        sb.AppendLine($"{r.name} | {r.status} | {r.pathType} | {r.savedPath} | {r.resolvedPath} | {r.id}");
    if (g.Count() > maxReported) sb.AppendLine($"... {g.Count() - maxReported} more not shown.");
    sb.AppendLine();
}

if (problems > 0)
    sb.AppendLine($"⚠ {problems} reference(s) are not Loaded or Unloaded — those are the ones that will not open on another machine.");
else
    sb.AppendLine("Every reference is Loaded or deliberately Unloaded.");

return sb.ToString();
