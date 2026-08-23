// ============================================================
// FRAGMENT (action) — action-create-assembly-views.cs
// PURPOSE: Turn each ASSEMBLY in `elements` into a set of shop-drawing views in one go — a 3D
//          orthographic, detail sections from the sides/top/front you ask for, a part list, a material
//          takeoff, and a sheet with all of it placed. The prefabrication/fabrication deliverable, which
//          is otherwise a long manual sequence per assembly.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — the ASSEMBLY INSTANCES, e.g.
//          from filter-by-category.cs with OST_Assemblies.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ AN ASSEMBLY IS THE ONLY THING IN REVIT THAT CAN GENERATE ITS OWN DRAWING SET, and it is the reason
//    this is worth a fragment rather than a manual routine. AssemblyViewUtils creates views that are
//    OWNED BY THE ASSEMBLY: they show only its members, they orient themselves to the assembly's own
//    origin rather than to project north, and they follow it if it moves. That is not reproducible with
//    create-view.cs, which makes a normal project view that happens to be cropped.
//        AssemblyViewUtils.Create3DOrthographic(doc, assemblyId)
//        AssemblyViewUtils.CreateDetailSection(doc, assemblyId, AssemblyDetailViewOrientation.…)
//        AssemblyViewUtils.CreatePartList(doc, assemblyId)
//        AssemblyViewUtils.CreateMaterialTakeoff(doc, assemblyId)
//        AssemblyViewUtils.CreateSheet(doc, assemblyId, titleBlockTypeId)
//    Note CreateSheet takes a TITLE BLOCK TYPE id, not a sheet — and it returns a sheet that is already
//    associated with the assembly, so its views can be placed on it and nothing else can.
//
// ✱✱ THE ASSEMBLY MUST HAVE ITS NAMING SORTED OUT FIRST. Revit refuses to create views for an assembly
//    whose type is not yet "complete" (`AssemblyInstance.AssemblyTypeName` empty, or the assembly still
//    being edited). The refusal comes back as an exception per assembly, is reported by name, and does
//    not stop the rest of the batch.
//
// ✱✱ THESE VIEWS ARE NOT PLACED ON THE SHEET BY THIS FRAGMENT unless placeOnSheet is on, and even then
//    the layout is a plain grid — Revit gives no automatic arrangement for assembly views. Placing is
//    Viewport.Create, the same call action-place-viewport-on-sheet.cs uses; the positions here are a
//    starting point to drag from, not a finished sheet.
//
// GOTCHA: DRY RUN BY DEFAULT — it lists what it would create per assembly and stops.
// GOTCHA: RUNNING IT TWICE MAKES A SECOND SET. There is no "already has views" check in the API, so this
//         reports how many views each assembly already owns and skips any assembly that already has
//         some, unless allowDuplicates is turned on. That guard is the difference between a tidy set and
//         forty stray views nobody can name.
// GOTCHA: a MATERIAL TAKEOFF and a PART LIST are schedules — they land in the project browser under
//         Schedules, grouped by the assembly, not under Views.
// GOTCHA: the detail-section orientations are fixed by Revit's own enum (Front/Back/Top/Bottom/Left/
//         Right/Detail…). Names vary slightly by version, so they are resolved BY NAME at run time and
//         any that this Revit does not have are reported instead of failing to compile.
// RELATED: create-sheet.cs and action-place-viewport-on-sheet.cs for ordinary project sheets — an
//          assembly sheet is a different thing and cannot host ordinary views.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE assembly and look at the
//   project browser before doing a whole batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;
bool create3D = true;
string[] detailSections = new[] { "Front", "Top", "Left" }; // AssemblyDetailViewOrientation names
bool createPartList = true;
bool createMaterialTakeoff = false;
bool placeOnSheet = false;          // also make an assembly sheet and drop the views onto it
string titleBlockTypeName = null;   // null = first title block type found
bool allowDuplicates = false;       // true = create another set even if the assembly already has views
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put filter-by-category.cs with OST_Assemblies above this.");
    return sb.ToString();
}

var assemblies = elements.OfType<AssemblyInstance>().ToList();
int notAssemblies = elements.Count - assemblies.Count;
if (assemblies.Count == 0)
{
    sb.AppendLine($"None of the {elements.Count} element(s) is an AssemblyInstance. Select assemblies (OST_Assemblies), not their members.");
    return sb.ToString();
}

// ---- how many views does each assembly already own? ----
Func<AssemblyInstance, int> existingViewCount = a =>
{
    try
    {
        return new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
            .Count(v => v.AssociatedAssemblyInstanceId == a.Id);
    }
    catch { return -1; }
};

// ---- resolve the orientation enum BY NAME, so one source runs on every Revit ----
var orientationType = typeof(AssemblyDetailViewOrientation);
var wantedOrientations = new List<(string name, object value)>();
var unknownOrientations = new List<string>();
foreach (var n in detailSections)
{
    try
    {
        var v = Enum.Parse(orientationType, n, true);
        wantedOrientations.Add((n, v));
    }
    catch { unknownOrientations.Add(n); }
}
if (unknownOrientations.Count > 0)
    sb.AppendLine($"⚠ This Revit has no detail-section orientation called: {string.Join(", ", unknownOrientations)}. Valid names here: {string.Join(", ", Enum.GetNames(orientationType))}");

