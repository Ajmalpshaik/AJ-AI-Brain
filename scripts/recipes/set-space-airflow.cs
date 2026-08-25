// ============================================================
// SCRIPT: set-space-airflow.cs
// PURPOSE: Create/find the MEP Space for each room on a level, set its Specified Supply/Return
//          Airflow from a thumb-rule, and cascade the new total to any air terminals already placed
//          in that room (refresh their Flow parameter, don't change count/position).
// SOURCE:  ../../knowledge/live-model/hvac-terminals.md § HVAC air terminal layout — Space airflow parameters
// STATUS:  living document — refine in place, don't fork a v2 file.
// ============================================================
// Backs the ajtools-hvac-space-airflow skill. Every number below is a per-request input — restate
// what you're using before running, never assume last session's rule still applies.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
ElementId levelId = (Document.ActiveView as ViewPlan)?.GenLevel?.Id ?? ElementId.InvalidElementId; // or set explicitly
// (cast first — GenLevel is a ViewPlan member; and note the trap: if the active view is a plan of the
//  WRONG level, every write lands on that level's rooms. The first report line names the level — read it.)
double cfmPerTon = 400;          // e.g. "14 sqm = 1 ton = 400 cfm"
double sqmPerTon = 14.0;
double returnAirflowFraction = 0.90; // e.g. "return 10% lower" -> 0.90
// ---- END INPUTS ----

if (levelId == ElementId.InvalidElementId)
{
    return "No active level resolved — set levelId explicitly in INPUTS.";
}

var sb = new System.Text.StringBuilder();

var rooms = new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.OST_Rooms)
    .WhereElementIsNotElementType()
    .Cast<Autodesk.Revit.DB.Architecture.Room>()
    .Where(r => r.LevelId == levelId && r.Area > 0)
    .ToList();

Func<FamilyInstance, string> terminalAirflowKind = fi =>
{
    var tokens = new List<string>();
    foreach (var paramName in new[] { "System Classification", "System Type", "System Name" })
    {
        var p = fi.LookupParameter(paramName);
        if (p == null) continue;
        tokens.Add(p.AsString() ?? p.AsValueString() ?? "");
    }

    tokens.Add(fi.Symbol?.Family?.Name ?? "");
    tokens.Add(fi.Symbol?.Name ?? "");

    var connectorSet = fi.MEPModel?.ConnectorManager?.Connectors;
    if (connectorSet != null)
    {
        var hvacConnectors = connectorSet.Cast<Connector>()
            .Where(c => c.Domain == Domain.DomainHvac)
            .ToList();
        tokens.AddRange(hvacConnectors.Select(c => c.DuctSystemType.ToString()));
    }

    foreach (var token in tokens.Where(tk => !string.IsNullOrWhiteSpace(tk)))
    {
        if (token.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0) return "return";
        if (token.IndexOf("supply", StringComparison.OrdinalIgnoreCase) >= 0) return "supply";
    }
    return "unknown";
};

string levelName = (Document.GetElement(levelId) as Level)?.Name ?? "?";
sb.AppendLine($"{rooms.Count} room(s) on level '{levelName}'.");
if (rooms.Count == 0 && new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance)).Any())
    sb.AppendLine("  NOTE: this document has linked model(s) and no rooms of its own on this level — on a"
        + " coordination model the rooms live in the architectural LINK, which this recipe cannot read."
        + " A zero here is NOT evidence the level has no rooms.");

