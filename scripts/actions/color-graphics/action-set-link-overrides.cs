// ============================================================
// FRAGMENT (action) — action-set-link-overrides.cs
// PURPOSE: Grey / halftone / fade a whole LINKED MODEL in one view — what the "Revit Links" tab of
//          Visibility-Graphics does, reached from code. This is the piece the grayout was missing: on a
//          real coordination job the architecture and structure arrive as LINKS, and a category
//          override in the host view does not reach inside a link at all.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
//
// WHY THIS EXISTS — the thing that quietly does nothing:
//   recipes/mep-grayout.cs sets CATEGORY overrides on the host document. If the walls and floors you
//   are trying to push into the background live in a link, that recipe greys nothing, reports success,
//   and the view looks untouched. Nothing errors, because nothing was wrong — the host simply has no
//   walls to grey. Run this alongside it on any linked job.
//
// TWO WAYS IN, AND THEY ARE NOT THE SAME:
//   * "whole-link"  — one override on the link INSTANCE, which is a single element in the host.
//                     Halftone, transparency, line colour and weight all work. Cheapest by far: one
//                     API call per link however many thousand elements are inside it. Works on every
//                     Revit here.
//   * "by-category" — reaches the elements INSIDE the link and overrides them category by category,
//                     which is what you need when the architect's grid must stay black while their
//                     walls go grey. Costs one override per element, so it is slow on a big link, and
//                     the collector it needs (the doc + view + link-instance one) does not exist on
//                     Revit 2020 — the fragment says so rather than failing.
//
// A REAL LIMIT, STATED PLAINLY: `RevitLinkGraphicsSettings` — the class that would let this set the
//   link's display MODE — carries only LinkVisibilityType and LinkedViewId. It has no halftone, no
//   transparency and no per-category override, on any Revit installed here, so it is not used. The
//   override path above is the one that actually works. (Verified against the shipped RevitAPI.dll on
//   2020 and 2024, not assumed.)
//
// SCOPE: one view, per view — same as every other override fragment here. To reuse it, apply to one
//        view and save that view as a View Template by hand.
// RELATED: recipes/mep-grayout.cs (the host-document half of the same job — run both on a linked job),
//          context/context-linked-models.cs (list the links first if you don't know their names),
//          filters/by-relationship/filter-by-linked-model-elements.cs (reach into a link for other work),
//          knowledge/live-model/graphic-override-precedence.md (what Revit throws away, and why).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Dry-run by default — it reports which links it WOULD change and
//    stops. Set dryRun = false only after reading the dry-run list. Do ONE link first, look at the
//    view, then do the rest.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool dryRun = true;                    // true = report only, change nothing

string linkNameContains = "";          // "" = every loaded link; else match on the link TYPE name
                                       // (the file name without .rvt, e.g. "ARCH" or "STR")

string mode = "whole-link";            // "whole-link"  = one override on each link instance (fast, all versions)
                                       // "by-category" = override the elements INSIDE the link, per category
                                       // "reset"       = clear this view's override off the link instance

bool halftone = true;                  // used by both override modes
int transparencyPercent = 80;          // 0 = solid, 100 = invisible

bool applyGreyColours = true;          // false = fade/halftone only, leave the link's own colours
// Same numbers as his own background standard in recipes/mep-grayout.cs, so a link treated here and a
// host wall treated there land on the same grey.
byte lineR = 150, lineG = 150, lineB = 150;
byte fillR = 200, fillG = 200, fillB = 200;
int lineWeight = 1;                    // 1..16

// "by-category" only. Categories NOT listed here are left completely alone inside the link.
BuiltInCategory[] categoriesToGrey = {
    BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Ceilings,
    BuiltInCategory.OST_Roofs, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
    BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns,
    BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Stairs,
    BuiltInCategory.OST_Furniture, BuiltInCategory.OST_Casework, BuiltInCategory.OST_GenericModel
};
int maxElementsPerLink = 5000;         // safety stop for "by-category" on a huge link

