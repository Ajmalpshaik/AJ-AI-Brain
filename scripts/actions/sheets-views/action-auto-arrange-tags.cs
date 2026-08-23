// ============================================================
// FRAGMENT (action) — action-auto-arrange-tags.cs
// PURPOSE: Push overlapping tags apart until nothing sits on top of anything else, leaving every tag as
//          near its original position as it can be and its leader still pointing at its element. The
//          tidy-up after a bulk tagging run, and the fix for the view where two tags print as one
//          unreadable smudge.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the TAGS,
//          e.g. filter-by-category.cs on a tag category, or filter-by-elements-in-view.cs. A set that
//          contains non-tags is fine; they are counted and ignored.
//
// ✱✱ WHY THIS IS NOT THE THREE TAG-TIDYING FRAGMENTS ALREADY HERE. action-arrange-tags-to-view-edges.cs
//    parks everything down the left and right edges; action-stack-tags.cs puts them in one column;
//    action-center-room-tags.cs centres room tags. All three MOVE TAGS SOMEWHERE CHOSEN. This one leaves
//    every tag where it is unless it has to move, and moves it the smallest distance that clears the
//    collision — which keeps a tag near the thing it labels, and that is what makes a drawing readable
//    rather than merely tidy.
//
// ✱✱ IT IS AN ITERATIVE PUSH, NOT A LAYOUT SOLVER, and it says how far it got. Each pass finds every
//    overlapping pair and pushes both apart along the line between their centres. Passes repeat until
//    nothing overlaps or `maxPasses` runs out. If it runs out, the report says how many pairs are STILL
//    overlapping — a genuinely over-full view cannot be fixed by nudging, and pretending otherwise would
//    be worse than saying so. That is when the edge-parking fragment is the right answer instead.
//
// ✱✱ TAG SIZE IS READ FROM THE VIEW, so the spacing is right at the scale the sheet actually prints at.
//    A gap that looks generous at 1:100 disappears at 1:50; using the tag's real bounding box in the
//    view rather than a fixed offset is what makes the result scale-correct.
//
// GOTCHA: DRY RUN BY DEFAULT — it reports how many collisions there are and how many passes it needs,
//         and moves nothing. Read that, then set dryRun = false.
// GOTCHA: TAG CLASSES SHARE NO BASE EXPOSING `TagHeadPosition` — IndependentTag, RoomTag, SpaceTag and
//         AreaTag each declare their own. It is reached by reflection for that reason (the same approach
//         as action-stack-tags.cs). A tag class without the property is counted and left alone.
// GOTCHA: PINNED TAGS ARE NOT MOVED. They are counted and named — a pinned tag is usually pinned on
//         purpose, and silently moving it is worse than leaving a collision.
// GOTCHA: IT ONLY SEES THE TAGS YOU GIVE IT. A tag that overlaps a TEXT NOTE or a dimension is not moved,
//         because that annotation is not in the set — action-check-annotation-overlap.cs is the sweep
//         that finds those.
// GOTCHA: MOVING A TAG DOES NOT MOVE ITS LEADER'S ELBOW, so a long move can leave an awkward leader.
//         `maxMoveMm` caps how far any tag is allowed to travel; a tag that cannot be cleared within
//         that is reported rather than dragged across the view.
// RELATED: action-auto-tag-mep.cs (place them first), action-check-annotation-overlap.cs (what is still
//          overlapping, including text and dimensions), action-arrange-tags-to-view-edges.cs (the answer
//          for a genuinely over-full view), action-force-tag-leader-lshape.cs (leader shape afterwards).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run the dry pass, then try it on one busy
//   area and look at the result before doing a whole sheet.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;              // true = count the collisions and the passes needed, move nothing
ElementId viewId = null;         // null = the active view (tags are view-specific)
double gapMm = 2.0;              // clear space to leave between two tags, in PAPER mm at the view's scale
int maxPasses = 25;              // give up after this many passes and report what is left
double maxMoveMm = 4000;         // model mm; refuse to drag a tag further than this from where it started
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

