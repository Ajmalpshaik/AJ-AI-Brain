// run_fragment — the rules it enforces, and the one invariant that matters.
//
// Nothing here needs a live Revit: every test runs in `preview` mode, which composes the script and
// stops. That is deliberate — the whole point of this tool is that the checkable half is checkable
// WITHOUT Revit, so its tests should not need one either.
//
// THE INVARIANT, and why it gets a whole-library sweep rather than a few examples: this tool rewrites
// proven C# in place. If it ever changes the SHAPE of an INPUTS block — drops a field, merges two,
// loses a trailing comment — it has corrupted a file that was proven correct, and the damage would
// show up as a Revit compile error at best and as a job silently running on the wrong values at worst.
// So the sweep asserts, over all 394 fragments, that rewriting the VALUES leaves the declared types,
// names and comments untouched. That is exactly the bug this test caught while the tool was being
// written: `byte colorR = 255, colorG = 0, colorB = 0;` declares three fields on one line — 50 such
// lines across 28 fragments, every colour job among them — and the first version replaced the whole
// initialiser, keeping colorR's new value and deleting colorG and colorB.

import test from "node:test";
import assert from "node:assert/strict";

import { register as registerRunFragment } from "../tools/run-fragment.js";
import { loadFragments, inputsBlockRange, parseDeclarationLine } from "../../tools/fragment-lib.mjs";

function makeRunner() {
  let handler;
  registerRunFragment({ registerTool: (name, config, h) => { handler = h; } });
  return async (args) => JSON.parse((await handler(args)).content[0].text);
}
const run = makeRunner();

const inputsBlocks = (code) =>
  [...code.matchAll(/---- INPUTS[\s\S]*?---- END INPUTS ----/g)].map((m) => m[0]);

// The declared shape of an INPUTS block: types, field names and trailing comments, with the VALUES
// deliberately excluded — those are the only thing allowed to change.
function shapeOf(src) {
  const r = inputsBlockRange(src);
  if (!r) return null;
  return src
    .slice(r.start, r.end)
    .split(/\r?\n/)
    .map(parseDeclarationLine)
    .filter(Boolean)
    .map((p) => `${p.type}|${p.declarators.map((d) => d.name).join(",")}|${p.tail}`)
    .join("\n");
}

test("composes a filter and an action into one script with a single return", async () => {
  const out = await run({
    describe: "Colour the supply ducts blue",
    fragments: ["filter-by-category", "action-set-color-uniform"],
    inputs: { targetCategory: "OST_DuctCurves", colorR: 0, colorG: 0, colorB: 255 },
    preview: true,
  });

  assert.equal(out.success, true);
  assert.match(out.code.split("\n")[0], /^\/\/ Colour the supply ducts blue$/, "line 1 is the spoken comment");
  // Count only TOP-LEVEL returns. Two other things in this composition look like one and are not:
  // three header comments mention `return sb.ToString();` ("add it as your own last line"), and a
  // helper inside the filter has its own indented `return`. The tool strips comments and tracks brace
  // depth for exactly this reason — if either were miscounted it would skip the return the script needs.
  const topLevelReturns = out.code.split("\n").filter((l) => /^return\b/.test(l));
  assert.equal(topLevelReturns.length, 1, "exactly one top-level return");
  assert.ok(out.code.trimEnd().endsWith("return sb.ToString();"), "and it is last");
  assert.equal(out.composition.returnAppended, true);
  assert.match(out.code, /BuiltInCategory targetCategory = BuiltInCategory\.OST_DuctCurves;/);
  assert.match(out.code, /byte colorR = 0, colorG = 0, colorB = 255;/);
});

test("a multi-field declaration line keeps the fields that were not supplied", async () => {
  const out = await run({
    describe: "Colour check",
    fragments: ["filter-by-category", "action-set-color-uniform"],
    inputs: { colorG: 77 },
    preview: true,
  });
  // colorR and colorB must survive at the file's values — 255 and 0 — not vanish.
  assert.match(out.code, /byte colorR = 255, colorG = 77, colorB = 0;/);
});

