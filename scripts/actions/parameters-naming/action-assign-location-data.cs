// ============================================================
// FRAGMENT (action) — action-assign-location-data.cs
// PURPOSE: WRITE where each element actually IS onto its own parameters — the Room or Space it sits in,
//          its Level, and its X/Y/Z coordinates. The "fill in the location columns" job that a schedule,
//          a handover register or a client's asset list needs, and that nothing in Revit fills for you.
//          Reads the position from GEOMETRY, not from what somebody typed.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// GOTCHA: DRY RUN BY DEFAULT — it writes parameters in bulk and a wrong parameter name silently writes
//         nothing on every element. The dry run tells you how many would actually receive each value.
// GOTCHA: THE TARGET PARAMETERS MUST ALREADY EXIST on those categories. This fragment does NOT create
//         them — a project or shared parameter has to be bound first. Anything missing is counted and
//         named per parameter, so "0 written" is never mysterious.
// GOTCHA: ROOM vs SPACE. Architecture puts people in ROOMS; MEP calculates in SPACES, and a model can
//         have both, in the same place, with different names. Pick with `useSpaceNotRoom` and say which
//         you used — this is the single most common way a location register comes out wrong.
// GOTCHA: `Room.IsPointInRoom` needs a point INSIDE the room's volume. An element at ceiling level can
//         sit above the room's upper limit and test false, so the probe Z is nudged to the room's own
//         mid-height rather than using the element's Z as given. A ceiling-mounted diffuser is exactly
//         the case that fails otherwise.
// GOTCHA: an element genuinely outside every room reports BLANK, and blank is written only when
//         `writeBlankWhenNotFound` is true. Leaving it false preserves whatever is already there rather
//         than wiping good data with an empty string.
// GOTCHA: coordinates are written in PROJECT coordinates (Revit internal, converted to mm). If the
//         client wants survey/shared coordinates that is a different transform and this does not do it.
// NOTE: every target parameter name is an input. Different clients call these different things — that
//       is exactly why nothing is hardcoded.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Assign Location tool.
//   Compile-checked on 2020/2024/2027. Dry-run first and check the found/not-found split.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                       // true = report what would be written; false = actually write
bool useSpaceNotRoom = false;             // false = Rooms (architectural), true = MEP Spaces
string roomNameParam = "";                // e.g. "MM_Room Name"  — "" = don't write it
string roomNumberParam = "";              // e.g. "MM_Room Number"
string levelNameParam = "";               // e.g. "MM_Level"
string coordXParam = "";                  // e.g. "MM_X"  (written in mm)
string coordYParam = "";
string coordZParam = "";
bool writeBlankWhenNotFound = false;      // false = leave existing text alone when no room/space found
// ---- END INPUTS ----

const double MM = 304.8;

Func<Element, string, string, bool> writeText = (el, pname, val) =>
{
    if (string.IsNullOrWhiteSpace(pname)) return false;
    try
    {
        var p = el.LookupParameter(pname);
        if (p == null || p.IsReadOnly) return false;
        if (p.StorageType == StorageType.String) { if (!dryRun) p.Set(val ?? ""); return true; }
        if (p.StorageType == StorageType.Double)
        {
            double d; if (!double.TryParse(val, out d)) return false;
            if (!dryRun) p.Set(d / MM);   // a length parameter is stored in feet
            return true;
        }
        return false;
    }
    catch { return false; }
};

// counters per target parameter
var written = new Dictionary<string, int>();
var missing = new Dictionary<string, int>();
Action<string, bool> tally = (pname, ok) =>
{
    if (string.IsNullOrWhiteSpace(pname)) return;
    var d = ok ? written : missing;
    if (!d.ContainsKey(pname)) d[pname] = 0;
    d[pname]++;
};

// all rooms/spaces once, not per element
var containers = new FilteredElementCollector(Document)
    .OfCategory(useSpaceNotRoom ? BuiltInCategory.OST_MEPSpaces : BuiltInCategory.OST_Rooms)
    .WhereElementIsNotElementType()
    .Cast<SpatialElement>()
    .Where(s => s != null && s.Area > 0)      // unplaced rooms have Area 0 and contain nothing
    .ToList();

