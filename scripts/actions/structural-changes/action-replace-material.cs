// ============================================================
// FRAGMENT (action) — action-replace-material.cs
// PURPOSE: Swap one material for another EVERYWHERE it is used by the elements in `elements` — inside
//          the layers of walls/floors/roofs/ceilings, and on the plain material parameters of family
//          instances and their types. The "this material was wrong / has been renamed / the client wants
//          a different finish" job, without opening every type dialog by hand.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs.
//
// ✱✱ A MATERIAL HIDES IN THREE DIFFERENT PLACES and a swap that misses one leaves the old material half
//    in use, which then refuses to purge:
//    1. COMPOUND STRUCTURE LAYERS — walls, floors, roofs, ceilings. The material is on each LAYER of the
//       TYPE's CompoundStructure. You must pull the structure out, edit the layer, and SET IT BACK; the
//       layer objects you get are copies, so editing them in place changes nothing.
//    2. MATERIAL PARAMETERS — any parameter whose StorageType is ElementId and whose definition is a
//       material (doors, windows, furniture, most loadable families), on the INSTANCE or on the TYPE.
//    3. "<By Category>" — which is NOT a material and NOT null: it is ElementId.InvalidElementId (-1).
//       Reading it as "no material" and skipping is how a swap quietly misses everything set by category.
//
// ✱✱ EDITING A TYPE CHANGES EVERY ELEMENT OF THAT TYPE, not just the ones you selected. Two walls of the
//    same type are ONE type: change it via either and both change. That is Revit working correctly, but
//    it surprises people, so this counts and reports TYPES touched separately from elements in.
//
// GOTCHA: A MATERIAL LOCKED BY A FAMILY FORMULA WILL NOT SET and does not throw a useful error — the
//         same class of refusal recorded for formula-driven Description parameters. Those are named.
// GOTCHA: DRY RUN BY DEFAULT — it lists every layer and parameter it would touch.
// RELATED: action-report-compound-structure.cs shows what the layers currently are, first.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on ONE wall type and check the
//   structure in Revit, then use it for the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string findMaterialName = "";      // REQUIRED — the material to replace. "<By Category>" targets unset ones.
string replaceMaterialName = "";   // REQUIRED — the material to put in its place
bool doCompoundLayers = true;      // swap inside wall/floor/roof/ceiling layer structures
bool doInstanceParams = true;      // swap material parameters on the instances themselves
bool doTypeParams = true;          // swap material parameters on their types (affects ALL of that type)
bool dryRun = true;                // true = report only, change nothing
// ---- END INPUTS ----

int layersChanged = 0, instParams = 0, typeParams = 0, typesTouched = 0, refused = 0;
var notes = new List<string>();
var doneTypes = new HashSet<int>();

var allMaterials = new FilteredElementCollector(Document).OfClass(typeof(Material)).Cast<Material>().ToList();
bool findIsByCategory = findMaterialName == "<By Category>";
var findMat = findIsByCategory ? null : allMaterials.FirstOrDefault(m => m.Name == findMaterialName);
var replaceMat = allMaterials.FirstOrDefault(m => m.Name == replaceMaterialName);
ElementId findId = findIsByCategory ? ElementId.InvalidElementId : (findMat?.Id ?? ElementId.InvalidElementId);

