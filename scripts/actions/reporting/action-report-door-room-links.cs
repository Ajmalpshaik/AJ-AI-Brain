// ============================================================
// FRAGMENT (action) — action-report-door-room-links.cs
// PURPOSE: Which room each door leads FROM and TO — verified against the geometry, not just read off
//          Revit's own properties — and optionally written into parameters so a door schedule can show
//          them. The data a door schedule, a room data sheet and a fire-escape check all need.
// ASSUMES: elements (List<Element>, doors) and sb (StringBuilder) exist from a filter above, e.g.
//          filter-by-category.cs with OST_Doors. If `elements` holds no doors it collects them itself.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ DO NOT TRUST `door.ToRoom` AND `door.FromRoom` ON THEIR OWN — this is the whole reason the fragment
//    is more than two lines. Those two properties are decided by the door's FACING ORIENTATION, so a
//    door that was flipped in the model has them the wrong way round relative to where the rooms
//    physically are. The plan looks right, the schedule reads backwards, and nothing warns you.
//    The fix is to check them: take a point a short distance either side of the door along its own
//    facing normal, and ask each candidate room `IsPointInRoom`. Where the answer disagrees with the
//    property, the geometry wins and the swap is REPORTED, not silently applied — a flipped door is a
//    modelling problem worth knowing about, not just a reporting one.
//    This is the Brain's standing rule in a new place: Revit's own data describes INTENT, and intent and
//    physical reality come apart (see START-HERE.md rule 1, and mep-trace.md for the same lesson on
//    connectors).
//
// ✱✱ FROM AND TO ARE PHASE-DEPENDENT. `get_FromRoom(phase)` / `get_ToRoom(phase)` take a Phase, and the
//    bare property answers for the LAST phase only. On a renovation job — where the room either side of
//    a door is exactly what changes — the bare property gives the wrong answer for every phase but one.
//    The phase used is named in the output, and `phaseName` lets you ask for a specific one.
//
// GOTCHA: a door in an exterior wall legitimately has only one room. Reported as `(outside)` rather than
//         as a failure, because "no room this side" is the correct answer for an entrance door.
//         A door with NEITHER side in a room usually means the rooms are not placed or not enclosed.
// GOTCHA: WRITING is off by default and needs the target parameters to already exist on the door —
//         normally shared parameters, since there are no built-ins for this. Missing parameters are
//         named, not silently skipped, so "it did nothing" always has a reason.
// GOTCHA: `IsPointInRoom` needs a point at a sensible height — the probe points are taken at the door's
//         own insertion height plus a small rise, not at the room's floor, so a threshold does not throw
//         the test.
// RELATED: action-report-room-space-data.cs reports the rooms themselves; this reports what connects
//          them. action-assign-location-data.cs writes room data onto general elements.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-24. Run it on a few doors you can see in plan and
//   check the from/to against the drawing before trusting a whole floor, especially any flip warnings.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string phaseName = "";              // "" = the model's last phase; otherwise the phase to ask about
double probeOffsetMm = 300;         // how far either side of the door to test for a room
bool writeToParameters = false;     // true = write the results onto the doors
string paramFromNumber = "FromRoomNumber";   // parameter names to write into (must already exist)
string paramFromName   = "FromRoomName";
string paramToNumber   = "ToRoomNumber";
string paramToName     = "ToRoomName";
bool dryRun = true;                 // with writeToParameters on, true = report what would be written
int maxReported = 60;
// ---- END INPUTS ----

const double MM = 304.8;
double probeFt = probeOffsetMm / MM;

// ---- the doors ----
var doors = (elements ?? new List<Element>()).OfType<FamilyInstance>()
    .Where(d => d.Category != null && d.Category.Id == new ElementId(BuiltInCategory.OST_Doors)).ToList();
if (doors.Count == 0)
{
    doors = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Doors)
        .WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();
    if (doors.Count > 0) sb.AppendLine($"(no doors in `elements` — collected all {doors.Count} door(s) in the model instead)");
}
if (doors.Count == 0) { sb.AppendLine("No doors in this model."); return sb.ToString(); }

// ---- the phase to ask about ----
var phases = new List<Phase>();
try { foreach (Phase p in Document.Phases) phases.Add(p); } catch { }
Phase phase = null;
if (!string.IsNullOrEmpty(phaseName)) phase = phases.FirstOrDefault(p => p.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase));
if (phase == null && phases.Count > 0) phase = phases[phases.Count - 1];
if (!string.IsNullOrEmpty(phaseName) && phase != null && !phase.Name.Equals(phaseName, StringComparison.OrdinalIgnoreCase))
    sb.AppendLine($"(phase '{phaseName}' not found — using '{phase.Name}'. Available: {string.Join(", ", phases.Select(p => p.Name))})");

Func<Autodesk.Revit.DB.Architecture.Room, string> roomNum = r =>
{
    if (r == null) return "";
    try { var p = r.get_Parameter(BuiltInParameter.ROOM_NUMBER); return p == null ? "" : (p.AsString() ?? ""); } catch { return ""; }
};
Func<Autodesk.Revit.DB.Architecture.Room, string> roomNam = r =>
{
    if (r == null) return "";
    try { var p = r.get_Parameter(BuiltInParameter.ROOM_NAME); return p == null ? (r.Name ?? "") : (p.AsString() ?? r.Name ?? ""); } catch { return r.Name ?? ""; }
};

