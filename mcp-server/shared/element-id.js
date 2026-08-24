// Element Ids that survive every Revit version — the SILENT half of the 2020-vs-2024+ split.
//
// TWO FAILURES, AND THIS IS THE DANGEROUS ONE. The unit API (see units.js) fails LOUD: the name is
// gone, the script will not compile, the tool reports an error on its first call and Ajmal knows
// immediately. Element Ids fail SILENT. `ElementId.IntegerValue` and `new ElementId(int)` still exist
// on Revit 2024 and 2027 (measured 2026-08-24: 8 occurrences in each RevitAPI.dll), so everything
// compiles and everything runs — until an id exceeds what a 32-bit int can hold. Revit went to 64-bit
// ids in 2024. A small test model passes; a real federated project model throws or truncates. That is
// the worst shape a bug can have here, because testing does not reveal it.
//
// knowledge/revit-version-compatibility.md measured 168 fragment files carrying this. The fix used
// there needs no `#if` and no per-version build, and it is the same one used here: ask the live
// ElementId type which member it actually has, once, and capture it.
//
// COST. The property and the constructor are each looked up ONE time per script, then captured in a
// closure — so a run over 5,000 elements costs 5,000 field reads, not 5,000 reflection lookups.
//
// WHY REFLECTION RATHER THAN JUST USING `long` EVERYWHERE. `ElementId(long)` does not exist on Revit
// 2020, and `ElementId.Value` does not either. Naming either one directly would fix 2024+ by breaking
// 2020 — trading one version for another, which is exactly what Ajmal asked not to happen. Reflection
// names neither at compile time, so one script is correct on all of them.

// C# that declares `__IdValue` (ElementId -> long) and `__MakeId` (long -> ElementId). Emit this once,
// at the top of a generated script, before anything that uses either.
export function idValuePreamble() {
  return [
    `var __idValueProp = typeof(ElementId).GetProperty("Value") ?? typeof(ElementId).GetProperty("IntegerValue");`,
    `Func<ElementId, long> __IdValue = __eid => Convert.ToInt64(__idValueProp.GetValue(__eid));`,
    `var __idCtor = typeof(ElementId).GetConstructor(new Type[] { typeof(long) }) ?? typeof(ElementId).GetConstructor(new Type[] { typeof(int) });`,
    `bool __idCtorTakesLong = __idCtor.GetParameters()[0].ParameterType == typeof(long);`,
    `Func<long, ElementId> __MakeId = __v => (ElementId)__idCtor.Invoke(new object[] { __idCtorTakesLong ? (object)__v : (object)(int)__v });`,
  ].join("\n");
}

// The C# name of the id-reading helper the preamble declares. Use as `__IdValue(e.Id)` in place of
// `e.Id.IntegerValue`.
export const ID_VALUE_CALL = "__IdValue";

// C# expression building an ElementId from a number, in place of `new ElementId(n)`.
export function makeIdExpr(id) {
  return `__MakeId(${Number(id)})`;
}
