// ============================================================
// FRAGMENT (action) — action-rename-phase.cs
// PURPOSE: Rename one or more existing project Phases, old name -> new name.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
// CONFIRMED IMPOSSIBLE on Revit 2020 — LIVE-CHECKED 2026-07-22: the original `phase.Name = newName` throws
// "This element does not support assignment of a user-specified name." Investigated further: Phase's
// "Name" instance parameter is ReadOnly=true, and so is the dedicated `BuiltInParameter.PHASE_NAME` — both
// confirmed via a real Phase's live Parameters collection. There is no writable Name field for Phase
// anywhere in the public API on this Revit version (the only writable Phase field found was Sequence
// Number, which only reorders, doesn't rename). This is UI-only: Manage tab > Phases > double-click the
// name cell in the Phasing dialog. Same capability class as action-create-phase.cs (also confirmed
// impossible) — Phases overall have almost no write API on Revit 2020 beyond assigning ELEMENTS to a
// phase (PHASE_CREATED/PHASE_DEMOLISHED, which work fine — see action-set-element-phase.cs).
// This fragment now reports the limitation instead of throwing.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
(string oldName, string newName)[] renames = { ("Existing", "Phase 0 - Existing") };
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var skipped = new List<string>();

foreach (var (oldName, newName) in renames)
{
    var phase = Document.Phases.Cast<Phase>().FirstOrDefault(ph => ph.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
    if (phase == null) { skipped.Add($"{oldName} (not found)"); continue; }
    skipped.Add($"{oldName} -> {newName} (Revit's public API cannot rename a Phase on this version — Name is read-only, no writable field exists anywhere. Rename manually: Manage > Phases.)");
}

sb.AppendLine($"Renamed 0 phase(s) — see notes.");
if (skipped.Count > 0) sb.AppendLine("Notes: " + string.Join("; ", skipped));
return sb.ToString();
