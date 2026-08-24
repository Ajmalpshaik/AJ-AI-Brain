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
import { composeFragments } from "../shared/fragment-runner.js";
import { defineTool } from "../shared/register.js";

// The composing, validating and INPUTS-rewriting all live in ../shared/fragment-runner.js, shared
// with the native tools that wrap one specific fragment (grayout, session_start, ...). Those tools
// are typed front doors onto the same engine — none of them carries its own copy of a fragment's C#.

export function register(server) {
  defineTool(server,
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
        const built = composeFragments({
          describe,
          names: Array.isArray(fragments) ? fragments : [fragments],
          values: inputs || {},
          prelude,
          appendReturn,
          requireAllInputs,
        });
        if (built.error) return asToolResult({ success: false, ...built });

        if (preview) {
          return asToolResult({ success: true, preview: true, composition: built.composition, code: built.code });
        }

        const result = await callBridge(built.code, allowDestructive);
        const merged = typeof result === "object" && result !== null ? { ...result } : { result };
        merged.composition = built.composition;
        return asToolResult(merged);
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
