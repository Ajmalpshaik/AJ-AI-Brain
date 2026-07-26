// ============================================================
// FRAGMENT (filter) — filter-by-types.cs
// PURPOSE: The TYPE elements themselves (FamilySymbol, or a system-family type like DuctType/PipeType/
//          WallType), matched by family name and/or type name — NOT the placed instances. Use this to
//          target a type that currently has ZERO instances placed (action-rename-family.cs and
//          action-duplicate-type.cs both resolve types FROM instances, so a type nothing is using yet is
//          otherwise unreachable) or for any report/rename job about types as such.
// PRODUCES: elements (List<Element>, each one really an ElementType — cast back with `as ElementType`, or
//           narrower, in the action), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctAccessory;
string familyNameContains = ""; // "" = not checked; only meaningful for Family-based categories (FamilySymbol)
string typeNameContains = "";   // "" = every type in the category/family
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var query = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsElementType()
    .Cast<ElementType>()
    .AsEnumerable();

if (!string.IsNullOrEmpty(familyNameContains))
{
    query = query.Where(t => (t is FamilySymbol fs ? fs.Family.Name : t.FamilyName)?.IndexOf(familyNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}
if (!string.IsNullOrEmpty(typeNameContains))
{
    query = query.Where(t => t.Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
}

List<Element> elements = query.OrderBy(t => t.Name).Cast<Element>().ToList();
sb.AppendLine($"Filtered {elements.Count} type(s) in category {targetCategory}" +
    (string.IsNullOrEmpty(familyNameContains) ? "" : $", family contains \"{familyNameContains}\"") +
    (string.IsNullOrEmpty(typeNameContains) ? "" : $", type name contains \"{typeNameContains}\"") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