if (elements == null || elements.Count == 0)
{
    sb.AppendLine("No elements in — put a filter above this (the tags).");
    return sb.ToString();
}

var view = viewId != null ? Document.GetElement(viewId) as View : Document.ActiveView;
if (view == null) { sb.AppendLine("STOP: no view resolved."); return sb.ToString(); }

// Paper mm -> model mm. A 2 mm paper gap is 200 model mm at 1:100 and 100 at 1:50 — this is what makes
// the result correct at the scale the sheet prints at rather than at whatever scale it was tuned on.
int scale = view.Scale <= 0 ? 100 : view.Scale;
double gapModelFt = ToFeet(gapMm * scale);

// ---- reflection: tag classes share no common base with TagHeadPosition ----
Func<Element, XYZ> headOf = el =>
{
    try { var p = el.GetType().GetProperty("TagHeadPosition"); return p == null ? null : p.GetValue(el, null) as XYZ; }
    catch { return null; }
};
Func<Element, XYZ, bool> setHead = (el, pt) =>
{
    try
    {
        var p = el.GetType().GetProperty("TagHeadPosition");
        if (p == null || !p.CanWrite) return false;
        p.SetValue(el, pt, null);
        return true;
    }
    catch { return false; }
};

// ---- gather the movable tags with their boxes ----
var items = new List<(Element El, XYZ Home, XYZ Now, double HalfX, double HalfY)>();
int noHead = 0, pinned = 0, noBox = 0;

foreach (var el in elements)
{
    var head = headOf(el);
    if (head == null) { noHead++; continue; }
    if (el.Pinned) { pinned++; continue; }

    BoundingBoxXYZ bb = null;
    try { bb = el.get_BoundingBox(view); } catch { }
    if (bb == null) { noBox++; continue; }

    double halfX = Math.Max((bb.Max.X - bb.Min.X) / 2.0, ToFeet(1));
    double halfY = Math.Max((bb.Max.Y - bb.Min.Y) / 2.0, ToFeet(1));
    items.Add((el, head, head, halfX, halfY));
}

sb.AppendLine($"ARRANGE TAGS — view '{view.Name}' at 1:{scale}, clear gap {gapMm:F1} mm on paper ({(gapMm * scale):F0} model mm)");
sb.AppendLine($"Movable tags: {items.Count}" +
              (noHead > 0 ? $"   no TagHeadPosition: {noHead}" : "") +
              (pinned > 0 ? $"   pinned (left alone): {pinned}" : "") +
              (noBox > 0 ? $"   no bounding box in this view: {noBox}" : ""));

if (items.Count < 2)
{
    sb.AppendLine("Fewer than two movable tags — nothing can overlap.");
    return sb.ToString();
}

// ---- the push ----
// Positions are worked out in memory first, so a dry run costs nothing and the real run makes one
// write per tag instead of one per pass.
var pos = items.Select(i => i.Now).ToList();

Func<int, int, bool> overlaps = (i, j) =>
{
    double dx = Math.Abs(pos[i].X - pos[j].X);
    double dy = Math.Abs(pos[i].Y - pos[j].Y);
    double needX = items[i].HalfX + items[j].HalfX + gapModelFt;
    double needY = items[i].HalfY + items[j].HalfY + gapModelFt;
    return dx < needX && dy < needY;
};

int initialCollisions = 0;
for (int i = 0; i < items.Count; i++)
    for (int j = i + 1; j < items.Count; j++)
        if (overlaps(i, j)) initialCollisions++;

int passesUsed = 0;
int remaining = initialCollisions;

