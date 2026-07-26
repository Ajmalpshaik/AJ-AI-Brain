// ============================================================
// FRAGMENT (creator) — create-scope-box.cs
// PURPOSE: Create one or more Scope Boxes from mm box corners.
// PRODUCES: elements (List<Element>, the newly created Scope Box(es) — EMPTY on this Revit version, see
//           CONFIRMED IMPOSSIBLE note), sb (StringBuilder, summary)
// NOT STANDALONE — see scripts/README.md for how to compose. A "creator" fills the same role as a filter
//          — it produces `elements` — so action-assign-scope-box-to-view.cs can chain onto it directly.
// CONFIRMED IMPOSSIBLE on Revit 2020 — LIVE-CHECKED 2026-07-22: the original fragment called
// `Document.Create.NewScopeBox`, which does not exist. Reflecting every method on `Document.Create`
// (Autodesk.Revit.Creation.Document) finds no method with "Scope", "Volume", or "Box" in the name at all.
// Scanning the ENTIRE RevitAPI assembly for any method or type named `ScopeBox`/`VolumeOfInterest`
// (the OST_VolumeOfInterest category's element type) turns up nothing but a few unrelated
// HideScopeBox(es) print/export flags and failure-definition enum entries — there is no public class for
// a Scope Box element and no public creation method anywhere. This is not a wrong-method-name bug to
// patch; Revit 2020's public API has no way to create a Scope Box. It is UI-only: View tab > Scope Box,
// then draw the rectangle by clicking two points in a plan view.
// This fragment now reports that limitation and produces an empty `elements` list rather than throwing a
// compile error (which would also break any composed script that chains action-assign-scope-box-to-
// view.cs onto it). If you need a Scope Box for testing or for a real project, create it manually first,
// then use filter-by-scope-box.cs to pick it up by name.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
List<(double minXMm, double minYMm, double minZMm, double maxXMm, double maxYMm, double maxZMm, string name)> boxesToCreate = new List<(double, double, double, double, double, double, string)>
{
    (0, 0, 0, 10000, 10000, 3000, "Scope Box 1")
};
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var elements = new List<Element>();

sb.AppendLine($"Cannot create {boxesToCreate.Count} scope box(es) — Revit's public API has no Scope Box creation method on this Revit version (confirmed by reflection, see file header). Create scope box(es) manually via View tab > Scope Box, then use filters/by-view-and-sheet/filter-by-scope-box.cs to reference them by name.");
// ---- continue with an action fragment below (e.g. action-assign-scope-box-to-view.cs), or add
//      return sb.ToString(); to stop here ----
