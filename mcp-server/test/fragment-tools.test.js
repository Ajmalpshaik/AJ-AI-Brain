// The five fragment-backed native tools — grayout, session_start, verify_connectivity,
// report_length_by_size, color_by_group.
//
// THE ONE RULE THEY ALL SHARE, and the reason they get their own test file: none of them may carry
// its own copy of a fragment's C#. The moment one does, there are two versions of a proven file and
// one will drift — which is the problem run_fragment was built to end. So every test here asserts
// against the composed output of the REAL fragment on disk, not against a fixed string. If someone
// later inlines the C# into a tool "to make it faster", these stop matching.
//
// All run in preview, so none needs a live Revit.

import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { register as registerGrayout } from "../tools/grayout.js";
import { register as registerSessionStart } from "../tools/session_start.js";
import { register as registerVerifyConnectivity } from "../tools/verify_connectivity.js";
import { register as registerReportLengthBySize } from "../tools/report_length_by_size.js";
import { register as registerColorByGroup } from "../tools/color_by_group.js";
import { z } from "zod";
import { inputsBlockRange } from "../../tools/fragment-lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const scripts = path.join(here, "..", "..", "scripts");

function toolFrom(register) {
  let handler, name, schema;
  register({ tool: (n, d, s, h) => { name = n; schema = s; handler = h; } });
  return {
    name,
    schema,
    run: async (args) => JSON.parse((await handler(args)).content[0].text),
    // The real MCP server validates against the declared schema before the handler ever runs; this
    // fake one does not, so a test about validation has to do it the same way the server would.
    validate: (args) => z.object(schema).safeParse(args),
  };
}

// Everything outside the INPUTS block must arrive exactly as it sits on disk.
function assertBodyUnchanged(code, relPath) {
  const disk = fs.readFileSync(path.join(scripts, relPath), "utf8");
  const strip = (src) => {
    const r = inputsBlockRange(src);
    return r ? src.slice(0, r.start) + src.slice(r.end) : src;
  };
  assert.ok(
    code.includes(strip(disk).trim().split("\n").slice(1, 6).join("\n")),
    `${relPath}: the tool altered the fragment outside its INPUTS block`
  );
}

test("grayout sets only the view and leaves all 32 standard values untouched", async () => {
  const { name, run } = toolFrom(registerGrayout);
  assert.equal(name, "grayout");

  const out = await run({ targetViewId: 123456, preview: true });
  assert.equal(out.success, true);

  const f = out.composition.fragments[0];
  assert.equal(f.fragment, "recipes/mep-grayout.cs");
  assert.deepEqual(f.filledIn, ["targetViewIdInt = 123456"], "only the view may be set by this tool");
  assert.equal(f.leftAtFileValue.length, 32, "the other 32 ARE the standard — they must not be touched");
  assert.match(out.code, /int\? targetViewIdInt = 123456;/);
  // Spot-check two real standard values survive verbatim.
  assert.match(out.code, /byte bgLineR = 150, bgLineG = 150, bgLineB = 150;/);
  assert.match(out.code, /byte floorR\s+= 240, floorG\s+= 240, floorB\s+= 240;/);
  assertBodyUnchanged(out.code, "recipes/mep-grayout.cs");
});

test("grayout with no view given touches nothing at all", async () => {
  const { run } = toolFrom(registerGrayout);
  const out = await run({ preview: true });
  assert.deepEqual(out.composition.fragments[0].filledIn, [], "an omitted optional must not write `undefined`");
  assert.match(out.code, /int\? targetViewIdInt = null;/);
});

test("session_start takes no inputs and admits it is not yet proven", async () => {
  const { name, run } = toolFrom(registerSessionStart);
  assert.equal(name, "session_start");

  const out = await run({ preview: true });
  assert.equal(out.success, true);
  assert.equal(out.composition.fragments[0].fragment, "context/context-session-start.cs");
  assert.deepEqual(out.composition.fragments[0].filledIn, []);
  assert.ok(
    (out.composition.warnings || []).some((w) => /Not proven against a real model/.test(w)),
    "an unproven fragment must say so THROUGH the native tool, not only through run_fragment"
  );
});

test("verify_connectivity passes its two inputs and keeps the proven method", async () => {
  const { name, run } = toolFrom(registerVerifyConnectivity);
  assert.equal(name, "verify_connectivity");

  const out = await run({ roomId: 918932, maxHops: 40, preview: true });
  assert.equal(out.success, true);
  assert.match(out.code, /roomId = new ElementId\(918932\)/);
  assert.match(out.code, /maxHops = 40/);
  assertBodyUnchanged(out.code, "recipes/verify-duct-connectivity.cs");

  const bare = await toolFrom(registerVerifyConnectivity).run({ preview: true });
  assert.equal(bare.composition.fragments[0].leftAtFileValue.length, 2, "both omitted inputs keep the file's values");
});

test("report_length_by_size composes the display-name filter with the reporter", async () => {
  const { name, run } = toolFrom(registerReportLengthBySize);
  assert.equal(name, "report_length_by_size");

  const out = await run({ category: "Ducts", preview: true });
  assert.equal(out.success, true);
  assert.deepEqual(
    out.composition.fragments.map((f) => f.fragment),
    ["filters/by-identity/filter-by-category-name.cs", "actions/reporting/action-report-length-by-size.cs"],
    "filter first, then the action — scripts/README.md's composition order"
  );
  assert.match(out.code, /categoryName = "Ducts"/);
  assert.equal(out.composition.returnAppended, true, "a filter+action composition needs the single return");
  assert.equal(out.code.split("\n").filter((l) => /^return\b/.test(l)).length, 1);
});

test("color_by_group defaults to a REPEATABLE colour mode", async () => {
  const { name, run } = toolFrom(registerColorByGroup);
  assert.equal(name, "color_by_group");

  const out = await run({ category: "Ducts", groupBy: "System Type", preview: true });
  assert.equal(out.success, true);
  assert.match(out.code, /colorMode = "palette"/,
    "random/pastel/neon pick a new hue each run, so a chart could never reuse the model's colours");
  assert.match(out.code, /groupByParameterName = "System Type"/);

  const neon = await toolFrom(registerColorByGroup).run({ category: "Ducts", groupBy: "Level", colorMode: "neon", preview: true });
  assert.match(neon.code, /colorMode = "neon"/, "but an explicit choice is still honoured");
});

test("the declared schemas reject bad input before Revit is involved", () => {
  const colorTool = toolFrom(registerColorByGroup);
  assert.equal(colorTool.validate({ category: "Ducts", groupBy: "Level", colorMode: "rainbow" }).success, false,
    "an invented colour mode must be refused");
  assert.equal(colorTool.validate({ groupBy: "Level" }).success, false, "category is required");
  assert.equal(colorTool.validate({ category: "Ducts", groupBy: "Level" }).success, true);

  const lengthTool = toolFrom(registerReportLengthBySize);
  assert.equal(lengthTool.validate({}).success, false, "category is required");
  assert.equal(lengthTool.validate({ category: "Pipes" }).success, true);

  // The two read-only tools must be callable with no arguments at all.
  assert.equal(toolFrom(registerSessionStart).validate({}).success, true);
  assert.equal(toolFrom(registerGrayout).validate({}).success, true);
  assert.equal(toolFrom(registerGrayout).validate({ targetViewId: "not a number" }).success, false);
});
