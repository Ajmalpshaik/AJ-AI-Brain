// run_fragment — run the PROVEN library by name, instead of retyping it.
//
// WHY THIS EXISTS. Until now every scripted job went the same way: search finds the right fragment,
// the assistant READS the whole .cs file, HAND-EDITS its INPUTS block, and PASTES the result into
// run_csharp. Three costs, all of them avoidable:
//
//   1. "PROVEN" stopped meaning anything at the moment of running. A fragment is marked verified
//      because it ran correctly once — and then it gets retyped from scratch on every later job, so
//      what actually reaches Revit is a fresh copy that has never been proven at all. Two wasted Revit
//      round trips were logged on 2026-08-20 alone, both from exactly this (see knowledge/brain-log.md
//      and live-model/core.md "API surface traps that cost a round trip"). This tool sends the file
//      BYTE-IDENTICAL apart from the INPUTS declarations, so the proof survives the run.
//   2. A mistyped or forgotten input was found by Revit, tens of seconds later, mid-job. Here it is
//      found locally before anything is sent — an unknown input name is an ERROR naming the real
//      fields, not a silently ignored key.
//   3. A composed filter+action job cost roughly 2,800 tokens of read-then-paste. Now it costs a
//      fragment name and a small object.
//
// WHAT IT IS NOT. It does not compile-check the C# — only Revit and tools/check-scripts.cmd do that.
// It validates the FORM (which fragments, which inputs, which types, does this compose at all), which
// is the half that can be checked without Revit. Passing here is a floor, not a ceiling: the library's
// standing rule still applies — run one element first, check the real result, then trust it for a batch.
//
// TRUST BOUNDARY. Unchanged from run_csharp: this sends C# to the same bridge, behind the same
// `allowDestructive` gate. It narrows what normally gets sent (a proven file with its declarations
// rewritten) rather than widening it.
//
// The composition rules it automates are scripts/README.md's own, "How to compose two or more
// fragments into one script": optional prelude, then the filter (or creator), then the actions, then
// exactly one `return sb.ToString();`, with a plain-English `//` comment on line 1 because that is the
// only thing the voice reads out before the model changes.

import { z } from "zod";
import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";
import {
  loadFragments,
  inputsBlockRange,
  resolveFragment,
  parseDeclarationLine,
  STATUS_LABEL,
} from "../../tools/fragment-lib.mjs";

// Thrown from deep inside the per-declarator rewrite and caught by the handler, so a bad value is
// reported as a plain message rather than a stack trace.
class InputError extends Error {}

const PRELUDE_PATH = "lib/prelude.cs";

// --- C# literal building ---------------------------------------------------------------------------
// Formats a JSON value by the DECLARED C# type, which is what makes this safe: the fragment's own
// declaration decides how its value is written, not a guess from the value's JavaScript shape.
// Anything not handled here must be passed as a C# expression string, and {raw: "..."} forces that
// for every type — the escape hatch, so an unusual input never blocks the tool.

