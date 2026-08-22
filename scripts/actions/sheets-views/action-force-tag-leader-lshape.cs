// ============================================================
// FRAGMENT (action) — action-force-tag-leader-lshape.cs
// PURPOSE: Force every TAG in `elements` to draw its leader as an L-SHAPE (a bent leader with a
//          horizontal last leg into the tag) instead of a straight diagonal — the drafting standard on
//          most issued drawings. Works on ANY tag class, not just duct tags.
// ASSUMES: elements (List<Element>, tags of any kind) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// SOURCE: knowledge/live-model/tagging.md — the elbow-clearance and leader-side sections.
//
// ✱✱ TAG CLASSES DO NOT SHARE A BASE THAT EXPOSES THESE PROPERTIES. `IndependentTag`, `RoomTag`,
//    `SpaceTag`, `AreaTag` and the rest each declare their own `LeaderElbow`, `LeaderEnd`,
//    `TagHeadPosition` and `LeaderEndCondition` with **no common interface**. So a fragment that wants
//    to handle "tags" generally has to read and write them **by REFLECTION, by name** — casting to
//    `IndependentTag` silently skips every room and space tag in the set.
//
// ✱✱ THE SECOND FACT: setting `LeaderElbow` often fails until `LeaderEndCondition` is set to **`Free`**.
//    A tag whose leader end is Attached has no free elbow to move. The sequence that works is: try the
//    elbow; if it will not take, set `LeaderEndCondition = Free`, set the elbow, then RESTORE the
//    original condition. Skipping the restore leaves every tag detached from its element.
//
// GOTCHA: DRY RUN BY DEFAULT — this changes annotation geometry across a whole view.
// GOTCHA: a tag with NO LEADER has no elbow to bend. Those are counted separately, not failed — turn
//         the leader on first if they should have one.
// GOTCHA: the elbow is placed so the last leg runs HORIZONTAL into the tag head, and then nudged clear
//         of the tag's own TEXT. An elbow that lands inside the text is the exact defect
//         `tagging.md` records under "Elbow clearance must clear the tag's own text" — a small guard
//         stub is not enough, it has to clear the real text width.
// GOTCHA: `elbowClearanceMm` is a PAPER distance and every mm constant here depends on `view.Scale`.
//         Read the scale first — `tagging.md` has a whole section on this being the commonest mistake.
// NOTE: read back after writing. A pinned tag accepts the write and does not move.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's L-Shape Leader tool.
//   Compile-checked on 2020/2024/2027. Do ONE tag and look at it before the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                  // true = report only; false = actually bend the leaders
double elbowClearanceMm = 4.0;       // PAPER mm clear of the tag text — scaled by view.Scale below
int? targetViewIdInt = null;         // null = active view
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

// Every mm constant here is PAPER mm — model distance depends on the view scale.
int scale = 1;
try { scale = Math.Max(1, view.Scale); } catch { }
double clearFt = (elbowClearanceMm * scale) / MM;

// ---------- reflection helpers: tag classes share no base for these ----------
Func<Element, string, object> getProp = (el, name) =>
{
    try { var p = el.GetType().GetProperty(name); return p == null ? null : p.GetValue(el, null); }
    catch { return null; }
};
Func<Element, string, object, bool> setProp = (el, name, val) =>
{
    try
    {
        var p = el.GetType().GetProperty(name);
        if (p == null || !p.CanWrite) return false;
        p.SetValue(el, val, null);
        return true;
    }
    catch { return false; }
};
Func<Element, string, bool> canWrite = (el, name) =>
{
    try { var p = el.GetType().GetProperty(name); return p != null && p.CanWrite; } catch { return false; }
};

