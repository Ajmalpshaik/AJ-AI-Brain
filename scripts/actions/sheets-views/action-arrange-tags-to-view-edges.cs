// ============================================================
// FRAGMENT (action) — action-arrange-tags-to-view-edges.cs
// PURPOSE: Clear the middle of a congested view by parking every tag in a neat column down the LEFT and
//          RIGHT edges of the crop, each still leadered back to its own element, with the leaders
//          fanned out so they do not cross. The "I can't read the drawing for tags" fix on a busy
//          services plan or a detail section.
// ASSUMES: elements (List<Element>, tags) and sb (StringBuilder) exist from a filter above, e.g.
//          filter-by-category.cs with OST_DuctTags / OST_PipeTags, or filter-by-elements-in-view.cs.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ THIS IS THE OPPOSITE JOB TO tag-elements-in-active-view.cs, and both are wanted at different times.
//    That recipe PLACES new tags close to their elements, scoring each candidate spot so the tag sits
//    next to the duct it names. This one takes tags that ALREADY EXIST and moves them all OUT of the
//    drawing to the margins. Use the recipe on a normal plan; use this where the services are so dense
//    that no tag can sit near its element without covering another — a plant room, a riser, a detail.
//
// ✱✱ THE VIEW'S CROP BOX IS THE COORDINATE SYSTEM, and everything here happens inside it. "Left" and
//    "up" on the paper are not model X and Y — on a rotated or sloped view they are neither. So each
//    tag head is pushed through `view.CropBox.Transform.Inverse` into the view's own flat 2D space
//    (X right, Y up, Z ignored), all the arranging is plain 2D arithmetic there, and the result is
//    pushed back out with `view.CropBox.Transform`. Doing the maths in model coordinates works only on
//    an unrotated plan and fails silently everywhere else.
//
// ✱✱ THE CROP BOX MUST BE ON. It is not a preference — the crop box IS the edge the tags are parked
//    against, and with no crop there is no edge and no defensible answer to "how far out". Reported and
//    refused rather than guessed.
//
// ✱✱ A TAG'S BOUNDING BOX INCLUDES ITS LEADER, so tag height is measured from LEADERLESS tags only.
//    Same trap as action-stack-tags.cs: on a leadered tag `get_BoundingBox(view)` returns the box round
//    the head AND the leader line, which on a busy plan is metres. The row spacing here comes from the
//    tallest leaderless tag; if every tag has a leader the fragment says so and uses a paper default,
//    rather than spacing the columns by somebody's leader length.
//
// ✱✱ UNCROSSING IS A SWAP, AND IT IS A HEURISTIC. Once every tag has a slot, any two whose leaders
//    cross are swapped with each other — a crossing means the upper tag belongs to the lower element.
//    Two passes settle almost every real view; it is not guaranteed to reach zero crossings and the
//    count left over is reported honestly rather than claimed as none.
//
// GOTCHA: DRY RUN BY DEFAULT — this moves every tag in the set and eyeballing the result is the only
//         real check.
// GOTCHA: PINNED tags are skipped. Setting TagHeadPosition on a pinned tag changes nothing and does not
//         throw — the silent no-op class, see geometry-and-transforms.md.
// GOTCHA: A ROOM OR SPACE TAG MOVED OUT OF ITS ROOM MUST BE GIVEN A LEADER or it reads as belonging to
//         wherever it landed. Revit does not do this for you. Handled below for RoomTag/SpaceTag/AreaTag.
// GOTCHA: this moves HEADS. It does not set elbows — run action-force-tag-leader-lshape.cs afterwards
//         if the drawing standard wants L-shaped leaders.
// GOTCHA: more tags than slots means the columns run past the crop. The slot count is derived from the
//         crop height and the row spacing, and any tag that cannot be given a slot is left where it is
//         and reported, never stacked on top of another.
// RELATED: action-stack-tags.cs puts a chosen SUBSET in one column at a point you name; this puts
//          EVERYTHING at the two edges automatically.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run it on one crowded view with dryRun on,
//   read the slot table, then apply and look at the paper.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;
int? targetViewIdInt = null;      // null = active view
double rowGapMm = 0;              // PAPER mm between rows; 0 = tallest leaderless tag + 20%
double edgeInsetMm = 0;           // PAPER mm inside the crop edge; 0 = sit on the edge
bool uncrossLeaders = true;
int uncrossPasses = 2;
// ---- END INPUTS ----

