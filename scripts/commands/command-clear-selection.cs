// ============================================================
// SCRIPT: command-clear-selection.cs
// PURPOSE: Clear the active Revit selection — the reverse of action-select-elements.cs's "set" mode with
//          an empty list, split out as its own one-line command since clearing comes up on its own often
//          enough (e.g. before a fresh filter+action run, so a leftover selection doesn't confuse the
//          next screenshot/step) not to need composing a filter first.
// ============================================================

var sb = new System.Text.StringBuilder();
int previousCount = UIDocument.Selection.GetElementIds().Count;
UIDocument.Selection.SetElementIds(new List<ElementId>());
sb.AppendLine($"Selection cleared (was {previousCount} element(s)).");
return sb.ToString();
