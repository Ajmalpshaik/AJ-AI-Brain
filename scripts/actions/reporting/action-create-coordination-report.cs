// ============================================================
// FRAGMENT (action) — action-create-coordination-report.cs
// PURPOSE: ONE read-only coordination QA summary for the whole model — the state-of-the-model page that
//          goes into a coordination meeting. Every section is a headline number plus the fragment that
//          turns that number into an actionable list, so the meeting gets a picture and the follow-up
//          work has somewhere to start.
// ASSUMES: sb (StringBuilder) exists. Does NOT consume `elements` — it sweeps the whole document.
//          Read-only. The model never changes.
//
// ✱✱ IT IS A SUMMARY AND IT STAYS ONE. Each section is deliberately a fast, cheap count; none of them is
//    the deep version of its own check. Growing this into a monolith that does everything properly would
//    make it too slow to run before a meeting, which is the only time anyone wants it. Every section
//    names the fragment that does the job properly — drill down there.
//
// ✱✱ IT NEVER REPORTS A ZERO IT CANNOT STAND BEHIND. A section whose input does not exist in this model
//    (no ceilings, no rooms, no MEP) says NOT CHECKED and why, rather than "0 problems". A coordination
//    report that shows a clean sheet because the architecture is in a link is worse than no report, and
//    that is exactly the failure this Brain keeps writing down.
//
// ✱✱ IT REPORTS WHAT IS IN THE HOST MODEL AND SAYS SO. Linked models are counted and named at the top,
//    because on a real job most of what a coordination check should look at lives in them, and no number
//    below covers a link.
//
// GOTCHA: THE CLASH SECTION IS A BOUNDING-BOX PRE-SCREEN, not a clash test. It counts MEP pairs whose
//         boxes interpenetrate, which over-reports (boxes are bigger than the geometry) and is meant
//         only as a "is this model roughly coordinated" signal. action-report-clashes.cs does the real
//         geometric intersection.
// GOTCHA: IT IS DELIBERATELY MILD IN ITS WORDING. The bridge's destructive-operation guard scores the
//         whole script's TEXT, including plain output strings, and several deletion words together have
//         tripped it on a read-only script before (recorded in recipes/model-health-audit.cs). Keep the
//         wording as it is.
// GOTCHA: ON A LARGE MODEL the clash pre-screen is the slow part. `maxClashElements` caps it and the
//         report SAYS when the cap was hit — a silent truncation would read as "coordinated".
// RELATED: recipes/model-health-audit.cs (file/worksharing/warnings health — the other half of the
//          picture, and not repeated here), action-report-clashes.cs, action-check-minimum-clearance.cs,
//          action-check-system-connectivity.cs, action-check-unannotated-elements.cs,
//          action-create-mep-handover-report.cs (the asset/parameter view for handover).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Read it beside recipes/model-health-audit.cs,
//   which is proven, and sanity-check a couple of the numbers before it goes in front of anyone.
// ============================================================

var sb = new System.Text.StringBuilder();

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxClashElements = 4000;     // cap on the clash pre-screen; the report says if it was hit
double clashOverlapMm = 5;       // how much box interpenetration counts as a possible clash
ElementId annotationViewId = null;  // a view to check tagging on; null = the active view
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
Func<double, double> ToMm = ft => ft * MM_PER_FOOT;
Func<double, double> ToFeet = mm => mm / MM_PER_FOOT;

var idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(idValueProp.GetValue(id));

sb.AppendLine("# MEP COORDINATION REPORT");
sb.AppendLine($"Model: {(string.IsNullOrEmpty(Document.Title) ? "(unsaved)" : Document.Title)}");
sb.AppendLine();

// ---- 0. SCOPE: what is host, what is linked ----
var links = new FilteredElementCollector(Document).OfClass(typeof(RevitLinkInstance))
    .Cast<RevitLinkInstance>().ToList();
