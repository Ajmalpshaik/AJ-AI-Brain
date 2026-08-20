// ============================================================
// FRAGMENT (action) — action-report-sheet-title-blocks.cs
// PURPOSE: Report which title block (Family + Type) is currently placed on each ViewSheet in `elements`
//          — "what title block is on this sheet" / "what title blocks do we have across all sheets".
//          Read-only, no transaction needed. The "check before you act" companion to
//          action-set-sheet-title-block.cs.
// ASSUMES: elements (List<Element>, each really a ViewSheet — e.g. from filters/by-view-and-sheet/filter-by-sheets.cs) and
//          sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

var allTitleBlocks = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_TitleBlocks)
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>()
    .ToList();

var rows = new List<(string sheetNumber, string sheetName, string family, string type, ElementId tbId)>();
int missing = 0;

foreach (var el in elements)
{
    var sheet = el as ViewSheet;
    if (sheet == null) continue;

    var tb = allTitleBlocks.FirstOrDefault(t => t.OwnerViewId == sheet.Id);
    if (tb == null)
    {
        rows.Add((sheet.SheetNumber, sheet.Name, "(none)", "(none)", null));
        missing++;
    }
    else
    {
        rows.Add((sheet.SheetNumber, sheet.Name, tb.Symbol.Family.Name, tb.Symbol.Name, tb.Id));
    }
}

sb.AppendLine($"Title block report for {rows.Count} sheet(s), {missing} with NO title block placed:");
sb.AppendLine("Sheet Number | Sheet Name | Title Block Family | Title Block Type | Instance Id");
sb.AppendLine("--- | --- | --- | --- | ---");
foreach (var r in rows.OrderBy(r => r.sheetNumber))
    sb.AppendLine($"{r.sheetNumber} | {r.sheetName} | {r.family} | {r.type} | {(r.tbId != null ? r.tbId.ToString() : "-")}");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
