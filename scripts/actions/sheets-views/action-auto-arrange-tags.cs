// ============================================================
// FRAGMENT (action) — action-auto-arrange-tags.cs
// PURPOSE: Push overlapping tags apart until nothing sits on top of anything else, leaving every tag as
//          near its original position as it can be. The tidy-up for tags THAT ALREADY EXIST — placed by
//          hand, by an earlier run, or by action-auto-tag-mep.cs — which is the case the placement
//          recipe cannot reach because it only arranges the tags it places itself.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter above — the TAGS,
//          e.g. filter-by-category.cs on a tag category, or filter-by-elements-in-view.cs. A set that
//          contains non-tags is fine; they are counted and ignored.
//
// ✱✱ READ ../../../knowledge/live-model/tagging.md BEFORE TOUCHING THIS FILE. Four separate findings in
//    it are live measurements against a real model, and this fragment originally got all four wrong
//    (written 2026-08-23, corrected 2026-08-24 when Ajmal asked whether the new tag fragments would
//    clash with the existing ones — they did). They are restated below because getting any one of them
//    wrong makes this fragment look broken in a way that reads like a different bug.
//
// ✱✱ 1. A TAG'S BOUNDING BOX INCLUDES ITS LEADER, so it cannot be used to measure the tag. On a leadered
//    tag `get_BoundingBox(view)` returns the box round the head AND the leader line — on a congested plan
//    that is metres, not millimetres, and spacing derived from it flings tags metres apart. So sizes are
//    measured ONLY from leaderless tags; leadered ones take the median of those, or a stated paper
//    default when the whole set is leadered. The report says which was used and how many it measured
//    from — a stated default beats a measured number that is silently wrong.
//
// ✱✱ 2. AN L-SHAPED TAG STAYS PUT; THE STRAIGHT ONE MOVES. Ajmal's own instruction: "try to make clash
//    free with moving straight leader tag, L shaped one keep same place, no need to move." A bent leader
//    has already been threaded round something — its own text, a duct, another tag — so nudging it risks
//    reopening a problem that was already solved. When exactly one of a clashing pair is straight, that
//    one takes 100% of the separation; only a pair of the same kind splits it 50/50. Straight vs L-shaped
//    is derived FRESH from each tag's current elbow, never tracked as state.
//
// ✱✱ 3. IT IS TWO-PHASE, because the preference above can fail to converge — measured live, 1 tag of 38
//    had genuinely no room to escape by moving alone. Phase 1 runs the straight-only preference for its
//    full budget; if pairs remain, Phase 2 drops the preference and lets both move. Which pairs needed
//    the exception is REPORTED — a "keep it in place" preference must not silently block real clash
//    resolution, and must not be silently dropped either.
//
// ✱✱ 4. MOVING A TAG DRAGS A FREE LEADER'S END OFF ITS ELEMENT. A leader whose LeaderEndCondition is
//    Attached follows its element and needs nothing; a Free one has its own end point that travels with
//    the head. Every free leader end is read BEFORE the move and written back after, or a bulk tidy-up
//    quietly pulls every arrow off the thing it was pointing at.
//
// ✱✱ THE ARITHMETIC IS IN VIEW SPACE, not model X/Y. "Left" and "up" on the paper are not model X and Y
//    on a rotated plan, a section or a sloped view. Every position goes through view.RightDirection /
//    view.UpDirection first, so the result is right on any view rather than only on a north-up plan.
//
// GOTCHA: DRY RUN BY DEFAULT — it reports the collisions, the phases needed and what would move.
// GOTCHA: **For a CONGESTED view, recipes/tag-elements-in-active-view.cs is the better tool and it is
//         live-verified**: it scores each tag's side, follows real flow direction, recomputes each
//         elbow as it goes, and resolves overlaps as part of placement. Use this fragment when the tags
//         already exist and you only want them separated.
// GOTCHA: IT DOES NOT RECOMPUTE ELBOWS. The placement recipe re-derives each tag's elbow after a move
//         with the same function that placed it; that function is part of the recipe, not of a general
//         tidy-up. So a moved L-shaped tag can be left with an awkward elbow — another reason the
//         straight-first preference is the default and not an option.
// GOTCHA: PINNED TAGS ARE NOT MOVED — counted and named. A pinned tag is usually pinned on purpose.
// GOTCHA: IT ONLY SEES THE TAGS YOU GIVE IT. A tag overlapping a TEXT NOTE or a dimension is not moved —
//         action-check-annotation-overlap.cs is the sweep that finds those.
// SOURCE: ../../../knowledge/live-model/tagging.md — sections "A tag's bounding box includes its LEADER",
//         "Prefer moving the straight-leader tag", "Tag-vs-tag overlap resolution", "Moving a tag moves
//         its leader end with it", and "Aligning annotation: do the arithmetic in the VIEW's coordinates".
// RELATED: recipes/tag-elements-in-active-view.cs (scored placement + its own PASS 2 resolver — the
//          proven route), action-auto-tag-mep.cs (place them first), action-stack-tags.cs and
//          action-arrange-tags-to-view-edges.cs (park them somewhere chosen instead),
//          action-check-annotation-overlap.cs (what is still overlapping, text and dimensions included).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23, rewritten 2026-08-24 against the four
//   findings above. Run the dry pass, try it on one busy area, and look at the leaders afterwards.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report collisions and what would move, move nothing
ElementId viewId = null;             // null = the active view (tags are view-specific)
double gapMm = 2.0;                  // clear space between two tags, in PAPER mm at the view's scale
int phase1Passes = 15;               // straight-leader-only budget (measured: real convergence ~3)
int phase2Passes = 10;               // unrestricted 50/50 fallback for whatever Phase 1 could not solve
double maxMoveMm = 4000;             // model mm; refuse to drag a tag further than this
bool preferMovingStraightLeader = true;   // Ajmal's rule — leave L-shaped tags where they are

