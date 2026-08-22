// One path from a native tool to the bridge, so the five fragment-backed tools do not each repeat
// the compose / preview / send / error dance.
//
// The engine (fragment-runner.js) stays pure and never touches Revit; this is the thin layer that
// does. Split that way so `preview` and a real run compose through byte-identical code.

import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "./tool-result.js";
import { composeFragments } from "./fragment-runner.js";

export async function runComposed({ describe, names, values = {}, preview = false,
                                    allowDestructive = false, appendReturn } = {}) {
  try {
    const built = composeFragments({ describe, names, values, appendReturn });
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

// Every fragment-backed tool offers this, for the same reason run_fragment does: seeing the exact
// composed script before it runs is the cheapest way to catch a wrong assumption.
import { z } from "zod";
export const previewField = {
  preview: z
    .boolean()
    .optional()
    .describe("Return the exact composed C# WITHOUT sending it to Revit. Default false."),
};

// Drops undefined keys, so an omitted optional argument leaves the fragment's own value in place
// (and gets reported as "left at the file's value") instead of being written in as `undefined`.
export function defined(obj) {
  return Object.fromEntries(Object.entries(obj).filter(([, v]) => v !== undefined));
}
