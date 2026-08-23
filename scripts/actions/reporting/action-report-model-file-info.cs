// ============================================================
// FRAGMENT (action) — action-report-model-file-info.cs
// PURPOSE: Read what a `.rvt` file IS — Revit version, central or local, who made the local, whether
//          it is workshared, whether local changes reached the central — for a whole folder, **WITHOUT
//          OPENING ANY OF THEM**. The "what did they actually send me" check, in seconds.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). It does not touch the open model and opens nothing.
//
// ✱✱ THIS IS THE POINT: `BasicFileInfo.Extract(path)` READS THE HEADER, NOT THE MODEL. Opening a Revit
//    file to answer "what version is this" costs minutes on a real project and locks the file while it
//    happens. Extract reads the small header block Revit writes at the front of every `.rvt` and comes
//    back in milliseconds. A folder of fifty models is a few seconds, not an afternoon.
//
// ✱✱ WHAT IT ANSWERS THAT NOTHING HERE COULD:
//    * **Is this a CENTRAL file or somebody's LOCAL?** Opening a local by mistake, or worse detaching
//      and saving over it, is the classic way to damage a workshared job. `IsCentral` / `IsLocal`.
//    * **Whose local is it?** `Username` — the person whose copy this is.
//    * **Did their work reach the central?** `AllLocalChangesSavedToCentral` — false means the model
//      they sent is missing work that only exists on their machine.
//    * **Is it newer than the Revit that would open it?** `IsSavedInLaterVersion` — the answer to
//      "why won't this open" BEFORE the failed attempt, not after.
//    * **Is it mid-save?** `IsInProgress` — a file caught during a save is not safe to copy.
//
// ✱✱ ONE THING TO KNOW ABOUT THE VERSION READING. `Format` is the raw version number Revit stamps in
//    the header. `IsSavedInCurrentVersion` compares it against the Revit that is RUNNING RIGHT NOW —
//    so the same file reports differently depending on which Revit you ask, and that is correct
//    behaviour rather than a bug. The running version is printed alongside so the answer can be read.
//
// ✱✱ AND A LIMIT WORTH STATING: this reads the HEADER. It cannot tell you what is inside the model —
//    no element counts, no categories, no warnings. For that the file has to be opened, which is
//    action-compare-models.cs and action-batch-upgrade-revit-files.cs.
//
// RELATED: actions/structural-changes/action-batch-upgrade-revit-files.cs (run THIS first to see which
//          files actually need upgrading), actions/qa-checks/action-compare-models.cs,
//          actions/reporting/action-report-element-ownership.cs (who owns what INSIDE an open model).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only on disk — it opens nothing and changes nothing, so the
//    worst case is a wrong reading. Check one file's version against Revit's own open dialog first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string folderPath = @"D:\Ajmal\Models";   // folder of .rvt files to survey
bool includeSubfolders = true;
bool onlyProblems = false;                // true = list only files worth acting on (later version,
                                          //        unsynced local work, mid-save)
int maxFilesListed = 200;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (string.IsNullOrWhiteSpace(folderPath) || !System.IO.Directory.Exists(folderPath))
{
    sb.AppendLine($"Folder not found: {folderPath}");
    return sb.ToString();
}

