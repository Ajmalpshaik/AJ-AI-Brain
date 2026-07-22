// ============================================================
// FRAGMENT (filter) — filter-by-electrical-system.cs
// PURPOSE: Every element belonging to a specific Electrical System (circuit) — matched by its Circuit
//          Type (Power/Lighting/Data/etc.) and/or its own circuit name. Electrical Systems work
//          differently from Piping/Duct systems — the Type is a fixed enum, not a document element — so
//          this is a separate fragment rather than an extension of filter-by-system-type.cs/
//          filter-by-system-name.cs.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: not yet live-verified — no electrical work done in this model yet to confirm against.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string circuitTypeContains = ""; // e.g. "Power", "Lighting", "Data" — "" = any circuit type
string circuitNameContains = ""; // e.g. "Panel A" — "" = any circuit name
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

if (string.IsNullOrEmpty(circuitTypeContains) && string.IsNullOrEmpty(circuitNameContains))
{
    sb.AppendLine("No filter specified — set circuitTypeContains and/or circuitNameContains.");
}
else
{
    var matchingSystems = new FilteredElementCollector(Document)
        .OfClass(typeof(Autodesk.Revit.DB.Electrical.ElectricalSystem))
        .Cast<Autodesk.Revit.DB.Electrical.ElectricalSystem>()
        .Where(s =>
        {
            bool typeOk = string.IsNullOrEmpty(circuitTypeContains) || s.SystemType.ToString().IndexOf(circuitTypeContains, StringComparison.OrdinalIgnoreCase) >= 0;
            bool nameOk = string.IsNullOrEmpty(circuitNameContains) || (s.Name ?? "").IndexOf(circuitNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
            return typeOk && nameOk;
        })
        .ToList();

    elements = matchingSystems.SelectMany(s => s.Elements.Cast<Element>()).Distinct().ToList();

    var parts = new List<string>();
    if (!string.IsNullOrEmpty(circuitTypeContains)) parts.Add($"type contains \"{circuitTypeContains}\"");
    if (!string.IsNullOrEmpty(circuitNameContains)) parts.Add($"name contains \"{circuitNameContains}\"");
    sb.AppendLine($"Filtered {elements.Count} element(s) across {matchingSystems.Count} matching Electrical System(s), {string.Join(", ", parts)}.");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
