// ============================================================
// FRAGMENT (action) — action-report-element-dependencies.cs
// PURPOSE: For each element in `elements`, list everything that WOULD GO WITH IT — the tags, dimensions,
//          hosted families, sketch lines and openings Revit would take away too, plus what it is joined
//          to, what cuts it and what it cuts. The answer to "can I delete this / what breaks if I move
//          this", asked BEFORE the change rather than discovered after it.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-id-list.cs
//          or filter-by-current-selection.cs.
// NOT STANDALONE — see scripts/README.md for how to compose.
// READ-ONLY — opens no transaction, changes nothing.
//
// ✱✱ THE ONE CALL THAT ANSWERS IT: `element.GetDependentElements(null)`. Passing null as the filter means
//    "everything", and what comes back is Revit's own list of what it considers attached to this element
//    — the same set it removes when you delete it. It is not derivable by hand: it reaches tags that
//    reference the element, dimensions witnessed on it, families hosted in it, the sketch a floor is
//    drawn from, and openings cut through it. Nothing in this library was asking it.
//
// ✱✱ THE LIST ALWAYS CONTAINS THE ELEMENT ITSELF. That is not a bug and it must not be filtered out
//    blindly — it is why the count reads one higher than people expect. It is excluded from the table
//    below and stated in the summary instead, so the number is never quietly wrong in either direction.
//
// ✱✱ "CAN I DELETE IT" IS A SEPARATE QUESTION, and Revit will answer it directly:
//    `DocumentValidation.CanDeleteElement(doc, id)`. An element can have zero dependents and still be
//    undeletable — a pinned element, the last level, an element owned by another user in a workshared
//    model. Both questions are asked here because acting on one alone gives a confident wrong answer.
//
// GOTCHA: JOINED, CUT and CUTTING are relationships that survive deletion of neither side cleanly — a
//         wall joined to three others changes shape when one goes. They are reported separately from the
//         dependent list because Revit does NOT delete them with the element; it re-cleans the geometry,
//         which is the change nobody sees coming in a plan.
// GOTCHA: this reports what Revit knows about. It cannot see a DIMENSION IN A LINKED MODEL, or a
//         reference from another file, or the drawing on a sheet — use
//         action-report-views-showing-element.cs for "which drawings show this".
// RELATED: filter-by-pin-status.cs and action-report-constraints.cs answer the neighbouring question
//          "why won't this move"; this one answers "what goes with it if it does".
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE tagged, hosted element (a door
//   in a wall, or a diffuser in a ceiling) and check the list against what Revit warns when you press
//   Delete on that element by hand.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxElementsReported = 25;     // how many of `elements` to detail
int maxDependentsPerElement = 30; // cap per element, rest summarised
bool includeGeometryRelations = true; // joined / cut / cutting — costs a few extra calls per element
// ---- END INPUTS ----

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this (filter-by-id-list.cs if you have Ids).");
    return sb.ToString();
}

int totalDependents = 0, undeletable = 0, reported = 0;

sb.AppendLine($"Dependency report for {elements.Count} element(s) — read-only, nothing changed.");
sb.AppendLine();

