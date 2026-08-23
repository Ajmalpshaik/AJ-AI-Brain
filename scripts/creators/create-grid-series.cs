// ============================================================
// FRAGMENT (creator) — create-grid-series.cs
// PURPOSE: Set out a WHOLE STRUCTURAL GRID from a start point and a list of bay spacings — both
//          directions, named automatically A/B/C and 1/2/3, in one pass. The setting-out job as it is
//          actually described on a drawing: "6000, 6000, 7500, 6000 across; 8000, 8000 up".
// PRODUCES: elements (List<Element>, the newly created Grids), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// WHY THIS EXISTS ALONGSIDE create-grid.cs: that one takes explicit mm endpoint pairs, one tuple per
//   grid, which is right when the lines are already known. For a bay layout it means working out every
//   coordinate by hand before you can start — and for twelve grids in two directions that is where the
//   mistakes come from. Here the spacings ARE the input, which is how a grid is dimensioned.
//
// ✱✱ SPACINGS ARE GAPS, NOT POSITIONS. Four spacings make FIVE grids: the first sits at the start
//    point and each spacing steps to the next. Reading them as positions gives you one grid too few
//    and every line in the wrong place — worth stating because both readings look sensible.
//
// ✱✱ THE NAME HAS TO INCREMENT THE WAY A DRAWING DOES. Letters run A..Z then AA, AB — not A..Z then
//    stop, and not A1. Numbers run 1, 2, 3 from whatever you start at. A name Revit already has in the
//    document is REFUSED, so the grid is created and then reported as unnamed rather than losing the
//    whole run to one clash.
//
// ✱✱ REVIT'S OWN CONVENTION, and this is the one that surprises people: the grids you LABEL with
//    letters usually RUN horizontally and are spaced VERTICALLY. So `acrossSpacingsMm` spaces the
//    letter grids apart going up the page, and each of those lines is drawn left-to-right. Set
//    `swapNaming = true` if this project letters the other set.
//
// RELATED: creators/create-grid.cs (explicit endpoints, one at a time),
//          creators/create-levels.cs (the vertical equivalent — levels from storey heights),
//          actions/structural-changes/action-maximize-datum-extents.cs (make them reach across every view).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Creates real elements. Run it once on an empty area and look at the
//    result before using it on a live model — grids are easy to delete but tedious to undo one by one.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
double startXMm = 0;                 // the first intersection — bottom-left of the grid, in project mm
double startYMm = 0;

// Gaps between grids, in mm. N spacings produce N+1 grids. Empty list = skip that direction.
double[] acrossSpacingsMm = { 6000, 6000, 7500, 6000 };   // steps in +Y, lines drawn along X  -> lettered
double[] upSpacingsMm     = { 8000, 8000 };               // steps in +X, lines drawn along Y  -> numbered

string firstLetter = "A";            // names for the first set: A, B, C ... Z, AA, AB
int firstNumber = 1;                 // names for the second set: 1, 2, 3
bool swapNaming = false;             // true = number the horizontal set and letter the vertical set

double extensionMm = 3000;           // how far each line runs past the outermost grid at BOTH ends
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

Func<double, double> mm = v => v / 304.8;

if (acrossSpacingsMm.Any(s => s <= 0) || upSpacingsMm.Any(s => s <= 0))
{
    sb.AppendLine("Every spacing must be greater than zero — a zero or negative gap puts two grids on the same line.");
    return sb.ToString();
}
if (acrossSpacingsMm.Length == 0 && upSpacingsMm.Length == 0)
{
    sb.AppendLine("Both spacing lists are empty — nothing to create.");
    return sb.ToString();
}

// Positions accumulated from the gaps. N gaps -> N+1 positions, starting at 0.
Func<double[], List<double>> PositionsFrom = gaps =>
{
    var outp = new List<double> { 0.0 };
    double run = 0.0;
    foreach (var g in gaps) { run += g; outp.Add(run); }
    return outp;
};

var acrossPos = acrossSpacingsMm.Length > 0 ? PositionsFrom(acrossSpacingsMm) : new List<double>();
var upPos     = upSpacingsMm.Length > 0     ? PositionsFrom(upSpacingsMm)     : new List<double>();

