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
import { register as registerRunFragment } from "../tools/run-fragment.js";
import { register as registerGrayout } from "../tools/grayout.js";
import { register as registerSessionStart } from "../tools/session_start.js";
import { register as registerVerifyConnectivity } from "../tools/verify_connectivity.js";
import { register as registerReportLengthBySize } from "../tools/report_length_by_size.js";
import { register as registerColorByGroup } from "../tools/color_by_group.js";
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

// Not a Revit tool, and deliberately NOT added to the lists below: the two tests there assert
// exactly 23 registrations and that every handler fails with a bridge error. search_brain never
// touches the bridge, so it gets its own test at the bottom of this file and the 23-tool guard
// stays meaningful.
import { register as registerSearchBrain } from "../brain-tools/search-brain.js";
import { register as registerSearchGraph } from "../brain-tools/search-graph.js";

// One representative, schema-valid argument object per tool — enough to exercise every branch in each
// handler's own C#-building code (category resolution, optional fields, view targeting, etc.).
const SAMPLE_ARGS = {
  run_csharp: { code: '"smoke-test"' },
  // A real PROVEN fragment with a real input, so this exercises resolution, the INPUTS rewrite and the
  // composition decision — not just the registration. preview is deliberately OFF: like every other
  // handler here, it must reach the bridge and fail gracefully there.
  run_fragment: {
    describe: "Smoke test the fragment runner",
    fragments: "filter-by-category",
    inputs: { targetCategory: "OST_DuctCurves" },
  },
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
  // The five fragment-backed tools. Each must reach the bridge like every other handler here, so
  // none of them sets preview: the point is proving the compose-and-send path runs to completion.
  grayout: {},
  session_start: {},
  verify_connectivity: {},
  report_length_by_size: { category: "Ducts" },
  color_by_group: { category: "Ducts", groupBy: "System Type" },
};

const EXPECTED_TOOL_NAMES = Object.keys(SAMPLE_ARGS).sort();

function createFakeServer() {
  const registrations = [];
  return {
    registrations,
    registerTool(name, config, handler) {
      registrations.push({
        name,
        description: config.description,
        schema: config.inputSchema,
        annotations: config.annotations,
        handler,
      });
    },
  };
}

test("every tool module registers exactly one well-formed tool", () => {
  const server = createFakeServer();
  for (const register of [
    registerRunCsharp, registerRunFragment, registerGrayout, registerSessionStart,
    registerVerifyConnectivity, registerReportLengthBySize, registerColorByGroup,
    registerPing, registerModelSummary, registerListElements, registerCountElements,
    registerHideElements, registerUnhideElements, registerIsolateElements, registerResetIsolation,
    registerSetColor, registerResetGraphicOverrides, registerSetTransparency, registerSelectElements,
    registerSetParameterValue, registerReportParameters, registerMoveElements, registerDeleteElements,
  ]) {
    register(server);
  }

  assert.equal(server.registrations.length, 23, "expected exactly 23 registered tools");

  const names = server.registrations.map((r) => r.name).sort();
  assert.deepEqual(names, EXPECTED_TOOL_NAMES, "registered tool names drifted from this test's expectations");
  assert.equal(new Set(names).size, names.length, "duplicate tool name registered");

  for (const r of server.registrations) {
    assert.ok(r.description && r.description.length > 10, `${r.name}: description missing or too short`);
    assert.equal(typeof r.schema, "object", `${r.name}: schema must be an object`);
    assert.equal(typeof r.handler, "function", `${r.name}: handler must be a function`);
  }
});

