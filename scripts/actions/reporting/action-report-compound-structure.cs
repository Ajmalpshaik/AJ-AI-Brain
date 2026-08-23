// ============================================================
// FRAGMENT (action) — action-report-compound-structure.cs
// PURPOSE: Report the layer build-up (compound structure) of the wall/floor/roof/ceiling TYPES behind
//          `elements` — function, material, thickness per layer, core boundaries, total thickness.
//          The "what is this wall made of" answer, deduped per type.
// ASSUMES: elements (List<Element>, walls/floors/roofs/ceilings — or their types directly via
//          filter-by-types.cs) and sb exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
// GOTCHA: curtain walls and model-in-place have no compound structure — reported as such, not an error.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 9 walls correctly deduped to 1 type, layer/width/core reported.
// ============================================================

Func<double, double> toMm = v => v * 304.8;

// dedupe: instance -> its type; a type element passed directly is used as-is
var typeIds = new HashSet<ElementId>();
var typesToReport = new List<ElementType>();
foreach (var el in elements)
{
    var asType = el as ElementType;
    var ty = asType ?? Document.GetElement(el.GetTypeId()) as ElementType;
    if (ty != null && typeIds.Add(ty.Id)) typesToReport.Add(ty);
}

int withStructure = 0, without = 0;

foreach (var ty in typesToReport)
{
    CompoundStructure cs = null;
    var host = ty as HostObjAttributes;
    if (host != null) { try { cs = host.GetCompoundStructure(); } catch { } }

    if (cs == null)
    {
        without++;
        sb.AppendLine($"- Type '{ty.FamilyName}: {ty.Name}' (Id {ty.Id}) — no compound structure (curtain/in-place/non-host type).");
        continue;
    }

    withStructure++;
    var layers = cs.GetLayers();

    // The CORE total, not just which layers are core. This is the number behind the three different
    // dimensions the same wall can give you: total width is the outside-face-to-outside-face figure,
    // the core width is what the structure actually is, and the difference is the finishes. A room
    // measured to Finish, to Center and to CoreBoundary differs by exactly these amounts — see
    // action-report-room-dimensions.cs and the boundary-location note in action-report-room-boundaries.cs.
    // Added 2026-08-24.
    double coreWidthFt = 0;
    int firstCore = 0, lastCore = -1;
    try { firstCore = cs.GetFirstCoreLayerIndex(); lastCore = cs.GetLastCoreLayerIndex(); } catch { }
    for (int ci = firstCore; ci <= lastCore && ci < layers.Count; ci++)
    {
        if (ci < 0) continue;
        try { coreWidthFt += layers[ci].Width; } catch { }
    }
    double totalFt = 0;
    try { totalFt = cs.GetWidth(); } catch { }
    double finishesFt = totalFt - coreWidthFt;

    sb.AppendLine($"- Type '{ty.FamilyName}: {ty.Name}' (Id {ty.Id}) — {layers.Count} layer(s), total {toMm(totalFt):F0} mm (exterior/top first):");
    sb.AppendLine($"    core {toMm(coreWidthFt):F0} mm + finishes {toMm(finishesFt):F0} mm  <- the gap between a Finish dimension and a CoreBoundary one");
    int idx = 0;
    foreach (var layer in layers)
    {
        var mat = layer.MaterialId != ElementId.InvalidElementId ? Document.GetElement(layer.MaterialId)?.Name : null;
        bool isCore = idx >= firstCore && idx <= lastCore;
        sb.AppendLine($"    {idx + 1}. {layer.Function}, {toMm(layer.Width):F1} mm, material: {mat ?? "(by category)"}{(isCore ? "  [CORE]" : "")}");
        idx++;
    }
}

sb.AppendLine($"Compound structure: {withStructure} type(s) reported, {without} without structure, from {elements.Count} element(s) -> {typesToReport.Count} distinct type(s).");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
