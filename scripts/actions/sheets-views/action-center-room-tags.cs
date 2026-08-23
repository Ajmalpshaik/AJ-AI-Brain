// ============================================================
// FRAGMENT (action) — action-center-room-tags.cs
// PURPOSE: Move each ROOM TAG in `elements` so its head sits on the CENTRE of the room it tags — the
//          tidy-up after tags have been dragged about, or after rooms were re-shaped and the tags
//          stayed put. Works on tags of rooms in a LINKED model too.
// ASSUMES: elements (List<Element>, RoomTags) and sb (StringBuilder) exist from a filter above.
//          Anything that is not a RoomTag is ignored and counted.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱ THE REAL WORK IS FINDING "THE CENTRE", and a bounding-box centre is the wrong answer often enough
//   to matter. Four methods are tried in this order, and the order is the whole point:
//     1. **BOUNDARY CENTROID** — the true area-weighted polygon centroid from `GetBoundarySegments`,
//        summed across every loop, so a room with a hole in it still lands correctly.
//     2. **Bounding-box centre** — only if the boundary cannot be read at all.
//     3. **AN INTERIOR GRID POINT** — because on an L-shaped or U-shaped room **the true centroid can
//        fall OUTSIDE the room**, in the corridor or the other tenancy. This step samples points inside
//        and takes one that `IsPointInRoom` accepts. Without it, a script that "works" quietly puts
//        L-shaped rooms' tags in the wrong space.
//     4. The room's own `Location` point — last resort.
//
// GOTCHA: DRY RUN BY DEFAULT — this moves annotation in bulk and the result is only judged by eye.
// GOTCHA: `tag.TagHeadPosition` is what you set. Setting the tag's `Location` does not do it.
// GOTCHA: A LINKED ROOM NEEDS THE LINK TRANSFORM. A tag in this model can tag a room in a link; the
//         room's centre then comes back in the LINK's coordinates and must be pushed through
//         `RevitLinkInstance.GetTotalTransform()` before it means anything here. Skipping that puts
//         every tag at the wrong end of the site.
// GOTCHA: an ORPHANED tag (its room deleted) has no room to centre on — counted separately, not as a
//         failure. So is a room with zero Area, which is an unplaced room.
// GOTCHA: PINNED tags are skipped. `TagHeadPosition` on a pinned tag does not move it and does not
//         throw — the same silent no-op as `MoveElement`, see geometry-and-transforms.md.
// GOTCHA: "already centred" is a SUCCESS and is counted separately, so "0 moved" on a tidy drawing
//         reads as "nothing to do" rather than "it failed".
// NOTE: this only moves the HEAD. Revit draws the leader automatically; it does not need setting.
// ⚠ THAT NOTE IS PROVEN ONLY FOR TAGS WITHOUT A LEADER (flagged 2026-08-23). The live verification below
//   ran on three centred room tags, and a room tag sitting on its own room normally has no leader — so
//   the leadered case was never exercised. A room tag's leader END is a settable point
//   (`SpatialElementTag.LeaderEnd`), and moving the head can drag it: other implementations of this job
//   deliberately read that end BEFORE the move and write it back after. If a leadered tag comes out with
//   its arrow in the wrong place, that is the reason and the fix is to capture/restore `LeaderEnd`
//   around the `TagHeadPosition` write. Left as a flagged unknown rather than a speculative change to a
//   proven fragment — see ../../../knowledge/live-model/tagging.md § Moving a tag moves its leader end.
//   The direction of travel here helps: this moves tags INTO their room, where a leader is least likely.
// ✓ LIVE-VERIFIED 2026-08-22 on `school.rvt`, Revit 2020.2.9, via run_fragment composed with
//   filter-by-category (OST_RoomTags). Dry run predicted moves of 1761 / 3035 / 1877 mm on three tags;
//   the real run moved exactly those three; a second dry run then reported "would move 0 | already
//   centred: 3", so it is idempotent. The predicted distances were cross-checked against an
//   independently written centroid calculation in the same session and agreed to within 1 mm.
//   Still untested on an L-shaped room — all three test rooms were rectangles, so steps 2 and 3 of the
//   four-step centre (the bbox and interior-grid fallbacks) did NOT execute. That path stays unproven.
//
// ⚠⚠ CS0246 ON `Room` / `RoomTag`, FIXED HERE 2026-08-22 — the defect that made "compile-checked" a
//    false comfort. This file was harvested and compile-checked on 2020/2024/2027 and still threw
//    "The type or namespace name 'RoomTag' could not be found" on its FIRST real run. The compile
//    harness supplies namespaces that the bridge's C# context does not. `Room`/`RoomTag` live in
//    `Autodesk.Revit.DB.Architecture` and `Space` in `Autodesk.Revit.DB.Mechanical`, and a COMPOSED
//    script cannot fix it with a `using` — C# wants those at the top of the file, and this fragment is
//    pasted into the middle. So every such type is written fully qualified below. Do not "tidy" them.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report the moves only; false = actually move
double toleranceMm = 5;              // closer than this to the centre already counts as centred
// ---- END INPUTS ----

const double MM = 304.8;
double tolFt = toleranceMm / MM;

// link instances, so a tag on a linked room can be resolved
var links = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance))
    .Cast<RevitLinkInstance>().ToList();

