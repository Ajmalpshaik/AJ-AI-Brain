#!/usr/bin/env node
// PostToolUse hook - record every call that reached the live Revit model.
//
// WHY: nothing anywhere recorded what real work actually happened. Every session had real
// elements, real numbers, real failures, and all of it evaporated when the session ended. So
// three questions about this Brain were unanswerable:
//
//   - which of the 398 fragments actually do the work (guess: about 40 do 90% of it)
//   - which have never once run on a real job
//   - which fail repeatedly against a real model
//
// That last one matters most: a fragment failing on a real model is the single most valuable
// signal this system produces, and today it disappears.
//
// It also makes DELETING safe. An unused fragment is not free - it competes in every search,
// so it is one more thing the right answer can lose to. Nobody can prune 269 fragments without
// evidence of which ones are dead, and this is that evidence.
//
// HOW IT IDENTIFIES A FRAGMENT: composed C# keeps its source headers, in the fixed form
//   // FRAGMENT (creator) - create-levels.cs
// so the fragment names are read straight out of the code that was actually sent. No guessing,
// and it stays correct as fragments are renamed, because it records what the script said.
//
// Writes to job-log/, which sits outside every indexed folder on purpose: a growing log inside
// knowledge/ would become another big matcher competing in search, which is the mistake
// brain-log.md and glossary.md each had to be discounted for.
//
// Always exits 0 and never prints. A log that fails must not disturb the work it records.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const logFile = path.join(here, "..", "job-log", "revit-runs.jsonl");

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

try {
  const payload = JSON.parse(readStdin() || "{}");
  const toolName = payload.tool_name ?? "";

  // Only calls that actually reached Revit.
  if (!toolName.includes("aj-tools-aj-ai")) process.exit(0);

  const input = payload.tool_input ?? {};
  const code = typeof input.code === "string" ? input.code : "";

  // "// FRAGMENT (creator) - create-levels.cs" -> "create-levels.cs". The dash may be an em
  // dash or a hyphen depending on the fragment, so match either.
  const fragments = [...code.matchAll(/FRAGMENT\s*\([^)]*\)\s*[—-]\s*([\w.-]+\.cs)/g)].map(
    (m) => m[1]
  );

  // run_fragment NAMES its fragments instead of pasting their code, so there is no `code` field
  // to read them out of. Without this the log went blind the moment that tool started being used
  // - and it is the tool meant to carry most jobs, so the usage record would have quietly become
  // a record of the OLD way only. Normalised to the same bare "name.cs" the regex above produces,
  // so counts from both paths add up instead of splitting in two.
  const named = input.fragments;
  for (const f of Array.isArray(named) ? named : named ? [named] : []) {
    if (typeof f !== "string" || !f.trim()) continue;
    const base = f.trim().replace(/\\/g, "/").split("/").pop();
    fragments.push(base.endsWith(".cs") ? base : base + ".cs");
  }

  // DID IT ACTUALLY WORK? Until 2026-08-23 this log recorded what was sent and never whether it
  // landed, so the one question that matters - is the library more reliable than fresh code? - could
  // not be answered from five days of data. Worse, the tool call SUCCEEDING is not the same as the
  // C# succeeding: the bridge returns `Success: false` inside a perfectly normal MCP response when
  // the code throws or fails to compile, which is exactly how `action-center-room-tags.cs` reported
  // CS0246 on 2026-08-22. So read the bridge's own flag out of the response body rather than trusting
  // that a reply arrived at all.
  let ok;
  try {
    const blocks = Array.isArray(payload.tool_response) ? payload.tool_response : [];
    const text = blocks.map((b) => (typeof b?.text === "string" ? b.text : "")).join("\n");
    if (text.trim()) {
      const parsed = JSON.parse(text);
      const flag = parsed?.Success ?? parsed?.success;
      if (typeof flag === "boolean") ok = flag;
    }
  } catch {
    // Not JSON, or no response recorded. Leave `ok` undefined rather than guessing - an invented
    // success is worse than a gap, because it would poison the very comparison this field exists for.
  }

  const entry = {
    date: new Date().toISOString().slice(0, 10),
    tool: toolName.replace(/^mcp__[^_]*__/, "").replace(/^mcp__.*?__/, ""),
    fragments: [...new Set(fragments)],
    codeLines: code ? code.split("\n").length : 0,
    category: input.category ?? undefined,
    ok,
  };

  fs.mkdirSync(path.dirname(logFile), { recursive: true });
  fs.appendFileSync(logFile, JSON.stringify(entry) + "\n", "utf8");
} catch {
  // Never disturb the work being recorded.
}

process.exit(0);