// Used ONLY when the set has no leaderless tag to measure from. Paper mm.
double defaultTagWidthMm = 20;
double defaultTagHeightMm = 3;
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

int scale = 1;
try { scale = Math.Max(1, view.Scale); } catch { }
double gapFt = ToFeet(gapMm * scale);

// ---- view space. "Left"/"up" on paper are not model X/Y on a rotated plan or a section. ----
XYZ rt = view.RightDirection, up = view.UpDirection, o = view.Origin;
Func<XYZ, double> vx = p => (p - o).DotProduct(rt);
Func<XYZ, double> vy = p => (p - o).DotProduct(up);

// ---- reflection: tag classes share no base exposing these ----
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

// The tagged references, needed for every per-reference leader call on 2022+.
Func<Element, List<object>> taggedRefs = el =>
{
    var outp = new List<object>();
    try
    {
        var m = el.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
        if (m == null) return outp;
        var list = m.Invoke(el, null) as System.Collections.IEnumerable;
        if (list != null) foreach (var r in list) outp.Add(r);
    }
    catch { }
    return outp;
};

// L-SHAPED = it has an elbow. Derived fresh from the tag's CURRENT geometry, never tracked as state.
// Pre-2022 exposes a `LeaderElbow` property; 2022+ moved to GetLeaderElbow(Reference) with
// HasLeaderElbow(Reference) as the guard — calling the getter without it throws on a straight leader.
Func<Element, bool> isLShaped = el =>
{
    if (!hasLeader(el)) return false;
    try
    {
        var prop = el.GetType().GetProperty("LeaderElbow");
        if (prop != null) return prop.GetValue(el, null) as XYZ != null;

        foreach (var r in taggedRefs(el))
        {
            var has = el.GetType().GetMethod("HasLeaderElbow", new[] { r.GetType() });
            if (has != null)
            {
                var v = has.Invoke(el, new[] { r });
                if (v is bool && (bool)v) return true;
            }
        }
    }
    catch { }
    return false;
};