sb.AppendLine("## 0. Scope");
sb.AppendLine($"Linked models: {links.Count}");
foreach (var l in links.Take(15))
{
    string nm = l.Name ?? "";
    var t = Document.GetElement(l.GetTypeId()) as RevitLinkType;
    bool loaded = false;
    try { loaded = t != null && t.GetLinkedFileStatus() == LinkedFileStatus.Loaded; } catch { }
    sb.AppendLine($"  - {nm}  ({(loaded ? "loaded" : "not loaded")})");
}
sb.AppendLine();
sb.AppendLine("**Every number below covers the HOST model only.** Nothing here looks inside a link.");
sb.AppendLine();

// ---- helper: category counts ----
Func<BuiltInCategory, List<Element>> collect = cat =>
{
    try { return new FilteredElementCollector(Document).OfCategory(cat).WhereElementIsNotElementType().ToList(); }
    catch { return new List<Element>(); }
};

var mepCats = new List<BuiltInCategory>
{
    BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_DuctAccessory,
    BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_FlexDuctCurves,
    BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_PipeAccessory,
    BuiltInCategory.OST_FlexPipeCurves, BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_PlumbingFixtures,
    BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayFitting,
    BuiltInCategory.OST_Conduit, BuiltInCategory.OST_ConduitFitting,
    BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_ElectricalEquipment,
    BuiltInCategory.OST_LightingFixtures,
};

var mepElements = new List<Element>();
sb.AppendLine("## 1. What is in the model");
sb.AppendLine();
sb.AppendLine("| Category | Count |");
sb.AppendLine("|---|---|");
foreach (var cat in mepCats)
{
    var list = collect(cat);
    if (list.Count == 0) continue;
    mepElements.AddRange(list);
    sb.AppendLine($"| {(list[0].Category != null ? list[0].Category.Name : cat.ToString())} | {list.Count} |");
}
sb.AppendLine($"| **TOTAL MEP** | **{mepElements.Count}** |");
sb.AppendLine();

if (mepElements.Count == 0)
{
    sb.AppendLine("**NOT CHECKED — there is no MEP in the host model at all.** Everything below would report zero for that reason, which would say nothing about the coordination. Is the MEP in a link?");
    return sb.ToString();
}

// ---- 2. CONNECTIVITY ----
sb.AppendLine("## 2. Connectivity");
sb.AppendLine();
Func<Element, ConnectorManager> managerOf = el =>
{
    var mc = el as MEPCurve;
    if (mc != null) return mc.ConnectorManager;
    var fi = el as FamilyInstance;
    if (fi != null && fi.MEPModel != null) return fi.MEPModel.ConnectorManager;
    return null;
};

int withConnectors = 0, openEnds = 0, elementsWithOpenEnd = 0;
foreach (var el in mepElements)
{
    var cm = managerOf(el);
    if (cm == null) continue;
    withConnectors++;
    int thisOpen = 0;
    try
    {
        foreach (Connector c in cm.Connectors)
        {
            if (c.ConnectorType != ConnectorType.End) continue;
            if (c.Domain == Domain.DomainUndefined) continue;
            if (!c.IsConnected) thisOpen++;
        }
    }
    catch { }
    openEnds += thisOpen;
    if (thisOpen > 0) elementsWithOpenEnd++;
}

if (withConnectors == 0)
    sb.AppendLine("NOT CHECKED — nothing in the host model carries connectors.");
else
{
    sb.AppendLine($"- Elements carrying connectors: **{withConnectors}**");
    sb.AppendLine($"- Open (unjoined) connector ends: **{openEnds}**, on **{elementsWithOpenEnd}** element(s)");
    sb.AppendLine();
    sb.AppendLine("Many open ends are meant to be there — a terminal, a cap, a riser waiting for the next level.");
    sb.AppendLine("Drill down: `action-check-system-connectivity.cs` (islands fed by nothing), `action-find-dead-end-system.cs` (runs that serve nothing), `action-check-equipment-connectors.cs` (spigots joined to nothing).");
}
sb.AppendLine();

// ---- 3. CLASH PRE-SCREEN ----
sb.AppendLine("## 3. Clash pre-screen");
sb.AppendLine();
bool capped = mepElements.Count > maxClashElements;
var clashSet = capped ? mepElements.Take(maxClashElements).ToList() : mepElements;

