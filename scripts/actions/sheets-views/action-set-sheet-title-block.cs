// ============================================================
// FRAGMENT (action) — action-set-sheet-title-block.cs
// PURPOSE: Change the title block on each ViewSheet in `elements` to a different named Type — "update the
//          title block on this sheet to X" / bulk-swap every sheet's title block at once. If the target
//          type belongs to the SAME family as what's already on the sheet, this is a cheap in-place
//          `ChangeTypeId` (position/instance parameters kept). If it's a DIFFERENT family, the existing
//          title block instance is deleted and a new one placed at the sheet origin (Revit always places
//          a title block at (0,0,0) on its sheet) — any instance-level values typed into the OLD title
//          block (revision-schedule position edits, per-sheet overrides) are lost in that case; only
//          Sheet-level parameters (Sheet Number/Name, issue date, etc.) are unaffected since those live on
//          the ViewSheet itself, not the title block instance.
// ASSUMES: elements (List<Element>, each really a ViewSheet — e.g. from filters/by-view-and-sheet/filter-by-sheets.cs) and
//          sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: run action-report-sheet-title-blocks.cs first to see current state/family before a cross-family
//         swap on a batch of sheets, per the explorer-first discipline.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetFamilyNameContains = ""; // "" = not checked, match by type name alone
string targetTypeName = "New Title Block Type";
// ---- END INPUTS ----

var targetType = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_TitleBlocks)
    .WhereElementIsElementType()
    .Cast<FamilySymbol>()
    .FirstOrDefault(fs =>
        fs.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase) &&
        (string.IsNullOrEmpty(targetFamilyNameContains) || fs.Family.Name.IndexOf(targetFamilyNameContains, StringComparison.OrdinalIgnoreCase) >= 0));

if (targetType == null)
{
    var available = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>();
    sb.AppendLine($"Title Block type '{targetTypeName}' not found" + (string.IsNullOrEmpty(targetFamilyNameContains) ? "" : $" in family containing '{targetFamilyNameContains}'") +
        $". Available: {string.Join(", ", available.Select(fs => $"{fs.Family.Name} : {fs.Name}"))}");
}
else
{
    int swappedInPlace = 0, replaced = 0, skipped = 0;
    var failures = new List<string>();

    using (var t = new Transaction(Document, "AJ Tools - Set Sheet Title Block"))
    {
        t.Start();
        try
        {
            if (!targetType.IsActive) { targetType.Activate(); Document.Regenerate(); }

            var allTitleBlocks = new FilteredElementCollector(Document)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var el in elements)
            {
                var sheet = el as ViewSheet;
                if (sheet == null) { skipped++; continue; }

                var existing = allTitleBlocks.FirstOrDefault(tb => tb.OwnerViewId == sheet.Id);

                try
                {
                    if (existing != null && existing.Symbol.Family.Id == targetType.Family.Id)
                    {
                        existing.Symbol = targetType;
                        swappedInPlace++;
                    }
                    else
                    {
                        if (existing != null) Document.Delete(existing.Id);
                        Document.Create.NewFamilyInstance(XYZ.Zero, targetType, sheet);
                        replaced++;
                    }
                }
                catch (Exception exOne)
                {
                    skipped++;
                    failures.Add($"'{sheet.SheetNumber}': {exOne.Message}");
                }
            }
            t.Commit();
            sb.AppendLine($"Set title block '{targetType.Family.Name} : {targetType.Name}' — {swappedInPlace} sheet(s) swapped in place (same family), {replaced} sheet(s) replaced (different family/none before), skipped {skipped}.");
            if (failures.Count > 0)
                sb.AppendLine("Skipped detail: " + string.Join("; ", failures.Take(10)) + (failures.Count > 10 ? $" ... and {failures.Count - 10} more" : ""));
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to set title block — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
