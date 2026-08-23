// ============================================================
// FRAGMENT (action) — action-create-from-room-boundaries.cs
// PURPOSE: Build a Floor, a Ceiling, a Filled Region or detail lines ON each Room/Space in `elements`,
//          taking the shape from the room's OWN boundary — "put a ceiling in every room", "give me a
//          slab under this block of rooms", "hatch these rooms so I can mark them up". No typed
//          coordinates: the room already knows its shape, including its holes.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist — Rooms or Spaces, e.g. from
//          filter-by-category.cs on OST_Rooms.
// PRODUCES: appends the new elements to `elements` so an action below (set-parameter, colour) can chain.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ THE FACT THAT MAKES THIS WORTH HAVING: GetBoundarySegments returns a LIST OF LOOPS, and only the
//    FIRST is the outside edge. Every loop after it is a HOLE — a core, a shaft, a column the room wraps
//    around. Build from loop [0] alone and the slab covers the shaft; build from all of them and the
//    hole is cut for you. creators/create-floor.cs takes one typed loop and says so in its header ("no
//    openings"); this is the fragment for the real-room case.
//
// GOTCHA: A ROOM THAT IS NOT ENCLOSED HAS ZERO AREA and no usable boundary. Those are skipped and named
//         in the report rather than silently producing nothing — see filter-by-unenclosed-spatial-
//         elements.cs to find them first.
// GOTCHA: NO MODAL DIALOG CAN BE ANSWERED FROM A SCRIPT. Creating floors room by room raises Revit's
//         "highlighted floors overlap" warning, and the usual fix — an IFailuresPreprocessor that
//         swallows it — CANNOT BE WRITTEN IN A FRAGMENT: the bridge wraps every fragment in a single
//         method, so no `class ... : IFailuresPreprocessor` will compile (same structural limit already
//         recorded in action-transfer-views-between-documents.cs). What DOES work is
//         FailureHandlingOptions, which is settings rather than an interface:
//         SetForcedModalHandling(false) + SetClearAfterRollback(true) keeps the warning non-blocking.
//         See knowledge/live-model/failure-handling-without-a-class.md.
// VERSION: Floor.Create / Ceiling.Create are reached BY NAME at run time so one source covers 2020-2027.
//          Revit 2022 replaced Document.Create.NewFloor(CurveArray,...) with Floor.Create(IList<CurveLoop>,...);
//          naming either directly stops it compiling on the other side. CEILINGS ARE 2022+ ONLY — on
//          2020 Ceiling.Create does not exist and this reports that instead of throwing.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Every Revit call in it is proven elsewhere in
//   this library (boundary walk: action-report-room-boundaries.cs; floor creation: create-floor.cs).
//   Run it on ONE room first, look at the result, then use it for the batch.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string createWhat = "floor";        // "floor" | "ceiling" | "filledRegion" | "detailLines"
string typeName = null;             // exact type name; null = first of that type in the project
double offsetMm = 0;                // height above the room's level (floors/ceilings only)
bool copyRoomNameToComments = true; // write the room's name into the new element's Comments
bool dryRun = true;                 // true = report what WOULD be made, change nothing
int maxCreate = 0;                  // 0 = no cap; otherwise stop after this many
// ---- END INPUTS ----

Func<double, double> mm = v => v / 304.8;
var madeElements = new List<Element>();
int skippedUnenclosed = 0, failed = 0, holesCut = 0;
var failures = new List<string>();

var rooms = elements.Where(e => e is Autodesk.Revit.DB.Architecture.Room
                             || e is Autodesk.Revit.DB.Mechanical.Space
                             || e is SpatialElement).ToList();