test("an unset input is reported as left at the file's value, never as a default", async () => {
  const out = await run({ describe: "Warn check", fragments: "filter-by-category", preview: true });
  assert.ok(out.composition.warnings.some((w) => /LEFT AT THE FILE'S VALUE/.test(w)));
  assert.ok(out.composition.fragments[0].leftAtFileValue.some((s) => s.startsWith("targetCategory")));
});

test("requireAllInputs refuses rather than running on a file value", async () => {
  const out = await run({
    describe: "Strict", fragments: "filter-by-category",
    inputs: { targetCategory: "OST_DuctCurves" }, requireAllInputs: true, preview: true,
  });
  assert.equal(out.success, false);
  assert.match(out.error, /requireAllInputs/);
  assert.ok(out.notSupplied.some((s) => s.startsWith("levelIdFilter")));
});

test("an unknown input name is an error naming the real fields, not a silent no-op", async () => {
  const out = await run({
    describe: "Typo", fragments: "filter-by-category",
    inputs: { targetCategry: "OST_DuctCurves" }, preview: true,
  });
  assert.equal(out.success, false);
  assert.match(out.error, /targetCategry/);
  assert.ok(out.declaredInputs.some((d) => d.startsWith("targetCategory ")), "it names the right spelling");
});

test("a wrong-typed value is refused before anything is composed", async () => {
  const out = await run({
    describe: "Type", fragments: "filter-by-category",
    inputs: { targetCategory: 42 }, preview: true,
  });
  assert.equal(out.success, false);
  assert.match(out.error, /BuiltInCategory needs a string/);
});

test("an unknown fragment name is an error listing the near misses", async () => {
  const out = await run({ describe: "Missing", fragments: "action-set-color", preview: true });
  assert.equal(out.success, false);
  assert.match(out.error, /No fragment named/);
  assert.ok(out.candidates.includes("actions/color-graphics/action-set-color-uniform.cs"));
});

test("two filters composed is refused — both declare sb and elements", async () => {
  const out = await run({
    describe: "Double filter", fragments: ["filter-by-category", "filter-by-family"], preview: true,
  });
  assert.equal(out.success, false);
  assert.match(out.error, /each declare `(sb|elements)`/);
});

test("a fragment that already returns does not get a second return appended", async () => {
  const out = await run({ describe: "Standalone", fragments: "context-session-start", preview: true });
  assert.equal(out.success, true);
  assert.equal(out.composition.returnAppended, false);
  assert.match(out.composition.returnReason, /already returns/);
});

test("the prelude, when asked for, goes first", async () => {
  const out = await run({
    describe: "With prelude", fragments: "filter-by-category", prelude: true, preview: true,
  });
  assert.equal(out.composition.fragments[0].fragment, "lib/prelude.cs");
  assert.ok(out.code.indexOf("MM_PER_FOOT") < out.code.indexOf("targetCategory"), "prelude precedes the filter");
});

test("an unproven fragment says so instead of passing as verified", async () => {
  const unproven = loadFragments().find((f) => f.inputs.length && f.status !== "verified");
  if (!unproven) return; // nothing left unproven — a good problem to have
  const out = await run({ describe: "Status", fragments: unproven.path, preview: true });
  assert.ok(out.composition.warnings.some((w) => /Not proven against a real model/.test(w)));
});

test("C# literals are built from the declared type, and strings are escaped", async () => {
  const out = await run({
    describe: "Literals",
    fragments: "filter-by-wrong-category",
    inputs: {
      nameKeywords: ['he said "hi"', "back\\slash"],
      tagPrefixes: [],
      expectedCategory: "OST_DuctTerminal",
      onlyMismatches: false,
    },
    preview: true,
  });
  assert.match(out.code, /new string\[\] \{ "he said \\"hi\\"", "back\\\\slash" \}/);
  assert.match(out.code, /new string\[\] \{ \}/);
  assert.match(out.code, /BuiltInCategory expectedCategory = BuiltInCategory\.OST_DuctTerminal;/);
  assert.match(out.code, /bool onlyMismatches = false;/);
});

// --- the sweep ---------------------------------------------------------------------------------------
test("every fragment in the library rewrites without changing its INPUTS shape", async () => {
  const sample = (type) => {
    const t = type.replace(/\s+/g, "");
    const base = t.endsWith("?") ? t.slice(0, -1) : t;
    const arr = base.match(/^(\w+)\[\]$/) || base.match(/^List<(\w+)>$/);
    if (arr) return [sample(arr[1]), sample(arr[1])];
    switch (base) {
      case "string": return 'a "quoted" value';
      case "bool": return true;
      case "int": case "long": case "short": case "byte": case "uint": case "ulong": case "sbyte": return 7;
      case "double": return 12.5;
      case "float": return 1.5;
      case "decimal": return 2.5;
      case "BuiltInCategory": return "OST_DuctCurves";
      case "ElementId": return 12345;
      default: return { raw: `default(${base})` };
    }
  };

  const withInputs = loadFragments({ withSource: true }).filter((f) => f.inputs.length);
  assert.ok(withInputs.length > 200, `expected the library to have input-bearing fragments, got ${withInputs.length}`);

  const problems = [];
  for (const f of withInputs) {
    const inputs = {};
    for (const i of f.inputs) inputs[i.name] = sample(i.type);

    const out = await run({ describe: "sweep", fragments: f.path, inputs, preview: true, appendReturn: false });
    if (out.success === false) { problems.push(`${f.path}: refused — ${out.error}`); continue; }
    if (shapeOf(f.src) !== shapeOf(out.code)) { problems.push(`${f.path}: INPUTS shape changed`); continue; }
    const left = out.composition.fragments[0].leftAtFileValue;
    if (left.length) problems.push(`${f.path}: ${left.length} input(s) not applied — ${left.join(", ")}`);
  }

  assert.deepEqual(problems, [], `${problems.length} of ${withInputs.length} fragments failed the sweep`);
});

test("everything outside the INPUTS block is sent byte-identical", async () => {
  const f = loadFragments({ withSource: true }).find((x) => x.path === "filters/by-identity/filter-by-category.cs");
  const out = await run({
    describe: "Byte check", fragments: f.path,
    inputs: { targetCategory: "OST_DuctTerminal" }, preview: true, appendReturn: false,
  });

  const strip = (src) => {
    const r = inputsBlockRange(src);
    return src.slice(0, r.start) + src.slice(r.end);
  };
  // The composed script is the spoken comment, a blank line, then the fragment verbatim.
  const sent = out.code.split("\n").slice(2).join("\n");
  assert.equal(strip(sent), strip(f.src), "the fragment's own code was altered outside its INPUTS block");
  assert.equal(inputsBlocks(out.code).length, 1);
});
