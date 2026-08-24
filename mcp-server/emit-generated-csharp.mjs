// Writes out every distinct C# script this server can generate, so tools/check-scripts.cmd can
// compile it against every Revit on the PC — the same treatment scripts/*.cs has always had.
//
// THE GAP THIS CLOSES. check-scripts compiles the fragment library and nothing else. But roughly half
// the C# that reaches Revit in a normal session is not a fragment: it is built here, line by line, by
// tools/*.js. That half had never been compiled against any Revit. The result was
// `DisplayUnitType.DUT_MILLIMETERS`, removed from the API after 2020, sitting in eight places for
// months after the fragment library had been swept clean of it — with a green check-scripts run the
// whole time, because check-scripts was not looking here.
//
// BRANCHES, NOT TOOLS. Calling each tool once would not have caught that bug: three of the eight
// copies only appear when a mm filter is used, and one only on the numeric branch of
// set_parameter_value. So the cases below deliberately walk the code paths — every comparison mode,
// with and without a view, both branches of set_parameter_value — rather than every tool name once.
// A tool with no branches gets one case; the element filter gets seven.
//
// HOW THE CAPTURE WORKS. Handlers are called for real, with AJ_EMIT_CSHARP set, so the C# is grabbed
// inside callBridge at the one point every script passes through (see bridge-connection.js). Nothing
// is sent to Revit and no bridge is needed.
//
//   node emit-generated-csharp.mjs <output-dir>

import { mkdirSync, rmSync, readdirSync, renameSync } from "node:fs";
import path from "node:path";

const outDir = path.resolve(process.argv[2] || "generated-csharp");
rmSync(outDir, { recursive: true, force: true });
mkdirSync(outDir, { recursive: true });
process.env.AJ_EMIT_CSHARP = outDir;

const modules = {
  run_csharp: "./tools/run-csharp.js",
  run_fragment: "./tools/run-fragment.js",
  grayout: "./tools/grayout.js",
  session_start: "./tools/session_start.js",
  verify_connectivity: "./tools/verify_connectivity.js",
  report_length_by_size: "./tools/report_length_by_size.js",
  color_by_group: "./tools/color_by_group.js",
  model_summary: "./tools/model-summary.js",
  list_elements: "./tools/list-elements.js",
  count_elements: "./tools/count-elements.js",
  hide_elements: "./tools/hide-elements.js",
  unhide_elements: "./tools/unhide-elements.js",
  isolate_elements: "./tools/isolate-elements.js",
  reset_isolation: "./tools/reset-isolation.js",
  set_color: "./tools/set-color.js",
  reset_graphic_overrides: "./tools/reset-graphic-overrides.js",
  set_transparency: "./tools/set-transparency.js",
  select_elements: "./tools/select-elements.js",
  set_parameter_value: "./tools/set-parameter-value.js",
  report_parameters: "./tools/report-parameters.js",
  move_elements: "./tools/move-elements.js",
  delete_elements: "./tools/delete-elements.js",
};

