// ============================================================
// FRAGMENT (action) — action-stack-tags.cs
// PURPOSE: Arrange the TAGS in `elements` into a neat VERTICAL STACK at one point — the tidy column of
//          tags at the side of a busy area, each still leadered back to its own element. The scripted
//          form of "stack these tags here".
// ASSUMES: elements (List<Element>, tags) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/tagging.md
//
// GOTCHA: STACK ORDER IS NEAREST-ELEMENT-FIRST, not the order the filter happened to return. Tags are
//         sorted by their tagged element's distance along the stack axis, so the leaders fan out
//         without crossing each other. Sorting by tag id instead produces a plate of spaghetti that
//         looks like the tool is broken.
// GOTCHA: `gapMm` IS A PAPER DISTANCE and must be multiplied by `view.Scale` to become a model
//         distance. Every mm clearance constant in tag work depends on the view scale — see the
//         dedicated section in tagging.md; it is the commonest mistake in this whole subject.
// GOTCHA: the gap must clear the TAG'S OWN HEIGHT or the stack overlaps itself. If `gapMm` is left at 0
//         this measures the tallest tag in the set and uses that plus a margin, and says which it used.
//
// ✱✱ FIXED 2026-08-23 — THE AUTO GAP WAS MEASURING THE LEADER, NOT THE TAG.
//    `tag.get_BoundingBox(view)` on a tag WITH A LEADER returns the box around the head AND the leader
//    line, so on a busy plan the "tallest tag" came back as however far the leader stretched — metres,
//    not millimetres — and the automatic gap blew the stack apart. The head is only the text box when
//    the tag has no leader.
//    **So the measurement now uses LEADERLESS TAGS ONLY**, which are the only ones whose box is honest.
//    If every tag in the set has a leader there is nothing safe to measure, and rather than quietly
//    using a wrong number the fragment says so and falls back to a stated paper default — or, if
//    `measureByTemporaryLeaderRemoval` is turned on, takes the measurement properly:
//        open a transaction group -> set HasLeader = false on every tag -> COMMIT (the geometry only
//        regenerates on commit) -> measure the now-honest boxes -> ROLL THE GROUP BACK so the leaders
//        are exactly as they were -> then do the real work.
//    That temporary-change-then-rollback is a general measuring technique worth knowing: it is the only
//    way to measure something whose geometry depends on state you do not want to keep. It is OFF by
//    default because it means a dry run touches the model (and rolls back), which this fragment
//    otherwise never does.
// GOTCHA: tag classes DO NOT share a base exposing `TagHeadPosition` — `IndependentTag`, `RoomTag`,
//         `SpaceTag` and the rest each declare their own. Reflection by name is used so a mixed
//         selection works; casting to `IndependentTag` would silently skip every room tag.
// GOTCHA: DRY RUN BY DEFAULT. A stack is easy to place in the wrong spot and tedious to undo by hand.
// GOTCHA: this MOVES TAG HEADS ONLY. Revit redraws each leader automatically. It does not set elbows —
//         run `action-force-tag-leader-lshape.cs` afterwards if the drawing standard wants L-shapes.
// NOTE: the stack point is given in mm in the VIEW'S own plane, so the same numbers work in a plan, a
//       section or on a sheet.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Stack Tags, whose own
//   version asks the user to click the base point. Compile-checked on 2020/2024/2027.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double stackXMm = 0;                 // where the stack starts, in the VIEW's plane, mm
double stackYMm = 0;
double gapMm = 0;                    // PAPER mm between tags; 0 = measure the tallest tag and use that
string direction = "down";           // "down" | "up" — which way the stack grows from the start point
bool dryRun = true;                  // true = report the moves only; false = actually move
int? targetViewIdInt = null;         // null = active view
bool measureByTemporaryLeaderRemoval = false; // only when gapMm = 0 AND every tag has a leader:
                                     // strip leaders, commit, measure, roll back. Touches the model
                                     // (and undoes it) even on a dry run — read the header first.
// ---- END INPUTS ----

const double MM = 304.8;