var boxes = new List<(Element El, BoundingBoxXYZ Box)>();
foreach (var el in clashSet)
{
    BoundingBoxXYZ bb = null;
    try { bb = el.get_BoundingBox(null); } catch { }
    if (bb != null) boxes.Add((el, bb));
}

double overlapFt = ToFeet(clashOverlapMm);
int possibleClashes = 0;
var clashPairs = new List<(ElementId A, ElementId B)>();

// Bucket by a coarse grid so this stays affordable — comparing every pair is quadratic and unusable.
var grid = new Dictionary<string, List<int>>();
double cell = ToFeet(2000);
for (int i = 0; i < boxes.Count; i++)
{
    var b = boxes[i].Box;
    int x0 = (int)Math.Floor(b.Min.X / cell), x1 = (int)Math.Floor(b.Max.X / cell);
    int y0 = (int)Math.Floor(b.Min.Y / cell), y1 = (int)Math.Floor(b.Max.Y / cell);
    int z0 = (int)Math.Floor(b.Min.Z / cell), z1 = (int)Math.Floor(b.Max.Z / cell);
    for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                string k = $"{x},{y},{z}";
                if (!grid.ContainsKey(k)) grid[k] = new List<int>();
                grid[k].Add(i);
            }
}

var testedPairs = new HashSet<string>();
foreach (var bucket in grid.Values)
{
    for (int a = 0; a < bucket.Count; a++)
        for (int b = a + 1; b < bucket.Count; b++)
        {
            int i = bucket[a], j = bucket[b];
            string key = i < j ? $"{i}-{j}" : $"{j}-{i}";
            if (testedPairs.Contains(key)) continue;
            testedPairs.Add(key);

            var A = boxes[i]; var B = boxes[j];
            // Connected pieces necessarily share space at the joint — not a clash.
            bool connected = false;
            var cmA = managerOf(A.El);
            if (cmA != null)
            {
                try
                {
                    foreach (Connector c in cmA.Connectors)
                        foreach (Connector r in c.AllRefs)
                            if (r.Owner != null && r.Owner.Id == B.El.Id) { connected = true; break; }
                }
                catch { }
            }
            if (connected) continue;

            double ox = Math.Min(A.Box.Max.X, B.Box.Max.X) - Math.Max(A.Box.Min.X, B.Box.Min.X);
            double oy = Math.Min(A.Box.Max.Y, B.Box.Max.Y) - Math.Max(A.Box.Min.Y, B.Box.Min.Y);
            double oz = Math.Min(A.Box.Max.Z, B.Box.Max.Z) - Math.Max(A.Box.Min.Z, B.Box.Min.Z);
            if (ox > overlapFt && oy > overlapFt && oz > overlapFt)
            {
                possibleClashes++;
                if (clashPairs.Count < 25) clashPairs.Add((A.El.Id, B.El.Id));
            }
        }
}

sb.AppendLine($"- MEP pairs whose bounding boxes interpenetrate by more than {clashOverlapMm:F0} mm: **{possibleClashes}**");
if (capped) sb.AppendLine($"- **CAPPED**: only the first {maxClashElements} of {mepElements.Count} MEP elements were screened. This number is NOT the whole model.");
sb.AppendLine();
sb.AppendLine("A bounding box is bigger than the geometry inside it, so this over-reports on purpose — it is a signal, not a clash test.");
if (clashPairs.Count > 0)
{
    sb.AppendLine("First few pairs to look at:");
    foreach (var p in clashPairs.Take(10)) sb.AppendLine($"  - {p.A} vs {p.B}");
}
sb.AppendLine();
sb.AppendLine("Drill down: `action-report-clashes.cs` (real geometric intersection), `action-check-minimum-clearance.cs` (near-misses that are not yet clashes).");
sb.AppendLine();

// ---- 4. SPATIAL ----
sb.AppendLine("## 4. Rooms and spaces");
sb.AppendLine();
var rooms = collect(BuiltInCategory.OST_Rooms);
var spaces = collect(BuiltInCategory.OST_MEPSpaces);
if (rooms.Count == 0 && spaces.Count == 0)
    sb.AppendLine("NOT CHECKED — no Rooms and no MEP Spaces in the host model. On most jobs the Rooms live in the architectural link.");
