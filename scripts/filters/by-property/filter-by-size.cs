// ============================================================
// FRAGMENT (filter) — filter-by-size.cs
// PURPOSE: Category narrowed by SIZE, handling round (Diameter) and rectangular (Width x Height) MEP
//          sizing together in one pass — "give me the ø150 OR 300x200 ones" without knowing ahead of
//          time which candidates are round vs. rectangular. filter-by-category-and-numeric-param.cs
//          already covers a SINGLE dimension (just Diameter, or just Width) — this fragment's real
//          value is the combined round-or-rectangular check, plus an optional simple text match against
//          Revit's own calculated "Size" parameter (e.g. "300x200mm", "ø150mm") when the exact number
//          isn't critical.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// ============================================================
// If sizeTextContains is set, it wins and the numeric inputs below are ignored — simplest path when a
// plain substring match on the calculated "Size" string is enough.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves;
string sizeTextContains = ""; // "" = use the numeric inputs below instead; else e.g. "300x200", "150" — matches the calculated "Size" parameter text
double diameterMm = 0; // 0 = not checked — set for round ducts/pipes
double widthMm = 0; // 0 = not checked — set together with heightMm for rectangular
double heightMm = 0; // 0 = not checked
double toleranceMm = 1;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var collector = new FilteredElementCollector(Document).OfCategory(targetCategory).WhereElementIsNotElementType();

List<Element> elements;

if (!string.IsNullOrEmpty(sizeTextContains))
{
    elements = collector.Where(e =>
    {
        var p = e.LookupParameter("Size");
        var text = p?.AsString() ?? p?.AsValueString() ?? "";
        return text.IndexOf(sizeTextContains, StringComparison.OrdinalIgnoreCase) >= 0;
    }).ToList();
}
else
{
    Func<Element, bool> matchesDiameter = e =>
    {
        if (diameterMm <= 0) return false;
        var p = e.LookupParameter("Diameter");
        if (p == null || p.StorageType != StorageType.Double) return false;
        double vMm = p.AsDouble() * 304.8;
        return Math.Abs(vMm - diameterMm) <= toleranceMm;
    };

    Func<Element, bool> matchesRectangular = e =>
    {
        if (widthMm <= 0 && heightMm <= 0) return false;
        var pw = e.LookupParameter("Width");
        var ph = e.LookupParameter("Height");
        bool widthOk = widthMm <= 0 || (pw != null && pw.StorageType == StorageType.Double &&
            Math.Abs(pw.AsDouble() * 304.8 - widthMm) <= toleranceMm);
        bool heightOk = heightMm <= 0 || (ph != null && ph.StorageType == StorageType.Double &&
            Math.Abs(ph.AsDouble() * 304.8 - heightMm) <= toleranceMm);
        bool anyChecked = (widthMm > 0 && pw != null) || (heightMm > 0 && ph != null);
        return anyChecked && widthOk && heightOk;
    };

    elements = collector.Where(e => matchesDiameter(e) || matchesRectangular(e)).ToList();
}

var parts = new List<string>();
if (!string.IsNullOrEmpty(sizeTextContains)) parts.Add($"Size text contains \"{sizeTextContains}\"");
if (diameterMm > 0) parts.Add($"Diameter {diameterMm}mm");
if (widthMm > 0) parts.Add($"Width {widthMm}mm");
if (heightMm > 0) parts.Add($"Height {heightMm}mm");
sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}, {string.Join(" / ", parts)}.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
