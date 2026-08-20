// ============================================================
// FRAGMENT (creator) — load-family.cs
// PURPOSE: Load one or more .rfa family files from disk into the project — File > Load Family, scripted.
//          The missing first step before create-point-based-element.cs when the family isn't in the
//          model yet.
// PRODUCES: elements (List<Element>, every FamilySymbol/type of each newly loaded family — the things
//          placement fragments consume), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: if a family with the same name is ALREADY in the project, LoadFamily returns false — reported,
//         not overwritten. Overwriting an existing family's parameter values on purpose needs an
//         IFamilyLoadOptions implementation, deliberately NOT done here (silent overwrites are how office
//         content gets corrupted; rename or remove the old family first, on explicit request).
// GOTCHA: a freshly loaded FamilySymbol is NOT active until first placed — placement code must call
//         .Activate() + Regenerate before NewFamilyInstance (create-point-based-element.cs handles this).
// LIVE-VERIFIED 2026-08-14 — loaded M_Electrical Panel.rfa (3 types) and M_Outlet-Duplex.rfa (1 type)
//         from the stock US Metric library in one call; both read back as real FamilySymbols. The
//         not-found path was exercised too (wrong paths -> "NOT FOUND on disk", clean rollback).
// A PREDICTED BUG THAT IS NOT REAL — recorded so nobody "fixes" it again. The open-items list claimed the
//         already-loaded case (`ok == false`, `fam != null`) fell through both branches and printed
//         NOTHING. Measured on 2020: loading an already-present family returns **ok=False and fam=NULL**,
//         so it lands in branch 2 and IS reported correctly. The case said to trigger the gap does not
//         occur. A plain `else` is kept below anyway — it costs nothing and closes the theoretical hole —
//         but the fragment was never silent about a real family.
// GOTCHA: the stock library nests deeply. M_Electrical Panel.rfa is NOT in `...\US Metric\Electrical\`;
//         it is in `...\US Metric\Electrical\Architectural\Electrical Power\Distribution\`. Search with
//         SearchOption.AllDirectories for the real path rather than assuming the folder.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] rfaPaths = new[] { @"C:\Temp\families\MyFamily.rfa" }; // full paths to .rfa files
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Load Families"))
{
    t.Start();
    try
    {
        int loaded = 0, failed = 0;
        foreach (var path in rfaPaths)
        {
            if (!System.IO.File.Exists(path))
            {
                failed++;
                sb.AppendLine($"NOT FOUND on disk: '{path}'");
                continue;
            }
            Family fam = null;
            bool ok = false;
            try { ok = Document.LoadFamily(path, out fam); }
            catch (Exception exOne) { sb.AppendLine($"FAILED '{path}': {exOne.Message}"); }

            if (ok && fam != null)
            {
                loaded++;
                var symbols = fam.GetFamilySymbolIds().Select(id => Document.GetElement(id)).OfType<FamilySymbol>().ToList();
                elements.AddRange(symbols);
                sb.AppendLine($"Loaded family '{fam.Name}' (Id {fam.Id}) — {symbols.Count} type(s): {string.Join(", ", symbols.Select(s => $"'{s.Name}' (Id {s.Id})"))}");
            }
            else if (fam == null && !ok)
            {
                failed++;
                sb.AppendLine($"NOT loaded: '{System.IO.Path.GetFileNameWithoutExtension(path)}' — most likely a family with this name already exists in the project (rename/remove it first if replacing is really wanted).");
            }
            else
            {
                // Never observed on 2020 (already-loaded returns ok=False + fam=NULL, caught above), but a
                // mixed ok/fam result must not pass in silence — that is how a load looks successful and isn't.
                failed++;
                sb.AppendLine($"UNEXPECTED result for '{System.IO.Path.GetFileNameWithoutExtension(path)}': LoadFamily returned ok={ok} but fam={(fam == null ? "null" : "'" + fam.Name + "'")}. Treated as not loaded — check this family by hand.");
            }
        }

        if (loaded > 0) { t.Commit(); sb.AppendLine($"{loaded} famil{(loaded == 1 ? "y" : "ies")} loaded, {failed} failed/skipped."); }
        else { t.RollBack(); sb.AppendLine("Nothing was loaded — transaction rolled back."); }
    }
    catch (Exception ex)
    {
        try { t.RollBack(); } catch { }
        sb.AppendLine($"FAILED during family load — rolled back. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