using (var t = new Transaction(Document, "AJ Tools - Update Space Airflow"))
{
    t.Start();
    try
    {

    foreach (var room in rooms)
    {
        double areaSqm = room.Area / 10.763910416709722;
        double tons = areaSqm / sqmPerTon;
        double supplyCfm = tons * cfmPerTon;
        double supplyLs = supplyCfm / 60.0;
        double returnCfm = supplyCfm * returnAirflowFraction;
        double returnLsInternal = returnCfm / 60.0;

        // Find or create the Space at this room's location.
        // A Space with no LocationPoint is UNPLACED — it must never match. The old fallback tested
        // XYZ.Zero against the room, so an orphaned Space got the airflow of whichever room happened
        // to contain the project origin, while the room's real Space stayed untouched (2026-08-25).
        var existingSpace = new FilteredElementCollector(Document)
            .OfCategory(BuiltInCategory.OST_MEPSpaces)
            .WhereElementIsNotElementType()
            .Cast<Autodesk.Revit.DB.Mechanical.Space>()
            .FirstOrDefault(s => s.Location is LocationPoint lp && room.IsPointInRoom(lp.Point));

        Autodesk.Revit.DB.Mechanical.Space space = existingSpace;
        if (space == null)
        {
            var roomCenter = (room.Location as LocationPoint)?.Point ?? XYZ.Zero;
            var uv = new UV(roomCenter.X, roomCenter.Y);
            space = Document.Create.NewSpace(Document.GetElement(levelId) as Level, uv);
        }

        // Capture the write results — a missing parameter or a refused Set must show in the report,
        // not vanish behind `?.` while the summary line still quotes the numbers as written (2026-08-25).
        bool wroteSupply = space.get_Parameter(BuiltInParameter.ROOM_DESIGN_SUPPLY_AIRFLOW_PARAM)?.Set(supplyLs) ?? false;
        space.ReturnAirflow = Autodesk.Revit.DB.Mechanical.ReturnAirflowType.Specified;
        bool wroteReturn = space.get_Parameter(BuiltInParameter.ROOM_DESIGN_RETURN_AIRFLOW_PARAM)?.Set(returnLsInternal) ?? false;
        if (!wroteSupply || !wroteReturn)
            sb.AppendLine($"  WARNING {room.Name}: airflow write refused or parameter missing on Space Id {space.Id}"
                + $" (supply written: {wroteSupply}, return written: {wroteReturn}).");

        // Cascade to already-placed terminals in this room.
        var terminals = new FilteredElementCollector(Document)
            .OfCategory(BuiltInCategory.OST_DuctTerminal)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Where(fi =>
            {
                var loc = (fi.Location as LocationPoint)?.Point;
                if (loc == null) return false;
                // Room.IsPointInRoom is a 3D check — use the room's own Z if the terminal sits above/below it.
                var testPt = new XYZ(loc.X, loc.Y, (room.Location as LocationPoint)?.Point.Z ?? loc.Z);
                return room.IsPointInRoom(testPt);
            })
            .ToList();

        var supplyTerms = terminals.Where(fi => terminalAirflowKind(fi) == "supply").ToList();
        var returnTerms = terminals.Where(fi => terminalAirflowKind(fi) == "return").ToList();
        int unknownCount = terminals.Count - supplyTerms.Count - returnTerms.Count;

        int refreshed = 0;
        Action<IEnumerable<FamilyInstance>, double> setFlow = (targetTerminals, cfmEach) =>
        {
            double internalFlow = cfmEach / 60.0;
            foreach (var term in targetTerminals)
            {
                var flowParam = term.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM);
                if (flowParam != null && !flowParam.IsReadOnly)
                {
                    flowParam.Set(internalFlow);
                    refreshed++;
                }
            }
        };

        if (supplyTerms.Count > 0)
        {
            setFlow(supplyTerms, supplyCfm / supplyTerms.Count);
        }
        if (returnTerms.Count > 0)
        {
            setFlow(returnTerms, returnCfm / returnTerms.Count);
        }
        if (terminals.Count > 0 && supplyTerms.Count == 0 && returnTerms.Count == 0)
        {
            sb.AppendLine($"  WARNING: {terminals.Count} terminal(s) found, but none could be classified as supply or return - Flow left unchanged.");
        }

        sb.AppendLine($"{room.Name} {room.Number}: {areaSqm:F1} sqm, supply {supplyCfm:F0} CFM, return {returnCfm:F0} CFM, {refreshed}/{terminals.Count} terminal(s) refreshed ({supplyTerms.Count} supply, {returnTerms.Count} return, {unknownCount} unknown).");
    }

    t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        return sb.AppendLine("FAILED: " + ex.Message).ToString();
    }
}

return sb.ToString();
