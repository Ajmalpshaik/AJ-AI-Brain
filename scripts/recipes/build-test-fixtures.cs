// ============================================================
// RECIPE — build-test-fixtures.cs
// PURPOSE: Create, in an empty scratch model, the fixtures that several fragments cannot be tested
//          without. brain-log's open items list has a whole category reading "needs a live bridge AND a
//          model that actually contains the fixture — this is the real blocker, not effort." This is
//          the script that removes that blocker for everything an API can actually build.
//
// WHAT IT UNBLOCKS (fragments that today report BLOCKED for want of a fixture):
//   insulation -> action-add-remove-insulation.cs, filter-by-insulation-status.cs,
//                 filter-by-insulation-type.cs
//   assembly   -> filter-by-assembly.cs
//   group      -> filter-by-group.cs, action-ungroup-elements.cs
//   worksets   -> action-set-workset.cs, action-rename-workset.cs, filter-by-workset.cs,
//                 action-report-element-ownership.cs, command-sync-with-central.cs
//
// WHAT IT DELIBERATELY DOES NOT ATTEMPT — already proven impossible on Revit 2020, and re-attempting a
// settled answer wastes a session. It reports them instead:
//   Ceilings (Ceiling.Create is 2022+), Scope Boxes, Phase create/rename (Document.Phases is read-only),
//   Design Option activation (no setter exists in the assembly), workset DELETE (2022+).
// And the ones that need a file this script cannot invent: RVT links, a CAD import, a PDF printer,
// a flip-capable family, a sleeve family. Those stay manual.
//
// SAFETY: this CREATES elements. It is for a throwaway scratch model, never a project. It refuses to run
//         unless you confirm that explicitly, and refuses again if the model looks real (more than
//         maxExistingElements model elements) unless you override. Everything runs inside ONE
//         TransactionGroup, so any failure rolls the whole thing back rather than leaving half a fixture.
//
// STATUS: NOT YET RUN — written 2026-08-04 without Revit. Most API calls are copied from a fragment in
//         this library that already uses them: Duct.Create (creators/create-duct.cs), DuctInsulation.
//         Create (actions/structural-changes/action-add-remove-insulation.cs), Workset.Create
//         (creators/create-workset.cs), NewGroup (actions/structural-changes/action-group-elements.cs).
//         THREE calls have no prior use anywhere in this library and are the least proven part:
//           - AssemblyInstance.Create  (the assembly fragment only READS assemblies, never made one)
//           - Document.EnableWorksharing  (zero prior use — and the hardest to undo if wrong)
//           - GroupType.Name setter
//         Run the groups one at a time rather than all at once the first time, and leave
//         makeWorksharing off until the rest is proven.
// SOURCE:  ../../knowledge/brain-log.md (the fixture-blocked open items this exists to clear)
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool iConfirmThisIsAScratchModel = false;  // MUST be true. Nothing runs otherwise.
int  maxExistingElements         = 50;     // refuse if the model already holds more than this
bool overrideSizeGuard           = false;  // set true only if you really mean it on a busy model

bool makeDucts       = true;   // 6 ducts — the carrier for the insulation/assembly/group fixtures
bool makeInsulation  = true;   // insulation on 2 ducts
bool makeAssembly    = true;   // an AssemblyInstance from 2 ducts
bool makeGroup       = true;   // a model Group from 2 ducts

// Worksharing is a ONE-WAY door: a workshared file cannot be simply un-workshared, and Revit will want
// to save it as a central. Off by default on purpose. Only switch it on for a model you are happy to
// throw away afterwards.
bool makeWorksharing = false;
string worksetNameA  = "AJ Test Workset A";
string worksetNameB  = "AJ Test Workset B";

double ductLengthMm  = 3000;   // each test duct
double ductSpacingMm = 1500;   // gap between parallel ducts
double insulationThicknessMm = 25;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
Func<double, double> mm = v => v / 304.8;

sb.AppendLine("BUILD TEST FIXTURES");
sb.AppendLine("===================");

if (!iConfirmThisIsAScratchModel)
{
    sb.AppendLine("REFUSED — iConfirmThisIsAScratchModel is false.");
    sb.AppendLine("This script creates elements. Set it to true ONLY on a throwaway model, never a project.");
    return sb.ToString();
}

// Cheap sanity read before writing anything: a real project should not be fixtured over by accident.
int existingCount = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_DuctCurves).WhereElementIsNotElementType().ToElementIds().Count
    + new FilteredElementCollector(Document).OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElementIds().Count
    + new FilteredElementCollector(Document).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType().ToElementIds().Count;