// ---- THE VERSION BRIDGE ----------------------------------------------------------------
// Revit 2023 REMOVED IndependentTag.LeaderElbow / .LeaderEnd / .TaggedLocalElementId in favour of a
// per-reference API: GetLeaderEnd(Reference), SetLeaderElbow(Reference, XYZ), GetTaggedReferences().
// PROVED by compiling a probe against the installed 2020 / 2024 / 2027 assemblies on 2026-08-22 —
// present on 2020, GONE on 2024 and 2027. Reflecting on the property NAME therefore finds nothing on
// 2023+ and every duct tag reports "no writable LeaderElbow" while the fragment compiles perfectly.
// RoomTag and SpaceTag were NOT changed and still carry the plain properties, so both routes are needed.
Func<Element, object> firstTaggedRef = el =>
{
    try
    {
        var m = el.GetType().GetMethod("GetTaggedReferences", Type.EmptyTypes);
        if (m == null) return null;
        var list = m.Invoke(el, null) as System.Collections.IEnumerable;
        if (list == null) return null;
        foreach (var r in list) return r;   // single-reference tag = the normal case
    }
    catch { }
    return null;
};
// LeaderEnd: property on 2020-2022, GetLeaderEnd(Reference) on 2023+
Func<Element, XYZ> leaderEndOf = el =>
{
    var viaProp = getProp(el, "LeaderEnd") as XYZ;
    if (viaProp != null) return viaProp;
    try
    {
        var r = firstTaggedRef(el);
        if (r == null) return null;
        var m = el.GetType().GetMethod("GetLeaderEnd", new[] { r.GetType() });
        return m == null ? null : m.Invoke(el, new[] { r }) as XYZ;
    }
    catch { return null; }
};
// LeaderElbow: property on 2020-2022, GetLeaderElbow/SetLeaderElbow(Reference, ...) on 2023+
Func<Element, XYZ> elbowOf = el =>
{
    var viaProp = getProp(el, "LeaderElbow") as XYZ;
    if (viaProp != null) return viaProp;
    try
    {
        var r = firstTaggedRef(el);
        if (r == null) return null;
        var m = el.GetType().GetMethod("GetLeaderElbow", new[] { r.GetType() });
        return m == null ? null : m.Invoke(el, new[] { r }) as XYZ;
    }
    catch { return null; }
};
Func<Element, XYZ, bool> setElbow = (el, v) =>
{
    if (setProp(el, "LeaderElbow", v)) return true;
    try
    {
        var r = firstTaggedRef(el);
        if (r == null) return false;
        var m = el.GetType().GetMethod("SetLeaderElbow", new[] { r.GetType(), typeof(XYZ) });
        if (m == null) return false;
        m.Invoke(el, new object[] { r, v });
        return true;
    }
    catch { return false; }
};
Func<Element, bool> canBendLeader = el =>
    canWrite(el, "LeaderElbow") || el.GetType().GetMethod("SetLeaderElbow") != null;

int bent = 0, alreadyBent = 0, noLeader = 0, noProperty = 0, pinnedSkipped = 0, failed = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Force Tag Leader L-Shape"))
{
    t.Start();
    try
    {
        foreach (var el in elements)
        {
            if (el == null || !el.IsValidObject) { noProperty++; continue; }
            if (el.Pinned) { pinnedSkipped++; continue; }

            // No common base — every one of these is looked up by name on the concrete type.
            if (!canBendLeader(el)) { noProperty++; continue; }

            var headObj = getProp(el, "TagHeadPosition") as XYZ;
            var endObj = leaderEndOf(el);
            if (headObj == null || endObj == null) { noLeader++; continue; }

            // An L-shape: come off the element, then run HORIZONTAL into the tag head.
            // Elbow shares the head's Y (the horizontal last leg) and sits back toward the element in X.
            double dirX = headObj.X >= endObj.X ? -1.0 : 1.0;
            var elbow = new XYZ(headObj.X + dirX * clearFt, headObj.Y, headObj.Z);

            var current = elbowOf(el);
            if (current != null && current.DistanceTo(elbow) < 1e-6) { alreadyBent++; continue; }

            if (dryRun)
            {
                bent++;
                if (rows.Count < 25) rows.Add(el.Id + " | " + (el.Category != null ? el.Category.Name : "?") +
                                              " | " + (current == null ? "straight" : "already bent"));
                continue;
            }

            bool ok = setElbow(el, elbow);

            if (!ok)
            {
                // The elbow will not take while the leader end is Attached — free it, set, restore.
                object originalCondition = getProp(el, "LeaderEndCondition");
                bool freed = false;
                try
                {
                    var p = el.GetType().GetProperty("LeaderEndCondition");
                    if (p != null && p.CanWrite && p.PropertyType.IsEnum)
                    {
                        var free = Enum.Parse(p.PropertyType, "Free");
                        p.SetValue(el, free, null);
                        freed = true;
                    }
                }
                catch { }

                if (freed)
                {
                    ok = setElbow(el, elbow);
                    // RESTORE — leaving every tag detached from its element would be far worse.
                    if (originalCondition != null) setProp(el, "LeaderEndCondition", originalCondition);
                }
            }

            if (!ok) { failed++; continue; }

            // A pinned tag accepts the write without moving — read it back.
            var after = elbowOf(el);
            if (after == null || after.DistanceTo(elbow) > 1e-6) { failed++; continue; }

            bent++;
            if (rows.Count < 25)
                rows.Add(el.Id + " | " + (el.Category != null ? el.Category.Name : "?") + " | bent");
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing changed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing changed. " : "") +
              $"L-shape leaders in '{view.Name}' (scale 1:{scale}, {elbowClearanceMm} mm paper = " +
              $"{Math.Round(clearFt * MM)} mm model): {(dryRun ? "would bend" : "bent")} {bent}" +
              $" | already bent: {alreadyBent}" +
              (noLeader > 0 ? $" | no leader: {noLeader}" : "") +
              (noProperty > 0 ? $" | no writable LeaderElbow: {noProperty}" : "") +
              (pinnedSkipped > 0 ? $" | pinned: {pinnedSkipped}" : "") +
              (failed > 0 ? $" | FAILED: {failed}" : ""));

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Tag Id | Category | State");
    sb.AppendLine("--- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
    if (bent > rows.Count) sb.AppendLine("... and " + (bent - rows.Count) + " more");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
