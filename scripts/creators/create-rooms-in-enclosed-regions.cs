// ============================================================
// FRAGMENT (creator) — create-rooms-in-enclosed-regions.cs
// PURPOSE: Fill every enclosed region on a level with a Room, in one pass — and REUSE the project's
//          existing UNPLACED rooms before creating new ones, so you don't end up with orphaned "Room 1"
//          elements sitting in the browser next to a fresh duplicate.
//          The unblocker for everything that needs real rooms: room-by-room routing, coverage layouts,
//          `filter-by-room.cs`, room schedules.
// PRODUCES: elements (List<Element>, the rooms now placed), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// WHY THIS EXISTS SEPARATELY FROM create-room.cs: that one places a room AT A POINT you supply, and needs
//   you to know the point. This one asks Revit which regions are actually enclosed
//   (`Document.get_PlanTopology(level).Circuits`) and fills them all — no coordinates needed. It is also
//   the only one that can rescue an existing unplaced room, which `NewRoom(level, UV)` cannot do.
//
// GOTCHA: an UNPLACED room (Area = 0) is a real element that encloses nothing. Filters, coverage and
//         room grouping all silently return "(none)" for everything until it is placed. That is a very
//         easy hour to lose — if room-based anything comes back empty, check Area > 0 FIRST.
// GOTCHA: `PlanCircuit.IsRoomLocated` tells you a region is already taken; skipping those is what makes
//         this safe to re-run. It will not double up.
// GOTCHA: it only fills what Revit considers ENCLOSED. A gap in the walls means no circuit and no room —
//         the count of empty circuits is reported so a missing region is visible rather than mysterious.
// ✓ LIVE-VERIFIED 2026-07-26 on Project1 — 3 enclosed regions, 3 unplaced rooms, all three reused and
//   placed in one pass: 238.6 / 251.0 / 270.8 m2. Room-based grouping worked immediately afterwards.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int levelIdInt = 0;                 // 0 = the lowest level in the project
bool reuseUnplacedRooms = true;     // true = rescue existing Area-0 rooms before creating new ones
bool createNewIfNeeded = true;      // true = create fresh rooms for regions left over
string namePrefix = "";             // optional — rename each placed room to prefix + running number
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();
Func<double,double> toM2 = v => v / 10.763910416709722;

var level = levelIdInt != 0
    ? Document.GetElement(new ElementId(levelIdInt)) as Level
    : new FilteredElementCollector(Document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();

if (level == null)
{
    sb.AppendLine(levelIdInt != 0 ? $"levelIdInt {levelIdInt} is not a Level." : "No Level in this project.");
}
else
{
    var unplaced = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms)
        .WhereElementIsNotElementType().Cast<Autodesk.Revit.DB.Architecture.Room>()
        .Where(r => r.Area <= 0).OrderBy(r => r.Id).ToList();

    using (var t = new Transaction(Document, "AJ Tools - Fill Enclosed Regions With Rooms"))
    {
        t.Start();
        try
        {
            var topo = Document.get_PlanTopology(level);
            int total = 0, alreadyTaken = 0, reused = 0, created = 0, skipped = 0;
            int nextUnplaced = 0, counter = 1;

            foreach (PlanCircuit circuit in topo.Circuits)
            {
                total++;
                if (circuit.IsRoomLocated) { alreadyTaken++; continue; }

                Autodesk.Revit.DB.Architecture.Room placed = null;
                if (reuseUnplacedRooms && nextUnplaced < unplaced.Count)
                {
                    placed = Document.Create.NewRoom(unplaced[nextUnplaced], circuit);   // rescue an existing one
                    nextUnplaced++; reused++;
                }
                else if (createNewIfNeeded)
                {
                    placed = Document.Create.NewRoom(null, circuit);                     // brand new
                    created++;
                }
                else { skipped++; continue; }

                if (placed != null)
                {
                    if (!string.IsNullOrEmpty(namePrefix))
                    { try { placed.get_Parameter(BuiltInParameter.ROOM_NAME)?.Set($"{namePrefix}{counter}"); } catch { } }
                    elements.Add(placed);
                    counter++;
                }
            }

            t.Commit();
            sb.AppendLine($"Level '{level.Name}': {total} enclosed region(s) — {alreadyTaken} already had a room, "
                + $"{reused} existing unplaced room(s) reused, {created} new room(s) created"
                + (skipped > 0 ? $", {skipped} left empty (createNewIfNeeded was false)" : "") + ".");
            foreach (var e in elements)
            {
                var rm = e as Autodesk.Revit.DB.Architecture.Room;
                sb.AppendLine($"  - '{rm.Name}' (Id {rm.Id}) {toM2(rm.Area):F1} m2");
            }
            if (reuseUnplacedRooms && nextUnplaced < unplaced.Count)
                sb.AppendLine($"  {unplaced.Count - nextUnplaced} unplaced room(s) still left over — more rooms than enclosed regions.");
            if (total == 0)
                sb.AppendLine("  No enclosed regions at all — the walls do not form a closed loop on this level.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED — rolled back, no room placed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