// ---------- the four-step centre ----------
Func<Autodesk.Revit.DB.Architecture.Room, XYZ> centreOf = room =>
{
    if (room == null || !room.IsValidObject) return null;
    try { if (room.Area <= 1e-6) return null; } catch { return null; }   // unplaced room

    // 1. true area-weighted centroid across every boundary loop
    try
    {
        var loops = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
        if (loops != null && loops.Count > 0)
        {
            double cross = 0, cx = 0, cy = 0, zsum = 0; int zn = 0;
            foreach (var loop in loops)
            {
                var pts = new List<XYZ>();
                foreach (var seg in loop)
                {
                    var c = seg.GetCurve();
                    if (c != null) { pts.Add(c.GetEndPoint(0)); zsum += c.GetEndPoint(0).Z; zn++; }
                }
                if (pts.Count < 3) continue;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pts[i]; var q = pts[(i + 1) % pts.Count];
                    double f = p.X * q.Y - q.X * p.Y;
                    cross += f; cx += (p.X + q.X) * f; cy += (p.Y + q.Y) * f;
                }
            }
            if (Math.Abs(cross) > 1e-9)
            {
                double z = zn > 0 ? zsum / zn : 0;
                var cand = new XYZ(cx / (3 * cross), cy / (3 * cross), z);
                // On an L-shape the true centroid can be OUTSIDE the room — check before trusting it.
                try { if (room.IsPointInRoom(cand)) return cand; } catch { return cand; }
            }
        }
    }
    catch { }

    var bb = room.get_BoundingBox(null);

    // 2. bounding-box centre, if it is actually inside
    if (bb != null)
    {
        var c2 = new XYZ((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2, (bb.Min.Z + bb.Max.Z) / 2);
        try { if (room.IsPointInRoom(c2)) return c2; } catch { }
    }

    // 3. AN INTERIOR GRID POINT — the L-shaped-room case the other two get wrong
    if (bb != null)
    {
        double z = (bb.Min.Z + bb.Max.Z) / 2;
        for (int gi = 1; gi <= 7; gi++)
            for (int gj = 1; gj <= 7; gj++)
            {
                var p = new XYZ(bb.Min.X + (bb.Max.X - bb.Min.X) * gi / 8.0,
                                bb.Min.Y + (bb.Max.Y - bb.Min.Y) * gj / 8.0, z);
                try { if (room.IsPointInRoom(p)) return p; } catch { }
            }
    }

    // 4. last resort
    var lp = room.Location as LocationPoint;
    return lp != null ? lp.Point : null;
};

int moved = 0, already = 0, pinned = 0, orphaned = 0, noCentre = 0, notATag = 0, failed = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Center Room Tags"))
{
    t.Start();
    try
    {
        foreach (var e in elements)
        {
            var tag = e as Autodesk.Revit.DB.Architecture.RoomTag;
            if (tag == null || !tag.IsValidObject) { notATag++; continue; }
            if (tag.Pinned) { pinned++; continue; }

            Autodesk.Revit.DB.Architecture.Room room = null;
            Transform toHost = Transform.Identity;

            try { room = tag.Room; } catch { }

            if (room == null)
            {
                // A tag on a LINKED room: resolve through the link and take its transform.
                try
                {
                    var linkId = tag.TaggedRoomId;
                    if (linkId != null && linkId.LinkInstanceId != ElementId.InvalidElementId)
                    {
                        var li = Document.GetElement(linkId.LinkInstanceId) as RevitLinkInstance;
                        if (li != null)
                        {
                            var ldoc = li.GetLinkDocument();
                            if (ldoc != null) room = ldoc.GetElement(linkId.LinkedElementId) as Autodesk.Revit.DB.Architecture.Room;
                            toHost = li.GetTotalTransform();
                        }
                    }
                }
                catch { }
            }

            if (room == null) { orphaned++; continue; }

            var centre = centreOf(room);
            if (centre == null) { noCentre++; continue; }

            // A LINKED room's centre is in the LINK's coordinates until this line.
            var target = toHost.OfPoint(centre);

            XYZ head;
            try { head = tag.TagHeadPosition; } catch { failed++; continue; }
            if (head == null) { failed++; continue; }

            var flatTarget = new XYZ(target.X, target.Y, head.Z);   // keep the tag's own plane
            if (head.DistanceTo(flatTarget) <= tolFt) { already++; continue; }

            if (!dryRun)
            {
                try { tag.TagHeadPosition = flatTarget; } catch { failed++; continue; }
                // TagHeadPosition on a pinned tag is a silent no-op — read it back.
                XYZ after;
                try { after = tag.TagHeadPosition; } catch { after = head; }
                if (after.DistanceTo(flatTarget) > tolFt) { failed++; continue; }
            }

            moved++;
            if (rows.Count < 25)
                rows.Add(tag.Id + " | " + (room.Name ?? "") + " | " +
                         Math.Round(head.DistanceTo(flatTarget) * MM) + (toHost.IsIdentity ? "" : " | linked"));
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing moved. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing moved. " : "") +
              $"Room tags: {(dryRun ? "would move" : "moved")} {moved} | already centred: {already}" +
              (pinned > 0 ? $" | pinned, skipped: {pinned}" : "") +
              (orphaned > 0 ? $" | orphaned (no room): {orphaned}" : "") +
              (noCentre > 0 ? $" | room unplaced or no centre: {noCentre}" : "") +
              (notATag > 0 ? $" | not a RoomTag: {notATag}" : "") +
              (failed > 0 ? $" | FAILED: {failed}" : ""));

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Tag Id | Room | Moved (mm) | Note");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r.Contains("| linked") ? r : r + " | ");
    if (moved > rows.Count) sb.AppendLine("... and " + (moved - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
