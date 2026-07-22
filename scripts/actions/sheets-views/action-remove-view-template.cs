// ============================================================
// FRAGMENT (action) — action-remove-view-template.cs
// PURPOSE: Detach the View Template from one or more views (sets View.ViewTemplateId back to
//          InvalidElementId) — the paired "undo" for action-apply-view-template.cs. The view keeps
//          whatever settings the template last gave it (they just stop being template-controlled); the
//          template definition itself is untouched and still applied to any other view using it. Set
//          deleteTemplateElement = true to also permanently delete the template from the whole document
//          (removes it from EVERY view using it, not just the ones listed here) — treat that like any
//          other delete: confirm with the user first, and it needs allowDestructive: true on the bridge
//          call too.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — operates on VIEWS, not model elements.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int[] targetViewIdInts = { }; // empty = active view only; else every listed view Id gets detached
bool deleteTemplateElement = false; // true = permanently delete the template from the document — destructive, confirm first
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var targetViews = targetViewIdInts.Length > 0
    ? targetViewIdInts.Select(id => Document.GetElement(new ElementId(id)) as View).Where(v => v != null).ToList()
    : new List<View> { Document.ActiveView };

int okCount = 0;
var skipped = new List<string>();
var templateIdsSeen = new HashSet<ElementId>();

using (var t = new Transaction(Document, "AJ Tools - Remove View Template"))
{
    t.Start();
    try
    {
        foreach (var v in targetViews)
        {
            if (v.IsTemplate) { skipped.Add($"{v.Name} (is itself a template)"); continue; }
            if (v.ViewTemplateId == ElementId.InvalidElementId) { skipped.Add($"{v.Name} (no template applied)"); continue; }

            templateIdsSeen.Add(v.ViewTemplateId);
            v.ViewTemplateId = ElementId.InvalidElementId;
            okCount++;
        }

        string deleteNote = "";
        if (deleteTemplateElement && templateIdsSeen.Count > 0)
        {
            var deleted = Document.Delete(templateIdsSeen.ToList());
            deleteNote = $" Deleted {templateIdsSeen.Count} template definition(s) from the document entirely ({deleted.Count} element(s) removed total, including dependents).";
        }

        t.Commit();
        sb.AppendLine($"Detached View Template from {okCount} view(s).{deleteNote}");
        if (skipped.Count > 0) sb.AppendLine("Skipped: " + string.Join("; ", skipped));
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to remove the View Template — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
return sb.ToString();
