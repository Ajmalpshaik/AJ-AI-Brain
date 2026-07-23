#!/usr/bin/env node
// Portable (cross-platform, no PowerShell needed) equivalent of verify-consistency.ps1 — for sessions
// running on Linux/macOS/Claude Code on the web, where `pwsh`/Windows PowerShell isn't available.
//
// Same three checks, same job: catch drift in this Brain's cross-references before it goes stale.
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
for (const f of ["START-HERE.md", "SETUP.md", "AGENT-SPEC.md"]) {
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
