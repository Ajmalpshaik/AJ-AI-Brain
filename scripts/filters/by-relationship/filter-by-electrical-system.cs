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
// STATUS: ✓ LIVE-VERIFIED 2026-08-14 against a purpose-built circuit — the "no electrical work in this
//         model" blocker was cleared by BUILDING one, not by waiting for one. Fixture: M_Duplex
//         Receptacle + M_Lighting and Appliance Panelboard loaded from the stock US Metric library, then
//         `ElectricalSystem.Create(doc, [receptacle], PowerCircuit)` + `SelectPanel(panel)`.
//         circuitTypeContains="Power" returned exactly the receptacle across 1 system; "Data" returned 0
//         (negative control — proves the type test excludes rather than passing everything).
// GOTCHA: `ElectricalSystem.Elements` returns the LOADS on the circuit, NOT the panel feeding it. The
//         verified run returned 1 element (the receptacle) for a circuit that plainly involves 2 pieces of
//         equipment. Use `.PanelName` / `.BaseEquipment` for the panel — do not report "1 element on the
//         circuit" as if the panel were missing.
// GOTCHA: only the **MEP** library families carry electrical connectors. The Architectural ones
//         (`...\Electrical\Architectural\...\M_Electrical Panel.rfa`, `M_Outlet-Duplex.rfa`) place fine
//         but their `MEPModel.ConnectorManager` is NULL, and `ElectricalSystem.Create` then fails with
//         "There should be at least one component that can create the specified circuit type". Take
//         terminals/distribution from `...\Electrical\MEP\Electric Power\...` instead.
// GOTCHA: on a panelboard, `Connector.IsConnected` THROWS "Connection status is available only for
//         connectors of PhysicalConn type" — its 6 connectors include CableTrayConduit MasterSurface ones.
//         Guard every per-connector read by `Domain`/`ConnectorType` before touching it.
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
