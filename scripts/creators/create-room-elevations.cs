// ============================================================
// FRAGMENT (creator) — create-room-elevations.cs
// PURPOSE: Place an ElevationMarker at a room's center (or an explicit mm point) in a plan view and
//          create 1–4 interior elevation views around it — the "room elevations" job create-view.cs
//          deliberately excludes. Fills the Elevation slot of the ViewFamily set.
// PRODUCES: elements (List<Element>, the newly created elevation Views), sb
// NOT STANDALONE — see scripts/README.md for how to compose. Chain action-rename-element.cs /
//          action-apply-view-template.cs / action-place-viewport-on-sheet.cs onto the result.
// GOTCHA: the marker's 4 slots (index 0–3) point in Revit's default marker orientation — they are NOT
//         auto-rotated to face the room's walls. If the room is rotated, rotate the marker element
//         afterwards (it's a normal element; action-rotate-elements.cs works on it) — the views follow.
// GOTCHA: planViewIdInt must be a ViewPlan (floor/ceiling plan) that is NOT a view template; the marker
//         is visible in that view and the elevations inherit its phase.
// LIVE-VERIFIED 2026-08-14 — marker placed on the room's true centre, all 4 slots producing views at
//         1:50; the project's ViewSection count going 16 -> 20 was confirmed from a SEPARATE bridge call
//         rather than from this script's own report.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int roomIdInt = 0;              // Room whose center gets the marker; 0 = use centerXMm/centerYMm instead
double centerXMm = 0, centerYMm = 0; // only used when roomIdInt == 0
int planViewIdInt = 0;          // the ViewPlan the marker is placed in (required)
int elevationCount = 4;         // 1..4 — how many of the marker's 4 slots to fill (indices 0..count-1)
int scaleDenominator = 50;      // view scale, e.g. 50 → 1:50
string namePrefix = null;       // null = keep Revit's default names; else "<prefix> 1", "<prefix> 2", ...
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

var planView = Document.GetElement(new ElementId(planViewIdInt)) as ViewPlan;
if (planView == null || planView.IsTemplate)
{
    sb.AppendLine($"planViewIdInt {planViewIdInt} is not a usable ViewPlan (not found, or a view template).");
}
else if (elevationCount < 1 || elevationCount > 4)
{
    sb.AppendLine($"elevationCount {elevationCount} is out of range — a marker has exactly 4 slots (use 1..4).");
}
else
{
    XYZ center = null;
    if (roomIdInt != 0)
    {
        var room = Document.GetElement(new ElementId(roomIdInt)) as Autodesk.Revit.DB.Architecture.Room;
        if (room == null) sb.AppendLine($"roomIdInt {roomIdInt} is not a valid Room.");
        else
        {
            var lp = room.Location as LocationPoint;
            if (lp != null) center = lp.Point;
            else
            {
                var bb = room.get_BoundingBox(null);
                if (bb != null) center = (bb.Min + bb.Max) / 2.0;
                else sb.AppendLine($"Room {roomIdInt} has no location point and no bounding box — is it placed?");
            }
        }
    }
    else
    {
        center = new XYZ(mm(centerXMm), mm(centerYMm), 0);
    }

    if (center != null)
    {
        var vft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(v => v.ViewFamily == ViewFamily.Elevation);
        if (vft == null)
        {
            sb.AppendLine("No Elevation ViewFamilyType found in the project.");
        }
        else
        {
            using (var t = new Transaction(Document, "AJ Tools - Create Room Elevations"))
            {
                t.Start();
                try
                {
                    var marker = ElevationMarker.CreateElevationMarker(Document, vft.Id, center, scaleDenominator);
                    for (int i = 0; i < elevationCount; i++)
                    {
                        var elevView = marker.CreateElevation(Document, planView.Id, i);
                        if (!string.IsNullOrEmpty(namePrefix))
                        {
                            try { elevView.Name = $"{namePrefix} {i + 1}"; } catch { } // name collision — keeps default name
                        }
                        elements.Add(elevView);
                    }
                    t.Commit();
                    sb.AppendLine($"Created elevation marker (Id {marker.Id}) with {elements.Count} elevation view(s) in plan '{planView.Name}':");
                    foreach (var v in elements)
                        sb.AppendLine($"  - '{v.Name}' (Id {v.Id})");
                }
                catch (Exception ex)
                {
                    try { t.RollBack(); } catch { }
                    sb.AppendLine($"FAILED to create room elevations — rolled back, nothing changed. Reason: {ex.Message}");
                    elements = new List<Element>();
                }
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
