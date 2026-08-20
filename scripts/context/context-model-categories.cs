// ============================================================
// SCRIPT (context) — context-model-categories.cs
// PURPOSE: List model categories (the OST_ ones elements actually belong to), optionally narrowed by a
//          keyword. Read-only, safe to call anytime.
// LESSON (2026-07-14): an unfiltered dump of
// every model category is long and rarely what's actually needed — always try a keyword first; only
// leave it blank when a genuinely full list was asked for.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string categoryKeyword = null;   // e.g. "Duct" — leave null only if a full list was actually requested
// ---- END INPUTS ----

// Version-proof id value: ElementId.IntegerValue (pre-2024) vs .Value (2024+). Looked up once.
// A BuiltInCategory is an enum over that NUMBER, so the cast needs the number, not the ElementId.
var _idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");
Func<ElementId, long> IdValue = id => Convert.ToInt64(_idValueProp.GetValue(id));

var sb = new System.Text.StringBuilder();
var categories = Document.Settings.Categories.Cast<Category>()
    .Where(c => c.CategoryType == CategoryType.Model)
    .Where(c => string.IsNullOrEmpty(categoryKeyword) || c.Name.IndexOf(categoryKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
    .OrderBy(c => c.Name)
    .ToList();

sb.AppendLine($"Model categories matching {(string.IsNullOrEmpty(categoryKeyword) ? "(no keyword — full list)" : $"'{categoryKeyword}'")}: {categories.Count}");
foreach (var c in categories)
    sb.AppendLine($"  - {c.Name} (Id {c.Id}, BuiltInCategory {(BuiltInCategory)IdValue(c.Id)})");

return sb.ToString();