// Free leader ends travel with the head and must be put back. Attached ones follow their element.
Func<Element, bool> hasFreeLeaderEnd = el =>
{
    try
    {
        var p = el.GetType().GetProperty("LeaderEndCondition");
        if (p == null) return false;
        var v = p.GetValue(el, null);
        return v != null && v.ToString() == "Free";
    }
    catch { return false; }
};
Func<Element, List<Tuple<object, XYZ>>> captureEnds = el =>
{
    var saved = new List<Tuple<object, XYZ>>();
    if (!hasLeader(el) || !hasFreeLeaderEnd(el)) return saved;
    try
    {
        var prop = el.GetType().GetProperty("LeaderEnd");
        if (prop != null)
        {
            var pt = prop.GetValue(el, null) as XYZ;
            if (pt != null) saved.Add(Tuple.Create((object)null, pt));
            return saved;
        }
        foreach (var r in taggedRefs(el))
        {
            var gm = el.GetType().GetMethod("GetLeaderEnd", new[] { r.GetType() });
            if (gm == null) continue;
            var pt = gm.Invoke(el, new[] { r }) as XYZ;
            if (pt != null) saved.Add(Tuple.Create((object)r, pt));
        }
    }
    catch { }
    return saved;
};
Func<Element, List<Tuple<object, XYZ>>, bool> restoreEnds = (el, saved) =>
{
    if (saved == null || saved.Count == 0) return true;
    try
    {
        foreach (var s in saved)
        {
            if (s.Item1 == null)
            {
                var prop = el.GetType().GetProperty("LeaderEnd");
                if (prop != null && prop.CanWrite) prop.SetValue(el, s.Item2, null);
                continue;
            }
            var sm = el.GetType().GetMethod("SetLeaderEnd", new[] { s.Item1.GetType(), typeof(XYZ) });
            if (sm != null) sm.Invoke(el, new object[] { s.Item1, s.Item2 });
        }
        return true;
    }
    catch { return false; }
};

// ---- gather ----
var els = new List<Element>();
var homeX = new List<double>(); var homeY = new List<double>();
var posX = new List<double>(); var posY = new List<double>();
var halfW = new List<double>(); var halfH = new List<double>();
var lShaped = new List<bool>();

int noHead = 0, pinned = 0;
int measuredFrom = 0, leaderedNotMeasured = 0;
var measuredW = new List<double>(); var measuredH = new List<double>();

foreach (var el in elements)
{
    if (el == null || !el.IsValidObject) { noHead++; continue; }
    if (el.Pinned) { pinned++; continue; }
    var head = headOf(el);
    if (head == null) { noHead++; continue; }

    double w = -1, h = -1;
    // ONLY a leaderless tag can be measured — a leadered one's box encloses the leader line.
    if (!hasLeader(el))
    {
        try
        {
            var bb = el.get_BoundingBox(view);
            if (bb != null)
            {
                var d = bb.Max - bb.Min;
                w = Math.Abs(d.DotProduct(rt));
                h = Math.Abs(d.DotProduct(up));
                if (w > 0 && h > 0) { measuredW.Add(w); measuredH.Add(h); measuredFrom++; }
            }
        }
        catch { }
    }
    else leaderedNotMeasured++;

    els.Add(el);
    homeX.Add(vx(head)); homeY.Add(vy(head));
    posX.Add(vx(head)); posY.Add(vy(head));
    halfW.Add(w > 0 ? w / 2.0 : -1);
    halfH.Add(h > 0 ? h / 2.0 : -1);
    lShaped.Add(isLShaped(el));
}

if (els.Count < 2)
{
    sb.AppendLine($"Fewer than two movable tags ({noHead} had no TagHeadPosition, {pinned} pinned) — nothing can overlap.");
    return sb.ToString();
}

// A leadered tag takes the MEDIAN of what was measured, or the stated paper default if nothing was.
Func<List<double>, double> median = list =>
{
    if (list.Count == 0) return -1;
    var s = list.OrderBy(x => x).ToList();
    return s[s.Count / 2];
};
double medW = median(measuredW), medH = median(measuredH);
string sizeSource;
if (medW > 0 && medH > 0)
    sizeSource = $"median of {measuredFrom} leaderless tag(s): {ToMm(medW) / scale:F1} x {ToMm(medH) / scale:F1} mm on paper";
else
{
    medW = ToFeet(defaultTagWidthMm * scale);
    medH = ToFeet(defaultTagHeightMm * scale);
    sizeSource = $"STATED DEFAULT {defaultTagWidthMm:F1} x {defaultTagHeightMm:F1} mm on paper — every tag in the set is leadered, so none could be measured";
}
for (int i = 0; i < els.Count; i++)
{
    if (halfW[i] <= 0) halfW[i] = medW / 2.0;
    if (halfH[i] <= 0) halfH[i] = medH / 2.0;
}

