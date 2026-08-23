// ============================================================
// FRAGMENT (action) — action-convert-cad-to-directshape.cs
// PURPOSE: Turn an imported CAD solid into REAL Revit elements — one DirectShape per solid, in a category
//          you choose. The fix for "the contractor sent a 3D DWG and Revit treats it as one lump":
//          afterwards each piece can be selected, filtered, coloured, scheduled and clash-checked like
//          anything else in the model.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — ImportInstance elements
//          (a linked or imported CAD), e.g. from filter-by-links.cs or filter-by-category.cs on
//          OST_ImportObjectStyles.
//
// ✱✱ WHY AN IMPORTED CAD IS USELESS UNTIL YOU DO THIS. An `ImportInstance` is ONE element holding all the
//    geometry. It has no category per piece, no parameters, nothing to schedule, and a clash check can
//    only tell you "something in the DWG" — never which pipe. `DirectShape.CreateElement` makes each
//    solid its own element in a real Revit category, and from that moment the whole library works on it.
//
// ✱✱ THE GEOMETRY IS ONE LEVEL DOWN, AND THAT IS THE STEP PEOPLE MISS. `get_Geometry()` on an
//    ImportInstance returns GeometryInstances, not solids. You must call `GetInstanceGeometry()` on each
//    one to reach the actual bodies, and the ones you want are `Solid` with `Volume > 0` — a DWG is full
//    of zero-volume faces and stray curves that look like geometry and are not.
//
// ✱✱ AND `IncludeNonVisibleObjects` MUST BE TRUE. Much of an imported solid arrives on layers Revit
//    considers non-visible; leave the default and you convert a fraction of the model and never know.
//
// GOTCHA: THIS DOES NOT DELETE THE IMPORT. The original stays where it is so you can compare, and turn it
//         off yourself once you are happy. Deleting it is a separate, deliberate step.
// GOTCHA: A DirectShape carries NO parameters of its own beyond the category's built-ins. It is geometry
//         in a category, not a family — you cannot give it a type. If you need types, this is the wrong
//         route and the geometry has to be rebuilt as a family.
// GOTCHA: one DirectShape per solid means a busy DWG can produce hundreds of elements. `maxCreate` is a
//         real guard, not a formality — run the dry run first and read the count.
// GOTCHA: the category must be one Revit allows for a DirectShape. Most model categories are; annotation
//         categories are not, and the refusal is reported by name rather than swallowed.
// RELATED: filter-by-links.cs finds the imports; action-report-geometry-complexity.cs will tell you
//          afterwards how heavy what you just made really is.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Run the dry run on ONE import and check the
//   solid count looks like the drawing before creating anything.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string targetCategory = "OST_GenericModel";  // e.g. OST_GenericModel, OST_DuctCurves, OST_PipeCurves, OST_StructuralFraming
double minVolumeM3 = 0.0;                    // ignore slivers smaller than this (0 = keep everything with volume)
bool dryRun = true;                          // true = count what WOULD be made, create nothing
int maxCreate = 200;                         // hard cap; 0 = no cap
// ---- END INPUTS ----

const double FT3_PER_M3 = 35.3146667;

var imports = elements.OfType<ImportInstance>().ToList();
if (imports.Count == 0)
{
    // Be helpful rather than blank — people reach for this with the whole model selected.
    var available = new FilteredElementCollector(Document).OfClass(typeof(ImportInstance))
        .Cast<ImportInstance>().ToList();
    sb.AppendLine("No ImportInstance (imported/linked CAD) in `elements`.");
    sb.AppendLine(available.Count == 0
        ? "There is no imported CAD in this document at all."
        : $"This document has {available.Count}: " + string.Join(", ",
            available.Select(i => { try { return Document.GetElement(i.GetTypeId())?.Name ?? i.Id.ToString(); } catch { return i.Id.ToString(); } })));
    return sb.ToString();
}

// Resolve the category and check Revit will accept it for a DirectShape.
BuiltInCategory bic;
if (!Enum.TryParse(targetCategory, out bic))
{
    sb.AppendLine($"'{targetCategory}' is not a BuiltInCategory name. Use e.g. OST_GenericModel, OST_DuctCurves, OST_PipeCurves.");
    return sb.ToString();
}
var catId = new ElementId(bic);
if (!DirectShape.IsValidCategoryId(catId, Document))
{
    sb.AppendLine($"Revit will not allow a DirectShape in category '{targetCategory}'.");
    sb.AppendLine("OST_GenericModel always works and is the safe default.");
    return sb.ToString();
}