string[] files;
try
{
    files = System.IO.Directory.GetFiles(folderPath, "*.rvt",
        includeSubfolders ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
}
catch (Exception exDir)
{
    sb.AppendLine($"Could not read {folderPath}: {exDir.Message}");
    return sb.ToString();
}

if (files.Length == 0)
{
    sb.AppendLine($"No .rvt files in {folderPath}" + (includeSubfolders ? " or its subfolders." : "."));
    return sb.ToString();
}

string runningVersion = "";
try { runningVersion = Document.Application.VersionNumber; } catch { }

int centrals = 0, locals = 0, standalone = 0, unreadable = 0;
int laterVersion = 0, unsynced = 0, inProgress = 0;
var rows = new List<string>();
var byFormat = new Dictionary<string, int>();

foreach (var path in files)
{
    string name = System.IO.Path.GetFileName(path);
    BasicFileInfo info = null;
    try { info = BasicFileInfo.Extract(path); }
    catch (Exception exOne)
    {
        unreadable++;
        if (rows.Count < maxFilesListed)
            rows.Add($"  {name}\n      UNREADABLE — {exOne.Message}");
        continue;
    }
    if (info == null) { unreadable++; continue; }

    string fmt = "";
    try { fmt = info.Format ?? ""; } catch { }
    if (string.IsNullOrEmpty(fmt)) fmt = "(unknown)";
    if (!byFormat.ContainsKey(fmt)) byFormat[fmt] = 0;
    byFormat[fmt]++;

    bool ws = false, isCentral = false, isLocal = false, madeLocal = false;
    bool later = false, current = false, midSave = false, allSaved = true;
    string user = "", central = "";

    try { ws = info.IsWorkshared; } catch { }
    try { isCentral = info.IsCentral; } catch { }
    try { isLocal = info.IsLocal; } catch { }
    try { madeLocal = info.IsCreatedLocal; } catch { }
    try { later = info.IsSavedInLaterVersion; } catch { }
    try { current = info.IsSavedInCurrentVersion; } catch { }
    try { midSave = info.IsInProgress; } catch { }
    try { allSaved = info.AllLocalChangesSavedToCentral; } catch { }
    try { user = info.Username ?? ""; } catch { }
    try { central = info.CentralPath ?? ""; } catch { }

    if (isCentral) centrals++;
    else if (isLocal || madeLocal) locals++;
    else standalone++;

    var flags = new List<string>();
    if (later)   { laterVersion++; flags.Add("SAVED IN A LATER REVIT — this Revit cannot open it"); }
    if (midSave) { inProgress++;   flags.Add("SAVE IN PROGRESS — not safe to copy"); }
    if ((isLocal || madeLocal) && !allSaved) { unsynced++; flags.Add("LOCAL WITH UNSYNCED WORK — changes exist only on that machine"); }

    bool interesting = flags.Count > 0;
    if (onlyProblems && !interesting) continue;
    if (rows.Count >= maxFilesListed) continue;

    string kind = isCentral ? "CENTRAL" : ((isLocal || madeLocal) ? "local" : (ws ? "workshared" : "standalone"));
    var line = new System.Text.StringBuilder();
    line.Append($"  {name}\n      {kind}, format {fmt}");
    if (!current && !later) line.Append(" (older than this Revit)");
    if (!string.IsNullOrEmpty(user)) line.Append($", user '{user}'");
    if (!string.IsNullOrEmpty(central)) line.Append($"\n      central: {central}");
    foreach (var f in flags) line.Append($"\n      ! {f}");
    rows.Add(line.ToString());
}

sb.AppendLine($"REVIT FILES IN {folderPath}{(includeSubfolders ? " (including subfolders)" : "")} — {files.Length} file(s), read from the HEADER without opening any of them.");
if (!string.IsNullOrEmpty(runningVersion))
    sb.AppendLine($"  This Revit is version {runningVersion} — the 'current / later' answers below are relative to it.");
sb.AppendLine($"  {centrals} central, {locals} local, {standalone} standalone/non-workshared"
    + (unreadable > 0 ? $", {unreadable} unreadable" : "") + ".");

if (byFormat.Count > 0)
    sb.AppendLine("  by saved format: " + string.Join(", ", byFormat.OrderBy(k => k.Key).Select(k => $"{k.Key} x{k.Value}")));

if (laterVersion > 0 || unsynced > 0 || inProgress > 0)
{
    sb.AppendLine();
    sb.AppendLine("NEEDS ATTENTION:");
    if (laterVersion > 0) sb.AppendLine($"  {laterVersion} saved in a LATER Revit — this Revit cannot open them at all. Upgrading is one-way; get them re-issued instead.");
    if (unsynced > 0)     sb.AppendLine($"  {unsynced} local file(s) with work that never reached the central — what you have is not what they think they sent.");
    if (inProgress > 0)   sb.AppendLine($"  {inProgress} caught mid-save — do not copy or open these until the save finishes.");
}

if (rows.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine(onlyProblems ? "FILES WORTH ACTING ON" : $"EVERY FILE (first {rows.Count})");
    foreach (var r in rows) sb.AppendLine(r);
    if (files.Length > rows.Count && !onlyProblems)
        sb.AppendLine($"  ... {files.Length - rows.Count} more not listed (raise maxFilesListed).");
}
else if (onlyProblems)
{
    sb.AppendLine();
    sb.AppendLine("Nothing needs attention — no later-version files, no unsynced locals, none mid-save.");
}

sb.AppendLine();
sb.AppendLine("This reads the file HEADER only. It cannot say what is inside — no element counts, no categories, no warnings. For that the file has to be opened.");

return sb.ToString();
