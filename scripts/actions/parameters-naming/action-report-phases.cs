// ============================================================
// FRAGMENT (action) — action-report-phases.cs
// PURPOSE: List every project Phase in order — name, Element Id, and position. Read-only. The missing
//          discovery step for action-rename-phase.cs/action-delete-phase.cs/action-set-element-phase.cs,
//          which all need an exact existing phase name to work from.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// ============================================================

var sb = new System.Text.StringBuilder();
var phases = Document.Phases.Cast<Phase>().ToList();

sb.AppendLine($"{phases.Count} phase(s), in order:");
for (int i = 0; i < phases.Count; i++)
{
    sb.AppendLine($"  {i + 1}. '{phases[i].Name}' (Id {phases[i].Id})");
}
return sb.ToString();