// ---- collect the solids, one level down (see the header) ----
var found = new List<Tuple<ImportInstance, Solid>>();
int zeroVolume = 0, tooSmall = 0;

foreach (var imp in imports)
{
    var opt = new Options { IncludeNonVisibleObjects = true, DetailLevel = ViewDetailLevel.Fine };
    GeometryElement geo = null;
    try { geo = imp.get_Geometry(opt); } catch { }
    if (geo == null) continue;

    Action<GeometryElement> walk = null;
    walk = (ge) =>
    {
        foreach (GeometryObject g in ge)
        {
            var solid = g as Solid;
            if (solid != null)
            {
                if (solid.Volume <= 0) { zeroVolume++; continue; }
                if (minVolumeM3 > 0 && solid.Volume < minVolumeM3 * FT3_PER_M3) { tooSmall++; continue; }
                found.Add(Tuple.Create(imp, solid));
                continue;
            }
            var inst = g as GeometryInstance;
            if (inst != null)
            {
                var sub = inst.GetInstanceGeometry();   // THE step that is easy to miss
                if (sub != null) walk(sub);
            }
        }
    };
    walk(geo);
}

double totalM3 = found.Sum(f => f.Item2.Volume) / FT3_PER_M3;

sb.AppendLine($"{imports.Count} imported CAD element(s) in.");
sb.AppendLine($"Usable solids found: {found.Count}   (total volume {totalM3:F2} m³)");
if (zeroVolume > 0) sb.AppendLine($"  {zeroVolume} zero-volume body/bodies ignored — faces and curves, not solids.");
if (tooSmall > 0)   sb.AppendLine($"  {tooSmall} solid(s) below {minVolumeM3} m³ ignored.");
sb.AppendLine($"Target category: {targetCategory}");
sb.AppendLine();

if (found.Count == 0)
{
    sb.AppendLine("NOTHING TO CONVERT. If the drawing clearly has 3D in it, the usual causes are:");
    sb.AppendLine("  - it is a 2D DWG (lines and hatches only) — there is nothing solid to make;");
    sb.AppendLine("  - the solids are ACIS bodies Revit did not import as solids, which no script can fix here;");
    sb.AppendLine("  - the import is a LINK rather than an import — bind it first, then run this.");
}
else if (maxCreate > 0 && found.Count > maxCreate && !dryRun)
{
    sb.AppendLine($"STOPPED — {found.Count} solids is over the cap of {maxCreate}. Raise maxCreate if that is really intended.");
}
else if (dryRun)
{
    sb.AppendLine($"DRY RUN — nothing created. {found.Count} DirectShape element(s) would be made in {targetCategory}.");
    if (maxCreate > 0 && found.Count > maxCreate)
        sb.AppendLine($"NOTE: that is over the cap of {maxCreate} — raise it before the real run or it will stop.");
    sb.AppendLine("The original import is NOT deleted; turn it off yourself once you have compared the two.");
    sb.AppendLine("Set dryRun = false to create them.");
}
else
{
    int made = 0, failed = 0;
    var newOnes = new List<Element>();

    using (var t = new Transaction(Document, "AJ Tools - CAD to DirectShape"))
    {
        t.Start();
        foreach (var f in found)
        {
            try
            {
                var ds = DirectShape.CreateElement(Document, catId);
                ds.ApplicationId = "AJ Tools";
                ds.ApplicationDataId = f.Item1.Id.ToString();
                ds.SetShape(new List<GeometryObject> { f.Item2 });

                // Name it after the import so the origin is traceable afterwards.
                try
                {
                    var srcName = Document.GetElement(f.Item1.GetTypeId())?.Name;
                    if (!string.IsNullOrWhiteSpace(srcName)) ds.Name = srcName;
                }
                catch { }

                newOnes.Add(ds);
                made++;
            }
            catch (Exception ex)
            {
                failed++;
                if (failed <= 10) sb.AppendLine($"  solid from {f.Item1.Id}: {ex.Message}");
            }
        }
        t.Commit();
    }

    elements.AddRange(newOnes);
    sb.AppendLine($"Created {made} DirectShape element(s) in {targetCategory}. Failed: {failed}.");
    sb.AppendLine("The original import is still there — compare the two, then hide or delete it deliberately.");
    sb.AppendLine("These are now ordinary elements: filter, colour, schedule and clash-check them like anything else.");
    sb.AppendLine("Worth running next: action-report-geometry-complexity.cs, to see how heavy what you just made really is.");
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
