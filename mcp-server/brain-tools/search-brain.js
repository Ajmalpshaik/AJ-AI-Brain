// search_brain - ask the AJ AI Brain a question in plain English.
//
// WHY THIS IS NOT IN mcp-server/tools/:
// tools/brain-status.mjs:90 counts every .js file in mcp-server/tools/ and reports the total
// as "native tools", meaning Revit bridge tools. This one never touches Revit, so putting it
// in that folder would quietly turn a true number into a false one - the exact
// documentation-ahead-of-reality failure this repo keeps having. Different kind of tool,
// different folder, both counts stay honest.
//
// WHY IT EXISTS AT ALL:
// semantic-index\ask-brain-hybrid.cmd is a Windows batch file. On Claude Code for web, or any
// Linux/macOS container, it does not fail - it silently does nothing. That is how a whole
// session of edits went through unchecked on 2026-08-04 when a .ps1 hook wrapper had the same
// problem. A tool call works everywhere Node runs.
//
// Reads only. Never touches Revit or the AJ AI bridge, so it works with Revit closed.

import fs from "node:fs";
import path from "node:path";
import { spawnCapture } from "./spawn-capture.js";
import { fileURLToPath } from "node:url";
import { z } from "zod";
import { asToolResult } from "../shared/tool-result.js";
import { defineTool } from "../shared/register.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "..", "semantic-index");

// Windows puts the venv interpreter in Scripts/, every other platform in bin/.
function venvPython() {
  const candidates = [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ];
  return candidates.find((p) => fs.existsSync(p)) || null;
}

export function register(server) {
  defineTool(server,
    "search_brain",
    "Search the AJ AI Brain in plain English for the skill, knowledge note or C# fragment " +
      "that answers a question. Matches meaning as well as exact words, and marks fragments " +
      "PROVEN or unproven. Use it before writing any new C# and before answering any Revit " +
      "how-to question. Read the top 3-5 results, not just the first - the last reproducible " +
      "run scored 3/14 at #1 and 5/14 in the top 3, and the misses are usually site " +
      "vocabulary (semantic-index/score-history.md is the only figure to quote). Reads only; " +
      "works with Revit closed.",
    {
      query: z
        .string()
        .describe("The question in plain English, in the user's own words, site terms and all."),
      top: z
        .number()
        .int()
        .min(1)
        .max(20)
        .optional()
        .describe("How many results to return. Defaults to 5."),
      area: z
        .enum(["fragment", "knowledge", "skill", "guide"])
        .optional()
        .describe("Restrict to one part of the Brain. Omit to search all of it."),
    },
    async ({ query, top, area }) => {
      try {
        const python = venvPython();
        if (!python) {
          return asToolResult({
            success: false,
            error:
              "No Python found in semantic-index/venv. Set it up per " +
              "semantic-index/requirements.txt, then retry.",
          });
        }

        const args = [path.join(semanticRoot, "brain_search_hybrid.py"), query];
        if (top) args.push("--top", String(top));
        if (area) args.push("--area", area);

        const result = await spawnCapture(python, args, {
          cwd: semanticRoot,
          maxBuffer: 10 * 1024 * 1024,
          timeout: 120000, // a cold model load is seconds, not minutes; never hang the server
        });

        if (result.error) {
          return asToolResult({ success: false, error: result.error.message });
        }
        if (result.status !== 0) {
          return asToolResult({
            success: false,
            error: `${result.stdout || ""}${result.stderr || ""}`.trim() || "search failed",
          });
        }

        // Returned as plain text rather than through asToolResult on purpose. The search's own
        // layout - ranks, "found by: meaning #3 + words #1", [PROVEN] markers, and the STALE
        // INDEX banner - IS the payload here. Wrapping it in JSON.stringify would escape every
        // newline and make the one thing this tool exists to show unreadable. Errors still go
        // through asToolResult so the failure shape matches every other tool.
        return {
          content: [{ type: "text", text: result.stdout.trim() }],
          isError: false,
        };
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
