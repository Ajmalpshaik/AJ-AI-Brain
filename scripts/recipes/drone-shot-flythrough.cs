// ============================================================
// FRAGMENT (recipe) — drone-shot-flythrough.cs
// PURPOSE: A walkthrough / "drone shot" through the model, exported as numbered PNG frames on disk —
//          "fly from Room 1 to Room 2", "follow this path". Finds the rooms, routes THROUGH THEIR DOORS
//          rather than through walls, flies a perspective camera along the path and exports one frame
//          per step. The frames become a video with ffmpeg (see the bottom of this header).
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// ✓ LIVE-VERIFIED 2026-08-22 on the `school` model — Room 1 (-6667, 6578) to Room 2 (10208, 4260) via
//   both their doors: 240 frames at 1920 px in 58 s (0.24 s per frame), 0 failed, 3.4 MB on disk, files
//   confirmed present from OUTSIDE Revit. Frames 0 / 97 / 204 / 239 checked by eye: starts in Room 1
//   facing its door, passes through both doorways, ends inside Room 2 in correct perspective.
//
// ⚠⚠ THE HEADING BUG THAT THIS FIXES, because it is easy to reintroduce. The camera aims at a point a
//    little FURTHER ALONG the path. At the very end there is nothing further along — `pointAt(s+step)`
//    returns the camera's own position, the forward vector collapses to zero, and a naive fallback
//    (`XYZ.BasisX`) points the last frames at a blank wall. The fix is to look BEHIND when you cannot
//    look ahead, which preserves the direction of travel. Caught live on the first run.
//
// GOTCHA: THE BRIDGE REFUSES TO DELETE FILES — "Deletes or moves files/folders on disk: this cannot be
//         undone with Ctrl+Z." So this NEVER clears the folder. Re-running with the same frame count
//         overwrites the same filenames cleanly; re-running with FEWER frames leaves the extra old ones
//         behind, which would corrupt a video. Use a new folder, or clear it yourself in Explorer.
// GOTCHA: the view MUST be perspective. An existing 3D view cannot be switched from ortho, so this
//         creates (or reuses) its own perspective view named by `cameraViewName`. That view is left in
//         the project — it is how you check the shot, and deleting views is not this fragment's job.
// GOTCHA: ROUTING IS DOOR-TO-DOOR, NOT PATHFINDING. It takes each room's nearest door and joins
//         room -> doorA -> doorB -> room. That is right for two rooms off the same corridor. For rooms
//         several turns apart the middle leg can still cut a corner through a wall — pass explicit
//         `waypointsMm` for those, which is what that input is for.
// GOTCHA: eye height is measured from the LEVEL, not from the floor finish. 1600 mm reads as a person;
//         2200-2500 mm reads as a drone. It is a per-request number — ask. In ROOM mode each point
//         takes its own room's/door's real Z, so upper floors and survey datums are handled (fixed
//         2026-08-25 — it used to be an absolute Z above project zero, right only on a level at 0).
//         In explicit-`waypointsMm` mode there is no room to read a level from, so the height IS
//         absolute above project internal zero — add the level height into eyeHeightMm yourself.
// GOTCHA: rooms are read from THIS document only. On a coordination model where the rooms live in the
//         architectural LINK, the room lookup returns nothing and the reply lists an empty room set —
//         that is the link seam, not an empty building. Use explicit waypointsMm there.
// GOTCHA: `Document.ExportImage` takes no transaction (document I/O), but `SetOrientation` needs one.
//         So each frame is: transaction -> orient -> commit -> export. Exporting inside an open
//         transaction fails.
// GOTCHA: Revit names the file itself as "<prefix> - 3D View - <view name>.png", so the PREFIX must
//         change per frame or every frame overwrites the last. That is why it is `frame_0000` etc.
// NOTE: file size is small because a bare model is mostly flat colour. A modelled-out project produces
//       much larger, better-looking frames at the same speed.
// NOTE: to make the video (ffmpeg is NOT installed on this PC as of 2026-08-22 — install it first):
//         ffmpeg -framerate 30 -i "<folder>\frame_%04d - 3D View - <viewName>.png"
//                -c:v libx264 -pix_fmt yuv420p "<folder>.mp4"
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string fromRoom = "Room 1";          // room NAME or NUMBER to start in
string toRoom   = "Room 2";          // room NAME or NUMBER to finish in
double[] waypointsMm = null;         // OPTIONAL explicit path, flat x,y pairs {x1,y1, x2,y2, ...} —
                                     // overrides the room routing entirely when set
