// ============================================================
// FRAGMENT (filter) — filter-by-material.cs
// PURPOSE: Elements that use a specific Revit Material — compound-layer/structural material by default;
//          set includePaint = true to also catch paint overrides. Pairs naturally with
//          action-material-takeoff.cs (which reports material quantities once elements are picked some
//          other way) — this fragment is the reverse: pick elements BY material.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// Whole-category scan is required — Revit has no direct "material -> elements" collector, so this walks
// the category's elements and checks each one's GetMaterialIds(). Keep useCategoryFilter on to bound the
// scan; per ../../knowledge/live-model/core.md, an unfiltered whole-model version is unbounded.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string materialName = "Steel, Galvanized";
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
bool includePaint = false;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

var material = new FilteredElementCollector(Document)
    .OfClass(typeof(Material))
    .Cast<Material>()
    .FirstOrDefault(m => m.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));

if (material == null)
{
    sb.AppendLine($"Material '{materialName}' not found — check the exact Revit material name.");
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
                var ids = e.GetMaterialIds(includePaint);
                return ids != null && ids.Contains(material.Id);
            }
            catch { return false; }
        })
        .ToList();

    string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
    sb.AppendLine($"Filtered {elements.Count} element(s) using material '{material.Name}', category: {categoryLabel}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