const double MM = 304.8;

View view = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (view == null || view.IsTemplate)
{
    sb.AppendLine("Target view not found, or it is a view template.");
    return sb.ToString();
}

bool cropOn = false;
try { cropOn = view.CropBoxActive; } catch { }
if (!cropOn)
{
    sb.AppendLine($"View '{view.Name}' has NO ACTIVE CROP BOX. This fragment parks tags against the crop edges, so without one there is no edge to park against and no honest answer to how far out to go.");
    sb.AppendLine("Turn the crop on (action-set-crop-box-settings.cs), or use action-stack-tags.cs and name the point yourself.");
    return sb.ToString();
}

BoundingBoxXYZ crop = null;
try { crop = view.CropBox; } catch { }
if (crop == null) { sb.AppendLine("Could not read the view's crop box."); return sb.ToString(); }

int scale = 1;
try { scale = Math.Max(1, view.Scale); } catch { }

Transform toView, fromView;
try { toView = crop.Transform.Inverse; fromView = crop.Transform; }
catch { sb.AppendLine("Could not read the crop box transform."); return sb.ToString(); }

// ---- reflection: tag classes share no base for these ----
Func<Element, XYZ> headOf = el =>
{
    try { var p = el.GetType().GetProperty("TagHeadPosition"); return p == null ? null : p.GetValue(el, null) as XYZ; }
    catch { return null; }
};
Func<Element, XYZ, bool> setHead = (el, v) =>
{
    try
    {
        var p = el.GetType().GetProperty("TagHeadPosition");
        if (p == null || !p.CanWrite) return false;
        p.SetValue(el, v, null);
        return true;
    }
    catch { return false; }
};
Func<Element, bool> hasLeader = el =>
{
    try
    {
        var p = el.GetType().GetProperty("HasLeader");
        if (p == null) return false;
        var v = p.GetValue(el, null);
        return v is bool && (bool)v;
    }
    catch { return false; }
};
Func<Element, bool> giveLeader = el =>
{
    try
    {
        var p = el.GetType().GetProperty("HasLeader");
        if (p == null || !p.CanWrite) return false;
        p.SetValue(el, true, null);
        return true;
    }
    catch { return false; }
};

// The tagged element's position is the leader's anchor whatever the leader's own condition, and it
// avoids the whole per-version LeaderEnd dance. IndependentTag's target members changed name at Revit
// 2022 and the old ones were REMOVED, so both routes are looked up by name — a missing member is a
// COMPILE error, not something a try/catch can rescue.
var _mLocalIds = typeof(IndependentTag).GetMethod("GetTaggedLocalElementIds", Type.EmptyTypes); // 2022+
var _pLocalId  = typeof(IndependentTag).GetProperty("TaggedLocalElementId");                    // pre-2022

Func<Element, XYZ> anchorOf = el =>
{
    // 1. an IndependentTag: ask what it tags, and use that element's centre in the view
    var itag = el as IndependentTag;
    if (itag != null)
    {
        var ids = new List<ElementId>();
        try
        {
            if (_mLocalIds != null)
            {
                var raw = _mLocalIds.Invoke(itag, null) as System.Collections.IEnumerable;
                if (raw != null) foreach (var o in raw) { var id = o as ElementId; if (id != null) ids.Add(id); }
            }
            else if (_pLocalId != null)
            {
                var id = _pLocalId.GetValue(itag, null) as ElementId;
                if (id != null) ids.Add(id);
            }
        }
        catch { }
        foreach (var id in ids)
        {
            var target = Document.GetElement(id);
            if (target == null) continue;
            try
            {
                var bb = target.get_BoundingBox(view) ?? target.get_BoundingBox(null);
                if (bb != null) return (bb.Min + bb.Max) / 2;
            }
            catch { }
            try { var lp = target.Location as LocationPoint; if (lp != null) return lp.Point; } catch { }
        }
    }
    // 2. a room/space/area tag: its own location is over the room it names
    try { var lp2 = el.Location as LocationPoint; if (lp2 != null) return lp2.Point; } catch { }
    // 3. last resort: the head itself, which parks it on whichever side it is already nearest
    return headOf(el);
};

// ---- gather ----
var rows = new List<string>();
int pinnedSkipped = 0, noHead = 0, noAnchor = 0;
double tallest = 0; int measuredFrom = 0, leaderedCount = 0;

// tag, head in VIEW space, anchor in VIEW space
var tags = new List<Tuple<Element, XYZ, XYZ>>();

