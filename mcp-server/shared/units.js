// Millimetres <-> Revit's internal feet, as plain arithmetic — deliberately NOT the Revit unit API.
//
// WHY THIS FILE EXISTS. `DisplayUnitType.DUT_MILLIMETERS` was REMOVED from the Revit API after 2020.
// Measured against the real DLLs on 2026-08-24: the name appears 4 times in Revit 2020's RevitAPI.dll
// and 0 times in Revit 2024's and Revit 2027's. Because the bridge compiles the script it is sent,
// naming it is not a runtime warning — it is a hard compile error, so the tool fails on its very first
// call. `model_summary` and `move_elements` were dead on any Revit above 2020, and twelve more tools
// died the moment a mm filter was used.
//
// The fragment library was swept clean of exactly this on 2026-08-20 (see knowledge/brain-log.md and
// knowledge/revit-version-compatibility.md — 93 files). This server was missed, and carried eight of
// them until 2026-08-24, because tools/check-scripts.cmd only ever compiled scripts/*.cs and has never
// looked inside mcp-server/.
//
// THE REPLACEMENT NEEDS NO VERSION GUARD. Revit's internal length unit is decimal feet, and one foot
// is EXACTLY 304.8 mm by definition — not an approximation, a definition. So the conversion is
// arithmetic that no API release can deprecate, and one build of this server works on 2020 through
// 2027 and every version after. Same constant and same reasoning as scripts/lib/prelude.cs, which is
// why the whole fragment library compiles on all three installed Revits today.
//
// `#if` is not an option here for the same reason it is not one in the fragments: the bridge compiles
// a bare string with no REVIT20XX symbols defined.

export const MM_PER_FOOT = 304.8;

// C# expression turning a millimetre NUMBER into internal feet. Parenthesised so it is safe to drop
// into any larger expression.
export function mmToFtExpr(mm) {
  return `(${Number(mm) || 0} / ${MM_PER_FOOT})`;
}

// C# expression turning an internal-feet EXPRESSION (e.g. `parameter.AsDouble()`) into millimetres.
export function ftToMmExpr(feetExpr) {
  return `((${feetExpr}) * ${MM_PER_FOOT})`;
}
