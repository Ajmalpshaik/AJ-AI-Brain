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
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
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
                sb.AppendLine($"Loaded family '{fam.Name}' (Id {fam.Id.IntegerValue}) — {symbols.Count} type(s): {string.Join(", ", symbols.Select(s => $"'{s.Name}' (Id {s.Id.IntegerValue})"))}");
            }
            else if (fam == null && !ok)
            {
                failed++;
                sb.AppendLine($"NOT loaded: '{System.IO.Path.GetFileNameWithoutExtension(path)}' — most likely a family with this name already exists in the project (rename/remove it first if replacing is really wanted).");
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
