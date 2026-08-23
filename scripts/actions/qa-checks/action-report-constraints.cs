// ============================================================
// FRAGMENT (action) — action-report-constraints.cs
// PURPOSE: Answer "why won't this element move?" — list every dimension/alignment CONSTRAINT attached to
//          each element in `elements`, say what it is locked to, and on request remove them. The other
//          half of the "it's pinned / it's grouped / it won't budge" family, and the one the Brain had
//          nothing for.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
//
// ✱✱ WHY A CONSTRAINT IS INVISIBLE UNTIL YOU GO LOOKING: it is not a property OF the element. It is a
//    SEPARATE element, in category OST_Constraints, that holds References to two or more things. There is
//    no `wall.Constraints` to read. The only way to find what is holding an element is to walk EVERY
//    constraint in the model and check whether any of its References points back at your element. That is
//    why this reads as slow and why nothing surfaces it by accident.
//
// ✱✱ WHAT IT ACTUALLY TELLS YOU. Each Reference carries an `ElementReferenceType` — the reference is to a
//    face, a centreline, a stable surface, and so on. Two elements can be locked together through faces
//    you never dimensioned yourself, because an EQ constraint or a locked alignment made one. The report
//    prints, per constraint: its type (`Dimension` for a locked dimension, an alignment otherwise), how
//    many references it holds, and every OTHER element on the far end with its category — which is the
//    thing that will move (or refuse to) when you touch this one.
//
// ⚠ REMOVING A CONSTRAINT IS NOT COSMETIC. Deleting a locked dimension deletes the DIMENSION — the
//    annotation disappears from the drawing along with the lock. That is usually not what someone means
//    by "unlock it". `removeMode = "unlockOnly"` clears the padlock and KEEPS the dimension, which is
//    almost always the right move; `"delete"` removes the constraint element entirely and is there for
//    genuine leftovers. Dry run by default either way.
//
// GOTCHA: this walks all constraints once per run, not once per element — on a big model expect a few
//         seconds, and pass a small `elements` set rather than the whole category.
// GOTCHA: a constraint that references a DELETED element still exists and reports its far end as
//         "(missing)". Those are the genuine leftovers worth deleting.
// RELATED: filter-by-pin-status.cs (pinned), filter-by-group.cs (grouped) — the other two reasons an
//          element refuses to move.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE stuck element first.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string removeMode = "report";   // "report" | "unlockOnly" | "delete"
bool dryRun = true;             // true = say what WOULD happen, change nothing
int maxConstraintsListed = 40;  // cap the printout on a badly over-constrained model
// ---- END INPUTS ----