// Added 2026-08-24. Every tool shipped WITHOUT annotations until that day, which meant a client had
// no way to tell `count_elements` from `delete_elements` and had to treat both the same. Declaring
// them is only half the fix — the other half is this test, so they cannot quietly go missing again.
//
// It deliberately checks the DANGEROUS end by name rather than only "an annotation exists". A blanket
// "everything is labelled" assertion would still pass if delete_elements were relabelled read-only,
// and that is the single mislabelling that actually costs something.
test("every tool declares what it is allowed to do, and the risky ones say so", () => {
  const server = createFakeServer();
  for (const register of [
    registerRunCsharp, registerRunFragment, registerGrayout, registerSessionStart,
    registerVerifyConnectivity, registerReportLengthBySize, registerColorByGroup,
    registerPing, registerModelSummary, registerListElements, registerCountElements,
    registerHideElements, registerUnhideElements, registerIsolateElements, registerResetIsolation,
    registerSetColor, registerResetGraphicOverrides, registerSetTransparency, registerSelectElements,
    registerSetParameterValue, registerReportParameters, registerMoveElements, registerDeleteElements,
  ]) {
    register(server);
  }

  for (const r of server.registrations) {
    assert.equal(typeof r.annotations, "object", `${r.name}: no annotations — add it to TOOL_SAFETY`);
    for (const hint of ["readOnlyHint", "destructiveHint", "idempotentHint", "openWorldHint"]) {
      assert.equal(typeof r.annotations[hint], "boolean", `${r.name}: ${hint} must be declared explicitly`);
    }
    assert.ok(
      !(r.annotations.readOnlyHint && r.annotations.destructiveHint),
      `${r.name}: cannot be both read-only and destructive`
    );
  }

  const byName = Object.fromEntries(server.registrations.map((r) => [r.name, r.annotations]));

  for (const name of ["delete_elements", "move_elements", "set_parameter_value", "run_csharp", "run_fragment"]) {
    assert.equal(byName[name].destructiveHint, true, `${name} changes the model — it must be flagged destructive`);
    assert.equal(byName[name].readOnlyHint, false, `${name} must never be flagged read-only`);
  }

  for (const name of ["ping", "count_elements", "list_elements", "model_summary", "report_parameters"]) {
    assert.equal(byName[name].readOnlyHint, true, `${name} only reads — flagging it otherwise costs a needless prompt`);
    assert.equal(byName[name].destructiveHint, false, `${name} changes nothing`);
  }

  // A second move is twice the distance, so it is the one write here that is genuinely not idempotent.
  assert.equal(byName.move_elements.idempotentHint, false, "calling move_elements twice moves twice as far");

  // Only the two arbitrary-C# tools are open-world; everything else is bounded by its own schema.
  const openWorld = server.registrations.filter((r) => r.annotations.openWorldHint).map((r) => r.name).sort();
  assert.deepEqual(openWorld, ["run_csharp", "run_fragment"], "only the arbitrary-C# tools are open-world");
});

// SAFETY GATE, added 2026-08-13 after a near miss.
//
// The test below invokes EVERY handler with SAMPLE_ARGS, and those args include
// `delete_elements: { category: "Ducts", confirm: true }`, plus move_elements,
// set_parameter_value and the hide/colour tools. Its whole premise is the sentence at the
// top of this file: "no live Revit/bridge connection needed". That premise is not checked
// anywhere, and it stops being true the moment Revit is open with the bridge connected -
// which is exactly when someone is most likely to run `npm test`.
//
// It happened: this suite was run during a live session on 2026-08-13. It survived only
// because the active document was an unrelated blank `Project1` with zero ducts, so every
// destructive call matched nothing. Had the real model been active, it would have deleted
// 354 ducts, with `confirm: true` already supplied.
//
// So the premise is now enforced instead of assumed. A read-only `ping` decides it.
//
// UPDATED 2026-08-20, because the gate FAILED OPEN and this suite ran against a live Revit.
// The old rule was "ping errored -> not live". But an error is not proof the bridge is dead: a bridge
// that is merely BUSY answers "Another script is still running", and a bridge mid-reconnect answers
// something else again. Both were read as "no bridge", which is the one way this gate must never fail.
// Caught by the audit log, which recorded the probe reaching Revit and being rejected by Revit's OWN
// busy-guard - the last line of defence, not this one.
//
// So the logic is inverted: only an explicit not-connected / pipe-missing answer counts as proof the
// bridge is down. Anything else - any other error, a thrown exception, an unreadable result - is treated
// as LIVE and skips the destructive suite. A false skip costs one test run; a false "not live" costs
// 354 ducts.
const NOT_LIVE_PROOF = /not connected|pipe not found|Reconnect from the AJ AI pane/i;

async function bridgeIsLive() {
  try {
    const probe = createFakeServer();
    registerPing(probe);
    const result = await probe.registrations[0].handler({});
    if (result?.isError === false) return true;
    return !NOT_LIVE_PROOF.test(JSON.stringify(result ?? ""));
  } catch (err) {
    return !NOT_LIVE_PROOF.test(String((err && err.message) || err || ""));
  }
}

