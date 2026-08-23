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
import net from "node:net";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { shortlistBlock } from "./shortlist.mjs";

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

// THE SHORTLIST, printed before the search block and independently of it.
//
// The search is the weak link - the right answer is #1 on 5 of 28 real questions
// (semantic-index/score-history.md) - and the fragments Ajmal uses every day should not be
// subject to it at all. This puts them in front of the assistant on every message, ranked by his
// own last 30 days of real work, so the decision order can actually start at step 1:
//   shortlist or native tool  ->  search for a fragment  ->  write new C#
//
// Printed FIRST, so it is read before the search results it is meant to pre-empt. Computed
// in-process from one .jsonl - never a spawn, which is the 426 ms -> 38 ms lesson from the
// fragment index (knowledge/brain-log.md, 2026-08-21) and this runs on every single message.
//
// Wrapped, because a hook that throws blocks what Ajmal typed. No log, unreadable log, half-written
// last line: all of them print nothing and get out of the way.
try {
  const shortlist = shortlistBlock();
  if (shortlist) console.log(shortlist + "\n");
} catch {
  // silent, always - see the header
}

// --- fast path: ask a warm server, in Node, with no Python at all -----------------------
//
// This hook runs on EVERY message. Measured 2026-08-21 it cost 3,536 ms, and even with the warm
// server behind a Python relay it still cost 840 ms - of which only ~231 ms was the search. The
// rest was starting an interpreter to pass a socket message that Node can send itself.
//
// So when a server is up, Node speaks to it directly and asynchronously. A hook does not need
// synchronous I/O: it only has to write its block to stdout before the process exits, which a
// callback does perfectly well. That removes an entire process launch from every message.
//
// The server returns the FINISHED block, built by brain_context.build_block, not raw results.
// One formatter, in one language - including the two guardrail lines that tell the session to
// say so when nothing matched. A JavaScript copy of that format is exactly how a guardrail
// stops being emitted without anyone noticing.
//
// Any problem at all - no server, stale details, refused, slow, malformed - falls through to
// the Python path, which is unchanged and still correct on its own.
function askWarmServer(question) {
  return new Promise((resolve) => {
    let info;
    try {
      const infoPath = path.join(semanticRoot, "brain-server.json");
      if (!fs.existsSync(infoPath)) return resolve(null);
      info = JSON.parse(fs.readFileSync(infoPath, "utf8"));
      if (!info.port || !info.token) return resolve(null);
    } catch {
      return resolve(null);
    }

    const request = JSON.stringify({
      token: info.token, cmd: "context", query: question, top_k: 5, log: true,
    });

    let done = false;
    const finish = (value) => { if (!done) { done = true; resolve(value); } };

    const sock = net.createConnection({ host: "127.0.0.1", port: Number(info.port) });
    let buf = "";
    sock.setTimeout(15000, () => { sock.destroy(); finish(null); });
    sock.on("connect", () => sock.write(request + "\n"));
    sock.on("data", (chunk) => {
      buf += chunk;
      if (!buf.includes("\n")) return;
      sock.end();
      try {
        const reply = JSON.parse(buf.trim().split("\n")[0]);
        finish(reply.ok ? (reply.block || "") : null);
      } catch {
        finish(null);
      }
    });
    sock.on("error", () => finish(null));
    sock.on("close", () => finish(null));
  });
}

const warm = await askWarmServer(prompt);
if (warm !== null) {
  if (warm.trim()) console.log(warm.trim());
  process.exit(0);
}

const python = venvPython();
if (!python) process.exit(0); // silent: a missing venv must not spam every message

const result = spawnSync(
  python,
  // --log records the question and its hits in job-log/questions.jsonl. Over months that
  // answers "which of the 351 fragments actually get used", which nothing else can, and
  // builds the question -> file pairs a fine-tune would need.
  [path.join(semanticRoot, "brain_context.py"), "--top", "5", "--log", prompt],
  // windowsHide: this hook runs with no console of its own, so a console child gets a NEW
  // console - which Windows 11 hands to Windows Terminal as a black window over Revit.
  { encoding: "utf8", cwd: semanticRoot, maxBuffer: 4 * 1024 * 1024, timeout: 60000,
    windowsHide: true }
);

if (result.error || result.status !== 0) process.exit(0);

const block = (result.stdout || "").trim();
if (block) console.log(block);

process.exit(0);
