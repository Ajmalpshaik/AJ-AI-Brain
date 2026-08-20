// ============================================================
// FRAGMENT (filter) — filter-by-length.cs
// PURPOSE: Category narrowed by LENGTH (mm) vs. a comparison — the dedicated, discoverable version of
//          what filter-by-category-and-numeric-param.cs can already do with parameterName = "Length",
//          but bound directly to BuiltInParameter.CURVE_ELEM_LENGTH instead of a display-name guess —
//          works on any curve-based element (pipe, duct, wall, cable tray, structural framing, ...).
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
double valueMm = 1000;
// Comparison: "eq" (within toleranceMm), "gte", "lte", "between"
string comparison = "gte";
double valueMaxMm = 0; // only used when comparison == "between" — the upper bound
double toleranceMm = 1; // only used when comparison == "eq"
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

double valueFt = valueMm / 304.8;
double toleranceFt = toleranceMm / 304.8;
double valueMaxFt = valueMaxMm / 304.8;

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        var p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
        if (p == null || p.StorageType != StorageType.Double) return false;
        double v = p.AsDouble();
        switch (comparison)
        {
            case "gte": return v >= valueFt;
            case "lte": return v <= valueFt;
            case "between": return v >= valueFt && v <= valueMaxFt;
            default: return Math.Abs(v - valueFt) <= toleranceFt; // "eq"
        }
    })
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory} where Length {comparison} {valueMm}mm.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
