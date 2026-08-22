// ============================================================
// FRAGMENT (action) — action-find-replace-text-notes.cs
// PURPOSE: Find (report) or find-and-replace a string inside the TEXT of the TextNotes in `elements` —
//          annotation QA across views/sheets. Different job from action-add-parameter-prefix-suffix.cs
//          (which edits parameter values) — this edits TextNote.Text, the words on the drawing.
// ASSUMES: elements (List<Element>, TextNotes — filter-by-category.cs on OST_TextNotes, or
//          filter-by-parameter-text.cs to pre-narrow) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose.
// GOTCHA: report first (mode="find"), replace second — the explorer-first rule; a text replace across
//         hundreds of sheets is bulk by any definition.
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED — created 2026-07-26 from the round-2
//          suggestions.", which the README had already superseded. The verification was recorded there and never
//          here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string mode = "find";           // "find" (report matches only) | "replace"
string findText = "";           // text to search for (plain substring)
string replaceWith = "";        // replace mode only
bool caseSensitive = false;
// ---- END INPUTS ----

var notes = elements.OfType<TextNote>().ToList();
var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

Func<string, bool> matches = txt =>
    !string.IsNullOrEmpty(txt) && txt.IndexOf(findText, comparison) >= 0;

if (notes.Count == 0)
{
    sb.AppendLine("No TextNotes in `elements` — feed this from filter-by-category.cs on Text Notes (OST_TextNotes).");
}
else if (string.IsNullOrEmpty(findText))
{
    sb.AppendLine("findText is empty — nothing to search for.");
}
else if (mode == "find")
{
    var hits = notes.Where(n => matches(n.Text)).ToList();
    sb.AppendLine($"'{findText}' found in {hits.Count} of {notes.Count} TextNote(s){(caseSensitive ? " (case-sensitive)" : "")}:");
    foreach (var n in hits.Take(30))
    {
        var owner = Document.GetElement(n.OwnerViewId) as View;
        var preview = n.Text.Replace("\r", " ").Replace("\n", " ");
        if (preview.Length > 60) preview = preview.Substring(0, 60) + "...";
        sb.AppendLine($"  - Id {n.Id} in view '{owner?.Name ?? "?"}': \"{preview}\"");
    }
    if (hits.Count > 30) sb.AppendLine($"  ... +{hits.Count - 30} more");
}
else if (mode == "replace")
{
    using (var t = new Transaction(Document, "AJ Tools - Find/Replace Text Notes"))
    {
        t.Start();
        try
        {
            int changed = 0;
            foreach (var n in notes.Where(n => matches(n.Text)))
            {
                string oldText = n.Text;
                string newText;
                if (caseSensitive)
                {
                    newText = oldText.Replace(findText, replaceWith);
                }
                else
                {
                    // case-insensitive replace, preserving everything around the matches
                    var sbT = new System.Text.StringBuilder(); int idx = 0;
                    while (true)
                    {
                        int hit = oldText.IndexOf(findText, idx, comparison);
                        if (hit < 0) { sbT.Append(oldText.Substring(idx)); break; }
                        sbT.Append(oldText.Substring(idx, hit - idx)).Append(replaceWith);
                        idx = hit + findText.Length;
                    }
                    newText = sbT.ToString();
                }
                if (newText != oldText) { n.Text = newText; changed++; }
            }
            t.Commit();
            sb.AppendLine($"Replaced '{findText}' -> '{replaceWith}' in {changed} of {notes.Count} TextNote(s){(caseSensitive ? " (case-sensitive)" : "")}.");
        }
        catch (Exception ex)
        {
            try { t.RollBack(); } catch { }
            sb.AppendLine($"FAILED mid-replace — rolled back, NO text was changed. Reason: {ex.Message}");
        }
    }
}
else
{
    sb.AppendLine($"Unknown mode '{mode}' — use \"find\" or \"replace\".");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
