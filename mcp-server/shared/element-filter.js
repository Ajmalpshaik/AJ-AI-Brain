// Shared C# generation for the native tool set — one element-resolution clause (by explicit Ids, or
// by category + optional family/numeric-parameter filter) feeds every action tool in tools/. Mirrors
// the filter+action fragment architecture in scripts/README.md.

import { z } from "zod";
import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "./tool-result.js";
import { mmToFtExpr } from "./units.js";
import { idValuePreamble, ID_VALUE_CALL, makeIdExpr } from "./element-id.js";

export function cs(value) {
  return JSON.stringify(value ?? null);
}

// Produces C# that declares `List<Element> elements` and `var sb = new System.Text.StringBuilder();`.
// `elementIds`, if given, takes priority over the category filter (caller already knows exactly which
// elements it wants — e.g. chained from a prior list_elements call).
export function buildElementsClause(input) {
  const { elementIds, category, familyName, parameterName, comparison, valueMm, valueMaxMm, toleranceMm } = input;

  if (elementIds && elementIds.length > 0) {
    const idArray = elementIds.map((id) => makeIdExpr(id)).join(", ");
    return [
      idValuePreamble(),
      `var __ids = new List<ElementId> { ${idArray} };`,
      `List<Element> elements = __ids.Select(id => Document.GetElement(id)).Where(e => e != null).ToList();`,
      `var sb = new System.Text.StringBuilder();`,
      `sb.AppendLine("Resolved " + elements.Count + " of " + __ids.Count + " given Element Id(s).");`,
    ].join("\n");
  }

  const lines = [
    idValuePreamble(),
    `Category __category = null;`,
    `foreach (Category __cat in Document.Settings.Categories) { if (__cat.Name.Equals(${cs(category)}, StringComparison.OrdinalIgnoreCase)) { __category = __cat; break; } }`,
    `List<Element> elements = new List<Element>();`,
    `var sb = new System.Text.StringBuilder();`,
    `if (__category == null)`,
    `{`,
    `    sb.AppendLine("Category '" + ${cs(category)} + "' not found — check the exact Revit category display name.");`,
    `}`,
    `else`,
    `{`,
    `    elements = new FilteredElementCollector(Document).OfCategoryId(__category.Id).WhereElementIsNotElementType().ToList();`,
  ];

  if (familyName) {
    lines.push(
      `    elements = elements.Where(e => (e as FamilyInstance)?.Symbol?.Family?.Name?.Equals(${cs(familyName)}, StringComparison.OrdinalIgnoreCase) == true).ToList();`
    );
  }

  if (parameterName) {
    let comparisonExpr;
    switch (comparison) {
      case "gte":
        comparisonExpr = "v >= __valueFt";
        break;
      case "lte":
        comparisonExpr = "v <= __valueFt";
        break;
      case "between":
        comparisonExpr = "v >= __valueFt && v <= __valueMaxFt";
        break;
      default:
        comparisonExpr = "Math.Abs(v - __valueFt) <= __toleranceFt";
    }
    lines.push(
      `    double __valueFt = ${mmToFtExpr(valueMm)};`,
      `    double __toleranceFt = ${mmToFtExpr(Number(toleranceMm) || 1)};`,
      `    double __valueMaxFt = ${mmToFtExpr(valueMaxMm)};`,
      `    elements = elements.Where(e => { var p = e.LookupParameter(${cs(parameterName)}); if (p == null || p.StorageType != StorageType.Double) return false; double v = p.AsDouble(); return ${comparisonExpr}; }).ToList();`
    );
  }

  lines.push(
    `    sb.AppendLine("Matched " + elements.Count + " element(s) in '" + __category.Name + "'.");`,
    `}`
  );
  return lines.join("\n");
}

// `standalone` matters because the id helpers can only be declared ONCE per script. Every tool that
// takes a view also resolves elements first, so buildElementsClause has already emitted the preamble
// and this must not repeat it. reset_isolation is the one tool that takes a view and nothing else —
// it passes standalone:true and gets its own copy.
export function buildViewClause(targetViewId, { standalone = false } = {}) {
  const resolve = targetViewId
    ? `View view = Document.GetElement(${makeIdExpr(targetViewId)}) as View;`
    : `View view = Document.ActiveView;`;
  return standalone && targetViewId ? [idValuePreamble(), resolve].join("\n") : resolve;
}

// Shared zod fields for the element-resolution input every element-targeting tool accepts.
export const filterFields = {
  elementIds: z
    .array(z.number())
    .optional()
    .describe("Exact Element Ids to target directly — takes priority over category/filter if given."),
  category: z
    .string()
    .optional()
    .describe("Revit category display name, e.g. 'Ducts', 'Walls', 'Air Terminals'. Required unless elementIds is given."),
  familyName: z.string().optional().describe("Narrow to one family within the category."),
  parameterName: z.string().optional().describe("Numeric parameter to filter by, e.g. 'Height', 'Diameter'."),
  comparison: z.enum(["eq", "gte", "lte", "between"]).optional().describe("Defaults to 'eq' if parameterName is set."),
  valueMm: z.number().optional().describe("Comparison value in mm."),
  valueMaxMm: z.number().optional().describe("Upper bound in mm, only used when comparison is 'between'."),
  toleranceMm: z.number().optional().describe("Tolerance in mm, only used when comparison is 'eq'. Defaults to 1mm."),
};

export const viewField = {
  targetViewId: z
    .number()
    .optional()
    .describe("Element Id of the view to target. Omit to use the active view. Can target any view, not just what's on screen."),
};

export async function runGenerated(script, allowDestructive) {
  try {
    const result = await callBridge(script, !!allowDestructive);
    return asToolResult(result);
  } catch (err) {
    return asToolResult({ success: false, error: err.message });
  }
}
