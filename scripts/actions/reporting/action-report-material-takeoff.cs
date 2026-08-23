// ============================================================
// FRAGMENT (action) — action-report-material-takeoff.cs
// PURPOSE: Sum material area/volume across every element in `elements`, grouped by material name —
//          a quantities/takeoff report. Read-only, no transaction needed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ PAINT AND GEOMETRY ARE TWO SEPARATE MATERIAL SETS, and this fragment used to report only one.
//    `GetMaterialIds(false)` returns the materials in the element's geometry and compound structure.
//    `GetMaterialIds(true)` returns PAINT materials — the ones applied face-by-face with Modify > Paint.
//    They are different sets, and the area call must be given the SAME flag: `GetMaterialArea(id, true)`
//    for a painted face, `GetMaterialArea(id, false)` for a geometric one. Reading only the geometry set
//    made a painted finish report as zero — which on a finishes schedule reads as "that material is not
//    in the model" rather than "I did not ask for it". Fixed 2026-08-23; both sets are reported now and
//    each row says which it came from.
//
// ✱✱ PAINT HAS AREA BUT NEVER VOLUME. There is no paint overload of GetMaterialVolume — paint is a
//    surface, not a body. A painted material therefore shows an area with a BLANK volume, and that blank
//    is the correct answer, not missing data. Sorting by volume would bury every painted finish at the
//    bottom of the table, so this sorts by area.
//
// GOTCHA: the numbers come from Revit's own material takeoff engine, so they agree with a Material
//         Takeoff schedule. They are NOT recomputed from geometry here.
// RELATED: action-purge-unused.cs mode="materials" had the identical paint blind spot — it could offer a
//          paint-only material for deletion — and is fixed in the same pass. filter-by-material.cs
//          already had an includePaint switch and was correct.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool includePaint = true;     // false = geometry/compound-structure materials only (the old behaviour)
bool includeGeometry = true;  // false = painted finishes only
// ---- END INPUTS ----

// name -> (geometry area, paint area, volume). Kept apart so a material that is BOTH painted somewhere
// and structural somewhere else does not silently merge two different quantities into one number.
var totals = new Dictionary<string, (double geomAreaSqm, double paintAreaSqm, double volumeCum)>();
int skipped = 0;

const double SQFT_PER_SQM = 10.763910416709722;
const double CUFT_PER_CUM = 35.314666721488595;

foreach (var e in elements)
{
    ICollection<ElementId> geometryIds = null, paintIds = null;
    if (includeGeometry) { try { geometryIds = e.GetMaterialIds(false); } catch { } }
    if (includePaint)    { try { paintIds    = e.GetMaterialIds(true);  } catch { } }

    int found = (geometryIds == null ? 0 : geometryIds.Count) + (paintIds == null ? 0 : paintIds.Count);
    if (found == 0) { skipped++; continue; }

    if (geometryIds != null)
    {
        foreach (var matId in geometryIds)
        {
            string name = (Document.GetElement(matId) as Material)?.Name ?? "Unknown";
            double areaFt2 = 0, volumeFt3 = 0;
            try { areaFt2 = e.GetMaterialArea(matId, false); } catch { }
            try { volumeFt3 = e.GetMaterialVolume(matId); } catch { }
            totals.TryGetValue(name, out var existing);
            totals[name] = (existing.geomAreaSqm + areaFt2 / SQFT_PER_SQM,
                            existing.paintAreaSqm,
                            existing.volumeCum + volumeFt3 / CUFT_PER_CUM);
        }
    }

    if (paintIds != null)
    {
        foreach (var matId in paintIds)
        {
            string name = (Document.GetElement(matId) as Material)?.Name ?? "Unknown";
            double areaFt2 = 0;
            // true = the PAINTED area of this material on this element. No volume call exists for paint.
            try { areaFt2 = e.GetMaterialArea(matId, true); } catch { }
            totals.TryGetValue(name, out var existing);
            totals[name] = (existing.geomAreaSqm,
                            existing.paintAreaSqm + areaFt2 / SQFT_PER_SQM,
                            existing.volumeCum);
        }
    }
}

sb.AppendLine($"Material takeoff across {elements.Count} element(s) ({skipped} skipped, no material data):");
if (!includePaint) sb.AppendLine("(paint materials EXCLUDED by input — painted finishes will not appear)");
if (!includeGeometry) sb.AppendLine("(geometry materials EXCLUDED by input — only painted finishes appear)");
sb.AppendLine("Material | Area (sqm) | of which painted (sqm) | Volume (cum)");
sb.AppendLine("--- | --- | --- | ---");
foreach (var kv in totals.OrderByDescending(kv => kv.Value.geomAreaSqm + kv.Value.paintAreaSqm))
{
    double totalArea = kv.Value.geomAreaSqm + kv.Value.paintAreaSqm;
    string paintCell = kv.Value.paintAreaSqm > 0 ? kv.Value.paintAreaSqm.ToString("F2") : "-";
    // blank, not 0.00 — a paint-only material genuinely has no volume, and 0.00 would read as measured
    string volumeCell = kv.Value.volumeCum > 0 ? kv.Value.volumeCum.ToString("F2") : "-";
    sb.AppendLine($"{kv.Key} | {totalArea:F2} | {paintCell} | {volumeCell}");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
