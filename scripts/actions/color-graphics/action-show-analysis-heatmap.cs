// ============================================================
// FRAGMENT (action) — action-show-analysis-heatmap.cs
// PURPOSE: Paint a GRADIENT HEATMAP over the model from a number per element — pipes by pressure drop,
//          spaces by airflow, ducts by velocity, rooms by occupancy. Revit's own Analysis Visualization
//          Framework, with its colour legend, rather than a flat per-element colour override.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist, e.g. from filter-by-category.cs.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// HOW THIS DIFFERS FROM EVERY OTHER COLOUR FRAGMENT HERE, and why it is worth having:
//   `action-color-by-group.cs` and friends set a per-element OverrideGraphicSettings — one flat colour
//   per bucket, no scale, no legend, and you cannot tell 26 l/s from 260 l/s because both are "blue".
//   This is a CONTINUOUS scale: Revit interpolates the colour between your min and max, draws the
//   legend itself, and the result reads like an analysis drawing rather than a selection highlight.
//   Use the override fragments to mark WHICH; use this to show HOW MUCH.
//
// ✱✱ FIVE THINGS THAT MAKE IT SHOW NOTHING, none of which errors:
//   1. THE VIEW MUST BE TOLD TO USE THE STYLE. Creating the display style is not enough — the view's
//      own `VIEW_ANALYSIS_DISPLAY_STYLE` parameter has to point at it. Skip that and the data is
//      stored, the legend is absent, and the model looks untouched.
//   2. THE SCHEMA ID GOES STALE. `RegisterResult` hands back an int that is only valid while that
//      schema is still registered on that view's manager. Cache it across runs, or let someone clear
//      the view, and `UpdateSpatialFieldPrimitive` writes against an id that no longer exists. Always
//      re-check it against `GetRegisteredResults()` — which is what this does on every run.
//   3. IT ONLY DRAWS ON FACES IT WAS GIVEN. The primitive is attached to a `Face`, so an element with
//      no solid geometry in this view (an annotation, a line-based element, anything the view's detail
//      level draws as a stick) contributes nothing and is reported as skipped, not as failed.
//   4. A 3D VIEW SHOWS IT BEST. It works in a plan, but on a plan the painted faces are the ones you
//      are looking down at, which for a pipe is the top of a cylinder — nearly invisible.
//   5. THE VALUES MUST VARY. Min == max makes the whole model one colour, which reads as broken.
//      Reported explicitly rather than left to look like a failure.
//
// ✱✱ AND ONE HARD LIMIT: a single spatial-field primitive takes about 1000 points before Revit starts
//   dropping them. This paints per FACE, so the limit is not normally reached — but if this is ever
//   extended to point clouds, split the points across several primitives.
//
// RELATED: actions/color-graphics/action-color-by-group.cs (flat colour per bucket — the "which" tool),
//          actions/reporting/action-report-coverage.cs and the HVAC skills (where the numbers come from),
//          skills/ajtools-visual-report/SKILL.md (his rule: numbers also get a CHART in the reply —
//          this paints the model, it does not replace the chart).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. It CREATES a display style and writes analysis data into the view.
//    `mode = "clear"` removes the data again. Run it on a handful of elements first and look at the view.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "show";                 // "show"  = paint the heatmap
                                      // "clear" = wipe analysis data off the target view and stop

// Where the number comes from, per element.
string valueSource = "parameter";     // "parameter" = read parameterName off each element
                                      // "index"     = 1,2,3... in the order elements arrive (a test pattern)
string parameterName = "Flow";        // instance parameter first, then the element's type

string legendTitle = "Flow";          // shown on Revit's own legend
string legendUnits = "";              // free text, e.g. "l/s" — Revit does not convert anything

// The gradient. Revit interpolates between these two.
byte minR = 0,   minG = 0,   minB = 255;     // low  = blue
byte maxR = 255, maxG = 0,   maxB = 0;       // high = red
bool showLegend = true;
bool showGridLines = false;           // grid lines across each painted face

int? targetViewIdInt = null;          // null = active view
int maxElements = 500;                // safety stop — every face of every element is a primitive
// ---- END INPUTS ----

