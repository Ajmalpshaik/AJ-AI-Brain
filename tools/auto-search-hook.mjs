#!/usr/bin/env node
// UserPromptSubmit hook - search the Brain for what Ajmal just asked, before the assistant reads
// it, and put the top hits into context.
//
// WHY: retrieval used to be optional. Nothing forced a search, so whether this Brain got consulted
// at all depended on the assistant remembering to run a command - and when it forgot, the answer
// came from general Revit knowledge instead of 269 proven fragments and three weeks of gotchas that
// exist nowhere else. This removes the remembering.
//
// WHY IT IS GATED: a search costs ~3.5 s, because it loads a 166 MB embedding model. That is
// nothing on a real question - the answer was going to take longer anyway - and pure waste on "ok"
// or "go ahead". So short confirmations and slash commands are skipped outright. Deliberately NOT
// solved by building a warm search service: gating is the cheap fix, and the expensive one should
// only be built if the delay is ever actually felt.
//
// It emits the COMPACT block from semantic-index/brain_context.py, never the full search output -
// full output carries a long snippet per hit and would bloat every single message in the session.
//
// Never call ask-brain-hybrid.cmd from here. Every .cmd wrapper in this repo ends with `pause` and
// would block forever waiting for a keypress.
//
// Always exits 0, and stays silent on every failure path. A hook that errors must never block or
// pollute what Ajmal typed.

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "semantic-index");

// Replies aimed at the assistant, not questions aimed at the Brain. Searching these costs 3.5 s and
// returns nothing anyone wanted.
const CONFIRMATIONS =
  /^(ok(ay)?|k|yes|yep|yeah|no|nope|sure|fine|good|great|nice|thanks|thank you|ta|go|go ahead|do it|dot it|proceed|continue|carry on|next|stop|wait|hold on|please|correct|right|exactly|merge it|start|begin|done|yes please|go on)\b[\s.!?]*$/i;

function shouldSearch(prompt) {
  const text = (prompt || "").trim();
  if (!text) return false;
  if (text.startsWith("/")) return false; // slash command, not a question
  if (CONFIRMATIONS.test(text)) return false; // "ok", "go ahead", "merge it"
  if (text.split(/\s+/).length < 4) return false; // too short to retrieve usefully
  return true;
}

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

// Windows puts the venv interpreter in Scripts/, every other platform in bin/.
function venvPython() {
  const candidates = [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ];
  return candidates.find((p) => fs.existsSync(p)) || null;
}

let payload;
try {
  payload = JSON.parse(readStdin() || "{}");
} catch {
  process.exit(0);
}

const prompt = payload.prompt ?? "";
if (!shouldSearch(prompt)) process.exit(0);

const python = venvPython();
if (!python) process.exit(0); // silent: a missing venv must not spam every message

const result = spawnSync(
  python,
  [path.join(semanticRoot, "brain_context.py"), "--top", "5", prompt],
  { encoding: "utf8", cwd: semanticRoot, maxBuffer: 4 * 1024 * 1024, timeout: 60000 }
);

if (result.error || result.status !== 0) process.exit(0);

const block = (result.stdout || "").trim();
if (block) console.log(block);

process.exit(0);