// One entry per code path that can produce DIFFERENT C#, named for what it covers.
const CASES = [
  ["count_elements__by_category", "count_elements", { category: "Ducts" }],
  ["count_elements__by_family", "count_elements", { category: "Ducts", familyName: "Round Duct" }],
  ["count_elements__param_eq", "count_elements", { category: "Ducts", parameterName: "Diameter", comparison: "eq", valueMm: 300, toleranceMm: 5 }],
  ["count_elements__param_gte", "count_elements", { category: "Ducts", parameterName: "Diameter", comparison: "gte", valueMm: 300 }],
  ["count_elements__param_lte", "count_elements", { category: "Ducts", parameterName: "Diameter", comparison: "lte", valueMm: 300 }],
  ["count_elements__param_between", "count_elements", { category: "Ducts", parameterName: "Diameter", comparison: "between", valueMm: 200, valueMaxMm: 400 }],
  ["count_elements__by_ids", "count_elements", { elementIds: [918932, 918933] }],

  ["list_elements__default", "list_elements", { category: "Ducts" }],
  ["list_elements__capped", "list_elements", { category: "Ducts", maxRows: 5 }],

  ["model_summary__count_only", "model_summary", { category: "ducts" }],
  ["model_summary__with_parameter", "model_summary", { category: "ducts", parameter: "Height" }],

  ["hide_elements__active_view", "hide_elements", { category: "Ducts" }],
  ["hide_elements__named_view", "hide_elements", { category: "Ducts", targetViewId: 12345 }],
  ["unhide_elements", "unhide_elements", { category: "Ducts" }],
  ["isolate_elements", "isolate_elements", { category: "Ducts" }],
  ["reset_isolation__active_view", "reset_isolation", {}],
  ["reset_isolation__named_view", "reset_isolation", { targetViewId: 12345 }],

  ["set_color", "set_color", { category: "Ducts", r: 255, g: 0, b: 0 }],
  ["set_color__named_view", "set_color", { category: "Ducts", r: 0, g: 128, b: 255, targetViewId: 12345 }],
  ["reset_graphic_overrides", "reset_graphic_overrides", { category: "Ducts" }],
  ["set_transparency", "set_transparency", { category: "Ducts", percent: 50 }],
  ["select_elements", "select_elements", { category: "Ducts" }],

  ["set_parameter_value__string", "set_parameter_value", { category: "Ducts", parameterNameToSet: "Comments", stringValue: "test" }],
  ["set_parameter_value__numeric_mm", "set_parameter_value", { category: "Ducts", parameterNameToSet: "Offset", numericValueMm: 2700 }],
  ["report_parameters", "report_parameters", { category: "Ducts", parameterNames: ["Mark", "Comments"] }],
  ["move_elements", "move_elements", { category: "Ducts", offsetXmm: 100, offsetYmm: -50, offsetZmm: 250 }],
  ["delete_elements", "delete_elements", { category: "Ducts", confirm: true }],

  // Fragment-backed tools. The fragments themselves are already checked as scripts/*.cs; what is new
  // here is the COMPOSED result — prelude plus filter plus action, with the INPUTS rewritten.
  ["grayout", "grayout", { targetViewId: 12345 }],
  ["session_start", "session_start", {}],
  ["verify_connectivity", "verify_connectivity", { roomId: 918932, maxHops: 40 }],
  ["report_length_by_size", "report_length_by_size", { category: "Ducts" }],
  ["color_by_group", "color_by_group", { category: "Ducts", groupBy: "System Type" }],
  ["run_fragment__composed", "run_fragment", {
    describe: "Compile check the composed path",
    fragments: ["filter-by-category", "action-count-by-group"],
    inputs: { targetCategory: "OST_DuctCurves" },
  }],
];

const handlers = {};
for (const [name, modulePath] of Object.entries(modules)) {
  const { register } = await import(modulePath);
  register({ registerTool: (n, _config, handler) => { handlers[n] = handler; } });
}

let written = 0;
const problems = [];

for (const [label, tool, args] of CASES) {
  const handler = handlers[tool];
  if (!handler) {
    problems.push(label + ": no handler named " + tool);
    continue;
  }

  const before = new Set(readdirSync(outDir));
  try {
    await handler(args);
  } catch (err) {
    problems.push(label + ": handler threw — " + err.message);
    continue;
  }
  const fresh = readdirSync(outDir).filter((f) => !before.has(f));
  if (fresh.length !== 1) {
    problems.push(label + ": expected 1 captured script, got " + fresh.length);
    continue;
  }
  renameSync(path.join(outDir, fresh[0]), path.join(outDir, label + ".cs"));
  written++;
}

// Every tool must appear in at least one case, or a whole tool's C# goes unchecked in silence — the
// exact shape of the bug this file exists to prevent. run_csharp is the one exemption: its C# is
// whatever the caller pasted, so there is nothing of ours to compile.
const covered = new Set(CASES.map((entry) => entry[1]));
for (const name of Object.keys(handlers)) {
  if (name !== "run_csharp" && !covered.has(name)) {
    problems.push(name + ": no case — its C# would go unchecked");
  }
}

console.log(written + " generated script(s) written to " + outDir);
if (problems.length) {
  console.error("PROBLEMS:");
  for (const p of problems) console.error("  " + p);
  process.exit(1);
}
