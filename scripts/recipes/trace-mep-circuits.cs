// ============================================================
// SCRIPT: trace-mep-circuits.cs
// PURPOSE: Trace real physical MEP circuits for a filtered pipe/duct system type, when tags/naming
//          and Connector.IsConnected can't be trusted. Bulk-clusters the whole filtered set at once
//          (not one named path at a time), finds each circuit's open ends, and matches those to the
//          nearest real equipment.
// SOURCE:  ../../knowledge/live-model/mep-trace.md § Tracing real MEP connectivity (when tags/naming can't be trusted)
// STATUS:  living document — refine in place, don't fork a v2 file.
// LIVE-VERIFIED 2026-07-23 — FOUND AND FIXED A REAL BUG: the category-union collector below applied
// `.WhereElementIsNotElementType()` to EACH category before `.UnionWith(...)` — confirmed via isolated
// testing that `FilteredElementCollector.UnionWith()` does not preserve a quick-filter applied to either
// side before the union; the merged result silently included every TYPE element too (52 elements instead
// of the real 4 instances, in a live A/B/C test). Fixed by moving `.WhereElementIsNotElementType()` to run
// ONCE, after all four UnionWith calls, on the combined collector — this ordering is confirmed correct.
// Same root cause as filter-by-system-type.cs and filter-by-system-name.cs, fixed there too — see those
// files' headers for the full investigation.
// ============================================================
// The system-name filter is ALWAYS an input — never hardcode it. Check glossary.md for the mapping
// from the user's word to the real Revit system-type name(s) (e.g. "refrigerant" -> anything containing "DXS").

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string systemNameContains = "DXS";  // e.g. "DXS" (refrigerant), "CDP" (condensate), "WSP" (water supply)
double clusterToleranceFt = 50 / 304.8; // ~50mm worked well
// (a maxHops input used to sit here — removed 2026-08-25: it was declared and never read. This recipe
//  clusters by union-find, which has no hops to limit; the hop-walking sibling that DOES enforce it is
//  recipes/verify-duct-connectivity.cs.)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// Step 1 — collect every pipe + fitting whose system name/type matches the filter.
var candidates = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_PipeCurves)
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_PipeFitting))
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctCurves))
    .UnionWith(new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_DuctFitting))
    .WhereElementIsNotElementType()
    .Where(e =>
    {
        // Match against System NAME and system TYPE together. The old form fell back at the
        // PARAMETER-OBJECT level (`?? get_Parameter(TYPE)`), and every collected category HAS a
        // System Name parameter — so the type fallback was dead code and a type abbreviation the
        // name did not embed returned "Collected 0 element(s)" as if it were a fact (found 2026-08-25).
        var name = string.Join("|", new[] {
            e.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM)?.AsString(),
            e.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString(),
            (Document.GetElement(e.GetTypeId()) as Element)?.Name
        }.Where(s => !string.IsNullOrEmpty(s)));
        return name.IndexOf(systemNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
    })
    .ToList();

sb.AppendLine($"Collected {candidates.Count} element(s) matching system filter '{systemNameContains}'.");
if (candidates.Count == 0) return sb.ToString();
// (elemConnectors count is reported after gathering, so an element dropped for having no
// ConnectorManager is visible rather than silently missing from the clustering.)

// Gather all connectors per element up front.
var elemConnectors = new Dictionary<ElementId, List<Connector>>();
foreach (var e in candidates)
{
    var mgr = (e as MEPCurve)?.ConnectorManager
        ?? (e is FamilyInstance fi ? fi.MEPModel?.ConnectorManager : null);
    if (mgr == null) continue;
    elemConnectors[e.Id] = mgr.Connectors.Cast<Connector>().ToList();
}