double eyeHeightMm = 1600;           // ASK. 1600 = person, 2200-2500 = drone
int frames = 240;                    // 240 = 8 s at 30 fps
int pixelWidth = 1920;
string outputFolder = @"D:\Ajmal\AJ Photos\flythrough";
string cameraViewName = "AJ Drone Shot";
bool useDoors = true;                // route through each room's nearest door
// ---- END INPUTS ----

const double MM = 304.8;
var sb = new System.Text.StringBuilder();
System.IO.Directory.CreateDirectory(outputFolder);

// ---------- build the path ----------
var wp = new List<XYZ>();
double z = eyeHeightMm / MM;

if (waypointsMm != null && waypointsMm.Length >= 4)
{
    for (int i = 0; i + 1 < waypointsMm.Length; i += 2)
        wp.Add(new XYZ(waypointsMm[i] / MM, waypointsMm[i + 1] / MM, z));
    sb.AppendLine("Path: " + wp.Count + " explicit waypoint(s).");
}
else
{
    Func<string, SpatialElement> findRoom = key =>
    {
        var all = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType().Cast<SpatialElement>()
            .Where(r => { try { return r.Area > 1e-6; } catch { return false; } }).ToList();
        foreach (var r in all)
        {
            if (string.Equals(r.Name ?? "", key, StringComparison.OrdinalIgnoreCase)) return r;
            try { var p = r.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                  if (p != null && string.Equals(p.AsString() ?? "", key, StringComparison.OrdinalIgnoreCase)) return r; } catch { }
        }
        return all.FirstOrDefault(r => (r.Name ?? "").IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
    };

    var a = findRoom(fromRoom); var b = findRoom(toRoom);
    if (a == null || b == null)
    {
        var names = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType().Cast<SpatialElement>().Select(r => r.Name ?? "");
        return "Room not found (" + (a == null ? fromRoom : toRoom) + "). Rooms here: " + string.Join(" | ", names);
    }

    // The room's own point already carries the LEVEL's internal Z, so adding z on top makes the
    // eye height level-relative — fixed 2026-08-25: this used to be a bare `z`, an ABSOLUTE height
    // above project internal zero, which buried the camera in the slab on any level not at Z=0.
    // It passed live only because the proof model's rooms sat on a level at exactly 0.
    Func<SpatialElement, XYZ> centreOf = r =>
    {
        var lp = r.Location as LocationPoint;
        if (lp != null) return new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + z);
        var bb = r.get_BoundingBox(null);
        return bb != null ? new XYZ((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2, bb.Min.Z + z) : XYZ.Zero;
    };

    XYZ pa = centreOf(a), pb = centreOf(b);
    wp.Add(pa);

    if (useDoors)
    {
        var doors = new FilteredElementCollector(Document).OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType().ToElements()
            .Select(d => new { E = d, P = (d.Location as LocationPoint) })
            .Where(x => x.P != null).ToList();

        // the door that belongs to each room: prefer FromRoom/ToRoom, else the nearest one
        Func<SpatialElement, XYZ, XYZ> doorFor = (room, centre) =>
        {
            foreach (var d in doors)
            {
                var fi = d.E as FamilyInstance;
                try {
                    if (fi != null && ((fi.ToRoom != null && fi.ToRoom.Id == room.Id) ||
                                       (fi.FromRoom != null && fi.FromRoom.Id == room.Id)))
                        return new XYZ(d.P.Point.X, d.P.Point.Y, d.P.Point.Z + z);
                } catch { }
            }
            var near = doors.OrderBy(d => new XYZ(d.P.Point.X, d.P.Point.Y, d.P.Point.Z + z).DistanceTo(centre)).FirstOrDefault();
            return near == null ? null : new XYZ(near.P.Point.X, near.P.Point.Y, near.P.Point.Z + z);
        };

        var da = doorFor(a, pa); var db = doorFor(b, pb);
        if (da != null) wp.Add(da);
        if (db != null && (da == null || db.DistanceTo(da) > 1e-6)) wp.Add(db);
        sb.AppendLine("Routed through " + ((da != null ? 1 : 0) + (db != null ? 1 : 0)) + " door(s).");
    }
    wp.Add(pb);
    sb.AppendLine("Path: '" + (a.Name ?? "") + "' -> '" + (b.Name ?? "") + "'");
}

if (wp.Count < 2) return "Need at least two path points.";

// ---------- the perspective camera ----------
View3D cam = new FilteredElementCollector(Document).OfClass(typeof(View3D)).Cast<View3D>()
    .FirstOrDefault(v => v != null && !v.IsTemplate && v.Name == cameraViewName);

using (var t = new Transaction(Document, "AJ Tools - Flythrough camera"))
{
    t.Start();
    try
    {
        if (cam == null)
        {
            var vft = new FilteredElementCollector(Document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);
            if (vft == null) { t.RollBack(); return "No 3D ViewFamilyType in this project."; }
            cam = View3D.CreatePerspective(Document, vft.Id);   // an existing ORTHO view cannot be converted
            cam.Name = cameraViewName;
        }
        if (cam.IsLocked) cam.Unlock();
        cam.DetailLevel = ViewDetailLevel.Fine;
        cam.DisplayStyle = DisplayStyle.Shading;
        try { cam.CropBoxActive = false; cam.CropBoxVisible = false; } catch { }
        t.Commit();
    }
    catch (Exception ex) { t.RollBack(); return "Camera view setup failed: " + ex.Message; }
}

// ---------- fly it ----------
double total = 0;
for (int i = 1; i < wp.Count; i++) total += wp[i - 1].DistanceTo(wp[i]);
if (total < 1e-9) return "The path has no length.";

Func<double, XYZ> pointAt = s =>
{
    s = Math.Min(1.0, Math.Max(0.0, s));
    double d = s * total, acc = 0;
    for (int i = 1; i < wp.Count; i++)
    {
        double seg = wp[i - 1].DistanceTo(wp[i]);
        if (acc + seg >= d || i == wp.Count - 1)
        { double f = seg < 1e-9 ? 0 : (d - acc) / seg; return wp[i - 1] + (wp[i] - wp[i - 1]).Multiply(Math.Min(1, Math.Max(0, f))); }
        acc += seg;
    }
    return wp[wp.Count - 1];
};

int made = 0, failed = 0, usedBehind = 0;
var t0 = DateTime.Now;

for (int f = 0; f < frames; f++)
{
    double s = frames == 1 ? 0 : (double)f / (frames - 1);
    XYZ eye = pointAt(s);

    XYZ fwd = pointAt(s + 0.04) - eye;                 // look ahead along the path
    if (fwd.GetLength() < 1e-6)                        // THE FIX: at the end there is no ahead,
    { fwd = eye - pointAt(s - 0.04); usedBehind++; }   // so keep the direction of travel
    fwd = new XYZ(fwd.X, fwd.Y, 0);
    if (fwd.GetLength() < 1e-6) { failed++; continue; }
    fwd = fwd.Normalize();

    using (var t = new Transaction(Document, "AJ Tools - Flythrough frame"))
    {
        t.Start();
        try { cam.SetOrientation(new ViewOrientation3D(eye, XYZ.BasisZ, fwd)); t.Commit(); }
        catch { t.RollBack(); failed++; continue; }
    }

    // Export takes NO transaction, and the PREFIX must change per frame or each one overwrites the last.
    var opts = new ImageExportOptions();
    opts.FilePath = System.IO.Path.Combine(outputFolder, "frame_" + f.ToString("D4"));
    opts.ExportRange = ExportRange.SetOfViews;
    opts.SetViewsAndSheets(new List<ElementId> { cam.Id });
    opts.HLRandWFViewsFileType = ImageFileType.PNG;
    opts.ShadowViewsFileType = ImageFileType.PNG;
    opts.ImageResolution = ImageResolution.DPI_150;
    opts.PixelSize = pixelWidth;
    opts.ZoomType = ZoomFitType.FitToPage;
    try { Document.ExportImage(opts); made++; } catch { failed++; }
}

double secs = (DateTime.Now - t0).TotalSeconds;
var files = System.IO.Directory.GetFiles(outputFolder, "*.png");

sb.AppendLine("Length    : " + Math.Round(total * MM / 1000.0, 2) + " m, eye height " + eyeHeightMm + " mm");
sb.AppendLine("Frames    : " + made + " exported, " + failed + " failed, " + pixelWidth + " px wide");
sb.AppendLine("Time      : " + Math.Round(secs, 1) + " s (" + Math.Round(secs / Math.Max(1, made), 2) + " s per frame)");
sb.AppendLine("Folder    : " + outputFolder);
sb.AppendLine("On disk   : " + files.Length + " png, " + Math.Round(files.Sum(x => new System.IO.FileInfo(x).Length) / 1048576.0, 1) + " MB");
sb.AppendLine("At 30 fps : " + Math.Round(made / 30.0, 1) + " seconds of video");
if (files.Length > made)
    sb.AppendLine("*** " + (files.Length - made) + " OLDER png(s) are still in this folder from a previous, longer " +
                  "run — the bridge cannot delete files. Clear the folder yourself before making a video. ***");
sb.AppendLine("View '" + cameraViewName + "' (" + cam.Id + ") is left in the project so the shot can be checked.");
return sb.ToString();
