// ============================================================
// FRAGMENT (action) — action-export-families.cs
// PURPOSE: Pull every loadable family OUT of the open project and save each one as its own .rfa in a
//          folder. The way you recover a family from a model somebody sent you, and the way you take a
//          whole project's family set into the library in one pass.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). It does not modify the open project at all.
//
// WHY THIS EXISTS: `Document.EditFamily` appeared in NO fragment in this library, so "get me that family
//   out of this model" had no answer. It is the counterpart to creators/load-family.cs, which puts one
//   in.
//
// ✱✱ IT WORKS BECAUSE EditFamily HANDS BACK A DOCUMENT WITH NO WINDOW. The bridge cannot save the
//    document Ajmal is looking at — that restriction is about the UI document — but `EditFamily`
//    returns a background family document, and a document with no UI window saves normally. Same
//    mechanism that lets families be authored end to end, and the same one the folder upgrade uses.
//    Each family is opened, saved and CLOSED before the next starts, so memory does not climb through a
//    library-sized project.
//
// ✱✱ THREE KINDS OF FAMILY WILL NOT COME OUT, and none of them is an error:
//    * SYSTEM families — walls, floors, ducts, pipes, roofs. They are not loadable, have no .rfa, and
//      live only inside the project. `Family.IsEditable` is false for them and they are counted and
//      named as skipped rather than reported as failures.
//    * IN-PLACE families. They belong to the project by definition, and Revit refuses to edit them out.
//    * Anything the file is not allowed to open — a family from a newer Revit, or one whose file is
//      protected.
//
// ✱✱ NAME CLASHES ARE REAL AND SILENT. Two families can share a name in a project if they came from
//    different folders, and writing both to `<name>.rfa` means the second overwrites the first with no
//    warning. This numbers the second and after (`Name (2).rfa`) and says so in the report.
//
// RELATED: creators/load-family.cs (the reverse — put one in, and note it uses Revit's OWN
//          IFamilyLoadOptions rather than a class a fragment cannot declare),
//          actions/structural-changes/action-batch-upgrade-revit-files.cs (a FOLDER of .rfa files moved
//          to this Revit version — run that after this to upgrade what came out),
//          actions/structural-changes/action-purge-unused-families.cs (decide what is worth keeping first).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Dry-run by default — it lists what it WOULD write and stops. Set
//    dryRun = false only after reading that list. Writes files to disk; it never changes the model.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                       // true = list what would be written, write nothing

string targetFolder = @"D:\Ajmal\FamilyExport";   // created if it does not exist

string nameContains = "";                 // "" = every loadable family; else only names matching
BuiltInCategory[] onlyCategories = { };   // empty = every category; else restrict, e.g.
                                          // { BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_DuctTerminal }

bool foldersByCategory = false;           // true = write into <targetFolder>\<Category>\<Family>.rfa
int maxFamilies = 0;                      // 0 = no limit; else stop after this many (try 3 first)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (string.IsNullOrWhiteSpace(targetFolder))
{
    sb.AppendLine("targetFolder is empty — nothing to write to.");
    return sb.ToString();
}

var families = new FilteredElementCollector(Document)
    .OfClass(typeof(Family))
    .Cast<Family>()
    .Where(f => f != null && f.IsValidObject)
    .ToList();

if (families.Count == 0)
{
    sb.AppendLine("This project contains no loadable families at all. (Walls, floors, ducts and pipes are SYSTEM families — they have no .rfa and cannot be exported.)");
    return sb.ToString();
}

Func<Family, string> CategoryOf = f =>
{
    try { return f.FamilyCategory != null ? (f.FamilyCategory.Name ?? "Uncategorised") : "Uncategorised"; }
    catch { return "Uncategorised"; }
};

var wantedCatIds = new HashSet<ElementId>();
foreach (var bic in onlyCategories)
{
    try { var c = Category.GetCategory(Document, bic); if (c != null) wantedCatIds.Add(c.Id); } catch { }
}

var notEditable = new List<string>();
var candidates = new List<Family>();