int found = 0, notFound = 0, noPoint = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Assign Location Data"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            if (e == null) { noPoint++; continue; }

            XYZ pt = null;
            var lp = e.Location as LocationPoint;
            if (lp != null) pt = lp.Point;
            else
            {
                var lc = e.Location as LocationCurve;
                if (lc != null && lc.Curve != null) pt = lc.Curve.Evaluate(0.5, true);
            }
            if (pt == null) { noPoint++; continue; }

            // find the containing room/space
            SpatialElement hit = null;
            foreach (var c in containers)
            {
                try
                {
                    // Probe at the ROOM'S OWN mid-height, not the element's Z — a ceiling-mounted
                    // element sits above the room's upper limit and would test false.
                    var bb = c.get_BoundingBox(null);
                    double probeZ = bb != null ? (bb.Min.Z + bb.Max.Z) / 2.0 : pt.Z;
                    var probe = new XYZ(pt.X, pt.Y, probeZ);

                    // Fully qualified on purpose: the bridge's C# context has no `using` for
                    // Architecture or Mechanical, and a composed script cannot add one mid-file
                    // (C# wants usings at the top). Bare `Room`/`Space` compile-check fine and then
                    // fail CS0246 the moment they run — proved on action-center-room-tags.cs,
                    // 2026-08-22. Same rule for Level? no: Level IS in Autodesk.Revit.DB.
                    var room = c as Autodesk.Revit.DB.Architecture.Room;
                    if (room != null) { if (room.IsPointInRoom(probe)) { hit = c; break; } continue; }
                    var space = c as Autodesk.Revit.DB.Mechanical.Space;
                    if (space != null && space.IsPointInSpace(probe)) { hit = c; break; }
                }
                catch { }
            }

            if (hit != null) found++; else notFound++;

            string rName = hit != null ? (hit.Name ?? "") : "";
            string rNum = "";
            try { var np = hit != null ? hit.get_Parameter(BuiltInParameter.ROOM_NUMBER) : null; rNum = np != null ? (np.AsString() ?? "") : ""; } catch { }

            string lvlName = "";
            try { var lvl = Document.GetElement(e.LevelId) as Level; if (lvl != null) lvlName = lvl.Name; } catch { }

            bool haveContainer = hit != null;
            if (haveContainer || writeBlankWhenNotFound)
            {
                tally(roomNameParam, writeText(e, roomNameParam, rName));
                tally(roomNumberParam, writeText(e, roomNumberParam, rNum));
            }
            tally(levelNameParam, writeText(e, levelNameParam, lvlName));
            tally(coordXParam, writeText(e, coordXParam, Math.Round(pt.X * MM, 1).ToString()));
            tally(coordYParam, writeText(e, coordYParam, Math.Round(pt.Y * MM, 1).ToString()));
            tally(coordZParam, writeText(e, coordZParam, Math.Round(pt.Z * MM, 1).ToString()));

            if (rows.Count < 25)
                rows.Add(e.Id + " | " + (e.Category != null ? e.Category.Name : "?") + " | " +
                         (haveContainer ? (rNum + " " + rName).Trim() : "(none)") + " | " + lvlName + " | " +
                         Math.Round(pt.X * MM) + ", " + Math.Round(pt.Y * MM) + ", " + Math.Round(pt.Z * MM));
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing written. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing written. " : "") +
              $"Location data from {(useSpaceNotRoom ? "MEP SPACES" : "ROOMS")} across {elements.Count} element(s): " +
              $"inside a {(useSpaceNotRoom ? "space" : "room")}: {found} | outside every one: {notFound}" +
              (noPoint > 0 ? $" | no usable position: {noPoint}" : ""));
if (notFound > 0 && !writeBlankWhenNotFound)
    sb.AppendLine($"The {notFound} outside kept whatever their parameters already held " +
                  "(writeBlankWhenNotFound = false).");

var allNames = written.Keys.Union(missing.Keys).OrderBy(k => k).ToList();
if (allNames.Count == 0)
{
    sb.AppendLine("No target parameter names were set — nothing to write. Fill in roomNameParam, " +
                  "levelNameParam, coordXParam etc.");
}
else
{
    sb.AppendLine("");
    sb.AppendLine("Parameter | " + (dryRun ? "Would write" : "Written") + " | Not present / read-only");
    sb.AppendLine("--- | --- | ---");
    foreach (var n in allNames)
        sb.AppendLine(n + " | " + (written.ContainsKey(n) ? written[n] : 0) + " | " +
                      (missing.ContainsKey(n) ? missing[n] : 0));
    if (allNames.Any(n => !written.ContainsKey(n) || written[n] == 0))
        sb.AppendLine("A parameter with 0 written is NOT BOUND to these categories (or is read-only) — " +
                      "bind it first; this fragment does not create parameters.");
}

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Id | Category | " + (useSpaceNotRoom ? "Space" : "Room") + " | Level | X, Y, Z (mm)");
    sb.AppendLine("--- | --- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (elements.Count > rows.Count) sb.AppendLine("... and " + (elements.Count - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
