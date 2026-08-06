// ============================================================
// FRAGMENT (action) — action-duplicate-view-template.cs
// PURPOSE: Duplicate an existing View Template (by name) into a new, separately-named template with the
//          same settings — a starting point for a variant without hand-rebuilding it. Different from
//          action-duplicate-views.cs: that one consumes `elements` from filter-by-views.cs, which
//          deliberately EXCLUDES templates (IsTemplate elements aren't "a view" in that filter's sense),
//          so templates need their own by-name lookup instead of a filter.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`), same as the other View Template fragments.
// SOURCE: ../../../knowledge/live-model/views.md § "Duplicating a VIEW TEMPLATE" — the technique and the
//          auto-suffix behaviour were already recorded there on 2026-08-01. Read that note before
//          changing anything here.
//
// FIXED 2026-08-07 — the previous version could never succeed. It called
//          `source.Duplicate(ViewDuplicateOption.Duplicate)` behind a `CanViewBeDuplicated` guard, and on
//          Revit 2020 that guard returns FALSE for every View Template on all three duplicate options —
//          so it always reported "cannot be duplicated" and did nothing. The guard was not the bug:
//          removing it and calling `Duplicate()` anyway throws `InvalidOperationException: View cannot be
//          duplicated`. **A View Template cannot be copied with View.Duplicate on this API at all.**
//          `ElementTransformUtils.CopyElements` can, which is exactly what views.md already said.
//          Verified live 2026-08-07: 'Mechanical Plan' (scale 100, detail Medium) copied to a new
//          template carrying scale 100 / detail Medium, template count 16 -> 17, no existing view
//          touched, source unchanged.
// GOTCHA: Revit auto-suffixes the copy's name ('Mechanical Plan' -> 'Mechanical Plan1') because template
//          names must be unique, and the name is only settled once the copy transaction has COMMITTED.
//          That is why the rename is a second transaction rather than part of the first — read the name
//          back, don't predict the suffix.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceTemplateName = "Mechanical - Coordination";
string newTemplateName = "Mechanical - Coordination (Copy)";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var source = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
    .FirstOrDefault(v => v.IsTemplate && v.Name.Equals(sourceTemplateName, StringComparison.OrdinalIgnoreCase));

if (source == null)
{
    var available = new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).Select(v => v.Name);
    sb.AppendLine($"View Template '{sourceTemplateName}' not found. Available: {string.Join(", ", available)}");
}
else if (new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
             .Any(v => v.IsTemplate && v.Name.Equals(newTemplateName, StringComparison.OrdinalIgnoreCase)))
{
    // Template names must be unique — say so here rather than letting the rename throw mid-sequence.
    sb.AppendLine($"A View Template called '{newTemplateName}' already exists — pick a different newTemplateName.");
}
else
{
    // One group over both transactions: if the rename fails, the copy is rolled back too, so a
    // half-finished 'Mechanical Plan1' is never left behind for the user to find and wonder about.
    using (var tg = new TransactionGroup(Document, "AJ Tools - Duplicate View Template"))
    {
        tg.Start();
        try
        {
            ElementId newId;
            using (var t = new Transaction(Document, "AJ Tools - Copy View Template"))
            {
                t.Start();
                var copied = ElementTransformUtils.CopyElements(
                    Document, new List<ElementId> { source.Id }, Document, Transform.Identity, new CopyPasteOptions());
                newId = copied.FirstOrDefault();
                if (newId == null || newId == ElementId.InvalidElementId)
                    throw new InvalidOperationException("CopyElements returned no new element.");
                t.Commit();
            }

            var newTemplate = Document.GetElement(newId) as View;
            if (newTemplate == null) throw new InvalidOperationException($"Copied element {newId.IntegerValue} is not a View.");
            string autoName = newTemplate.Name;   // read it back — the suffix is Revit's choice, not ours

            using (var t = new Transaction(Document, "AJ Tools - Rename Copied View Template"))
            {
                t.Start();
                newTemplate.Name = newTemplateName;
                t.Commit();
            }

            tg.Assimilate();
            sb.AppendLine($"Duplicated View Template '{sourceTemplateName}' as '{newTemplate.Name}' (Id {newId.IntegerValue}). " +
                          $"Revit first named the copy '{autoName}'; it was renamed after the copy committed. " +
                          "The source template and every view using it are unaffected.");
        }
        catch (Exception ex)
        {
            tg.RollBack();
            sb.AppendLine($"FAILED to duplicate the View Template — whole sequence rolled back, no partial copy left behind. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
