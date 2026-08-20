// ============================================================
// FRAGMENT (action) — action-manage-named-set.cs
// PURPOSE: Name a set of elements once, then re-select / isolate / hide / show it by that name later —
//          the "park the beams while I run the ducts, then bring them back" job. Built on Revit's own
//          SelectionFilterElement, so the set lives IN the model: it survives the session and Revit
//          itself maintains it as elements are deleted.
// STANDALONE — makes its own sb and returns its own string; no filter fragment needed above.
// GOTCHA: this deliberately does NOT hold a frozen list of Element Ids in memory. A frozen id list goes
//         stale exactly the way recall does (START-HERE.md rule 2) — the user edits and undoes between
//         messages. Every mode re-resolves the ids through the document and REPORTS how many no longer
//         resolve, so a stale set is visible rather than silent.
// GOTCHA: "create" reads the CURRENT REVIT SELECTION, on purpose. To save the result of a filter chain
//         instead, use action-create-selection-filter.cs — that one consumes `elements`. The two are
//         complementary; neither replaces the other.
// GOTCHA: "hide" is temporary by default, matching house convention — a lasting visibility change needs
//         an explicit ask (see ../../../knowledge/live-model/views.md).
// SOURCE: ../../../knowledge/live-model/views.md § View visibility patterns
// STATUS: not yet live-verified — compile-checked only (2026-08-20). Run mode="list" first (read-only),
//         then one "create" and one "isolate" on a small set, before trusting it on real work.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "list";        // "list" | "create" | "add" | "remove" | "select" | "isolate" | "hide" | "show" | "delete"
string setName = "";         // required for every mode except "list"
int? targetViewIdInt = null; // null = active view; only used by isolate / hide / show
bool permanentHide = false;  // false = temporary hide (default); true = permanent View.HideElements
bool confirmDelete = false;  // "delete" refuses to run until this is explicitly true
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var allSets = new FilteredElementCollector(Document)
    .OfClass(typeof(SelectionFilterElement))
    .Cast<SelectionFilterElement>()
    .OrderBy(s => s.Name)
    .ToList();

Func<SelectionFilterElement, List<Element>> Resolve = s =>
    s.GetElementIds()
     .Select(id => Document.GetElement(id))
     .Where(e => e != null)
     .ToList();

if (mode == "list")
{
    sb.AppendLine($"Named sets in this model: {allSets.Count}");
    foreach (var s in allSets)
    {
        int stored = s.GetElementIds().Count;
        int liveCount = Resolve(s).Count;
        string staleNote = stored == liveCount ? "" : $"  <- {stored - liveCount} stored id(s) no longer resolve";
        sb.AppendLine($"  - '{s.Name}' (Id {s.Id}) — {liveCount} element(s){staleNote}");
    }
    if (allSets.Count == 0) sb.AppendLine("  (none yet — select elements in Revit and run mode=\"create\" with a setName)");
    return sb.ToString();
}

if (string.IsNullOrWhiteSpace(setName))
{
    sb.AppendLine($"mode=\"{mode}\" needs a setName. Run mode=\"list\" to see what already exists.");
    return sb.ToString();
}

var target = allSets.FirstOrDefault(s => string.Equals(s.Name, setName, StringComparison.OrdinalIgnoreCase));