foreach (var el in elements)
{
    if (el == null || !el.IsValidObject) { noHead++; continue; }
    if (el.Pinned) { pinnedSkipped++; continue; }

    var head = headOf(el);
    if (head == null) { noHead++; continue; }

    var anchor = anchorOf(el);
    if (anchor == null) { noAnchor++; continue; }

    if (hasLeader(el)) leaderedCount++;
    else
    {
        try
        {
            var bb = el.get_BoundingBox(view);
            if (bb != null)
            {
                var lo = toView.OfPoint(bb.Min); var hi = toView.OfPoint(bb.Max);
                double h = Math.Abs(hi.Y - lo.Y);
                if (h > tallest) tallest = h;
                measuredFrom++;
            }
        }
        catch { }
    }

    var hv = toView.OfPoint(head);
    var av = toView.OfPoint(anchor);
    tags.Add(Tuple.Create(el, new XYZ(hv.X, hv.Y, 0), new XYZ(av.X, av.Y, 0)));
}

if (tags.Count == 0)
{
    sb.AppendLine($"No movable tags ({noHead} with no TagHeadPosition, {pinnedSkipped} pinned, {noAnchor} with no readable anchor).");
    return sb.ToString();
}

// ---- row spacing ----
double rowGapFt; string gapSource;
if (rowGapMm > 0) { rowGapFt = (rowGapMm * scale) / MM; gapSource = rowGapMm + " mm paper at 1:" + scale; }
else if (tallest > 0) { rowGapFt = tallest * 1.2; gapSource = "tallest of " + measuredFrom + " leaderless tag(s), " + Math.Round(tallest * MM) + " mm model + 20%"; }
else
{
    rowGapFt = (5.0 * scale) / MM;
    gapSource = "fallback 5 mm paper at 1:" + scale + " — all " + leaderedCount + " tag(s) have leaders, so none could be measured honestly (a leadered tag's box includes its leader). Set rowGapMm to be sure";
}

// ---- the two columns of slots, in view space ----
double left = Math.Min(crop.Min.X, crop.Max.X), right = Math.Max(crop.Min.X, crop.Max.X);
double bottom = Math.Min(crop.Min.Y, crop.Max.Y), top = Math.Max(crop.Min.Y, crop.Max.Y);
double inset = (edgeInsetMm * scale) / MM;
double midX = (left + right) / 2.0;

int slotsPerSide = (int)Math.Floor((top - bottom) / rowGapFt);
if (slotsPerSide < 1) slotsPerSide = 1;

Func<bool, List<XYZ>> slotsFor = isLeft =>
{
    var list = new List<XYZ>();
    double x = isLeft ? left + inset : right - inset;
    for (int i = 0; i < slotsPerSide; i++) list.Add(new XYZ(x, top - (i + 0.5) * rowGapFt, 0));
    return list;
};

var leftSlots = slotsFor(true);
var rightSlots = slotsFor(false);

// side = whichever edge the tag's own ELEMENT is nearer, so leaders point inward and stay short
var leftTags = tags.Where(t => t.Item3.X <= midX).OrderBy(t => -t.Item3.Y).ToList();
var rightTags = tags.Where(t => t.Item3.X > midX).OrderBy(t => -t.Item3.Y).ToList();

// ---- assign: nearest free slot to where the tag already is, then take that slot out of the pool ----
var placement = new Dictionary<ElementId, XYZ>();
var unplaced = new List<Element>();

Action<List<Tuple<Element, XYZ, XYZ>>, List<XYZ>> assign = (list, slots) =>
{
    foreach (var t in list)
    {
        if (slots.Count == 0) { unplaced.Add(t.Item1); continue; }
        XYZ best = slots[0]; double bestD = t.Item2.DistanceTo(best);
        foreach (var s in slots)
        {
            double d = t.Item2.DistanceTo(s);
            if (d < bestD) { bestD = d; best = s; }
        }
        slots.Remove(best);
        placement[t.Item1.Id] = best;
    }
};
assign(leftTags, leftSlots);
assign(rightTags, rightSlots);

