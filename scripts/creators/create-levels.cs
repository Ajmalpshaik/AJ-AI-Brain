// ============================================================
// FRAGMENT (creator) — create-levels.cs
// PURPOSE: Batch-create Levels, either at even spacing from a start elevation, or at an explicit list
//          of elevations. Matches the user's own past request shape ("create levels up to N").
// PRODUCES: elements (List<Element>, the newly created Level(s)), sb (StringBuilder, summary appended)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role
//          as a filter — it produces `elements` — so any action fragment (rename, color, report) can
//          be appended after it exactly like it would after a filter.
// ============================================================
// Every elevation/spacing number is a per-request input — never a default.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool useExplicitElevations = false;
List<double> explicitElevationsMm = new List<double> { 0, 3000, 6000 }; // used when useExplicitElevations = true
int count = 3;                 // used when useExplicitElevations = false
double startElevationMm = 0;
double spacingMm = 3000;
string namePrefix = "Level ";  // level N gets name "{namePrefix}{N}" unless it collides with an existing name
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<double> elevationsMm = useExplicitElevations
    ? explicitElevationsMm
    : Enumerable.Range(0, count).Select(i => startElevationMm + i * spacingMm).ToList();

// CASE-INSENSITIVE on purpose. `.ToHashSet()` uses the default ordinal comparer, so with a lowercase
// namePrefix the guard does not see the existing "Level 1" and happily names a new level "level 1" —
// and Revit ACCEPTS it (measured live 2026-08-07), leaving two levels whose names differ only in case.
// create-material.cs already solves the same duplicate-name problem with OrdinalIgnoreCase; this now
// matches it.
var existingNames = new FilteredElementCollector(Document)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .Select(l => l.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

List<Element> elements = new List<Element>();

using (var t = new Transaction(Document, "AJ Tools - Create Levels"))
{
    t.Start();
    try
    {
        int n = 1;
        foreach (var elevMm in elevationsMm)
        {
            double elevFt = UnitUtils.ConvertToInternalUnits(elevMm, DisplayUnitType.DUT_MILLIMETERS);
            var level = Level.Create(Document, elevFt);

            string candidateName = $"{namePrefix}{n}";
            while (existingNames.Contains(candidateName)) { n++; candidateName = $"{namePrefix}{n}"; }
            level.Name = candidateName;
            existingNames.Add(candidateName);

            elements.Add(level);
            n++;
        }
        t.Commit();
        // Report what the MODEL holds — name, Id and the elevation read back off each Level — not the
        // requested list. The old line echoed `elevationsMm` and printed no names and no Element Ids, so
        // a level that landed at the wrong elevation or took a different auto-name was invisible, and it
        // broke this README's own "always report the Element ID for specific elements" rule.
        sb.AppendLine($"Created {elements.Count} level(s):");
        foreach (Level madeLevel in elements.Cast<Level>())
            sb.AppendLine($"  '{madeLevel.Name}' (Id {madeLevel.Id.IntegerValue}) at {UnitUtils.ConvertFromInternalUnits(madeLevel.Elevation, DisplayUnitType.DUT_MILLIMETERS):0.##} mm");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED to create levels — rolled back, nothing changed. Reason: {ex.Message}");
        elements = new List<Element>();
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
