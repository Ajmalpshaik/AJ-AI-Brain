// ============================================================
// EXAMPLE — fully assembled, ready to run via run_csharp
// Purges every View Template not currently applied to any view.
// Assembled from: filters/filter-by-view-templates.cs   (usage = "unused")
//                + actions/structural-changes/action-delete-elements.cs
// This is what composing looks like in practice for a destructive job — copy this shape, but see the
// MANDATORY safety note below before running it for real. See ../README.md for the full explanation.
// ============================================================
// MANDATORY before running this for real: run just the FILTER section below (down through the summary
// sb.AppendLine call), add your own return sb.ToString(), read the real count and the actual template
// names back to the user, and get an explicit go-ahead — never chain straight into the delete
// unconfirmed, no matter how small the count looks. The bridge call itself also needs
// allowDestructive: true (it refuses Delete otherwise) — a second, independent gate, not a substitute
// for asking first. This file is the REFERENCE shape, not a script to run blind.

// ---- FILTER: view templates, unused only ----
string nameContains = ""; // "" = every unused template; else narrow by a name substring first
string usage = "unused";

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfClass(typeof(View))
    .Cast<View>()
    .Where(v => v.IsTemplate)
    .AsEnumerable();

if (!string.IsNullOrEmpty(nameContains))
{
    query = query.Where(v => v.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

var usedTemplateIds = new HashSet<ElementId>(
    new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
        .Select(v => v.ViewTemplateId));

query = query.Where(v => !usedTemplateIds.Contains(v.Id));

List<Element> elements = query.OrderBy(v => v.Name).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} View Template(s), usage: {usage}" + (string.IsNullOrEmpty(nameContains) ? "" : $", name contains \"{nameContains}\"") + ".");

// ---- ACTION: delete ----
int deleted = 0, skipped = 0;
var failures = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Purge Unused View Templates"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            try
            {
                if (!Document.GetElement(e.Id).IsValidObject) { skipped++; continue; }
                Document.Delete(e.Id);
                deleted++;
            }
            catch (Exception exOne)
            {
                skipped++;
                failures.Add($"'{e.Name}' (Id {e.Id.IntegerValue}): {exOne.Message}");
            }
        }
        t.Commit();
        sb.AppendLine($"Deleted {deleted} unused View Template(s), skipped {skipped}.");
        if (failures.Count > 0)
            sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) +
                (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to purge — rolled back, nothing changed. Reason: {ex.Message}");
    }
}

// ---- FINAL RETURN (only the last fragment in a composed script needs this) ----
return sb.ToString();