// Spreadsheet-column naming: A..Z, AA, AB. Anything that is not a plain letter run is left alone and
// simply numbered off the end, rather than producing nonsense.
Func<string, int, string> LetterAt = (start, step) =>
{
    string s = (start ?? "A").Trim().ToUpperInvariant();
    if (s.Length == 0 || !s.All(char.IsLetter)) return (start ?? "A") + (step + 1);
    int n = 0;
    foreach (var ch in s) n = n * 26 + (ch - 'A' + 1);
    n += step;
    var outp = "";
    while (n > 0) { int r = (n - 1) % 26; outp = (char)('A' + r) + outp; n = (n - 1) / 26; }
    return outp;
};

// Every name already in the document — a clash is refused by Revit, and knowing up front turns that
// into a reported skip instead of a thrown run.
var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var g in new FilteredElementCollector(Document).OfClass(typeof(Grid)).Cast<Grid>())
{
    try { if (!string.IsNullOrEmpty(g.Name)) existingNames.Add(g.Name); } catch { }
}

double ext = mm(extensionMm);
double x0 = mm(startXMm), y0 = mm(startYMm);

// The full extent of the OTHER direction decides how long each line has to be, so the two sets cross.
double acrossSpan = acrossPos.Count > 0 ? mm(acrossPos.Last()) : 0.0;
double upSpan     = upPos.Count > 0     ? mm(upPos.Last())     : 0.0;

int created = 0, unnamed = 0;
var names = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Create Grid Series"))
{
    t.Start();
    try
    {
        // Set 1 — lines running along X, stepped in +Y. Lettered by default.
        for (int i = 0; i < acrossPos.Count; i++)
        {
            double y = y0 + mm(acrossPos[i]);
            var p1 = new XYZ(x0 - ext, y, 0);
            var p2 = new XYZ(x0 + upSpan + ext, y, 0);
            var grid = Grid.Create(Document, Line.CreateBound(p1, p2));
            elements.Add(grid);
            created++;

            string want = swapNaming ? (firstNumber + i).ToString() : LetterAt(firstLetter, i);
            if (existingNames.Contains(want)) { unnamed++; names.Add($"{want} (already used — left as {grid.Name})"); }
            else
            {
                try { grid.Name = want; existingNames.Add(want); names.Add(want); }
                catch { unnamed++; names.Add($"{want} (refused — left as {grid.Name})"); }
            }
        }

        // Set 2 — lines running along Y, stepped in +X. Numbered by default.
        for (int i = 0; i < upPos.Count; i++)
        {
            double x = x0 + mm(upPos[i]);
            var p1 = new XYZ(x, y0 - ext, 0);
            var p2 = new XYZ(x, y0 + acrossSpan + ext, 0);
            var grid = Grid.Create(Document, Line.CreateBound(p1, p2));
            elements.Add(grid);
            created++;

            string want = swapNaming ? LetterAt(firstLetter, i) : (firstNumber + i).ToString();
            if (existingNames.Contains(want)) { unnamed++; names.Add($"{want} (already used — left as {grid.Name})"); }
            else
            {
                try { grid.Name = want; existingNames.Add(want); names.Add(want); }
                catch { unnamed++; names.Add($"{want} (refused — left as {grid.Name})"); }
            }
        }

        t.Commit();

        sb.AppendLine($"Created {created} grid(s) from ({startXMm}, {startYMm}) mm — "
            + $"{acrossPos.Count} across ({string.Join(", ", acrossSpacingsMm)} mm) and "
            + $"{upPos.Count} up ({string.Join(", ", upSpacingsMm)} mm).");
        sb.AppendLine($"  Names: {string.Join(" · ", names)}");
        if (unnamed > 0)
            sb.AppendLine($"  {unnamed} grid(s) kept Revit's default name because the one wanted was already in use — rename them, or delete the old grid first.");
        sb.AppendLine("  Grids are created flat at Z=0 and appear on every plan view whose extents reach them. Check one bay dimension against the drawing before relying on the set.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        elements.Clear();
        sb.AppendLine($"FAILED to create the grid series — rolled back, nothing created. Reason: {ex.Message}");
    }
}
