#!/usr/bin/env node
// Turn the 267 C# fragments into something you can LOOK UP instead of READ.
//
// WHY THIS EXISTS: the library's own rule is "reuse an existing module when it fits; only write new code
// when nothing does" (AGENT-SPEC §2.4, and the live-model skill's decision order). That rule is right,
// and it is also the rule most easily broken by accident: today the only way to apply it is to read
// scripts/README.md end to end - 500+ lines of prose tables - and decide. When that read feels
// expensive, an agent writes fresh C# for a job a proven fragment already covered. The fragment was
// never missing; it was just hard to find.
//
// So this reads the fragments themselves and answers the questions that actually get asked:
//   "is there something for ducts already?"   "which of these are actually proven?"
//   "what do I have to fill in to use it?"
//
// COMPUTED FROM DISK EVERY RUN, STORED NOWHERE - the same rule brain-status.mjs follows, for the same
// reason: a checked-in index of a folder that changes is a lie waiting to happen (README said 8 skills
// against 9; AGENT-SPEC said 206 fragments against 264).
//
// Usage:
//   node tools/fragment-index.mjs                       counts by kind, and parse coverage
//   node tools/fragment-index.mjs --find duct           search name + purpose + inputs
//   node tools/fragment-index.mjs --find workset --verified     only proven ones
//   node tools/fragment-index.mjs --unverified          everything not yet run
//   node tools/fragment-index.mjs --show recipes/place-fcu.cs   one fragment, with its input fields
//   node tools/fragment-index.mjs --json                machine-readable, for another tool to consume

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const brainRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const scriptsDir = path.join(brainRoot, "scripts");
const argv = process.argv.slice(2);
const flag = (n) => argv.includes(n);
const valueOf = (n) => { const i = argv.indexOf(n); return i >= 0 ? argv[i + 1] : null; };

function walk(dir, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full, results);
    else if (e.name.endsWith(".cs")) results.push(full);
  }
  return results;
}

// --- header fields ---------------------------------------------------------------------------------
// A field runs from "// FIELD:" until the next "// FIELD:" or a ==== divider. The continuation lines are
// indented under it, which is why this cannot just take the single matching line.
function headerField(lines, name) {
  const start = lines.findIndex((l) => new RegExp(`^//\\s*${name}:`).test(l));
  if (start === -1) return null;
  const out = [lines[start].replace(new RegExp(`^//\\s*${name}:\\s*`), "")];
  for (let i = start + 1; i < lines.length; i++) {
    const l = lines[i];
    if (!l.startsWith("//")) break;
    if (/^\/\/\s*={5,}/.test(l)) break;
    // Next field. The separator is not always a colon: "NOT STANDALONE — see scripts/README.md" uses an
    // em dash, and matching only ":" let it bleed into the end of PRODUCES on every filter fragment.
    // Continuation lines never match this, because they start with ordinary mixed-case prose.
    if (/^\/\/\s*[A-Z][A-Z0-9 _]{2,}\s*(?::|—|-)/.test(l)) break;
    out.push(l.replace(/^\/\/\s*/, ""));
  }
  return out.join(" ").replace(/\s+/g, " ").trim();
}

// --- INPUTS block ----------------------------------------------------------------------------------
// These are the "form fields": what a caller must supply to use the fragment. Parsing them is the whole
// point - it turns "read this file and work out what it needs" into a list.
function parseInputs(src) {
  const m = src.match(/----\s*INPUTS[\s\S]*?----\s*\n([\s\S]*?)\/\/\s*----\s*END INPUTS/);
  if (!m) return [];
  const out = [];
  for (const raw of m[1].split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("//")) continue;
    const d = line.match(/^([A-Za-z_][\w<>,?\[\]. ]*?)\s+([A-Za-z_]\w*)\s*=\s*(.+?);\s*(?:\/\/\s*(.*))?$/);
    if (d) out.push({ type: d[1].trim(), name: d[2], default: d[3].trim(), note: (d[4] || "").trim() });
  }
  return out;
}

// --- verification status ---------------------------------------------------------------------------
// Read off scripts/README.md's own per-fragment markers - the convention the library already maintains
// by hand. A row with no marker either way is the honest "never run, never flagged" case.
const readme = fs.existsSync(path.join(scriptsDir, "README.md"))
  ? fs.readFileSync(path.join(scriptsDir, "README.md"), "utf8")
  : "";
const readmeRows = readme.split(/\r?\n/).filter((l) => l.startsWith("| [`"));

function statusOf(rel) {
  // Match the row whose markdown LINK TARGET is this fragment - `](path)` - not merely any row that
  // mentions the path. 8 fragments are named inside a DIFFERENT fragment's row as prose ("feeds
  // creators/create-dimension.cs", "paired with action-import-parameters-from-csv.cs"), and a plain
  // .includes() picked up whichever row came first, so those 8 inherited a neighbour's verification
  // status. That is the exact failure this whole tool exists to prevent: confidently reported, wrong.
  const row = readmeRows.find((l) => l.includes(`](${rel})`));
  if (!row) return "not-in-readme";
  if (/NOT yet live-verified/.test(row)) return "untested";
  if (/verified 2026/.test(row)) return "verified";
  if (/CONFIRMED IMPOSSIBLE/.test(row)) return "impossible";
  if (/BLOCKED/.test(row)) return "blocked";
  return "no-status";
}

