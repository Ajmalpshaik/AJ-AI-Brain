// Structural regression test for every native MCP tool — no live Revit/bridge connection needed.
//
// Imports each tools/*.js module, registers it against a fake MCP server, then invokes every handler
// with a representative, schema-shaped argument object. None of this ever reaches a real bridge (no
// AJTools discovery file exists in CI/dev), so every call is expected to resolve to the "bridge not
// connected" error — the point isn't that call, it's proving the C# generation code in each handler
// (buildElementsClause, template strings, etc.) runs to completion without throwing first. That code
// path is exactly where a past session found a real bug (a corrupted NUL byte in bridge-connection.js)
// that `node --check` alone did not catch — see knowledge/brain-log.md, 2026-07-22.

import test from "node:test";
import assert from "node:assert/strict";

import { register as registerRunCsharp } from "../tools/run-csharp.js";
import { register as registerPing } from "../tools/ping.js";
import { register as registerModelSummary } from "../tools/model-summary.js";
import { register as registerListElements } from "../tools/list-elements.js";
import { register as registerCountElements } from "../tools/count-elements.js";
import { register as registerHideElements } from "../tools/hide-elements.js";
import { register as registerUnhideElements } from "../tools/unhide-elements.js";
import { register as registerIsolateElements } from "../tools/isolate-elements.js";
import { register as registerResetIsolation } from "../tools/reset-isolation.js";
import { register as registerSetColor } from "../tools/set-color.js";
import { register as registerResetGraphicOverrides } from "../tools/reset-graphic-overrides.js";
import { register as registerSetTransparency } from "../tools/set-transparency.js";
import { register as registerSelectElements } from "../tools/select-elements.js";
import { register as registerSetParameterValue } from "../tools/set-parameter-value.js";
import { register as registerReportParameters } from "../tools/report-parameters.js";
import { register as registerMoveElements } from "../tools/move-elements.js";
import { register as registerDeleteElements } from "../tools/delete-elements.js";

// One representative, schema-valid argument object per tool — enough to exercise every branch in each
// handler's own C#-building code (category resolution, optional fields, view targeting, etc.).
const SAMPLE_ARGS = {
  run_csharp: { code: '"smoke-test"' },
  ping: {},
  model_summary: { category: "ducts", parameter: "Height" },
  list_elements: { category: "Ducts" },
  count_elements: { category: "Ducts" },
  hide_elements: { category: "Ducts", permanent: false },
  unhide_elements: { category: "Ducts" },
  isolate_elements: { category: "Ducts" },
  reset_isolation: {},
  set_color: { category: "Ducts", r: 255, g: 0, b: 0 },
  reset_graphic_overrides: { category: "Ducts" },
  set_transparency: { category: "Ducts", percent: 50 },
  select_elements: { category: "Ducts" },
  set_parameter_value: { category: "Ducts", parameterNameToSet: "Comments", stringValue: "test" },
  report_parameters: { category: "Ducts", parameterNames: ["Mark"] },
  move_elements: { category: "Ducts", offsetXmm: 10, offsetYmm: 0, offsetZmm: 0 },
  delete_elements: { category: "Ducts", confirm: true },
};

const EXPECTED_TOOL_NAMES = Object.keys(SAMPLE_ARGS).sort();

function createFakeServer() {
  const registrations = [];
  return {
    registrations,
    tool(name, description, schema, handler) {
      registrations.push({ name, description, schema, handler });
    },
  };
}

test("every tool module registers exactly one well-formed tool", () => {
  const server = createFakeServer();
  for (const register of [
    registerRunCsharp, registerPing, registerModelSummary, registerListElements, registerCountElements,
    registerHideElements, registerUnhideElements, registerIsolateElements, registerResetIsolation,
    registerSetColor, registerResetGraphicOverrides, registerSetTransparency, registerSelectElements,
    registerSetParameterValue, registerReportParameters, registerMoveElements, registerDeleteElements,
  ]) {
    register(server);
  }

  assert.equal(server.registrations.length, 17, "expected exactly 17 registered tools");

  const names = server.registrations.map((r) => r.name).sort();
  assert.deepEqual(names, EXPECTED_TOOL_NAMES, "registered tool names drifted from this test's expectations");
  assert.equal(new Set(names).size, names.length, "duplicate tool name registered");

  for (const r of server.registrations) {
    assert.ok(r.description && r.description.length > 10, `${r.name}: description missing or too short`);
    assert.equal(typeof r.schema, "object", `${r.name}: schema must be an object`);
    assert.equal(typeof r.handler, "function", `${r.name}: handler must be a function`);
  }
});

test("every handler runs its C# generation to completion and fails gracefully with no bridge", async () => {
  const server = createFakeServer();
  for (const register of [
    registerRunCsharp, registerPing, registerModelSummary, registerListElements, registerCountElements,
    registerHideElements, registerUnhideElements, registerIsolateElements, registerResetIsolation,
    registerSetColor, registerResetGraphicOverrides, registerSetTransparency, registerSelectElements,
    registerSetParameterValue, registerReportParameters, registerMoveElements, registerDeleteElements,
  ]) {
    register(server);
  }

  for (const { name, handler } of server.registrations) {
    const args = SAMPLE_ARGS[name];
    assert.ok(args, `${name}: no sample args defined in this test — add one`);

    let result;
    try {
      result = await handler(args);
    } catch (err) {
      assert.fail(
        `${name}: handler threw synchronously instead of reaching the bridge call — likely a bug in ` +
          `its C#-generation code, not a bridge/connection issue: ${err.stack || err.message}`
      );
    }

    assert.ok(result && Array.isArray(result.content) && result.content[0]?.type === "text", `${name}: malformed tool result shape`);
    assert.equal(result.isError, true, `${name}: expected isError (no bridge is connected in this test env)`);
    assert.match(
      result.content[0].text,
      /bridge/i,
      `${name}: expected the "AJ AI Bridge is not connected" error, got: ${result.content[0].text}`
    );
  }
});
