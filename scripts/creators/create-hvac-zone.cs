// ============================================================
// FRAGMENT (creator) — create-hvac-zone.cs
// PURPOSE: Create an HVAC Zone on a Level and add existing MEP Spaces to it — the grouping layer above
//          Spaces that create-space.cs / set-space-airflow.cs stop at.
// PRODUCES: elements (List<Element>, the single new Zone), sb
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: a Space can belong to only one Zone — Spaces already in another named zone are MOVED by Revit
//         when added here (they silently leave the old zone); check zone membership first if that matters.
// GOTCHA: the Zone and the Spaces must be on the same Phase — mismatches are the usual reason AddSpaces
//         rejects a space.
// NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2 suggestions.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string zoneName = "Zone 1";
int levelIdInt = 0;             // level the zone lives on
string phaseName = null;        // null = the document's last phase (usually "New Construction")
int[] spaceIdInts = new int[] { };  // Space Element Ids to add (from filter-by-space.cs / report-room-space-data)
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

var level = Document.GetElement(new ElementId(levelIdInt)) as Level;
Phase phase = null;
var phases = Document.Phases.Cast<Phase>().ToList();
phase = phaseName == null ? phases.LastOrDefault() : phases.FirstOrDefault(p => p.Name == phaseName);

if (level == null) sb.AppendLine($"levelIdInt {levelIdInt} is not a valid Level.");
else if (phase == null) sb.AppendLine(phaseName == null ? "Document has no Phases at all (unexpected)." : $"Phase '{phaseName}' not found. Available: {string.Join(", ", phases.Select(p => p.Name))}");
else
{
    using (var t = new Transaction(Document, "AJ Tools - Create HVAC Zone"))
    {
        t.Start();
        try
        {
            var zone = Document.Create.NewZone(level, phase);
            try { zone.Name = zoneName; } catch { } // name collision — zone still created with default name

            int added = 0, rejected = 0;
            var notes = new List<string>();
            if (spaceIdInts.Length > 0)
            {
                var spaceSet = new Autodesk.Revit.DB.Mechanical.SpaceSet();
                foreach (var sid in spaceIdInts)
                {
                    var space = Document.GetElement(new ElementId(sid)) as Autodesk.Revit.DB.Mechanical.Space;
                    if (space == null) { rejected++; notes.Add($"Id {sid}: not a Space"); continue; }
                    spaceSet.Insert(space);
                }
                if (spaceSet.Size > 0)
                {
                    bool ok = zone.AddSpaces(spaceSet);
                    if (ok) added = spaceSet.Size;
                    else { rejected += spaceSet.Size; notes.Add("AddSpaces returned false — check the Phase gotcha in the header"); }
                }
            }

            elements.Add(zone);
            t.Commit();
            sb.AppendLine($"Created zone '{zone.Name}' (Id {zone.Id.IntegerValue}) on level '{level.Name}', phase '{phase.Name}' — {added} space(s) added, {rejected} rejected.");
            if (notes.Count > 0) sb.AppendLine("  " + string.Join("; ", notes));
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED to create zone — rolled back, nothing changed. Reason: {ex.Message}");
            elements = new List<Element>();
        }
    }
}
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
