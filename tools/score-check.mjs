#!/usr/bin/env node
// Stop hook - re-score retrieval whenever something that can change ranking has changed,
// and shout if the score went DOWN.
//
// WHY: semantic-index/score_brain.py existed all day and nothing ever ran it. It caught three
// real problems on 2026-08-13 - a discount that broke a different question, a theory that was
// wrong, and a regression nobody had noticed for a week - but every one of those was caught
// because a person happened to run it. A measuring tape nobody picks up measures nothing.
//
// So this watches the five files that can move a ranking and scores automatically when one of
// them changes. It is the same discipline applied by hand three times today, made automatic:
// change a weight, get a number back, unprompted.
//
// It does NOT block anything. A regression is reported, never enforced - sometimes a drop is
// the accepted cost of a fix, and that is Ajmal's call, not a hook's.
//
//   node tools/score-check.mjs           run if the watched files changed
//   node tools/score-check.mjs --force   score now regardless
//
// Always exits 0.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const semanticRoot = path.join(root, "semantic-index");
const historyFile = path.join(semanticRoot, "score-history.md");
const fingerprintFile = path.join(semanticRoot, "run-temp", ".score-fingerprint");

// Everything that can change what comes back for a question. Ranking logic, chunking rules,
// the query-expansion table, and the questions themselves.
const WATCHED = [
  "semantic-index/brain_search_hybrid.py",
  "semantic-index/brain_common.py",
  "semantic-index/brain_index.py",
  "semantic-index/test-questions.md",
  "knowledge/site-vocabulary.md",
];

function fingerprint() {
  const h = crypto.createHash("sha256");
  for (const rel of WATCHED) {
    const p = path.join(root, rel);
    h.update(rel);
    // Normalise line endings: git converts LF to CRLF on checkout here, and a fingerprint that
    // changes on checkout would cry wolf on every branch switch.
    h.update(fs.existsSync(p) ? fs.readFileSync(p, "utf8").replace(/\r\n/g, "\n") : "MISSING");
  }
  return h.digest("hex");
}

function venvPython() {
  return [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ].find((p) => fs.existsSync(p));
}

function lastScore() {
  if (!fs.existsSync(historyFile)) return null;
  const lines = fs
    .readFileSync(historyFile, "utf8")
    .split("\n")
    .filter((l) => l.startsWith("- "));
  if (!lines.length) return null;
  const m = lines[lines.length - 1].match(/(\d+)\/(\d+) at #1/);
  return m ? { at1: Number(m[1]), total: Number(m[2]) } : null;
}

const force = process.argv.includes("--force");
const current = fingerprint();

let stored = "";
try {
  stored = fs.readFileSync(fingerprintFile, "utf8").trim();
} catch {
  // No fingerprint yet. Record the current one and score once, so there is a baseline.
}

if (!force && stored === current) process.exit(0);

const python = venvPython();
if (!python) process.exit(0);

const before = lastScore();

const result = spawnSync(python, [path.join(semanticRoot, "score_brain.py")], {
  encoding: "utf8",
  cwd: semanticRoot,
  timeout: 300000,
});

// Record the fingerprint even on failure, so a broken scorer does not re-run every single turn.
try {
  fs.mkdirSync(path.dirname(fingerprintFile), { recursive: true });
  fs.writeFileSync(fingerprintFile, current, "utf8");
} catch {
  /* not worth reporting */
}

if (result.error || result.status !== 0) {
  console.error(
    `Retrieval score could not be taken after a ranking-related change:\n` +
      `${result.error ? result.error.message : (result.stderr || "").trim()}`
  );
  process.exit(0);
}

const m = (result.stdout || "").match(/#1 correct\s+(\d+)\s*\/\s*(\d+)/);
if (!m) process.exit(0);

const after = { at1: Number(m[1]), total: Number(m[2]) };

// Only compare like with like: adding a question changes the denominator, and that is not a
// regression. Comparing 3/5 against 2/4 would raise a false alarm on the day a question is added.
if (before && before.total === after.total && after.at1 < before.at1) {
  console.error(
    `\nRETRIEVAL SCORE DROPPED: ${before.at1}/${before.total} -> ${after.at1}/${after.total} at #1.\n` +
      `A file that affects ranking changed and fewer questions now return the right answer first.\n` +
      `Run  semantic-index\\score-brain.cmd  to see which ones, and decide whether the drop is\n` +
      `an accepted cost or a mistake. Nothing has been blocked or reverted.\n`
  );
} else if (before && before.total !== after.total) {
  console.error(
    `\nRetrieval re-scored after a ranking change: now ${after.at1}/${after.total} at #1 ` +
      `(previously ${before.at1}/${before.total} — different question count, so not comparable).\n`
  );
}

process.exit(0);