if (rooms.Count == 0)
{
    sb.AppendLine("No Rooms or Spaces in `elements` — put filter-by-category.cs on OST_Rooms above this.");
}
else
{
    // ---- resolve the target type once ----
    ElementId typeId = ElementId.InvalidElementId;
    string typeLabel = "";
    bool typeOk = true;

    Func<Type, string> resolveType = (clrType) =>
    {
        var all = new FilteredElementCollector(Document).OfClass(clrType).Cast<ElementType>().ToList();
        if (all.Count == 0) { typeOk = false; return $"No {clrType.Name} exists in this project at all."; }
        var chosen = typeName == null ? all.First() : all.FirstOrDefault(t => t.Name == typeName);
        if (chosen == null) { typeOk = false; return $"{clrType.Name} '{typeName}' not found. Available: {string.Join(", ", all.Select(t => t.Name))}"; }
        typeId = chosen.Id; typeLabel = chosen.Name; return null;
    };

    string typeError = null;
    if (createWhat == "floor") typeError = resolveType(typeof(FloorType));
    else if (createWhat == "ceiling") typeError = resolveType(typeof(CeilingType));
    else if (createWhat == "filledRegion") typeError = resolveType(typeof(FilledRegionType));
    else if (createWhat == "detailLines") { typeLabel = "detail lines"; }
    else { typeOk = false; typeError = $"createWhat '{createWhat}' is not one of: floor, ceiling, filledRegion, detailLines."; }

    // ---- version probe: both creation APIs looked up by name ----
    var floorCreateM = typeof(Floor).GetMethod("Create",
        new[] { typeof(Document), typeof(IList<CurveLoop>), typeof(ElementId), typeof(ElementId) });
    var ceilingType = typeof(Element).Assembly.GetType("Autodesk.Revit.DB.Ceiling");
    var ceilingCreateM = ceilingType == null ? null : ceilingType.GetMethod("Create",
        new[] { typeof(Document), typeof(IList<CurveLoop>), typeof(ElementId), typeof(ElementId) });

    if (createWhat == "ceiling" && ceilingCreateM == null)
    {
        typeOk = false;
        typeError = "Ceiling creation needs Revit 2022 or newer — Ceiling.Create does not exist on this version. "
                  + "Ask for the ceiling to be drawn once by hand (Architecture > Ceiling); everything else here works on it.";
    }

    if (!typeOk)
    {
        sb.AppendLine(typeError);
    }
    else
    {
        var view = Document.ActiveView;
        if ((createWhat == "filledRegion" || createWhat == "detailLines") && (view == null || view.IsTemplate))
        {
            sb.AppendLine("Filled regions and detail lines are view-specific — open the view you want them in first.");
        }
        else
        {
            sb.AppendLine($"{rooms.Count} room(s) in. Creating: {createWhat}" + (typeLabel == "" ? "" : $" — type '{typeLabel}'"));
            if (offsetMm != 0 && (createWhat == "floor" || createWhat == "ceiling"))
                sb.AppendLine($"Offset above level: {offsetMm} mm");

            var opt = new SpatialElementBoundaryOptions();

            using (var t = new Transaction(Document, "AJ Tools - Create from room boundaries"))
            {
                if (!dryRun)
                {
                    t.Start();
                    // The fragment-safe substitute for a failure preprocessor — settings, not an interface.
                    var fo = t.GetFailureHandlingOptions();
                    fo.SetForcedModalHandling(false);
                    fo.SetClearAfterRollback(true);
                    t.SetFailureHandlingOptions(fo);
                }

                foreach (var el in rooms)
                {
                    if (maxCreate > 0 && madeElements.Count >= maxCreate) break;

                    var spatial = el as SpatialElement;
                    string roomName = spatial == null ? $"Id {el.Id.IntegerValue}" : $"{spatial.Name}";

                    // An unenclosed room reports zero area and gives no usable boundary.
                    var areaParam = el.get_Parameter(BuiltInParameter.ROOM_AREA);
                    if (areaParam == null || areaParam.AsDouble() <= 0)
                    {
                        skippedUnenclosed++;
                        failures.Add($"  skipped (not enclosed, zero area): {roomName}");
                        continue;
                    }

                    IList<IList<BoundarySegment>> loops = null;
                    try { loops = spatial.GetBoundarySegments(opt); } catch { }
                    if (loops == null || loops.Count == 0)
                    {
                        skippedUnenclosed++;
                        failures.Add($"  skipped (no boundary returned): {roomName}");
                        continue;
                    }

                    // Loop [0] is the outside edge; every loop after it is a hole.
                    var curveLoops = new List<CurveLoop>();
                    foreach (var loop in loops)
                    {
                        var cl = new CurveLoop();
                        bool loopOk = true;
                        foreach (var seg in loop)
                        {
                            try { cl.Append(seg.GetCurve()); } catch { loopOk = false; break; }
                        }
                        if (loopOk && cl.GetExactLength() > 0) curveLoops.Add(cl);
                    }

                    if (curveLoops.Count == 0)
                    {
                        failed++;
                        failures.Add($"  FAILED (boundary would not close): {roomName}");
                        continue;
                    }
                    if (curveLoops.Count > 1) holesCut += curveLoops.Count - 1;

                    if (dryRun)
                    {
                        sb.AppendLine($"  would create on '{roomName}' — 1 outline"
                            + (curveLoops.Count > 1 ? $" + {curveLoops.Count - 1} hole(s)" : ""));
                        madeElements.Add(el); // count only; nothing is created in a dry run
                        continue;
                    }

                    try
                    {
                        Element made = null;
                        var levelId = spatial.LevelId;

                        if (createWhat == "floor")
                        {
                            made = (Element)floorCreateM.Invoke(null,
                                new object[] { Document, (IList<CurveLoop>)curveLoops, typeId, levelId });
                            var p = made.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            if (p != null && !p.IsReadOnly) p.Set(mm(offsetMm));
                        }
                        else if (createWhat == "ceiling")
                        {
                            made = (Element)ceilingCreateM.Invoke(null,
                                new object[] { Document, (IList<CurveLoop>)curveLoops, typeId, levelId });
                            var p = made.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                            if (p != null && !p.IsReadOnly) p.Set(mm(offsetMm));
                        }
                        else if (createWhat == "filledRegion")
                        {
                            made = FilledRegion.Create(Document, typeId, view.Id, curveLoops);
                        }
                        else // detailLines
                        {
                            foreach (var cl in curveLoops)
                                foreach (var c in cl)
                                {
                                    var dc = Document.Create.NewDetailCurve(view, c);
                                    if (dc != null) madeElements.Add(dc);
                                }
                            sb.AppendLine($"  '{roomName}' — detail lines drawn");
                            continue;
                        }

                        if (made == null)
                        {
                            failed++;
                            failures.Add($"  FAILED (Revit returned nothing): {roomName}");
                            continue;
                        }

                        if (copyRoomNameToComments)
                        {
                            var c = made.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            if (c != null && !c.IsReadOnly) c.Set(roomName);
                        }

                        madeElements.Add(made);
                        sb.AppendLine($"  '{roomName}' -> {createWhat} Id {made.Id.IntegerValue}"
                            + (curveLoops.Count > 1 ? $" ({curveLoops.Count - 1} hole(s) cut)" : ""));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        failures.Add($"  FAILED: {roomName} — {msg}");
                    }
                }

                if (!dryRun) t.Commit();
            }

            sb.AppendLine();
            if (dryRun)
            {
                sb.AppendLine($"DRY RUN — nothing was created. {madeElements.Count} room(s) would be built on, "
                            + $"{holesCut} hole(s) would be cut, {skippedUnenclosed} skipped, {failed} would fail.");
                sb.AppendLine("Set dryRun = false to create.");
                madeElements.Clear();
            }
            else
            {
                sb.AppendLine($"Created {madeElements.Count}. Holes cut: {holesCut}. Skipped (not enclosed): {skippedUnenclosed}. Failed: {failed}.");
                elements.AddRange(madeElements);
            }

            foreach (var f in failures) sb.AppendLine(f);
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
