// ============================================================
// FRAGMENT (action) — action-set-line-style.cs
// PURPOSE: Override line WEIGHT and/or line PATTERN (dashed, dotted, ...) for every element in
//          `elements` — every other action in this group only ever touches line/fill COLOR, never weight
//          or pattern. Read-modify-write: reads each element's EXISTING overrides first, so a prior
//          color/transparency/halftone override on the same element isn't wiped out. Either input can be
//          left at its "don't change" value to touch only the other.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) already exist from a filter fragment above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int lineWeight = -1; // -1 = don't change; else 1-16 (Revit's valid line-weight range)
string linePatternName = ""; // "" = don't change; "solid" = reset to solid; else an exact Line Pattern name (e.g. "Dash", "Dot")
int? targetViewIdInt = null; // null = active view; set an Element Id to target any view, even one not currently open on screen
// ---- END INPUTS ----

View view = targetViewIdInt.HasValue ? Document.GetElement(new ElementId(targetViewIdInt.Value)) as View : Document.ActiveView;

if (view == null)
{
    sb.AppendLine($"Target view (Id {targetViewIdInt}) not found or is not a view.");
}
else
{
    ElementId patternId = null;
    if (!string.IsNullOrEmpty(linePatternName))
    {
        if (linePatternName.Equals("solid", StringComparison.OrdinalIgnoreCase))
        {
            patternId = LinePatternElement.GetSolidPatternId();
        }
        else
        {
            var patternElem = new FilteredElementCollector(Document).OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .FirstOrDefault(p => p.Name.Equals(linePatternName, StringComparison.OrdinalIgnoreCase));
            if (patternElem == null)
            {
                var available = new FilteredElementCollector(Document).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>().Select(p => p.Name);
                sb.AppendLine($"Line Pattern '{linePatternName}' not found. Available: {string.Join(", ", available)}");
            }
            else
            {
                patternId = patternElem.Id;
            }
        }
    }

    bool patternRequestedButMissing = !string.IsNullOrEmpty(linePatternName) && patternId == null;

    if (!patternRequestedButMissing)
    {
        using (var t = new Transaction(Document, "AJ Tools - Set Line Style"))
        {
            t.Start();
            try
            {
                foreach (var e in elements)
                {
                    var ogs = view.GetElementOverrides(e.Id);
                    if (lineWeight >= 1 && lineWeight <= 16)
                    {
                        ogs.SetProjectionLineWeight(lineWeight);
                        ogs.SetCutLineWeight(lineWeight);
                    }
                    if (patternId != null)
                    {
                        ogs.SetProjectionLinePatternId(patternId);
                        ogs.SetCutLinePatternId(patternId);
                    }
                    view.SetElementOverrides(e.Id, ogs);
                }
                t.Commit();
                sb.AppendLine($"Set line style on {elements.Count} element(s) in view '{view.Name}'" +
                    (lineWeight >= 1 && lineWeight <= 16 ? $", weight {lineWeight}" : "") +
                    (patternId != null ? $", pattern '{linePatternName}'" : "") + ".");
            }
            catch (Exception ex)
            {
                t.RollBack();
                sb.AppendLine($"FAILED to set line style — rolled back, nothing changed. Reason: {ex.Message}");
            }
        }
    }
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