if (string.IsNullOrWhiteSpace(findMaterialName) || string.IsNullOrWhiteSpace(replaceMaterialName))
{
    sb.AppendLine("Both findMaterialName and replaceMaterialName are required.");
}
else if (!findIsByCategory && findMat == null)
{
    sb.AppendLine($"Material '{findMaterialName}' not found. First 40 in this model: "
                + string.Join(", ", allMaterials.Select(m => m.Name).OrderBy(n => n).Take(40)));
}
else if (replaceMat == null)
{
    sb.AppendLine($"Material '{replaceMaterialName}' not found. First 40 in this model: "
                + string.Join(", ", allMaterials.Select(m => m.Name).OrderBy(n => n).Take(40)));
}
else if (elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter fragment above this.");
}
else
{
    sb.AppendLine($"Replacing '{findMaterialName}' with '{replaceMaterialName}' across {elements.Count} element(s).");
    sb.AppendLine();

    // Every material-valued parameter on an element, instance or type.
    Func<Element, List<Parameter>> materialParams = (e) =>
        e.Parameters.Cast<Parameter>()
         .Where(p => p.StorageType == StorageType.ElementId && !p.IsReadOnly)
         .Where(p => { try { return p.Definition.ParameterType == ParameterType.Material; }
                       catch { // newer API dropped ParameterType — fall back to what the value IS
                               try { var v = p.AsElementId();
                                     return v != ElementId.InvalidElementId && Document.GetElement(v) is Material; }
                               catch { return false; } } })
         .ToList();

    using (var t = new Transaction(Document, "AJ Tools - Replace material"))
    {
        if (!dryRun) t.Start();

        foreach (var el in elements)
        {
            string label = $"{el.Category?.Name ?? "element"} {el.Id.IntegerValue}";
            var typeEl = Document.GetElement(el.GetTypeId());

            // ---- 1. compound structure layers, on the TYPE ----
            if (doCompoundLayers && typeEl is HostObjAttributes && !doneTypes.Contains(typeEl.Id.IntegerValue))
            {
                var hoa = (HostObjAttributes)typeEl;
                CompoundStructure cs = null;
                try { cs = hoa.GetCompoundStructure(); } catch { }

                if (cs != null)
                {
                    var layers = cs.GetLayers();
                    int hits = 0;
                    for (int i = 0; i < layers.Count; i++)
                        if (layers[i].MaterialId == findId) hits++;

                    if (hits > 0)
                    {
                        if (dryRun)
                        {
                            sb.AppendLine($"  would change {hits} layer(s) in type '{typeEl.Name}' (affects every element of that type)");
                            layersChanged += hits; typesTouched++;
                        }
                        else
                        {
                            try
                            {
                                for (int i = 0; i < layers.Count; i++)
                                    if (layers[i].MaterialId == findId)
                                        cs.SetMaterialId(i, replaceMat.Id);   // index-based: the layer objects are copies
                                hoa.SetCompoundStructure(cs);                 // and the structure must be set BACK
                                layersChanged += hits; typesTouched++;
                                sb.AppendLine($"  type '{typeEl.Name}': {hits} layer(s) changed");
                            }
                            catch (Exception ex)
                            {
                                refused++;
                                notes.Add($"  type '{typeEl.Name}': compound structure refused — {ex.Message}");
                            }
                        }
                    }
                    doneTypes.Add(typeEl.Id.IntegerValue);
                }
            }

            // ---- 2. material parameters on the instance ----
            if (doInstanceParams)
            {
                foreach (var p in materialParams(el))
                {
                    if (p.AsElementId() != findId) continue;
                    if (dryRun) { sb.AppendLine($"  would set {label} · '{p.Definition.Name}'"); instParams++; continue; }
                    try
                    {
                        p.Set(replaceMat.Id);
                        if (p.AsElementId() == replaceMat.Id) { instParams++; }
                        else { refused++; notes.Add($"  {label} · '{p.Definition.Name}': accepted the set but did not change — locked by a family formula."); }
                    }
                    catch (Exception ex) { refused++; notes.Add($"  {label} · '{p.Definition.Name}': {ex.Message}"); }
                }
            }

            // ---- 3. material parameters on the type ----
            if (doTypeParams && typeEl != null && !doneTypes.Contains(-typeEl.Id.IntegerValue))
            {
                foreach (var p in materialParams(typeEl))
                {
                    if (p.AsElementId() != findId) continue;
                    if (dryRun) { sb.AppendLine($"  would set TYPE '{typeEl.Name}' · '{p.Definition.Name}' (affects every element of that type)"); typeParams++; continue; }
                    try
                    {
                        p.Set(replaceMat.Id);
                        if (p.AsElementId() == replaceMat.Id) { typeParams++; }
                        else { refused++; notes.Add($"  TYPE '{typeEl.Name}' · '{p.Definition.Name}': accepted the set but did not change — locked by a family formula."); }
                    }
                    catch (Exception ex) { refused++; notes.Add($"  TYPE '{typeEl.Name}' · '{p.Definition.Name}': {ex.Message}"); }
                }
                doneTypes.Add(-typeEl.Id.IntegerValue);
            }
        }

        if (!dryRun) t.Commit();
    }

    sb.AppendLine();
    sb.AppendLine(dryRun ? "DRY RUN — nothing changed. Set dryRun = false to apply." : "Applied.");
    sb.AppendLine($"  compound layers: {layersChanged} (across {typesTouched} type(s))");
    sb.AppendLine($"  instance parameters: {instParams}");
    sb.AppendLine($"  type parameters: {typeParams}");
    if (layersChanged + instParams + typeParams == 0)
        sb.AppendLine($"  NOTHING USES '{findMaterialName}' among these elements — check the name, or widen the filter above.");
    if (refused > 0) sb.AppendLine($"  refused: {refused}");
    foreach (var n in notes) sb.AppendLine(n);
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