// --- build -----------------------------------------------------------------------------------------
const fragments = walk(scriptsDir).map((full) => {
  const rel = path.relative(scriptsDir, full).split(path.sep).join("/");
  const src = fs.readFileSync(full, "utf8");
  const lines = src.split(/\r?\n/);
  const parts = rel.split("/");
  return {
    path: rel,
    kind: parts[0],
    group: parts.length > 2 ? parts[1] : null,
    name: path.basename(rel),
    purpose: headerField(lines, "PURPOSE") || "",
    assumes: headerField(lines, "ASSUMES") || "",
    produces: headerField(lines, "PRODUCES") || "",
    gotchas: lines.filter((l) => /^\/\/\s*GOTCHA/.test(l)).length,
    inputs: parseInputs(src),
    status: statusOf(rel),
  };
}).sort((a, b) => a.path.localeCompare(b.path));

const STATUS_LABEL = {
  verified: "PROVEN", untested: "not run", blocked: "blocked",
  impossible: "impossible", "no-status": "unproven", "not-in-readme": "?",
};

// --- output ----------------------------------------------------------------------------------------
// NOTE: this section deliberately uses `return` and `process.exitCode`, never `process.exit()`. On a
// pipe (`| head`, `| python3`, a tool consuming --json) Node's stdout is asynchronous, and process.exit()
// kills the process before the buffer flushes - which silently truncated the --json output mid-string
// the first time this was tested through a pipe. Setting exitCode and returning lets Node flush first.
function main() {
if (flag("--json")) {
  console.log(JSON.stringify(fragments, null, 2));
  return;
}

const showPath = valueOf("--show");
if (showPath) {
  const f = fragments.find((x) => x.path === showPath || x.name === showPath || x.path.endsWith("/" + showPath));
  if (!f) { console.log(`No fragment matching '${showPath}'.`); process.exitCode = 1; return; }
  console.log(`${f.path}   [${STATUS_LABEL[f.status]}]`);
  console.log(`\n${f.purpose}\n`);
  if (f.assumes) console.log(`NEEDS FIRST : ${f.assumes}`);
  if (f.produces) console.log(`GIVES BACK  : ${f.produces}`);
  if (f.gotchas) console.log(`GOTCHAS     : ${f.gotchas} recorded in the file — read them before running`);
  if (f.inputs.length) {
    console.log(`\nFILL IN (${f.inputs.length}) — never reuse a past job's values:`);
    for (const i of f.inputs) {
      console.log(`  ${i.name.padEnd(24)} ${i.type.padEnd(18)} e.g. ${i.default}`);
      if (i.note) console.log(`  ${"".padEnd(24)} ${i.note}`);
    }
  } else {
    console.log("\nFILL IN     : nothing — this fragment takes no inputs.");
  }
  return;
}

let hits = fragments;
const term = valueOf("--find");
if (term) {
  const t = term.toLowerCase();
  hits = hits.filter((f) =>
    f.path.toLowerCase().includes(t) ||
    f.purpose.toLowerCase().includes(t) ||
    f.inputs.some((i) => (i.name + " " + i.note).toLowerCase().includes(t)));
}
if (flag("--verified")) hits = hits.filter((f) => f.status === "verified");
if (flag("--unverified")) hits = hits.filter((f) => f.status === "no-status" || f.status === "untested");

if (term || flag("--verified") || flag("--unverified")) {
  if (!hits.length) {
    console.log("Nothing matched.");
    console.log("That is a real answer: if no fragment fits, write the smallest correct one — then save it.");
    return;
  }
  console.log(`${hits.length} fragment(s):\n`);
  for (const f of hits) {
    const short = f.purpose.length > 96 ? f.purpose.slice(0, 96) + "…" : f.purpose;
    console.log(`  [${STATUS_LABEL[f.status].padEnd(10)}] ${f.path}`);
    console.log(`               ${short}`);
    if (f.inputs.length) console.log(`               fill in: ${f.inputs.map((i) => i.name).join(", ")}`);
    console.log("");
  }
  console.log(`Detail: node tools/fragment-index.mjs --show <path>`);
  return;
}

// Default view: the shape of the library, and how much of it this tool could actually read.
const byKind = {};
for (const f of fragments) (byKind[f.kind] ||= []).push(f);
const byStatus = {};
for (const f of fragments) byStatus[f.status] = (byStatus[f.status] || 0) + 1;

console.log(`AJ AI Brain — fragment index (${fragments.length} fragments, computed from disk)\n`);
for (const [kind, list] of Object.entries(byKind).sort()) {
  console.log(`  ${kind.padEnd(10)} ${String(list.length).padStart(3)}`);
}
console.log(`\nProven status:`);
for (const [s, n] of Object.entries(byStatus).sort((a, b) => b[1] - a[1])) {
  console.log(`  ${STATUS_LABEL[s].padEnd(12)} ${String(n).padStart(3)}`);
}
const withInputs = fragments.filter((f) => f.inputs.length).length;
const withPurpose = fragments.filter((f) => f.purpose).length;
console.log(`\nParsed: ${withPurpose}/${fragments.length} purposes, ${withInputs}/${fragments.length} with input fields`);
console.log(`\n  node tools/fragment-index.mjs --find <word> [--verified] | --show <path> | --json`);
console.log(`  Rule this serves: reuse a fragment when one fits; only write new C# when none does.`);
}

main();