// ---------- modes that build or change the set ----------
if (mode == "create" || mode == "add" || mode == "remove")
{
    var selected = UIDocument.Selection.GetElementIds().ToList();
    if (selected.Count == 0)
    {
        sb.AppendLine($"Nothing is selected in Revit right now, so there is nothing to {mode}.");
        sb.AppendLine("Ask the user to select the elements first, then re-run.");
        return sb.ToString();
    }

    using (var t = new Transaction(Document, "AJ Tools - Manage Named Set"))
    {
        t.Start();
        try
        {
            if (mode == "create")
            {
                if (target == null)
                {
                    var created = SelectionFilterElement.Create(Document, setName);
                    created.SetElementIds(selected);
                    sb.AppendLine($"Created named set '{setName}' (Id {created.Id}) holding {selected.Count} selected element(s).");
                }
                else
                {
                    int had = target.GetElementIds().Count;
                    target.SetElementIds(selected);
                    sb.AppendLine($"Replaced named set '{target.Name}' (Id {target.Id}) — was {had} element(s), now {selected.Count}.");
                }
            }
            else if (target == null)
            {
                sb.AppendLine($"No named set called '{setName}' — cannot {mode}. Run mode=\"list\" to see what exists.");
            }
            else
            {
                var current = new HashSet<ElementId>(target.GetElementIds());
                int before = current.Count;
                if (mode == "add") { foreach (var id in selected) current.Add(id); }
                else { foreach (var id in selected) current.Remove(id); }
                target.SetElementIds(current.ToList());
                sb.AppendLine($"{(mode == "add" ? "Added to" : "Removed from")} '{target.Name}' — was {before} element(s), now {current.Count}.");
            }
            t.Commit();
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to {mode} — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
    return sb.ToString();
}

// ---------- every remaining mode needs the set to exist ----------
if (target == null)
{
    sb.AppendLine($"No named set called '{setName}' in this model. Run mode=\"list\" to see what exists.");
    return sb.ToString();
}

int storedCount = target.GetElementIds().Count;
var liveMembers = Resolve(target);
if (liveMembers.Count != storedCount)
    sb.AppendLine($"NOTE: '{target.Name}' stores {storedCount} id(s) but only {liveMembers.Count} still resolve — {storedCount - liveMembers.Count} have been removed since it was saved.");

if (liveMembers.Count == 0)
{
    sb.AppendLine($"Named set '{target.Name}' resolves to 0 live element(s) — nothing to act on.");
    return sb.ToString();
}

if (mode == "select")
{
    UIDocument.Selection.SetElementIds(liveMembers.Select(e => e.Id).ToList());
    sb.AppendLine($"Selected the {liveMembers.Count} element(s) of '{target.Name}' in Revit (replaced any prior selection).");
    return sb.ToString();
}

if (mode == "delete")
{
    if (!confirmDelete)
    {
        sb.AppendLine($"Refusing to drop named set '{target.Name}' (Id {target.Id}, {liveMembers.Count} element(s)) — set confirmDelete = true to go ahead.");
        sb.AppendLine("This drops the SAVED SET only. The elements themselves stay exactly as they are either way.");
        return sb.ToString();
    }
    using (var t = new Transaction(Document, "AJ Tools - Drop Named Set"))
    {
        t.Start();
        try
        {
            string gone = target.Name;
            Document.Delete(target.Id);
            t.Commit();
            sb.AppendLine($"Dropped the named set '{gone}'. Its {liveMembers.Count} element(s) are untouched and still in the model.");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to drop the named set — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
    return sb.ToString();
}

// ---------- view-scoped modes ----------
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;
if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
    return sb.ToString();
}

if (mode == "isolate" && !view.CanUseTemporaryVisibilityModes())
{
    sb.AppendLine($"View '{view.Name}' ({view.ViewType}) does not support temporary isolate — skipped.");
    return sb.ToString();
}

using (var t2 = new Transaction(Document, "AJ Tools - Named Set Visibility"))
{
    t2.Start();
    try
    {
        if (mode == "isolate")
        {
            if (view.IsTemporaryHideIsolateActive())
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            view.IsolateElementsTemporary(liveMembers.Select(e => e.Id).ToList());
            sb.AppendLine($"Isolated the {liveMembers.Count} element(s) of '{target.Name}' in '{view.Name}' (temporary — Reset Temporary Hide/Isolate clears it).");
        }
        else if (mode == "hide")
        {
            var hideable = liveMembers.Where(e => e.CanBeHidden(view)).Select(e => e.Id).ToList();
            int skipped = liveMembers.Count - hideable.Count;
            if (hideable.Count == 0)
            {
                sb.AppendLine($"None of the {liveMembers.Count} element(s) of '{target.Name}' can be hidden in '{view.Name}'.");
            }
            else
            {
                if (permanentHide) view.HideElements(hideable);
                else view.HideElementsTemporary(hideable);
                string how = permanentHide ? "permanently" : "temporarily (Reset Temporary Hide/Isolate brings them back)";
                sb.AppendLine($"Hid {hideable.Count} element(s) of '{target.Name}' in '{view.Name}' {how}.");
                if (skipped > 0) sb.AppendLine($"  {skipped} could not be hidden in this view and were left alone.");
            }
        }
        else if (mode == "show")
        {
            if (view.IsTemporaryHideIsolateActive())
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                sb.AppendLine($"Cleared the temporary hide/isolate mode on '{view.Name}'.");
            }
            var hidden = liveMembers.Where(e => e.IsHidden(view)).Select(e => e.Id).ToList();
            if (hidden.Count > 0)
            {
                view.UnhideElements(hidden);
                sb.AppendLine($"Brought back {hidden.Count} permanently-hidden element(s) of '{target.Name}' in '{view.Name}'.");
            }
            else
            {
                sb.AppendLine($"No element of '{target.Name}' was permanently hidden in '{view.Name}'.");
            }
        }
        else
        {
            sb.AppendLine($"Unknown mode \"{mode}\". Use: list | create | add | remove | select | isolate | hide | show | delete");
        }
        t2.Commit();
    }
    catch (Exception ex)
    {
        t2.RollBack();
        sb.AppendLine($"FAILED on mode=\"{mode}\" — rolled back, nothing changed. Reason: {ex.Message}");
    }
}

return sb.ToString();
