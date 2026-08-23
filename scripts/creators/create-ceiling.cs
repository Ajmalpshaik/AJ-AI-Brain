// ============================================================
// FRAGMENT (creator) — create-ceiling.cs
// PURPOSE: Create a Ceiling from a closed boundary of mm plan points on a given Level.
// PRODUCES: elements (List<Element>, the single new Ceiling, wrapped in a list), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱✱ CORRECTED 2026-08-23 — READ THIS BEFORE TRUSTING ANY OLDER NOTE. Until today this fragment created
//    nothing at all: it was a stub reading "CONFIRMED IMPOSSIBLE — Ceiling.Create only exists from Revit
//    2022". The API fact was right and still is. The VERDICT was wrong, because it was written when only
//    Revit 2020 was being tested and then stated without a version qualifier, so it read as "ceilings
//    cannot be made, full stop". This PC runs Revit 2020, 2024 AND 2027. On 2024 and 2027 ceiling
//    creation works normally, and the Brain was talking itself out of a job it could do.
//    The lesson, worth more than the fragment: an "impossible" recorded against ONE version has to SAY
//    which version, or it silently becomes a lie the day another one is installed.
//
// VERSION: Ceiling.Create is reached BY NAME at run time, so this one source compiles and runs on 2020
//          through 2027. On Revit 2020 the method genuinely does not exist — the fragment then reports
//          that clearly (ask for one ceiling to be drawn by hand: Architecture tab > Ceiling) instead of
//          throwing a compile error. Everything else in this library works on ceilings that already
//          exist on any version — ray-trace-to-ceiling.cs, action-snap-to-ceiling-grid.cs, the filters.
// GOTCHA: the boundary auto-closes (last point connects back to the first) and must not self-intersect.
// RELATED: to build ceilings on the shape of ROOMS rather than typed coordinates — the common real job —
//          use actions/structural-changes/action-create-from-room-boundaries.cs instead.
// ⚠ NOT YET RUN AGAINST A REAL MODEL in its working form — rewritten 2026-08-23. The boundary-building
//   and reflection pattern is the one proven in create-floor.cs. Run it once on a small test room.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int levelIdInt = 0;             // required — the Level the ceiling belongs to
string ceilingTypeName = null;  // exact CeilingType name; null = first CeilingType in the project
double heightAboveLevelMm = 2700; // ceiling height above that level
// closed boundary in plan, mm — pairs of {x, y}; last point auto-connects to the first
double[,] boundaryMm = new double[,] { { 0, 0 }, { 4000, 0 }, { 4000, 3000 }, { 0, 3000 } };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

// Looked up by name — see the VERSION note. Naming the type directly would break the 2020 compile.
var ceilingType = typeof(Element).Assembly.GetType("Autodesk.Revit.DB.Ceiling");
var ceilingCreateM = ceilingType == null ? null : ceilingType.GetMethod("Create",
    new[] { typeof(Document), typeof(IList<CurveLoop>), typeof(ElementId), typeof(ElementId) });

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;

if (ceilingCreateM == null)
{
    sb.AppendLine("This Revit version has no Ceiling.Create — it arrived in Revit 2022, and this session is older.");
    sb.AppendLine("Ask for ONE ceiling to be drawn by hand (Architecture tab > Ceiling). Everything else here works on it:");
    sb.AppendLine("  ray-trace-to-ceiling.cs, action-snap-to-ceiling-grid.cs, filter-by-category.cs on OST_Ceilings.");
    sb.AppendLine("On Revit 2022+ this same fragment creates the ceiling directly — nothing to change.");
}
else if (level == null)
{
    sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
}
else if (boundaryMm.GetLength(0) < 3)
{
    sb.AppendLine($"Boundary has only {boundaryMm.GetLength(0)} point(s) — a ceiling needs at least 3.");
}
else
{
    var ceilingTypes = new FilteredElementCollector(Document).OfClass(typeof(CeilingType)).Cast<CeilingType>().ToList();
    var chosen = ceilingTypeName == null
        ? ceilingTypes.FirstOrDefault()
        : ceilingTypes.FirstOrDefault(ct => ct.Name == ceilingTypeName);

    if (chosen == null)
    {
        sb.AppendLine(ceilingTypeName == null
            ? "No CeilingType exists in this project at all."
            : $"CeilingType '{ceilingTypeName}' not found. Available: {string.Join(", ", ceilingTypes.Select(ct => ct.Name))}");
    }
    else
    {
        using (var t = new Transaction(Document, "AJ Tools - Create Ceiling"))
        {
            t.Start();
            try
            {
                int n = boundaryMm.GetLength(0);
                double z = level.Elevation;
                var lines = new List<Curve>();
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    var p1 = new XYZ(mm(boundaryMm[i, 0]), mm(boundaryMm[i, 1]), z);
                    var p2 = new XYZ(mm(boundaryMm[j, 0]), mm(boundaryMm[j, 1]), z);
                    lines.Add(Line.CreateBound(p1, p2));
                }

                var loop = CurveLoop.Create(lines);
                var made = (Element)ceilingCreateM.Invoke(null,
                    new object[] { Document, (IList<CurveLoop>)new List<CurveLoop> { loop }, chosen.Id, level.Id });

                if (made == null)
                {
                    sb.AppendLine("Revit returned no ceiling — check the boundary does not self-intersect.");
                    t.RollBack();
                }
                else
                {
                    var p = made.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                    if (p != null && !p.IsReadOnly) p.Set(mm(heightAboveLevelMm));

                    elements.Add(made);
                    t.Commit();
                    sb.AppendLine($"Created Ceiling Id {made.Id} — type '{chosen.Name}', "
                                + $"level '{level.Name}', {heightAboveLevelMm} mm above it.");
                }
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                sb.AppendLine($"Ceiling creation failed: {msg}");
                if (t.HasStarted() && !t.HasEnded()) t.RollBack();
            }
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
