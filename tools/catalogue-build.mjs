#!/usr/bin/env node
// Regenerate docs/fragment-catalogue.md from the fragments on disk.
//
// WHY THIS EXISTS: the catalogue was written by hand on 2026-08-23 and was already stale a day later —
// it said "359 fragments" against 366 on disk. It is exactly the failure this repo keeps having (README
// said 8 skills against 9; AGENT-SPEC said 206 fragments against 264), and the reason
// brain-status.mjs and fragment-index.mjs compute from disk every run instead of storing a summary.
//
// A catalogue is the one case where a stored file is still wanted — it lives in `docs/`, outside the
// search index, and is meant to be READ end to end, which a command's output is not. So the answer is
// not "stop keeping it", it is "stop writing it by hand". Run this after adding or retiring fragments:
//
//     node tools/catalogue-build.mjs
//
// It reads `node tools/fragment-index.mjs --json`, which parses each fragment's own header and pairs it
// with that fragment's row in scripts/README.md — the single source of truth for verification status.

import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");
const out = join(root, "docs", "fragment-catalogue.md");

const frags = JSON.parse(
  execFileSync(process.execPath, [join(here, "fragment-index.mjs"), "--json"], {
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
  })
);

const ICON = {
  verified: "✅",
  untested: "⚠️",
  "no-status": "❓",
  blocked: "⛔",
  impossible: "🚫",
};
const LABEL = {
  verified: "✅ PROVEN",
  untested: "⚠️ not run",
  "no-status": "❓ unproven",
  blocked: "⛔ blocked",
  impossible: "🚫 impossible",
};

// One line per fragment, trimmed — the catalogue is for scanning, not for reading a whole header.
const trim = (s, n) => {
  const flat = (s || "").replace(/\s+/g, " ").trim();
  if (!flat) return "(no purpose line in the header)";
  return flat.length <= n ? flat : flat.slice(0, n).replace(/[ ,;.]+$/, "") + "...";
};

const counts = {};
for (const f of frags) counts[f.status] = (counts[f.status] || 0) + 1;

// Today's date is stamped, because a catalogue with no date cannot be judged stale.
const stamp = new Date().toISOString().slice(0, 10);

const L = [];
L.push("# AJ AI Brain — every fragment, and what it does");
L.push("");
L.push(
  `**${frags.length} fragments**, generated from the files on ${stamp}. Status comes from each fragment's row in ` +
    "`scripts/README.md`, which is this repo's single source of truth for it."
);
L.push("");
L.push(
  "**Generated — do not edit by hand.** Run `node tools/catalogue-build.mjs` after adding or retiring a " +
    "fragment. The hand-written version of this file was stale within a day of being written."
);
L.push("");
L.push("✅ proven on a real model · ⚠️ written, not yet run · ❓ no status recorded · ⛔ blocked · 🚫 impossible on this Revit");
L.push("");
L.push("| Status | Count |");
L.push("|---|---|");
for (const k of ["verified", "untested", "no-status", "blocked", "impossible"]) {
  if (counts[k]) L.push(`| ${LABEL[k]} | ${counts[k]} |`);
}
L.push("");

// Group by folder, in the same order the scripts/ tree reads.
const groups = new Map();
for (const f of frags) {
  const key = f.group ? `${f.kind}/${f.group}` : f.kind;
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push(f);
}
for (const key of [...groups.keys()].sort()) {
  const list = groups.get(key).sort((a, b) => a.name.localeCompare(b.name));
  L.push(`## ${key}  *(${list.length})*`);
  L.push("");
  L.push("| | Fragment | What it does |");
  L.push("|---|---|---|");
  for (const f of list) {
    L.push(`| ${ICON[f.status] || "❓"} | \`${f.name}\` | ${trim(f.purpose, 190).replace(/\|/g, "\\|")} |`);
  }
  L.push("");
}

writeFileSync(out, L.join("\n"), "utf8");
console.log(`docs/fragment-catalogue.md rebuilt — ${frags.length} fragments, ${groups.size} folders.`);
