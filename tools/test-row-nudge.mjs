#!/usr/bin/env node
// Stop hook - say out loud which real questions Ajmal asked that are NOT yet test rows.
//
// WHY THIS EXISTS: on 2026-08-13 the assistant told Ajmal it would record every question he
// asked as a test row "as it comes", and then failed to do it seven times in a single working
// session. He had to point it out. The questions were all being logged to job-log/ the whole
// time - what was missing was anything that made the gap VISIBLE.
//
// So this is the same lesson as tools/reindex-run.mjs and tools/score-check.mjs, a third time:
// a step that depends on remembering is a step that will be skipped. Make it impossible to
// not notice instead of promising harder.
//
// It reports, never blocks and never writes. Choosing the expected answer for a question is a
// judgement call - and Ajmal's, not a hook's.
//
//   node tools/test-row-nudge.mjs           uncaptured questions, newest first
//   node tools/test-row-nudge.mjs --all     no cap on how many are listed
//
// Always exits 0.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const logFile = path.join(root, "job-log", "questions.jsonl");
const testFile = path.join(root, "semantic-index", "test-questions.md");

const SHOW = process.argv.includes("--all") ? Infinity : 6;

// Questions that are about the Brain itself rather than Revit work. They are real questions,
// but they are not what this test set measures, and listing them every turn trains the reader
// to ignore the whole message.
const META = /(test question|score|rag|embedding|re-?rank|brain|phase \d|commit|push|merge|how meny quastain|how many question)/i;

function norm(s) {
  return (s || "").toLowerCase().replace(/\s+/g, " ").trim();
}

try {
  if (!fs.existsSync(logFile) || !fs.existsSync(testFile)) process.exit(0);

  const captured = new Set();
  for (const line of fs.readFileSync(testFile, "utf8").split("\n")) {
    const t = line.trim();
    if (!t.startsWith("|")) continue;
    const cells = t.slice(1, -1).split("|").map((c) => c.trim());
    if (cells.length === 2 && cells[1].includes("/")) captured.add(norm(cells[0]));
  }

  const seen = new Set();
  const missing = [];
  const lines = fs.readFileSync(logFile, "utf8").split("\n").filter((l) => l.trim());

  for (let i = lines.length - 1; i >= 0; i--) {
    let entry;
    try {
      entry = JSON.parse(lines[i]);
    } catch {
      continue;
    }
    const q = (entry.question || "").trim();
    const key = norm(q);
    if (!q || seen.has(key) || captured.has(key) || META.test(q)) continue;
    seen.add(key);
    missing.push(q);
  }

  if (!missing.length) process.exit(0);

  const shown = missing.slice(0, SHOW);
  const lines_out = [
    ``,
    `${missing.length} question(s) asked but not yet in semantic-index/test-questions.md:`,
    ...shown.map((q) => `   "${q.length > 76 ? q.slice(0, 76) + "..." : q}"`),
  ];
  if (missing.length > shown.length) {
    lines_out.push(`   ... and ${missing.length - shown.length} more (--all to list)`);
  }
  lines_out.push(
    `Add the ones that were real Revit work, with the file that SHOULD have answered.`,
    ``
  );
  console.error(lines_out.join("\n"));
} catch {
  // A nudge that fails must never disturb the work it is nudging about.
}

process.exit(0);