for (int pass = 0; pass < maxPasses && remaining > 0; pass++)
{
    passesUsed++;
    bool movedAny = false;

    for (int i = 0; i < items.Count; i++)
    {
        for (int j = i + 1; j < items.Count; j++)
        {
            if (!overlaps(i, j)) continue;

            double needX = items[i].HalfX + items[j].HalfX + gapModelFt;
            double needY = items[i].HalfY + items[j].HalfY + gapModelFt;
            double dx = pos[j].X - pos[i].X;
            double dy = pos[j].Y - pos[i].Y;

            // Push along whichever axis needs the LESS movement to clear — the smallest move that
            // solves it keeps every tag as near its element as possible.
            double pushX = needX - Math.Abs(dx);
            double pushY = needY - Math.Abs(dy);

            double mx = 0, my = 0;
            if (pushX <= pushY)
            {
                double dir = dx >= 0 ? 1 : -1;
                if (Math.Abs(dx) < 1e-9) dir = ((i + j) % 2 == 0) ? 1 : -1;  // exactly stacked: split them
                mx = dir * (pushX / 2.0 + 1e-6);
            }
            else
            {
                double dir = dy >= 0 ? 1 : -1;
                if (Math.Abs(dy) < 1e-9) dir = ((i + j) % 2 == 0) ? 1 : -1;
                my = dir * (pushY / 2.0 + 1e-6);
            }

            pos[i] = new XYZ(pos[i].X - mx, pos[i].Y - my, pos[i].Z);
            pos[j] = new XYZ(pos[j].X + mx, pos[j].Y + my, pos[j].Z);
            movedAny = true;
        }
    }

    remaining = 0;
    for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
            if (overlaps(i, j)) remaining++;

    if (!movedAny) break;
}

// ---- anything dragged too far goes back where it started ----
var overMoved = new List<ElementId>();
for (int i = 0; i < items.Count; i++)
{
    double moved = ToMm(items[i].Home.DistanceTo(pos[i]));
    if (moved > maxMoveMm) { pos[i] = items[i].Home; overMoved.Add(items[i].El.Id); }
}

int willMove = 0;
double totalMove = 0, worstMove = 0;
for (int i = 0; i < items.Count; i++)
{
    double d = ToMm(items[i].Home.DistanceTo(pos[i]));
    if (d > 0.5) { willMove++; totalMove += d; worstMove = Math.Max(worstMove, d); }
}

sb.AppendLine();
sb.AppendLine($"Overlapping pairs at the start: {initialCollisions}");
sb.AppendLine($"Passes used: {passesUsed} of {maxPasses}");
sb.AppendLine($"Overlapping pairs left: {remaining}");
sb.AppendLine($"Tags that need to move: {willMove}   average move {(willMove > 0 ? totalMove / willMove : 0):F0} mm, furthest {worstMove:F0} mm");
if (overMoved.Count > 0)
    sb.AppendLine($"PUT BACK ({overMoved.Count}) — these would have travelled further than maxMoveMm and were returned to where they started, so they may still collide: {string.Join(", ", overMoved.Take(15).Select(i => i.ToString()))}");

if (initialCollisions == 0)
{
    sb.AppendLine();
    sb.AppendLine("CLEAR — no tag in this set overlaps another. Nothing to do.");
    return sb.ToString();
}

if (remaining > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NOT FULLY SOLVED — {remaining} pair(s) still overlap after {passesUsed} pass(es). Nudging cannot fix a view with more tags than space:");
    sb.AppendLine("  raise maxPasses, reduce gapMm, or use action-arrange-tags-to-view-edges.cs, which parks tags at the crop edges instead.");
}

if (willMove == 0) return sb.ToString();

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing moved. Set dryRun = false to apply.");
    return sb.ToString();
}

// ---- apply ----
int done = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - arrange tags"))
{
    tx.Start();
    try
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ToMm(items[i].Home.DistanceTo(pos[i])) <= 0.5) continue;
            if (setHead(items[i].El, pos[i])) done++;
            else failures.Add($"{items[i].El.Id}: TagHeadPosition would not accept the new point");
        }
        tx.Commit();
    }
    catch (Exception ex)
    {
        tx.RollBack();
        sb.AppendLine($"FAILED (arrange tags) — rolled back, nothing moved. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine();
sb.AppendLine($"MOVED: {done} of {willMove} tag(s).");
if (failures.Count > 0)
{
    sb.AppendLine("NOT MOVED:");
    foreach (var f in failures.Take(20)) sb.AppendLine($"  {f}");
}

return sb.ToString();
