// ============================================================
// FRAGMENT (action) — action-export-3d-to-fbx.cs
// PURPOSE: Export a 3D view to FBX — the handover format for Navisworks, 3ds Max, Twinmotion and any
//          game engine — and, optionally, build a clean 3D view to export FROM rather than shipping
//          whatever the current view happens to be showing.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
//
// WHY THIS EXISTS: the library exports to IFC, NWC, PDF, DWG, images and CSV, and had no FBX path at
//   all — `FBXExportOptions` appeared in no fragment. FBX is the one the visualisation and clash people
//   ask for.
//
// ✱✱ THE EXPORT IS THE EASY HALF. `Document.Export(folder, name, ViewSet, FBXExportOptions)` is one
//    call. What decides whether the result is usable is the STATE OF THE VIEW, because FBX carries
//    exactly what the view draws — and a working coordination view is the wrong thing to ship:
//      * a live SECTION BOX crops the model, and the receiver gets a sliced building with no clue why;
//      * a live CROP does the same in plan extent;
//      * hidden categories stay hidden — whole trades silently absent from the export;
//      * annotations export as geometry and land in Navisworks as floating junk;
//      * Coarse detail exports coarse geometry, so a duct arrives as a stick.
//    `prepareCleanView = true` makes a fresh isometric with all of that set correctly, exports it, and
//    (unless you keep it) deletes it again — so the export is repeatable and nobody's working view is
//    touched.
//
// ✱✱ FBX EXPORT ONLY TAKES 3D VIEWS. A plan, section or sheet in the ViewSet is rejected by Revit, not
//    silently skipped, so the view is checked before the call rather than after the failure.
//
// RELATED: actions/sheets-views/action-export-nwc.cs and action-export-ifc.cs (the other coordination
//          formats), action-export-views-to-dwg.cs (2D), action-export-view-image.cs,
//          creators/create-workset-3d-views.cs (per-workset 3D views, a different job).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Dry-run by default — it reports what it WOULD export and stops.
//    Writes files to disk; with prepareCleanView it also creates (and by default deletes) one view.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;

string targetFolder = @"D:\Ajmal\Export";   // created if it does not exist
string fileName = "";                        // "" = use the view name; no extension either way

string source = "active";        // "active"  = the active view (must be a 3D view)
                                 // "named"   = the 3D view called viewName
                                 // "clean"   = build a fresh isometric set up for export

string viewName = "";            // used by source "named"

// "clean" only — how the throwaway view is set up.
bool keepCleanView = false;      // false = delete it again after exporting (leaves the model as found)
string cleanViewName = "AJ FBX Export";
bool hideAnnotations = true;
bool showAllCategories = true;   // switch every hidden category back on, so no trade is missing

// FBX options.
bool stopOnError = true;         // false = Revit pushes past problems and you may not know
bool withoutBoundaryEdges = false;  // true = drop the edge lines; smaller file, flatter look
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (string.IsNullOrWhiteSpace(targetFolder))
{
    sb.AppendLine("targetFolder is empty — nothing to write to.");
    return sb.ToString();
}

// ---- Resolve or plan the view
View3D view = null;
bool madeView = false;

if (source == "active")
{
    view = Document.ActiveView as View3D;
    if (view == null)
    {
        sb.AppendLine($"The active view is '{Document.ActiveView.Name}' ({Document.ActiveView.ViewType}). FBX export takes a 3D VIEW only — switch to one, or use source \"named\" or \"clean\".");
        return sb.ToString();
    }
}
else if (source == "named")
{
    view = new FilteredElementCollector(Document)
        .OfClass(typeof(View3D)).Cast<View3D>()
        .FirstOrDefault(v => v != null && !v.IsTemplate
            && string.Equals(v.Name, viewName, StringComparison.OrdinalIgnoreCase));
    if (view == null)
    {
        sb.AppendLine($"No 3D view named '{viewName}'. 3D views in this document:");
        foreach (var v in new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>().Where(v => !v.IsTemplate).Take(25))
            sb.AppendLine($"  '{v.Name}'");
        return sb.ToString();
    }
}
else if (source != "clean")
{
    sb.AppendLine($"Unknown source \"{source}\" — expected active / named / clean.");
    return sb.ToString();
}

string outName = !string.IsNullOrWhiteSpace(fileName)
    ? fileName
    : (source == "clean" ? cleanViewName : view.Name);
foreach (var bad in System.IO.Path.GetInvalidFileNameChars()) outName = outName.Replace(bad, '_');