// Step 2 — bulk-cluster by matching connector positions within tolerance (fallback path; try the
// fast path first in a real run by sampling Connector.IsConnected within one MEPSystem — omitted here
// for brevity, see ../../knowledge/live-model/mep-trace.md for that shortcut).
var parent = elemConnectors.Keys.ToDictionary(id => id, id => id); // union-find
Func<ElementId, ElementId> find = null;
find = id => parent[id] == id ? id : (parent[id] = find(parent[id]));
Action<ElementId, ElementId> union = (a, b) => { var ra = find(a); var rb = find(b); if (ra != rb) parent[ra] = rb; };

var allIds = elemConnectors.Keys.ToList();
for (int i = 0; i < allIds.Count; i++)
{
    for (int j = i + 1; j < allIds.Count; j++)
    {
        bool touches = elemConnectors[allIds[i]].Any(c1 =>
            elemConnectors[allIds[j]].Any(c2 => c1.Origin.DistanceTo(c2.Origin) <= clusterToleranceFt));
        if (touches) union(allIds[i], allIds[j]);
    }
}

var circuits = allIds.GroupBy(id => find(id)).ToList();
if (elemConnectors.Count < candidates.Count)
    sb.AppendLine($"  ({candidates.Count - elemConnectors.Count} of the collected elements have no ConnectorManager and are not in the clustering.)");
sb.AppendLine($"Grouped into {circuits.Count} physical circuit(s).");

// Step 3+4 — for each circuit, find open ends and match to nearest equipment.
var equipment = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
    .WhereElementIsNotElementType()
    .ToList();

int idx = 0;
foreach (var circuit in circuits)
{
    idx++;
    var members = circuit.ToList();
    var openConnectors = new List<Connector>();
    foreach (var id in members)
    {
        foreach (var c in elemConnectors[id])
        {
            bool hasNeighborInCircuit = members.Any(otherId => otherId != id &&
                elemConnectors[otherId].Any(oc => oc.Origin.DistanceTo(c.Origin) <= clusterToleranceFt));
            if (!hasNeighborInCircuit) openConnectors.Add(c);
        }
    }

    sb.AppendLine($"Circuit {idx}: {members.Count} element(s), {openConnectors.Count} open end(s).");
    if (equipment.Count == 0 && openConnectors.Count > 0)
    {
        // Without this guard, best stays double.MaxValue and the line below printed
        // "Open end near unknown (Id ), distance ∞mm" — nonsense reported as a match (found 2026-08-25).
        sb.AppendLine("  (no Mechanical Equipment in the model to match open ends against — a normal state before FCUs are placed.)");
        continue;
    }
    foreach (var oc in openConnectors)
    {
        Element nearestEquip = null;
        double best = double.MaxValue;
        foreach (var eq in equipment)
        {
            var mepModel = (eq as FamilyInstance)?.MEPModel;
            var eqConnectors = mepModel?.ConnectorManager?.Connectors?.Cast<Connector>().ToList();
            double dist;
            if (eqConnectors != null && eqConnectors.Any())
            {
                dist = eqConnectors.Min(ec => ec.Origin.DistanceTo(oc.Origin));
            }
            else
            {
                // No connectors on this equipment (common for outdoor/condensing units) — fall back to bbox distance.
                var bbox = eq.get_BoundingBox(null);
                if (bbox == null) continue;
                var center = (bbox.Min + bbox.Max) * 0.5;
                dist = center.DistanceTo(oc.Origin);
            }
            if (dist < best) { best = dist; nearestEquip = eq; }
        }
        if (nearestEquip == null)
        {
            sb.AppendLine("  Open end — no matchable equipment (every candidate lacked both connectors and a bounding box).");
            continue;
        }
        string equipName = nearestEquip.LookupParameter("Equipment Tag")?.AsString()
            ?? nearestEquip.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()
            ?? nearestEquip.Name ?? "unknown";
        sb.AppendLine($"  Open end near {equipName} (Id {nearestEquip.Id}), distance {best * 304.8:F0}mm.");
    }
}

sb.AppendLine("Verify each pairing before reporting as fact — check glossary.md for an already-confirmed pattern first.");
return sb.ToString();
