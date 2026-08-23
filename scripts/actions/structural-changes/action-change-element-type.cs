// ============================================================
// FRAGMENT (action) — action-change-element-type.cs
// PURPOSE: Bulk-swap every element in `elements` from its current type to a different named type within
//          the SAME family — e.g. change every "600x300" duct fitting instance to "600x600", or every
//          door instance from one type to another. An element whose family has no type by that name is
//          skipped, never guessed at.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ⚠⚠ ADDED 2026-08-23 — CHANGING A TYPE CAN LOSE INSTANCE PARAMETER VALUES, SILENTLY. `ChangeTypeId`
//    carries over the instance parameters that the new type also has, and drops the rest without a
//    word. Mark, Comments, a custom shared parameter that only the old family carried — gone, and the
//    element still looks fine. On a batch of 200 that is not something anyone notices until much later.
//    `preserveInstanceParameters` reads every writable instance parameter BEFORE the swap and writes it
//    back after, matching by parameter NAME (the Id changes with the type).
//    It deliberately never copies the identity parameters — Family, Family Name, Type, Type Name,
//    Family and Type, Image — because those ARE the thing being changed and restoring them would undo
//    the swap. It reports how many values it put back and how many refused.
//
// ⚠⚠ AND ONE MORE, ALSO 2026-08-23 — GROUPED ELEMENTS. An element inside a group can only carry a
//    per-instance parameter value if that parameter is flagged "Varies across groups"
//    (`Definition.VariesAcrossGroups`). If it is not, writing it back either refuses, or takes and
//    changes EVERY instance of that group across the model. Neither announces itself, and the second is
//    much worse than the data loss this restore was written to prevent. Grouped elements are therefore
//    skipped unless the parameter genuinely varies, and the count is reported so the skip is visible.
// ============================================================
// For anything bulk or hard to reverse, run the filter ALONE first (see README's explorer-first
// discipline) and confirm the count/preview with the user before appending this action.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetTypeName = "New Type Name";
bool preserveInstanceParameters = true;  // capture instance params before the swap, write them back after
// ---- END INPUTS ----

int changed = 0, skipped = 0, restored = 0, refused = 0, skippedGrouped = 0;

// The identity parameters — restoring these would undo the type change itself.
var identityParams = new HashSet<BuiltInParameter>
{
    BuiltInParameter.ELEM_FAMILY_PARAM,
    BuiltInParameter.ELEM_TYPE_PARAM,
    BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
    BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM,
    BuiltInParameter.SYMBOL_NAME_PARAM,
    BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM,
    BuiltInParameter.ALL_MODEL_FAMILY_NAME,
    BuiltInParameter.ALL_MODEL_TYPE_NAME,
    BuiltInParameter.ALL_MODEL_IMAGE
};

Func<Element, Dictionary<string, object>> captureParams = (el) =>
{
    var vals = new Dictionary<string, object>();
    foreach (Parameter p in el.Parameters)
    {
        if (p == null || p.IsReadOnly || p.Definition == null) continue;
        BuiltInParameter bip = BuiltInParameter.INVALID;
        try { bip = ((InternalDefinition)p.Definition).BuiltInParameter; } catch { }
        if (identityParams.Contains(bip)) continue;
        try
        {
            switch (p.StorageType)
            {
                case StorageType.Double:    vals[p.Definition.Name] = p.AsDouble(); break;
                case StorageType.Integer:   vals[p.Definition.Name] = p.AsInteger(); break;
                case StorageType.String:    vals[p.Definition.Name] = p.AsString(); break;
                case StorageType.ElementId: vals[p.Definition.Name] = p.AsElementId(); break;
            }
        }
        catch { }
    }
    return vals;
};

