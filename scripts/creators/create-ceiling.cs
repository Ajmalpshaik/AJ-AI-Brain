// ============================================================
// FRAGMENT (creator) — create-ceiling.cs
// PURPOSE: Create a Ceiling from a boundary — CONFIRMED IMPOSSIBLE on Revit 2020. The `Ceiling.Create`
//          API only exists from Revit 2022; on 2020 ceilings are UI-only (Architecture tab > Ceiling).
//          This fragment exists so the next session finds the recorded answer instead of a compile error —
//          same convention as create-scope-box.cs.
// PRODUCES: elements (empty List<Element>), sb (the explanation)
// NOT STANDALONE — see scripts/README.md for how to compose.
// NOTE: recipes/ray-trace-to-ceiling.cs WORKS with ceilings that already exist — only creation is blocked.
// ============================================================

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

sb.AppendLine("Ceiling creation is IMPOSSIBLE via the API on Revit 2020 — Ceiling.Create only exists from Revit 2022.");
sb.AppendLine("Ask the user to draw the ceiling manually: Architecture tab > Ceiling (sketch or automatic).");
sb.AppendLine("Working with EXISTING ceilings is fine — e.g. recipes/ray-trace-to-ceiling.cs, or filter-by-category.cs on OST_Ceilings.");
// ---- continue with an action fragment below, or add return sb.ToString(); to stop here ----
