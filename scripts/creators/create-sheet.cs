// ============================================================
// FRAGMENT (creator) — create-sheet.cs
// PURPOSE: Create one or more new sheets with a chosen title block, setting sheet number and name.
// PRODUCES: elements (List<Element>) — the newly created ViewSheet(s), sb (StringBuilder)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string titleBlockFamilyTypeName = null; // null = use the first available title block type in the project

// MODE A — explicit list. Use when the numbers/names don't follow one rule.
var sheetsToCreate = new List<(string number, string name)> {
    ("A-101", "New Sheet")
};

// MODE B — sequential run. Set sequenceCount > 0 to use this INSTEAD of the list above.
// For a drawing series where the number runs and the name counts: typing 26 tuples into MODE A by hand is
// where transcription errors come from.
// ASK FOR EVERY VALUE BELOW, EVERY TIME — count, prefix, start number, padding and name text are per-job
// inputs the user states fresh. The placeholders are deliberately fake so an unfilled one is obvious in the
// created sheets rather than silently plausible. Never carry a past job's series across.
int    sequenceCount   = 0;             // how many sheets — 0 = use MODE A instead — ASK
string numberPrefix    = "XXX-000-";    // text before the running number — ASK
int    numberStart     = 1;             // FIRST number, as an integer — ASK
int    numberPadding   = 3;             // digits, zero-padded: padding 6 turns 1026 into "001026" — ASK
string numberSuffix    = "";            // text after the running number, if any
string namePrefix      = "SHEET ";      // sheet NAME text before the count — ASK
int    namePadding     = 2;             // 1 -> "01"
// Names always count 1, 2, 3... from the first sheet, independent of the number series.
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

if (sequenceCount > 0)
{
    sheetsToCreate = new List<(string number, string name)>();
    for (int i = 0; i < sequenceCount; i++)
    {
        string num = numberPrefix + (numberStart + i).ToString("D" + numberPadding) + numberSuffix;
        string nm  = namePrefix + (i + 1).ToString("D" + namePadding);
        sheetsToCreate.Add((num, nm));
    }
}

FamilySymbol titleBlockType;
if (string.IsNullOrEmpty(titleBlockFamilyTypeName))
{
    titleBlockType = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks)
        .WhereElementIsElementType()
        .Cast<FamilySymbol>()
        .FirstOrDefault();
}
else
{
    titleBlockType = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_TitleBlocks)
        .WhereElementIsElementType()
        .Cast<FamilySymbol>()
        .FirstOrDefault(f => f.Name.Equals(titleBlockFamilyTypeName, StringComparison.OrdinalIgnoreCase));
}

if (titleBlockType == null)
{
    sb.AppendLine("No title block type found" + (string.IsNullOrEmpty(titleBlockFamilyTypeName) ? " in the project." : $" named '{titleBlockFamilyTypeName}'."));
}
else
{
    int created = 0, skipped = 0;
    using (var t = new Transaction(Document, "AJ Tools - Create Sheets"))
    {
        t.Start();
        try
        {
            if (!titleBlockType.IsActive) { titleBlockType.Activate(); Document.Regenerate(); }

            foreach (var pair in sheetsToCreate)
            {
                var existing = new FilteredElementCollector(Document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                    .FirstOrDefault(s => s.SheetNumber.Equals(pair.number, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { skipped++; continue; }

                var sheet = ViewSheet.Create(Document, titleBlockType.Id);
                sheet.SheetNumber = pair.number;
                sheet.Name = pair.name;
                elements.Add(sheet);
                created++;
            }
            t.Commit();
            sb.AppendLine($"Created {created} sheet(s), skipped {skipped} (sheet number already exists).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to create sheets — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with an action fragment below (e.g. action-report-parameters.cs), or add return sb.ToString(); to finish ----
