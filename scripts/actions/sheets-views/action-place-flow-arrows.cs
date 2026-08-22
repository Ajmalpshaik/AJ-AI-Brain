// ============================================================
// FRAGMENT (action) — action-place-flow-arrows.cs
// PURPOSE: Place FLOW-DIRECTION ARROW annotations along the ducts in `elements`, at a spacing, pointing
//          the way the air actually goes — the "show the flow direction on the drawing" job.
// ASSUMES: elements (List<Element>, Ducts) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
//
// ✱ THE DIRECTION COMES FROM THE CONNECTORS, NOT FROM THE CURVE. A duct's `LocationCurve` runs
//   whichever way it happened to be drawn — start-to-end tells you nothing about flow. The real answer
//   is on the connectors: find the one whose `Direction` is `In` and the one that is `Out`, and the
//   arrow points from In toward Out. Draw the arrow along the curve direction instead and half your
//   drawing will be pointing backwards, with nothing to warn you.
//
// GOTCHA: THIS NEEDS AN ANNOTATION FAMILY THAT ALREADY EXISTS in the project — an arrow symbol. Name it
//         in `arrowFamilyName`/`arrowTypeName`. The fragment does NOT create or load one; if the name
//         is not found it lists what generic annotation symbols ARE loaded and stops.
// GOTCHA: DRY RUN BY DEFAULT — this places annotation in bulk and a wrong spacing fills a drawing.
// GOTCHA: HORIZONTAL RUNS ONLY. A riser has no meaningful arrow in a plan view, so a run whose
//         direction is near-vertical is skipped and counted rather than given a nonsense arrow.
// GOTCHA: the symbol is placed in the VIEW, then ROTATED about the view's own normal
//         (`view.ViewDirection`) to line up with the run. Rotating about world Z is wrong in any view
//         that is not a plain plan.
// GOTCHA: the family symbol must be ACTIVATED (`symbol.Activate()`) before the first placement or
//         `NewFamilyInstance` throws — the classic first-use-in-a-session trap.
// GOTCHA: this places annotation, which is VIEW-SPECIFIC. Arrows placed in one view do not appear in
//         another. That is correct behaviour, not a bug — place them per view you are issuing.
// NOTE: `spacingMm` is a per-request drawing decision, not a default. Ask.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Duct Flow Annotations.
//   Compile-checked on 2020/2024/2027. It needs a real arrow family to prove, which this Brain's test
//   model does not have. Run it on ONE duct and check which way the arrow points.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int viewIdInt = 0;                   // REQUIRED — the view to place the arrows in
string arrowFamilyName = "";         // REQUIRED — the annotation family holding the arrow
string arrowTypeName = "";           // "" = the family's first type
double spacingMm = 3000;             // ASK. distance between arrows along each run
bool dryRun = true;                  // true = report what would be placed; false = actually place
int maxArrows = 300;                 // hard stop, so a small spacing cannot bury a drawing
// ---- END INPUTS ----

const double MM = 304.8;

var view = Document.GetElement(new ElementId(viewIdInt)) as View;

if (view == null || view.IsTemplate)
{
    sb.AppendLine($"viewIdInt {viewIdInt} is not a usable view.");
    return sb.ToString();
}

// ---------- find the arrow symbol ----------
var symbols = new FilteredElementCollector(Document)
    .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
    .Where(s => s != null && s.Category != null &&
                s.Category.CategoryType == CategoryType.Annotation).ToList();

FamilySymbol symbol = null;
if (!string.IsNullOrWhiteSpace(arrowFamilyName))
    symbol = symbols.FirstOrDefault(s =>
        s.Family != null &&
        s.Family.Name.IndexOf(arrowFamilyName, StringComparison.OrdinalIgnoreCase) >= 0 &&
        (string.IsNullOrWhiteSpace(arrowTypeName) ||
         s.Name.IndexOf(arrowTypeName, StringComparison.OrdinalIgnoreCase) >= 0));

if (symbol == null)
{
    sb.AppendLine(string.IsNullOrWhiteSpace(arrowFamilyName)
        ? "Set arrowFamilyName to the annotation family holding the arrow."
        : $"No annotation family matching '{arrowFamilyName}'" +
          (string.IsNullOrWhiteSpace(arrowTypeName) ? "" : $" / type '{arrowTypeName}'") + ".");
    sb.AppendLine("Annotation symbols loaded in this project:");
    foreach (var g in symbols.GroupBy(s => s.Family != null ? s.Family.Name : "?").OrderBy(g => g.Key).Take(30))
        sb.AppendLine("  " + g.Key + " — types: " + string.Join(", ", g.Select(s => s.Name).Take(6)));
    return sb.ToString();
}

