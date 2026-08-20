// ============================================================
// FRAGMENT (filter) — filter-by-insulation-type.cs
// PURPOSE: The insulation/lining elements THEMSELVES (Duct Insulation, Duct Lining, Pipe Insulation —
//          all covered, not just Lining), narrowed by Type name, Material, and/or thickness (size in
//          mm). Answers "give me all 25mm insulation" or "give me all insulation using Material X". Set
//          resolveToHost = true to get back the underlying pipe/duct/fitting instead, e.g. to then
//          color-code the HOSTS by their insulation thickness. For a plain insulated/bare split with no
//          size/type/material condition, use filter-by-insulation-status.cs instead — cheaper, simpler.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: ✓ LIVE-VERIFIED — first 2026-08-06 (add-then-rollback, every input exercised), re-verified
//         2026-08-14. Both `Thickness` and `HostElementId` DO resolve on 2020 — the
//         caveat is answered, not outstanding. Against 4x 25 mm duct insulation: a 20–30 mm band returned
//         all 4, `resolveToHost = true` returned the 4 host Ducts, and a >=50 mm band returned 0 — that
//         last one deliberately, as a negative control proving the thickness test actually excludes rather
//         than passing everything through. A filter that only ever returns "all" looks correct until the
//         day it matters.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string kind = "any"; // "any", "ductinsulation", "ductlining", "pipeinsulation"
string typeNameContains = ""; // "" = no Type-name filter
string materialNameContains = ""; // "" = no Material filter
double minThicknessMm = 0; // 0 = no lower bound
double maxThicknessMm = 0; // 0 = no upper bound (treated as "no max" when 0)
bool resolveToHost = false; // true = return the underlying pipe/duct/fitting instead of the insulation/lining element itself
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

var candidates = new List<Autodesk.Revit.DB.InsulationLiningBase>();
if (kind == "any" || kind == "ductinsulation")
    candidates.AddRange(new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctInsulation)).Cast<Autodesk.Revit.DB.InsulationLiningBase>());
if (kind == "any" || kind == "ductlining")
    candidates.AddRange(new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Mechanical.DuctLining)).Cast<Autodesk.Revit.DB.InsulationLiningBase>());
if (kind == "any" || kind == "pipeinsulation")
    candidates.AddRange(new FilteredElementCollector(Document).OfClass(typeof(Autodesk.Revit.DB.Plumbing.PipeInsulation)).Cast<Autodesk.Revit.DB.InsulationLiningBase>());

double minThicknessFt = minThicknessMm / 304.8;
double maxThicknessFt = maxThicknessMm > 0 ? maxThicknessMm / 304.8 : double.MaxValue;

var matched = candidates.Where(il =>
{
    try
    {
        if (!string.IsNullOrEmpty(typeNameContains))
        {
            var type = Document.GetElement(il.GetTypeId()) as ElementType;
            if (type == null || type.Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) < 0) return false;
        }
        if (!string.IsNullOrEmpty(materialNameContains))
        {
            var matIds = il.GetMaterialIds(false);
            bool matchesMaterial = matIds != null && matIds.Any(id => (Document.GetElement(id) as Material)?.Name?.IndexOf(materialNameContains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!matchesMaterial) return false;
        }
        if (il.Thickness < minThicknessFt || il.Thickness > maxThicknessFt) return false;
        return true;
    }
    catch { return false; }
}).ToList();

List<Element> elements;
if (resolveToHost)
{
    elements = matched
        .Select(il => { try { return Document.GetElement(il.HostElementId); } catch { return null; } })
        .Where(e => e != null)
        .Distinct()
        .ToList();
}
else
{
    elements = matched.Cast<Element>().ToList();
}

var parts = new List<string>();
if (!string.IsNullOrEmpty(typeNameContains)) parts.Add($"type contains \"{typeNameContains}\"");
if (!string.IsNullOrEmpty(materialNameContains)) parts.Add($"material contains \"{materialNameContains}\"");
if (minThicknessMm > 0 || maxThicknessMm > 0) parts.Add($"thickness {minThicknessMm}-{(maxThicknessMm > 0 ? maxThicknessMm.ToString() : "∞")}mm");
sb.AppendLine($"Filtered {elements.Count} {(resolveToHost ? "host" : "insulation/lining")} element(s), kind: {kind}" + (parts.Count > 0 ? ", " + string.Join(", ", parts) : "") + ".");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
