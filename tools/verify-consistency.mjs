#!/usr/bin/env node
// Portable (cross-platform, no PowerShell needed) equivalent of verify-consistency.ps1 — for sessions
// running on Linux/macOS/Claude Code on the web, where `pwsh`/Windows PowerShell isn't available.
//
// Same six checks, same job: catch drift in this Brain's cross-references before it goes stale.
// Kept in sync by hand with verify-consistency.ps1 — if you change what one checks, change both.
//
// Usage: node tools/verify-consistency.mjs

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const brainRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const issues = [];

function walk(dir, filterFn, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, filterFn, results);
    else if (filterFn(entry.name)) results.push(full);
  }
  return results;
}

function getMarkdownLinkTargets(content) {
  const targets = [];
  for (const m of content.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
    const target = m[1];
    if (/^https?:\/\//.test(target) || target.startsWith("#")) continue;
    targets.push(target.split("#")[0]);
  }
  return targets;
}

// === 1. Skill frontmatter ===
console.log("=== 1. Skill frontmatter ===");
const skillsDir = path.join(brainRoot, "skills");
const skillFiles = fs.existsSync(skillsDir)
  ? fs.readdirSync(skillsDir, { withFileTypes: true })
      .filter((e) => e.isDirectory())
      .map((e) => path.join(skillsDir, e.name, "SKILL.md"))
      .filter((p) => fs.existsSync(p))
  : [];

for (const skillPath of skillFiles) {
  const content = fs.readFileSync(skillPath, "utf8");
  const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!match) {
    issues.push(`MISSING FRONTMATTER: ${skillPath}`);
    continue;
  }
  const frontmatter = match[1];
  if (!/^name:\s*\S+/m.test(frontmatter)) issues.push(`MISSING name in frontmatter: ${skillPath}`);
  if (!/^description:\s*\S+/m.test(frontmatter)) issues.push(`MISSING description in frontmatter: ${skillPath}`);
}
console.log(`Checked ${skillFiles.length} skill file(s).`);

// === 2. Markdown link targets ===
console.log("\n=== 2. Markdown link targets ===");
const mdFiles = [
  ...walk(path.join(brainRoot, "skills"), (n) => n.endsWith(".md")),
  ...walk(path.join(brainRoot, "knowledge"), (n) => n.endsWith(".md")),
  ...walk(path.join(brainRoot, "scripts"), (n) => n.endsWith(".md")),
];
for (const f of ["START-HERE.md", "SETUP.md", "AGENT-SPEC.md", "README.md", "CLAUDE.md"]) {
  const p = path.join(brainRoot, f);
  if (fs.existsSync(p)) mdFiles.push(p);
}
const toolsReadme = path.join(brainRoot, "mcp-server", "tools", "README.md");
if (fs.existsSync(toolsReadme)) mdFiles.push(toolsReadme);

let linkCount = 0;
for (const md of mdFiles) {
  const content = fs.readFileSync(md, "utf8");
  for (const target of getMarkdownLinkTargets(content)) {
    linkCount++;
    const resolved = path.join(path.dirname(md), target);
    if (!fs.existsSync(resolved)) {
      issues.push(`BROKEN LINK in ${md}: '${target}' -> ${resolved}`);
    }
  }
}
console.log(`Checked ${linkCount} link(s) across ${mdFiles.length} markdown file(s).`);

// === 3. Scripts README vs folder contents ===
console.log("\n=== 3. Scripts README vs folder contents ===");
const scriptsDir = path.join(brainRoot, "scripts");
const readmePath = path.join(scriptsDir, "README.md");
// Recursive (unlike the original ps1's per-subfolder, non-recursive listing, which went stale the
// moment scripts/actions/ was reorganized into job-grouped subfolders — see brain-log.md 2026-07-22.
// Paths below are always the FULL path relative to scripts/, subfolders included, so a nested file like
// actions/color-graphics/action-x.cs is checked against that exact string, not a flattened one.
const topLevelBuckets = ["filters", "actions", "recipes", "creators", "commands", "examples", "context"];

