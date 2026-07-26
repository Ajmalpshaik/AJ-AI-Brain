// ============================================================
// FRAGMENT (filter) — filter-by-family-type.cs
// PURPOSE: A specific Type (FamilySymbol) inside a Family, matched by name — e.g. one particular pipe
//          fitting size/type, not every type the family offers. Narrower than
//          filter-by-category-and-family.cs (which matches the whole family regardless of type) and
//          doesn't require knowing the type's ElementId up front the way that fragment's familySymbolId
//          shortcut does.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool useCategoryFilter = true;
BuiltInCategory targetCategory = BuiltInCategory.OST_PipeFitting;
string familyName = "Elbow - Generic";
string typeName = "50mm";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var collector = useCategoryFilter
    ? new FilteredElementCollector(Document).OfCategory(targetCategory)
    : new FilteredElementCollector(Document).OfClass(typeof(FamilyInstance));

List<Element> elements = collector
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .Where(fi => fi.Symbol != null
        && string.Equals(fi.Symbol.Family?.Name, familyName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(fi.Symbol.Name, typeName, StringComparison.OrdinalIgnoreCase))
    .Cast<Element>()
    .ToList();

string categoryLabel = useCategoryFilter ? targetCategory.ToString() : "all categories";
sb.AppendLine($"Filtered {elements.Count} element(s), family '{familyName}', type '{typeName}', category: {categoryLabel}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