if (existingCount > maxExistingElements && !overrideSizeGuard)
{
    sb.AppendLine($"REFUSED — this model already holds {existingCount} ducts/walls/family instances, more than");
    sb.AppendLine($"maxExistingElements ({maxExistingElements}). That does not look like an empty scratch model.");
    sb.AppendLine("Set overrideSizeGuard = true only if you are certain.");
    return sb.ToString();
}

var level = new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>()
    .OrderBy(l => l.Elevation).FirstOrDefault();
if (level == null)
{
    sb.AppendLine("REFUSED — no Level in this document. Even an empty scratch model needs one level.");
    return sb.ToString();
}
sb.AppendLine($"Model: {Document.Title} | Level: {level.Name} | existing elements counted: {existingCount}");
sb.AppendLine();

var createdDucts = new List<Element>();
int made = 0, skipped = 0;

// ---- 0. WORKSHARING — deliberately OUTSIDE the TransactionGroup below --------------------------
// Document.EnableWorksharing is a document-level operation, not a model edit: it cannot be called with
// a transaction open, and it cannot be rolled back. Putting it inside the group would either throw or
// promise an undo that does not exist. It runs first, on its own, and says plainly that it is one-way.
if (makeWorksharing)
{
    if (Document.IsWorkshared)
    {
        sb.AppendLine("REUSE worksharing — already workshared.");
    }
    else
    {
        Document.EnableWorksharing("Shared Levels and Grids", "Workset1");
        sb.AppendLine("MADE  worksharing — enabled. ONE-WAY: this file cannot simply be un-workshared,");
        sb.AppendLine("                    and Revit will now want it saved as a central file.");
    }
    using (var tws = new Transaction(Document, "AJ Tools - Fixture Worksets"))
    {
        tws.Start();
        try
        {
            foreach (var wsName in new[] { worksetNameA, worksetNameB })
            {
                if (WorksetTable.IsWorksetNameUnique(Document, wsName))
                {
                    var ws = Workset.Create(Document, wsName);
                    sb.AppendLine($"MADE  workset     — '{wsName}' (Id {ws.Id})");
                }
                else sb.AppendLine($"REUSE workset     — '{wsName}' already exists");
            }
            tws.Commit();
            sb.AppendLine("                    unblocks action-set-workset, action-rename-workset, filter-by-workset,");
            sb.AppendLine("                    action-report-element-ownership, command-sync-with-central");
            made++;
        }
        catch (Exception ex)
        {
            tws.RollBack();
            sb.AppendLine($"FAILED workset    — rolled back. Reason: {ex.Message}");
            skipped++;
        }
    }
}

