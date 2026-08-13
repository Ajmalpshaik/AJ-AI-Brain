#!/usr/bin/env node
// Read job-log/ and answer the questions nothing else in this Brain can answer:
//
//   node tools/job-report.mjs            what has actually been used
//   node tools/job-report.mjs --unused   fragments that have never once run on a real job
//
// The point is pruning. 269 fragments is a library or a hoard, and until now there was no way
// to tell which. An unused fragment is NOT free: it competes in every search, so it is one more
// thing the right answer can lose to. But nothing should be deleted on a guess - this is the
// evidence that makes deleting safe, and it needs months of real use before it means anything.
//
// Reads only. Touches nothing.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const logDir = path.join(root, "job-log");

function readJsonl(file) {
  if (!fs.existsSync(file)) return [];
  return fs
    .readFileSync(file, "utf8")
    .split("\n")
    .filter((l) => l.trim())
    .map((l) => {
      try {
        return JSON.parse(l);
      } catch {
        return null;
      }
    })
    .filter(Boolean);
}

function allFragments() {
  const out = [];
  const walk = (dir) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) walk(p);
      else if (e.name.endsWith(".cs")) out.push(e.name);
    }
  };
  walk(path.join(root, "scripts"));
  return out;
}

const questions = readJsonl(path.join(logDir, "questions.jsonl"));
const runs = readJsonl(path.join(logDir, "revit-runs.jsonl"));
const wantUnused = process.argv.includes("--unused");

// How often each file has been a top hit for a real question.
const hitCounts = new Map();
for (const q of questions) {
  for (const h of q.hits || []) {
    hitCounts.set(h.path, (hitCounts.get(h.path) || 0) + 1);
  }
}

// Which fragments have actually run against Revit.
const ranCounts = new Map();
for (const r of runs) {
  for (const f of r.fragments || []) {
    ranCounts.set(f, (ranCounts.get(f) || 0) + 1);
  }
}

if (wantUnused) {
  const fragments = allFragments();
  const never = fragments.filter((f) => !ranCounts.has(f)).sort();
  console.log(`Fragments never recorded running on a real job: ${never.length} of ${fragments.length}`);
  console.log(
    `\nTreat this as "no evidence yet", NOT "dead". The log started 2026-08-13, so a fragment`
  );
  console.log(`used heavily before then still shows here. Give it months before pruning anything.\n`);
  for (const f of never.slice(0, 40)) console.log(`  ${f}`);
  if (never.length > 40) console.log(`  ... and ${never.length - 40} more`);
  process.exit(0);
}

console.log("AJ AI Brain — job log report\n");
console.log(`  questions asked   ${questions.length}`);
console.log(`  Revit tool calls  ${runs.length}`);
console.log(`  fragments run     ${ranCounts.size}`);

if (questions.length === 0 && runs.length === 0) {
  console.log(`\nNothing recorded yet. The log starts filling from the next question asked.`);
  process.exit(0);
}

if (hitCounts.size) {
  console.log(`\n  Files most often returned for a real question:`);
  [...hitCounts.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
    .forEach(([p, c]) => console.log(`    ${String(c).padStart(4)}  ${p}`));
}

if (ranCounts.size) {
  console.log(`\n  Fragments actually run against Revit:`);
  [...ranCounts.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
    .forEach(([f, c]) => console.log(`    ${String(c).padStart(4)}  ${f}`));
} else {
  console.log(`\n  No Revit runs recorded yet — needs a live bridge session.`);
}

console.log(`\n  node tools/job-report.mjs --unused   to see what has never run`);