// ---- uncross: a crossing means two tags hold each other's slot, so swap them ----
Func<XYZ, XYZ, XYZ, XYZ, bool> segmentsCross = (p1, p2, p3, p4) =>
{
    // 2D orientation test in view space; touching endpoints do not count as a crossing
    Func<XYZ, XYZ, XYZ, double> cross = (a, b, c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    double d1 = cross(p3, p4, p1), d2 = cross(p3, p4, p2), d3 = cross(p1, p2, p3), d4 = cross(p1, p2, p4);
    return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
};

int crossingsBefore = 0, crossingsAfter = 0;
Func<List<Tuple<Element, XYZ, XYZ>>, int> countCrossings = list =>
{
    int n = 0;
    for (int i = 0; i < list.Count; i++)
        for (int j = i + 1; j < list.Count; j++)
        {
            XYZ hi, hj;
            if (!placement.TryGetValue(list[i].Item1.Id, out hi)) continue;
            if (!placement.TryGetValue(list[j].Item1.Id, out hj)) continue;
            if (segmentsCross(hi, list[i].Item3, hj, list[j].Item3)) n++;
        }
    return n;
};

crossingsBefore = countCrossings(leftTags) + countCrossings(rightTags);

if (uncrossLeaders)
{
    Action<List<Tuple<Element, XYZ, XYZ>>> uncross = list =>
    {
        for (int pass = 0; pass < Math.Max(1, uncrossPasses); pass++)
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    XYZ hi, hj;
                    if (!placement.TryGetValue(list[i].Item1.Id, out hi)) continue;
                    if (!placement.TryGetValue(list[j].Item1.Id, out hj)) continue;
                    if (segmentsCross(hi, list[i].Item3, hj, list[j].Item3))
                    {
                        placement[list[i].Item1.Id] = hj;
                        placement[list[j].Item1.Id] = hi;
                    }
                }
    };
    uncross(leftTags);
    uncross(rightTags);
}

crossingsAfter = countCrossings(leftTags) + countCrossings(rightTags);

// ---- report ----
sb.AppendLine($"View '{view.Name}' at 1:{scale} — {tags.Count} movable tag(s): {leftTags.Count} to the left edge, {rightTags.Count} to the right.");
sb.AppendLine($"Row spacing: {Math.Round(rowGapFt * MM)} mm model ({gapSource}). {slotsPerSide} slot(s) per side.");
if (unplaced.Count > 0) sb.AppendLine($"⚠ {unplaced.Count} tag(s) had no slot left and are LEFT WHERE THEY ARE — the crop is not tall enough for this many rows. Reduce rowGapMm or split the view.");
if (pinnedSkipped > 0 || noHead > 0 || noAnchor > 0)
    sb.AppendLine($"Skipped: {pinnedSkipped} pinned, {noHead} with no TagHeadPosition, {noAnchor} with no readable anchor.");
if (uncrossLeaders) sb.AppendLine($"Leader crossings: {crossingsBefore} before, {crossingsAfter} after {Math.Max(1, uncrossPasses)} swap pass(es).{(crossingsAfter > 0 ? " Swapping is a heuristic — the remainder need a hand." : "")}");

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing moved. Set dryRun = false to apply.");
    return sb.ToString();
}

int moved = 0, failed = 0, leadersTurnedOn = 0;
using (var t = new Transaction(Document, "AJ Tools - Arrange Tags to View Edges"))
{
    t.Start();
    try
    {
        var fo = t.GetFailureHandlingOptions();
        fo.SetForcedModalHandling(false);
        fo.SetClearAfterRollback(true);
        t.SetFailureHandlingOptions(fo);

        foreach (var tg in tags)
        {
            XYZ slot;
            if (!placement.TryGetValue(tg.Item1.Id, out slot)) continue;

            if (!setHead(tg.Item1, fromView.OfPoint(slot))) { failed++; continue; }

            // A room/space/area tag parked at the edge is no longer over its room — without a leader it
            // reads as naming whatever it landed on. Revit does not add one for you.
            if (!hasLeader(tg.Item1))
            {
                var tn = tg.Item1.GetType().Name;
                if (tn == "RoomTag" || tn == "SpaceTag" || tn == "AreaTag")
                    if (giveLeader(tg.Item1)) leadersTurnedOn++;
            }
            moved++;
        }

        t.Commit();
        sb.AppendLine();
        sb.AppendLine($"Moved {moved} tag(s) to the crop edges. {failed} refused.{(leadersTurnedOn > 0 ? $" Turned the leader on for {leadersTurnedOn} room/space/area tag(s) now sitting outside their own space." : "")}");
        sb.AppendLine("Look at the paper — the columns are Revit's own tag heads, so anything that still overlaps is a tag taller than the row spacing.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