if (fs.existsSync(readmePath)) {
  const readmeContent = fs.readFileSync(readmePath, "utf8");

  const onDisk = [];
  for (const bucket of topLevelBuckets) {
    const bucketPath = path.join(scriptsDir, bucket);
    for (const full of walk(bucketPath, (n) => n.endsWith(".cs"))) {
      onDisk.push(path.relative(scriptsDir, full).split(path.sep).join("/"));
    }
  }

  for (const rel of onDisk) {
    if (!readmeContent.includes(rel)) {
      issues.push(`SCRIPT NOT IN README: ${rel} exists on disk but isn't mentioned in scripts/README.md`);
    }
  }

  const bucketAlternation = topLevelBuckets.join("|");
  const refRegex = new RegExp(`\\(((?:${bucketAlternation})/[^)]+\\.cs)\\)`, "g");
  for (const m of readmeContent.matchAll(refRegex)) {
    const ref = m[1];
    if (!fs.existsSync(path.join(scriptsDir, ref))) {
      issues.push(`README REFERENCES MISSING SCRIPT: '${ref}' listed in README.md but not found on disk`);
    }
  }

  console.log(`Checked ${onDisk.length} script file(s) on disk against README.md.`);
} else {
  issues.push("MISSING FILE: scripts/README.md not found");
}

// === 4. Skill coverage in the entry documents ===
// Adding a skill folder is not enough for anyone — human or agent — to find it. README.md's table and
// the plugin manifest's description are what a reader/installer sees first, and neither is covered by
// check 2 (a link that resolves says nothing about a skill that was never linked at all). The fire
// sprinkler skill sat invisible in all of them for a week before this check existed.
console.log("\n=== 4. Skill coverage in entry documents ===");
const skillNames = skillFiles.map((p) => path.basename(path.dirname(p)));

// Only README.md and START-HERE.md are checked by NAME: both route using the real folder name, so an
// exact-string check is meaningful there. plugin.json/marketplace.json describe skills in prose ("HVAC
// terminal layout"), which no string check can verify — those are covered by the count check below.
const coverageTargets = [
  { file: "README.md", label: "README.md skills table" },
  { file: "START-HERE.md", label: "START-HERE.md routing table" },
];
for (const { file, label } of coverageTargets) {
  const p = path.join(brainRoot, file);
  if (!fs.existsSync(p)) continue;
  const content = fs.readFileSync(p, "utf8");
  for (const name of skillNames) {
    if (!content.includes(name)) {
      issues.push(`SKILL NOT LISTED: '${name}' exists in skills/ but isn't named in ${label} (${file})`);
    }
  }
}

// Any literal "N skills" claim in the entry docs must equal what's actually on disk. brain-log.md is
// deliberately excluded — its old counts are dated historical record, not claims about today.
for (const file of ["README.md", "SETUP.md", path.join(".claude-plugin", "plugin.json")]) {
  const p = path.join(brainRoot, file);
  if (!fs.existsSync(p)) continue;
  const content = fs.readFileSync(p, "utf8");
  for (const m of content.matchAll(/(\d+)\s+skills\b/g)) {
    if (Number(m[1]) !== skillNames.length) {
      issues.push(`SKILL COUNT DRIFT in ${file}: says "${m[0]}" but skills/ holds ${skillNames.length}`);
    }
  }
}
console.log(`Checked ${skillNames.length} skill(s) against ${coverageTargets.length} entry document(s).`);

// === 5. AGENT-SPEC fragment counts vs disk ===
// AGENT-SPEC.md advertises itself as the complete self-contained reference, so a stale fragment count
// there is a wrong answer, not a cosmetic nit — it drifted to 206-vs-264 (58 fragments unaccounted for)
// before this check existed.
console.log("\n=== 5. AGENT-SPEC fragment counts ===");
const specPath = path.join(brainRoot, "AGENT-SPEC.md");
if (fs.existsSync(specPath)) {
  const flat = fs.readFileSync(specPath, "utf8").replace(/\s+/g, " ");
  const countRe =
    /(\d+) real C# fragments in `scripts\/` \((\d+) filters, (\d+) actions, (\d+) creators, (\d+) commands, (\d+) recipes, (\d+) examples, (\d+) read-only/;
  const m = flat.match(countRe);
  if (!m) {
    issues.push(
      "AGENT-SPEC COUNT SENTENCE NOT FOUND: §3.5's fragment-count sentence was reworded — re-anchor this check in tools/verify-consistency.* or the counts go unchecked",
    );
  } else {
    const claimed = {
      total: Number(m[1]), filters: Number(m[2]), actions: Number(m[3]), creators: Number(m[4]),
      commands: Number(m[5]), recipes: Number(m[6]), examples: Number(m[7]), context: Number(m[8]),
    };
    const actual = { total: 0 };
    for (const bucket of topLevelBuckets) {
      actual[bucket] = walk(path.join(scriptsDir, bucket), (n) => n.endsWith(".cs")).length;
      actual.total += actual[bucket];
    }
    for (const key of Object.keys(claimed)) {
      if (claimed[key] !== actual[key]) {
        issues.push(`AGENT-SPEC COUNT DRIFT (§3.5): claims ${claimed[key]} ${key}, disk has ${actual[key]}`);
      }
    }
    console.log(`Checked 8 fragment-count claim(s) in AGENT-SPEC.md §3.5 against disk.`);
  }
} else {
  issues.push("MISSING FILE: AGENT-SPEC.md not found");
}