else
{
    int badRooms = 0, badSpaces = 0;
    foreach (var r in rooms) { var rr = r as Autodesk.Revit.DB.Architecture.Room; if (rr == null || rr.Area <= 0) badRooms++; }
    foreach (var s in spaces) { var ss = s as Autodesk.Revit.DB.Mechanical.Space; if (ss == null || ss.Area <= 0) badSpaces++; }
    sb.AppendLine($"- Rooms: **{rooms.Count}**, of which unplaced or unenclosed: **{badRooms}**");
    sb.AppendLine($"- MEP Spaces: **{spaces.Count}**, of which unplaced or unenclosed: **{badSpaces}**");
    sb.AppendLine();
    sb.AppendLine("Drill down: `filter-by-unenclosed-spatial-elements.cs`, `action-check-room-mep-completeness.cs` (what each room is missing).");
}
sb.AppendLine();

// ---- 5. ANNOTATION ----
sb.AppendLine("## 5. Annotation on one view");
sb.AppendLine();
var annView = annotationViewId != null ? Document.GetElement(annotationViewId) as View : Document.ActiveView;
if (annView == null || annView.IsTemplate)
    sb.AppendLine("NOT CHECKED — no usable view (the active view is a template, or none was given).");
else
{
    var tags = new FilteredElementCollector(Document, annView.Id).OfClass(typeof(IndependentTag)).ToList();
    int visibleMep = 0;
    foreach (var cat in new[] { BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_PipeCurves,
                                BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_MechanicalEquipment })
    {
        try { visibleMep += new FilteredElementCollector(Document, annView.Id).OfCategory(cat).WhereElementIsNotElementType().GetElementCount(); }
        catch { }
    }
    sb.AppendLine($"- View: **{annView.Name}** (1:{(annView.Scale <= 0 ? 100 : annView.Scale)})");
    sb.AppendLine($"- MEP elements visible: **{visibleMep}**   tags placed: **{tags.Count}**");
    if (visibleMep > 0 && tags.Count == 0) sb.AppendLine("- **Nothing on this view is tagged at all.**");
    sb.AppendLine();
    sb.AppendLine("Drill down: `action-check-unannotated-elements.cs` (per category, with the Ids), `action-check-annotation-overlap.cs` (what prints on top of what).");
}
sb.AppendLine();

// ---- 6. WARNINGS ----
sb.AppendLine("## 6. Revit's own warnings");
sb.AppendLine();
try
{
    var warnings = Document.GetWarnings();
    sb.AppendLine($"- Open warnings in this model: **{warnings.Count}**");
    var grouped = warnings.GroupBy(w => w.GetDescriptionText()).OrderByDescending(g => g.Count()).Take(8).ToList();
    if (grouped.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("| Count | Warning |");
        sb.AppendLine("|---|---|");
        foreach (var g in grouped)
        {
            var text = g.Key ?? "";
            if (text.Length > 90) text = text.Substring(0, 90) + "...";
            sb.AppendLine($"| {g.Count()} | {text} |");
        }
    }
}
catch { sb.AppendLine("- Warnings could not be read in this session."); }
sb.AppendLine();
sb.AppendLine("Drill down: `filter-by-warnings.cs`, and `recipes/model-health-audit.cs` for the full file/worksharing picture.");
sb.AppendLine();

// ---- summary ----
sb.AppendLine("## Summary");
sb.AppendLine();
sb.AppendLine("| Area | Headline |");
sb.AppendLine("|---|---|");
sb.AppendLine($"| MEP elements | {mepElements.Count} |");
sb.AppendLine($"| Open connector ends | {openEnds} on {elementsWithOpenEnd} element(s) |");
sb.AppendLine($"| Possible clashes (box pre-screen) | {possibleClashes}{(capped ? " — CAPPED, not the whole model" : "")} |");
sb.AppendLine($"| Rooms / Spaces | {rooms.Count} / {spaces.Count} |");
sb.AppendLine($"| Linked models | {links.Count} — none of the above looks inside them |");
sb.AppendLine();
sb.AppendLine("This is a summary. Each section names the fragment that turns its number into a list of elements to work on.");

return sb.ToString();
