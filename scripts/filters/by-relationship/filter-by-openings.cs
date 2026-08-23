// ============================================================
// FRAGMENT (filter) — filter-by-openings.cs
// PURPOSE: Every OPENING cut through the building — shafts, floor openings, wall openings, roof
//          openings — as an actionable `elements` set. The MEP question behind it: "where are the
//          penetrations", "is there a shaft here for this riser", "show me every hole in this slab".
// PRODUCES: elements (List<Element>), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ THE REASON A SINGLE QUERY MISSES MOST OF THEM: openings are not one category and not one class.
//    They are spread across FOUR BuiltInCategories —
//        OST_ShaftOpening       a shaft cutting several levels
//        OST_FloorOpening       a hole in a floor or slab
//        OST_SWallRectOpening   a rectangular hole in a wall
//        OST_RoofOpening        a hole in a roof
//    — and separately there is an `Opening` CLASS, which also picks up the openings Revit creates
//    implicitly for doors and windows. Query one category and the report looks clean while the shafts
//    are still there. This collects all four categories AND the class, then de-duplicates by Id.
//
// ✱✱ AND THE ONES THAT ARE NOT "OPENINGS" AT ALL. A hole cut by a FAMILY (a cast-in sleeve, a duct
//    penetration component, a generic-model void) is NOT in any of those categories — it is just a
//    family instance that happens to cut its host. `includeCuttingFamilies` finds those too, by asking
//    Revit which elements are cut by an instance (InstanceVoidCutUtils / the host's own cut list). If
//    you are auditing penetrations for coordination, you want this ON; if you are only after Revit's
//    own Opening elements, leave it off.
//
// GOTCHA: an opening's Name is usually the family/system name, not a location. The report gives the
//         HOST it cuts and the LEVEL, because that is what makes it findable on a drawing.
// GOTCHA: openings in a LINKED model are not returned — links are a separate document. Use
//         filter-by-linked-model-elements.cs for those.
// RELATED: recipes/create-mep-openings.cs makes them; place-sleeves-at-wall-penetrations.cs puts
//          sleeves in; this one FINDS what already exists, which nothing else here did.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Every call in it is proven elsewhere in
//   this library. Run it once and compare the count against what you can see in a 3D view.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool includeShafts = true;
bool includeFloorOpenings = true;
bool includeWallOpenings = true;
bool includeRoofOpenings = true;
bool includeCuttingFamilies = false;  // families that cut their host (sleeves, void components)
int onlyOnLevelIdInt = 0;             // 0 = every level
double minSizeMm = 0;                 // ignore anything whose largest plan dimension is under this
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();
var seen = new HashSet<ElementId>();

const double MM = 304.8;

var cats = new List<BuiltInCategory>();
if (includeShafts) cats.Add(BuiltInCategory.OST_ShaftOpening);
if (includeFloorOpenings) cats.Add(BuiltInCategory.OST_FloorOpening);
if (includeWallOpenings) cats.Add(BuiltInCategory.OST_SWallRectOpening);
if (includeRoofOpenings) cats.Add(BuiltInCategory.OST_RoofOpening);

var byCategory = new List<Element>();
if (cats.Count > 0)
{
    var mcf = new ElementMulticategoryFilter(cats);
    byCategory = new FilteredElementCollector(Document)
        .WherePasses(mcf).WhereElementIsNotElementType().ToElements().ToList();
}

// The Opening CLASS as well — it is not the same set as the four categories above.
var byClass = new FilteredElementCollector(Document)
    .OfClass(typeof(Opening)).WhereElementIsNotElementType().ToElements().ToList();

var candidates = new List<Element>();
foreach (var e in byCategory.Concat(byClass))
    if (seen.Add(e.Id)) candidates.Add(e);

// Families that cut their host — not an "Opening" to Revit, but a hole to everyone else.
int cuttingFamilies = 0;
if (includeCuttingFamilies)
{
    foreach (var fi in new FilteredElementCollector(Document)
                 .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
    {
        bool cuts = false;
        try { cuts = InstanceVoidCutUtils.CanBeCutWithVoid(fi) && InstanceVoidCutUtils.GetElementsBeingCut(fi).Count > 0; }
        catch { }
        if (cuts && seen.Add(fi.Id)) { candidates.Add(fi); cuttingFamilies++; }
    }
}

Func<Element, string> hostOf = (e) =>
{
    try
    {
        var op = e as Opening;
        if (op != null && op.Host != null) return $"{op.Host.Category?.Name} {op.Host.Id}";
        var fi = e as FamilyInstance;
        if (fi != null && fi.Host != null) return $"{fi.Host.Category?.Name} {fi.Host.Id}";
        if (fi != null)
        {
            var cut = InstanceVoidCutUtils.GetElementsBeingCut(fi);
            if (cut.Count > 0) { var h = Document.GetElement(cut.First()); return $"cuts {h?.Category?.Name} {h?.Id}"; }
        }
    }
    catch { }
    return "(no host)";
};

Func<Element, string> levelOf = (e) =>
{
    try
    {
        var lid = e.LevelId;
        if (lid != null && lid != ElementId.InvalidElementId)
            return Document.GetElement(lid)?.Name ?? "(level?)";
    }
    catch { }
    return "(no level)";
};

int filteredSmall = 0, filteredLevel = 0;

sb.AppendLine($"{candidates.Count} opening(s) found before filtering"
            + (includeCuttingFamilies ? $" (including {cuttingFamilies} cutting famil{(cuttingFamilies == 1 ? "y" : "ies")})" : ""));
sb.AppendLine();
sb.AppendLine($"{"Id",-10}{"Kind",-22}{"Size (mm)",-20}{"Level",-16}Host");

foreach (var e in candidates.OrderBy(x => x.Category?.Name).ThenBy(x => x.Id.ToString()))
{
    if (onlyOnLevelIdInt != 0)
    {
        var lid = ElementId.InvalidElementId;
        try { lid = e.LevelId; } catch { }
        if (lid == null || lid != new ElementId(onlyOnLevelIdInt)) { filteredLevel++; continue; }
    }

    string size = "(no bbox)";
    double biggest = double.MaxValue;
    var bb = e.get_BoundingBox(null);
    if (bb != null)
    {
        double dx = (bb.Max.X - bb.Min.X) * MM, dy = (bb.Max.Y - bb.Min.Y) * MM, dz = (bb.Max.Z - bb.Min.Z) * MM;
        biggest = Math.Max(dx, dy);
        size = $"{dx:F0} x {dy:F0} x {dz:F0}";
    }

    if (minSizeMm > 0 && biggest < minSizeMm) { filteredSmall++; continue; }

    elements.Add(e);
    sb.AppendLine($"{e.Id.ToString(),-10}{(e.Category?.Name ?? e.GetType().Name),-22}{size,-20}{levelOf(e),-16}{hostOf(e)}");
}

sb.AppendLine();
sb.AppendLine($"{elements.Count} opening(s) returned.");
if (filteredLevel > 0) sb.AppendLine($"  {filteredLevel} skipped — not on the requested level.");
if (filteredSmall > 0) sb.AppendLine($"  {filteredSmall} skipped — smaller than {minSizeMm} mm.");
if (elements.Count == 0)
    sb.AppendLine("Nothing found. If you can SEE holes in the model, they are probably cut by families — "
                + "set includeCuttingFamilies = true, or they are in a linked model (filter-by-linked-model-elements.cs).");
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