// === 6. Text encoding (mojibake) ===
// Windows PowerShell 5.1 reads UTF-8-without-BOM as ANSI, so a scripted read-modify-write double-encodes
// every non-ASCII character in the file (see CLAUDE.md). That corrupted 41 files once, and a survivor
// went unnoticed for months inside a C# string literal — s.Replace("<corrupted ø>", "") silently matched
// nothing, so every round duct size sorted as 0. Cheap to detect, expensive to find by eye.
console.log("\n=== 6. Text encoding ===");
// The patterns are DERIVED, not hand-typed: each real character below is UTF-8 encoded, then each byte
// is decoded as cp1252 — literally simulating the corruption — so the list can never drift out of step
// with what it claims to catch. It also keeps this file free of corrupted bytes, so the check doesn't
// flag its own pattern list (a hand-typed list does exactly that).
const CP1252_HIGH = {
  0x80: 0x20ac, 0x82: 0x201a, 0x83: 0x0192, 0x84: 0x201e, 0x85: 0x2026, 0x86: 0x2020, 0x87: 0x2021,
  0x88: 0x02c6, 0x89: 0x2030, 0x8a: 0x0160, 0x8b: 0x2039, 0x8c: 0x0152, 0x8e: 0x017d, 0x91: 0x2018,
  0x92: 0x2019, 0x93: 0x201c, 0x94: 0x201d, 0x95: 0x2022, 0x96: 0x2013, 0x97: 0x2014, 0x98: 0x02dc,
  0x99: 0x2122, 0x9a: 0x0161, 0x9b: 0x203a, 0x9c: 0x0153, 0x9e: 0x017e, 0x9f: 0x0178,
};

function mojibakeOf(ch) {
  let out = "";
  for (const b of Buffer.from(ch, "utf8")) {
    if (b < 0x80) out += String.fromCharCode(b);
    else if (CP1252_HIGH[b] !== undefined) out += String.fromCharCode(CP1252_HIGH[b]);
    else if (b >= 0xa0) out += String.fromCharCode(b);
    else return null; // 0x81/0x8d/0x8f/0x90/0x9d are undefined in cp1252 — no stable corrupted form
  }
  return out;
}

// Every non-ASCII character this Brain actually uses in prose, headers, tables and C# literals.
// Add to this string when a genuinely new one starts being used.
const USED_NON_ASCII = "—–‘’“”…↔↓↑→←✓✔✗•§°²³±«»·½¼ø×÷≈≥≤™€éèáíóúüñçâäåîôÅ📄";
const MOJIBAKE = [...new Set(USED_NON_ASCII)].map(mojibakeOf).filter(Boolean);
const textExt = [".md", ".cs", ".json", ".js", ".mjs", ".ps1"];
const skipDir = new Set([".git", "node_modules"]);

function walkAll(dir, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (skipDir.has(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walkAll(full, results);
    else if (textExt.includes(path.extname(entry.name)) && entry.name !== "package-lock.json") {
      results.push(full);
    }
  }
  return results;
}

const textFiles = walkAll(brainRoot);
for (const f of textFiles) {
  const lines = fs.readFileSync(f, "utf8").split(/\r?\n/);
  lines.forEach((line, i) => {
    for (const seq of MOJIBAKE) {
      if (line.includes(seq)) {
        issues.push(
          `MOJIBAKE in ${path.relative(brainRoot, f)}:${i + 1}: found '${seq}' — a UTF-8 character double-encoded by an ANSI read-modify-write; restore the real character (see CLAUDE.md)`,
        );
        break;
      }
    }
  });
}
console.log(`Checked ${textFiles.length} text file(s) for double-encoded characters.`);

// === Result ===
console.log("\n=== Result ===");
if (issues.length === 0) {
  console.log("All checks passed - no drift found.");
  process.exit(0);
} else {
  console.log(`${issues.length} issue(s) found:`);
  for (const issue of issues) console.log(`  - ${issue}`);
  process.exit(1);
}