// ---- title block, if a sheet is wanted ----
ElementId titleBlockId = ElementId.InvalidElementId;
if (placeOnSheet)
{
    var tbTypes = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().ToList();
    var chosen = string.IsNullOrEmpty(titleBlockTypeName)
        ? tbTypes.FirstOrDefault()
        : tbTypes.FirstOrDefault(t => t.Name.Equals(titleBlockTypeName, StringComparison.OrdinalIgnoreCase));
    if (chosen == null)
    {
        sb.AppendLine(tbTypes.Count == 0
            ? "placeOnSheet is on but this model has NO title block types loaded — load one first."
            : $"No title block type named '{titleBlockTypeName}'. Available: {string.Join(", ", tbTypes.Select(t => t.Name))}");
        return sb.ToString();
    }
    titleBlockId = chosen.Id;
}

// ---- plan ----
sb.AppendLine($"{assemblies.Count} assembly instance(s) in{(notAssemblies > 0 ? $" ({notAssemblies} non-assembly element(s) ignored)" : "")}.");
sb.AppendLine("Assembly | Type name | Views it already owns | Action");
sb.AppendLine("--- | --- | --- | ---");

var todo = new List<AssemblyInstance>();
foreach (var a in assemblies)
{
    int have = existingViewCount(a);
    string typeName = "";
    try { typeName = a.AssemblyTypeName ?? ""; } catch { }
    bool skip = have > 0 && !allowDuplicates;
    sb.AppendLine($"{a.Id} | {(string.IsNullOrEmpty(typeName) ? "(not named yet)" : typeName)} | {(have < 0 ? "?" : have.ToString())} | {(skip ? "SKIP — already has views" : "create")}");
    if (!skip) todo.Add(a);
}

int perAssembly = (create3D ? 1 : 0) + wantedOrientations.Count + (createPartList ? 1 : 0) + (createMaterialTakeoff ? 1 : 0);

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine($"DRY RUN — would create about {perAssembly} view(s) for each of {todo.Count} assembly/assemblies{(placeOnSheet ? ", plus one sheet each" : "")}. Nothing changed.");
    return sb.ToString();
}

if (todo.Count == 0)
{
    sb.AppendLine();
    sb.AppendLine("Nothing to do — every assembly already owns views. Set allowDuplicates = true only if a second set is really wanted.");
    return sb.ToString();
}

int made = 0, sheets = 0, failedAssemblies = 0;
using (var t = new Transaction(Document, "AJ Tools - Create Assembly Views"))
{
    t.Start();
    try
    {
        foreach (var a in todo)
        {
            var created = new List<View>();
            try
            {
                if (create3D)
                {
                    var v3 = AssemblyViewUtils.Create3DOrthographic(Document, a.Id);
                    if (v3 != null) created.Add(v3);
                }
                foreach (var (name, value) in wantedOrientations)
                {
                    var vs = AssemblyViewUtils.CreateDetailSection(Document, a.Id, (AssemblyDetailViewOrientation)value);
                    if (vs != null) created.Add(vs);
                }
                if (createPartList)
                {
                    var pl = AssemblyViewUtils.CreatePartList(Document, a.Id);
                    if (pl != null) created.Add(pl);
                }
                if (createMaterialTakeoff)
                {
                    var mt = AssemblyViewUtils.CreateMaterialTakeoff(Document, a.Id);
                    if (mt != null) created.Add(mt);
                }

                made += created.Count;

                if (placeOnSheet)
                {
                    var sheet = AssemblyViewUtils.CreateSheet(Document, a.Id, titleBlockId);
                    if (sheet != null)
                    {
                        sheets++;
                        // Plain grid. Revit arranges nothing for you; these are positions to drag from.
                        double x = 1.0, y = 1.0;
                        foreach (var v in created)
                        {
                            try
                            {
                                if (v is ViewSchedule)
                                    ScheduleSheetInstance.Create(Document, sheet.Id, v.Id, new XYZ(x, y, 0));
                                else if (Viewport.CanAddViewToSheet(Document, sheet.Id, v.Id))
                                    Viewport.Create(Document, sheet.Id, v.Id, new XYZ(x, y, 0));
                            }
                            catch { }
                            x += 0.6;
                            if (x > 2.2) { x = 1.0; y -= 0.6; }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failedAssemblies++;
                sb.AppendLine($"  FAILED for assembly {a.Id}: {ex.Message}");
            }
        }

        t.Commit();
        sb.AppendLine();
        sb.AppendLine($"Created {made} view(s){(placeOnSheet ? $" and {sheets} assembly sheet(s)" : "")} across {todo.Count - failedAssemblies} assembly/assemblies. {failedAssemblies} failed.");
        if (placeOnSheet) sb.AppendLine("The sheet layout is a plain grid — arrange it by hand; Revit has no automatic arrangement for assembly views.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