test("every handler runs its C# generation to completion and fails gracefully with no bridge", async (t) => {
  if (await bridgeIsLive()) {
    t.skip(
      "SKIPPED: the AJ AI Bridge is CONNECTED. This test invokes delete_elements with " +
        "confirm:true against the active document. Close the bridge (or Revit) and re-run."
    );
    return;
  }

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

test("set_parameter_value rejects both or neither of stringValue/numericValueMm before touching the bridge", async () => {
  const server = createFakeServer();
  registerSetParameterValue(server);
  const { handler } = server.registrations[0];

  for (const badArgs of [
    { category: "Ducts", parameterNameToSet: "Comments" }, // neither
    { category: "Ducts", parameterNameToSet: "Comments", stringValue: "x", numericValueMm: 1 }, // both
  ]) {
    const result = await handler(badArgs);
    assert.equal(result.isError, true, `expected a validation error for ${JSON.stringify(badArgs)}`);
    assert.match(result.content[0].text, /exactly one of/i);
  }
});

test("search_brain registers well-formed and returns without a bridge", async () => {
  const server = createFakeServer();
  registerSearchBrain(server);

  assert.equal(server.registrations.length, 1, "search-brain.js must register exactly one tool");

  const { name, description, schema, handler } = server.registrations[0];
  assert.equal(name, "search_brain");
  assert.ok(description && description.length > 10, "description missing or too short");
  assert.deepEqual(Object.keys(schema).sort(), ["area", "query", "top"]);
  assert.equal(typeof handler, "function");

  // The real invocation. Slower than the rest of this file (~10 s, it loads the embedding model),
  // but it is the only thing that proves the Python hand-off actually works end to end.
  //
  // Two outcomes are both correct, because semantic-index/venv/ is gitignored: on a machine that
  // has it, real results come back; on a fresh clone it has not been created yet and the handler
  // must say so cleanly rather than throw. Asserting only one of these would make `npm test` fail
  // on any fresh checkout, which is a worse bug than the one it would be guarding against.
  const result = await handler({ query: "how do I undo a mistake", top: 2 });

  assert.ok(
    result && Array.isArray(result.content) && result.content[0]?.type === "text",
    "malformed tool result shape"
  );

  if (result.isError) {
    assert.match(
      result.content[0].text,
      /python|venv/i,
      `the only acceptable failure here is a missing venv, got: ${result.content[0].text}`
    );
  } else {
    assert.match(
      result.content[0].text,
      /QUESTION:/,
      "expected the search's own output header in the results"
    );
  }
});

test("search_graph registers well-formed and returns without a bridge", async () => {
  const server = createFakeServer();
  registerSearchGraph(server);

  assert.equal(server.registrations.length, 1, "search-graph.js must register exactly one tool");

  const { name, description, schema, handler } = server.registrations[0];
  assert.equal(name, "search_graph");
  assert.ok(description && description.length > 10, "description missing or too short");
  assert.deepEqual(Object.keys(schema).sort(), ["budget", "mode", "nodeA", "nodeB", "question"]);
  assert.equal(typeof handler, "function");

  // Three outcomes are all correct, and asserting only one would make `npm test` fail on a
  // machine that is perfectly healthy: graphify-out/ is git-ignored so a fresh clone has no
  // graph, and `graphify` is a user-level CLI that may not be installed at all. What must
  // never happen is a throw, or a silent success carrying no traversal.
  const result = await handler({ question: "how do ducts connect to air terminals", budget: 400 });

  assert.ok(
    result && Array.isArray(result.content) && result.content[0]?.type === "text",
    "malformed tool result shape"
  );

  if (result.isError) {
    assert.match(
      result.content[0].text,
      /graph|graphify/i,
      `the only acceptable failures are a missing graph or a missing graphify CLI, got: ${result.content[0].text}`
    );
  } else {
    assert.match(
      result.content[0].text,
      /Traversal:|NODE /,
      "expected the graph traversal's own output in the results"
    );
  }
});

test("search_graph rejects mode 'path' without both endpoints", async () => {
  const server = createFakeServer();
  registerSearchGraph(server);
  const { handler } = server.registrations[0];

  const result = await handler({ question: "x", mode: "path", nodeA: "OnlyOne" });
  assert.equal(result.isError, true, "expected a validation error when nodeB is missing");
  assert.match(result.content[0].text, /nodeA and nodeB/i);
});