if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this.");
}
else if (removeMode != "report" && removeMode != "unlockOnly" && removeMode != "delete")
{
    sb.AppendLine($"removeMode '{removeMode}' is not one of: report, unlockOnly, delete.");
}
else
{
    // A constraint is a separate element — walk them all once, not per target.
    var allConstraints = new FilteredElementCollector(Document)
        .OfCategory(BuiltInCategory.OST_Constraints)
        .WhereElementIsNotElementType()
        .ToElements().ToList();

    var targetIds = new HashSet<ElementId>(elements.Select(e => e.Id));
    var hits = new List<Tuple<Element, Element>>();   // constraint, the target it holds
    var hitConstraints = new HashSet<ElementId>();

    foreach (var c in allConstraints)
    {
        ReferenceArray refs = null;
        try { refs = (c as Dimension)?.References; } catch { }
        if (refs == null) continue;

        foreach (Reference r in refs)
        {
            if (r == null || !targetIds.Contains(r.ElementId)) continue;
            var target = elements.First(e => e.Id == r.ElementId);
            hits.Add(Tuple.Create(c, target));
            hitConstraints.Add(c.Id);
            break;   // one hit per constraint per target is enough
        }
    }

    sb.AppendLine($"{elements.Count} element(s) checked against {allConstraints.Count} constraint(s) in the model.");
    sb.AppendLine($"{hitConstraints.Count} constraint(s) attach to them.");
    sb.AppendLine();

    if (hits.Count == 0)
    {
        sb.AppendLine("NOTHING IS CONSTRAINING THESE ELEMENTS. If one still will not move, the reason is");
        sb.AppendLine("something else — check filter-by-pin-status.cs (pinned) and filter-by-group.cs (in a group),");
        sb.AppendLine("or whether the view is showing a linked model rather than this one.");
    }
    else
    {
        int listed = 0;
        foreach (var grp in hits.GroupBy(h => h.Item2.Id))
        {
            var target = grp.First().Item2;
            sb.AppendLine($"{target.Category?.Name} {target.Id} — held by {grp.Count()} constraint(s):");

            foreach (var h in grp)
            {
                if (listed++ >= maxConstraintsListed) break;
                var c = h.Item1;
                var dim = c as Dimension;
                string kind = dim != null && dim.NumberOfSegments > 1 ? "Dimension (multi-segment)" : c.GetType().Name;
                bool locked = false;
                try { locked = dim != null && dim.AreSegmentsEqual; } catch { }

                var others = new List<string>();
                try
                {
                    foreach (Reference r in dim.References)
                    {
                        if (r.ElementId == target.Id) continue;
                        var other = Document.GetElement(r.ElementId);
                        others.Add(other == null
                            ? $"{r.ElementId} (missing)"
                            : $"{other.Category?.Name ?? other.GetType().Name} {r.ElementId} [{r.ElementReferenceType}]");
                    }
                }
                catch { }

                sb.AppendLine($"    {kind} {c.Id}{(locked ? " EQ-locked" : "")} -> "
                            + (others.Count == 0 ? "(no other end — leftover)" : string.Join(", ", others)));
            }
            sb.AppendLine();
        }
        if (listed >= maxConstraintsListed)
            sb.AppendLine($"... list capped at {maxConstraintsListed}. Raise maxConstraintsListed to see the rest.");

        // ---- act on them ----
        if (removeMode != "report")
        {
            int unlocked = 0, deleted = 0, failed = 0;
            if (dryRun)
            {
                sb.AppendLine(removeMode == "unlockOnly"
                    ? $"DRY RUN — would UNLOCK {hitConstraints.Count} constraint(s), keeping the dimensions on the drawing."
                    : $"DRY RUN — would DELETE {hitConstraints.Count} constraint(s). Any locked DIMENSION among them disappears from the drawing too.");
                sb.AppendLine("Set dryRun = false to apply.");
            }
            else
            {
                using (var t = new Transaction(Document, "AJ Tools - " + (removeMode == "delete" ? "Delete constraints" : "Unlock constraints")))
                {
                    t.Start();
                    foreach (var cid in hitConstraints)
                    {
                        try
                        {
                            if (removeMode == "unlockOnly")
                            {
                                var d = Document.GetElement(cid) as Dimension;
                                if (d != null)
                                {
                                    // `Dimension.IsLocked` is the padlock, and it is reached BY NAME because
                                    // it is not present on every Revit this library runs on. Clearing it
                                    // leaves the dimension itself on the drawing — that is the whole point
                                    // of this mode. If the property is not there, say so rather than
                                    // silently counting a success.
                                    var lockProp = typeof(Dimension).GetProperty("IsLocked");
                                    if (lockProp != null && lockProp.CanWrite)
                                    {
                                        lockProp.SetValue(d, false);
                                        unlocked++;
                                    }
                                    else
                                    {
                                        failed++;
                                        sb.AppendLine($"    {cid}: this Revit has no Dimension.IsLocked — "
                                                    + "unlock it by hand, or use removeMode = \"delete\" knowing the dimension goes too.");
                                    }
                                }
                            }
                            else
                            {
                                Document.Delete(cid);
                                deleted++;
                            }
                        }
                        catch (Exception ex) { failed++; sb.AppendLine($"    {cid}: FAILED — {ex.Message}"); }
                    }
                    t.Commit();
                }
                sb.AppendLine(removeMode == "unlockOnly"
                    ? $"Unlocked {unlocked} constraint(s); the dimensions are still on the drawing. Failed: {failed}."
                    : $"Deleted {deleted} constraint(s). Failed: {failed}.");
                sb.AppendLine("Re-read the element and try the move again — verify, do not assume.");
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
