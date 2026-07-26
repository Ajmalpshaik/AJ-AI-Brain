// ============================================================
// FRAGMENT (action) — action-export-parameters-to-csv.cs
// PURPOSE: Write chosen parameter values of `elements` to a CSV file — ElementId first column always, one
//          row per element. The Revit-side half of the Excel round-trip: the agent converts this CSV to
//          .xlsx outside Revit (and back), action-import-parameters-from-csv.cs reads the edited file in.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only on the model; writes ONE file on
//          disk at csvPath (tell the user the path — writing a file still counts as an outward effect).
// GOTCHA: values are written via AsValueString (display units, mm-side) so the user edits the numbers
//         they recognize; the import fragment sets them back through SetValueString the same way.
// GOTCHA: fields containing comma/quote/newline are CSV-quoted properly; Excel opens it as-is.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string csvPath = @"C:\Temp\aj-parameters.csv";      // output file — folder must exist
string[] parameterNames = new[] { "Mark", "Comments" }; // columns, in order; instance param first, Type fallback
bool includeElementName = true;                     // add a read-only Name column after ElementId
// ---- END INPUTS ----

Func<string, string> csvQ = v =>
{
    v = v ?? "";
    return (v.Contains(",") || v.Contains("\"") || v.Contains("\n"))
        ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
};

Func<Element, string, string> readParam = (el, name) =>
{
    var p = el.LookupParameter(name);
    if (p == null)
    {
        var typeEl = Document.GetElement(el.GetTypeId());
        p = typeEl?.LookupParameter(name);
    }
    if (p == null || !p.HasValue) return "";
    string v = null;
    try { v = p.AsValueString(); } catch { }
    if (string.IsNullOrEmpty(v) && p.StorageType == StorageType.String) v = p.AsString();
    if (string.IsNullOrEmpty(v) && p.StorageType == StorageType.Integer) v = p.AsInteger().ToString();
    return v ?? "";
};

try
{
    var lines = new List<string>();
    var header = "ElementId" + (includeElementName ? ",Name" : "") + "," + string.Join(",", parameterNames.Select(csvQ));
    lines.Add(header);

    foreach (var el in elements)
    {
        var cells = new List<string> { el.Id.IntegerValue.ToString() };
        if (includeElementName) cells.Add(csvQ(el.Name));
        foreach (var pn in parameterNames) cells.Add(csvQ(readParam(el, pn)));
        lines.Add(string.Join(",", cells));
    }

    System.IO.File.WriteAllLines(csvPath, lines, System.Text.Encoding.UTF8);
    sb.AppendLine($"Exported {elements.Count} element(s) x {parameterNames.Length} parameter(s) to '{csvPath}'.");
}
catch (Exception ex)
{
    sb.AppendLine($"FAILED to export parameters to CSV. Reason: {ex.Message}");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
