// ============================================================
// FRAGMENT (creator) — create-workset-3d-views.cs
// PURPOSE: Create one isometric 3D view PER USER WORKSET, each showing only its own workset and
//          hiding all the others — the coordination set-up job: "give me a 3D view for each workset so
//          I can see what is on it". One view named after each workset.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// GOTCHA: THE MODEL MUST BE WORKSHARED. `doc.IsWorkshared` is checked first; on a non-workshared file
//         there are no user worksets at all and the fragment stops with that message rather than
//         creating nothing and looking broken.
// GOTCHA: ONLY USER WORKSETS. `WorksetKind.UserWorkset` excludes the standard/view/family worksets
//         Revit maintains internally — those are not what anyone means by "a workset".
// GOTCHA: A VIEW NAME MUST BE UNIQUE IN THE DOCUMENT and Revit throws ArgumentException on a clash.
//         Existing view names are collected up front and a workset whose name is already taken is
//         SKIPPED, so re-running this is safe and adds only what is missing.
// GOTCHA: SETTING THE NAME CAN THROW AFTER THE VIEW IS ALREADY CREATED, which would leave an unnamed
//         stray 3D view behind. Every failure path deletes the half-made view before moving on.
// GOTCHA: `View3D.CreateIsometric` needs a 3D `ViewFamilyType`; a project with none cannot make one.
//         That is checked before the loop rather than failing per workset.
// NOTE: visibility is set per view with `SetWorksetVisibility` — Visible for its own workset, Hidden
//       for every other user workset. It does not touch the standard worksets.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's 3D Views as per Workset.
//   Compile-checked on 2020/2024/2027. This Brain's test model is NOT workshared, so the real path
//   here is unproven for the same reason `actions/parameters-naming/action-set-workset.cs` is.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string namePrefix = "";              // e.g. "WS - " to get "WS - Mechanical"; "" = the workset name alone
bool dryRun = true;                  // true = list what would be created; false = actually create
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is NOT workshared, so it has no user worksets. Nothing to create.");
    return sb.ToString();
}

var userWorksets = new FilteredWorksetCollector(Document)
    .OfKind(WorksetKind.UserWorkset)
    .ToWorksets()
    .OrderBy(w => w.Name)
    .ToList();

if (userWorksets.Count == 0)
{
    sb.AppendLine("No user worksets found in this model.");
    return sb.ToString();
}

var threeDType = new FilteredElementCollector(Document)
    .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
    .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

if (threeDType == null)
{
    sb.AppendLine("No 3D view family type exists in this project — a 3D view cannot be created.");
    return sb.ToString();
}

// A view name must be unique in the document, and a clash THROWS — so know them up front.
var existingNames = new HashSet<string>(
    new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => v != null).Select(v => v.Name), StringComparer.OrdinalIgnoreCase);

int created = 0, skippedExisting = 0, failed = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Create 3D Views as per Workset"))
{
    t.Start();
    try
    {
        foreach (var ws in userWorksets)
        {
            string viewName = namePrefix + ws.Name;

            if (existingNames.Contains(viewName)) { skippedExisting++; continue; }

            if (dryRun)
            {
                created++;
                existingNames.Add(viewName);
                if (rows.Count < 40) rows.Add(viewName + " | " + ws.Name);
                continue;
            }

            View3D v = null;
            try
            {
                v = View3D.CreateIsometric(Document, threeDType.Id);
                v.Name = viewName;   // can throw AFTER creation — the catch below cleans up

                foreach (var other in userWorksets)
                    v.SetWorksetVisibility(other.Id,
                        other.Id == ws.Id ? WorksetVisibility.Visible : WorksetVisibility.Hidden);

                existingNames.Add(viewName);
                created++;
                if (rows.Count < 40) rows.Add(viewName + " | " + ws.Name);
            }
            catch
            {
                // Never leave a half-made, unnamed 3D view behind.
                if (v != null) { try { Document.Delete(v.Id); } catch { } }
                failed++;
            }
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing created. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing created. " : "") +
              $"{userWorksets.Count} user workset(s): " + (dryRun ? "would create " : "created ") + created +
              " 3D view(s)" +
              (skippedExisting > 0 ? $", skipped {skippedExisting} (a view of that name already exists)" : "") +
              (failed > 0 ? $", {failed} failed" : "") + ".");

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("View name | Workset shown");
    sb.AppendLine("--- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (created > rows.Count) sb.AppendLine("... and " + (created - rows.Count) + " more");
}

return sb.ToString();
