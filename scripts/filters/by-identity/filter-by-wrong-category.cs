// ============================================================
// FRAGMENT (filter) — filter-by-wrong-category.cs
// PURPOSE: Find elements that ARE one thing but were MODELLED as another — the family name, type name
//          or equipment-tag prefix says "louvre" / "damper" / "fan", but the element sits in the wrong
//          Revit category. Answers "which of my X are not in category Y", and leaves exactly those
//          elements in `elements` so an action fragment can colour, select or report them.
//          Different from filter-by-category-and-family.cs, which finds things INSIDE one category —
//          this one deliberately scans every category and keeps only the mismatches.
// PRODUCES: elements (List<Element>), sb (StringBuilder, summary + a per-category breakdown)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// SOURCE: the technique was run live twice on 2026-08-20 — louvres on 4355-BHVD-3D-60P00-BL006A
//         (7 found, 0 correctly categorised) and on BL003A (10 found, 2 correct) — and its findings
//         drove a real re-categorisation. Its two id comparisons were moved off the removed
//         `ElementId.IntegerValue` on 2026-08-22 — see ../../../knowledge/revit-version-compatibility.md.
//         This generalised file is compile-checked, not yet run
//         verbatim; run it on one job and mark it PROVEN. See
//         ../../../knowledge/live-model/family-category-change.md for what changing the category costs.
// ============================================================
// Whole-model, unbounded scan — per ../../../knowledge/live-model/core.md "watch for unbounded output",
// this walks every FamilyInstance in the document. Fine on its own; don't chain several in one script.

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
// Words that identify the thing, matched case-insensitively against family name AND type name.
string[] nameKeywords   = new string[] { "louvre", "louver", "_eal", "eal_", "_fal", "fal_", "_stl", "stl_" };
// Equipment Tag prefixes that identify it when the family name does not. [] to skip.
// Project 4355 codes live in ../../../knowledge/glossary.md — HGH exhaust, HWL weather, HSL sand trap.
string[] tagPrefixes    = new string[] { "HGH", "HWL", "HSL" };
// The category these SHOULD be in. Anything matching above but sitting elsewhere is returned.
BuiltInCategory expectedCategory = BuiltInCategory.OST_DuctTerminal;
// true  = return only the mismatches (the normal case — "show me what's wrong")
// false = return every match including correctly-categorised ones (for a full inventory)
bool onlyMismatches     = true;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
sb.AppendLine("Document: " + Document.Title);   // core.md: always name the document you actually read

string TagOf(Element el)
{
    try
    {
        var p = el.LookupParameter("Equipment Tag");
        if (p != null && p.StorageType == StorageType.String && !string.IsNullOrEmpty(p.AsString())) return p.AsString();
        var m = el.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
        if (m != null && m.StorageType == StorageType.String) return m.AsString() ?? "";
    }
    catch { }
    return "";
}

var matched = new List<FamilyInstance>();
foreach (var fi in new FilteredElementCollector(Document)
    .OfClass(typeof(FamilyInstance))
    .WhereElementIsNotElementType()
    .Cast<FamilyInstance>())
{
    string fam = "", typ = "";
    try { fam = fi.Symbol?.Family?.Name ?? ""; } catch { }
    try { typ = fi.Symbol?.Name ?? ""; } catch { }

    string hay = (fam + " | " + typ).ToLowerInvariant();
    bool hit = false;
    foreach (var k in nameKeywords)
    {
        if (!string.IsNullOrEmpty(k) && hay.Contains(k.ToLowerInvariant())) { hit = true; break; }
    }
    if (!hit && tagPrefixes != null && tagPrefixes.Length > 0)
    {
        string t = TagOf(fi).Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(t))
        {
            foreach (var tp in tagPrefixes)
            {
                if (!string.IsNullOrEmpty(tp) && t.StartsWith(tp.ToUpperInvariant())) { hit = true; break; }
            }
        }
    }
    if (hit) matched.Add(fi);
}

// Compare ElementId to ElementId, never to an int: `ElementId.IntegerValue` was removed at Revit 2024
// and `new ElementId(BuiltInCategory)` + the `==`/`!=` operators are correct on 2020 through 2027 with
// no reflection — which matters here, because this comparison runs once per FamilyInstance in the model.
ElementId expectedId = new ElementId(expectedCategory);
var wrong = matched.Where(f => { try { return f.Category == null || f.Category.Id != expectedId; } catch { return true; } }).ToList();
var right = matched.Count - wrong.Count;

List<Element> elements = (onlyMismatches ? wrong : matched).Cast<Element>().ToList();

// Breakdown by category + family, so a false positive from a loose keyword is visible before acting.
var rows = new Dictionary<string, int>();
var tags = new Dictionary<string, string>();
foreach (var f in matched)
{
    string cat = ""; try { cat = f.Category?.Name ?? "(no category)"; } catch { cat = "(no category)"; }
    string fam = ""; try { fam = f.Symbol?.Family?.Name ?? ""; } catch { }
    bool ok = false; try { ok = f.Category != null && f.Category.Id == expectedId; } catch { }
    string key = (ok ? "OK   " : "WRONG") + "\t" + cat + "\t" + fam;
    if (!rows.ContainsKey(key)) { rows[key] = 0; tags[key] = ""; }
    rows[key] = rows[key] + 1;
    string tg = TagOf(f);
    if (!string.IsNullOrEmpty(tg) && tags[key].Length < 46)
    {
        if (tags[key].Length > 0) tags[key] += ", ";
        tags[key] += tg;
    }
}

sb.AppendLine("Matched " + matched.Count + " element(s); " + right + " already in the expected category, " + wrong.Count + " NOT.");
sb.AppendLine("STATUS | CATEGORY | FAMILY | COUNT | TAG SAMPLE");
foreach (var kv in rows.OrderBy(r => r.Key))
{
    var parts = kv.Key.Split('\t');
    sb.AppendLine(parts[0] + " | " + parts[1] + " | " + parts[2] + " | " + kv.Value + " | " + tags[kv.Key]);
}
sb.AppendLine("Returned " + elements.Count + " element(s) in `elements` (" + (onlyMismatches ? "mismatches only" : "every match") + ").");
if (matched.Count == 0)
    sb.AppendLine("Nothing matched — widen nameKeywords, or check the tag prefix against knowledge/glossary.md.");
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
