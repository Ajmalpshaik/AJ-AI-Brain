// ============================================================
// FRAGMENT (action) — action-join-geometry.cs
// PURPOSE: Join (or unjoin) the geometry of every element in `elements` with ONE specific target element
//          — e.g. join a batch of structural framing into a column, or unjoin a batch from a wall.
//          Many-to-one, the common real case; for element-to-element pairs within the set itself, run
//          this once per pair instead.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int targetElementIdInt = 0; // the ONE element every item in `elements` joins (or unjoins) with
string mode = "join"; // "join" | "unjoin"
// ---- END INPUTS ----

var target = Document.GetElement(new ElementId(targetElementIdInt));
if (target == null)
{
    sb.AppendLine($"Target element Id {targetElementIdInt} not found.");
}
else
{
    int done = 0, skipped = 0;

    using (var t = new Transaction(Document, "AJ Tools - Join Geometry"))
    {
        t.Start();
        try
        {
            foreach (var e in elements)
            {
                try
                {
                    bool alreadyJoined = JoinGeometryUtils.AreElementsJoined(Document, e, target);
                    if (mode == "unjoin")
                    {
                        if (alreadyJoined) { JoinGeometryUtils.UnjoinGeometry(Document, e, target); done++; }
                        else skipped++;
                    }
                    else
                    {
                        if (!alreadyJoined) { JoinGeometryUtils.JoinGeometry(Document, e, target); done++; }
                        else skipped++;
                    }
                }
                catch { skipped++; } // some element pairs can't be joined (no touching geometry, etc.) — skip, don't fail the batch
            }
            t.Commit();
            sb.AppendLine($"{(mode == "unjoin" ? "Unjoined" : "Joined")} {done} element(s) with '{target.Name}' (Id {target.Id.IntegerValue}), skipped {skipped} (already in that state, or geometry doesn't support it).");
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to join/unjoin geometry — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