if (dryRun)
{
    sb.AppendLine($"DRY RUN — would export FBX to {System.IO.Path.Combine(targetFolder, outName + ".fbx")}. Nothing written.");
    if (source == "clean")
    {
        sb.AppendLine($"  would CREATE a 3D view '{cleanViewName}' set up for export"
            + (keepCleanView ? " and keep it." : " and delete it again afterwards."));
        sb.AppendLine($"  set-up: detail Fine, style Realistic, discipline Coordination, crop off, section box off"
            + (showAllCategories ? ", every hidden category switched back on" : "")
            + (hideAnnotations ? ", annotations hidden" : ""));
    }
    else
    {
        sb.AppendLine($"  from the existing 3D view '{view.Name}'.");
        bool boxOn = false; try { boxOn = view.IsSectionBoxActive; } catch { }
        if (boxOn) sb.AppendLine("  ! This view has a LIVE SECTION BOX. The FBX will be cropped to it — the receiver gets a sliced building. Use source \"clean\" unless you mean that.");
        if (view.CropBoxActive) sb.AppendLine("  ! This view has a LIVE CROP. The FBX will be cropped to it.");
        try { if (view.DetailLevel == ViewDetailLevel.Coarse) sb.AppendLine("  ! Detail level is COARSE — ducts and pipes export as sticks, not as bodies."); } catch { }
    }
    sb.AppendLine("Set dryRun = false to export.");
    return sb.ToString();
}

try { System.IO.Directory.CreateDirectory(targetFolder); }
catch (Exception exDir)
{
    sb.AppendLine($"Could not create {targetFolder}: {exDir.Message}. Nothing written.");
    return sb.ToString();
}

// ---- Build the clean view if asked. Its own transaction, so a failure here leaves nothing behind.
if (source == "clean")
{
    using (var t = new Transaction(Document, "AJ Tools - Create FBX Export View"))
    {
        t.Start();
        try
        {
            var vft = new FilteredElementCollector(Document)
                .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            if (vft == null)
            {
                t.RollBack();
                sb.AppendLine("This document has no 3D view type, so a 3D view cannot be created. Use source \"active\" or \"named\".");
                return sb.ToString();
            }

            view = View3D.CreateIsometric(Document, vft.Id);
            madeView = true;

            // A name already in use throws, and losing the whole export to that would be silly.
            try { view.Name = cleanViewName; }
            catch { try { view.Name = cleanViewName + " " + view.Id.ToString(); } catch { } }
            outName = !string.IsNullOrWhiteSpace(fileName) ? outName : view.Name;
            foreach (var bad in System.IO.Path.GetInvalidFileNameChars()) outName = outName.Replace(bad, '_');

            // Each of these is refused by some view types, so each is asked first. A refusal is not a
            // failure — it just means this view does not carry that control.
            try { if (view.CanModifyViewDiscipline()) view.Discipline = ViewDiscipline.Coordination; } catch { }
            try { if (view.CanModifyDetailLevel()) view.DetailLevel = ViewDetailLevel.Fine; } catch { }
            try { if (view.CanModifyDisplayStyle()) view.DisplayStyle = DisplayStyle.Realistic; } catch { }

            if (showAllCategories)
            {
                foreach (Category cat in Document.Settings.Categories)
                {
                    try
                    {
                        if (view.GetCategoryHidden(cat.Id) && view.CanCategoryBeHidden(cat.Id))
                            view.SetCategoryHidden(cat.Id, false);
                    }
                    catch { }   // a category this view cannot control — leave it
                }
            }

            try { view.AreAnnotationCategoriesHidden = hideAnnotations; } catch { }

            // The three that silently crop the export.
            try { view.CropBoxActive = false; view.CropBoxVisible = false; } catch { }
            try { view.IsSectionBoxActive = false; } catch { }

            t.Commit();
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to build the export view — rolled back, nothing created. Reason: {ex.Message}");
            return sb.ToString();
        }
    }
}

// ---- Export. No transaction: an export reads the model, it does not modify it.
bool ok = false;
string failure = "";
try
{
    var set = new ViewSet();
    set.Insert(view);

    var opts = new FBXExportOptions();
    opts.StopOnError = stopOnError;
    opts.WithoutBoundaryEdges = withoutBoundaryEdges;

    ok = Document.Export(targetFolder, outName, set, opts);
}
catch (Exception ex)
{
    failure = ex.Message;
}

// ---- Clean up the throwaway view, whether the export worked or not.
if (madeView && !keepCleanView)
{
    using (var t = new Transaction(Document, "AJ Tools - Remove FBX Export View"))
    {
        t.Start();
        try { Document.Delete(view.Id); t.Commit(); }
        catch { t.RollBack(); sb.AppendLine($"NOTE — the temporary view '{cleanViewName}' could not be deleted and is still in the model. Delete it by hand."); }
    }
}

string outPath = System.IO.Path.Combine(targetFolder, outName + ".fbx");
if (ok)
{
    sb.AppendLine($"FBX exported: {outPath}");
    if (madeView)
        sb.AppendLine(keepCleanView
            ? $"  Exported from a purpose-built view '{cleanViewName}', which was KEPT in the model."
            : $"  Exported from a purpose-built view, which has been deleted again — the model is as you found it.");
    sb.AppendLine("  Open it in the receiving application before sending it on. FBX carries what the view drew, so a missing trade means a hidden category, not a failed export.");
}
else
{
    sb.AppendLine($"FBX export did NOT succeed. Target was {outPath}");
    if (!string.IsNullOrEmpty(failure)) sb.AppendLine($"  Reason: {failure}");
    else sb.AppendLine("  Revit returned false without an exception — most often the folder is not writable, or the view has nothing visible in it.");
}

return sb.ToString();