View view = targetViewIdInt.HasValue
    ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View
    : Document.ActiveView;

if (view == null || view.IsTemplate)
{
    sb.AppendLine("Target view not found or is a template.");
    return sb.ToString();
}

int scale = 1;
try { scale = Math.Max(1, view.Scale); } catch { }

// reflection — tag classes share no base exposing TagHeadPosition
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

// HasLeader is declared separately on IndependentTag, SpatialElementTag and friends — no shared base,
// same reason TagHeadPosition needs reflection. A tag class with no such property is treated as
// leaderless, which is the safe assumption for measuring.
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

// the tags we can actually move, and where each one's ELEMENT sits (for the ordering)
XYZ rt = view.RightDirection, up = view.UpDirection, o = view.Origin;
var items = new List<Tuple<Element, XYZ, double>>();   // tag, current head, sort key
int noHead = 0, pinnedSkipped = 0;
double tallest = 0;
int measuredFrom = 0, leaderedSkippedFromMeasure = 0;

foreach (var el in elements)
{
    if (el == null || !el.IsValidObject) { noHead++; continue; }
    if (el.Pinned) { pinnedSkipped++; continue; }

    var head = headOf(el);
    if (head == null) { noHead++; continue; }

    // Measure this tag's height for the default gap — but ONLY if it has no leader. A leadered tag's
    // bounding box encloses the leader line too, so its "height" is the leader's reach, not the head's.
    if (!hasLeader(el))
    {
        try
        {
            var bb = el.get_BoundingBox(view);
            if (bb != null)
            {
                double h = Math.Abs((bb.Max - bb.Min).DotProduct(up));
                if (h > tallest) tallest = h;
                measuredFrom++;
            }
        }
        catch { }
    }
    else leaderedSkippedFromMeasure++;

    // sort by the TAGGED ELEMENT's position, so leaders fan out instead of crossing
    // Revit 2023 REMOVED IndependentTag.LeaderEnd for GetLeaderEnd(Reference) — proved by compiling a
    // probe against the installed 2020/2024/2027 assemblies, 2026-08-22. Reading the property NAME alone
    // finds nothing on 2023+, so every duct tag would fall back to its HEAD position and the stack would
    // be ordered by where the tags happen to sit rather than by their elements — crossed leaders, which
    // is the one thing this fragment exists to avoid. RoomTag/SpaceTag still have the property.
    double key;
    try
    {
        XYZ pt = null;
        var prop = el.GetType().GetProperty("LeaderEnd");
        if (prop != null) pt = prop.GetValue(el, null) as XYZ;
        if (pt == null)
        {
            var refs = el.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
            if (refs != null)
            {
                var list = refs.Invoke(el, null) as System.Collections.IEnumerable;
                if (list != null)
                    foreach (var r in list)
                    {
                        var gm = el.GetType().GetMethod("GetLeaderEnd", new[] { r.GetType() });
                        if (gm != null) pt = gm.Invoke(el, new[] { r }) as XYZ;
                        break;
                    }
            }
        }
        key = ((pt ?? head) - o).DotProduct(up);
    }
    catch { key = (head - o).DotProduct(up); }

    items.Add(Tuple.Create(el, head, key));
}

if (items.Count == 0)
{
    sb.AppendLine($"No movable tags in the set ({noHead} had no TagHeadPosition, {pinnedSkipped} pinned).");
    return sb.ToString();
}