foreach (var f in families)
{
    string fname;
    try { fname = f.Name ?? ""; } catch { continue; }

    if (!string.IsNullOrEmpty(nameContains)
        && fname.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

    if (wantedCatIds.Count > 0)
    {
        try { if (f.FamilyCategory == null || !wantedCatIds.Contains(f.FamilyCategory.Id)) continue; }
        catch { continue; }
    }

    // The one honest gate. A system family or an in-place family reports false here, and that is a
    // fact about the family, not a failure of this fragment.
    bool editable = false;
    try { editable = f.IsEditable; } catch { }
    if (!editable) { notEditable.Add($"{fname}  [{CategoryOf(f)}]"); continue; }

    candidates.Add(f);
}

if (candidates.Count == 0)
{
    sb.AppendLine($"No loadable, editable family matched. {families.Count} family element(s) in the project, {notEditable.Count} of them not editable (system or in-place).");
    if (notEditable.Count > 0)
    {
        sb.AppendLine("Not editable — these have no .rfa to write:");
        foreach (var n in notEditable.Take(20)) sb.AppendLine($"  {n}");
        if (notEditable.Count > 20) sb.AppendLine($"  ... and {notEditable.Count - 20} more.");
    }
    return sb.ToString();
}

if (maxFamilies > 0 && candidates.Count > maxFamilies)
    candidates = candidates.Take(maxFamilies).ToList();

// Work the file names out UP FRONT so the dry run shows the real paths, clashes and all.
var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var plan = new List<Tuple<Family, string, bool>>();   // family, full path, wasRenamedForClash

foreach (var f in candidates)
{
    string fname;
    try { fname = f.Name ?? "Unnamed"; } catch { fname = "Unnamed"; }

    // Strip what Windows will not accept in a file name, or SaveAs throws on a family whose name
    // contains a slash — which happens more than expected on imported content.
    foreach (var bad in System.IO.Path.GetInvalidFileNameChars()) fname = fname.Replace(bad, '_');

    string dir = foldersByCategory
        ? System.IO.Path.Combine(targetFolder, CategoryOf(f))
        : targetFolder;

    string baseName = fname;
    string candidate = System.IO.Path.Combine(dir, baseName + ".rfa");
    bool renamed = false;
    int n = 2;
    while (used.Contains(candidate))
    {
        candidate = System.IO.Path.Combine(dir, $"{baseName} ({n}).rfa");
        renamed = true;
        n++;
    }
    used.Add(candidate);
    plan.Add(Tuple.Create(f, candidate, renamed));
}

int clashes = plan.Count(p => p.Item3);

if (dryRun)
{
    sb.AppendLine($"DRY RUN — would write {plan.Count} family file(s) to {targetFolder}. Nothing written.");
    foreach (var p in plan.Take(40))
        sb.AppendLine($"  {p.Item2}" + (p.Item3 ? "   <- name clash, numbered" : ""));
    if (plan.Count > 40) sb.AppendLine($"  ... and {plan.Count - 40} more.");
    if (clashes > 0)
        sb.AppendLine($"  {clashes} family name(s) appear more than once in this project — numbered so nothing is overwritten silently.");
    if (notEditable.Count > 0)
        sb.AppendLine($"  {notEditable.Count} family element(s) skipped as not editable (system or in-place) — they have no .rfa.");
    sb.AppendLine("Set dryRun = false to write. Try maxFamilies = 3 first and open one of the results.");
    return sb.ToString();
}

int written = 0, failed = 0;
var notes = new List<string>();

try { System.IO.Directory.CreateDirectory(targetFolder); }
catch (Exception exDir)
{
    sb.AppendLine($"Could not create {targetFolder}: {exDir.Message}. Nothing written.");
    return sb.ToString();
}

foreach (var p in plan)
{
    var fam = p.Item1;
    string path = p.Item2;
    string label;
    try { label = fam.Name ?? "Unnamed"; } catch { label = "Unnamed"; }

    Document famDoc = null;
    try
    {
        try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); } catch { }

        // NO Transaction here on purpose. EditFamily opens a SEPARATE document; the open project is
        // never modified, so there is nothing in it to roll back.
        famDoc = Document.EditFamily(fam);
        if (famDoc == null) { failed++; notes.Add($"  {label}: Revit returned no family document."); continue; }

        // Belt and braces: EditFamily should only ever hand back a family document, but saving a
        // PROJECT to a .rfa path would produce a file that looks right and opens as the wrong thing.
        if (!famDoc.IsFamilyDocument)
        {
            failed++;
            notes.Add($"  {label}: what came back is not a family document — not saved.");
            try { famDoc.Close(false); } catch { }
            continue;
        }

        var opts = new SaveAsOptions();
        opts.OverwriteExistingFile = true;
        opts.Compact = true;
        famDoc.SaveAs(path, opts);
        written++;
        if (p.Item3) notes.Add($"  {label}: written as {System.IO.Path.GetFileName(path)} (name clash).");
    }
    catch (Exception exOne)
    {
        failed++;
        notes.Add($"  {label}: FAILED — {exOne.Message}");
    }
    finally
    {
        // Close WITHOUT saving again — the SaveAs above is the save. Leaving these open is what makes a
        // big export run out of memory halfway through.
        try { if (famDoc != null) famDoc.Close(false); } catch { }
    }
}

sb.AppendLine($"Family export: {written} written, {failed} failed, {notEditable.Count} skipped as not editable.");
sb.AppendLine($"  Folder: {targetFolder}");
foreach (var n in notes.Take(50)) sb.AppendLine(n);
if (notes.Count > 50) sb.AppendLine($"  ... and {notes.Count - 50} more notes.");
if (notEditable.Count > 0)
    sb.AppendLine($"  The {notEditable.Count} skipped are SYSTEM or IN-PLACE families — they have no .rfa and never will. That is not a failure.");
if (written > 0)
    sb.AppendLine("Open one of the results before trusting the batch. If they need to be a different Revit version, run action-batch-upgrade-revit-files.cs on the folder.");

return sb.ToString();
