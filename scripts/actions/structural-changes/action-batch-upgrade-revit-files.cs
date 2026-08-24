// ============================================================
// FRAGMENT (action) — action-batch-upgrade-revit-files.cs
// PURPOSE: Upgrade a WHOLE FOLDER of families and templates to the Revit version that is currently
//          running — open each file, save a copy into a destination folder, close it, and keep going
//          when one fails. The batch version of "open it and press Save As", for a family library that
//          has to move to a new Revit.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). It does not touch the model that is open.
//
// ✱✱ IT WORKS BECAUSE OPENING A FILE THIS WAY GIVES A DOCUMENT WITH NO WINDOW.
//    `Document.Application.OpenDocumentFile(path)` returns a background document. The bridge cannot save
//    the document Ajmal is looking at — that restriction is about the UI document — but a document
//    opened like this has no UI window and saves normally. Same mechanism that lets families be authored
//    end to end. Each file is opened, SaveAs'd and closed before the next one starts, so memory does not
//    climb through a large library.
//
// ✱✱ IT WRITES TO A DIFFERENT FOLDER, ALWAYS. There is no in-place mode and that is deliberate: an
//    upgrade is one-way. Once a family is saved in a newer Revit, the older Revit cannot open it again,
//    so overwriting the originals destroys the only copy that still works for anyone on the old version.
//    Point destinationFolder somewhere new and compare before replacing anything.
//
// ✱✱ PROJECT FILES ARE OFF BY DEFAULT AND SHOULD USUALLY STAY OFF. Families (.rfa) and templates
//    (.rft/.rte) upgrade cleanly. A .rvt is a different animal: if it is WORKSHARED, opening it this way
//    can take a central-file lock or detach it, and saving a copy of a central file is a real decision
//    about the project, not a maintenance task. includeProjectFiles exists for a local, non-workshared
//    file and warns every time it is used.
//
// GOTCHA: DRY RUN BY DEFAULT — it lists exactly which files it would open, and stops. Read that list.
// GOTCHA: A FILE ALREADY OPEN IN THIS REVIT CANNOT BE OPENED AGAIN. Those are detected by name and
//         skipped with a note rather than throwing halfway through the batch.
// GOTCHA: THIS IS SLOW AND REVIT IS BUSY WHILE IT RUNS — seconds per file, and a big library is minutes.
//         maxFiles is a deliberate brake; raise it once one batch has come back right.
// GOTCHA: run it OUTSIDE any transaction on the current model — this fragment opens none, and must not
//         be pasted inside another fragment's transaction block.
// GOTCHA: a family that fails to upgrade usually fails for a real reason (a corrupt nested family, a
//         missing type catalogue). The message is captured per file and the batch continues.
// RELATED: tools\check-scripts.cmd answers the other half of "I installed a newer Revit" — whether the
//          SCRIPT library still compiles. This one moves the CONTENT.
// ⚠ NOT YET RUN AGAINST A REAL LIBRARY — written 2026-08-23. Run it with dryRun on, then on ONE folder
//   of five families, and open two of the results by hand before pointing it at the whole library.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] sourceFolders = new[] { @"D:\Ajmal\BIM Resources\BIM Resources\NEW" };
string destinationFolder = @"D:\Ajmal\BIM Resources\Upgraded";
bool recursive = true;
bool dryRun = true;               // true = list the files and stop
bool includeProjectFiles = false; // .rvt — read the warning above before turning this on
int maxFiles = 50;                // brake; raise once a small batch has come back right
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var extensions = new List<string> { ".rfa", ".rft", ".rte" };
if (includeProjectFiles) extensions.Add(".rvt");

// ---- collect ----
var files = new List<string>();
foreach (var folder in sourceFolders)
{
    if (!System.IO.Directory.Exists(folder)) { sb.AppendLine($"NOT FOUND: source folder '{folder}'"); continue; }
    try
    {
        var opt = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
        foreach (var f in System.IO.Directory.EnumerateFiles(folder, "*.*", opt))
        {
            var ext = System.IO.Path.GetExtension(f);
            if (extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                files.Add(f);
        }
    }
    catch (Exception ex) { sb.AppendLine($"Could not read '{folder}': {ex.Message}"); }
}

// A Revit backup is not a file to upgrade — ...0001.rfa next to the real one.
files = files.Where(f => !System.Text.RegularExpressions.Regex.IsMatch(
            System.IO.Path.GetFileNameWithoutExtension(f), @"\.\d{4}$")).ToList();

if (files.Count == 0)
{
    sb.AppendLine("No files matched. Checked for: " + string.Join(", ", extensions));
    return sb.ToString();
}

// ---- what is already open in this Revit? Those cannot be opened again. ----
var openTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
try
{
    foreach (Document d in Document.Application.Documents)
    {
        if (!string.IsNullOrEmpty(d.PathName)) openTitles.Add(System.IO.Path.GetFullPath(d.PathName));
    }
}
catch { }

string runningVersion = "?";
try { runningVersion = Document.Application.VersionNumber; } catch { }

sb.AppendLine($"=== BATCH UPGRADE to Revit {runningVersion} ===");
sb.AppendLine($"{files.Count} file(s) found. Destination: {destinationFolder}");
if (includeProjectFiles)
    sb.AppendLine("⚠ PROJECT FILES (.rvt) ARE INCLUDED. If any of them is workshared, do not run this — open it in Revit yourself.");

if (files.Count > maxFiles)
    sb.AppendLine($"⚠ Only the first {maxFiles} will be processed (maxFiles). {files.Count - maxFiles} left for a later run.");

var batch = files.Take(maxFiles).ToList();

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — these would be opened and saved as copies. Nothing has been opened.");
    foreach (var f in batch.Take(100)) sb.AppendLine("  " + f);
    if (batch.Count > 100) sb.AppendLine($"  ... {batch.Count - 100} more.");
    sb.AppendLine();
    sb.AppendLine("Set dryRun = false to run it. The originals are never overwritten.");
    return sb.ToString();
}

