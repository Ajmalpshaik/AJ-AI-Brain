// ============================================================
// FRAGMENT (creator) — create-workset.cs
// PURPOSE: Create one or more new user Worksets — feeds action-set-workset.cs (assign elements onto a
//          workset that didn't exist yet).
// PRODUCES: elements (List<Element>) — always EMPTY: Workset is not an Element in the Revit API (it's a
//           separate lightweight object, no ElementId of its own), so there's nothing for an action
//           fragment to consume afterward. This fragment is usually run standalone; report the created
//           Workset names/Ids directly from `sb` instead of chaining.
// NOT STANDALONE (in spirit) — see scripts/README.md; in practice this is normally the whole script.
// GOTCHA: only valid on a workshared (central/local) model, and only takes effect this way when NOT
//         editing from a non-workshared/detached state — reports plainly instead of throwing if worksharing
//         isn't on.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<string> worksetNames = new List<string> { "New Workset" };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>(); // always empty — see PRODUCES note above

if (!Document.IsWorkshared)
{
    sb.AppendLine("This document is not workshared — enable worksharing first (Collaborate > Manage Collaboration), Worksets can't be created otherwise.");
}
else
{
    int created = 0, skipped = 0;
    var createdNames = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Create Worksets"))
    {
        t.Start();
        try
        {
            var existingNames = new HashSet<string>(
                new FilteredWorksetCollector(Document).OfKind(WorksetKind.UserWorkset).Select(ws => ws.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in worksetNames)
            {
                if (existingNames.Contains(name)) { skipped++; continue; }
                var ws = Workset.Create(Document, name);
                createdNames.Add(ws.Name);
                existingNames.Add(ws.Name);
                created++;
            }
            t.Commit();
            sb.AppendLine($"Created {created} workset(s): {string.Join(", ", createdNames)}. Skipped {skipped} (name already exists).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create worksets — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- this fragment produces no `elements` to chain — add return sb.ToString(); to stop here ----
