// search_graph - ask the AJ AI Brain's KNOWLEDGE GRAPH, not its vector index.
//
// WHY THIS EXISTS
// The Brain already had a knowledge graph - 1,192 nodes, 1,518 edges, 328 named communities,
// rebuilt and repaired on 2026-08-13 - and nothing could reach it. Every search went through
// the vector index only. The graph was a fully-built, fully-paid-for asset that retrieval did
// not know existed.
//
// It is NOT a second opinion on the same question; it answers a different SHAPE of question.
// Measured on "how do I stop ducts overlapping the ceiling":
//   search_brain (vector) -> filter-by-element-intersection.cs, tagging.md, brain-log.md
//   search_graph  (graph) -> create-ceiling.cs, ray-trace-to-ceiling.cs, hvac-ducts.md
// The graph found ray-trace-to-ceiling.cs, which the vector index never surfaced at any depth,
// because it matched the ENTITIES in the question and then walked to their neighbours rather
// than comparing one embedding against another.
//
// Use it when the question is about how things CONNECT - "what depends on X", "what else
// touches this", "how do these two relate" - which is exactly what a vector index is worst at.
// Use search_brain when the question is about MEANING - "how do I do X".
//
// HONEST LIMITATION: the graph does NOT rebuild itself. The semantic index does (Stop hook),
// but the graph needs `graphify update .` for code and a subagent pass for markdown. So this
// can be out of date in a way search_brain cannot. Check knowledge/brain-log.md for the last
// rebuild date before trusting it on something written today.
//
// Reads only. Never touches Revit, so it works with Revit closed.

import fs from "node:fs";
import path from "node:path";
import { spawnCapture } from "./spawn-capture.js";
import { fileURLToPath } from "node:url";
import { z } from "zod";
import { asToolResult } from "../shared/tool-result.js";
import { defineTool } from "../shared/register.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.join(here, "..", "..");

// The graph must exist before any of this means anything.
function graphPath() {
  return path.join(brainRoot, "graphify-out", "graph.json");
}

// `graphify` is installed as a user-level CLI, not a repo dependency, so it may be absent on
// another machine even when the Brain folder was copied there. Fail with a sentence that says
// what to do, rather than a spawn error.
function graphifyBin() {
  const candidates = [
    process.env.GRAPHIFY_BIN,
    path.join(process.env.USERPROFILE || process.env.HOME || "", ".local", "bin", "graphify"),
    path.join(process.env.USERPROFILE || process.env.HOME || "", ".local", "bin", "graphify.exe"),
  ].filter(Boolean);
  for (const c of candidates) if (fs.existsSync(c)) return c;
  return "graphify"; // fall back to PATH
}

export function register(server) {
  defineTool(server,
    "search_graph",
    "Search the AJ AI Brain's knowledge GRAPH — how things connect, rather than what they mean. " +
      "Use for 'what depends on X', 'what else touches this', 'how do these two relate', or when " +
      "search_brain returns things that read right but miss the actual link. It answers a " +
      "different shape of question than search_brain and the two are worth using together. " +
      "Reads only; works with Revit closed. NOTE: the graph does not rebuild itself, so it can " +
      "be older than the vector index.",
    {
      question: z
        .string()
        .describe("The question, for mode 'query'. For 'explain' give a node label; for 'path' see nodeA/nodeB."),
      mode: z
        .enum(["query", "explain", "path"])
        .optional()
        .describe("query = walk out from the question's entities (default). explain = describe one node and its neighbours. path = shortest path between nodeA and nodeB."),
      nodeA: z.string().optional().describe("Start node label, mode 'path' only."),
      nodeB: z.string().optional().describe("End node label, mode 'path' only."),
      budget: z
        .number()
        .int()
        .min(200)
        .max(6000)
        .optional()
        .describe("Token cap on the answer (default 1500). Raise it if the result says TRUNCATED."),
    },
    async ({ question, mode, nodeA, nodeB, budget }) => {
      try {
        if (!fs.existsSync(graphPath())) {
          return asToolResult({
            success: false,
            error:
              "No knowledge graph at graphify-out/graph.json. Build it with `graphify update .` " +
              "(code only, no LLM) — see knowledge/brain-log.md, 2026-08-13, for the full pipeline.",
          });
        }

        const bin = graphifyBin();
        const chosen = mode || "query";
        let args;
        if (chosen === "path") {
          if (!nodeA || !nodeB) {
            return asToolResult({ success: false, error: "mode 'path' needs both nodeA and nodeB." });
          }
          args = ["path", nodeA, nodeB];
        } else if (chosen === "explain") {
          args = ["explain", question];
        } else {
          args = ["query", question, "--budget", String(budget || 1500)];
        }

        const result = await spawnCapture(bin, args, {
          cwd: brainRoot,
          maxBuffer: 10 * 1024 * 1024,
          timeout: 120000,
        });

        if (result.error) {
          return asToolResult({
            success: false,
            error:
              `Could not run graphify (${result.error.message}). It is a user-level CLI, not a ` +
              `repo dependency — install it, or set GRAPHIFY_BIN to its path.`,
          });
        }
        if (result.status !== 0) {
          return asToolResult({
            success: false,
            error: `${result.stdout || ""}${result.stderr || ""}`.trim() || "graph query failed",
          });
        }

        // Plain text, not asToolResult, for the same reason as search_brain: the traversal's own
        // layout (NODE lines with src=, community=, and its TRUNCATED warning) IS the payload,
        // and JSON.stringify would escape every newline out of readability.
        return { content: [{ type: "text", text: (result.stdout || "").trim() }], isError: false };
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