View avView = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (avView == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else if (avView.IsTemplate)
{
    sb.AppendLine($"'{avView.Name}' is a view template. Analysis results are written to a real view.");
}
else
{
    // ---- CLEAR ------------------------------------------------------------------------------------
    if (mode == "clear")
    {
        using (var t = new Transaction(Document, "AJ Tools - Clear Analysis Display"))
        {
            t.Start();
            try
            {
                var existing = Autodesk.Revit.DB.Analysis.SpatialFieldManager.GetSpatialFieldManager(avView);
                if (existing == null)
                {
                    t.RollBack();
                    sb.AppendLine($"View '{avView.Name}' carries no analysis data — nothing to clear.");
                }
                else
                {
                    existing.Clear();
                    t.Commit();
                    sb.AppendLine($"Cleared the analysis heatmap off view '{avView.Name}'. The display style itself is left in the project for reuse.");
                }
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to clear analysis data — rolled back. Reason: {ex.Message}");
            }
        }
    }
    else
    {
        // ---- Gather the value per element BEFORE opening a transaction, so a bad parameter name
        // ---- costs nothing and is reported as itself rather than as a failed write.
        var scored = new List<Tuple<Element, double>>();
        int noValue = 0, noGeometry = 0;

        var pool = elements.Take(maxElements).ToList();
        int idx = 0;
        foreach (var e in pool)
        {
            idx++;
            double v;
            if (valueSource == "index") { v = idx; }
            else
            {
                Parameter p = null;
                try { p = e.LookupParameter(parameterName); } catch { }
                if (p == null || !p.HasValue)
                {
                    try
                    {
                        var te = Document.GetElement(e.GetTypeId());
                        if (te != null) p = te.LookupParameter(parameterName);
                    }
                    catch { }
                }
                if (p == null || !p.HasValue) { noValue++; continue; }

                // Numbers only. A text parameter has no place on a gradient, and saying so is more
                // use than painting everything one colour.
                if (p.StorageType == StorageType.Double) v = p.AsDouble();
                else if (p.StorageType == StorageType.Integer) v = p.AsInteger();
                else { noValue++; continue; }
            }
            scored.Add(Tuple.Create(e, v));
        }

        if (scored.Count == 0)
        {
            sb.AppendLine($"No element carried a numeric '{parameterName}' (checked instance then type on {pool.Count} element(s)). Nothing painted.");
        }
        else
        {
            double lo = scored.Min(s => s.Item2), hi = scored.Max(s => s.Item2);

            using (var t = new Transaction(Document, "AJ Tools - Analysis Heatmap"))
            {
                t.Start();
                try
                {
                    // 1. The manager. One per view; created on demand. The `1` is how many values
                    //    each measurement carries — one number per point here.
                    var sfm = Autodesk.Revit.DB.Analysis.SpatialFieldManager.GetSpatialFieldManager(avView);
                    if (sfm == null) sfm = Autodesk.Revit.DB.Analysis.SpatialFieldManager.CreateSpatialFieldManager(avView, 1);
                    sfm.Clear();   // start from a clean view, or old primitives sit under the new ones

                    // 2. The schema. Re-registered every run on purpose — see trap 2 in the header.
                    int schemaId = -1;
                    try
                    {
                        foreach (var rid in sfm.GetRegisteredResults())
                        {
                            try
                            {
                                if (string.Equals(sfm.GetResultSchema(rid).Name, legendTitle, StringComparison.OrdinalIgnoreCase))
                                { schemaId = rid; break; }
                            }
                            catch { }   // a schema id that no longer resolves — exactly the stale case
                        }
                    }
                    catch { }
                    if (schemaId == -1)
                    {
                        var schema = new Autodesk.Revit.DB.Analysis.AnalysisResultSchema(legendTitle,
                            string.IsNullOrEmpty(legendUnits) ? legendTitle : legendTitle + " (" + legendUnits + ")");
                        schemaId = sfm.RegisterResult(schema);
                    }

                    // 3. Paint one primitive per FACE, one value for the whole face.
                    int painted = 0;
                    var geomOpt = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false, View = avView };

                    foreach (var pair in scored)
                    {
                        var el = pair.Item1;
                        double val = pair.Item2;
                        bool any = false;
                        try
                        {
                            var geo = el.get_Geometry(geomOpt);
                            if (geo == null) { noGeometry++; continue; }

                            foreach (var g in geo)
                            {
                                // A family instance hides its solids one level down, inside an
                                // instance geometry object. Miss this and every family paints nothing.
                                var solids = new List<Solid>();
                                var asSolid = g as Solid;
                                if (asSolid != null && asSolid.Faces.Size > 0) solids.Add(asSolid);
                                var inst = g as GeometryInstance;
                                if (inst != null)
                                {
                                    foreach (var ig in inst.GetInstanceGeometry())
                                    {
                                        var s2 = ig as Solid;
                                        if (s2 != null && s2.Faces.Size > 0) solids.Add(s2);
                                    }
                                }

                                foreach (var solid in solids)
                                {
                                    foreach (Face face in solid.Faces)
                                    {
                                        try
                                        {
                                            int prim = sfm.AddSpatialFieldPrimitive(face, Transform.Identity);
                                            var bb = face.GetBoundingBox();
                                            var uv = new List<UV> { bb.Min, bb.Max };
                                            var vals = new List<Autodesk.Revit.DB.Analysis.ValueAtPoint>
                                            {
                                                new Autodesk.Revit.DB.Analysis.ValueAtPoint(new List<double> { val }),
                                                new Autodesk.Revit.DB.Analysis.ValueAtPoint(new List<double> { val })
                                            };
                                            sfm.UpdateSpatialFieldPrimitive(prim,
                                                new Autodesk.Revit.DB.Analysis.FieldDomainPointsByUV(uv),
                                                new Autodesk.Revit.DB.Analysis.FieldValues(vals),
                                                schemaId);
                                            any = true;
                                        }
                                        catch { }   // a face Revit will not take a primitive on
                                    }
                                }
                            }
                        }
                        catch { }
                        if (any) painted++; else noGeometry++;
                    }

                    // 4. The display style, and pointing the view at it. Without step 4b nothing draws.
                    string styleName = "AJ Heatmap - " + legendTitle;
                    ElementId styleId = ElementId.InvalidElementId;
                    try { styleId = Autodesk.Revit.DB.Analysis.AnalysisDisplayStyle.FindByName(Document, styleName); } catch { }

                    // ✱✱ FindByName does NOT return InvalidElementId when the style is absent — it
                    //    returns an id that does not resolve. Testing the id's NUMBER would work and
                    //    means naming ElementId.IntegerValue (gone in 2027) or .Value (absent before
                    //    it), so this asks the document to resolve it instead: version-proof, and it
                    //    also catches a style that was deleted since it was named.
                    if (styleId == ElementId.InvalidElementId || Document.GetElement(styleId) == null)
                    {
                        var surface = new Autodesk.Revit.DB.Analysis.AnalysisDisplayColoredSurfaceSettings();
                        surface.ShowGridLines = showGridLines;
                        var colours = new Autodesk.Revit.DB.Analysis.AnalysisDisplayColorSettings();
                        colours.ColorSettingsType = Autodesk.Revit.DB.Analysis.AnalysisDisplayStyleColorSettingsType.GradientColor;
                        colours.MinColor = new Color(minR, minG, minB);
                        colours.MaxColor = new Color(maxR, maxG, maxB);
                        var legend = new Autodesk.Revit.DB.Analysis.AnalysisDisplayLegendSettings();
                        legend.ShowLegend = showLegend;
                        var created = Autodesk.Revit.DB.Analysis.AnalysisDisplayStyle
                            .CreateAnalysisDisplayStyle(Document, styleName, surface, colours, legend);
                        if (created != null) styleId = created.Id;
                    }

                    bool pointed = false;
                    if (styleId != ElementId.InvalidElementId)
                    {
                        var vp = avView.get_Parameter(BuiltInParameter.VIEW_ANALYSIS_DISPLAY_STYLE);
                        if (vp != null && !vp.IsReadOnly) { vp.Set(styleId); pointed = true; }
                    }

                    t.Commit();

                    sb.AppendLine($"HEATMAP on '{avView.Name}': {painted} element(s) painted, range {Math.Round(lo, 3)} to {Math.Round(hi, 3)}"
                        + (string.IsNullOrEmpty(legendUnits) ? "" : " " + legendUnits) + ".");
                    if (noValue > 0)    sb.AppendLine($"  {noValue} element(s) had no numeric '{parameterName}' on instance or type — not painted.");
                    if (noGeometry > 0) sb.AppendLine($"  {noGeometry} element(s) gave no solid faces in this view — nothing to paint on.");
                    if (elements.Count > maxElements)
                        sb.AppendLine($"  STOPPED at maxElements = {maxElements}; {elements.Count - maxElements} more were not painted.");
                    if (Math.Abs(hi - lo) < 1e-9)
                        sb.AppendLine("  ! Every value is the same, so the whole thing is ONE colour. That is the data, not a fault.");
                    if (!pointed)
                        sb.AppendLine("  ! The view would not accept the display style (its Analysis Display Style is read-only, usually a VIEW TEMPLATE). The data is stored but nothing will draw until that is set by hand.");
                    if (avView.ViewType != ViewType.ThreeD)
                        sb.AppendLine($"  Note: this is a {avView.ViewType} view. A heatmap reads best in 3D — in plan you only see the faces pointing up.");
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    sb.AppendLine($"FAILED to build the heatmap — rolled back, nothing changed. Reason: {ex.Message}");
                }
            }
        }
    }
}