foreach (var el in elements)
{
    if (el == null) continue;
    if (reported >= maxElementsReported) break;
    reported++;

    string elLabel = $"{el.Category?.Name ?? "(no category)"} '{el.Name}' (Id {el.Id})";

    // ---- what Revit would take with it ----
    var dependentIds = new List<ElementId>();
    string dependentError = null;
    try
    {
        var raw = el.GetDependentElements(null);
        if (raw != null)
            foreach (var id in raw)
                if (id != el.Id) dependentIds.Add(id); // the element itself is always in this list
    }
    catch (Exception ex) { dependentError = ex.Message; }

    // ---- and whether it can go at all ----
    bool canDelete = true;
    string deleteNote = "";
    try { canDelete = DocumentValidation.CanDeleteElement(Document, el.Id); }
    catch (Exception ex) { deleteNote = " (could not be checked: " + ex.Message + ")"; }
    if (!canDelete) undeletable++;

    totalDependents += dependentIds.Count;

    sb.AppendLine($"### {elLabel}");
    sb.AppendLine($"Deletable: {(canDelete ? "yes" : "NO — Revit refuses")}{deleteNote}");

    if (dependentError != null)
    {
        sb.AppendLine($"Dependents: could not be read — {dependentError}");
    }
    else if (dependentIds.Count == 0)
    {
        sb.AppendLine("Dependents: none — nothing else is attached to it.");
    }
    else
    {
        // Group by category so a wall with 40 hosted things reads as "40 doors", not 40 lines.
        var grouped = dependentIds
            .Select(id => Document.GetElement(id))
            .Where(d => d != null)
            .GroupBy(d => d.Category?.Name ?? "(no category)")
            .OrderByDescending(g => g.Count())
            .ToList();

        sb.AppendLine($"Dependents: {dependentIds.Count} element(s) would go with it —");
        sb.AppendLine("Category | Count | Ids");
        sb.AppendLine("--- | --- | ---");
        int shown = 0;
        foreach (var g in grouped)
        {
            var ids = g.Take(Math.Max(0, maxDependentsPerElement - shown)).Select(d => d.Id.ToString()).ToList();
            shown += ids.Count;
            string idCell = string.Join(", ", ids);
            if (g.Count() > ids.Count) idCell += $", +{g.Count() - ids.Count} more";
            sb.AppendLine($"{g.Key} | {g.Count()} | {idCell}");
            if (shown >= maxDependentsPerElement) break;
        }
    }

    if (includeGeometryRelations)
    {
        // These are NOT deleted with the element — but the geometry re-cleans when it goes, which is the
        // change that shows up on a printed plan and surprises people.
        var joined = new List<ElementId>();
        try { var j = JoinGeometryUtils.GetJoinedElements(Document, el); if (j != null) joined.AddRange(j); } catch { }

        var cuttingVoids = new List<ElementId>();
        try { var v = InstanceVoidCutUtils.GetCuttingVoidInstances(el); if (v != null) cuttingVoids.AddRange(v); } catch { }

        var beingCut = new List<ElementId>();
        var fi = el as FamilyInstance;
        if (fi != null)
        {
            try { var b = InstanceVoidCutUtils.GetElementsBeingCut(fi); if (b != null) beingCut.AddRange(b); } catch { }
        }

        var cuttingSolids = new List<ElementId>();
        try { var c = SolidSolidCutUtils.GetCuttingSolids(el); if (c != null) cuttingSolids.AddRange(c); } catch { }

        var solidsBeingCut = new List<ElementId>();
        try { var c = SolidSolidCutUtils.GetSolidsBeingCut(el); if (c != null) solidsBeingCut.AddRange(c); } catch { }

        if (joined.Count + cuttingVoids.Count + beingCut.Count + cuttingSolids.Count + solidsBeingCut.Count > 0)
        {
            sb.AppendLine("Geometry relationships (NOT deleted with it — but the shape re-cleans when it changes):");
            if (joined.Count > 0)          sb.AppendLine($"  joined to: {joined.Count} — {string.Join(", ", joined.Take(10).Select(i => i.ToString()))}");
            if (cuttingVoids.Count > 0)    sb.AppendLine($"  cut by void families: {cuttingVoids.Count} — {string.Join(", ", cuttingVoids.Take(10).Select(i => i.ToString()))}");
            if (beingCut.Count > 0)        sb.AppendLine($"  this void cuts: {beingCut.Count} — {string.Join(", ", beingCut.Take(10).Select(i => i.ToString()))}");
            if (cuttingSolids.Count > 0)   sb.AppendLine($"  cut by solids: {cuttingSolids.Count} — {string.Join(", ", cuttingSolids.Take(10).Select(i => i.ToString()))}");
            if (solidsBeingCut.Count > 0)  sb.AppendLine($"  this cuts: {solidsBeingCut.Count} — {string.Join(", ", solidsBeingCut.Take(10).Select(i => i.ToString()))}");
        }
    }

    sb.AppendLine();
}

if (elements.Count > maxElementsReported)
    sb.AppendLine($"... {elements.Count - maxElementsReported} more element(s) not detailed (raise maxElementsReported).");
sb.AppendLine($"TOTAL across the {reported} element(s) detailed: {totalDependents} dependent element(s), {undeletable} that Revit will not delete.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