// If nothing could be measured honestly (every tag is leadered) and we are allowed to, take the
// measurement the only correct way: strip the leaders, COMMIT so the geometry regenerates, measure,
// then roll the whole group back so the leaders are exactly as they were.
if (gapMm <= 0 && tallest <= 0 && leaderedSkippedFromMeasure > 0 && measureByTemporaryLeaderRemoval)
{
    using (var tg = new TransactionGroup(Document, "AJ Tools - Measure tags (temporary)"))
    {
        tg.Start();
        try
        {
            using (var tm = new Transaction(Document, "AJ Tools - Strip leaders to measure"))
            {
                tm.Start();
                var fo = tm.GetFailureHandlingOptions();
                fo.SetForcedModalHandling(false);
                fo.SetClearAfterRollback(true);
                tm.SetFailureHandlingOptions(fo);

                foreach (var it in items)
                {
                    try
                    {
                        var p = it.Item1.GetType().GetProperty("HasLeader");
                        if (p != null && p.CanWrite) p.SetValue(it.Item1, false, null);
                    }
                    catch { }
                }
                tm.Commit();   // the box is only honest AFTER the commit regenerates the tag
            }

            foreach (var it in items)
            {
                try
                {
                    var bb = it.Item1.get_BoundingBox(view);
                    if (bb != null)
                    {
                        double h = Math.Abs((bb.Max - bb.Min).DotProduct(up));
                        if (h > tallest) tallest = h;
                        measuredFrom++;
                    }
                }
                catch { }
            }
        }
        catch (Exception exM) { sb.AppendLine("(temporary measurement failed: " + exM.Message + ")"); }
        finally { if (tg.HasStarted()) tg.RollBack(); }   // ALWAYS put the leaders back
    }
}

// gap: PAPER mm -> model, or the tallest tag plus a margin
double gapFt;
string gapSource;
if (gapMm > 0) { gapFt = (gapMm * scale) / MM; gapSource = gapMm + " mm paper at 1:" + scale; }
else if (tallest > 0)
{
    gapFt = tallest * 1.25;
    gapSource = "measured tallest of " + measuredFrom + " leaderless tag(s), " + Math.Round(tallest * MM) + " mm model + 25%";
}
else
{
    gapFt = (4.0 * scale) / MM;
    gapSource = leaderedSkippedFromMeasure > 0
        ? "fallback 4 mm paper at 1:" + scale + " — ALL " + leaderedSkippedFromMeasure + " tag(s) have leaders, so none could be measured honestly (a leadered tag's bounding box includes its leader). Set gapMm yourself, or turn on measureByTemporaryLeaderRemoval"
        : "fallback 4 mm paper at 1:" + scale;
}

double sign = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? 1.0 : -1.0;

// nearest-element-first down the stack
var ordered = sign < 0 ? items.OrderByDescending(i => i.Item3).ToList() : items.OrderBy(i => i.Item3).ToList();

XYZ start = o + rt.Multiply(stackXMm / MM) + up.Multiply(stackYMm / MM);

int moved = 0, already = 0, failed = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Stack Tags"))
{
    t.Start();
    try
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            var el = ordered[i].Item1;
            var head = ordered[i].Item2;

            XYZ target = start + up.Multiply(sign * gapFt * i);
            target = new XYZ(target.X, target.Y, head.Z);   // keep the tag's own plane

            if (head.DistanceTo(target) < 1e-6) { already++; continue; }

            if (!dryRun)
            {
                if (!setHead(el, target)) { failed++; continue; }
                // a pinned tag accepts the write silently — read back
                var after = headOf(el);
                if (after == null || after.DistanceTo(target) > 1e-6) { failed++; continue; }
            }

            moved++;
            if (rows.Count < 25)
                rows.Add(el.Id + " | " + (el.Category != null ? el.Category.Name : "?") + " | " +
                         (i + 1) + " | " + Math.Round(head.DistanceTo(target) * MM));
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
              $"Stacked {(dryRun ? "would move " : "")}{moved} tag(s) {direction} from ({stackXMm}, {stackYMm}) mm " +
              $"in '{view.Name}', gap {Math.Round(gapFt * MM)} mm model — {gapSource}.");
sb.AppendLine("Order is NEAREST-ELEMENT-FIRST so the leaders fan out instead of crossing." +
              (already > 0 ? $"  Already in place: {already}." : "") +
              (noHead > 0 ? $"  No TagHeadPosition: {noHead}." : "") +
              (pinnedSkipped > 0 ? $"  Pinned, skipped: {pinnedSkipped}." : "") +
              (failed > 0 ? $"  FAILED: {failed}." : ""));

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Tag Id | Category | Slot | Moved (mm)");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (moved > rows.Count) sb.AppendLine("... and " + (moved - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