int? targetViewIdInt = null;           // null = active view; or an Element Id to target any view
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
    return sb.ToString();
}
if (view.IsTemplate)
{
    sb.AppendLine($"View '{view.Name}' is a VIEW TEMPLATE. Element overrides are set on a real view; apply there, then save the template.");
    return sb.ToString();
}
if (!view.AreGraphicsOverridesAllowed())
{
    sb.AppendLine($"View '{view.Name}' ({view.ViewType}) does not accept graphic overrides at all. Nothing to do.");
    return sb.ToString();
}
if (transparencyPercent < 0 || transparencyPercent > 100)
{
    sb.AppendLine($"transparencyPercent must be 0-100 (got {transparencyPercent}). Nothing changed.");
    return sb.ToString();
}

var linkInstances = new FilteredElementCollector(Document)
    .OfClass(typeof(RevitLinkInstance))
    .Cast<RevitLinkInstance>()
    .Where(li => li != null && li.IsValidObject)
    .ToList();

if (linkInstances.Count == 0)
{
    sb.AppendLine("No Revit links are loaded in this document — nothing to override.");
    return sb.ToString();
}

// Named by the link TYPE (the file), because that is the name a person recognises. Two placements of
// one file share a type name and are listed separately — each carries its own override.
Func<RevitLinkInstance, string> TypeNameOf = li =>
{
    try
    {
        var lt = Document.GetElement(li.GetTypeId()) as RevitLinkType;
        return lt != null ? (lt.Name ?? "(unnamed link)") : "(no link type)";
    }
    catch { return "(link type unreadable)"; }
};