function csString(v) {
  return '"' + String(v)
    .replace(/\\/g, "\\\\")
    .replace(/"/g, '\\"')
    .replace(/\r/g, "\\r")
    .replace(/\n/g, "\\n")
    .replace(/\t/g, "\\t") + '"';
}

// BuiltInCategory is by far the most common non-primitive input (30 of them, plus 17 arrays), and
// "OST_DuctTerminal" is how everyone writes it. Accept that and qualify it; leave an already-qualified
// or otherwise-shaped expression exactly as given.
function csCategory(v) {
  const s = String(v).trim();
  if (/^OST_\w+$/.test(s)) return "BuiltInCategory." + s;
  return s;
}

function isInt(v) {
  return typeof v === "number" && Number.isInteger(v);
}

function buildLiteral(type, value, name) {
  if (value && typeof value === "object" && !Array.isArray(value) && typeof value.raw === "string") {
    return { literal: value.raw };
  }

  const t = String(type).replace(/\s+/g, "");
  const nullable = t.endsWith("?");
  const base = nullable ? t.slice(0, -1) : t;

  if (value === null) {
    if (nullable || base === "string" || base.endsWith("[]") || base.startsWith("List<")) {
      return { literal: "null" };
    }
    if (base === "ElementId") return { literal: "ElementId.InvalidElementId" };
    return { error: `${name}: null is not valid for a non-nullable ${type}.` };
  }

  // Arrays and lists, e.g. string[] / int[] / BuiltInCategory[] / List<string>
  const arrMatch = base.match(/^(\w+)\[\]$/) || base.match(/^List<(\w+)>$/);
  if (arrMatch) {
    const inner = arrMatch[1];
    if (!Array.isArray(value)) {
      return { error: `${name}: ${type} needs an array, got ${typeof value}.` };
    }
    const parts = [];
    for (let i = 0; i < value.length; i++) {
      const r = buildLiteral(inner, value[i], `${name}[${i}]`);
      if (r.error) return r;
      parts.push(r.literal);
    }
    const ctor = base.startsWith("List<") ? `new List<${inner}>` : `new ${inner}[]`;
    return { literal: parts.length ? `${ctor} { ${parts.join(", ")} }` : `${ctor} { }` };
  }

  switch (base) {
    case "string":
      if (typeof value !== "string") return { error: `${name}: string needs a string, got ${typeof value}.` };
      return { literal: csString(value) };

    case "bool":
      if (typeof value !== "boolean") return { error: `${name}: bool needs true or false, got ${typeof value}.` };
      return { literal: value ? "true" : "false" };

    case "int": case "long": case "short": case "byte": case "uint": case "ulong": case "sbyte":
      if (!isInt(value)) return { error: `${name}: ${type} needs a whole number, got ${JSON.stringify(value)}.` };
      return { literal: String(value) };

    case "double":
      if (typeof value !== "number" || !Number.isFinite(value)) {
        return { error: `${name}: double needs a number, got ${JSON.stringify(value)}.` };
      }
      return { literal: Number.isInteger(value) ? `${value}.0` : String(value) };

    // A bare decimal literal is a DOUBLE in C# — `float f = 1.5;` and `decimal d = 1.5;` are both
    // compile errors without the suffix. Getting this wrong would be a round trip lost to a one-character
    // mistake, which is the whole thing this tool exists to stop.
    case "float":
      if (typeof value !== "number" || !Number.isFinite(value)) {
        return { error: `${name}: float needs a number, got ${JSON.stringify(value)}.` };
      }
      return { literal: `${value}f` };
    case "decimal":
      if (typeof value !== "number" || !Number.isFinite(value)) {
        return { error: `${name}: decimal needs a number, got ${JSON.stringify(value)}.` };
      }
      return { literal: `${value}m` };

    case "BuiltInCategory":
      if (typeof value !== "string") {
        return { error: `${name}: BuiltInCategory needs a string like "OST_DuctTerminal", got ${typeof value}.` };
      }
      return { literal: csCategory(value) };

    // An int literal is safe on every Revit 2020-2027: the constructor became ElementId(long) at 2024,
    // and C# widens int to long implicitly, so `new ElementId(123)` compiles on both sides of that
    // change while a long literal would break 2020. See knowledge/revit-version-compatibility.md.
    case "ElementId":
      if (isInt(value)) {
        if (value < -2147483648 || value > 2147483647) {
          return { error: `${name}: ${value} is outside int range — pass it as {raw: "new ElementId(${value}L)"} and check the Revit version first.` };
        }
        return { literal: `new ElementId(${value})` };
      }
      if (typeof value === "string") return { literal: value };
      return { error: `${name}: ElementId needs a number, or a C# expression as a string.` };

    default:
      // Every other type — an enum, XYZ, a struct — has no safe JSON mapping. Take a C# expression.
      if (typeof value === "string") return { literal: value };
      return {
        error: `${name}: no automatic conversion for type "${type}". Pass the C# expression as a string, e.g. {"${name}": "SomeEnum.Value"}.`,
      };
  }
}

// --- rewriting the INPUTS block --------------------------------------------------------------------
// In place, one declaration at a time, keeping the type, the name and the trailing comment. Only the
// initialiser between `=` and `;` moves. Everything outside the block is untouched, which is what
// "byte-identical apart from the declarations" means.

function applyInputs(src, values) {
  const range = inputsBlockRange(src);
  if (!range) return { applied: [], missing: [], src, hasBlock: false };

  const applied = [];
  const missing = [];
  const lines = src.slice(range.start, range.end).split(/\r?\n/);

  for (let i = 0; i < lines.length; i++) {
    const parsed = parseDeclarationLine(lines[i]);
    if (!parsed) continue;

    // Rebuild the whole line from its declarators. A line can carry several fields
    // (`byte colorR = 255, colorG = 0, colorB = 0;`), and replacing the initialiser as one string
    // would keep colorR's new value and DELETE colorG and colorB — the exact silent corruption this
    // tool exists to prevent, so it is rebuilt field by field instead.
    let touched = false;
    const rebuilt = parsed.declarators.map((d) => {
      if (!Object.prototype.hasOwnProperty.call(values, d.name)) {
        missing.push({ name: d.name, type: parsed.type, fileValue: d.value });
        return `${d.name} = ${d.value}`;
      }
      const built = buildLiteral(parsed.type, values[d.name], d.name);
      if (built.error) throw new InputError(built.error);
      applied.push({ name: d.name, type: parsed.type, was: d.value, now: built.literal });
      touched = true;
      return `${d.name} = ${built.literal}`;
    });

    if (touched) lines[i] = `${parsed.indent}${parsed.type} ${rebuilt.join(", ")};${parsed.tail}`;
  }

  return {
    applied,
    missing,
    hasBlock: true,
    src: src.slice(0, range.start) + lines.join("\n") + src.slice(range.end),
  };
}

// --- composition safety ----------------------------------------------------------------------------
// Strip comments and string/char literals so a brace inside `"{0}"` or a `//` in a URL cannot throw the
// depth count off. Only used for structural questions, never for anything that gets sent.
function stripLiteralsAndComments(code) {
  let out = "";
  let i = 0;
  while (i < code.length) {
    const c = code[i];
    const next = code[i + 1];
    if (c === "/" && next === "/") { while (i < code.length && code[i] !== "\n") i++; continue; }
    if (c === "/" && next === "*") { i += 2; while (i < code.length && !(code[i] === "*" && code[i + 1] === "/")) i++; i += 2; continue; }
    if (c === "@" && next === '"') {
      i += 2;
      while (i < code.length) {
        if (code[i] === '"' && code[i + 1] === '"') { i += 2; continue; }
        if (code[i] === '"') { i++; break; }
        i++;
      }
      continue;
    }
    if (c === '"' || c === "'") {
      const quote = c; i++;
      while (i < code.length) {
        if (code[i] === "\\") { i += 2; continue; }
        if (code[i] === quote) { i++; break; }
        i++;
      }
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

// Does this composed script already end itself? A recipe or a context fragment returns on its own; the
// 199 NOT-STANDALONE ones never do. Detected rather than assumed, because getting it wrong either
// drops the output or fails to compile.
function hasTopLevelReturn(code) {
  const clean = stripLiteralsAndComments(code);
  let depth = 0;
  for (let i = 0; i < clean.length; i++) {
    const c = clean[i];
    if (c === "{") depth++;
    else if (c === "}") depth = Math.max(0, depth - 1);
    else if (depth === 0 && c === "r" && /^return\b/.test(clean.slice(i))) {
      const before = i === 0 ? "" : clean[i - 1];
      if (!/[\w.]/.test(before)) return true;
    }
  }
  return false;
}

function declaresContractName(code, name) {
  const clean = stripLiteralsAndComments(code);
  return new RegExp(`(^|[^\\w.])(var|List<Element>|System\\.Text\\.StringBuilder|StringBuilder)\\s+${name}\\s*=`, "m").test(clean);
}

// --- the tool --------------------------------------------------------------------------------------

export function register(server) {
  server.tool(
    "run_fragment",
    "Run one or more PROVEN C# fragments from scripts/ against the live Revit document, by name, " +
      "with their INPUTS filled in — instead of reading the file and pasting an edited copy into run_csharp. " +
      "Composes them in the order given (scripts/README.md's rule: optional prelude, then the filter or " +
      "creator, then the actions) and appends the single `return sb.ToString();`. " +
      "Unknown input names, wrong types and impossible compositions are errors HERE, before anything reaches Revit. " +
      "Use preview:true to see the exact composed C# without sending it. " +
      "Find fragment names with `node tools/fragment-index.mjs --find <word>` or search_brain.",
    {
      describe: z
        .string()
        .min(3)
        .describe(
          "Plain English, under ten words, what this run does — 'Colour the supply ducts blue'. " +
            "Becomes the first // line of the script, which is the only thing spoken aloud before the model changes."
        ),
      fragments: z
        .union([z.string(), z.array(z.string()).min(1)])
        .describe(
          "Fragment name(s), composed in this order. 'filter-by-category' or " +
            "'filters/by-identity/filter-by-category.cs' both resolve; an ambiguous name is an error listing the candidates."
        ),
      inputs: z
        .record(z.any())
        .optional()
        .describe(
          "INPUTS values by declared name, across all composed fragments. Formatted by the fragment's own " +
            'declared C# type. BuiltInCategory takes "OST_DuctTerminal"; ElementId takes a number. For any ' +
            'other type pass the C# expression as a string, or {"raw": "<C# expression>"} to force it. ' +
            "An unset input keeps the file's value and is reported back — never assume it is a safe default."
        ),
      prelude: z
        .boolean()
        .optional()
        .describe("Paste scripts/lib/prelude.cs first, for its transaction/units/parameter helpers. Default false."),
      preview: z
        .boolean()
        .optional()
        .describe("Return the composed C# and the input report WITHOUT sending it to Revit. Default false."),
      appendReturn: z
        .boolean()
        .optional()
        .describe("Override the automatic `return sb.ToString();` decision. Leave unset unless it guessed wrong."),
      requireAllInputs: z
        .boolean()
        .optional()
        .describe("Refuse to run unless every declared input was supplied. Default false."),
      allowDestructive: z
        .boolean()
        .optional()
        .describe("Same gate as run_csharp — required for Delete/Purge/file writes. Defaults to false."),
    },
    async ({ describe, fragments, inputs, prelude, preview, appendReturn, requireAllInputs, allowDestructive }) => {
      try {
        const names = Array.isArray(fragments) ? fragments : [fragments];
        const values = inputs || {};
        const all = loadFragments({ withSource: true });

        // 1. Resolve every name before doing anything else, so an error names all the bad ones at once.
        const chosen = [];
        for (const n of names) {
          const r = resolveFragment(n, all);
          if (r.error) {
            return asToolResult({
              success: false,
              error: r.error,
              candidates: r.candidates,
              hint: "Search with `node tools/fragment-index.mjs --find <word>`, or search_brain for a plain-English question.",
            });
          }
          chosen.push(r.fragment);
        }

        if (prelude) {
          const p = resolveFragment(PRELUDE_PATH, all);
          if (p.error) return asToolResult({ success: false, error: `prelude requested but ${PRELUDE_PATH} is missing.` });
          if (!chosen.some((f) => f.path === p.fragment.path)) chosen.unshift(p.fragment);
        }

        // 2. An input name that matches nothing is a typo, and a typo that is silently ignored means a
        //    job runs on the file's value while the caller believes it ran on theirs. Refuse.
        const declared = new Map();
        for (const f of chosen) {
          for (const i of f.inputs) {
            if (declared.has(i.name) && declared.get(i.name) !== f.path) {
              return asToolResult({
                success: false,
                error: `Both ${declared.get(i.name)} and ${f.path} declare an input named "${i.name}". C# would reject the composed script as a duplicate local. Run them as two separate calls.`,
              });
            }
            declared.set(i.name, f.path);
          }
        }
        const unknown = Object.keys(values).filter((k) => !declared.has(k));
        if (unknown.length) {
          return asToolResult({
            success: false,
            error: `Not an input of these fragments: ${unknown.join(", ")}.`,
            declaredInputs: [...declared.entries()].map(([name, from]) => `${name}  (${from})`),
          });
        }

        // 3. Two filters composed both declare `sb` and `elements` — the contract names every fragment
        //    shares. That is a duplicate local, and Revit would be the one to say so.
        for (const contract of ["sb", "elements"]) {
          const declarers = chosen.filter((f) => declaresContractName(f.src, contract)).map((f) => f.path);
          if (declarers.length > 1) {
            return asToolResult({
              success: false,
              error: `${declarers.join(" and ")} each declare \`${contract}\`. A composition takes ONE filter or creator, then actions — see scripts/README.md.`,
            });
          }
        }

        // 4. Fill the INPUTS blocks.
        const bodies = [];
        const report = [];
        let missingAll = [];
        for (const f of chosen) {
          let res;
          try {
            res = applyInputs(f.src, values);
          } catch (e) {
            if (e instanceof InputError) return asToolResult({ success: false, error: e.message });
            throw e;
          }
          bodies.push(res.src);
          missingAll = missingAll.concat(res.missing.map((m) => ({ ...m, fragment: f.path })));
          report.push({
            fragment: f.path,
            status: STATUS_LABEL[f.status] || f.status,
            filledIn: res.applied.map((a) => `${a.name} = ${a.now}`),
            leftAtFileValue: res.missing.map((m) => `${m.name} = ${m.fileValue}`),
          });
        }

        if (requireAllInputs && missingAll.length) {
          return asToolResult({
            success: false,
            error: `requireAllInputs is set and ${missingAll.length} input(s) were not supplied.`,
            notSupplied: missingAll.map((m) => `${m.name} (${m.type}) in ${m.fragment} — file value ${m.fileValue}`),
          });
        }

        // 5. Compose. First line is the spoken one; the single return closes the whole script.
        const firstLine = "// " + String(describe).replace(/[\r\n]+/g, " ").trim();
        let code = [firstLine, ...bodies].join("\n\n");

        const alreadyReturns = hasTopLevelReturn(bodies.join("\n"));
        const hasSb = bodies.some((b) => declaresContractName(b, "sb"));
        const shouldAppend = appendReturn !== undefined ? appendReturn : !alreadyReturns && hasSb;
        if (shouldAppend) code += "\n\nreturn sb.ToString();\n";

        const composition = {
          fragments: report,
          returnAppended: shouldAppend,
          returnReason: appendReturn !== undefined
            ? "explicitly set by the caller"
            : alreadyReturns
              ? "not appended — a composed fragment already returns at top level"
              : hasSb
                ? "appended — nothing returned and `sb` is in scope"
                : "not appended — no `sb` is declared, so there is nothing to return",
          lines: code.split("\n").length,
        };

        // The warnings a caller must not be able to miss. Every number in this Brain is a per-request
        // input, never a default (START-HERE.md rule 3) — so an unset one is reported every time, and
        // an unproven fragment says so rather than passing quietly as if it were verified.
        const warnings = [];
        if (missingAll.length) {
          warnings.push(
            `${missingAll.length} input(s) LEFT AT THE FILE'S VALUE, which is not a default: ` +
              missingAll.map((m) => `${m.name}=${m.fileValue}`).join(", ")
          );
        }
        const unproven = report.filter((r) => r.status !== "PROVEN");
        if (unproven.length) {
          warnings.push(
            `Not proven against a real model: ${unproven.map((r) => `${r.fragment} [${r.status}]`).join(", ")}. ` +
              "Run it on one element and check the real result before trusting it for a batch."
          );
        }
        if (warnings.length) composition.warnings = warnings;

        if (preview) {
          return asToolResult({ success: true, preview: true, composition, code });
        }

        const result = await callBridge(code, allowDestructive);
        const merged = typeof result === "object" && result !== null ? { ...result } : { result };
        merged.composition = composition;
        return asToolResult(merged);
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