var results = new List<Tuple<FamilyInstance, Autodesk.Revit.DB.Architecture.Room, Autodesk.Revit.DB.Architecture.Room, string>>();
int flipped = 0, noRoomEitherSide = 0, oneSideOnly = 0;

foreach (var door in doors.Take(maxReported))
{
    Autodesk.Revit.DB.Architecture.Room fromR = null, toR = null;
    try
    {
        if (phase != null) { fromR = door.get_FromRoom(phase); toR = door.get_ToRoom(phase); }
        else { fromR = door.FromRoom; toR = door.ToRoom; }
    }
    catch { }

    // ---- verify against the geometry ----
    string note = "";
    XYZ pFrom = null, pTo = null;
    try
    {
        var lp = door.Location as LocationPoint;
        var facing = door.FacingOrientation;   // the way the door faces; TO is this side
        if (lp != null && facing != null && !facing.IsZeroLength())
        {
            var basePt = lp.Point + new XYZ(0, 0, 1.0);   // a little above the threshold
            pTo = basePt + facing.Normalize().Multiply(probeFt);
            pFrom = basePt - facing.Normalize().Multiply(probeFt);
        }
    }
    catch { }

    if (pFrom != null && pTo != null)
    {
        // Each candidate is tested against BOTH probe points. If Revit's "to" room actually contains the
        // FROM point, the door is flipped and the two are the wrong way round.
        bool swapped = false;
        try
        {
            if (toR != null && !toR.IsPointInRoom(pTo) && toR.IsPointInRoom(pFrom)) swapped = true;
            if (fromR != null && !fromR.IsPointInRoom(pFrom) && fromR.IsPointInRoom(pTo)) swapped = true;
        }
        catch { }

        if (swapped)
        {
            var t = toR; toR = fromR; fromR = t;
            note = "FLIPPED — Revit's From/To are the wrong way round for where the rooms actually are";
            flipped++;
        }
    }
    else note = "could not probe (no location point or no facing direction)";

    if (fromR == null && toR == null) { noRoomEitherSide++; if (note == "") note = "no room either side — rooms unplaced, or not enclosed here"; }
    else if (fromR == null || toR == null) { oneSideOnly++; if (note == "") note = "one side only — normal for an external door"; }

    results.Add(Tuple.Create(door, fromR, toR, note));
}

// ---- report ----
sb.AppendLine($"Door room links for {Math.Min(doors.Count, maxReported)} of {doors.Count} door(s), phase '{(phase != null ? phase.Name : "(none)")}':");
sb.AppendLine($"{flipped} flipped, {oneSideOnly} with one side only, {noRoomEitherSide} with no room either side.");
sb.AppendLine();
sb.AppendLine("Door Id | Mark | From room | To room | Note");
sb.AppendLine("--- | --- | --- | --- | ---");
foreach (var r in results)
{
    string mark = "";
    try { var p = r.Item1.get_Parameter(BuiltInParameter.ALL_MODEL_MARK); mark = p == null ? "" : (p.AsString() ?? ""); } catch { }
    string f = r.Item2 == null ? "(outside)" : $"{roomNum(r.Item2)} {roomNam(r.Item2)}".Trim();
    string t = r.Item3 == null ? "(outside)" : $"{roomNum(r.Item3)} {roomNam(r.Item3)}".Trim();
    sb.AppendLine($"{r.Item1.Id} | {mark} | {f} | {t} | {r.Item4}");
}
if (doors.Count > maxReported) sb.AppendLine($"... {doors.Count - maxReported} more door(s) not reported (raise maxReported).");

if (flipped > 0)
    sb.AppendLine($"\n⚠ {flipped} door(s) are FLIPPED relative to their rooms. The table above already shows the geometric truth, but the model's own From/To Room properties are backwards on those doors — worth fixing in the model, because any schedule reading them directly will be wrong.");

// ---- optional write ----
if (!writeToParameters)
{
    sb.AppendLine("\nReport only. Set writeToParameters = true to write these onto the doors (the four parameters must already exist).");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine($"\nDRY RUN — would write From/To room number and name onto {results.Count} door(s) via '{paramFromNumber}', '{paramFromName}', '{paramToNumber}', '{paramToName}'. Nothing changed.");
    return sb.ToString();
}

int written = 0; var missingParams = new HashSet<string>();
using (var t = new Transaction(Document, "AJ Tools - Write Door Room Links"))
{
    t.Start();
    try
    {
        foreach (var r in results)
        {
            Action<string, string> put = (pname, val) =>
            {
                if (string.IsNullOrEmpty(pname)) return;
                var p = r.Item1.LookupParameter(pname);
                if (p == null) { missingParams.Add(pname); return; }
                if (p.IsReadOnly) { missingParams.Add(pname + " (read-only)"); return; }
                try { p.Set(val ?? ""); } catch { }
            };
            put(paramFromNumber, roomNum(r.Item2));
            put(paramFromName,   roomNam(r.Item2));
            put(paramToNumber,   roomNum(r.Item3));
            put(paramToName,     roomNam(r.Item3));
            written++;
        }
        t.Commit();
        sb.AppendLine($"\nWrote room links onto {written} door(s).");
        if (missingParams.Count > 0)
            sb.AppendLine($"⚠ These parameters do not exist on the doors (or are read-only), so nothing was written to them: {string.Join(", ", missingParams)}. Add them as shared parameters first — action-add-project-parameter.cs.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