var targets = linkInstances
    .Where(li => string.IsNullOrEmpty(linkNameContains)
              || TypeNameOf(li).IndexOf(linkNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
    .ToList();

if (targets.Count == 0)
{
    sb.AppendLine($"No loaded link's type name contains \"{linkNameContains}\". Loaded links:");
    foreach (var li in linkInstances) sb.AppendLine($"  {TypeNameOf(li)}   (instance Id {li.Id})");
    return sb.ToString();
}

// The doc + view + link-instance collector arrived after Revit 2020. Naming that constructor directly
// would stop this fragment compiling on 2020, and a try/catch does NOT help — it never gets to run.
// Looked up once, here, so one source serves 2020 through 2027.
var _fecLinkCtor = typeof(FilteredElementCollector)
    .GetConstructor(new[] { typeof(Document), typeof(ElementId), typeof(ElementId) });

if (mode == "by-category" && _fecLinkCtor == null)
{
    sb.AppendLine("mode \"by-category\" needs the view+link collector, which this Revit version does not have (it arrived after 2020).");
    sb.AppendLine("Use mode \"whole-link\" instead — it halftones and fades the link as one element and works here.");
    return sb.ToString();
}

if (dryRun)
{
    sb.AppendLine($"DRY RUN — mode \"{mode}\" on {targets.Count} link instance(s) in view '{view.Name}'. Nothing changed.");
    foreach (var li in targets) sb.AppendLine($"  would set: {TypeNameOf(li)}   (instance Id {li.Id})");
    if (mode != "reset")
    {
        sb.AppendLine($"  halftone {halftone}, transparency {transparencyPercent}%"
            + (applyGreyColours ? $", lines {lineR},{lineG},{lineB}, fill {fillR},{fillG},{fillB}, weight {lineWeight}" : ", colours untouched"));
        if (mode == "by-category")
            sb.AppendLine($"  categories reached inside the link: {string.Join(", ", categoriesToGrey.Select(c => c.ToString()))}");
    }
    sb.AppendLine("Set dryRun = false to apply. Do ONE link first and look at the view.");
    return sb.ToString();
}

// The solid fill pattern — same lookup the rest of color-graphics/ uses. Without it a colour tints the
// line only and the surface keeps whatever the link's own material draws.
ElementId solidFillId = ElementId.InvalidElementId;
try
{
    var solid = new FilteredElementCollector(Document)
        .OfClass(typeof(FillPatternElement))
        .Cast<FillPatternElement>()
        .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
    if (solid != null) solidFillId = solid.Id;
}
catch { }

var grey = new OverrideGraphicSettings();
if (applyGreyColours)
{
    var lc = new Color(lineR, lineG, lineB);
    var fc = new Color(fillR, fillG, fillB);
    grey.SetProjectionLineColor(lc);
    grey.SetCutLineColor(lc);
    if (lineWeight >= 1 && lineWeight <= 16)
    {
        grey.SetProjectionLineWeight(lineWeight);
        grey.SetCutLineWeight(lineWeight);
    }
    if (solidFillId != ElementId.InvalidElementId)
    {
        grey.SetSurfaceForegroundPatternVisible(true);
        grey.SetSurfaceForegroundPatternId(solidFillId);
        grey.SetSurfaceForegroundPatternColor(fc);
        grey.SetCutForegroundPatternVisible(true);
        grey.SetCutForegroundPatternId(solidFillId);
        grey.SetCutForegroundPatternColor(fc);
    }
}
if (transparencyPercent > 0) grey.SetSurfaceTransparency(transparencyPercent);
if (halftone) grey.SetHalftone(true);

var blank = new OverrideGraphicSettings();   // an all-unset override IS Revit's "no override"

int linksDone = 0, elementsDone = 0, failed = 0;
var notes = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Set Link Overrides"))
{
    t.Start();
    try
    {
        foreach (var li in targets)
        {
            string name = TypeNameOf(li);
            try
            {
                if (mode == "reset")
                {
                    view.SetElementOverrides(li.Id, blank);
                    linksDone++;
                    notes.Add($"  {name}: override cleared off the link instance.");
                    continue;
                }

                if (mode == "whole-link")
                {
                    view.SetElementOverrides(li.Id, grey);
                    linksDone++;
                    notes.Add($"  {name}: whole-link override applied (Id {li.Id}).");
                    continue;
                }

                if (mode == "by-category")
                {
                    var wanted = new HashSet<ElementId>(categoriesToGrey
                        .Select(bic => { try { var c = Category.GetCategory(Document, bic); return c != null ? c.Id : ElementId.InvalidElementId; } catch { return ElementId.InvalidElementId; } })
                        .Where(id => id != ElementId.InvalidElementId));

                    var col = (FilteredElementCollector)_fecLinkCtor.Invoke(new object[] { Document, view.Id, li.Id });
                    int here = 0, capped = 0;
                    foreach (var e in col.WhereElementIsNotElementType().ToElements())
                    {
                        if (here >= maxElementsPerLink) { capped++; continue; }
                        try
                        {
                            if (e == null || e.Category == null) continue;
                            if (!wanted.Contains(e.Category.Id)) continue;
                            view.SetElementOverrides(e.Id, grey);
                            here++;
                        }
                        catch { }   // a linked element that refuses an override — not an error worth stopping for
                    }
                    elementsDone += here;
                    linksDone++;
                    notes.Add($"  {name}: {here} element(s) inside the link overridden"
                              + (capped > 0 ? $" — STOPPED at maxElementsPerLink, {capped} more left untouched" : "") + ".");
                    continue;
                }

                notes.Add($"  {name}: unknown mode \"{mode}\" — expected whole-link / by-category / reset. Skipped.");
            }
            catch (Exception exOne)
            {
                failed++;
                notes.Add($"  {name}: FAILED — {exOne.Message}");
            }
        }

        t.Commit();
        sb.AppendLine($"Link overrides in view '{view.Name}': {linksDone} link(s) set, {failed} failed (mode \"{mode}\").");
        if (mode == "by-category") sb.AppendLine($"  {elementsDone} element(s) inside the links were overridden.");
        foreach (var n in notes) sb.AppendLine(n);
        if (linksDone > 0)
            sb.AppendLine("Look at the view now. If the link did not change, this view is probably driven by a VIEW TEMPLATE — a template owns V/G and wins over what is set here.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to set link overrides — rolled back, nothing changed. Reason: {ex.Message}");
    }
}

return sb.ToString();