double spacing = spacingMm / MM;
if (spacing <= 0) { sb.AppendLine("spacingMm must be greater than 0."); return sb.ToString(); }

int placed = 0, skippedVertical = 0, skippedNoCurve = 0, noFlow = 0, failed = 0;
var rows = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Place Flow Arrows"))
{
    t.Start();
    try
    {
        if (!symbol.IsActive) { symbol.Activate(); Document.Regenerate(); }   // or the first placement throws

        foreach (var e in elements)
        {
            var lc = e == null ? null : e.Location as LocationCurve;
            var line = lc == null ? null : lc.Curve as Line;
            if (line == null) { skippedNoCurve++; continue; }

            XYZ dir = line.Direction;
            if (Math.Abs(dir.Z) > 0.9) { skippedVertical++; continue; }   // a riser gets no plan arrow

            // ---- FLOW direction from the CONNECTORS, not from the curve ----
            XYZ flowDir = null;
            try
            {
                var mep = e as MEPCurve;
                if (mep != null && mep.ConnectorManager != null)
                {
                    XYZ inPt = null, outPt = null;
                    foreach (Connector c in mep.ConnectorManager.Connectors)
                    {
                        if (c == null) continue;
                        string d = c.Direction.ToString();
                        if (d == "In" && inPt == null) inPt = c.Origin;
                        else if (d == "Out" && outPt == null) outPt = c.Origin;
                    }
                    if (inPt != null && outPt != null && inPt.DistanceTo(outPt) > 1e-6)
                        flowDir = (outPt - inPt).Normalize();
                }
            }
            catch { }

            if (flowDir == null) { noFlow++; flowDir = dir; }   // fall back, and SAY SO in the report

            XYZ flat = new XYZ(flowDir.X, flowDir.Y, 0);
            if (flat.GetLength() < 1e-9) { skippedVertical++; continue; }
            flat = flat.Normalize();

            // ---- one arrow every `spacing` along the run ----
            double len = line.Length;
            int n = Math.Max(1, (int)Math.Floor(len / spacing));
            for (int i = 1; i <= n; i++)
            {
                if (placed >= maxArrows) break;
                XYZ p = line.Evaluate((double)i / (n + 1), true);

                if (dryRun) { placed++; continue; }

                try
                {
                    var inst = Document.Create.NewFamilyInstance(p, symbol, view);
                    // Rotate about the VIEW'S normal, not world Z — world Z is wrong in a section.
                    double angle = Math.Atan2(flat.DotProduct(view.UpDirection), flat.DotProduct(view.RightDirection));
                    if (Math.Abs(angle) > 1e-9)
                    {
                        var axis = Line.CreateBound(p, p + view.ViewDirection);
                        ElementTransformUtils.RotateElement(Document, inst.Id, axis, angle);
                    }
                    placed++;
                }
                catch { failed++; }
            }

            if (rows.Count < 25)
                rows.Add(e.Id + " | " + Math.Round(len * MM) + " | " + n +
                         (flowDir == dir ? " | NO connector flow — used the drawn direction" : " | from connectors"));

            if (placed >= maxArrows) break;
        }

        if (dryRun) t.RollBack(); else t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing placed. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine((dryRun ? "DRY RUN — nothing placed. " : "") +
              $"{(dryRun ? "Would place" : "Placed")} {placed} arrow(s) from '{symbol.Family.Name}: {symbol.Name}' " +
              $"every {spacingMm} mm in view '{view.Name}'." +
              (placed >= maxArrows ? $"  STOPPED at the maxArrows cap ({maxArrows}) — raise spacingMm." : ""));
if (noFlow > 0)
    sb.AppendLine($"*** {noFlow} run(s) had NO usable connector flow direction, so the DRAWN direction was used " +
                  "— those arrows may point backwards. Check them, or connect the runs first. ***");
if (skippedVertical > 0) sb.AppendLine($"{skippedVertical} vertical run(s) skipped — a riser has no plan arrow.");
if (skippedNoCurve > 0) sb.AppendLine($"{skippedNoCurve} element(s) had no straight location curve and were skipped.");
if (failed > 0) sb.AppendLine($"{failed} placement(s) failed.");

if (rows.Count > 0)
{
    sb.AppendLine("");
    sb.AppendLine("Duct Id | Length (mm) | Arrows | Direction source");
    sb.AppendLine("--- | --- | --- | ---");
    foreach (var r in rows) sb.AppendLine(r);
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
