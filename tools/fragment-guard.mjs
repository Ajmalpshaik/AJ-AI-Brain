#!/usr/bin/env node
// PreToolUse hook - say "that fragment already exists" BEFORE the C# reaches Revit.
//
// WHY THIS EXISTS, measured rather than assumed. job-log/revit-runs.jsonl over 5 working days:
// 297 code runs into the model, 14 through run_fragment, 1 composed by hand, and 282 written from
// nothing - 95% of everything that touched the model was code that had never been run before, while
// 247 proven fragments sat unused on the shelf.
//
// tools/fragment-nudge.mjs already detects this. It is a STOP hook, so it reports the miss after the
// code has gone to Revit and the round trip is spent. It has been reporting it for days and the ratio
// did not move, which is the whole lesson: noticing is not preventing. The check has to happen at the
// only moment it can still change the outcome - just before the call.
//
// The cost of being wrong here is real and one-directional, so this ADVISES AND NEVER BLOCKS. A false
// "use this instead" that stops a job is far worse than a missed suggestion; the agent reads the note
// and decides. It also stays silent unless it is fairly confident, because a hook that fires on every
// call is a hook that gets ignored - the exact fate of the Stop-hook version.
//
//   echo '{"tool_name":"...","tool_input":{"code":"..."}}' | node tools/fragment-guard.mjs
//   node tools/fragment-guard.mjs --selftest    replay job-log/revit-runs.jsonl, report hit rate

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { loadFragments } from "./fragment-lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));

// Words that appear in nearly every Revit snippet carry no signal about WHICH job this is, so they
// would match everything and rank nothing. Dropping them is what turns a fuzzy match into a useful one.
const STOP = new Set([
  "the", "and", "for", "with", "that", "this", "from", "into", "each", "all", "new", "var", "int",
  "string", "bool", "double", "list", "return", "true", "false", "null", "public", "void", "using",
  "doc", "document", "element", "elements", "elementid", "sb", "append", "appendline", "tostring",
  "count", "value", "name", "get", "set", "add", "foreach", "where", "select", "tolist", "first",
  "firstordefault", "transaction", "start", "commit", "revit", "autodesk", "db", "filteredelementcollector",
  "wherepasses", "whereelementisnotelementtype", "ofcategory", "ofclass", "builtincategory",
  "parameter", "asdouble", "asstring", "asinteger", "length", "line", "point", "xyz", "level",
]);

// SPLIT camelCase AND snake_case BEFORE FILTERING, which is the difference between this working and
// not. `OfCategory` and `OST_DuctCurves` are single tokens to a naive splitter, so the words that
// actually say what the job IS - category, duct, curves - never survive to be matched. The first
// version of this file scored a textbook category filter at zero for exactly that reason.
function tokens(text) {
  const out = new Set();
  const words = String(text)
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")   // OfCategory -> Of Category
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2") // OSTDuct -> OST Duct
    .toLowerCase()
    .split(/[^a-z0-9]+/);
  for (const w of words) {
    if (w.length < 4 || STOP.has(w)) continue;
    out.add(w);
  }
  return out;
}

