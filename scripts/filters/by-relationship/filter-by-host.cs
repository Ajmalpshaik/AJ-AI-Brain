// ============================================================
// FRAGMENT (filter) — filter-by-host.cs
// PURPOSE: Elements hosted on a specific parent — a true Revit Host relationship, not just physical
//          proximity. Covers two real host mechanisms: (a) FamilyInstance.Host, used by host-based
//          families (a door/window on a wall, a face-hosted fitting), and (b) HostElementId, used by
//          Duct/Pipe Insulation and Lining. NOTE: pipe/duct fittings joined into a run are connected via
//          Connectors, not a Host relationship — for "fittings on this pipe" use
//          recipes/trace-mep-circuits.cs or filter-by-element-intersection.cs instead; this fragment
//          returns nothing for them.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: not yet live-verified — confirm InsulationLiningBase resolves in this Revit version before
//         trusting bulk results.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int hostElementIdInt = 0; // the parent element — set explicitly
bool useCategoryFilter = false;
BuiltInCategory targetCategory = BuiltInCategory.OST_Doors;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

var hostElement = Document.GetElement(new ElementId(hostElementIdInt));
if (hostElement == null)
{
    sb.AppendLine($"Host element Id {hostElementIdInt} not found.");
}
else
{
    var collector = useCategoryFilter
        ? new FilteredElementCollector(Document).OfCategory(targetCategory)
        : new FilteredElementCollector(Document);

    elements = collector
        .WhereElementIsNotElementType()
        .Where(e =>
        {
            try
            {
                if (e is FamilyInstance fi && fi.Host != null && fi.Host.Id == hostElement.Id) return true;
                if (e is Autodesk.Revit.DB.InsulationLiningBase il && il.HostElementId == hostElement.Id) return true;
            }
            catch { }
            return false;
        })
        .ToList();

    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    sb.AppendLine($"Filtered {elements.Count} element(s) hosted on '{hostElement.Name}' (Id {hostElement.Id}), candidates: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
