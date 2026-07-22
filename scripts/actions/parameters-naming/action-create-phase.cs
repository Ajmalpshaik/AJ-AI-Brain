// ============================================================
// FRAGMENT (action) — action-create-phase.cs
// PURPOSE: Create one or more new project Phases, each appended in order after whatever the current
//          last Phase is. Skips any name that already exists rather than creating a duplicate.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — a Phase isn't picked by a filter, it's a
//          whole-document naming operation, self-contained (declares its own `sb`, ends with its own
//          `return`).
// CONFIRMED IMPOSSIBLE on Revit 2020 — LIVE-CHECKED 2026-07-22: the original fragment called
// `Document.Phases.Insert(lastPhase)`, which doesn't even compile (`PhaseArray.Insert` takes 2 args —
// `(Phase item, int index)` — not 1). Fixing the argument count doesn't help either: empirically tested
// `Document.Phases.Append(...)` inside a real transaction and it throws `InvalidOperationException:
// Collection is read-only` at runtime. `Document.Phases.IsReadOnly` reads `true`. An exhaustive scan of
// every method in the RevitAPI assembly for anything Phase-related with Create/New/Add in the name (and a
// direct check of Document.Create for any Phase method) turned up nothing that creates a Phase. Revit
// 2020's public API cannot create a new Phase at all — it's UI-only: Manage tab > Phases > right-click a
// phase header in the Phasing dialog > Insert Before/After.
// This fragment now reports that limitation instead of throwing a compile error, so a composed script
// that chains something onto it still runs cleanly.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string[] newPhaseNames = { "Phase 1" };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

sb.AppendLine($"Cannot create {newPhaseNames.Length} phase(s) ({string.Join(", ", newPhaseNames)}) — Document.Phases is read-only on this Revit version (confirmed: Append()/Insert() both throw \"Collection is read-only\" at runtime) and no other Phase-creation API exists anywhere in RevitAPI.dll. Create phase(s) manually via Manage tab > Phases > Insert Before/After, then use action-rename-phase.cs to rename or action-set-element-phase.cs to assign elements to it.");
return sb.ToString();