// WHAT THE CODE DOES, not what it says. This is the rule table that actually works, and the reason
// it exists is worth keeping: the first version scored fragments by shared vocabulary and got the
// textbook case WRONG. Asked about a duct-category filter it offered three insulation fragments,
// because `filter-by-category.cs` describes itself as "Every instance of one category" - the word
// "duct" appears nowhere in it. The right answer to a specific piece of code is usually the GENERIC
// fragment, and a generic fragment shares no vocabulary with specific code. Word overlap cannot
// bridge that; an API signature can.
//
// Each rule fires only when its `must` patterns are all present and none of `not` is, so a big
// composite script does not get told it is really a one-line filter. Keep this table SHORT and only
// add a rule after seeing the real mistake it would have caught.
const RULES = [
  {
    fragment: "filter-by-category.cs",
    why: "this is a plain category collector",
    must: [/OfCategory\s*\(\s*BuiltInCategory\./],
    not: [/NewDimension|OverrideGraphicSettings|\.Set\s*\(|Create\s*\(|Delete\s*\(|NewFamilyInstance/],
  },
  {
    fragment: "filter-by-id-list.cs",
    why: "this resolves a fixed list of ElementIds",
    must: [/new\s+ElementId\s*\(/, /(int\[\]|List<int>|long\[\]|List<long>)/],
    not: [/NewDimension|OverrideGraphicSettings/],
  },
  {
    fragment: "action-set-parameter-value.cs",
    why: "this writes a parameter onto each element",
    must: [/LookupParameter\s*\(|get_Parameter\s*\(/, /\.Set\s*\(/],
    not: [/NewDimension|OverrideGraphicSettings/],
  },
  {
    fragment: "action-set-color-uniform.cs",
    why: "this applies a graphic override",
    must: [/OverrideGraphicSettings/],
    not: [],
  },
  {
    fragment: "action-dimension-rooms.cs",
    why: "this creates dimensions",
    must: [/NewDimension\s*\(/],
    not: [],
  },
  {
    fragment: "action-center-room-tags.cs",
    why: "this moves room tags",
    must: [/RoomTag|IndependentTag/, /(ElementTransformUtils\.MoveElement|\.Location\s*as\s*LocationPoint|TagHeadPosition)/],
    not: [],
  },
];

function structuralMatch(code, fragments) {
  const hits = [];
  for (const rule of RULES) {
    if (!rule.must.every((re) => re.test(code))) continue;
    if (rule.not.some((re) => re.test(code))) continue;
    const f = fragments.find((x) => x.name === rule.fragment);
    if (f) hits.push({ f, why: rule.why, shared: 99, score: 99 });
  }
  return hits;
}

// Score a fragment against the code by shared distinctive words. Deliberately simple: the point is to
// surface an obvious "you already have this", not to be a search engine - the Brain has one of those.
function rank(code, fragments) {
  const codeWords = tokens(code);
  if (codeWords.size < 3) return [];
  const scored = [];
  for (const f of fragments) {
    const hay = tokens([f.name, f.purpose, f.produces, (f.inputs || []).map((i) => i.name).join(" ")].join(" "));
    let shared = 0;
    for (const w of codeWords) if (hay.has(w)) shared++;
    if (!shared) continue;
    // Normalise by the fragment's own vocabulary so a long rambling header cannot outrank a precise
    // short one purely by having more words to collide with.
    scored.push({ f, score: shared / Math.sqrt(hay.size || 1), shared });
  }
  return scored.sort((a, b) => b.score - a.score);
}

const PROVEN = new Set(["verified", "live-verified", "proven"]);

function adviseFor(code, fragments) {
  // Already composed from the library: the headers are still in the code. Nothing to say.
  if (/FRAGMENT\s*\([^)]*\)\s*[—-]\s*[\w.-]+\.cs/.test(code)) return null;

  // Structural rules only. The word-overlap ranker below is kept because it is useful for --explain
  // when tuning, but it does NOT drive the advice: it was measured giving confidently wrong answers,
  // and a hook that cries wolf is a hook that gets ignored - the exact fate of the Stop-hook version
  // this replaces. Silence is the correct output whenever the rules do not fire.
  const hits = structuralMatch(code, fragments);
  return hits.length ? hits.slice(0, 3) : null;
}

if (process.argv.includes("--selftest")) {
  // Replay the real log. This is the only honest way to judge the thing: it either would have caught
  // those 282 or it would not, and guessing which is exactly the habit this repo exists to break.
  const fragments = loadFragments();
  const logFile = path.join(here, "..", "job-log", "revit-runs.jsonl");
  let fresh = 0, advised = 0;
  const examples = [];
  for (const line of fs.readFileSync(logFile, "utf8").split("\n")) {
    if (!line.trim()) continue;
    let j; try { j = JSON.parse(line); } catch { continue; }
    if (j.tool !== "run_csharp") continue;
    if (Array.isArray(j.fragments) && j.fragments.length) continue;
    fresh++;
    // The log stores codeLines, not the code itself, so a full replay is impossible. Say so rather
    // than print a number that looks measured and is not.
  }
  console.log(`run_csharp calls written from nothing: ${fresh}`);
  console.log("The job log records codeLines, not the code, so the past cannot be replayed.");
  console.log("Judge this live instead: it logs every call it advises on to job-log/guard.jsonl.");
  process.exit(0);
}

let payload;
try {
  payload = JSON.parse(fs.readFileSync(0, "utf8") || "{}");
} catch {
  process.exit(0);                       // unreadable input must never block a tool call
}

const toolName = payload.tool_name ?? "";
if (!/run_csharp$/.test(toolName)) process.exit(0);
const code = payload.tool_input?.code;
if (typeof code !== "string" || code.length < 40) process.exit(0);

let top;
try {
  top = adviseFor(code, loadFragments());
} catch {
  process.exit(0);                       // a bug in here must never cost a Revit round trip
}
if (!top) process.exit(0);

const lines = top.map((r) => {
  const status = PROVEN.has(r.f.status) ? "proven on a real model" : String(r.f.status || "unproven");
  return `  ${r.f.name}  (${status}) - ${String(r.f.purpose || "").slice(0, 120)}`;
});

const note = [
  "STOP AND CHECK BEFORE SENDING THIS. You are about to run hand-written C# against the model, and",
  "the library may already cover it. Closest existing fragments:",
  "",
  ...lines,
  "",
  "If one of these fits, run it with run_fragment instead - it is already proven against a real",
  "document, and this code is not. If none fits, carry on: this is advice, not a refusal.",
].join("\n");

// Record what it advised, so its usefulness can be judged from evidence later rather than argued.
try {
  fs.appendFileSync(
    path.join(here, "..", "job-log", "guard.jsonl"),
    JSON.stringify({
      date: new Date().toISOString().slice(0, 10),
      codeLines: code.split("\n").length,
      suggested: top.map((r) => r.f.name),
    }) + "\n",
  );
} catch { /* logging must never break the hook */ }

process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    additionalContext: note,
  },
}));