Action<Element, Dictionary<string, object>> restoreParams = (el, vals) =>
{
    // ⚠ An element inside a GROUP can only carry a per-instance value if the parameter is flagged
    // "Varies across groups". If it is not, writing either refuses outright or changes EVERY instance
    // of that group across the whole model — and neither outcome announces itself. Found 2026-08-23.
    bool grouped = el.GroupId != null && el.GroupId != ElementId.InvalidElementId;

    foreach (Parameter p in el.Parameters)
    {
        if (p == null || p.IsReadOnly || p.Definition == null) continue;
        BuiltInParameter bip = BuiltInParameter.INVALID;
        try { bip = ((InternalDefinition)p.Definition).BuiltInParameter; } catch { }
        if (identityParams.Contains(bip)) continue;
        if (!vals.ContainsKey(p.Definition.Name)) continue;

        if (grouped)
        {
            bool varies = false;
            try { varies = ((InternalDefinition)p.Definition).VariesAcrossGroups; } catch { }
            if (!varies) { skippedGrouped++; continue; }
        }

        var v = vals[p.Definition.Name];
        if (v == null) continue;
        try
        {
            bool ok = false;
            if (p.StorageType == StorageType.Double && v is double)       ok = p.Set((double)v);
            else if (p.StorageType == StorageType.Integer && v is int)    ok = p.Set((int)v);
            else if (p.StorageType == StorageType.String && v is string)  ok = p.Set((string)v);
            else if (p.StorageType == StorageType.ElementId && v is ElementId) ok = p.Set((ElementId)v);
            if (ok) restored++; else refused++;
        }
        catch { refused++; }
    }
};
var typeCache = new Dictionary<ElementId, ElementId>(); // current type Id -> resolved target type Id

using (var t = new Transaction(Document, "AJ Tools - Change Element Type"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var currentTypeId = e.GetTypeId();
            if (currentTypeId == ElementId.InvalidElementId) { skipped++; continue; }

            if (!typeCache.TryGetValue(currentTypeId, out var targetTypeId))
            {
                var currentType = Document.GetElement(currentTypeId) as ElementType;
                ElementType match = null;

                if (currentType is FamilySymbol currentSymbol)
                {
                    match = new FilteredElementCollector(Document)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(fs => fs.Family.Id == currentSymbol.Family.Id
                            && fs.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                }
                else if (currentType != null)
                {
                    // System-family types (duct/pipe/wall types, etc.) — match within the same type class.
                    match = new FilteredElementCollector(Document)
                        .OfClass(currentType.GetType())
                        .Cast<ElementType>()
                        .FirstOrDefault(et => et.FamilyName == currentType.FamilyName
                            && et.Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                }

                targetTypeId = match?.Id ?? ElementId.InvalidElementId;
                typeCache[currentTypeId] = targetTypeId;
            }

            if (targetTypeId == ElementId.InvalidElementId || targetTypeId == currentTypeId) { skipped++; continue; }

            try
            {
                // Capture BEFORE the swap — after it, the old values are already gone.
                var saved = preserveInstanceParameters ? captureParams(e) : null;

                var newId = e.ChangeTypeId(targetTypeId);
                changed++;

                if (preserveInstanceParameters)
                {
                    // ChangeTypeId can return a NEW element id — some kinds are recreated, not edited.
                    var after = (newId != null && newId != ElementId.InvalidElementId)
                        ? Document.GetElement(newId) : e;
                    if (after != null) restoreParams(after, saved);
                }
            }
            catch { skipped++; } // some type changes aren't valid for a given instance — skip, don't fail the batch
        }
        t.Commit();
        sb.AppendLine($"Changed {changed} element(s) to type '{targetTypeName}', skipped {skipped} (target type not found in that family, already that type, or change not supported for that instance).");
        if (preserveInstanceParameters)
        {
            sb.AppendLine($"Instance parameters written back: {restored}. Refused (read-only on the new type, or not present on it): {refused}.");
            if (skippedGrouped > 0)
                sb.AppendLine($"Not written back on GROUPED elements (parameter does not vary across groups): {skippedGrouped} — writing those would change every instance of the group.");
        }
        else
            sb.AppendLine("preserveInstanceParameters was OFF — any instance value the new type does not carry has been lost.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to change element type — rolled back, nothing changed. Reason: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