using (var group = new TransactionGroup(Document, "AJ Tools - Build Test Fixtures"))
{
    group.Start();
    try
    {
        // ---- 1. DUCTS — the carrier every other fixture below needs ------------------------------
        if (makeDucts)
        {
            var sysType = new FilteredElementCollector(Document)
                .OfClass(typeof(Autodesk.Revit.DB.Mechanical.MechanicalSystemType))
                .Cast<Autodesk.Revit.DB.Mechanical.MechanicalSystemType>().FirstOrDefault();
            var ductType = new FilteredElementCollector(Document)
                .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctType))
                .Cast<Autodesk.Revit.DB.Mechanical.DuctType>().FirstOrDefault();

            if (sysType == null || ductType == null)
            {
                sb.AppendLine("SKIP  ducts       — no MechanicalSystemType or DuctType in this document.");
                sb.AppendLine("                    Open a MECHANICAL template, or load a duct family, then re-run.");
                skipped++;
            }
            else
            {
                using (var t = new Transaction(Document, "AJ Tools - Fixture Ducts"))
                {
                    t.Start();
                    for (int i = 0; i < 6; i++)
                    {
                        var y = mm(i * ductSpacingMm);
                        var p1 = new XYZ(0, y, mm(3000));
                        var p2 = new XYZ(mm(ductLengthMm), y, mm(3000));
                        var d = Autodesk.Revit.DB.Mechanical.Duct.Create(
                            Document, sysType.Id, ductType.Id, level.Id, p1, p2);
                        if (d != null) createdDucts.Add(d);
                    }
                    t.Commit();
                }
                sb.AppendLine($"MADE  ducts       — {createdDucts.Count}: {string.Join(", ", createdDucts.Select(d => d.Id))}");
                made++;
            }
        }
        else
        {
            // Reuse whatever is already there, so the fixture groups below can still run.
            createdDucts = new FilteredElementCollector(Document)
                .OfCategory(BuiltInCategory.OST_DuctCurves).WhereElementIsNotElementType().ToList();
            sb.AppendLine($"REUSE ducts       — {createdDucts.Count} already in the model");
        }

        // ---- 2. INSULATION — unblocks three fragments ---------------------------------------------
        if (makeInsulation)
        {
            var insType = new FilteredElementCollector(Document)
                .OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctInsulationType))
                .Cast<ElementType>().FirstOrDefault();
            if (insType == null)
            {
                sb.AppendLine("SKIP  insulation  — no DuctInsulationType in this document (load one, then re-run).");
                skipped++;
            }
            else if (createdDucts.Count < 2)
            {
                sb.AppendLine("SKIP  insulation  — needs at least 2 ducts.");
                skipped++;
            }
            else
            {
                using (var t = new Transaction(Document, "AJ Tools - Fixture Insulation"))
                {
                    t.Start();
                    foreach (var d in createdDucts.Take(2))
                    {
                        Autodesk.Revit.DB.Mechanical.DuctInsulation.Create(
                            Document, d.Id, insType.Id, mm(insulationThicknessMm));
                    }
                    t.Commit();
                }
                sb.AppendLine($"MADE  insulation  — {insulationThicknessMm}mm on ducts {string.Join(", ", createdDucts.Take(2).Select(d => d.Id))}");
                sb.AppendLine("                    unblocks action-add-remove-insulation, filter-by-insulation-status, filter-by-insulation-type");
                made++;
            }
        }

        // ---- 3. ASSEMBLY — unblocks filter-by-assembly ---------------------------------------------
        if (makeAssembly)
        {
            if (createdDucts.Count < 4)
            {
                sb.AppendLine("SKIP  assembly    — needs at least 4 ducts (2 are used by insulation).");
                skipped++;
            }
            else
            {
                var members = createdDucts.Skip(2).Take(2).Select(d => d.Id).ToList();
                using (var t = new Transaction(Document, "AJ Tools - Fixture Assembly"))
                {
                    t.Start();
                    var naming = Document.GetElement(members[0]).Category.Id;
                    var asm = AssemblyInstance.Create(Document, members, naming);
                    t.Commit();
                    sb.AppendLine($"MADE  assembly    — Id {asm.Id} from ducts {string.Join(", ", members.Select(i => i))}");
                    sb.AppendLine("                    unblocks filter-by-assembly");
                }
                made++;
            }
        }

        // ---- 4. GROUP — unblocks filter-by-group / action-ungroup-elements -------------------------
        if (makeGroup)
        {
            if (createdDucts.Count < 6)
            {
                sb.AppendLine("SKIP  group       — needs at least 6 ducts (4 are used above).");
                skipped++;
            }
            else
            {
                var members = createdDucts.Skip(4).Take(2).Select(d => d.Id).ToList();
                using (var t = new Transaction(Document, "AJ Tools - Fixture Group"))
                {
                    t.Start();
                    var g = Document.Create.NewGroup(members);
                    g.GroupType.Name = "AJ Test Group";
                    t.Commit();
                    sb.AppendLine($"MADE  group       — Id {g.Id} from ducts {string.Join(", ", members.Select(i => i))}");
                    sb.AppendLine("                    unblocks filter-by-group, action-ungroup-elements");
                }
                made++;
            }
        }

        group.Assimilate();
    }
    catch (Exception ex)
    {
        group.RollBack();
        sb.AppendLine();
        sb.AppendLine($"FAILED — whole fixture build rolled back, model unchanged. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"{made} fixture group(s) built, {skipped} skipped.");
sb.AppendLine();
sb.AppendLine("NOT ATTEMPTED — settled answers, do not re-try:");
sb.AppendLine("  Ceilings (Ceiling.Create is 2022+), Scope Boxes, Phase create/rename (Document.Phases is");
sb.AppendLine("  read-only), Design Option activation (no setter exists), workset DELETE (API is 2022+).");
sb.AppendLine("STILL MANUAL — need a file this script cannot invent:");
sb.AppendLine("  an RVT link, a CAD import, a PDF printer driver, a flip-capable family, a sleeve family.");
sb.AppendLine();
sb.AppendLine("Next: re-run the fragments listed above against this model and record the real result in");
sb.AppendLine("scripts/README.md, replacing their BLOCKED marker with a verified one.");
return sb.ToString();
