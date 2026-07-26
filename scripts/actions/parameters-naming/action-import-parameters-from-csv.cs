// ============================================================
// FRAGMENT (action) — action-import-parameters-from-csv.cs
// PURPOSE: Read a CSV (ElementId first column + one column per parameter — the exact shape
//          action-export-parameters-to-csv.cs writes) and set those parameter values back onto the model.
//          The write half of the Excel round-trip: the agent converts the user's edited .xlsx to CSV
//          outside Revit first.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — every row resolves its own element by
//          Id from the file, so pair it with any cheap filter (or none, if composed standalone add
//          `var sb = new System.Text.StringBuilder();` first).
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: this BULK-WRITES parameter values — explorer-first rule applies: report the row count to the
//         user and get a go-ahead before running on a big file.
// GOTCHA: numeric parameters are set via SetValueString (display units, mm-side — same convention the
//         export uses); read-only parameters and unknown Ids are skipped and REPORTED, never silent.
// GOTCHA: a "Name" column (from includeElementName on export) is ignored on import — it's a read-back
//         aid, not a writable target here; rename via action-rename-element.cs instead.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the tool-gap backlog.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string csvPath = @"C:\Temp\aj-parameters.csv";  // input file — ElementId column + parameter columns
// ---- END INPUTS ----

// minimal CSV parser that honors "quoted, fields" (the quoting the export fragment writes)
Func<string, List<string>> parseCsvLine = line =>
{
    var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool inQ = false;
    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (inQ)
        {
            if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
            else if (ch == '"') inQ = false;
            else cur.Append(ch);
        }
        else if (ch == '"') inQ = true;
        else if (ch == ',') { cells.Add(cur.ToString()); cur.Clear(); }
        else cur.Append(ch);
    }
    cells.Add(cur.ToString());
    return cells;
};

if (!System.IO.File.Exists(csvPath))
{
    sb.AppendLine($"CSV file not found: '{csvPath}'.");
}
else
{
    var lines = System.IO.File.ReadAllLines(csvPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
    if (lines.Count < 2) { sb.AppendLine($"'{csvPath}' has no data rows."); }
    else
    {
        var header = parseCsvLine(lines[0]);
        if (header.Count < 2 || header[0] != "ElementId")
        {
            sb.AppendLine($"Unexpected header — first column must be 'ElementId'. Got: {string.Join(", ", header)}");
        }
        else
        {
            var paramCols = new List<int>();
            for (int c = 1; c < header.Count; c++)
                if (header[c] != "Name") paramCols.Add(c); // "Name" column is export read-back aid only

            int setCount = 0, skipped = 0;
            var problems = new List<string>();

            using (var t = new Transaction(Document, "AJ Tools - Import Parameters From CSV"))
            {
                t.Start();
                try
                {
                    foreach (var line in lines.Skip(1))
                    {
                        var cells = parseCsvLine(line);
                        int idInt;
                        if (!int.TryParse(cells[0], out idInt)) { skipped++; problems.Add($"bad Id '{cells[0]}'"); continue; }
                        var el = Document.GetElement(new ElementId(idInt));
                        if (el == null) { skipped++; problems.Add($"Id {idInt} not in model"); continue; }

                        foreach (int c in paramCols)
                        {
                            if (c >= cells.Count) continue;
                            string pname = header[c], val = cells[c];
                            var p = el.LookupParameter(pname);
                            if (p == null)
                            {
                                var typeEl = Document.GetElement(el.GetTypeId());
                                p = typeEl?.LookupParameter(pname);
                            }
                            if (p == null) { skipped++; problems.Add($"Id {idInt}: no parameter '{pname}'"); continue; }
                            if (p.IsReadOnly) { skipped++; problems.Add($"Id {idInt}: '{pname}' is read-only"); continue; }

                            bool ok = false;
                            try
                            {
                                switch (p.StorageType)
                                {
                                    case StorageType.String: ok = p.Set(val); break;
                                    case StorageType.Integer: int iv; ok = int.TryParse(val, out iv) && p.Set(iv); break;
                                    default: ok = p.SetValueString(val); break; // Double + ElementId-backed: display-unit path
                                }
                            }
                            catch (Exception exSet) { problems.Add($"Id {idInt}: '{pname}' — {exSet.Message}"); }
                            if (ok) setCount++; else skipped++;
                        }
                    }
                    t.Commit();
                    sb.AppendLine($"Imported '{csvPath}': {setCount} value(s) set, {skipped} skipped, across {lines.Count - 1} row(s).");
                    if (problems.Count > 0)
                        sb.AppendLine("Skipped detail: " + string.Join("; ", problems.Take(25)) + (problems.Count > 25 ? $"; ... +{problems.Count - 25} more" : ""));
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"FAILED mid-import — rolled back, NO values were changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