sb.AppendLine($"ARRANGE TAGS — view '{view.Name}' at 1:{scale}, clear gap {gapMm:F1} mm on paper");
sb.AppendLine($"Movable tags: {els.Count}" +
              (noHead > 0 ? $"   no TagHeadPosition: {noHead}" : "") +
              (pinned > 0 ? $"   pinned (left alone): {pinned}" : ""));
sb.AppendLine($"Tag size source: {sizeSource}");
if (leaderedNotMeasured > 0)
    sb.AppendLine($"  ({leaderedNotMeasured} leadered tag(s) were NOT measured — a leadered tag's bounding box encloses its leader line, so measuring it gives the leader's reach, not the tag)");
sb.AppendLine($"L-shaped (bent leader, kept in place where possible): {lShaped.Count(x => x)}   straight/leaderless: {lShaped.Count(x => !x)}");
sb.AppendLine();

// ---- the push ----
Func<int, int, bool> overlaps = (i, j) =>
    Math.Abs(posX[i] - posX[j]) < halfW[i] + halfW[j] + gapFt &&
    Math.Abs(posY[i] - posY[j]) < halfH[i] + halfH[j] + gapFt;

Func<int> countOverlaps = () =>
{
    int n = 0;
    for (int i = 0; i < els.Count; i++)
        for (int j = i + 1; j < els.Count; j++)
            if (overlaps(i, j)) n++;
    return n;
};

int initial = countOverlaps();
var neededException = new List<string>();

// One pass. `straightOnly` = Phase 1's preference: when exactly one of a pair is straight it takes the
// whole push. Returns true if anything moved.
Func<bool, bool> onePass = straightOnly =>
{
    bool moved = false;
    for (int i = 0; i < els.Count; i++)
    {
        for (int j = i + 1; j < els.Count; j++)
        {
            if (!overlaps(i, j)) continue;

            double needX = halfW[i] + halfW[j] + gapFt;
            double needY = halfH[i] + halfH[j] + gapFt;
            double dx = posX[j] - posX[i], dy = posY[j] - posY[i];
            double pushX = needX - Math.Abs(dx), pushY = needY - Math.Abs(dy);

            // Minimum-translation-vector: separate along whichever axis needs the SMALLER move, so each
            // tag stays as near its element as possible.
            bool useX = pushX <= pushY;
            double amount = useX ? pushX : pushY;
            double dir;
            if (useX) { dir = dx >= 0 ? 1 : -1; if (Math.Abs(dx) < 1e-9) dir = ((i + j) % 2 == 0) ? 1 : -1; }
            else { dir = dy >= 0 ? 1 : -1; if (Math.Abs(dy) < 1e-9) dir = ((i + j) % 2 == 0) ? 1 : -1; }

            // Who moves. Ajmal's rule: the straight one takes it; the L-shaped one stays.
            double shareI = 0.5, shareJ = 0.5;
            if (straightOnly && preferMovingStraightLeader && lShaped[i] != lShaped[j])
            {
                if (lShaped[i]) { shareI = 0.0; shareJ = 1.0; }
                else { shareI = 1.0; shareJ = 0.0; }
            }

            double mv = amount + 1e-6;
            if (useX)
            {
                posX[i] -= dir * mv * shareI;
                posX[j] += dir * mv * shareJ;
            }
            else
            {
                posY[i] -= dir * mv * shareI;
                posY[j] += dir * mv * shareJ;
            }
            moved = true;
        }
    }
    return moved;
};

int phase1Used = 0, phase2Used = 0;
int remaining = initial;

for (int p = 0; p < phase1Passes && remaining > 0; p++)
{
    phase1Used++;
    if (!onePass(true)) break;
    remaining = countOverlaps();
}

int afterPhase1 = remaining;
if (remaining > 0 && phase2Passes > 0)
{
    for (int p = 0; p < phase2Passes && remaining > 0; p++)
    {
        phase2Used++;
        if (!onePass(false)) break;
        remaining = countOverlaps();
    }
    if (afterPhase1 != remaining)
        neededException.Add($"{afterPhase1 - remaining} pair(s) could only be separated by moving an L-shaped tag as well — the straight-only preference could not solve them alone.");
}

