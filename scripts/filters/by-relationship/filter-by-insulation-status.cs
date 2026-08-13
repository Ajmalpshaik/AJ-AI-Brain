// ============================================================
// FRAGMENT (filter) — filter-by-insulation-status.cs
// PURPOSE: Pipe/duct/fitting elements that HAVE insulation (and/or lining) applied, or DON'T — QA sweep
//          ("which ducts still need insulation"). Checks the real InsulationLiningBase.HostElementId
//          relationship, not a parameter guess. For filtering the insulation/lining elements THEMSELVES
//          by size/type/material, use filter-by-insulation-type.cs instead — this one answers "is it
//          insulated", that one answers "what kind of insulation is it".
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: ✓ LIVE-VERIFIED — first 2026-08-06 (add-then-rollback, both branches flipped), re-verified
//         2026-08-14 against a fixture left standing in the model. `InsulationLiningBase.HostElementId`
//         DOES resolve on 2020; the old caveat is answered, not outstanding. 2026-08-14 run: 17 ducts of
//         which 4 insulated — wantInsulated=true returned exactly those 4 ids, false returned exactly 13.
//         Fixture built by action-add-remove-insulation.cs, which had itself been wrongly marked blocked
//         for "no insulation fixture" — it CREATES the fixture. Two ducts are deliberately left insulated
//         in the test model so this stays re-runnable without rebuilding anything.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
BuiltInCategory targetCategory = BuiltInCategory.OST_DuctCurves; // the HOST category being checked — OST_DuctCurves, OST_PipeCurves, OST_DuctFitting, OST_PipeFitting, etc.
bool wantInsulated = true; // true = has insulation and/or lining; false = bare, has neither
bool countLiningAsInsulated = true; // true = Duct Lining counts as "insulated" too, not just external Insulation
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// Build the host->has-covering lookup ONCE (reverse scan), instead of re-scanning per candidate.
var insulatedHostIds = new HashSet<int>();
foreach (Autodesk.Revit.DB.InsulationLiningBase il in new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctInsulation)))
{
    try { insulatedHostIds.Add(il.HostElementId.IntegerValue); } catch { }
}
foreach (Autodesk.Revit.DB.InsulationLiningBase il in new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeInsulation)))
{
    try { insulatedHostIds.Add(il.HostElementId.IntegerValue); } catch { }
}
if (countLiningAsInsulated)
{
    foreach (Autodesk.Revit.DB.InsulationLiningBase il in new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctLining)))
    {
        try { insulatedHostIds.Add(il.HostElementId.IntegerValue); } catch { }
    }
}

List<Element> elements = new FilteredElementCollector(Document)
    .OfCategory(targetCategory)
    .WhereElementIsNotElementType()
    .Where(e => insulatedHostIds.Contains(e.Id.IntegerValue) == wantInsulated)
    .ToList();

sb.AppendLine($"Filtered {elements.Count} element(s) in category {targetCategory}, {(wantInsulated ? "insulated" : "bare (no insulation/lining)")}" +
    (countLiningAsInsulated ? " (Lining counted as insulated)" : " (Lining NOT counted as insulated)") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
