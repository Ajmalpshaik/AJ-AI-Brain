#!/usr/bin/env node
// Portable (cross-platform, no PowerShell needed) equivalent of verify-consistency.ps1 — for sessions
// running on Linux/macOS/Claude Code on the web, where `pwsh`/Windows PowerShell isn't available.
//
// Same eight checks, same job: catch drift in this Brain's cross-references before it goes stale.
// Kept in sync by hand with verify-consistency.ps1 — if you change what one checks, change both.
//
// Usage: node tools/verify-consistency.mjs

import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
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
const topLevelBuckets = ["filters", "actions", "recipes", "creators", "commands", "examples", "context", "lib"];

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
    /(\d+) real C# fragments in `scripts\/` \((\d+) filters, (\d+) actions, (\d+) creators, (\d+) commands, (\d+) recipes, (\d+) examples, (\d+) read-only `context\/` fragments, (\d+) shared/;
  const m = flat.match(countRe);
  if (!m) {
    issues.push(
      "AGENT-SPEC COUNT SENTENCE NOT FOUND: §3.5's fragment-count sentence was reworded — re-anchor this check in tools/verify-consistency.* or the counts go unchecked",
    );
  } else {
    const claimed = {
      total: Number(m[1]), filters: Number(m[2]), actions: Number(m[3]), creators: Number(m[4]),
      commands: Number(m[5]), recipes: Number(m[6]), examples: Number(m[7]), context: Number(m[8]),
      lib: Number(m[9]),
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

// ASK GIT WHICH FILES ARE OURS, rather than walking the whole folder.
//
// This check runs on EVERY Edit and Write via the PostToolUse hook, so its cost is paid dozens of
// times in a working session, not once. Walking everything meant reading 2,456 files and 12.2 MB
// each time - 2.5 s of the checker's 3.7 s - and most of it was the Python venv, the pip cache and
// the downloaded embedding model. Those are third-party or machine-generated: our editing cannot
// corrupt them, and if it could, this checker is not what would tell us.
//
// `--cached --others --exclude-standard` is tracked files PLUS new ones not yet added, minus
// anything gitignored. That matters: a brand-new fragment is exactly the file most likely to carry
// a fresh encoding fault, and `--cached` alone would have skipped it until it was committed.
//
// Measured 2026-08-21: 2,456 files / 12.2 MB / ~2,530 ms  ->  426 files / 3.2 MB / ~424 ms.
// Falls back to the full walk if git is unavailable or this is not a repository, so a checkout
// without git still gets checked - slowly, but checked.
function ourTextFiles() {
  try {
    const out = execSync("git ls-files --cached --others --exclude-standard", {
      cwd: brainRoot, encoding: "utf8", maxBuffer: 1e8, stdio: ["ignore", "pipe", "ignore"],
    });
    const files = out.split("\n")
      .filter((f) => f && textExt.includes(path.extname(f)) && path.basename(f) !== "package-lock.json")
      .map((f) => path.join(brainRoot, f));
    if (files.length) return files;
  } catch {
    // no git, or not a repository - fall through
  }
  return walkAll(brainRoot);
}

const textFiles = ourTextFiles();
for (const f of textFiles) {
  // Listing and reading are separate moments, so a file can legitimately vanish in between and this
  // whole checker would die on it — which is exactly what happened on 2026-08-11, when the voice
  // queue still lived in the repo and its files were deleted milliseconds after being written.
  // A file that is gone is nothing to report: by definition it is not part of the Brain any more.
  let lines;
  try {
    lines = fs.readFileSync(f, "utf8").split(/\r?\n/);
  } catch (error) {
    if (error.code === "ENOENT") continue;
    throw error;
  }
  lines.forEach((line, i) => {
    for (const seq of MOJIBAKE) {
      if (line.includes(seq)) {
        issues.push(
          `MOJIBAKE in ${path.relative(brainRoot, f)}:${i + 1}: found '${seq}' — a UTF-8 character double-encoded by an ANSI read-modify-write; restore the real character (see CLAUDE.md)`,
        );
        break;
      }
    }
    // Control characters, which are almost always a Windows path eaten by a string escape:
    // "AJTools\bridges" written through a language that reads \b becomes AJTools<0x08>ridges, and
    // "tools\verify-x.ps1" becomes tools<0x0b>erify-x.ps1. It is invisible in most editors, and the
    // damage is real: the SessionStart banner printed the bridges path with the "b" missing for days,
    // and six files across the two repos carried one before the 2026-08-20 sweep found them. Tab is
    // allowed (it is legitimate in markdown tables and code); \r is already stripped by the split.
    let ctrlCode = -1;
    for (let c = 0; c < line.length; c++) {
      const code = line.charCodeAt(c);
      // tab (9) is legitimate; newline/CR are already gone via the split
      if (code < 9 || (code > 10 && code < 32)) { ctrlCode = code; break; }
    }
    if (ctrlCode !== -1) {
      const hex = `0x${ctrlCode.toString(16).padStart(2, "0")}`;
      issues.push(
        `CONTROL CHAR in ${path.relative(brainRoot, f)}:${i + 1}: found ${hex} — almost certainly a backslash path escape that got interpreted (\b, \v, \0); write the literal backslash instead`,
      );
    }
  });
}
console.log(`Checked ${textFiles.length} text file(s) for double-encoded and control characters.`);

// === 7. Cross-references inside script fragments ===
// Fragment headers point at the knowledge file that explains them ("// SOURCE: ../../knowledge/..."),
// which is how an agent gets from a piece of code to the reasoning behind it. These are plain C#
// comments, NOT markdown links, so check 2 never saw them - and 19 of them were silently broken, every
// recipe/command/context/creator fragment using ../knowledge/ where its depth needed ../../knowledge/.
// An agent following one landed on nothing.
console.log("\n=== 7. Cross-references inside script fragments ===");
let fragRefCount = 0;
for (const f of walk(scriptsDir, (n) => n.endsWith(".cs"))) {
  const content = fs.readFileSync(f, "utf8");
  for (const m of content.matchAll(/\.\.[./]*[\w./-]*\.md/g)) {
    fragRefCount++;
    const resolved = path.resolve(path.dirname(f), m[0]);
    if (!fs.existsSync(resolved)) {
      const rel = path.relative(brainRoot, f).split(path.sep).join("/");
      issues.push(`BROKEN FRAGMENT REF in ${rel}: '${m[0]}' does not resolve (check the ../ depth for this file's folder)`);
    }
  }
}
console.log(`Checked ${fragRefCount} cross-reference(s) inside script fragments.`);

// === 8. Semantic index coverage claims ===
// CLAUDE.md, START-HERE.md and README.md each state how many files ask-brain-hybrid searches
// ("searches all N files"). That number drifted 306-vs-309 for two days before anyone noticed —
// three knowledge notes were added and no doc moved. The rules below mirror what the indexer
// actually includes (semantic-index/brain_common.py: INDEX_TARGETS + ROOT_DOCS + FILE_EXTENSIONS);
// if you change the covered set there, change this count AND the three docs in the same turn.
console.log("\n=== 8. Semantic index coverage claims ===");
let indexable = 0;
for (const folder of ["scripts", "knowledge", "skills"]) {
  indexable += walk(path.join(brainRoot, folder), (n) => n.endsWith(".md") || n.endsWith(".cs")).length;
}
for (const rel of ["AGENT-SPEC.md", "START-HERE.md", "README.md", "SETUP.md", "CLAUDE.md",
                   path.join("mcp-server", "tools", "README.md")]) {
  if (fs.existsSync(path.join(brainRoot, rel))) indexable++;
}
let claimCount = 0;
for (const file of ["CLAUDE.md", "START-HERE.md", "README.md"]) {
  const p = path.join(brainRoot, file);
  if (!fs.existsSync(p)) continue;
  const content = fs.readFileSync(p, "utf8");
  for (const m of content.matchAll(/searches all (\d+) files/g)) {
    claimCount++;
    if (Number(m[1]) !== indexable) {
      issues.push(`INDEX COUNT DRIFT in ${file}: says "${m[0]}" but the indexable set on disk holds ${indexable} files`);
    }
  }
}
console.log(`Checked ${claimCount} "searches all N files" claim(s) against ${indexable} indexable file(s) on disk.`);

// === 9. Live counts stated anywhere in markdown ===
// Check 5 above guards ONE sentence in ONE file. Everything else went unchecked, and on 2026-08-22 an
// audit found nine wrong live claims across CLAUDE.md, START-HERE.md, README.md, AGENT-SPEC.md,
// knowledge/INDEX.md, semantic-index/README.md, job-log/README.md and two agent definitions - fragment
// counts frozen at 267/269/282/285 while disk held 290, and native tools at 17/19 against 20. CLAUDE.md
// itself opens by warning about exactly this failure and was carrying it on line 79.
//
// A number is treated as a LIVE CLAIM unless one of these is true, which is also how you fix a false
// positive - and both fixes make the prose better rather than papering over it:
//   * the line carries a yyyy-mm-dd date, so it is a stamped historical measurement;
//   * the line carries an explicit <!-- count-history --> marker;
//   * the file is an append-only dated record (brain-log, a log.md, score-history, a job-log snapshot).
// A heading's date does NOT cover the lines under it: nothing downstream can see the heading, so a
// historical figure has to say so on its own line.
console.log("\n=== 9. Live counts stated anywhere in markdown ===");
const liveCounts = {
  fragments: walk(scriptsDir, (n) => n.endsWith(".cs")).length,
  skills: fs.existsSync(skillsDir)
    ? fs.readdirSync(skillsDir, { withFileTypes: true }).filter((e) => e.isDirectory()).length
    : 0,
  "native tools": walk(path.join(brainRoot, "mcp-server", "tools"), (n) => n.endsWith(".js")).length,
};
// Only phrasings that assert a WHOLE-LIBRARY total. A bare "93 fragments" is usually a subset
// ("93 fragments use DisplayUnitType") and must not be flagged, so every pattern needs a totalising
// article or an explicit scope.
const countPatterns = [
  [/\ball (\d+) (?:C# )?fragments\b/gi, "fragments"],
  [/\bthe (\d+) (?:C# )?fragments\b/gi, "fragments"],
  [/\b(\d+) fragments in `scripts\//gi, "fragments"],
  [/\b(?:all|the|its|holds|has|carries|only) (\d+) skills\b/gi, "skills"],
  [/\b(\d+) skills ·/gi, "skills"],
  [/\b(\d+) native tools\b/gi, "native tools"],
  [/\b(\d+) tool files\b/gi, "native tools"],
];
const datedRecord = /(brain-log\.md|score-history\.md|\/log\.md|job-log\/snapshots\/|HANDOVER\.md)$/;
let countClaims = 0;
for (const file of walkAll(brainRoot).filter((f) => f.endsWith(".md"))) {
  const rel = path.relative(brainRoot, file).split(path.sep).join("/");
  if (datedRecord.test(rel)) continue;
  const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (/20\d\d-\d\d-\d\d/.test(line)) continue;
    if (/<!-- count-history -->/.test(line)) continue;
    for (const [re, key] of countPatterns) {
      re.lastIndex = 0;
      let m;
      while ((m = re.exec(line)) !== null) {
        countClaims++;
        if (Number(m[1]) !== liveCounts[key]) {
          issues.push(
            `LIVE COUNT DRIFT in ${rel}:${i + 1}: says "${m[0]}" but disk has ${liveCounts[key]} ${key}` +
              ` - update it, or if the figure is historical give the line a date or a <!-- count-history --> marker`,
          );
        }
      }
    }
  }
}
console.log(
  `Checked ${countClaims} live count claim(s) against disk ` +
    `(${liveCounts.fragments} fragments, ${liveCounts.skills} skills, ${liveCounts["native tools"]} native tools).`,
);

// === 10. Fragment header status vs its scripts/README.md row ===
// scripts/README.md is the verification record - brain-status.mjs counts from it, and it carries the
// evidence ("length 4000 mm, LevelId 311"). But a modeller opening a fragment reads the HEADER, and on
// 2026-08-22 nineteen headers still said "NOT YET LIVE-VERIFIED" for fragments proven on 2026-08-06/07:
// the big verification campaign updated the README row and never went back to the file. The log had
// already recorded the mirror image of this bug on 2026-08-07 (a stale "NOT yet live-verified" clause
// left INSIDE a README row hid four fragments from the count) - it was fixed in the README and the other
// half was missed. Two records of the same fact drift apart unless something compares them.
//
// Only the README-says-verified / header-says-never-ran direction is checked. The reverse (a header
// overclaiming) cannot be caught by keyword: fragments legitimately write "proven live in this Brain"
// about a TECHNIQUE inside a GOTCHA block, which is true and not a status claim.
console.log("\n=== 10. Fragment header status vs README row ===");
const readmeRows = new Map();
for (const line of fs.readFileSync(path.join(scriptsDir, "README.md"), "utf8").split(/\r?\n/)) {
  if (!line.startsWith("| [`")) continue;
  const m = line.match(/\]\(([^)]+\.cs)\)/);
  if (m) readmeRows.set(path.normalize(path.join("scripts", m[1])), line);
}
// A status DECLARATION opens a comment line. Text quoted inside a later correction note, and prose in a
// GOTCHA continuation, must not count - that is why this anchors to the start of the line and skips the
// correction wording explicitly.
const neverRan = /^\/\/\s*(?:STATUS:\s*)?(?:BLOCKED[^—-]*[—-]\s*)?NOT ?(?:YET )?LIVE-VERIFIED\b/i;
let statusChecked = 0;
for (const f of walk(scriptsDir, (n) => n.endsWith(".cs"))) {
  const rel = path.relative(brainRoot, f);
  const row = readmeRows.get(path.normalize(rel));
  if (!row) continue;
  const readmeVerified =
    /verified(?:\s+\w+)? 2026-\d\d-\d\d/.test(row) && !/NOT yet live-verified/.test(row);
  if (!readmeVerified) continue;
  statusChecked++;
  const head = fs.readFileSync(f, "utf8").split(/\r?\n/).slice(0, 45);
  for (let i = 0; i < head.length; i++) {
    // The anchor above is what excludes quoted text: a correction note's continuation line starts with a
    // date, not with the keyword, so it cannot match. An earlier version ALSO skipped any line containing
    // "Header corrected", which silently swallowed the declaration line itself - the check then passed a
    // deliberately broken header during its own test. A skip broad enough to hide the bug is worse than no
    // skip, so the anchor does the work alone now.
    if (/still read "/.test(head[i])) continue;
    if (neverRan.test(head[i])) {
      issues.push(
        `FRAGMENT STATUS DRIFT in ${rel.split(path.sep).join("/")}:${i + 1}: the header still says it has` +
          ` never run, but its scripts/README.md row records a live verification - update the header, or` +
          ` if only one code path is proven say which (see the PARTLY VERIFIED headers for the wording)`,
      );
      break;
    }
  }
}
console.log(`Checked ${statusChecked} README-verified fragment(s) against their own header status.`);

// === 11. Outside sources named in fragments and skills ===
// Ajmal's instruction, 2026-08-20: "do not mention any thing that we took from this web site or repo...
// the words also remove". Everything of that kind was stripped that day - from the docs. The strip never
// reached scripts/, and on 2026-08-22 seven fragment headers still carried a "(Dynamo-package
// equivalent: X's Y nodes.)" line: pure attribution, no technical content, in the PURPOSE block a
// modeller reads first. Same shape of miss as the verification campaign that updated README rows and not
// the fragment headers - a sweep over documentation does not reach code.
//
// Now covers knowledge/ too. It was scoped to scripts/ and skills/ only while one file's fate was open -
// knowledge/dynamo-vocabulary-map.md, which mapped community package names to fragments. Ajmal settled it
// on 2026-08-22: "the dynamo files no need becose we dont have anyting related to dynamo". Deleted, so
// the exception is gone and knowledge notes are scanned like everything else.
//
// Two things stay excluded, each for a stated reason rather than convenience:
//   * knowledge/brain-log.md - dated, append-only history. Entries from July record a package comparison
//     that really happened; rewriting them would make the log lie about the Brain's own past.
//   * the "Sources consulted" section of knowledge/nfpa13-sprinkler-spacing.md - Ajmal's decision the
//     same day: "keep the nfpa links". Fire-code values whose own heading warns they are secondary
//     summaries; removing the sources would leave unverified life-safety numbers looking authoritative.
console.log("\n=== 11. Outside sources in fragments, skills and knowledge ===");
const outsideNames = /\b(rhythm|clockwork|bimorph|genius ?loci|archi-?lab|mepover|data-?shapes|spring nodes|orchid|dynamo)\b/i;
const sourceExempt = /(brain-log\.md|nfpa13-sprinkler-spacing\.md)$/;
let sourceScanned = 0;
for (const dir of ["scripts", "skills", "knowledge"]) {
  for (const f of walk(path.join(brainRoot, dir), (n) => n.endsWith(".cs") || n.endsWith(".md"))) {
    const rel = path.relative(brainRoot, f).split(path.sep).join("/");
    if (sourceExempt.test(rel)) continue;
    sourceScanned++;
    const lines = fs.readFileSync(f, "utf8").split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      const m = lines[i].match(outsideNames);
      if (m) {
        issues.push(
          `OUTSIDE SOURCE NAMED in ${rel}:${i + 1}: "${m[0]}" - Ajmal's 2026-08-20 rule is that no other` +
            ` people's repos, tools, products or names appear in this Brain. If the technique is worth` +
            ` having, state it in this Brain's own words; do not record where it came from`,
        );
      }
    }
  }
}
console.log(`Scanned ${sourceScanned} fragment/skill/knowledge file(s) for outside-source names.`);

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