// ---- anything dragged too far goes home ----
var overMoved = new List<ElementId>();
for (int i = 0; i < els.Count; i++)
{
    double dMm = ToMm(Math.Sqrt(Math.Pow(posX[i] - homeX[i], 2) + Math.Pow(posY[i] - homeY[i], 2)));
    if (dMm > maxMoveMm) { posX[i] = homeX[i]; posY[i] = homeY[i]; overMoved.Add(els[i].Id); }
}

int willMove = 0; double totalMove = 0, worstMove = 0;
for (int i = 0; i < els.Count; i++)
{
    double d = ToMm(Math.Sqrt(Math.Pow(posX[i] - homeX[i], 2) + Math.Pow(posY[i] - homeY[i], 2)));
    if (d > 0.5) { willMove++; totalMove += d; worstMove = Math.Max(worstMove, d); }
}

sb.AppendLine($"Overlapping pairs at the start: {initial}");
sb.AppendLine($"Phase 1 (straight-leader tags only): {phase1Used} pass(es) -> {afterPhase1} pair(s) left");
if (phase2Used > 0) sb.AppendLine($"Phase 2 (both tags may move):        {phase2Used} pass(es) -> {remaining} pair(s) left");
sb.AppendLine($"Tags that need to move: {willMove}   average {(willMove > 0 ? totalMove / willMove : 0):F0} mm, furthest {worstMove:F0} mm");
foreach (var n in neededException) sb.AppendLine($"NOTE: {n}");
if (overMoved.Count > 0)
    sb.AppendLine($"PUT BACK ({overMoved.Count}) — further than maxMoveMm, returned home and may still collide: {string.Join(", ", overMoved.Take(15).Select(i => i.ToString()))}");

if (initial == 0)
{
    sb.AppendLine();
    sb.AppendLine("CLEAR — no tag in this set overlaps another. Nothing to do.");
    return sb.ToString();
}
if (remaining > 0)
{
    sb.AppendLine();
    sb.AppendLine($"NOT FULLY SOLVED — {remaining} pair(s) still overlap. Nudging cannot fix a view with more tags than space:");
    sb.AppendLine("  raise the pass budgets, reduce gapMm, or use action-arrange-tags-to-view-edges.cs, which parks tags at the crop edges instead.");
}
if (willMove == 0) return sb.ToString();

if (dryRun)
{
    sb.AppendLine();
    sb.AppendLine("DRY RUN — nothing moved. Set dryRun = false to apply.");
    return sb.ToString();
}

// ---- apply ----
int done = 0, endsRestored = 0;
var failures = new List<string>();

using (var tx = new Transaction(Document, "AJ Tools - arrange tags"))
{
    tx.Start();
    try
    {
        for (int i = 0; i < els.Count; i++)
        {
            double dMm = ToMm(Math.Sqrt(Math.Pow(posX[i] - homeX[i], 2) + Math.Pow(posY[i] - homeY[i], 2)));
            if (dMm <= 0.5) continue;

            // Read the free leader end BEFORE the move — setting the head drags it along.
            var saved = captureEnds(els[i]);

            var head = headOf(els[i]);
            if (head == null) { failures.Add($"{els[i].Id}: lost its head position"); continue; }
            var newHead = head + rt * (posX[i] - vx(head)) + up * (posY[i] - vy(head));

            if (!setHead(els[i], newHead))
            {
                failures.Add($"{els[i].Id}: TagHeadPosition would not accept the new point");
                continue;
            }
            done++;

            if (saved.Count > 0)
            {
                if (restoreEnds(els[i], saved)) endsRestored++;
                else failures.Add($"{els[i].Id}: moved, but its free leader end could NOT be put back — the arrow may no longer point at its element");
            }
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
sb.AppendLine($"MOVED: {done} of {willMove} tag(s).   Free leader ends put back: {endsRestored}");
if (failures.Count > 0)
{
    sb.AppendLine("PROBLEMS:");
    foreach (var f in failures.Take(20)) sb.AppendLine($"  {f}");
    if (failures.Count > 20) sb.AppendLine($"  ... and {failures.Count - 20} more");
}
sb.AppendLine("Elbows are NOT recomputed here — check any moved L-shaped tag, and see action-force-tag-leader-lshape.cs if a leader wants reshaping.");

return sb.ToString();