if (!System.IO.Directory.Exists(destinationFolder))
{
    try { System.IO.Directory.CreateDirectory(destinationFolder); }
    catch (Exception ex) { sb.AppendLine($"Could not create '{destinationFolder}': {ex.Message}"); return sb.ToString(); }
}

int done = 0, failed = 0, skipped = 0;
var failures = new List<string>();

foreach (var file in batch)
{
    string full;
    try { full = System.IO.Path.GetFullPath(file); } catch { full = file; }

    if (openTitles.Contains(full)) { skipped++; failures.Add($"SKIPPED (already open in this Revit): {file}"); continue; }

    string target = System.IO.Path.Combine(destinationFolder, System.IO.Path.GetFileName(file));
    if (string.Equals(System.IO.Path.GetFullPath(target), full, StringComparison.OrdinalIgnoreCase))
    {
        skipped++;
        failures.Add($"SKIPPED (destination is the source — upgrading in place is refused): {file}");
        continue;
    }

    Document opened = null;
    try
    {
        // ✱✱ OPEN OPTIONS, ADDED 2026-08-24 — AND ON THIS FRAGMENT IT IS A SAFETY FIX, NOT A SPEED ONE.
        //    `OpenDocumentFile(path)` with no options, pointed at a WORKSHARED CENTRAL file, opens it as
        //    the central. Upgrading and saving from there is a genuinely damaging thing to do to a live
        //    project. `DetachAndPreserveWorksets` is the only correct way to open somebody else's model
        //    for a batch operation: the copy on disk is untouched until the explicit SaveAs below, and
        //    the detached document cannot write back to the central at all.
        //    `AllowOpeningLocalByWrongUser` matters because a folder of files to upgrade routinely
        //    contains other people's locals, and without it Revit simply refuses those.
        //    Worksets are left OPEN here on purpose — unlike a read-only comparison, an upgrade must
        //    rewrite every element in the file, so closing them would leave content unconverted.
        var openOpts = new OpenOptions
        {
            DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
            AllowOpeningLocalByWrongUser = true,
            Audit = false
        };
        try { openOpts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets)); }
        catch { }
        opened = Document.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(file), openOpts);
        var opts = new SaveAsOptions { OverwriteExistingFile = true };

        // Compact on the way out. An upgrade is the one moment the whole file is being rewritten
        // anyway, so this costs nothing extra and is the difference between a library that grows every
        // version and one that does not.
        opts.Compact = true;

        // ✱✱ THE THUMBNAIL, which is the bit that gets forgotten and then noticed by everyone.
        // A Revit file's preview image comes from ONE nominated view. Save without saying which, and
        // the upgraded copy can come out with a blank or meaningless preview — and for a family library
        // that is browsed by thumbnail in the Revit dialog, that is most of how people find anything.
        // Order: keep whatever the file already nominated; else the first 3D view Revit will accept;
        // else the first plan. `IsViewIdValidForPreview` is the gate — not every view can be a preview,
        // and handing it one that cannot makes SaveAs throw. Added 2026-08-23.
        try
        {
            var previewSettings = opened.GetDocumentPreviewSettings();
            if (previewSettings.PreviewViewId != ElementId.InvalidElementId)
            {
                opts.PreviewViewId = previewSettings.PreviewViewId;
            }
            else
            {
                var candidate = new FilteredElementCollector(opened).OfClass(typeof(View3D))
                        .Cast<View>()
                        .FirstOrDefault(v => !v.IsTemplate && previewSettings.IsViewIdValidForPreview(v.Id))
                    ?? new FilteredElementCollector(opened).OfClass(typeof(ViewPlan))
                        .Cast<View>()
                        .FirstOrDefault(v => !v.IsTemplate && previewSettings.IsViewIdValidForPreview(v.Id));

                if (candidate != null) opts.PreviewViewId = candidate.Id;
            }
        }
        catch { }   // no preview is worse than a good one and better than a failed save

        opened.SaveAs(target, opts);
        done++;
    }
    catch (Exception ex)
    {
        failed++;
        failures.Add($"FAILED: {file} — {ex.Message}");
    }
    finally
    {
        // Close without saving again. A document left open holds the file and the memory.
        if (opened != null) { try { opened.Close(false); } catch { } }
    }
}

sb.AppendLine();
sb.AppendLine($"Upgraded {done} file(s) into '{destinationFolder}'. {failed} failed, {skipped} skipped.");
if (failures.Count > 0)
{
    sb.AppendLine();
    foreach (var f in failures.Take(50)) sb.AppendLine("  " + f);
    if (failures.Count > 50) sb.AppendLine($"  ... {failures.Count - 50} more.");
}
sb.AppendLine();
sb.AppendLine("The originals are untouched. Open two of the upgraded files by hand before replacing anything.");

return sb.ToString();
